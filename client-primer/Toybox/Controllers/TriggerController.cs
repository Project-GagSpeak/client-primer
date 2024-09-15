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
using Dalamud.Game.ClientState.Objects.Types;
using GagSpeak.ChatMessages;
using OtterGuiInternal.Enums;
using GagspeakAPI.Data.Enum;
using GagSpeak.UI;
using GagSpeak.Interop.Ipc;
using GagSpeak.PlayerData.Data;
using GagSpeak.Toybox.Services;
using Dalamud.Utility;

namespace GagSpeak.PlayerData.Handlers;

// Trigger Controller helps manage the currently active triggers and listens in on the received action effects
public class TriggerController : DisposableMediatorSubscriberBase
{
    private readonly ClientConfigurationManager _clientConfigs;
    private readonly PlayerCharacterManager _playerManager;
    private readonly PairManager _pairManager;
    private readonly ActionEffectMonitor _receiveActionEffectHookManager;
    private readonly MonitoredPlayerState _playerStateMonitor;
    private readonly OnFrameworkService _frameworkService;
    private readonly ToyboxVibeService _vibeService;
    private readonly IpcCallerMoodles _moodlesIpc;
    private readonly IClientState _clientState;
    private readonly IDataManager _gameData;


    public TriggerController(ILogger<TriggerController> logger, GagspeakMediator mediator,
        ClientConfigurationManager clientConfiguration, PlayerCharacterManager playerManager, 
        PairManager pairManager, ActionEffectMonitor actionEffectMonitor, 
        MonitoredPlayerState playerMonitor, OnFrameworkService frameworkUtils,
        ToyboxVibeService vibeService, IpcCallerMoodles moodles, IClientState clientState, 
        IDataManager dataManager) : base(logger, mediator)

    {
        _clientConfigs = clientConfiguration;
        _playerManager = playerManager;
        _pairManager = pairManager;
        _receiveActionEffectHookManager = actionEffectMonitor;
        _playerStateMonitor = playerMonitor;
        _frameworkService = frameworkUtils;
        _vibeService = vibeService;
        _moodlesIpc = moodles;
        _clientState = clientState;
        _gameData = dataManager;

        ActionEffectMonitor.ActionEffectEntryEvent += OnActionEffectEvent;

        Mediator.Subscribe<RestraintSetToggledMessage>(this, (msg) => CheckActiveRestraintTriggers(msg));

        Mediator.Subscribe<GagTypeChanged>(this, (msg) => CheckGagStateTriggers(msg));

        Mediator.Subscribe<DelayedFrameworkUpdateMessage>(this, (_) => UpdateTriggerMonitors());

        Mediator.Subscribe<FrameworkUpdateMessage>(this, (_) => UpdateTrackedPlayerHealth());
    }


    public static List<MonitoredPlayerState> MonitoredPlayers { get; private set; } = new List<MonitoredPlayerState>();
    private bool ShouldEnableActionEffectHooks => _clientConfigs.ActiveTriggers.Any(x => x.Type is TriggerKind.SpellAction);

    protected override void Dispose(bool disposing)
    {
        ActionEffectMonitor.ActionEffectEntryEvent -= OnActionEffectEvent;
        base.Dispose(disposing);
    }

    public void CheckActiveChatTriggers(XivChatType chatType, string senderNameWithWorld, string message)
    {
        // Check to see if any active chat triggers are in the message
        var channel = ChatChannel.GetChatChannelFromXivChatType(chatType);
        if (channel == null) 
            return;

        var matchingTriggers = _clientConfigs.ActiveChatTriggers
            .Where(x => x.AllowedChannels.Any() 
                && x.AllowedChannels.Contains(channel.Value)
                && x.FromPlayerName == senderNameWithWorld 
                && message.Contains(x.ChatText, StringComparison.Ordinal))
            .ToList();
        // if the triggers is not empty, perform logic, but return if there isnt any.
        if(!matchingTriggers.Any())
            return;

        foreach (var trigger in matchingTriggers)
        {
            ExecuteTriggerAction(trigger);
        }
    }

    private void CheckSpellActionTriggers(ActionEffectEntry actionEffect)
    {
        var relevantTriggers = _clientConfigs.ActiveSpellActionTriggers
            .Where(trigger =>
                (trigger.ActionID == uint.MaxValue || trigger.ActionID == actionEffect.ActionID) &&
                trigger.ActionKind == actionEffect.Type)
            .ToList();

        foreach (var trigger in relevantTriggers)
        {
            try
            {
                // Determine if the direction matches
                var isSourcePlayer = _clientState.LocalPlayer!.ObjectIndex == actionEffect.SourceID;
                var isTargetPlayer = _clientState.LocalPlayer!.ObjectIndex == actionEffect.TargetID;

                bool directionMatches = trigger.Direction switch
                {
                    TriggerDirection.Self => isSourcePlayer && !isTargetPlayer,
                    TriggerDirection.SelfToOther => isSourcePlayer && isTargetPlayer,
                    TriggerDirection.Other => !isSourcePlayer && isTargetPlayer,
                    TriggerDirection.OtherToSelf => !isSourcePlayer && isTargetPlayer,
                    TriggerDirection.Any => true,
                    _ => false,
                };

                if (!directionMatches)
                {
                    Logger.LogDebug("Direction didn't match");
                    return; // Use return instead of continue in lambda expressions
                }

                // Check damage thresholds for relevant action kinds
                bool isDamageRelated = trigger.ActionKind is
                    LimitedActionEffectType.Heal or
                    LimitedActionEffectType.Damage or
                    LimitedActionEffectType.BlockedDamage or
                    LimitedActionEffectType.ParriedDamage;

                if (isDamageRelated && (actionEffect.Damage < trigger.ThresholdMinValue || actionEffect.Damage > trigger.ThresholdMaxValue))
                {
                    Logger.LogDebug($"{actionEffect.Type} Threshold not met");
                    return; // Use return instead of continue in lambda expressions
                }

                // Execute trigger action if all conditions are met
                Logger.LogDebug($"{actionEffect.Type} Action Triggered");
                ExecuteTriggerAction(trigger);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error processing trigger");
            }
        };
    }

    private void CheckActiveRestraintTriggers(RestraintSetToggledMessage msg)
    {
        // if the state is unlock or remove, we should return.
        if (msg.State == NewState.Unlocked || msg.State == NewState.Disabled)
            return;

        // if there is a currently active set already that is locked, disregard.
        if(_clientConfigs.GetActiveSet() != null && _clientConfigs.GetActiveSet().Locked)
            return;

        var set = _clientConfigs.GetRestraintSet(msg.SetIdx);
        // make this only allow apply and lock, especially on the setup.
        var matchingTriggers = _clientConfigs.ActiveRestraintTriggers
            .Where(trigger => trigger.RestraintSetName == set.Name && trigger.RestraintState == msg.State)
            .ToList();

        // if the triggers is not empty, perform logic, but return if there isnt any.
        if (!matchingTriggers.Any())
            return;

        // find the relevant trigger with the highest priority.
        var highestPriorityTrigger = matchingTriggers
            .OrderByDescending(x => x.Priority)
            .FirstOrDefault();

        // execute this trigger action.
        if (highestPriorityTrigger != null)
        {
            ExecuteTriggerAction(highestPriorityTrigger);
        }
    }

    // you can create a loophole (disable gag => action to equip gag) with this but oh well. For creativity.
    private void CheckGagStateTriggers(GagTypeChanged msg)
    {
        NewState newState = msg.NewGagType == GagList.GagType.None ? NewState.Disabled : NewState.Enabled;
        // Check to see if any active gag triggers are in the message
        var matchingTriggers = _clientConfigs.ActiveGagStateTriggers
            .Where(x => x.Gag == msg.NewGagType && x.GagState == newState)
            .ToList();        

        // if the triggers is not empty, perform logic, but return if there isnt any.
        if (!matchingTriggers.Any())
            return;

        // find the relevant trigger with the highest priority.
        var highestPriorityTrigger = matchingTriggers
            .OrderByDescending(x => x.Priority)
            .FirstOrDefault();

        // execute this trigger action.
        if (highestPriorityTrigger != null)
        {
            ExecuteTriggerAction(highestPriorityTrigger);
        }
    }

    private async void ExecuteTriggerAction(Trigger trigger)
    {
        Logger.LogInformation("Your Trigger With Name {name} and priority {priority} is now triggering action {action}",
            trigger.Name, trigger.Priority, trigger.TriggerActionKind.ToName());

        switch (trigger.TriggerActionKind)
        {
            case TriggerActionKind.SexToy:
                _vibeService.DeviceHandler.ExecuteVibeTrigger(trigger);
                Logger.LogInformation("Vibe Trigger Executed");
                break;

            case TriggerActionKind.ShockCollar:
                if(_playerManager.GlobalPerms == null || _playerManager.GlobalPerms.GlobalShockShareCode.IsNullOrEmpty() || _playerManager.GlobalPiShockPerms.MaxIntensity == -1)
                {
                    Logger.LogError("Cannot apply a shock collar action without global permissions set.\n These are used for Trigger Limitations.");
                }
                _vibeService.ExecuteShockAction(_playerManager.GlobalPerms!.GlobalShockShareCode, trigger.ShockTriggerAction);
                Logger.LogInformation("Applied Shock Collar action.");
                break;

            case TriggerActionKind.Restraint:
                // if a set is active and already locked, do not execute, and log error.
                if (_clientConfigs.GetActiveSet() != null && _clientConfigs.GetActiveSet().Locked)
                {
                    Logger.LogError("Cannot apply a restraint set while another is active and locked.");
                    return;
                }
                Logger.LogInformation("Applying Restraint Set {restraintName} with state {state}", trigger.RestraintNameAction, NewState.Enabled);
                var idx = _clientConfigs.GetRestraintSetIdxByName(trigger.RestraintNameAction);
                await _clientConfigs.SetRestraintSetState(idx, "SelfApplied", NewState.Enabled, true);
                break;

            case TriggerActionKind.Gag:
                if (_playerManager.AppearanceData == null) return;
                // if a gag on the layer we want to apply to is already equipped and locked, do not execute, and log error.
                if (_playerManager.AppearanceData.GagSlots[(int)trigger.GagLayerAction].GagType != GagList.GagType.None.GetGagAlias()
                 && _playerManager.AppearanceData.GagSlots[(int)trigger.GagLayerAction].Padlock != Padlocks.None.ToString())
                {
                    Logger.LogError("Cannot apply a gag while another is active and locked.");
                    return;
                }
                // otherwise, we can change the gag type on that layer.
                Logger.LogInformation("Applying Gag Type {gagType} to Layer {gagLayer}", trigger.GagTypeAction, trigger.GagLayerAction);
                Mediator.Publish(new GagTypeChanged(trigger.GagTypeAction, trigger.GagLayerAction));
                break;

            case TriggerActionKind.Moodle:
                if(!_moodlesIpc.APIAvailable)
                {
                    Logger.LogError("Moodles IPC is not available, cannot execute moodle trigger.");
                    return;
                }
                // see if the moodle status guid exists in our list of stored statuses.
                if(_playerManager.LastIpcData != null && _playerManager.LastIpcData.MoodlesStatuses.Any(x => x.GUID == trigger.MoodlesIdentifier))
                {
                    // we have a valid moodle to set, so go ahead and try to apply it!
                    Logger.LogInformation("Applying moodle status with GUID {guid}", trigger.MoodlesIdentifier);
                    await _moodlesIpc.ApplyOwnStatusByGUID(new List<Guid>() { trigger.MoodlesIdentifier });
                    return;
                }
                break;

            case TriggerActionKind.MoodlePreset:
                if(!_moodlesIpc.APIAvailable)
                {
                    Logger.LogError("Moodles IPC is not available, cannot execute moodle trigger.");
                    return;
                }
                // see if the moodle preset guid exists in our list of stored presets.
                if(_playerManager.LastIpcData != null && _playerManager.LastIpcData.MoodlesPresets.Any(x => x.Item1 == trigger.MoodlesIdentifier))
                {
                    // we have a valid moodle to set, so go ahead and try to apply it!
                    Logger.LogInformation("Applying Moodle preset with GUID {guid}", trigger.MoodlesIdentifier);
                    await _moodlesIpc.ApplyOwnPresetByGUID(trigger.MoodlesIdentifier);
                    return;
                }
                Logger.LogDebug("Moodle preset with GUID {guid} not found in the list of presets.", trigger.MoodlesIdentifier);
                break;
        }
    }


    #region Controller Helper Methods & Update Checks
    public static string TrackedPlayersString()
    {
        return string.Join(Environment.NewLine, MonitoredPlayers.Select(x =>
            $"{x.PlayerNameWithWorld}\n-- HP: {x.CurrentHp}\n-- Max HP: {x.MaxHp}"));
    }

    public async void OnActionEffectEvent(List<ActionEffectEntry> actionEffects)
    {
        if (_clientState.LocalPlayer == null)
        {
            Logger.LogWarning("Not Processing Action Effects while player is null!");
            return;
        }

        try
        {
            await _frameworkService.RunOnFrameworkThread(() =>
            {
                foreach (var actionEffect in actionEffects)
                {
                    if (_clientConfigs.GagspeakConfig.LogActionEffects)
                    {
                        // Perform logging and action processing for each effect
                        var sourceCharaStr = (_frameworkService.SearchObjectTableById(actionEffect.SourceID) as IPlayerCharacter)?.GetNameWithWorld() ?? "UNKN OBJ";
                        var targetCharaStr = (_frameworkService.SearchObjectTableById(actionEffect.TargetID) as IPlayerCharacter)?.GetNameWithWorld() ?? "UNKN OBJ";
                        var actionStr = _gameData.GetExcelSheet<GameAction>()!.GetRow(actionEffect.ActionID)?.Name.ToString() ?? "UNKN ACT";
                        Logger.LogDebug($"Source:{sourceCharaStr}, Target: {targetCharaStr}, Action: {actionStr}, Action ID:{actionEffect.ActionID}, Type: {actionEffect.Type.ToString()} Amount: {actionEffect.Damage}");
                    }
                    CheckSpellActionTriggers(actionEffect);
                };
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error during ActionEffectEvent");
        }
    }


    private void UpdateTriggerMonitors()
    {
        // update the monitors for ActionSpell Triggers.
        if (ShouldEnableActionEffectHooks)
        {
            _receiveActionEffectHookManager.EnableHook();
        }
        else
        {
            _receiveActionEffectHookManager.DisableHook();
        }

        try
        {
            if (_clientConfigs.ActiveHealthPercentTriggers.Any())
            {
                // get the list of players to monitor.
                var activeTriggerPlayersToMonitor = _clientConfigs.ActiveHealthPercentTriggers.Select(x => x.PlayerToMonitor).Distinct().ToList();
                var activelyMonitoredPlayers = MonitoredPlayers.Select(x => x.PlayerNameWithWorld).ToList();
                var visiblePlayerCharacters = _frameworkService.GetObjectTablePlayers().Where(player => activeTriggerPlayersToMonitor.Contains(player.GetNameWithWorld()));

                // if any of the monitored players are not visible, we should remove them from the monitored list.
                // in other words, remove all monitored players whose PlayerNameWithWorld is not a part of the visiblePlayerCharacters.
                MonitoredPlayers.RemoveAll(x => !visiblePlayerCharacters.Any(player => player.GetNameWithWorld() == x.PlayerNameWithWorld));

                // if any of the visible players are not being monitored, we should add them to the monitored list.
                // in other words, add all visible players whose PlayerNameWithWorld is not a part of the activelyMonitoredPlayers.
                var playersToAdd = visiblePlayerCharacters.Where(player => !activelyMonitoredPlayers.Contains(player.GetNameWithWorld()));
                foreach (var player in playersToAdd)
                {
                    MonitoredPlayers.Add(new MonitoredPlayerState(player));
                }
            }
            else
            {
                MonitoredPlayers.Clear();
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error during UpdateTriggerMonitors");
        }
    }

    private void UpdateTrackedPlayerHealth()
    {
        try
        {
            if (!MonitoredPlayers.Any()) return;

            // By grouping triggers into a dictionary where the key is the player name, we reduce the need to filter triggers multiple times.
            var triggersByPlayer = _clientConfigs.ActiveHealthPercentTriggers
                .GroupBy(t => t.PlayerToMonitor)
                .ToDictionary(g => g.Key, g => g.ToList());

            // Each player is processed once per update, and triggers are checked efficiently using the pre-computed dictionary.
            foreach (var player in MonitoredPlayers)
            {
                if (!player.HasHpChanged()) continue;

                // Logger.LogDebug("Hp Changed from {PreviousHp} to {CurrentHp}", player.PreviousHp, player.CurrentHp);
                
                // Calculate health percentages once per player to avoid redundancies.
                float percentageHP = player.CurrentHp * 100f / player.MaxHp;
                float previousPercentageHP = player.PreviousHp * 100f / player.PreviousMaxHp;

                if (triggersByPlayer.TryGetValue(player.PlayerNameWithWorld, out var triggers))
                {
                    // Process each trigger for this player
                    foreach (var trigger in triggers)
                    {
                        bool isValid = false;

                        // Check if health thresholds are met based on trigger type
                        if (trigger.PassKind == ThresholdPassType.Under)
                        {
                            isValid = trigger.UsePercentageHealth
                                ? (previousPercentageHP > trigger.MinHealthValue && percentageHP <= trigger.MinHealthValue) ||
                                  (previousPercentageHP > trigger.MaxHealthValue && percentageHP <= trigger.MaxHealthValue)
                                : (player.PreviousHp > trigger.MinHealthValue && player.CurrentHp <= trigger.MinHealthValue) ||
                                  (player.PreviousHp > trigger.MaxHealthValue && player.CurrentHp <= trigger.MaxHealthValue);
                        }
                        else if (trigger.PassKind == ThresholdPassType.Over)
                        {
                            isValid = trigger.UsePercentageHealth
                                ? (previousPercentageHP < trigger.MinHealthValue && percentageHP >= trigger.MinHealthValue) ||
                                  (previousPercentageHP < trigger.MaxHealthValue && percentageHP >= trigger.MaxHealthValue)
                                : (player.PreviousHp < trigger.MinHealthValue && player.CurrentHp >= trigger.MinHealthValue) ||
                                  (player.PreviousHp < trigger.MaxHealthValue && player.CurrentHp >= trigger.MaxHealthValue);
                        }

                        if (isValid)
                        {
                            ExecuteTriggerAction(trigger);
                        }
                    }
                }

                // update the HP
                player.UpdateHpChange();
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error during UpdateTrackedPlayerHealth");
        }
    }
    #endregion Controller Helper Methods & Update Checks
}
