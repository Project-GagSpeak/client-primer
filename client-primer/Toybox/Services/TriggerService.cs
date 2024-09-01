using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using GagSpeak.PlayerData.Handlers;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using GagspeakAPI.Data.VibeServer;

namespace GagSpeak.Toybox.Services;

// handles the management of the connected devices or simulated vibrator.
public class TriggerService : DisposableMediatorSubscriberBase
{
    private readonly ClientConfigurationManager _clientConfigs;
    private readonly TriggerController _triggerController; // for managing the logic about if a trigger should be executed.
    private readonly ToyboxVibeService _vibeService; // for trigger execution handling.

    public TriggerService(ILogger<TriggerService> logger,
        GagspeakMediator mediator, ClientConfigurationManager clientConfigs,
        TriggerController triggerController, ToyboxVibeService vibeService)
        : base(logger, mediator)
    {
        _clientConfigs = clientConfigs;
        _triggerController = triggerController;
        _vibeService = vibeService;
    }

    public VibratorMode CurrentVibratorModeUsed => _clientConfigs.GagspeakConfig.VibratorMode;

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
    }

    public void CheckChatMessageForTrigger(XivChatType chatChannel, ref SeString sender, ref SeString message)
    {
        // turn the sender into the player-name-with-world and then call upon the trigger controllers chat checker.

        // if it is valid, execute the trigger via the vibe service.

        // This process mimics the same workflow that the vibe plugin does in a more clean manner.

    }

}





