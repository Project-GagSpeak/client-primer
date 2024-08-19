using FFXIVClientStructs.FFXIV.Client.Game.Character;
using GagSpeak.Interop.Ipc;
using GagSpeak.PlayerData.Factories;
using GagSpeak.PlayerData.Handlers;
using GagSpeak.Services.Mediator;
using GagSpeak.UpdateMonitoring;
using GagspeakAPI.Data.Character;

namespace GagSpeak.PlayerData.Services;

#pragma warning disable MA0040

// this is a sealed scoped class meaning the cache service would be unique for every player assigned to it.
public sealed class CacheCreationService : DisposableMediatorSubscriberBase
{
    private readonly SemaphoreSlim _cacheCreateLock = new(1);
    private GameObjectHandler _cacheToCreate;          // player object cache to create.
    private readonly OnFrameworkService _frameworkUtil;
    private readonly IpcManager _ipcManager;
    private readonly CancellationTokenSource _cts = new();
    private readonly CharacterCompositeData _playerCompositeData = new();
    private readonly GameObjectHandler _playerObject;          // handler for player object.
    private Task? _cacheCreationTask;
    private CancellationTokenSource _moodlesCts = new();
    private bool _isZoning = false;

    public CacheCreationService(ILogger<CacheCreationService> logger, GagspeakMediator mediator,
        GameObjectHandlerFactory gameObjectHandlerFactory, OnFrameworkService frameworkUtil,
        IpcManager ipcManager) : base(logger, mediator)
    {
        _frameworkUtil = frameworkUtil;
        _ipcManager = ipcManager;

        // called upon whenever a new cache should be added to the cache creation service.
        Mediator.Subscribe<CreateCacheForObjectMessage>(this, (msg) =>
        {
            Logger.LogDebug("Received CreateCacheForObject for {handler}, updating", msg.ObjectToCreateFor);
            _cacheCreateLock.Wait();
            _cacheToCreate = msg.ObjectToCreateFor;
            _cacheCreateLock.Release();
        });

        Mediator.Subscribe<ZoneSwitchStartMessage>(this, (msg) => _isZoning = true);
        Mediator.Subscribe<ZoneSwitchEndMessage>(this, (msg) => _isZoning = false);

        _playerObject = gameObjectHandlerFactory.Create(frameworkUtil.GetPlayerPointer, isWatched: true).GetAwaiter().GetResult();

        Mediator.Subscribe<ClearCacheForObjectMessage>(this, (msg) =>
        {
            _ = Task.Run(() =>
            {
                Logger.LogTrace("Clearing cache for {obj}", msg.ObjectToCreateFor);
                Mediator.Publish(new CharacterDataCreatedMessage(_playerCompositeData));
            });
        });

        Mediator.Subscribe<MoodlesStatusManagerChangedMessage>(this, (msg) =>
        {
            if (_isZoning) return;
            // Assuming _playerObject is now a single GameObjectHandler instance
            if (_playerObject != null && _playerObject.Address == msg.Address)
            {
                Logger.LogDebug("Received Moodles change, updating player");
                MoodlesChanged();
            }
        });

        Mediator.Subscribe<DelayedFrameworkUpdateMessage>(this, (msg) => ProcessCacheCreation());
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        _playerObject.Dispose();
        _cts.Dispose();
    }

    private async Task AddPlayerCacheToCreate()
    {
        await _cacheCreateLock.WaitAsync().ConfigureAwait(false);
        _cacheToCreate = _playerObject;
        _cacheCreateLock.Release();
    }

    private void MoodlesChanged()
    {
        _moodlesCts?.Cancel();
        _moodlesCts?.Dispose();
        _moodlesCts = new();
        var token = _moodlesCts.Token;

        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(0.5), token).ConfigureAwait(false);
            await AddPlayerCacheToCreate().ConfigureAwait(false);
        }, token);
    }

    private void ProcessCacheCreation()
    {
        if (_isZoning) return;

        if (_cacheToCreate != null && (_cacheCreationTask?.IsCompleted ?? true))
        {
            _cacheCreateLock.Wait();
            var toCreate = _cacheToCreate;
            // set the cache to create to null
            _cacheToCreate = null;
            _cacheCreateLock.Release();

            _cacheCreationTask = Task.Run(async () =>
            {
                try
                {
                    await BuildCharacterData(_playerCompositeData, toCreate, _cts.Token).ConfigureAwait(false);
                    Mediator.Publish(new CharacterDataCreatedMessage(_playerCompositeData));
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
        else if (_cacheToCreate != null)
        {
            Logger.LogDebug("Cache Creation stored until previous creation finished");
        }
    }

    /*   Creating and Buildering Character Information from IPC data */
    public async Task BuildCharacterData(CharacterCompositeData previousData, GameObjectHandler playerRelatedObject, CancellationToken token)
    {
        if (playerRelatedObject == null || playerRelatedObject.Address == nint.Zero) return;

        try
        {
            var pointerIsZero = await CheckForNullDrawObject(playerRelatedObject.Address).ConfigureAwait(false);
            if (pointerIsZero)
            {
                Logger.LogTrace("Pointer was zero for object being built");
                return;
            }

            await CreateCharacterData(previousData, playerRelatedObject, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            Logger.LogDebug("Cancelled creating Character data for {object}", playerRelatedObject);
        }
        catch (Exception e)
        {
            Logger.LogWarning(e, "Failed to create {object} data", playerRelatedObject);
        }
    }

    private async Task<bool> CheckForNullDrawObject(nint playerPointer)
    {
        // Assuming _frameworkUtil is correctly defined in the class context
        return await _frameworkUtil.RunOnFrameworkThread(() => CheckForNullDrawObjectUnsafe(playerPointer)).ConfigureAwait(false);
    }

    private unsafe bool CheckForNullDrawObjectUnsafe(nint playerPointer)
    {
        // Correct handling for managed type pointers, assuming Character is correctly defined and accessible
        return ((Character*)playerPointer)->GameObject.DrawObject == null;
    }

    private async Task<CharacterCompositeData> CreateCharacterData(CharacterCompositeData previousData, GameObjectHandler playerRelatedObject, CancellationToken token)
    {
        var charaPointer = playerRelatedObject.Address;

        Logger.LogDebug("Updating IPC data relevant to character data for {obj}", playerRelatedObject);

        var start = DateTime.UtcNow;

        // grab the moodles data from the player object.
        previousData.IPCData.MoodlesData = await _ipcManager.Moodles.GetStatusAsync(playerRelatedObject.Address).ConfigureAwait(false) ?? string.Empty;
        Logger.LogDebug("Moodles is now: {moodles}", previousData.IPCData.MoodlesData);

        Logger.LogInformation("IPC Update for player object took {time}ms", TimeSpan.FromTicks(DateTime.UtcNow.Ticks - start.Ticks).TotalMilliseconds);

        return previousData;
    }


}
#pragma warning restore MA0040
