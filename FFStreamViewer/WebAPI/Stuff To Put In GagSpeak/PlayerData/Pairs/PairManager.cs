using Dalamud.Interface.Internal.Notifications;
using FFStreamViewer.WebAPI.GagspeakConfiguration;
using FFStreamViewer.WebAPI.PlayerData.Factories;
using FFStreamViewer.WebAPI.Services.Events;
using FFStreamViewer.WebAPI.Services.Mediator;
using Gagspeak.API.Data;
using Gagspeak.API.Data.Comparer;
using Gagspeak.API.Dto.User;

namespace FFStreamViewer.WebAPI.PlayerData.Pairs;

/// <summary>
/// General note to self, pairs used to have "own permissions" and "other permissions" but they were removed.
/// <para> If down the line something like this is an answer to a problem of mine, then find a way to utilize it.</para>
/// </summary>
public sealed class PairManager : DisposableMediatorSubscriberBase
{
    ILogger<PairManager> _logger;
    private readonly ConcurrentDictionary<UserData, Pair> _allClientPairs;  // all client-pair'ed users on the client.
    private readonly GagspeakConfigService _configurationService;           // the Gagspeak Configuration Service
    private readonly PairFactory _pairFactory;                              // the pair factory
    private Lazy<List<Pair>> _directPairsInternal;                          // the internal direct pairs.
    public List<Pair> DirectPairs => _directPairsInternal.Value;            // the direct pairs the client has with other users.
    public Pair? LastAddedUser { get; internal set; }                       // the most recently added user.

    public ConcurrentDictionary<UserData, Pair> ClientPairs => _allClientPairs; // the client's pair list

    public PairManager(ILogger<PairManager> logger, PairFactory pairFactory,
        GagspeakConfigService configurationService, GagspeakMediator mediator) : base(logger, mediator)
    {
        _logger = logger;
        _allClientPairs = new(UserDataComparer.Instance);
        _pairFactory = pairFactory;
        _configurationService = configurationService;
        // subscribe to the disconnected message, and clear all pairs when it is received.
        Mediator.Subscribe<DisconnectedMessage>(this, (_) => ClearPairs());
        // subscribe to the cutscene end message, and reapply the pair data when it is received.
        Mediator.Subscribe<CutsceneEndMessage>(this, (_) => ReapplyPairData());
        _directPairsInternal = DirectPairsLazy();
    }

    /// <summary> Used to add a user pair to the client's pair list.
    /// <para> Should only be called by the apicontrollers `LoadIninitialPairs` function on startup.</para>
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
            _allClientPairs[dto.User].ApplyLastReceivedData();
        }
        _logger.LogTrace("Recreating the lazy list of direct pairs.");
        // recreate the lazy list of direct pairs.
        RecreateLazy();
    }

    /// <summary> Is called by the function callbacks from the signalR server.
    /// <para> 
    /// When you request to the server to add another user to your client pairs, 
    /// the server will send back a call once the pair is added. This call then calls this function.
    /// When this function is ran, that user will be appended to your client pairs.
    /// </para> 
    /// <para> This should only ever be called upon by the signalR server callback. </para>
    /// </summary>
    public void AddUserPair(UserPairDto dto, bool addToLastAddedUser = true)
    {
        // if we are recieving the userpair Dto for someone not yet in our client pair list, add them to the list.
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
        _allClientPairs[dto.User].ApplyLastReceivedData();
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
    public List<Pair> GetOnlineUserPairs() => _allClientPairs.Where(p => !string.IsNullOrEmpty(p.Value.GetPlayerNameHash())).Select(p => p.Value).ToList();

    /// <summary> fetches the total number of online users that are also visible to the client.</summary>
    public int GetVisibleUserCount() => _allClientPairs.Count(p => p.Value.IsVisible);

    /// <summary> Fetches the list of userData UIDS for the pairs that are currently visible to the client.</summary>
    public List<UserData> GetVisibleUsers() => _allClientPairs.Where(p => p.Value.IsVisible).Select(p => p.Key).ToList();

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

    /// <summary> Function called upon by the ApiController.Callbacks, which listens to function calls from the connected server.
    /// <para> This sends the client an OnlineUserIdentDto, meaning they were in the clients pairlist</para>
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
            RecreateLazy();
            return;
        }

        // if send notification is on, then we should send the online notification to the client.
        if (sendNotif && _configurationService.Current.ShowOnlineNotifications
            && (_configurationService.Current.ShowOnlineNotificationsOnlyForNamedPairs && !string.IsNullOrEmpty(pair.GetNickname())
            || !_configurationService.Current.ShowOnlineNotificationsOnlyForNamedPairs))
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

        // recreate the lazy list of direct pairs.
        RecreateLazy();
    }

    /// <summary> Method is called upon by the ApiController.Callbacks, which listens to function calls from the connected server.
    /// <para> This method delivers an OnlineUserCharaData Dto to our client, so we can get the latest information from them.</para>
    /// </summary>
    public void ReceiveCharaData(OnlineUserCharaDataDto dto)
    {
        // if the user in the Dto is not in our client's pair list, throw an exception.
        if (!_allClientPairs.TryGetValue(dto.User, out var pair)) throw new InvalidOperationException("No user found for " + dto.User);

        // if they are found, publish an event message that we have received character data from our paired User
        Mediator.Publish(new EventMessage(new Event(pair.UserData, nameof(PairManager), EventSeverity.Informational, "Received Character Data")));

        // apply the data to the pair in the client's pair list.
        _allClientPairs[dto.User].ApplyData(dto);
    }

    /// <summary> Removes a user pair from the client's pair list.</summary>
    public void RemoveUserPair(UserDto dto)
    {
        // try and get the value from the client's pair list
        if (_allClientPairs.TryGetValue(dto.User, out var pair))
        {
            // set the pair's individual pair status (your status for them) to none.
            pair.UserPair.IndividualPairStatus = Gagspeak.API.Data.Enum.IndividualPairStatus.None;

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

    /// <summary> Called upon by the ApiControllers server callback functions.
    /// <para> Method presents to the user an updated individual pair status dto for a clientpair user.</para>
    /// </summary>
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
        .Where(k => k.IndividualPairStatus != Gagspeak.API.Data.Enum.IndividualPairStatus.None).ToList());

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
        // for each pair in the clients pairlist, apply the last received data
        foreach (var pair in _allClientPairs.Select(k => k.Value))
        {
            pair.ApplyLastReceivedData(forced: true);
        }
    }

    /// <summary> Recreates the lazy list of direct pairs.</summary>
    private void RecreateLazy()
    {
        // recreate the direct pairs lazy list
        _directPairsInternal = DirectPairsLazy();
        // publish a message to refresh the UI
        Mediator.Publish(new RefreshUiMessage());
    }
}
