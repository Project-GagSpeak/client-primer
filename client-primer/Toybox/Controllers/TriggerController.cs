using Dalamud.Game.Text;
using GagSpeak.GagspeakConfiguration.Models;
using GagSpeak.PlayerData.Pairs;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using GagSpeak.UpdateMonitoring;
using GagSpeak.UpdateMonitoring.Triggers;
using GagspeakAPI.Data.VibeServer;

namespace GagSpeak.PlayerData.Handlers;

// Trigger Controller helps manage the currently active triggers and listens in on the received action effects
public class TriggerController : DisposableMediatorSubscriberBase
{
    private readonly ClientConfigurationManager _clientConfigs;
    private readonly PairManager _pairManager;
    private readonly ActionEffectMonitor _receiveActionEffectHookManager;
    private readonly OnFrameworkService _frameworkService;

    public TriggerController(ILogger<DeviceController> logger, GagspeakMediator mediator,
        ClientConfigurationManager clientConfiguration, PairManager pairManager,
        ActionEffectMonitor actionEffectMonitor) : base(logger, mediator)
    {
        _clientConfigs = clientConfiguration;
        _pairManager = pairManager;
        _receiveActionEffectHookManager = actionEffectMonitor;

        Mediator.Subscribe<DelayedFrameworkUpdateMessage>(this, (msg) => UpdateTriggerMonitors());
    }

    private List<Trigger> ActiveTriggers => _clientConfigs.GetActiveTriggers();
    private bool ShouldEnableActionEffectHooks => ActiveTriggers.Any(x => x.Type == TriggerKind.SpellAction || x.Type == TriggerKind.HealthPercent);

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
    }

    public List<ChatTrigger> CheckActiveChatTriggers(XivChatType chatType, string senderNameWithWorld, string message)
    {
        // return the list of chat triggers whom the triggeredby matches the senderNamewithworld, and the chatmessage is contained in the message string with string comparison ordinal.
        return ActiveTriggers
            .OfType<ChatTrigger>()
            .Where(x => x.FromPlayerName == senderNameWithWorld && message.Contains(x.ChatText, StringComparison.Ordinal))
            .ToList();
    }




    private void UpdateTriggerMonitors()
    {
        if (ShouldEnableActionEffectHooks)
        {
            _receiveActionEffectHookManager.EnableHook();
        }
        else
        {
            _receiveActionEffectHookManager.DisableHook();
        }
    }
}

public struct MonitoredTrigger
{

}
