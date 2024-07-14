using GagSpeak.Services.Mediator;
using Interop.Ipc;

namespace GagSpeak.Interop.Ipc;

/// <summary>
/// The primary manager for all IPC calls.
/// </summary>
public sealed partial class IpcManager : DisposableMediatorSubscriberBase
{
    public IpcCallerCustomize CustomizePlus { get; init; }
    public IpcCallerGlamourer Glamourer { get; }
    public IpcCallerPenumbra Penumbra { get; }
    public IpcCallerMoodles Moodles { get; }

    public IpcManager(ILogger<IpcManager> logger, GagspeakMediator mediator,
        IpcCallerCustomize ipcCustomize, IpcCallerGlamourer ipcGlamourer,
        IpcCallerPenumbra ipcPenumbra, IpcCallerMoodles moodlesIpc
        ) : base(logger, mediator)
    {
        CustomizePlus = ipcCustomize;
        Glamourer = ipcGlamourer;
        Penumbra = ipcPenumbra;
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
        Penumbra.CheckAPI();
        Glamourer.CheckAPI();
        CustomizePlus.CheckAPI();
        Moodles.CheckAPI();
    }
}
