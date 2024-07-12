using FFStreamViewer.WebAPI.Services.Mediator;

namespace FFStreamViewer.WebAPI.Interop.Ipc;

/// <summary>
/// The primary manager for all IPC calls.
/// </summary>
public sealed partial class IpcManager : DisposableMediatorSubscriberBase
{
    public IpcCallerMoodles Moodles { get; }

    public IpcManager(ILogger<IpcManager> logger, GagspeakMediator mediator,
        IpcCallerMoodles moodlesIpc) : base(logger, mediator)
    {
        Moodles = moodlesIpc;

        // subscribe to the delayed framework update message, which will call upon the periodic API state check.
        Mediator.Subscribe<DelayedFrameworkUpdateMessage>(this, (_) => PeriodicApiStateCheck());

        try // do an initial check
        {
            PeriodicApiStateCheck();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to check for some IPC, plugin not installed?");
        }
    }

    private void PeriodicApiStateCheck()
    {
        Moodles.CheckAPI();
    }
}
