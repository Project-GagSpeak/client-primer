using Dalamud.Interface.ImGuiNotification;
using GagSpeak.GagspeakConfiguration;
using GagSpeak.PlayerData.Factories;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Events;
using GagSpeak.Services.Mediator;
using GagspeakAPI.Data;
using GagspeakAPI.Data.Comparer;
using GagspeakAPI.Dto.User;
using GagspeakAPI.Data.Permissions;
using GagspeakAPI.Dto.Connection;
using GagspeakAPI.Dto.Permissions;
using GagspeakAPI.Dto.UserPair;
using System.Reflection;
using Penumbra.GameData;
using GagspeakAPI.Data.Character;

namespace GagSpeak.PlayerData.Pairs;

/// <summary>
/// General note to self, pairs used to have "own permissions" and "other permissions" but they were removed.
/// <para> If down the line something like this is an answer to a problem of mine, then find a way to utilize it.</para>
/// </summary>
public sealed partial class PairManager : DisposableMediatorSubscriberBase
{
    ILogger<PairManager> _logger;
    private readonly ConcurrentDictionary<UserData, Pair> _allClientPairs;  // concurrent dictionary of all paired paired to the client.
    private readonly ClientConfigurationManager _clientConfigurationManager;// client-end related configs manager           
    private readonly PairFactory _pairFactory;                              // the pair factory for creating new pair objects
    private Lazy<List<Pair>> _directPairsInternal;                          // the internal direct pairs lazy list for optimization
    public List<Pair> DirectPairs => _directPairsInternal.Value;            // the direct pairs the client has with other users.
    public Pair? LastAddedUser { get; internal set; }                       // the user pair most recently added to the pair list.

    public PairManager(ILogger<PairManager> logger, PairFactory pairFactory,
        ClientConfigurationManager configurationService, GagspeakMediator mediator) : base(logger, mediator)
    {
        _logger = logger;
        _allClientPairs = new(UserDataComparer.Instance);
        _pairFactory = pairFactory;
        _clientConfigurationManager = configurationService;

        // subscribe to the disconnected message, and clear all pairs when it is received.
        Mediator.Subscribe<DisconnectedMessage>(this, (_) => ClearPairs());
        
        // subscribe to the cutscene end message, and reapply the pair data when it is received.
        Mediator.Subscribe<CutsceneEndMessage>(this, (_) => ReapplyPairData());
        
        _directPairsInternal = DirectPairsLazy();
    }

    /// <summary>
    /// 
    /// Should only be called by the apicontrollers `LoadIninitialPairs` function on startup.
    /// 
    /// <para>
    /// 
    /// Will pass in the necessary information to create a new pair, 
    /// including their global permissions and permissions for the client pair.
    /// 
    /// </para>
    /// </summary>
    public void AddUserPair(UserPairDto dto)
    {
        _logger.LogTrace("Scanning all client pairs to see if added user already exists");
        // if the user is not in the client's pair list, create a new pair for them.
        if (!_allClientPairs.ContainsKey(dto.User))
        {
            _logger.LogDebug("User {user} not found in client pairs, creating new pair", dto.User);
            // create a new pair object for the user through the pair factory
            _allClientPairs[dto.User] = _pairFactory.Create(dto);
        }
        // if the user is in the client's pair list, apply the last received data to the pair.
        else
        {
            _logger.LogDebug("User {user} found in client pairs, applying last received data instead.", dto.User);
            // apply the last received data to the pair.
            _allClientPairs[dto.User].UserPair.IndividualPairStatus = dto.IndividualPairStatus;
            _allClientPairs[dto.User].ApplyLastReceivedIpcData();
        }
        _logger.LogTrace("Recreating the lazy list of direct pairs.");
        // recreate the lazy list of direct pairs.
        RecreateLazy();
    }

    /// <summary> 
    /// 
    /// This should only ever be called upon by the signalR server callback.
    /// 
    /// <para> 
    /// 
    /// When you request to the server to add another user to your client pairs, 
    /// the server will send back a call once the pair is added. This call then calls this function.
    /// When this function is ran, that user will be appended to your client pairs.
    /// 
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
        Logger.LogDebug("Clearing all Pairs");
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

    /// <summary> Fetch the list of userData UID's for all pairs who have OnlineToyboxUser to true.</summary>
    public List<Pair> GetOnlineToyboxUsers() => _allClientPairs.Where(p => p.Value.OnlineToyboxUser).Select(p => p.Value).ToList();

    // fetch the list of all online user pairs via their UID's
    public List<string> GetOnlineUserUids() => _allClientPairs.Select(p => p.Key.UID).ToList();

    // Fetch a user's UserData off of their UID
    public UserData? GetUserDataFromUID(string uid) => _allClientPairs.Keys.FirstOrDefault(p => p.UID == uid);

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
            Logger.LogDebug("Pair {pair} already has a cached player, recreating the lazy list of direct pairs.", pair.UserData);
            RecreateLazy();
            return;
        }

        // if send notification is on, then we should send the online notification to the client.
        if (sendNotif && _clientConfigurationManager.GagspeakConfig.ShowOnlineNotifications
            && (_clientConfigurationManager.GagspeakConfig.ShowOnlineNotificationsOnlyForNamedPairs && !string.IsNullOrEmpty(pair.GetNickname())
            || !_clientConfigurationManager.GagspeakConfig.ShowOnlineNotificationsOnlyForNamedPairs))
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

        // break down the composite data into its core components, and update the necessary parts.
        _allClientPairs[dto.User].ApplyVisibleData(new OnlineUserCharaIpcDataDto(dto.User, dto.CompositeData.IPCData, dto.UpdateKind));

        // apply the other appearances to the pair.
        _allClientPairs[dto.User].ApplyAppearanceData(new OnlineUserCharaAppearanceDataDto(dto.User, dto.CompositeData.AppearanceData, dto.UpdateKind));

        // apply the wardrobe data to the pair.
        _allClientPairs[dto.User].ApplyWardrobeData(new OnlineUserCharaWardrobeDataDto(dto.User, dto.CompositeData.WardrobeData, dto.UpdateKind));

        // apply the alias data (FOR OUR CLIENTPAIR ONLY) to the aliasData object.

        // first see if our clientUID exists as a key in dto.CompositeData.AliasData. If it does not, define it as an empty data.
        if (!dto.CompositeData.AliasData.ContainsKey(clientUID))
        {
            _allClientPairs[dto.User].ApplyAliasData(new OnlineUserCharaAliasDataDto(dto.User, dto.CompositeData.AliasData[clientUID], dto.UpdateKind));
        }
        else
        {
            // REVIEW: This might cause issues with any potential desync. If it does look into
            _allClientPairs[dto.User].ApplyAliasData(new OnlineUserCharaAliasDataDto(dto.User, new CharacterAliasData(), dto.UpdateKind));
        }

        // apply the pattern data to the pair.
        _allClientPairs[dto.User].ApplyPatternData(new OnlineUserCharaToyboxDataDto(dto.User, dto.CompositeData.ToyboxData, dto.UpdateKind));
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
    public void ReceiveCharaPatternData(OnlineUserCharaToyboxDataDto dto)
    {
        // if the user in the Dto is not in our client's pair list, throw an exception.
        if (!_allClientPairs.TryGetValue(dto.User, out var pair)) throw new InvalidOperationException("No user found for " + dto.User);

        // if they are found, publish an event message that we have received character data from our paired User
        Mediator.Publish(new EventMessage(new Event(pair.UserData, nameof(PairManager), EventSeverity.Informational, "Received Character Pattern Data")));

        // apply the pattern data to the pair.
        _allClientPairs[dto.User].ApplyPatternData(dto);
    }



    /// <summary> Removes a user pair from the client's pair list.</summary>
    public void RemoveUserPair(UserDto dto)
    {
        // try and get the value from the client's pair list
        if (_allClientPairs.TryGetValue(dto.User, out var pair))
        {
            // set the pair's individual pair status (your status for them) to none.
            pair.UserPair.IndividualPairStatus = GagspeakAPI.Data.Enum.IndividualPairStatus.None;

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

    /// <summary> The override disposal method for the pair manager</summary>
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        // dispose of the pairs
        DisposePairs();
    }

    /// <summary> The lazy list of direct pairs, remade from the _allClientPairs</summary>
    private Lazy<List<Pair>> DirectPairsLazy() => new(() => _allClientPairs.Select(k => k.Value)
        .Where(k => k.IndividualPairStatus != GagspeakAPI.Data.Enum.IndividualPairStatus.None).ToList());

    /// <summary> Disposes of all the pairs in the client's pair list.</summary>
    private void DisposePairs()
    {
        // log the action about to occur
        Logger.LogDebug("Disposing all Pairs");
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
