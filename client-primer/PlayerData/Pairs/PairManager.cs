using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Plugin.Services;
using GagSpeak.GagspeakConfiguration;
using GagSpeak.PlayerData.Factories;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Events;
using GagSpeak.Services.Mediator;
using GagspeakAPI.Data;
using GagspeakAPI.Data.Character;
using GagspeakAPI.Data.Comparer;
using GagspeakAPI.Data.Permissions;
using GagspeakAPI.Dto.Connection;
using GagspeakAPI.Dto.User;
using GagspeakAPI.Dto.UserPair;
using Dalamud.Game.Gui.ContextMenu;
using GagspeakAPI.Enums;

namespace GagSpeak.PlayerData.Pairs;

/// <summary>
/// General note to self, pairs used to have "own permissions" and "other permissions" but they were removed.
/// <para> If down the line something like this is an answer to a problem of mine, then find a way to utilize it.</para>
/// </summary>
public sealed partial class PairManager : DisposableMediatorSubscriberBase
{
    private readonly ConcurrentDictionary<UserData, Pair> _allClientPairs;  // concurrent dictionary of all paired paired to the client.
    private readonly GagspeakConfigService _mainConfig;                     // main gagspeak config
    private readonly PairFactory _pairFactory;                              // the pair factory for creating new pair objects
    private readonly IContextMenu _contextMenu;                             // adds GagSpeak options when right clicking players.
    
    private Lazy<List<Pair>> _directPairsInternal;                          // the internal direct pairs lazy list for optimization
    
    public List<Pair> DirectPairs => _directPairsInternal.Value;            // the direct pairs the client has with other users.
    public Pair? LastAddedUser { get; internal set; }                       // the user pair most recently added to the pair list.

    public PairManager(ILogger<PairManager> logger, GagspeakMediator mediator,
        PairFactory pairFactory, GagspeakConfigService mainConfig, 
        IContextMenu contextMenu) : base(logger, mediator)
    {
        _allClientPairs = new(UserDataComparer.Instance);
        _pairFactory = pairFactory;
        _mainConfig = mainConfig;
        _contextMenu = contextMenu;

        Mediator.Subscribe<DisconnectedMessage>(this, (_) => ClearPairs());
        Mediator.Subscribe<CutsceneEndMessage>(this, (_) => ReapplyPairData());

        _directPairsInternal = DirectPairsLazy();

        _contextMenu.OnMenuOpened += OnOpenPairContextMenu;
    }

    private void OnOpenPairContextMenu(IMenuOpenedArgs args)
    {
        // make sure its a player context menu
        Logger.LogInformation("Opening Pair Context Menu of type "+args.MenuType, LoggerType.ContextDtr);

        if (args.MenuType == ContextMenuType.Inventory) return;
        
        // don't open if we don't want to show context menus
        if (!_mainConfig.Current.ContextMenusShow) return;

        // otherwise, locate the pair and add the context menu args to the visible pairs.
        foreach (var pair in _allClientPairs.Where((p => p.Value.IsVisible)))
        {
            pair.Value.AddContextMenu(args);
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _contextMenu.OnMenuOpened -= OnOpenPairContextMenu;
        // dispose of the pairs
        DisposePairs();
    }


    /// <summary>
    /// Should only be called by the api controllers `LoadInitialPairs` function on startup.
    /// <para>
    /// Will pass in the necessary information to create a new pair, 
    /// including their global permissions and permissions for the client pair.
    /// </para>
    /// </summary>
    public void AddUserPair(UserPairDto dto)
    {
        Logger.LogTrace("Scanning all client pairs to see if added user already exists", LoggerType.PairManagement);
        // if the user is not in the client's pair list, create a new pair for them.
        if (!_allClientPairs.ContainsKey(dto.User))
        {
            Logger.LogDebug("User "+dto.User.UID+" not found in client pairs, creating new pair", LoggerType.PairManagement);
            // create a new pair object for the user through the pair factory
            _allClientPairs[dto.User] = _pairFactory.Create(dto);
        }
        // if the user is in the client's pair list, apply the last received data to the pair.
        else
        {
            Logger.LogDebug("User " + dto.User.UID + " found in client pairs, applying last received data instead.", LoggerType.PairManagement);
            // apply the last received data to the pair.
            _allClientPairs[dto.User].UserPair.IndividualPairStatus = dto.IndividualPairStatus;
            _allClientPairs[dto.User].ApplyLastReceivedIpcData();
        }
        Logger.LogTrace("Recreating the lazy list of direct pairs.", LoggerType.PairManagement);
        // recreate the lazy list of direct pairs.
        RecreateLazy();
    }

    /// <summary> 
    /// This should only ever be called upon by the signalR server callback.
    /// <para> 
    /// When you request to the server to add another user to your client pairs, 
    /// the server will send back a call once the pair is added. This call then calls this function.
    /// When this function is ran, that user will be appended to your client pairs.
    /// </para> 
    /// </summary>
    public void AddUserPair(UserPairDto dto, bool addToLastAddedUser = true)
    {
        // if we are receiving the userpair Dto for someone not yet in our client pair list, add them to the list.
        if (!_allClientPairs.ContainsKey(dto.User))
        {
            // to add them, use our pair factory to generate a new pair object for them.
            _allClientPairs[dto.User] = _pairFactory.Create(dto);
        }
        // Otherwise, the user sending us this pair Dto is already in our client pair list, so we can set addToLastAddedUser to false.
        else
        {
            addToLastAddedUser = false;
        }

        // lets apply / update the individualPairStatus of the userpair object for this Dto in our client pair list.
        _allClientPairs[dto.User].UserPair.IndividualPairStatus = dto.IndividualPairStatus;
        // if we should add the content to the last added user, then set the last added user to the user.
        if (addToLastAddedUser)
        {
            LastAddedUser = _allClientPairs[dto.User];
        }
        // finally, be sure to apply the last recieved data to this user's Pair object.
        _allClientPairs[dto.User].ApplyLastReceivedIpcData();
        // recreate the lazy list of direct pairs.
        RecreateLazy();
    }

    /// <summary> Clears all pairs from the client's pair list.</summary>
    public void ClearPairs()
    {
        Logger.LogDebug("Clearing all Pairs", LoggerType.PairManagement);
        // dispose of all our pairs
        DisposePairs();
        // clear the client's pair list
        _allClientPairs.Clear();
        // recreate the lazy list of direct pairs
        RecreateLazy();
    }

    /// <summary> Fetches the filtered list of user pair objects where only users that are currently online are returned.</summary>
    public List<Pair> GetOnlineUserPairs()
        => _allClientPairs.Where(p => !string.IsNullOrEmpty(p.Value.GetPlayerNameHash())).Select(p => p.Value).ToList();

    /// <summary> Fetches all online userPairs, but returns the key instead of value like above.</summary>
    public List<UserData> GetOnlineUserDatas()
        => _allClientPairs.Where(p => !string.IsNullOrEmpty(p.Value.GetPlayerNameHash())).Select(p => p.Key).ToList();

    /// <summary> fetches the total number of online users that are also visible to the client.</summary>
    public int GetVisibleUserCount() => _allClientPairs.Count(p => p.Value.IsVisible);

    /// <summary> Fetches the list of userData UIDS for the pairs that are currently visible to the client.</summary>
    public List<UserData> GetVisibleUsers() => _allClientPairs.Where(p => p.Value.IsVisible).Select(p => p.Key).ToList();

    /// <summary> Gets all pairs where IsVisible is true and returns their game objects in a list form, excluding null values. </summary>
    public List<IGameObject> GetVisiblePairGameObjects()
        => _allClientPairs.Select(p => p.Value.VisiblePairGameObject).Where(gameObject => gameObject != null).ToList()!;

    /// <summary> Fetch the list of userData UID's for all pairs who have OnlineToyboxUser to true.</summary>
    public List<Pair> GetOnlineToyboxUsers() => _allClientPairs.Where(p => p.Value.OnlineToyboxUser).Select(p => p.Value).ToList();

    // fetch the list of all online user pairs via their UID's
    public List<string> GetOnlineUserUids() => _allClientPairs.Select(p => p.Key.UID).ToList();

    // Fetch a user's UserData off of their UID
    public UserData? GetUserDataFromUID(string uid) => _allClientPairs.Keys.FirstOrDefault(p => p.UID == uid);

    public (MoodlesGSpeakPairPerms, MoodlesGSpeakPairPerms) GetMoodlePermsForPairByName(string nameWithWorld)
    {
        var pair = _allClientPairs.FirstOrDefault(p => p.Value.PlayerNameWithWorld == nameWithWorld).Value;
        if (pair == null || pair.UserPairOwnUniquePairPerms == null || pair.UserPairUniquePairPerms == null)
        {
            return (new MoodlesGSpeakPairPerms(), new MoodlesGSpeakPairPerms());
        }

        var ownPerms = (
            pair.UserPairOwnUniquePairPerms.AllowPositiveStatusTypes,
            pair.UserPairOwnUniquePairPerms.AllowNegativeStatusTypes,
            pair.UserPairOwnUniquePairPerms.AllowSpecialStatusTypes,
            pair.UserPairOwnUniquePairPerms.PairCanApplyYourMoodlesToYou,
            pair.UserPairOwnUniquePairPerms.PairCanApplyOwnMoodlesToYou,
            pair.UserPairOwnUniquePairPerms.MaxMoodleTime,
            pair.UserPairOwnUniquePairPerms.AllowPermanentMoodles,
            pair.UserPairOwnUniquePairPerms.AllowRemovingMoodles
        );

        var uniquePerms = (
                pair.UserPairUniquePairPerms.AllowPositiveStatusTypes,
                pair.UserPairUniquePairPerms.AllowNegativeStatusTypes,
                pair.UserPairUniquePairPerms.AllowSpecialStatusTypes,
                pair.UserPairUniquePairPerms.PairCanApplyYourMoodlesToYou,
                pair.UserPairUniquePairPerms.PairCanApplyOwnMoodlesToYou,
                pair.UserPairUniquePairPerms.MaxMoodleTime,
                pair.UserPairUniquePairPerms.AllowPermanentMoodles,
                pair.UserPairUniquePairPerms.AllowRemovingMoodles
        );

        return (ownPerms, uniquePerms);
    }

    /// <summary> Marks a user pair as offline.</summary>
    public void MarkPairOffline(UserData user)
    {
        // if the user is in the client's pair list, mark them as offline.
        if (_allClientPairs.TryGetValue(user, out var pair))
        {
            // end a message to clear the profile data of the user
            Mediator.Publish(new ClearProfileDataMessage(pair.UserData));
            // mark the pair as offline
            pair.MarkOffline();
        }
        // recreate the lazy list of direct pairs
        RecreateLazy();
    }

    public void MarkPairToyboxOffline(UserData user)
    {
        if (_allClientPairs.TryGetValue(user, out var pair))
        {
            pair.MarkToyboxOffline();
        }
        RecreateLazy();
    }

    public void MarkPairToyboxOnline(UserData user)
    {
        if (_allClientPairs.TryGetValue(user, out var pair))
        {
            pair.MarkToyboxOnline();
        }
        RecreateLazy();
    }

    /// <summary> 
    /// 
    /// Function called upon by the ApiController.Callbacks, which listens to function calls from the connected server.
    /// 
    /// <para> 
    /// 
    /// This sends the client an OnlineUserIdentDto, meaning they were in the clients pair list
    /// 
    /// </para>
    /// </summary>
    public void MarkPairOnline(OnlineUserIdentDto dto, bool sendNotif = true)
    {
        // if the user is not in the client's pair list, throw an exception.
        if (!_allClientPairs.ContainsKey(dto.User)) throw new InvalidOperationException("No user found for " + dto);

        // publish a message to clear the profile data of the user. (still not sure why we need to do this lol)
        Mediator.Publish(new ClearProfileDataMessage(dto.User));

        // create a pair var and set it to the user in the client's pair list.
        var pair = _allClientPairs[dto.User];
        // if the pair has a cached player, recreate the lazy list.
        if (pair.HasCachedPlayer)
        {
            Logger.LogDebug("Pair "+dto.User.UID+" already has a cached player, recreating the lazy list of direct pairs.", LoggerType.PairManagement);
            RecreateLazy();
            return;
        }

        // if send notification is on, then we should send the online notification to the client.
        if (sendNotif && _mainConfig.Current.ShowOnlineNotifications
            && (_mainConfig.Current.ShowOnlineNotificationsOnlyForNamedPairs && !string.IsNullOrEmpty(pair.GetNickname())
            || !_mainConfig.Current.ShowOnlineNotificationsOnlyForNamedPairs))
        {
            // get the nickname from the pair, if it is not null, set the nickname to the pair's nickname.
            string? nickname = pair.GetNickname();
            // create a message to send to the client.
            var msg = !string.IsNullOrEmpty(nickname)
                ? $"{nickname} ({pair.UserData.AliasOrUID}) is now online"
                : $"{pair.UserData.AliasOrUID} is now online";
            // publish a notification message to the client that a paired user is now online.
            Mediator.Publish(new NotificationMessage("User online", msg, NotificationType.Info, TimeSpan.FromSeconds(5)));
        }

        // create a cached player for the pair using the Dto
        pair.CreateCachedPlayer(dto);

        // push our composite data to them.
        Mediator.Publish(new PairWentOnlineMessage(dto.User));

        // recreate the lazy list of direct pairs.
        RecreateLazy();
    }

    /// <summary> 
    /// Method is called upon by the ApiController.Callbacks, which listens to function calls from the connected server.
    /// It then returns the composite DTO, which is split into its core components and updates the correct user pair.
    /// </summary>
    public void ReceiveCharaCompositeData(OnlineUserCompositeDataDto dto, string clientUID)
    {
        // if the user in the Dto is not in our client's pair list, throw an exception.
        if (!_allClientPairs.TryGetValue(dto.User, out var pair)) throw new InvalidOperationException("No user found for " + dto.User);

        // if they are found, publish an event message that we have received character data from our paired User
        Mediator.Publish(new EventMessage(new Event(pair.UserData, nameof(PairManager), EventSeverity.Informational, "Received Character Composite Data")));

        _allClientPairs[dto.User].ApplyAppearanceData(new OnlineUserCharaAppearanceDataDto(dto.User, dto.CompositeData.AppearanceData, dto.UpdateKind));
        _allClientPairs[dto.User].ApplyWardrobeData(new OnlineUserCharaWardrobeDataDto(dto.User, dto.CompositeData.WardrobeData, dto.UpdateKind));
        _allClientPairs[dto.User].ApplyToyboxData(new OnlineUserCharaToyboxDataDto(dto.User, dto.CompositeData.ToyboxData, dto.UpdateKind));
        _allClientPairs[dto.User].ApplyPiShockPermData(new OnlineUserCharaPiShockPermDto(dto.User, dto.CompositeData.GlobalShockPermissions, DataUpdateKind.PiShockGlobalUpdated));


        // first see if our clientUID exists as a key in dto.CompositeData.AliasData. If it does not, define it as an empty data.
        if (dto.CompositeData.AliasData.ContainsKey(clientUID))
            _allClientPairs[dto.User].ApplyAliasData(new OnlineUserCharaAliasDataDto(dto.User, dto.CompositeData.AliasData[clientUID], dto.UpdateKind));
        else
            _allClientPairs[dto.User].ApplyAliasData(new OnlineUserCharaAliasDataDto(dto.User, new CharacterAliasData(), dto.UpdateKind));

        // pishock perms for pair.
        if (dto.CompositeData.PairShockPermissions.ContainsKey(clientUID))
            _allClientPairs[dto.User].ApplyPiShockPermData(new OnlineUserCharaPiShockPermDto(dto.User, dto.CompositeData.PairShockPermissions[clientUID], DataUpdateKind.PiShockPairPermsForUserUpdated));
        else
            _allClientPairs[dto.User].ApplyPiShockPermData(new OnlineUserCharaPiShockPermDto(dto.User, new PiShockPermissions(), DataUpdateKind.PiShockPairPermsForUserUpdated));
    }

    /// <summary> Method similar to compositeData, but this will only update the IPC data of the user pair. </summary>
    public void ReceiveCharaIpcData(OnlineUserCharaIpcDataDto dto)
    {
        // if the user in the Dto is not in our client's pair list, throw an exception.
        if (!_allClientPairs.TryGetValue(dto.User, out var pair)) throw new InvalidOperationException("No user found for " + dto.User);

        // if they are found, publish an event message that we have received character data from our paired User
        Mediator.Publish(new EventMessage(new Event(pair.UserData, nameof(PairManager), EventSeverity.Informational, "Received Character IPC Data")));

        // apply the IPC data to the pair.
        _allClientPairs[dto.User].ApplyVisibleData(dto);
    }

    /// <summary> Method similar to compositeData, but this will only update the appearance data of the user pair. </summary>
    public void ReceiveCharaAppearanceData(OnlineUserCharaAppearanceDataDto dto)
    {
        // locate the pair that should be updated with the appearance data.
        if (!_allClientPairs.TryGetValue(dto.User, out var pair)) throw new InvalidOperationException("No user found for " + dto.User);

        // publish event if found.
        Mediator.Publish(new EventMessage(new Event(pair.UserData, nameof(PairManager), EventSeverity.Informational, "Received Character Appearance Data")));

        // Apply the update.
        _allClientPairs[dto.User].ApplyAppearanceData(dto);
    }

    /// <summary> Method similar to compositeData, but this will only update the wardrobe data of the user pair. </summary>
    public void ReceiveCharaWardrobeData(OnlineUserCharaWardrobeDataDto dto)
    {
        // if the user in the Dto is not in our client's pair list, throw an exception.
        if (!_allClientPairs.TryGetValue(dto.User, out var pair)) throw new InvalidOperationException("No user found for " + dto.User);

        // if they are found, publish an event message that we have received character data from our paired User
        Mediator.Publish(new EventMessage(new Event(pair.UserData, nameof(PairManager), EventSeverity.Informational, "Received Character Wardrobe Data")));

        // apply the wardrobe data to the pair.
        _allClientPairs[dto.User].ApplyWardrobeData(dto);
    }

    /// <summary> Method similar to compositeData, but this will only update the alias data of the user pair. </summary>
    public void ReceiveCharaAliasData(OnlineUserCharaAliasDataDto dto)
    {
        // if the user in the Dto is not in our client's pair list, throw an exception.
        if (!_allClientPairs.TryGetValue(dto.User, out var pair)) throw new InvalidOperationException("No user found for " + dto.User);

        // if they are found, publish an event message that we have received character data from our paired User
        Mediator.Publish(new EventMessage(new Event(pair.UserData, nameof(PairManager), EventSeverity.Informational, "Received Character Alias Data")));

        // apply the alias data to the pair.
        _allClientPairs[dto.User].ApplyAliasData(dto);
    }

    /// <summary> Method similar to compositeData, but this will only update the pattern data of the user pair. </summary>
    public void ReceiveCharaToyboxData(OnlineUserCharaToyboxDataDto dto)
    {
        // if the user in the Dto is not in our client's pair list, throw an exception.
        if (!_allClientPairs.TryGetValue(dto.User, out var pair)) throw new InvalidOperationException("No user found for " + dto.User);

        // if they are found, publish an event message that we have received character data from our paired User
        Mediator.Publish(new EventMessage(new Event(pair.UserData, nameof(PairManager), EventSeverity.Informational, "Received Character Pattern Data")));

        // apply the pattern data to the pair.
        _allClientPairs[dto.User].ApplyToyboxData(dto);
    }

    /// <summary> Method similar to compositeData, but this will only update the shock permissions of the user pair. </summary>
    public void ReceiveCharaPiShockPermData(OnlineUserCharaPiShockPermDto dto)
    {
        // if the user in the Dto is not in our client's pair list, throw an exception.
        if (!_allClientPairs.TryGetValue(dto.User, out var pair)) throw new InvalidOperationException("No user found for " + dto.User);

        // if they are found, publish an event message that we have received character data from our paired User
        Mediator.Publish(new EventMessage(new Event(pair.UserData, nameof(PairManager), EventSeverity.Informational, "Received Character PiShock Permissions Data")));

        // apply the shock permissions data to the pair.
        _allClientPairs[dto.User].ApplyPiShockPermData(dto);
    }



    /// <summary> Removes a user pair from the client's pair list.</summary>
    public void RemoveUserPair(UserDto dto)
    {
        // try and get the value from the client's pair list
        if (_allClientPairs.TryGetValue(dto.User, out var pair))
        {
            // set the pair's individual pair status (your status for them) to none.
            pair.UserPair.IndividualPairStatus = IndividualPairStatus.None;

            // if the pair has no connections, mark them as offline.
            if (!pair.HasAnyConnection())
            {
                // make them as offline
                pair.MarkOffline();
                // try and remove the pair from the client's pair list.
                _allClientPairs.TryRemove(dto.User, out _);
            }
        }

        // recreate the lazy list of direct pairs.
        RecreateLazy();
    }

    /// <summary> Called upon by the ApiControllers server callback functions to update a pairs individual pair status.</summary>
    internal void UpdateIndividualPairStatus(UserIndividualPairStatusDto dto)
    {
        // if the user is in the client's pair list, update their individual pair status.
        if (_allClientPairs.TryGetValue(dto.User, out var pair))
        {
            // update the individual pair status of the user in the client's pair list.
            pair.UserPair.IndividualPairStatus = dto.IndividualPairStatus;
            // recreate the list of direct pairs.
            RecreateLazy();
        }
    }

    /// <summary> The lazy list of direct pairs, remade from the _allClientPairs</summary>
    private Lazy<List<Pair>> DirectPairsLazy() => new(() => _allClientPairs.Select(k => k.Value)
        .Where(k => k.IndividualPairStatus != IndividualPairStatus.None).ToList());

    /// <summary> Disposes of all the pairs in the client's pair list.</summary>
    private void DisposePairs()
    {
        // log the action about to occur
        Logger.LogDebug("Disposing all Pairs", LoggerType.PairManagement);
        // for each pair in the client's pair list, dispose of them by marking them as offline.
        Parallel.ForEach(_allClientPairs, item =>
        {
            // mark the pair as offline
            item.Value.MarkOffline();
        });

        // recreate the list of direct pairs
        RecreateLazy();
    }

    /// <summary> Reapplies the last received data to all the pairs in the client's pair list.</summary>
    private void ReapplyPairData()
    {
        // for each pair in the clients pair list, apply the last received data
        foreach (var pair in _allClientPairs.Select(k => k.Value))
        {
            pair.ApplyLastReceivedIpcData(forced: true);
        }
    }

    /// <summary> Recreates the lazy list of direct pairs.</summary>
    private void RecreateLazy(bool PushUiRefresh = true)
    {
        // recreate the direct pairs lazy list
        _directPairsInternal = DirectPairsLazy();
        // publish a message to refresh the UI
        if (PushUiRefresh)
        {
            Mediator.Publish(new RefreshUiMessage());
        }
    }
}
