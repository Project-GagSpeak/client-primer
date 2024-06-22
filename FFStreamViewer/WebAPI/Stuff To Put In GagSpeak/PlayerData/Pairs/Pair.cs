using FFStreamViewer.WebAPI.PlayerData.Factories;
using FFStreamViewer.WebAPI.PlayerData.Handlers;
using FFStreamViewer.WebAPI.Services.Mediator;
using FFStreamViewer.WebAPI.Services.ServerConfiguration;
using FFStreamViewer.WebAPI.SignalR.Utils;
using Gagspeak.API.Data;
using Gagspeak.API.Data.CharacterData;
using Gagspeak.API.Data.Enum;
using Gagspeak.API.Dto.User;

namespace FFStreamViewer.WebAPI.PlayerData.Pairs;

/// <summary> Stores information about a paired user of the client.
/// <para> The Pair object is created by the PairFactory, which is responsible for generating pair objects.</para>
/// <para> These pair objects are then created and deleted via the pair manager</para>
/// <para> The pair handler is what helps with the management of the CachedPlayer objects.</para>
/// </summary>
public class Pair
{
    private readonly PairHandlerFactory _cachedPlayerFactory;                       // the factory for making cached players
    private readonly SemaphoreSlim _creationSemaphore = new(1);                     // the semaphore for creation of the cached player
    private readonly ILogger<Pair> _logger;                                         // the logger for the pair class
    private readonly GagspeakMediator _mediator;                                    // GagspeakMediator
    private readonly ServerConfigurationManager _serverConfigurationManager;        // the server configuration manager
    private CancellationTokenSource _applicationCts = new CancellationTokenSource();// the application CTS
    private OnlineUserIdentDto? _onlineUserIdentDto = null;                         // the onlineUserIdentDto of the pair

    /// <summary> The cached player PairHandler object for this pair. (STORES THE CHARACTER DATA FOR THIS PAIRED USER)
    /// <para>No data will be applied to this user pair until its CachedPlayer is created</para>
    /// <para>The ONLY FUNCTION that sets CachedPlayer to anything besides disposed or null is the <c>CreateCachedPlayer</c> Function</para>
    /// </summary>
    private PairHandler? CachedPlayer { get; set; }

    /// <summary> The most recently received character data for this pair.
    /// <para> This is only set in the ApplyData function, which passes in an OnlineUserCharaData Dto</para>
    /// <para> 
    /// It is primarily used for us to indicate if we have data to apply to the user at all, and not the actual storage for it.
    /// </para>
    /// </summary>
    public CharacterData? LastReceivedCharacterData { get; set; }

    /// <summary> The UserPairDto that is send in the creation of this pair object in its constructor.</summary>
    public UserPairDto UserPair { get; set; }


    /// <summary> a reference variable for the pair class, where the UserData is the UserPair's UID/Alias UID
    public UserData UserData => UserPair.User;

    // Basic reference getter attributes
    public bool HasCachedPlayer => CachedPlayer != null && !string.IsNullOrEmpty(CachedPlayer.PlayerName) && _onlineUserIdentDto != null;
    public IndividualPairStatus IndividualPairStatus => UserPair.IndividualPairStatus;  // the individual pair status of the pair in relation to the client.
    public bool IsDirectlyPaired => IndividualPairStatus != IndividualPairStatus.None;  // if the pair is directly paired.
    public bool IsOneSidedPair => IndividualPairStatus == IndividualPairStatus.OneSided; // if the pair is one sided.
    public bool IsOnline => CachedPlayer != null;                                       // lets us know if the paired user is online. 
    public bool IsPaired => IndividualPairStatus == IndividualPairStatus.Bidirectional; // if the user is paired bidirectionally.
    public bool IsVisible => CachedPlayer?.IsVisible ?? false;                          // if the paired user is visible.
    public string? PlayerName => CachedPlayer?.PlayerName ?? string.Empty;  // the name of the player
    // public bool IsPaused => UserPair.OwnPermissions.IsPaused(); (pausing not implemented yet)


    public Pair(ILogger<Pair> logger, UserPairDto userPair,
        PairHandlerFactory cachedPlayerFactory, GagspeakMediator mediator,
        ServerConfigurationManager serverConfigurationManager)
    {
        _logger = logger;
        _cachedPlayerFactory = cachedPlayerFactory;
        _mediator = mediator;
        _serverConfigurationManager = serverConfigurationManager;
        UserPair = userPair;
    }

    /// <summary>
    /// Apply the data retrieved from an updated online user's information.
    /// </summary>
    /// <param name="data"> the data to apply to the user</param>
    public void ApplyData(OnlineUserCharaDataDto data)
    {
        _applicationCts = _applicationCts.CancelRecreate();
        // set the last recieved character data to the data.CharaData
        LastReceivedCharacterData = data.CharaData;

        // if the cachedplayer is null
        if (CachedPlayer == null)
        {
            // log that we received data for the user, but the cached player does not exist, and we are waiting.
            _logger.LogDebug("Received Data for {uid} but CachedPlayer does not exist, waiting", data.User.UID);
            // asynchronously run the following code
            _ = Task.Run(async () =>
            {
                // create a new cancellation token source
                using var timeoutCts = new CancellationTokenSource();
                // auto cancel it after 120 seconds
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(120));
                // create a new cancellation token source for the application token
                var appToken = _applicationCts.Token;
                // create a new linked token source with the timeout token and the application token
                using var combined = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, appToken);

                // while the cached player is still null and the combined token is not cancelled
                while (CachedPlayer == null && !combined.Token.IsCancellationRequested)
                {
                    // wait for 250 milliseconds
                    await Task.Delay(250, combined.Token).ConfigureAwait(false);
                }

                // if the combined token is not cancelled STILL
                if (!combined.IsCancellationRequested)
                {
                    // apply the last received data
                    _logger.LogDebug("Applying delayed data for {uid}", data.User.UID);
                    ApplyLastReceivedData(); // in essence, this means apply the character data send in the Dto
                }
            });
            return;
        }

        // otherwise, just apply the last received data.
        ApplyLastReceivedData();
    }

    /// <summary> Method that applies the last received data to the cached player.
    /// <para> It does this only if the CachedPlayer is not null, and the LastRecievedCharacterData is not null.</para>
    /// </summary>
    /// <param name="forced">if this method was forced or not 
    /// (will remove for now, but you can always add it back in later if people want it i guess)</param>
    public void ApplyLastReceivedData(bool forced = false)
    {
        // if we have not yet recieved data from the player at least once since being online, return and do not apply.
        // ( This implies that the pair object has had its CreateCachedPlayer method called )
        if (CachedPlayer == null) return;
        // if the last received character data is null, return and do not apply.
        if (LastReceivedCharacterData == null) return;

        // we have satisfied the conditions to apply the character data to our paired user, so apply it.
        CachedPlayer.ApplyCharacterData(Guid.NewGuid(), LastReceivedCharacterData);
    }

    /// <summary> Method that creates the cached player (PairHandler) object for the client pair.
    /// <para> This method is ONLY EVER CALLED BY THE PAIR MANAGER under the <c>MarkPairOnline</c> method!</para>
    /// <para> Until the CachedPlayer object is made, the client will not apply any data sent from this paired user.</para>
    /// </summary>
    public void CreateCachedPlayer(OnlineUserIdentDto? dto = null)
    {
        try
        {
            // wait for the creation semaphore to be available
            _creationSemaphore.Wait();

            // If the cachedPlayer is already stored for this pair, we do not need to create it again, so return.
            if (CachedPlayer != null) return;

            // if the Dto sent to us by the server is null, and the pairs onlineUserIdentDto is null, dispose of the cached player and return.
            if (dto == null && _onlineUserIdentDto == null)
            {
                // dispose of the cachedplayer and set it to null before returning
                CachedPlayer?.Dispose();
                CachedPlayer = null;
                return;
            }

            // if the OnlineUserIdentDto contains information, we should update our pairs _onlineUserIdentDto to the dto
            if (dto != null)
            {
                _onlineUserIdentDto = dto;
            }

            // not we can dispose of the cached player
            CachedPlayer?.Dispose();
            // and create a new one from our _cachedPlayerFactory (the pair handler factory)
            CachedPlayer = _cachedPlayerFactory.Create(new OnlineUserIdentDto(UserData, _onlineUserIdentDto!.Ident));
        }
        finally
        {
            // release the creation semaphore
            _creationSemaphore.Release();
        }
    }

    /// <summary> Get the nicknames for the user. (still dont know how this is meant to have any value at all) </summary>
    public string? GetNickname()
    {
        return _serverConfigurationManager.GetNicknameForUid(UserData.UID);
    }

    /// <summary> Get the player name hash. </summary>
    public string GetPlayerNameHash()
    {
        return CachedPlayer?.PlayerNameHash ?? string.Empty;
    }

    /// <summary> A boolean of if the pair has any connection to the client. </summary>
    public bool HasAnyConnection()
    {
        return UserPair.IndividualPairStatus != IndividualPairStatus.None;
    }

    /// <summary> Marks the pair as offline. </summary>
    public void MarkOffline()
    {
        try
        {
            // block current thread until it can enter the semaphore
            _creationSemaphore.Wait();
            // set the online user ident dto to null
            _onlineUserIdentDto = null;
            // set the last received character data to null
            LastReceivedCharacterData = null;
            // set the pair handler player = to the cached player
            var player = CachedPlayer;
            // set the cached player to null
            CachedPlayer = null;
            // dispose of the player object.
            player?.Dispose();
        }
        finally
        {
            // release the creation semaphore
            _creationSemaphore.Release();
        }
    }
}
