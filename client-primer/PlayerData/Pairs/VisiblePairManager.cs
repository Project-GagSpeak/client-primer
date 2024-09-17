using GagSpeak.PlayerData.Handlers;
using GagSpeak.Services.Mediator;
using GagspeakAPI.Data;
using GagspeakAPI.Data.Character;
using GagSpeak.UpdateMonitoring;
using GagSpeak.WebAPI;
using GagSpeak.PlayerData.Data;
using GagSpeak.PlayerData.Pairs;
using GagspeakAPI.Data.Enum;

namespace GagSpeak.PlayerData.Pairs;

/// <summary>
/// Manages the transfer of IPC data between visibly rendered pairs.
/// </summary>
public class VisiblePairManager : DisposableMediatorSubscriberBase
{
    private readonly ApiController _apiController;
    private readonly OnFrameworkService _frameworkUtil;
    private readonly PlayerCharacterData _playerManager;
    private readonly PairManager _pairManager;

    // Stores the last recieved IpcData from our client player characters cache creation service.
    private CharacterIPCData LastIpcData = null!;

    // stores the set of newly visible players to update with our latest IPC data.
    private readonly HashSet<PairHandler> _newVisiblePlayers = [];

    public VisiblePairManager(ILogger<VisiblePairManager> logger,
        GagspeakMediator mediator, ApiController apiController,
        OnFrameworkService dalamudUtil, PlayerCharacterData playerManager,
        PairManager pairManager) : base(logger, mediator)
    {
        _apiController = apiController;
        _frameworkUtil = dalamudUtil;
        _playerManager = playerManager;
        _pairManager = pairManager;

        // Cyclic check for any new visible players to push IPC Data to.
        Mediator.Subscribe<DelayedFrameworkUpdateMessage>(this, (_) => FrameworkOnUpdate());

        // Fired whenever our IPC data is updated. Sends to visible players.
        Mediator.Subscribe<CharacterIpcDataCreatedMessage>(this, (msg) =>
        {
            var newData = msg.CharacterIPCData;
            // Send if attached data is different from last sent data.
            // this check also helps us ensure that we are not receiving the same data as pairHandlerVisible
            if (LastIpcData == null || LastIpcData.Equals(newData))
            {
                Logger.LogDebug("Pushing new IPC data to all visible players");
                LastIpcData = newData;
                PushCharacterIpcData(_pairManager.GetVisibleUsers(), msg.UpdateKind);
            }
            else
            {
                Logger.LogDebug("Data was no different. Not sending data");
            }
        });

        // Called whenever we are requesting to apply a set of moodles from our clients Moodle Statuses, to another pair.
        Mediator.Subscribe<MoodlesApplyStatusToPair>(this, (msg) =>
        {
            Logger.LogDebug("Applying List of your Statuses from your Moodles to {user}", msg.StatusDto.User.AliasOrUID);
            _ = Task.Run(async () =>
            {
                await _apiController.UserApplyMoodlesByStatus(msg.StatusDto).ConfigureAwait(false);
            });
        });


        // Add pair to list when they become visible.
        Mediator.Subscribe<PairHandlerVisibleMessage>(this, (msg) => _newVisiblePlayers.Add(msg.Player));

    }

    private void FrameworkOnUpdate()
    {
        // return if Client Player is not visible or not connected.
        if (!_frameworkUtil.GetIsPlayerPresent() || !_apiController.IsConnected) return;

        // return if no new visible players.
        if (!_newVisiblePlayers.Any()) return;

        // Copy all new visible players into a new list and clear the old list.
        var newVisiblePlayers = _newVisiblePlayers.ToList();
        _newVisiblePlayers.Clear();

        // Push our IPC data to those players, applying our moodles data & sending customize+ info.
        Logger.LogTrace("Has new visible players, pushing character data");
        PushCharacterIpcData(newVisiblePlayers.Select(c => c.OnlineUser.User).ToList(), DataUpdateKind.IpcUpdateVisible);
    }

    /// <summary>
    /// Pushes the character IPC data to the server for the visible players.
    /// </summary>
    private void PushCharacterIpcData(List<UserData> visablePlayers, DataUpdateKind updateKind)
    {
        // If the list contains any contents and we have new data, asynchronously push it to the server.
        if (visablePlayers.Any() && LastIpcData != null)
        {
            _ = Task.Run(async () =>
            {
                await _apiController.PushCharacterIpcData(LastIpcData, visablePlayers, updateKind).ConfigureAwait(false);
            });
        }
        else
        {
            Logger.LogWarning("No visible players to push IPC data to");
        }
    }
}
