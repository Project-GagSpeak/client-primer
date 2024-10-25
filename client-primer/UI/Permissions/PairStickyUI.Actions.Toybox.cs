using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using GagSpeak.Utils;
using GagSpeak.WebAPI;
using GagSpeak.WebAPI.Utils;
using GagspeakAPI.Data;
using GagspeakAPI.Data.Character;
using GagspeakAPI.Dto.Permissions;
using GagspeakAPI.Enums;
using ImGuiNET;
using OtterGui;
using System.Numerics;

namespace GagSpeak.UI.Permissions;

/// <summary>
/// Contains functions relative to the paired users permissions for the client user.
/// 
/// Yes its messy, yet it's long, but i functionalized it best i could for the insane 
/// amount of logic being performed without adding too much overhead.
/// </summary>
public partial class PairStickyUI
{
    private void DrawToyboxActions()
    {
        var lastToyboxData = UserPairForPerms.LastReceivedToyboxData;
        if (lastToyboxData is null) 
            return;

        bool openVibeRemoteDisabled = !UserPairForPerms.OnlineToyboxUser || !PairPerms.CanUseVibeRemote;
        bool patternExecuteDisabled = !PairPerms.CanExecutePatterns || !UserPairForPerms.UserPairGlobalPerms.ToyIsActive || !lastToyboxData.PatternList.Any();
        bool patternStopDisabled = !PairPerms.CanStopPatterns || !UserPairForPerms.UserPairGlobalPerms.ToyIsActive || !lastToyboxData.PatternList.Any(x => x.Enabled);
        bool alarmToggleDisabled = !PairPerms.CanToggleAlarms || !lastToyboxData.AlarmList.Any();
        bool alarmSendDisabled = !PairPerms.CanSendAlarms;
        bool triggerToggleDisabled = !PairPerms.CanToggleTriggers || !lastToyboxData.TriggerList.Any();

        ////////// TOGGLE PAIRS ACTIVE TOYS //////////
        string toyToggleText = UserPairForPerms.UserPairGlobalPerms.ToyIsActive ? "Turn Off " + PairUID + "'s Toys" : "Turn On " + PairUID + "'s Toys";
        if (_uiShared.IconTextButton(FontAwesomeIcon.User, toyToggleText, WindowMenuWidth, true))
        {
            _ = _apiHubMain.UserUpdateOtherGlobalPerm(new UserGlobalPermChangeDto(UserPairForPerms.UserData,
                new KeyValuePair<string, object>("ToyIsActive", !UserPairForPerms.UserPairGlobalPerms.ToyIsActive), MainHub.PlayerUserData));
            _logger.LogDebug("Toggled Toybox for " + PairUID + "(New State: " + !UserPairForPerms.UserPairGlobalPerms.ToyIsActive + ")", LoggerType.Permissions);
        }
        UiSharedService.AttachToolTip("Toggles the state of " + PairUID + "'s connected Toys.");

        ////////// OPEN VIBE REMOTE WITH PAIR //////////
        if (_uiShared.IconTextButton(FontAwesomeIcon.Mobile, "Create Vibe Remote with " + PairNickOrAliasOrUID, WindowMenuWidth, true, openVibeRemoteDisabled))
        {
            // open a new private hosted room between the two of you automatically.
            // figure out how to do this later.
            _logger.LogDebug("Vibe Remote instance button pressed for " + PairNickOrAliasOrUID);
        }
        UiSharedService.AttachToolTip("Open a Remote UI that let's you control " + PairNickOrAliasOrUID + "'s Toys.");

        ////////// EXECUTE PATTERN ON PAIR'S TOY //////////
        if (_uiShared.IconTextButton(FontAwesomeIcon.PlayCircle, ("Activate " + PairUID + "'s Patterns"), WindowMenuWidth, true, patternExecuteDisabled))
        {
            Opened = (Opened == InteractionType.ActivatePattern) ? InteractionType.None : InteractionType.ActivatePattern;
        }
        UiSharedService.AttachToolTip("Play one of " + PairUID + "'s patterns to their active Toy.");
        if (Opened is InteractionType.ActivatePattern)
        {
            using (var actionChild = ImRaii.Child("PatternExecuteChild", new Vector2(WindowMenuWidth, ImGui.GetFrameHeight()), false))
            {
                if (!actionChild) return;

                // Grab the currently stored selected PatternDto.
                PatternDto storedPatternName = _permActions.GetSelectedItem<PatternDto>("ExecutePatternForPairPermCombo", PairUID) ?? new PatternDto();

                _permActions.DrawGenericComboButton(PairUID, "ExecutePatternForPairPermCombo", "Play", WindowMenuWidth,
                    comboItems: lastToyboxData.PatternList, 
                    itemToName: (Pattern) => Pattern.Name + "(" + Pattern.Duration.Minutes + "m + " + Pattern.Duration.Seconds + "s)" + (Pattern.ShouldLoop ? " Loop" : ""),
                    isSearchable: true, 
                    buttonDisabled: storedPatternName.Identifier == Guid.Empty,
                    isIconButton: true,
                    initialSelectedItem: lastToyboxData.PatternList.FirstOrDefault(x => x.Enabled) ?? lastToyboxData.PatternList.First(),
                    icon: FontAwesomeIcon.Play,
                    onSelected: (selected) => { _logger.LogDebug("Selected Pattern Set: " + selected, LoggerType.Permissions); },
                    onButton: (onButtonPress) =>
                    {
                        try
                        {
                            var newToyboxData = lastToyboxData.DeepClone();
                            if (newToyboxData is null || onButtonPress is null) throw new Exception("Toybox data is null, not sending");
                            // set all other stored patterns active state to false, and the pattern with the onButtonPress matching GUID to true.
                            newToyboxData.TransactionId = onButtonPress.Identifier;

                            // Run the call to execute the pattern to the server.
                            _ = _apiHubMain.UserPushPairDataToyboxUpdate(new(UserPairForPerms.UserData, newToyboxData, DataUpdateKind.ToyboxPatternExecuted));
                            _logger.LogDebug("Executing Pattern " + onButtonPress.Name + " on " + PairNickOrAliasOrUID + "'s Toy", LoggerType.Permissions);
                            Opened = InteractionType.None;
                        }
                        catch (Exception e) { _logger.LogError("Failed to push updated ToyboxPattern data: " + e.Message); }
                    });
            }
            ImGui.Separator();
        }

        ////////// STOP RUNNING PATTERN ON PAIR'S TOY //////////
        if (_uiShared.IconTextButton(FontAwesomeIcon.StopCircle, "Stop " + PairNickOrAliasOrUID + "'s Active Pattern", WindowMenuWidth, true, patternStopDisabled))
        {
            try
            {
                var newToyboxData = lastToyboxData.DeepClone();
                if (newToyboxData == null) throw new Exception("Toybox data is null, not sending");

                _ = _apiHubMain.UserPushPairDataToyboxUpdate(new(UserPairForPerms.UserData, newToyboxData, DataUpdateKind.ToyboxPatternStopped));
                _logger.LogDebug("Stopped active Pattern running on "+PairNickOrAliasOrUID+"'s toy", LoggerType.Permissions);
            }
            catch (Exception e) { _logger.LogError("Failed to push updated ToyboxPattern data: " + e.Message); }
        }
        UiSharedService.AttachToolTip("Halt the active pattern on " + PairUID + "'s Toy");

        ////////// TOGGLE ALARM FOR PAIR //////////
        if (_uiShared.IconTextButton(FontAwesomeIcon.Clock, "Toggle " + PairNickOrAliasOrUID + "'s Alarms", WindowMenuWidth, true, alarmToggleDisabled))
        {
            Opened = Opened == InteractionType.ToggleAlarm ? InteractionType.None : InteractionType.ToggleAlarm;
        }
        UiSharedService.AttachToolTip("Toggle " + PairUID + "'s Alarms.");
        if (Opened is InteractionType.ToggleAlarm)
        {
            using (var actionChild = ImRaii.Child("AlarmToggleChild", new Vector2(WindowMenuWidth, ImGui.GetFrameHeight()), false))
            {
                if (!actionChild) return;
                
                
                AlarmDto selectedAlarm = _permActions.GetSelectedItem<AlarmDto>("ToggleAlarmForPairPermCombo", PairUID) ?? new AlarmDto();
                
                _permActions.DrawGenericComboButton(PairUID, "ExecutePatternForPairPermCombo", (selectedAlarm.Enabled ? "Disable" : "Enable"), WindowMenuWidth, 
                    comboItems: lastToyboxData.AlarmList, 
                    itemToName: (Alarm) => Alarm.Name + " (" + (Alarm.SetTimeUTC.ToLocalTime().ToString("HH:mm")) + ") (Plays: " + Alarm.PatternThatPlays + ")",
                    isSearchable: true,
                    buttonDisabled: selectedAlarm.Name == string.Empty,
                    isIconButton: false,
                    initialSelectedItem: lastToyboxData.AlarmList.FirstOrDefault(x => x.Name == selectedAlarm.Name) ?? lastToyboxData.AlarmList.First(),
                    onSelected: (selected) => { _logger.LogDebug("Selected Alarm: " + selected?.Name); },
                    onButton: (onButtonPress) =>
                    {
                        try
                        {
                            if (onButtonPress is null) throw new Exception("Alarm is null, not sending");
                            var newToyboxData = lastToyboxData.DeepClone();
                            var alarmToToggle = newToyboxData.AlarmList.FirstOrDefault(x => x.Identifier == onButtonPress.Identifier);
                            if (alarmToToggle is null) throw new Exception("Alarm not found in list.");
                            // toggle the alarm state.
                            alarmToToggle.Enabled = !alarmToToggle.Enabled;
                            newToyboxData.TransactionId = onButtonPress.Identifier;
                            // Send out the command.
                            _ = _apiHubMain.UserPushPairDataToyboxUpdate(new(UserPairForPerms.UserData, newToyboxData, DataUpdateKind.ToyboxAlarmToggled));
                            _logger.LogDebug("Toggling Alarm " + onButtonPress.Name + " on " + PairNickOrAliasOrUID + "'s AlarmList", LoggerType.Permissions);
                            Opened = InteractionType.None;
                        }
                        catch (Exception e) { _logger.LogError("Failed to push updated ToyboxPattern data: " + e.Message); }
                    });
            }
            ImGui.Separator();
        }

        ////////// TOGGLE TRIGGER FOR PAIR //////////
        if (_uiShared.IconTextButton(FontAwesomeIcon.LandMineOn, "Toggle " + PairNickOrAliasOrUID + "'s Triggers", WindowMenuWidth, true, triggerToggleDisabled))
        {
            Opened = Opened == InteractionType.ToggleTrigger ? InteractionType.None : InteractionType.ToggleTrigger;
        }
        UiSharedService.AttachToolTip("Toggle the state of a trigger in " + PairNickOrAliasOrUID + "'s triggerList.");
        if (Opened is InteractionType.ToggleTrigger)
        {
            using (var actionChild = ImRaii.Child("TriggerToggleChild", new Vector2(WindowMenuWidth, ImGui.GetFrameHeight()), false))
            {
                if (!actionChild) return;

                TriggerDto selected = _permActions.GetSelectedItem<TriggerDto>("ToggleTriggerForPairPermCombo", PairUID) ?? new TriggerDto();

                _permActions.DrawGenericComboButton(PairUID, "ToggleTriggerForPairPermCombo", selected.Enabled ? "Disable" : "Enable", WindowMenuWidth,
                   comboItems: lastToyboxData.TriggerList,
                   itemToName: (Trigger) => Trigger.Name + " (Type: " + Trigger.Type.TriggerKindToString()+ ") (" + Trigger.ActionOnTrigger.ToName() + ")",
                   isSearchable: true,
                   buttonDisabled: selected.Name == string.Empty,
                   onSelected: (onSelected) => { _logger.LogDebug("Selected Trigger: " + onSelected?.Name ?? "UNK NAME", LoggerType.Permissions); },
                   onButton: (onButtonPress) =>
                    {
                        try
                        {
                            if (onButtonPress is null) throw new Exception("Trigger is null, not sending");
                            var newToyboxData = lastToyboxData.DeepClone();
                            var triggerToToggle = newToyboxData.TriggerList.FirstOrDefault(x => x.Identifier == onButtonPress.Identifier);
                            if (triggerToToggle is null) throw new Exception("Trigger not found in list.");
                            // toggle the trigger state.
                            triggerToToggle.Enabled = !triggerToToggle.Enabled;
                            newToyboxData.TransactionId = onButtonPress.Identifier;
                            _ = _apiHubMain.UserPushPairDataToyboxUpdate(new(UserPairForPerms.UserData, newToyboxData, DataUpdateKind.ToyboxTriggerToggled));
                            _logger.LogDebug("Toggling Trigger "+onButtonPress.Name+" on "+PairNickOrAliasOrUID+"'s TriggerList", LoggerType.Permissions);
                            Opened = InteractionType.None;
                        }
                        catch (Exception e) { _logger.LogError("Failed to push updated ToyboxPattern data: " + e.Message); }
                    });
            }
        }
        ImGui.Separator();
    }
}
