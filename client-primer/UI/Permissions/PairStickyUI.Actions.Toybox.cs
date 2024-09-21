using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using GagSpeak.WebAPI.Utils;
using GagspeakAPI.Data;
using GagspeakAPI.Dto.Permissions;
using GagspeakAPI.Enums;
using ImGuiNET;
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
        var pairUniquePerms = UserPairForPerms.UserPairUniquePairPerms;
        if (lastToyboxData == null || pairUniquePerms == null) return;

        ////////// TOGGLE PAIRS ACTIVE TOYS //////////
        if (pairUniquePerms.CanToggleToyState)
        {
            string toyToggleText = UserPairForPerms.UserPairGlobalPerms.ToyIsActive ? "Turn Off " + PairUID + "'s Toys" : "Turn On " + PairUID + "'s Toys";
            if (_uiShared.IconTextButton(FontAwesomeIcon.User, toyToggleText, WindowMenuWidth, true))
            {
                _ = _apiController.UserUpdateOtherGlobalPerm(new UserGlobalPermChangeDto(UserPairForPerms.UserData,
                    new KeyValuePair<string, object>("ToyIsActive", !UserPairForPerms.UserPairGlobalPerms.ToyIsActive)));
                _logger.LogDebug("Toggled Toybox for " + PairUID + "(New State: " + !UserPairForPerms.UserPairGlobalPerms.ToyIsActive + ")");
            }
            UiSharedService.AttachToolTip("Toggles the state of " + PairUID + "'s connected Toys.");
        }

        ////////// OPEN VIBE REMOTE WITH PAIR //////////
        if (!UserPairForPerms.OnlineToyboxUser && pairUniquePerms.CanUseVibeRemote)
        {
            // create a permission to define if a room with this pair is established to change the text.
            string toyVibeRemoteText = "Create Vibe Remote with " + PairUID;
            if (_uiShared.IconTextButton(FontAwesomeIcon.Mobile, toyVibeRemoteText, WindowMenuWidth, true))
            {
                // open a new private hosted room between the two of you automatically.
                // figure out how to do this later.
                _logger.LogDebug("Vibe Remote instance button pressed for " + PairUID);
            }
            UiSharedService.AttachToolTip(toyVibeRemoteText + " to control " + PairUID + "'s Toys.");
        }

        ////////// TOGGLE ALARM FOR PAIR //////////
        var disableAlarms = !pairUniquePerms.CanToggleAlarms || !lastToyboxData.AlarmList.Any();
        if (_uiShared.IconTextButton(FontAwesomeIcon.Clock, "Toggle " + PairUID + "'s Alarms", WindowMenuWidth, true, disableAlarms))
        {
            Opened = Opened == ActiveActionButton.ToggleAlarm ? ActiveActionButton.None : ActiveActionButton.ToggleAlarm;
        }
        UiSharedService.AttachToolTip("Toggle " + PairUID + "'s Alarms.");
        if (Opened is ActiveActionButton.ToggleAlarm)
        {
            using (var actionChild = ImRaii.Child("AlarmToggleChild", new Vector2(WindowMenuWidth, ImGui.GetFrameHeight()), false))
            {
                if (!actionChild) return;

                AlarmInfo selectedAlarm = _permActions.GetSelectedItem<AlarmInfo>("ToggleAlarmForPairPermCombo", UserPairForPerms.UserData.UID) ?? new AlarmInfo();
                bool disabledCondition = selectedAlarm.Identifier == Guid.Empty || !lastToyboxData.AlarmList.Any();
                string buttonText = (selectedAlarm.Identifier == Guid.Empty ? "Enable Alarm " : "Disable Alarm");

                _permActions.DrawGenericComboButton(UserPairForPerms.UserData.UID, "ExecutePatternForPairPermCombo", buttonText,
                WindowMenuWidth, lastToyboxData.AlarmList, (Alarm) => Alarm.Name, true, disabledCondition, false, selectedAlarm,
                FontAwesomeIcon.None, ImGuiComboFlags.None, (selected) => { _logger.LogDebug("Selected Alarm: " + selected?.Name); },
                (onButtonPress) =>
                {
                    try
                    {
                        var newToyboxData = lastToyboxData.DeepClone();
                        if (newToyboxData == null || onButtonPress == null) throw new Exception("Toybox data is null, not sending");
                        // locate the alarm in the alarm list matching the selected alarm in on button press
                        var alarmToToggle = newToyboxData.AlarmList.IndexOf(onButtonPress);
                        if (alarmToToggle == -1) throw new Exception("Alarm not found in list.");

                        // toggle the alarm state.
                        newToyboxData.AlarmList[alarmToToggle].Enabled = !newToyboxData.AlarmList[alarmToToggle].Enabled;

                        _ = _apiController.UserPushPairDataToyboxUpdate(new(UserPairForPerms.UserData, newToyboxData, DataUpdateKind.ToyboxAlarmToggled));
                        _logger.LogDebug("Toggling Alarm {0} on {1}'s AlarmList", onButtonPress.Name, PairNickOrAliasOrUID);
                        Opened = ActiveActionButton.None;
                    }
                    catch (Exception e) { _logger.LogError("Failed to push updated ToyboxPattern data: " + e.Message); }
                });
            }
            ImGui.Separator();
        }

        ////////// EXECUTE PATTERN ON PAIR'S TOY //////////
        var disablePatternButton = !pairUniquePerms.CanExecutePatterns || !UserPairForPerms.UserPairGlobalPerms.ToyIsActive || !lastToyboxData.PatternList.Any();
        if (_uiShared.IconTextButton(FontAwesomeIcon.PlayCircle, ("Activate " + PairUID + "'s Patterns"), WindowMenuWidth, true, disablePatternButton))
        {
            Opened = Opened == ActiveActionButton.ActivatePattern ? ActiveActionButton.None : ActiveActionButton.ActivatePattern;
        }
        UiSharedService.AttachToolTip("Play one of " + PairUID + "'s patterns to their active Toy.");
        if (Opened is ActiveActionButton.ActivatePattern)
        {
            using (var actionChild = ImRaii.Child("PatternExecuteChild", new Vector2(WindowMenuWidth, ImGui.GetFrameHeight()), false))
            {
                if (!actionChild) return;

                PatternInfo storedPatternName = _permActions.GetSelectedItem<PatternInfo>("ExecutePatternForPairPermCombo", UserPairForPerms.UserData.UID) ?? new PatternInfo();
                bool disabledCondition = storedPatternName.Identifier == Guid.Empty;

                _permActions.DrawGenericComboButton(UserPairForPerms.UserData.UID, "ExecutePatternForPairPermCombo", "Play Pattern",
                WindowMenuWidth, lastToyboxData.PatternList, (Pattern) => Pattern.Name, true, disabledCondition, true, storedPatternName,
                FontAwesomeIcon.Play, ImGuiComboFlags.None, (selected) => { _logger.LogDebug("Selected Pattern Set: " + selected); },
                (onButtonPress) =>
                {
                    try
                    {
                        var newToyboxData = lastToyboxData.DeepClone();
                        if (newToyboxData == null || onButtonPress == null) throw new Exception("Toybox data is null, not sending");

                        // set all other stored patterns active state to false, and the pattern with the onButtonPress matching GUID to true.
                        newToyboxData.ActivePatternGuid = onButtonPress.Identifier;

                        // Run the call to execute the pattern to the server.
                        _ = _apiController.UserPushPairDataToyboxUpdate(new(UserPairForPerms.UserData, newToyboxData, DataUpdateKind.ToyboxPatternExecuted));
                        _logger.LogDebug("Executing Pattern {0} to {1}'s toy", onButtonPress.Name, PairNickOrAliasOrUID);
                        Opened = ActiveActionButton.None;
                    }
                    catch (Exception e) { _logger.LogError("Failed to push updated ToyboxPattern data: " + e.Message); }
                });
            }
            ImGui.Separator();
        }

        ////////// STOP RUNNING PATTERN ON PAIR'S TOY //////////
        bool disableStopPattern = !pairUniquePerms.CanStopPatterns || !UserPairForPerms.UserPairGlobalPerms.ToyIsActive || lastToyboxData.ActivePatternGuid == Guid.Empty;
        if (_uiShared.IconTextButton(FontAwesomeIcon.StopCircle, "Stop " + PairUID + "'s Active Pattern", WindowMenuWidth, true, disableStopPattern))
        {
            try
            {
                var newToyboxData = lastToyboxData.DeepClone();
                if (newToyboxData == null) throw new Exception("Toybox data is null, not sending");
                newToyboxData.ActivePatternGuid = Guid.Empty;

                _ = _apiController.UserPushPairDataToyboxUpdate(new(UserPairForPerms.UserData, newToyboxData, DataUpdateKind.ToyboxPatternStopped));
                _logger.LogDebug("Stopped active Pattern running on {1}'s toy", PairNickOrAliasOrUID);
            }
            catch (Exception e) { _logger.LogError("Failed to push updated ToyboxPattern data: " + e.Message); }
        }
        UiSharedService.AttachToolTip("Halt the active pattern on " + PairUID + "'s Toy");


        ////////// TOGGLE TRIGGER FOR PAIR //////////
        var disableTriggers = !pairUniquePerms.CanToggleTriggers;
        if (_uiShared.IconTextButton(FontAwesomeIcon.LandMineOn, "Toggle " + PairUID + "'s Triggers", WindowMenuWidth, true, disableTriggers))
        {
            Opened = Opened == ActiveActionButton.ToggleTrigger ? ActiveActionButton.None : ActiveActionButton.ToggleTrigger;
        }
        UiSharedService.AttachToolTip("Toggle the state of a trigger in " + PairUID + "'s triggerList.");
        if (Opened is ActiveActionButton.ToggleTrigger)
        {
            using (var actionChild = ImRaii.Child("TriggerToggleChild", new Vector2(WindowMenuWidth, ImGui.GetFrameHeight()), false))
            {
                if (!actionChild) return;

                TriggerInfo selected = _permActions.GetSelectedItem<TriggerInfo>("ToggleTriggerForPairPermCombo", UserPairForPerms.UserData.UID) ?? new TriggerInfo();
                bool disabled = selected.Identifier == Guid.Empty || !lastToyboxData.TriggerList.Any();
                string buttonText = selected.Enabled ? "Disable Trigger" : "Enable Trigger";

                _permActions.DrawGenericComboButton(UserPairForPerms.UserData.UID, "ToggleTriggerForPairPermCombo", buttonText,
                WindowMenuWidth, lastToyboxData.TriggerList, (Trigger) => Trigger.Name, true, disabled, false, selected,
                FontAwesomeIcon.None, ImGuiComboFlags.None, (selected) => { _logger.LogDebug("Selected Trigger: " + selected?.Name); },
                (onButtonPress) =>
                {
                    try
                    {
                        var newToyboxData = lastToyboxData.DeepClone();
                        if (newToyboxData == null || onButtonPress == null) throw new Exception("Toybox data is null, not sending");
                        // locate the alarm in the alarm list matching the selected alarm in on button press
                        var triggerToToggle = newToyboxData.TriggerList.FindIndex(x => x.Identifier == onButtonPress.Identifier);
                        if (triggerToToggle == -1) throw new Exception("Trigger not found in list.");

                        // toggle the alarm state.
                        newToyboxData.TriggerList[triggerToToggle].Enabled = !newToyboxData.TriggerList[triggerToToggle].Enabled;

                        _ = _apiController.UserPushPairDataToyboxUpdate(new(UserPairForPerms.UserData, newToyboxData, DataUpdateKind.ToyboxTriggerToggled));
                        _logger.LogDebug("Toggling Trigger {0} on {1}'s TriggerList", onButtonPress.Name, PairNickOrAliasOrUID);
                        Opened = ActiveActionButton.None;
                    }
                    catch (Exception e) { _logger.LogError("Failed to push updated ToyboxPattern data: " + e.Message); }
                });
            }
        }
        ImGui.Separator();
    }
}
