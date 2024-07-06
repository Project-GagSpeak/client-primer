using FFStreamViewer.WebAPI.Services;
using FFStreamViewer.WebAPI.Services.Mediator;
using FFStreamViewer.WebAPI.SignalR.Utils;

namespace FFStreamViewer.WebAPI.PlayerData.Handlers;
public sealed class GameObjectHandler : DisposableMediatorSubscriberBase
{
    private readonly OnFrameworkService _frameworkUtil; // for method helpers handled on the game's framework thread.
    private readonly Func<IntPtr> _getAddress;          // for getting the address of the object.
    private readonly bool _isOwnedObject;               // if this is an owned object of the cache creation service.
    private Task? _delayedZoningTask;                   // task to delay checking and updating owned object between zones.
    private CancellationTokenSource _zoningCts = new(); // CTS for the zoning task
    private CancellationTokenSource? _clearCts = new(); // CTS for the cache creation service
    private bool _haltProcessing = false;               // if we should halt the processing of our managed character.

    public GameObjectHandler(ILogger<GameObjectHandler> logger, GagspeakMediator mediator,
        OnFrameworkService frameworkUtil, Func<IntPtr> getAddress, bool ownedObject = true) : base(logger, mediator)
    {
        _frameworkUtil = frameworkUtil;
        _getAddress = () =>
        {
            _frameworkUtil.EnsureIsOnFramework();
            return getAddress.Invoke();
        };
        _isOwnedObject = ownedObject;
        Name = string.Empty;

        // cutscene mediators would help with halting processing.

        Mediator.Subscribe<ZoneSwitchEndMessage>(this, (_) => ZoneSwitchEnd());
        Mediator.Subscribe<ZoneSwitchStartMessage>(this, (_) => ZoneSwitchStart());

        // this might require a bit of expansion into the cache creation service, but only if what we have now isnt enough.
        Mediator.Publish(new GameObjectHandlerCreatedMessage(this, _isOwnedObject));
        // run the object checks on framework thread.
        _frameworkUtil.RunOnFrameworkThread(CheckAndUpdateObject).GetAwaiter().GetResult();

    }

    // this is very likely going to go wrong since there is an
    // address updater inside of the updateobject method we erased.
    // (im certain it will, but i just want to see SOMETHING happen right now)
    public IntPtr Address { get; private set; } // addr of character
    public string Name { get; private set; } // the name of the character
    private IntPtr DrawObjectAddress { get; set; } // the address of the characters draw object.

    public void Invalidate() => Address = IntPtr.Zero; // sets game objects address to 0

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        Mediator.Publish(new GameObjectHandlerDestroyedMessage(this, _isOwnedObject));
    }

    private void FrameworkUpdate()
    {
        if (!_delayedZoningTask?.IsCompleted ?? false) return;

        try
        {
            CheckAndUpdateObject();
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Error during FrameworkUpdate of {this}", this);
        }
    }

    /* Performs an operation on each framework update to check and update the owned objects we have. 
     * It's critical that this doesn't take too much processing power.                              */
    private unsafe void CheckAndUpdateObject()
    {
        // store the previous address and draw object.
        var prevAddr = Address;
        var prevDrawObj = DrawObjectAddress;

        // update the address of this game object.
        Address = _getAddress();
        // if the address still exists, update the draw object address.
        if (Address != IntPtr.Zero)
        {
            // _ptrNullCounter = 0; // Might have signifigance later we'll see.
            var drawObjAddr = (IntPtr)((FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)Address)->DrawObject;
            DrawObjectAddress = drawObjAddr;
        }
        // otherwise, it doesnt exist, so set the draw object address to 0.
        else
        {
            DrawObjectAddress = IntPtr.Zero;
        }

        // if we are halting processing due to changing zones or something else like a cutscene or whatever, return.
        if (_haltProcessing) return;

        // otherwise, check if the address or draw object address has changed.
        bool drawObjDiff = DrawObjectAddress != prevDrawObj;
        bool addrDiff = Address != prevAddr;

        // check if the updated Address and Draw object are both not 0
        if (Address != IntPtr.Zero && DrawObjectAddress != IntPtr.Zero)
        {
            // if they are both not 0, and the clear cts is not null (aka we want to cancel), cancel the clear cts.
            if (_clearCts != null)
            {
                Logger.LogDebug("Cancelling Clear Task");
                _clearCts.CancelDispose();
                _clearCts = null;
            }

            // if this is not a owned object, instantly publish the character changed message and return.
            if (!_isOwnedObject)
            {
                Logger.LogTrace("Is not a owned object.");
                /* Consume */
                return;
            }

            // if there was a difference in the address, or draw object, and it is an owned object, then publish the cache creation.
            if ((addrDiff || drawObjDiff) && _isOwnedObject)
            {
                Logger.LogDebug("Changed, Sending CreateCacheObjectMessage");
                Mediator.Publish(new CreateCacheForObjectMessage(this));
            }
        }
        // otherwise, if the new address OR the new draw object was IntPtr.Zero / not visible, and we had a change in address or draw object.
        else if (addrDiff || drawObjDiff)
        {
            // log the change, and if it is an owned object, publish the clear cache message since it is no longer valid.
            Logger.LogTrace("[{this}] Changed", this);
            if (_isOwnedObject)
            {
                Logger.LogDebug("[{this}] Calling upon ClearAsync due to an owned object vanishing.", this);
                _clearCts?.CancelDispose();
                _clearCts = new();
                var token = _clearCts.Token;
                _ = Task.Run(() => ClearAsync(token), token);
            }
        }
    }

    /// <summary> The bridge to our cache creation service for the object handler. Should be called upon disposal of the gameobjecthandler
    /// <returns> Clears the cache for the object handler in the cache creation service via mediator publish. </returns>
    private async Task ClearAsync(CancellationToken token)
    {
        Logger.LogDebug("Running Clear Task");
        await Task.Delay(TimeSpan.FromSeconds(1), token).ConfigureAwait(false);
        Logger.LogDebug("Sending ClearCachedForObjectMessage");
        Mediator.Publish(new ClearCacheForObjectMessage(this));
        _clearCts = null;
    }

    private void ZoneSwitchEnd()
    {
        if (!_isOwnedObject || _haltProcessing) return;

        _clearCts?.Cancel();
        _clearCts?.Dispose();
        _clearCts = null;
        try
        {
            _zoningCts?.CancelAfter(2500);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Zoning CTS cancel issue");
        }
    }

    private void ZoneSwitchStart()
    {
        if (!_isOwnedObject || _haltProcessing) return;

        _zoningCts = new();
        Logger.LogDebug("[{obj}] Starting Delay After Zoning", this);
        _delayedZoningTask = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(120), _zoningCts.Token).ConfigureAwait(false);
            }
            catch { /* consume */ }
            finally
            {
                Logger.LogDebug("[{this}] Delay after zoning complete", this);
                _zoningCts.Dispose();
            }
        });
    }
}
