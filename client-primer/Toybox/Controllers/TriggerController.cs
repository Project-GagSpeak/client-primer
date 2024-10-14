using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using GagSpeak.ChatMessages;
using GagSpeak.GagspeakConfiguration.Models;
using GagSpeak.Interop.Ipc;
using GagSpeak.PlayerData.Data;
using GagSpeak.PlayerData.Handlers;
using GagSpeak.PlayerData.Pairs;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using GagSpeak.Toybox.Data;
using GagSpeak.Toybox.Services;
using GagSpeak.UI;
using GagSpeak.UpdateMonitoring;
using GagSpeak.UpdateMonitoring.Triggers;
using GagSpeak.Utils;
using GagspeakAPI.Extensions;
using System.Text.RegularExpressions;
using GameAction = Lumina.Excel.GeneratedSheets.Action;

namespace GagSpeak.Toybox.Controllers;

// Trigger Controller helps manage the currently active triggers and listens in on the received action effects
public class TriggerController : DisposableMediatorSubscriberBase
{
    private readonly PlayerCharacterData _playerManager;
    private readonly ToyboxFactory _playerMonitorFactory;
    private readonly ActionEffectMonitor _receiveActionEffectHookManager;
    private readonly WardrobeHandler _wardrobeHandler;
    private readonly ClientConfigurationManager _clientConfigs;
    private readonly GagManager _gagManager;
    private readonly PairManager _pairManager;
    private readonly AppearanceHandler _appearanceHandler;
    private readonly OnFrameworkService _frameworkService;
    private readonly ToyboxVibeService _vibeService;
    private readonly IpcCallerMoodles _moodlesIpc;
    private readonly IChatGui _chatGui;
    private readonly IClientState _clientState;
    private readonly IDataManager _gameData;

    public TriggerController(ILogger<TriggerController> logger, GagspeakMediator mediator,
        PlayerCharacterData playerManager, ToyboxFactory playerMonitorFactory,
        ActionEffectMonitor receiveActionEffectHookManager, WardrobeHandler wardrobeHandler,
        ClientConfigurationManager clientConfigs, GagManager gagManager,
        PairManager pairManager, AppearanceHandler appearanceHandler, 
        OnFrameworkService frameworkService, ToyboxVibeService vibeService,
        IpcCallerMoodles moodlesIpc, IChatGui chatGui, IClientState clientState,
        IDataManager gameData) : base(logger, mediator)
    {
        _playerManager = playerManager;
        _playerMonitorFactory = playerMonitorFactory;
        _receiveActionEffectHookManager = receiveActionEffectHookManager;
        _wardrobeHandler = wardrobeHandler;
        _clientConfigs = clientConfigs;
        _gagManager = gagManager;
        _pairManager = pairManager;
        _appearanceHandler = appearanceHandler;
        _frameworkService = frameworkService;
        _vibeService = vibeService;
        _moodlesIpc = moodlesIpc;
        _chatGui = chatGui;
        _clientState = clientState;
        _gameData = gameData;

        ActionEffectMonitor.ActionEffectEntryEvent += OnActionEffectEvent;

        Mediator.Subscribe<RestraintSetToggledMessage>(this, (msg) => CheckActiveRestraintTriggers(msg));

        Mediator.Subscribe<GagTypeChanged>(this, (msg) =>
        {

            NewState newState = msg.NewGagType == GagType.None ? NewState.Disabled : NewState.Enabled;
            CheckGagStateTriggers(msg.NewGagType, newState);
        });

        Mediator.Subscribe<DelayedFrameworkUpdateMessage>(this, (_) => UpdateTriggerMonitors());

        Mediator.Subscribe<FrameworkUpdateMessage>(this, (_) => UpdateTrackedPlayerHealth());
    }

    // make the last interaction the interaction that has the DateTime LastRoll value closest to the UtcNow value.
    private List<DeathRollSession> ActiveDeathDeathRollSessions = new List<DeathRollSession>();
    public DeathRollSession? LastInteractedSession => ActiveDeathDeathRollSessions.OrderBy(x => Math.Abs((x.LastRoll - DateTime.UtcNow).TotalMilliseconds)).FirstOrDefault();
    public bool AnyDeathRollSessionsActive => ActiveDeathDeathRollSessions.Any();
    public int? LatestSessionCapNumber => (LastInteractedSession != null) ? LastInteractedSession.CurrentRollCap : null;

    public DeathRollSession? GetLastInteractedSession(string playerNameWithWorld)
    {
        return ActiveDeathDeathRollSessions
            .OrderBy(x => Math.Abs((x.LastRoll - DateTime.UtcNow).TotalMilliseconds))
            .Where(x => x.Initializer == playerNameWithWorld || (x.Opponent == playerNameWithWorld || x.Opponent == string.Empty))
            .FirstOrDefault();
    }


    public static List<MonitoredPlayerState> MonitoredPlayers { get; private set; } = new List<MonitoredPlayerState>();
    private bool ShouldProcessActionEffectHooks => _clientConfigs.ActiveTriggers.Any(x => x.Type is TriggerKind.SpellAction);

    protected override void Dispose(bool disposing)
    {
        ActionEffectMonitor.ActionEffectEntryEvent -= OnActionEffectEvent;
        base.Dispose(disposing);
    }

    public void CheckActiveChatTriggers(XivChatType chatType, string senderNameWithWorld, string message)
    {
        // if the sender name is ourselves, ignore the message.
        if (senderNameWithWorld == _clientState.LocalPlayer?.GetNameWithWorld()) return;

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
        if (!matchingTriggers.Any())
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
                    Logger.LogDebug("Direction didn't match", LoggerType.ToyboxTriggers);
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
                    Logger.LogDebug($"{actionEffect.Type} Threshold not met", LoggerType.ToyboxTriggers);
                    return; // Use return instead of continue in lambda expressions
                }

                // Execute trigger action if all conditions are met
                Logger.LogDebug($"{actionEffect.Type} Action Triggered", LoggerType.ToyboxTriggers);
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
        if (_clientConfigs.GetActiveSet() != null && _clientConfigs.GetActiveSet().Locked)
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

    private void CheckGagStateTriggers(GagType gagType, NewState newState)
    {
        // Check to see if any active gag triggers are in the message
        var matchingTriggers = _clientConfigs.ActiveGagStateTriggers
            .Where(x => x.Gag == gagType && x.GagState == newState)
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

    public void CheckActiveSocialTriggers(XivChatType type, string nameWithWorld, SeString sender, SeString message)
    {
        // if it doesnt contain an Dice Icon its not valid. Can't check for "Dice" because of linguistic differences.
        if (!(message.Payloads.Find(pay => pay.Type == PayloadType.Icon) != null)) return;

        // if the triggers is not empty, perform logic, but return if there isnt any.
        if (_clientState.LocalPlayer == null) return;

        // We have a DeathRoll Trigger active, so handle the message.
        HandleDeathRollMessage(message.TextValue, out bool InitializerMsg, out int RollValue, out int RollCap);

        // if initializing a new DeathRoll session
        if (InitializerMsg)
        {
            ActiveDeathDeathRollSessions.RemoveAll(x => x.Initializer == nameWithWorld);
            Logger.LogDebug("[DeathRoll] New DeathRoll session created by "+nameWithWorld, LoggerType.ToyboxTriggers);
            ActiveDeathDeathRollSessions.Add(new DeathRollSession(nameWithWorld, RollCap));
        }
        // if its not an Initializer message, but we are responding to someone else's Roll Session, find the session and roll.
        else if (!InitializerMsg && RollValue != int.MaxValue && RollCap != int.MaxValue)
        {
            // we don't necessarily need to be set as the opponent yet, but the RollCap must match the sessions current rollCap.
            var matchedSession = ActiveDeathDeathRollSessions.FirstOrDefault(x => !x.IsComplete && !x.SessionExpired && x.CurrentRollCap == RollCap);
            if (matchedSession != null)
            {
                if (matchedSession.TryNextRoll(nameWithWorld, RollValue, RollCap))
                {
                    Logger.LogDebug("[DeathRoll] Rolled in active Session with "+RollValue+" (out of "+RollCap+")", LoggerType.ToyboxTriggers);
                }
                else
                {
                    Logger.LogDebug("[DeathRoll] Roll not processed, as you are not part of the session.", LoggerType.ToyboxTriggers);
                }
            }
        }
        else
        {
            Logger.LogDebug("[DeathRoll] Roll doesn't match any active Sessions.", LoggerType.ToyboxTriggers);
        }

        var matchingDeathRollTriggers = _clientConfigs.ActiveSocialTriggers.Where(x => x.SocialType == SocialActionType.DeathRollLoss).ToList();

        if (!matchingDeathRollTriggers.Any())
            return;

        // if there are any active DeathRolls that are marked as complete, not expired...
        var completedLostDeathRolls = ActiveDeathDeathRollSessions
            .Where(x => x.IsComplete && !x.SessionExpired && (
            (x.Initializer == _clientState.LocalPlayer.GetNameWithWorld() && x.LastRoller == LatestRoller.Initializer)
            || (x.Opponent == _clientState.LocalPlayer.GetNameWithWorld() && x.LastRoller == LatestRoller.Opponent)))
            .ToList();

        // if there are any completedLostDeathRolls that exist, we should fire our trigger, and clear the rollSession.
        if (completedLostDeathRolls.Any())
        {
            SeStringBuilder se = new SeStringBuilder().AddItalicsOn().AddText("[Gagspeak] You Lost a DeathRoll!").AddItalicsOff();
            _chatGui.PrintError(se.BuiltString);

            foreach (var trigger in matchingDeathRollTriggers)
            {
                ExecuteTriggerAction(trigger);
            }
            UnlocksEventManager.AchievementEvent(UnlocksEvent.DeathRollCompleted);
            ActiveDeathDeathRollSessions.RemoveAll(x => completedLostDeathRolls.Contains(x));
            Logger.LogDebug("DeathRoll Trigger Executed, and Session Removed.", LoggerType.ToyboxTriggers);
        }
    }

    private void HandleDeathRollMessage(string messageValue, out bool InitializerMsg, out int RollValue, out int RollCap)
    {
        // determine if this is an Initializer message or continuation message:
        InitializerMsg = (!messageValue.Contains("(") && !messageValue.Contains(")"));
        RollCap = int.MaxValue;
        RollValue = int.MaxValue;

        // Use regex to extract all numbers from the message.
        MatchCollection matches = Regex.Matches(messageValue, @"\d+");

        // If it's an initializer message, we only expect a single roll value (i.e., the initial roll).
        if (InitializerMsg && matches.Count > 0)
        {
            RollCap = int.Parse(matches[0].Value);
        }
        // If it's a continuation message, we expect both a rolled value and a roll cap.
        else if (!InitializerMsg && matches.Count == 2)
        {
            RollValue = int.Parse(matches[0].Value);   // The first number is the rolled value.
            RollCap = int.Parse(matches[1].Value);  // The second number is the roll cap (after "out of").
        }
    }

    private async void ExecuteTriggerAction(Trigger trigger)
    {
        Logger.LogInformation("Your Trigger With Name "+trigger.Name+" and priority "+trigger.Priority+" triggering action "
            + trigger.TriggerActionKind.ToName(), LoggerType.ToyboxTriggers);

        switch (trigger.TriggerActionKind)
        {
            case TriggerActionKind.SexToy:
                _vibeService.DeviceHandler.ExecuteVibeTrigger(trigger);
                UnlocksEventManager.AchievementEvent(UnlocksEvent.TriggerFired);
                Logger.LogInformation("Vibe Trigger Executed", LoggerType.ToyboxTriggers);
                break;

            case TriggerActionKind.ShockCollar:
                if (_playerManager.GlobalPerms == null || _playerManager.GlobalPerms.GlobalShockShareCode.IsNullOrEmpty() || _playerManager.GlobalPiShockPerms.MaxIntensity == -1)
                {
                    Logger.LogError("Cannot apply a shock collar action without global permissions set.\n These are used for Trigger Limitations.");
                }
                _vibeService.ExecuteShockAction(_playerManager.GlobalPerms!.GlobalShockShareCode, trigger.ShockTriggerAction);
                UnlocksEventManager.AchievementEvent(UnlocksEvent.TriggerFired);
                Logger.LogInformation("Applied Shock Collar action.", LoggerType.ToyboxTriggers);
                break;

            case TriggerActionKind.Restraint:
                // if a set is active and already locked, do not execute, and log error.
                if (_clientConfigs.GetActiveSet() != null && _clientConfigs.GetActiveSet().Locked)
                {
                    Logger.LogError("Cannot apply a restraint set while another is active and locked.");
                    return;
                }
                Logger.LogInformation("Applying Restraint Set "+trigger.RestraintNameAction+" with state "+NewState.Enabled, LoggerType.ToyboxTriggers);
                var idx = _clientConfigs.GetRestraintSetIdxByName(trigger.RestraintNameAction);
                await _wardrobeHandler.EnableRestraintSet(idx, Globals.SelfApplied, true);
                UnlocksEventManager.AchievementEvent(UnlocksEvent.TriggerFired);
                break;

            case TriggerActionKind.Gag:
                if (_playerManager.AppearanceData == null) return;
                // if a gag on the layer we want to apply to is already equipped and locked, do not execute, and log error.
                if (_playerManager.AppearanceData.GagSlots[(int)trigger.GagLayerAction].GagType != GagType.None.GagName()
                 && _playerManager.AppearanceData.GagSlots[(int)trigger.GagLayerAction].Padlock != Padlocks.None.ToName())
                {
                    Logger.LogError("Cannot apply a gag while another is active and locked.");
                    return;
                }
                // otherwise, we can change the gag type on that layer.
                Logger.LogInformation("Applying Gag Type "+trigger.GagTypeAction+" to layer "+trigger.GagLayerAction);
                await _appearanceHandler.GagApplied(trigger.GagLayerAction, trigger.GagTypeAction, isSelfApplied: true);
                UnlocksEventManager.AchievementEvent(UnlocksEvent.TriggerFired);
                break;

            case TriggerActionKind.Moodle:
                if (!IpcCallerMoodles.APIAvailable)
                {
                    Logger.LogError("Moodles IPC is not available, cannot execute moodle trigger.");
                    return;
                }
                // see if the moodle status guid exists in our list of stored statuses.
                if (_playerManager.LastIpcData != null && _playerManager.LastIpcData.MoodlesStatuses.Any(x => x.GUID == trigger.MoodlesIdentifier))
                {
                    // we have a valid moodle to set, so go ahead and try to apply it!
                    Logger.LogInformation("Applying moodle status with GUID "+trigger.MoodlesIdentifier, LoggerType.ToyboxTriggers);
                    await _moodlesIpc.ApplyOwnStatusByGUID(new List<Guid>() { trigger.MoodlesIdentifier });
                    UnlocksEventManager.AchievementEvent(UnlocksEvent.TriggerFired);
                    return;
                }
                break;

            case TriggerActionKind.MoodlePreset:
                if (!IpcCallerMoodles.APIAvailable)
                {
                    Logger.LogError("Moodles IPC is not available, cannot execute moodle trigger.");
                    return;
                }
                // see if the Moodle preset guid exists in our list of stored presets.
                if (_playerManager.LastIpcData != null && _playerManager.LastIpcData.MoodlesPresets.Any(x => x.Item1 == trigger.MoodlesIdentifier))
                {
                    // we have a valid Moodle to set, so go ahead and try to apply it!
                    Logger.LogInformation("Applying Moodle preset with GUID "+trigger.MoodlesIdentifier, LoggerType.ToyboxTriggers);
                    await _moodlesIpc.ApplyOwnPresetByGUID(trigger.MoodlesIdentifier);
                    UnlocksEventManager.AchievementEvent(UnlocksEvent.TriggerFired);
                    return;
                }
                Logger.LogDebug("Moodle preset with GUID "+trigger.MoodlesIdentifier+" not found in the list of presets.", LoggerType.ToyboxTriggers);
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

        if (!ShouldProcessActionEffectHooks) // do not process the effect if we are not supposed to.
            return;

        try
        {
            await _frameworkService.RunOnFrameworkThread(() =>
            {
                foreach (var actionEffect in actionEffects)
                {
                    if (LoggerFilter.FilteredCategories.Contains(LoggerType.ActionEffects))
                    {
                        // Perform logging and action processing for each effect
                        var sourceCharaStr = (_frameworkService.SearchObjectTableById(actionEffect.SourceID) as IPlayerCharacter)?.GetNameWithWorld() ?? "UNKN OBJ";
                        var targetCharaStr = (_frameworkService.SearchObjectTableById(actionEffect.TargetID) as IPlayerCharacter)?.GetNameWithWorld() ?? "UNKN OBJ";
                        var actionStr = _gameData.GetExcelSheet<GameAction>()!.GetRow(actionEffect.ActionID)?.Name.ToString() ?? "UNKN ACT";
                        Logger.LogDebug($"Source:{sourceCharaStr}, Target: {targetCharaStr}, Action: {actionStr}, Action ID:{actionEffect.ActionID}, " +
                            $"Type: {actionEffect.Type.ToString()} Amount: {actionEffect.Damage}", LoggerType.ActionEffects);
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
                    MonitoredPlayers.Add(_playerMonitorFactory.CreatePlayerMonitor(player));
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

        // clean up death roll sessions.
        if (ActiveDeathDeathRollSessions.Any(x => x.SessionExpired))
        {
            Logger.LogDebug("[DeathRoll] Cleaning up expired DeathRoll Sessions.", LoggerType.ToyboxTriggers);
            ActiveDeathDeathRollSessions.RemoveAll(x => x.SessionExpired);
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
