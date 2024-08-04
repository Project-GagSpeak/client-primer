/*using GagSpeak.PlayerData.Handlers;
using GagSpeak.Services.Mediator;
using GagspeakAPI.Data;
using GagspeakAPI.Data.Character;
using GagSpeak.UpdateMonitoring;
using GagSpeak.WebAPI;
using GagSpeak.PlayerData.Data;

namespace GagSpeak.PlayerData.Pairs;

/// <summary>
/// The class that manages all of the connected online users on the GagSpeak server.
/// </summary>
public class OnlinePlayerManager : DisposableMediatorSubscriberBase
{
    private readonly ApiController _apiController;
    private readonly OnFrameworkService _frameworkUtil;
    private readonly HashSet<PairHandler> _newVisiblePlayers = [];
    private readonly PairManager _pairManager;
    private CharacterCompositeData? _lastSentData;
    // store our PlayerCharacterManager instance.
    private PlayerCharacterManager _playerManager;

    public OnlinePlayerManager(ILogger<OnlinePlayerManager> logger,
        ApiController apiController, OnFrameworkService dalamudUtil, 
        PairManager pairManager, GagspeakMediator mediator) 
        : base(logger, mediator)
    {
        _apiController = apiController;
        _frameworkUtil = dalamudUtil;
        _pairManager = pairManager;

        // our mediator subscribers.
        Mediator.Subscribe<DelayedFrameworkUpdateMessage>(this, (_) => FrameworkOnUpdate());
        // fired whenever we have a change in our IPC data (or general data??? idk)
        Mediator.Subscribe<CharacterDataCreatedMessage>(this, (msg) =>
        {
            var newData = msg.CharacterData;
            // if the character created is different from our last sent data, then we will push the data.
            if (_lastSentData == null || (!string.Equals(newData, _lastSentData)))
            {
                // update our last send data, and then push our data to the server.
                Logger.LogDebug("Pushing data for visible players");
                _lastSentData = newData;
                PushCharacterIpcData(_pairManager.GetVisibleUsers());
            }
            else
            {
                Logger.LogDebug("Not sending data for {newData}", newData);
            }
        });
        Mediator.Subscribe<PairHandlerVisibleMessage>(this, (msg) => _newVisiblePlayers.Add(msg.Player));
        Mediator.Subscribe<ConnectedMessage>(this, (_) =>
        {
            Logger.LogInformation("Connected to server, pushing IPC data to all visible users");
            PushCharacterIpcData(_pairManager.GetVisibleUsers());
        });
        Mediator.Subscribe<OnlinePairsLoadedMessage>(this, (_) =>
        {
            Logger.LogInformation("Online pairs loaded, pushing Composite data to all online users");
            PushCharacterCompositeData(_pairManager.GetOnlineUserPairs().Select(c => c.UserPair.User).ToList());
        });
    }

    private void FrameworkOnUpdate()
    {
        if (!_frameworkUtil.GetIsPlayerPresent() || !_apiController.IsConnected) return;

        if (!_newVisiblePlayers.Any()) return;
        var newVisiblePlayers = _newVisiblePlayers.ToList();
        _newVisiblePlayers.Clear();
        Logger.LogTrace("Has new visible players, pushing character data");
        // push the character data if there is new visible players to push it to that are not already present.
        PushCharacterIpcData(newVisiblePlayers.Select(c => c.OnlineUser.User).ToList());
    }

    /// <summary> Pushes the character data to the server for the visible players </summary>
    private void PushCharacterCompositeData(List<UserData> onlinePlayers)
    {
        if (onlinePlayers.Any() && _lastSentData != null)
        {
            _ = Task.Run(async () =>
            {
                await _apiController.PushCharacterCompositeData(_lastSentData, onlinePlayers).ConfigureAwait(false);
            });
        }
        else
        {
            Logger.LogWarning("No online players to push composite data to");
        }
    }

    /// <summary> Pushes the character IPC data to the server for the visible players </summary>
    private void PushCharacterIpcData(List<UserData> visiblePlayers)
    {
        if (visiblePlayers.Any() && _lastSentData?.IPCData != null)
        {
            _ = Task.Run(async () =>
            {
                await _apiController.PushCharacterIpcData(_lastSentData.IPCData, visiblePlayers).ConfigureAwait(false);
            });
        }
    }

    /// <summary> Pushes the character appearance data to the server for the visible players </summary>
    private void PushCharacterAppearanceData(List<UserData> onlinePlayers)
    {
        if (onlinePlayers.Any() && _lastSentData?.AppearanceData != null)
        {
            _ = Task.Run(async () =>
            {
                await _apiController.PushCharacterAppearanceData(_lastSentData.AppearanceData, onlinePlayers).ConfigureAwait(false);
            });
        }
    }

    /// <summary> Pushes the character wardrobe data to the server for the visible players </summary>
    private void PushCharacterWardrobeData(List<UserData> onlinePlayers)
    {
        if (onlinePlayers.Any() && _lastSentData?.WardrobeData != null)
        {
            _ = Task.Run(async () =>
            {
                await _apiController.PushCharacterWardrobeData(_lastSentData.WardrobeData, onlinePlayers).ConfigureAwait(false);
            });
        }
    }

    /// <summary> Pushes the character alias list data to the server for the visible players (should ideally only need to do this for one recipient) </summary>
    private void PushCharacterAliasListData(UserData onlinePlayers)
    {
        if (_lastSentData?.AliasData != null)
        {
            _ = Task.Run(async () =>
            {
                await _apiController.PushCharacterAliasListData(_lastSentData.AliasData[onlinePlayers.UID], onlinePlayers).ConfigureAwait(false);
            });
        }
    }

    /// <summary> Pushes the character pattern data to the server for the visible players </summary>
    private void PushCharacterPatternData(List<UserData> onlinePlayers)
    {
        if (onlinePlayers.Any() && _lastSentData?.ToyboxData != null)
        {
            _ = Task.Run(async () =>
            {
                await _apiController.PushCharacterToyboxInfoData(_lastSentData.ToyboxData, onlinePlayers).ConfigureAwait(false);
            });
        }
    }
}
*/
