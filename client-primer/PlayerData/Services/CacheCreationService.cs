using FFXIVClientStructs.FFXIV.Client.Game.Character;
using GagSpeak.Interop.Ipc;
using GagSpeak.PlayerData.Factories;
using GagSpeak.PlayerData.Handlers;
using GagSpeak.Services.Mediator;
using GagSpeak.UpdateMonitoring;
using GagspeakAPI.Data.Character;
using GagspeakAPI.Data.Enum;

namespace GagSpeak.PlayerData.Services;

#pragma warning disable MA0040

public struct CacheData
{
    public GameObjectHandler? GameObj { get; set; }
    public DataUpdateKind UpdateKind { get; set; }
    public Guid Guid { get; set; }

    public CacheData(GameObjectHandler? handler, DataUpdateKind updateKind, Guid guid)
    {
        GameObj = handler;
        UpdateKind = updateKind;
        Guid = guid;
    }
}


// Made for the player character.
// Holds the cached information about changes to make to the player.
// Changes are pushed to the visible player manager. Which performs the API calls.
public sealed class CacheCreationService : DisposableMediatorSubscriberBase
{
    private readonly SemaphoreSlim _cacheCreateLock = new(1);
    private readonly OnFrameworkService _frameworkUtil;
    private readonly IpcManager _ipcManager;
    private readonly CancellationTokenSource _cts = new();
    private Task? _cacheCreationTask;
    private CancellationTokenSource _moodlesCts = new();
    private bool _isZoning = false;

    // player object cache to create. Item1 == NULL while no changes have occurred.
    private CacheData _cacheToCreate;

    private readonly CharacterIPCData _playerIpcData = new(); // handler for our player character's IPC data.
    private readonly GameObjectHandler _playerObject;         // handler for player characters object.

    public CacheCreationService(ILogger<CacheCreationService> logger, GagspeakMediator mediator,
        GameObjectHandlerFactory gameObjectHandlerFactory, OnFrameworkService frameworkUtil,
        IpcManager ipcManager) : base(logger, mediator)
    {
        _frameworkUtil = frameworkUtil;
        _ipcManager = ipcManager;

        _playerObject = gameObjectHandlerFactory.Create(frameworkUtil.GetPlayerPointer, isWatched: true).GetAwaiter().GetResult();


        // called upon whenever a new cache should be added to the cache creation service.
        Mediator.Subscribe<CreateCacheForObjectMessage>(this, async (msg) =>
        {
            Logger.LogDebug("Received CreateCacheForObject for {handler}, updating", msg.ObjectToCreateFor);
            _cacheCreateLock.Wait();
            if (_ipcManager.Moodles.APIAvailable) await FetchLatestMoodlesDataASync().ConfigureAwait(false);
            _cacheToCreate = new CacheData(msg.ObjectToCreateFor, DataUpdateKind.IpcUpdateVisible, Guid.Empty);
            _cacheCreateLock.Release();
        });

        Mediator.Subscribe<ZoneSwitchStartMessage>(this, (msg) => _isZoning = true);
        Mediator.Subscribe<ZoneSwitchEndMessage>(this, (msg) => _isZoning = false);

        Mediator.Subscribe<ClearCacheForObjectMessage>(this, (msg) =>
        {
            _ = Task.Run(() =>
            {
                _playerIpcData.MoodlesData = string.Empty;
                _playerIpcData.MoodlesStatuses.Clear();
                _playerIpcData.MoodlesPresets.Clear();
                Logger.LogDebug("Clearing cache for {obj}", msg.ObjectToCreateFor);
                Mediator.Publish(new CharacterIpcDataCreatedMessage(_playerIpcData, DataUpdateKind.IpcMoodlesCleared));
            });
        });

        Mediator.Subscribe<MoodlesReady>(this, async (_) =>
        {
            // run an api check before this to force update 
            _ipcManager.Moodles.CheckAPI();
            await FetchLatestMoodlesDataASync().ConfigureAwait(false);
            Logger.LogDebug("Moodles is now ready, fetching latest info and pushing to all visible pairs");
            Mediator.Publish(new CharacterIpcDataCreatedMessage(_playerIpcData, DataUpdateKind.IpcUpdateVisible));
        });

        Mediator.Subscribe<MoodlesStatusManagerChangedMessage>(this, (msg) =>
        {
            if (_isZoning) return;
            // Assuming _playerObject is now a single GameObjectHandler instance
            if (_playerObject != null && _playerObject.Address == msg.Address)
            {
                Logger.LogDebug("Updating visible pairs with latest Moodles Data. [Status Manager Changed!]");
                _ = AddPlayerCacheToCreate(DataUpdateKind.IpcMoodlesStatusManagerChanged, Guid.Empty);
            }
        });

        Mediator.Subscribe<MoodlesStatusModified>(this, (msg) =>
        {
            if (_isZoning) return;
            // Assuming _playerObject is now a single GameObjectHandler instance
            if (_playerObject != null && _playerObject.Address != nint.Zero)
            {
                Logger.LogDebug("Updating visible pairs with latest Moodles Data. [You Changed Settings of a Status!]");
                _ = AddPlayerCacheToCreate(DataUpdateKind.IpcMoodlesStatusesUpdated, msg.Guid);
            }
        });

        Mediator.Subscribe<MoodlesPresetModified>(this, (msg) =>
        {
            if (_isZoning) return;
            if (_playerObject != null && _playerObject.Address != nint.Zero)
            {
                Logger.LogDebug("Received a Status Manager change for Moodles. Updating player with latest IPC");
                _ = AddPlayerCacheToCreate(DataUpdateKind.IpcMoodlesPresetsUpdated, msg.Guid);
            }
        });

        Mediator.Subscribe<FrameworkUpdateMessage>(this, (msg) => ProcessCacheCreation());
    }

    private async Task FetchLatestMoodlesDataASync()
    {
        _playerIpcData.MoodlesData = await _ipcManager.Moodles.GetStatusAsync(_playerObject.NameWithWorld).ConfigureAwait(false) ?? string.Empty;
        _playerIpcData.MoodlesDataStatuses = await _ipcManager.Moodles.GetStatusInfoAsync(_playerObject.NameWithWorld).ConfigureAwait(false) ?? new();
        _playerIpcData.MoodlesStatuses = await _ipcManager.Moodles.GetMoodlesInfoAsync().ConfigureAwait(false) ?? new();
        _playerIpcData.MoodlesPresets = await _ipcManager.Moodles.GetPresetsInfoAsync().ConfigureAwait(false) ?? new();
        Logger.LogDebug("Latest Data from Moodles Fetched.");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        _playerObject.Dispose();
        _cts.Dispose();
    }

    private async Task AddPlayerCacheToCreate(DataUpdateKind updateKind, Guid guid = default)
    {
        await _cacheCreateLock.WaitAsync().ConfigureAwait(false);
        _cacheToCreate = new CacheData(_playerObject, updateKind, guid);
        _cacheCreateLock.Release();
    }

    private void ProcessCacheCreation()
    {
        if (_isZoning) return;

        if (_cacheToCreate.GameObj != null && (_cacheCreationTask?.IsCompleted ?? true))
        {
            _cacheCreateLock.Wait();
            var toCreate = _cacheToCreate;
            // set the cache to create to null
            _cacheToCreate.GameObj = null;
            _cacheCreateLock.Release();

            _cacheCreationTask = Task.Run(async () =>
            {
                try
                {
                    await BuildCharacterData(_playerIpcData, toCreate, _cts.Token).ConfigureAwait(false);
                    Mediator.Publish(new CharacterIpcDataCreatedMessage(_playerIpcData, toCreate.UpdateKind));
                }
                catch (Exception ex)
                {
                    Logger.LogCritical(ex, "Error during Cache Creation Processing");
                }
                finally
                {
                    Logger.LogDebug("Cache Creation complete");
                }
            }, _cts.Token);
        }
        else if (_cacheToCreate.GameObj != null)
        {
            Logger.LogDebug("Cache Creation stored until previous creation finished");
        }
    }

    /*   Creating and Buildering Character Information from IPC data */
    public async Task BuildCharacterData(CharacterIPCData prevData, CacheData playerObjData, CancellationToken token)
    {
        if (playerObjData.GameObj == null || playerObjData.GameObj.Address == nint.Zero) return;

        try
        {
            var pointerIsZero = await CheckForNullDrawObject(playerObjData.GameObj.Address).ConfigureAwait(false);
            if (pointerIsZero)
            {
                Logger.LogTrace("Pointer was zero for object being built");
                return;
            }

            var start = DateTime.UtcNow;
            // Obtain the Status Manager State for the player object.
            switch (playerObjData.UpdateKind)
            {
                case DataUpdateKind.IpcMoodlesStatusesUpdated:
                    {
                        await StatusSettingsUpdate(prevData, playerObjData, playerObjData.Guid).ConfigureAwait(false);
                    }
                    break;
                case DataUpdateKind.IpcMoodlesPresetsUpdated:
                    {
                        await PresetSettingsUpdate(prevData, playerObjData, playerObjData.Guid).ConfigureAwait(false);
                    }
                    break;
                case DataUpdateKind.None:
                case DataUpdateKind.IpcUpdateVisible:
                    {
                        prevData.MoodlesData = await _ipcManager.Moodles.GetStatusAsync(playerObjData.GameObj.NameWithWorld).ConfigureAwait(false) ?? string.Empty;
                        prevData.MoodlesDataStatuses = await _ipcManager.Moodles.GetStatusInfoAsync(playerObjData.GameObj.NameWithWorld).ConfigureAwait(false) ?? new();
                        prevData.MoodlesStatuses = await _ipcManager.Moodles.GetMoodlesInfoAsync().ConfigureAwait(false) ?? new();
                        prevData.MoodlesPresets = await _ipcManager.Moodles.GetPresetsInfoAsync().ConfigureAwait(false) ?? new();
                    }
                    break;
                case DataUpdateKind.IpcMoodlesStatusManagerChanged:
                    {
                        await StatusManagerUpdate(prevData, playerObjData).ConfigureAwait(false);
                    }
                    break;
                case DataUpdateKind.IpcMoodlesCleared: Logger.LogTrace("Clearing Moodles Data for {object}", playerObjData); break;
                default: Logger.LogWarning("Unknown Update Kind for {object}", playerObjData); break;
            }
            Logger.LogInformation("IPC Update for player object took {time}ms", TimeSpan.FromTicks(DateTime.UtcNow.Ticks - start.Ticks).TotalMilliseconds);
            Logger.LogTrace("Data: {data}", prevData.MoodlesData);
            Logger.LogTrace("StatusManager Statuses: {data}", prevData.MoodlesDataStatuses.Count);
            Logger.LogTrace("Statuses: {data}", prevData.MoodlesStatuses.Count);
            Logger.LogTrace("Presets: {data}", prevData.MoodlesPresets.Count);
        }
        catch (OperationCanceledException)
        {
            Logger.LogDebug("Cancelled creating Character data for {object}", playerObjData);
        }
        catch (Exception e)
        {
            Logger.LogWarning(e, "Failed to create {object} data", playerObjData);
        }
    }

    private async Task StatusManagerUpdate(CharacterIPCData data, CacheData playerObjData)
    {
        data.MoodlesData = await _ipcManager.Moodles.GetStatusAsync(playerObjData.GameObj!.NameWithWorld).ConfigureAwait(false) ?? string.Empty;
        data.MoodlesDataStatuses = await _ipcManager.Moodles.GetStatusInfoAsync(playerObjData.GameObj.NameWithWorld).ConfigureAwait(false) ?? new();
    }

    private async Task StatusSettingsUpdate(CharacterIPCData data, CacheData playerObjData, Guid guid)
    {
        // Find the index of the tuple containing the GUID.
        var index = data.MoodlesStatuses.FindIndex(x => x.GUID == guid);
        if (index != -1)
        {
            data.MoodlesStatuses[index] = await _ipcManager.Moodles.GetMoodleInfoAsync(guid).ConfigureAwait(false) ?? new();
        }
        else
        {
            data.MoodlesStatuses = await _ipcManager.Moodles.GetMoodlesInfoAsync().ConfigureAwait(false) ?? new();
        }
    }

    private async Task PresetSettingsUpdate(CharacterIPCData data, CacheData playerObjData, Guid guid)
    {
        // Find the index containing the GUID.
        var index = data.MoodlesPresets.FindIndex(x => x.Item1 == guid);
        if (index != -1)
        {
            data.MoodlesPresets[index] = await _ipcManager.Moodles.GetPresetInfoAsync(guid).ConfigureAwait(false) ?? new();
        }
        else
        {
            data.MoodlesPresets = await _ipcManager.Moodles.GetPresetsInfoAsync().ConfigureAwait(false) ?? new();
        }
    }

    private async Task<bool> CheckForNullDrawObject(nint playerPointer)
        => await _frameworkUtil.RunOnFrameworkThread(() => CheckForNullDrawObjectUnsafe(playerPointer)).ConfigureAwait(false);

    private unsafe bool CheckForNullDrawObjectUnsafe(nint playerPointer)
        => ((Character*)playerPointer)->GameObject.DrawObject == null;
}
#pragma warning restore MA0040
