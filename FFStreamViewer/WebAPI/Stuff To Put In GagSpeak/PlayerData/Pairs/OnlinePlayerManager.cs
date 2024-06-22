using FFStreamViewer.WebAPI.PlayerData.Handlers;
using FFStreamViewer.WebAPI.Services;
using FFStreamViewer.WebAPI.Services.Mediator;
using Gagspeak.API.Data;
using Gagspeak.API.Data.CharacterData;

namespace FFStreamViewer.WebAPI.PlayerData.Pairs;

/// <summary>
/// The class that manages all of the connected online users on the GagSpeak server.
/// </summary>
public class OnlinePlayerManager : DisposableMediatorSubscriberBase
{
    private readonly ApiController _apiController;
    private readonly OnFrameworkService _frameworkUtil;
    private readonly HashSet<PairHandler> _newVisiblePlayers = [];
    private readonly PairManager _pairManager;
    private CharacterData? _lastSentData;

    public OnlinePlayerManager(ILogger<OnlinePlayerManager> logger, ApiController apiController, OnFrameworkService dalamudUtil,
        PairManager pairManager, GagspeakMediator mediator) : base(logger, mediator)
    {
        _apiController = apiController;
        _frameworkUtil = dalamudUtil;
        _pairManager = pairManager;

        // our mediator subscribers.
        Mediator.Subscribe<DelayedFrameworkUpdateMessage>(this, (_) => FrameworkOnUpdate());
        Mediator.Subscribe<CharacterDataCreatedMessage>(this, (msg) =>
        {
            var newData = msg.CharacterData;
            // if the character created is different from our last sent data, then we will push the data.
            if (_lastSentData == null || (!string.Equals(newData, _lastSentData)))
            {
                // update our last send data, and then push our data to the server.
                Logger.LogDebug("Pushing data for visible players");
                _lastSentData = newData;
                PushCharacterData(_pairManager.GetVisibleUsers());
            }
            else
            {
                Logger.LogDebug("Not sending data for {newData}", newData);
            }
        });
        Mediator.Subscribe<PairHandlerVisibleMessage>(this, (msg) => _newVisiblePlayers.Add(msg.Player));
        Mediator.Subscribe<ConnectedMessage>(this, (_) => PushCharacterData(_pairManager.GetVisibleUsers()));
    }

    private void FrameworkOnUpdate()
    {
        if (!_frameworkUtil.GetIsPlayerPresent() || !_apiController.IsConnected) return;

        if (!_newVisiblePlayers.Any()) return;
        var newVisiblePlayers = _newVisiblePlayers.ToList();
        _newVisiblePlayers.Clear();
        Logger.LogTrace("Has new visible players, pushing character data");
        // push the character data if there is new visible players to push it to that are not already present.
        PushCharacterData(newVisiblePlayers.Select(c => c.OnlineUser.User).ToList());
    }

    /// <summary> Pushes the character data to the server for the visible players </summary>
    private void PushCharacterData(List<UserData> visiblePlayers)
    {
        if (visiblePlayers.Any() && _lastSentData != null)
        {
            _ = Task.Run(async () =>
            {
                await _apiController.PushCharacterData(_lastSentData, visiblePlayers).ConfigureAwait(false);
            });
        }
    }
}
