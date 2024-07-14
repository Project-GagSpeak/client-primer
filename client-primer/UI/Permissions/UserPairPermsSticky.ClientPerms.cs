using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using GagSpeak.PlayerData.Pairs;
using GagSpeak.Services.Mediator;
using GagspeakAPI.Data.Enum;
using GagspeakAPI.Dto.Permissions;
using ImGuiNET;

namespace GagSpeak.UI.Permissions;

/// <summary>
/// CLIENT PERMS PARTIAL CLASS
/// </summary>
public partial class UserPairPermsSticky
{
    public void DrawClientPermsForPair()
    {
        var _menuWidth = ImGui.GetContentRegionAvail().X - ImGui.GetFrameHeight();

        /* ----------- GLOBAL SETTINGS ----------- */
        ImGui.TextUnformatted("Global Settings");

        DrawSetting("Live Chat Garbler Access", "LiveChatGarblerActiveAllowed",
            UserPairForPerms.UserPair.OwnEditAccessPerms.LiveChatGarblerActiveAllowed,
            FontAwesomeIcon.Lock, FontAwesomeIcon.Unlock, true, PermType.YesNo);

        DrawSetting("Live Chat Garbler Lock Access", "LiveChatGarblerLockedAllowed", 
            UserPairForPerms.UserPair.OwnEditAccessPerms.LiveChatGarblerLockedAllowed,
            FontAwesomeIcon.Lock, FontAwesomeIcon.Unlock, true, PermType.YesNo);

        DrawSetting("Lock Toybox UI Access", "LockToyboxUIAllowed", 
            UserPairForPerms.UserPair.OwnEditAccessPerms.LockToyboxUIAllowed,
            FontAwesomeIcon.Lock, FontAwesomeIcon.Unlock, true, PermType.YesNo);

        ImGui.Separator();
        /* ----------- GAG PERMISSIONS ----------- */
        ImGui.TextUnformatted("Gag Permissions");

        DrawSetting("Gag Item Auto-Equip", "ItemAutoEquipAllowed", 
            UserPairForPerms.UserPair.OwnEditAccessPerms.ItemAutoEquipAllowed,
            FontAwesomeIcon.Lock, FontAwesomeIcon.Unlock, true, PermType.YesNo);

        DrawSetting("Can Set Max Gag Lock Time", "MaxLockTimeAllowed",
            UserPairForPerms.UserPair.OwnEditAccessPerms.MaxLockTimeAllowed,
            FontAwesomeIcon.Lock, FontAwesomeIcon.Unlock, true, PermType.YesNo);

        DrawSetting("Max Gag Lock Time", "MaxLockTime", 
            UserPairForPerms.UserPair.OwnPairPerms.MaxLockTime,
            FontAwesomeIcon.Lock, FontAwesomeIcon.Unlock, false, PermType.TimeSpan);

        DrawSetting("Extended Lock Times Access", "ExtendedLockTimesAllowed",
            UserPairForPerms.UserPair.OwnEditAccessPerms.ExtendedLockTimesAllowed,
            FontAwesomeIcon.Lock, FontAwesomeIcon.Unlock, true, PermType.YesNo);

        ImGui.Separator();
        /* ----------- RESTRAINT SET PERMISSIONS ----------- */
        ImGui.TextUnformatted("Restraint Set Permissions");

        DrawSetting("Restraint Set Applying", "ApplyRestraintSetsAllowed", 
            UserPairForPerms.UserPair.OwnEditAccessPerms.ApplyRestraintSetsAllowed,
            FontAwesomeIcon.Lock, FontAwesomeIcon.Unlock, true, PermType.YesNo);

        DrawSetting("Restraint Set Locking", "LockRestraintSetsAllowed",
            UserPairForPerms.UserPair.OwnEditAccessPerms.LockRestraintSetsAllowed,
            FontAwesomeIcon.Lock, FontAwesomeIcon.Unlock, true, PermType.YesNo);

        DrawSetting("Can Set Max Lock Time", "MaxAllowedRestraintTimeAllowed",
            UserPairForPerms.UserPair.OwnEditAccessPerms.MaxAllowedRestraintTimeAllowed,
            FontAwesomeIcon.Lock, FontAwesomeIcon.Unlock, true, PermType.YesNo);

        DrawSetting("Max Restraint Set Lock Time", "MaxAllowedRestraintTime",
            UserPairForPerms.UserPair.OwnPairPerms.MaxAllowedRestraintTime,
            FontAwesomeIcon.Lock, FontAwesomeIcon.Unlock, false, PermType.TimeSpan);

        DrawSetting("Restraint Set Removing", "RemoveRestraintSetsAllowed",
            UserPairForPerms.UserPair.OwnEditAccessPerms.RemoveRestraintSetsAllowed,
            FontAwesomeIcon.Lock, FontAwesomeIcon.Unlock, true, PermType.YesNo);

        ImGui.Separator();
        /* ----------- PUPPETEER PERMISSIONS ----------- */
        ImGui.TextUnformatted("Puppeteer Permissions");

        DrawSetting("Trigger Phrase", "TriggerPhrase", 
            UserPairForPerms.UserPair.OwnPairPerms.TriggerPhrase,
            FontAwesomeIcon.Lock, FontAwesomeIcon.Unlock, false, PermType.String);

        DrawSetting("Start Bracket Character", "StartChar",
            UserPairForPerms.UserPair.OwnPairPerms.StartChar,
            FontAwesomeIcon.Lock, FontAwesomeIcon.Unlock, false, PermType.Char);

        DrawSetting("End Bracket Character", "EndChar",
            UserPairForPerms.UserPair.OwnPairPerms.EndChar,
            FontAwesomeIcon.Lock, FontAwesomeIcon.Unlock, false, PermType.Char);

        DrawSetting("Allow Sit Commands", "AllowSitRequestsAllowed",
            UserPairForPerms.UserPair.OwnEditAccessPerms.AllowSitRequestsAllowed,
            FontAwesomeIcon.Lock, FontAwesomeIcon.Unlock, true, PermType.YesNo);

        DrawSetting("Allow Motion Commands", "AllowMotionRequestsAllowed",
            UserPairForPerms.UserPair.OwnEditAccessPerms.AllowMotionRequestsAllowed,
            FontAwesomeIcon.Lock, FontAwesomeIcon.Unlock, true, PermType.YesNo);

        DrawSetting("Allow All Commands", "AllowAllRequestsAllowed",
            UserPairForPerms.UserPair.OwnEditAccessPerms.AllowAllRequestsAllowed,
            FontAwesomeIcon.Lock, FontAwesomeIcon.Unlock, true, PermType.YesNo);

        ImGui.Separator();
        /* ----------- MOODLES PERMISSIONS ----------- */
        ImGui.TextUnformatted("Moodles Permissions");

        DrawSetting("Positive Status Types Allowed", "AllowPositiveStatusTypesAllowed",
            UserPairForPerms.UserPair.OwnEditAccessPerms.AllowPositiveStatusTypesAllowed,
            FontAwesomeIcon.Lock, FontAwesomeIcon.Unlock, true, PermType.YesNo);

        DrawSetting("Negative Status Types Allowed", "AllowNegativeStatusTypesAllowed",
            UserPairForPerms.UserPair.OwnEditAccessPerms.AllowNegativeStatusTypesAllowed,
            FontAwesomeIcon.Lock, FontAwesomeIcon.Unlock, true, PermType.YesNo);

        DrawSetting("Special Status Types Allowed", "AllowSpecialStatusTypesAllowed",
            UserPairForPerms.UserPair.OwnEditAccessPerms.AllowSpecialStatusTypesAllowed,
            FontAwesomeIcon.Lock, FontAwesomeIcon.Unlock, true, PermType.YesNo);

        DrawSetting("Pair can Apply Your Moodles", "PairCanApplyOwnMoodlesToYouAllowed",
            UserPairForPerms.UserPair.OwnEditAccessPerms.PairCanApplyOwnMoodlesToYouAllowed,
            FontAwesomeIcon.Lock, FontAwesomeIcon.Unlock, true, PermType.YesNo);

        DrawSetting("Pair can Apply Their Moodles", "PairCanApplyYourMoodlesToYouAllowed",
            UserPairForPerms.UserPair.OwnEditAccessPerms.PairCanApplyYourMoodlesToYouAllowed,
            FontAwesomeIcon.Lock, FontAwesomeIcon.Unlock, true, PermType.YesNo);

        DrawSetting("Max Moodle Time Access", "MaxMoodleTimeAllowed",
            UserPairForPerms.UserPair.OwnEditAccessPerms.MaxMoodleTimeAllowed,
            FontAwesomeIcon.Lock, FontAwesomeIcon.Unlock, true, PermType.YesNo);

        DrawSetting("Max Moodle Time", "MaxMoodleTime",
            UserPairForPerms.UserPair.OwnPairPerms.MaxMoodleTime,
            FontAwesomeIcon.Lock, FontAwesomeIcon.Unlock, false, PermType.TimeSpan);

        DrawSetting("Permanent Moodles Allowed", "AllowPermanentMoodlesAllowed",
            UserPairForPerms.UserPair.OwnEditAccessPerms.AllowPermanentMoodlesAllowed,
            FontAwesomeIcon.Lock, FontAwesomeIcon.Unlock, true, PermType.YesNo);

        ImGui.Separator();
        /* ----------- TOYBOX PERMISSIONS ----------- */
        ImGui.TextUnformatted("Toybox Permissions");

        DrawSetting("Toy State Changing", "ChangeToyStateAllowed",
            UserPairForPerms.UserPair.OwnEditAccessPerms.ChangeToyStateAllowed,
            FontAwesomeIcon.Lock, FontAwesomeIcon.Unlock, true, PermType.YesNo);

        DrawSetting("Intensity Control", "CanControlIntensityAllowed",
            UserPairForPerms.UserPair.OwnEditAccessPerms.CanControlIntensityAllowed,
            FontAwesomeIcon.Lock, FontAwesomeIcon.Unlock, true, PermType.YesNo);

        DrawSetting("Can Set Vibrator Alarms", "VibratorAlarmsAllowed",
            UserPairForPerms.UserPair.OwnEditAccessPerms.VibratorAlarmsAllowed,
            FontAwesomeIcon.Lock, FontAwesomeIcon.Unlock, true, PermType.YesNo);

        DrawSetting("Realtime Vibe Remote Access", "CanUseRealtimeVibeRemoteAllowed",
            UserPairForPerms.UserPair.OwnEditAccessPerms.CanUseRealtimeVibeRemoteAllowed,
            FontAwesomeIcon.Lock, FontAwesomeIcon.Unlock, true, PermType.YesNo);

        DrawSetting("Can Execute Patterns", "CanExecutePatternsAllowed",
            UserPairForPerms.UserPair.OwnEditAccessPerms.CanExecutePatternsAllowed,
            FontAwesomeIcon.Lock, FontAwesomeIcon.Unlock, true, PermType.YesNo);

        DrawSetting("Can Execute Triggers", "CanExecuteTriggersAllowed",
            UserPairForPerms.UserPair.OwnEditAccessPerms.CanExecuteTriggersAllowed,
            FontAwesomeIcon.Lock, FontAwesomeIcon.Unlock, true, PermType.YesNo);

        DrawSetting("Can Create Triggers", "CanCreateTriggersAllowed",
            UserPairForPerms.UserPair.OwnEditAccessPerms.CanCreateTriggersAllowed,
            FontAwesomeIcon.Lock, FontAwesomeIcon.Unlock, true, PermType.YesNo);

        DrawSetting("Can Send Triggers", "CanSendTriggersAllowed",
            UserPairForPerms.UserPair.OwnEditAccessPerms.CanSendTriggersAllowed,
            FontAwesomeIcon.Lock, FontAwesomeIcon.Unlock, true, PermType.YesNo);

        ImGui.Separator();
        /* ----------- HARDCORE PERMISSIONS ----------- */
        ImGui.TextUnformatted("Hardcore Permissions");

        DrawSetting($"Hardcore With {UserPairForPerms.UserData.AliasOrUID}", "InHardcore",
            UserPairForPerms.UserPair.OwnPairPerms.InHardcore,
            FontAwesomeIcon.Lock, FontAwesomeIcon.Unlock, false, PermType.YesNo);

        DrawSetting("Allow Hardcore Follow", "AllowForcedFollow",
            UserPairForPerms.UserPair.OwnPairPerms.AllowForcedFollow,
            FontAwesomeIcon.Lock, FontAwesomeIcon.Unlock, false, PermType.YesNo);

        DrawSetting("Allow Hardcore Sit", "AllowForcedSit",
            UserPairForPerms.UserPair.OwnPairPerms.AllowForcedSit,
            FontAwesomeIcon.Lock, FontAwesomeIcon.Unlock, false, PermType.YesNo);

        DrawSetting("Allow Hardcore Stay", "AllowForcedToStay",
            UserPairForPerms.UserPair.OwnPairPerms.AllowForcedToStay,
            FontAwesomeIcon.Lock, FontAwesomeIcon.Unlock, false, PermType.YesNo);

        DrawSetting("Allow Blindfold", "AllowBlindfold",
            UserPairForPerms.UserPair.OwnPairPerms.AllowBlindfold,
            FontAwesomeIcon.Lock, FontAwesomeIcon.Unlock, false, PermType.YesNo);
    }

    /// <summary>
    /// The primary call for displaying a setting for the client permissions.
    /// </summary>
    /// <param name="label"> the label to display for the permission object </param>
    /// <param name="permissionKey"> the permission attribute we are displaying </param>
    /// <param name="currentState"> the current state of the permission attribute </param>
    /// <param name="iconOn"> icon to be displayed when permission is enabled </param>
    /// <param name="iconOff"> icon to be displayed when permission is disabled </param
    /// <param name="isEditAccessPerm"> Let's us know if we are using the edit access object or pair permission object </param>
    /// <param name="type"> what permission type it is (string, char, timespan, boolean) </param>
    private void DrawSetting(string label, string permissionKey, object currentState,
        FontAwesomeIcon iconOn, FontAwesomeIcon iconOff, bool isEditAccessPerm, PermType type)
    {
        string labelWithState = label;
        FontAwesomeIcon icon = iconOff;
        string tooltip = string.Empty;

        switch (type)
        {
            case PermType.YesNo:
                bool boolState = (bool)currentState;
                icon = boolState ? iconOn : iconOff;
                tooltip = $"Changes {_uiSharedService.ApiController.PlayerUserData.AliasOrUID}'s {label} State to " + (boolState ? "Disabled" : "Enabled");
                break;
            case PermType.TimeSpan:
                TimeSpan timeSpanState = (TimeSpan)currentState;
                labelWithState = label + ": " + _uiSharedService.TimeSpanToString(timeSpanState);
                tooltip = $"Current {label} is {_uiSharedService.TimeSpanToString(timeSpanState)}. Click to edit.";
                icon = iconOn;
                break;
            case PermType.String:
                string stringState = (string)currentState ?? "EMPTY_FIELD";
                labelWithState = label + ": " + stringState;
                tooltip = $"Current {label} is {stringState}. Click to edit.";
                icon = iconOn;
                break;
            case PermType.Char:
                char charState = (char)currentState;
                labelWithState = label + ": " + charState;
                tooltip = $"Current {label} is {charState}. Click to edit.";
                icon = iconOn;
                break;
        }

        try
        {
            DrawPermissionButton(labelWithState, icon, tooltip, permissionKey, isEditAccessPerm, type);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to update permissions for {_uiSharedService.ApiController.PlayerUserData.AliasOrUID} :: {ex}");
        }
    }

    /// <summary>
    /// The function responsible for identifying the permission item based on the permission type. 
    /// It will call DrawPermission inside this, drawing the item.
    /// </summary>
    /// <param name="label"> the label to display for the permission object </param>
    /// <param name="icon"> the icon to display to the left-hand side </param>
    /// <param name="tooltip"> the tooltip to display when the item is hovered over. </param>
    /// <param name="permissionKey"> the permission attribute we are displaying </param>
    /// <param name="type"> what permission type it is (string, char, timespan, boolean) </param>
    /// <param name="isEditAccessPerm"> if it is a global perm. if false, it means it is a pair permission. </param>
    private void DrawPermissionButton(string label, FontAwesomeIcon icon,
        string tooltip, string permissionKey, bool isEditAccessPerm, PermType type)
    {
        var _menuWidth = ImGui.GetContentRegionAvail().X;

        if (isEditAccessPerm)
        {
            var permissions = UserPairForPerms.UserPair.OwnEditAccessPerms;
            DrawPermission(permissions, label, icon, tooltip, permissionKey, type, _menuWidth, isEditAccessPerm);
        }
        else
        {
            var permissions = UserPairForPerms.UserPair.OwnPairPerms;
            DrawPermission(permissions, label, icon, tooltip, permissionKey, type, _menuWidth, isEditAccessPerm);
        }
    }

    /// <summary>
    /// Responsible for calling the correct display item based on the permission type and permission object
    /// </summary>
    /// <param name="permissions"> the permissions object that is either a globalpermission object or pair permission object </param>
    /// <param name="label"> the label to display for the permission object </param>
    /// <param name="icon"> the icon to display to the left-hand side </param>
    /// <param name="tooltip"> the tooltip to display when the item is hovered over. </param>
    /// <param name="permissionKey"> the permission attribute we are displaying </param>
    /// <param name="type"> what permission type it is (string, char, timespan, boolean) </param>
    /// <param name="_menuWidth"> the width of the item to display </param>
    /// <param name="isGlobalPerm"> if it is a global perm. if false, it means it is a pair permission. </param>
    private void DrawPermission(object permissions, string label, FontAwesomeIcon icon,
        string tooltip, string permissionKey, PermType type, float _menuWidth, bool isEditAccessPerm)
    {
        bool _wasHovered = false;
        // if type is boolean
        if (type == PermType.YesNo)
        {
            bool newValueState = (bool)permissions.GetType().GetProperty(permissionKey)?.GetValue(permissions);
            if (_uiSharedService.IconTextButton(icon, label, _menuWidth, true, false))
            {
                SetPermission(permissions, permissionKey, !newValueState, isEditAccessPerm);
            }
            UiSharedService.AttachToolTip(tooltip);
        }
        else if (type == PermType.TimeSpan)
        {
            string timeSpanString = _uiSharedService.TimeSpanToString((TimeSpan)permissions.GetType().GetProperty(permissionKey)?.GetValue(permissions)!) ?? "0d0h0m0s";

            if (_uiSharedService.IconInputText(icon, label, "format 0d0h0m0s...", ref timeSpanString, 32, _menuWidth, true, false)) { /* Consume */ }
            // if deactivated set perm
            if (ImGui.IsItemDeactivatedAfterEdit() 
                && timeSpanString != _uiSharedService.TimeSpanToString((TimeSpan)permissions.GetType().GetProperty(permissionKey)?.GetValue(permissions)!))
            {
                if (_uiSharedService.TryParseTimeSpan(timeSpanString, out TimeSpan result))
                {
                    SetPermission(permissions, permissionKey, result, isEditAccessPerm);
                }
                else
                {
                    Logger.LogWarning("You tried to set an invalid timespan format. Please use the format 0d0h0m0s");
                    timeSpanString = "0d0h0m0s";
                }
            }
            // add tooltip
            UiSharedService.AttachToolTip(tooltip);
        }
        else if (type == PermType.String)
        {
            string inputStr = permissions.GetType().GetProperty(permissionKey)?.GetValue(permissions)?.ToString() ?? "EMPTY_FIELD";

            if (_uiSharedService.IconInputText(icon, label, "input here...", ref inputStr, 32, _menuWidth, true, false)) { /* Consume */ }
            // if deactivated set perm
            if (ImGui.IsItemDeactivatedAfterEdit() && inputStr != (string)permissions.GetType().GetProperty(permissionKey)?.GetValue(permissions)!)
            {
                SetPermission(permissions, permissionKey, inputStr, isEditAccessPerm);
            }
            // add tooltip
            UiSharedService.AttachToolTip(tooltip);
        }
        else if (type == PermType.Char)
        {
            string inputChar = permissions.GetType().GetProperty(permissionKey)?.GetValue(permissions)?.ToString() ?? ".";

            if (_uiSharedService.IconInputText(icon, label, ".", ref inputChar, 1, _menuWidth, true, false)) { /* Consume */ }
            // if deactivated set perm
            if (ImGui.IsItemDeactivatedAfterEdit())
            {
                // get the original value as a string (it is originally char)
                string originalValue = permissions.GetType().GetProperty(permissionKey)?.GetValue(permissions)?.ToString() ?? ".";

                // see if it is different from the existing one
                if (!inputChar.Equals(originalValue))
                {
                    // ensure new value is char before passing in
                    char newValue = inputChar.Length > 0 ? inputChar[0] : '.';
                    SetPermission(permissions, permissionKey, newValue, isEditAccessPerm);
                }
            }
            // add tooltip
            UiSharedService.AttachToolTip(tooltip);
        }
    }

    /// <summary>
    /// Sets the permission that we modified for ourselves or another player.
    /// </summary>
    /// <param name="permissions"> the global permissions or pair permissions object </param>
    /// <param name="permissionKey"> the attribute of the object we are changing</param>
    /// <param name="newValue"> the new value it is being changed to </param>
    /// <param name="isGlobalPerm"> if it is a global permission object or a pair permission object. </param>
    private void SetPermission(object permissions, string permissionKey, object newValue, bool isEditAccessPerm)
    {
        // DO NOT UPDATE THIS HERE, WE WILL GET A CALLBACK FROM THE SERVER INFORMING US OF THE SUCCSS
        // permissions.GetType().GetProperty(permissionKey)?.SetValue(permissions, newValue);

        // update the value on the server end
        if (isEditAccessPerm)
        {
            _logger.LogTrace($"Updated own edit access permission: {permissionKey} to {newValue}");
            _ = _apiController.UserUpdateOwnPairPermAccess(new UserPairAccessChangeDto(UserPairForPerms.UserData,
                new KeyValuePair<string, object>(permissionKey, newValue)));
        }
        else
        {
            _logger.LogTrace($"Updated own pair permission: {permissionKey} to {newValue}");
            _ = _apiController.UserUpdateOwnPairPerm(new UserPairPermChangeDto(UserPairForPerms.UserData,
                new KeyValuePair<string, object>(permissionKey, newValue)));
        }
    }
}
