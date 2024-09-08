using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Text;
using Dalamud.Plugin.Services;
using GagSpeak.GagspeakConfiguration.Models;
using GagSpeak.PlayerData.Pairs;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using GagSpeak.UpdateMonitoring;
using GagSpeak.UpdateMonitoring.Triggers;
using GagspeakAPI.Data.VibeServer;
using GagSpeak.Utils;
using GameAction = Lumina.Excel.GeneratedSheets.Action;

namespace GagSpeak.PlayerData.Handlers;

// Trigger Controller helps manage the currently active triggers and listens in on the received action effects
public class TriggerController : DisposableMediatorSubscriberBase
{
    private readonly ClientConfigurationManager _clientConfigs;
    private readonly PairManager _pairManager;
    private readonly ActionEffectMonitor _receiveActionEffectHookManager;
    private readonly OnFrameworkService _frameworkService;
    private readonly IDataManager _gameData;


    public TriggerController(ILogger<TriggerController> logger, GagspeakMediator mediator,
        ClientConfigurationManager clientConfiguration, PairManager pairManager,
        ActionEffectMonitor actionEffectMonitor, OnFrameworkService frameworkUtils,
        IDataManager dataManager) : base(logger, mediator)
    {
        _clientConfigs = clientConfiguration;
        _pairManager = pairManager;
        _receiveActionEffectHookManager = actionEffectMonitor;
        _frameworkService = frameworkUtils;
        _gameData = dataManager;
        ActionEffectMonitor.ActionEffectEntryEvent += OnActionEffectEvent;
        Mediator.Subscribe<DelayedFrameworkUpdateMessage>(this, (msg) => UpdateTriggerMonitors());
    }

    private List<Trigger> ActiveTriggers => _clientConfigs.GetActiveTriggers();
    private bool ShouldEnableActionEffectHooks => ActiveTriggers.Any(x => x.Type == TriggerKind.SpellAction || x.Type == TriggerKind.HealthPercent);

    protected override void Dispose(bool disposing)
    {
        ActionEffectMonitor.ActionEffectEntryEvent -= OnActionEffectEvent;
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

    public async void OnActionEffectEvent(List<ActionEffectEntry> actionEffects)
    {
        Logger.LogInformation("Action Effect Event Triggered");
        string sourceCharaStr = "";
        string targetCharaStr = "";
        string actionStr = "";

        if(_clientConfigs.GagspeakConfig.LogActionEffects)
        {
            await _frameworkService.RunOnFrameworkThread(() =>
            {
                foreach (var actionEffect in actionEffects)
                {
                    sourceCharaStr = (_frameworkService.SearchObjectTableById(actionEffect.SourceID) as IPlayerCharacter)?.GetNameWithWorld() ?? "UNKN OBJ";
                    targetCharaStr = (_frameworkService.SearchObjectTableById(actionEffect.TargetID) as IPlayerCharacter)?.GetNameWithWorld() ?? "UNKN OBJ";
                    actionStr = _gameData.GetExcelSheet<GameAction>()!.GetRow(actionEffect.ActionID)?.Name.ToString() ?? "UNKN ACT";

                    Logger.LogDebug($"Source:{sourceCharaStr}, Target: {targetCharaStr}, Action: {actionStr}, Action ID:{actionEffect.ActionID}, Type: {actionEffect.Type.ToString()} Ammount: {actionEffect.Damage}");
                }
            });
        }
    }


    private void UpdateTriggerMonitors()
    {
/*        if (ShouldEnableActionEffectHooks)
        {
            _receiveActionEffectHookManager.EnableHook();
        }
        else
        {
            _receiveActionEffectHookManager.DisableHook();
        }*/
    }
}

public struct MonitoredTrigger
{

}
