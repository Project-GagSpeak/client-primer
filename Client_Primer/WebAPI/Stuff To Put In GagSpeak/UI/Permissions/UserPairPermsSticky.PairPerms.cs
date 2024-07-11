using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using FFStreamViewer.WebAPI.Services.Mediator;
using Gagspeak.API.Data.Enum;
using GagSpeak.API.Dto.Permissions;
using ImGuiNET;

namespace FFStreamViewer.WebAPI.UI.Permissions;

/// <summary>
/// Contains functions relative to the paired users permissions for the client user.
/// </summary>
public partial class UserPairPermsSticky
{
    public void DrawPairPermsForClient()
    {
        var _menuWidth = ImGui.GetContentRegionAvail().X - ImGui.GetFrameHeight();

        /* ----------- GLOBAL SETTINGS ----------- */
        ImGui.TextUnformatted("Global Settings");

        DrawSettingPair("Live Chat Garbler", "LiveChatGarblerActive", UserPairForPerms.UserPair.OtherGlobalPerms.LiveChatGarblerActive,
            UserPairForPerms.UserPair.OtherEditAccessPerms.LiveChatGarblerActiveAllowed, FontAwesomeIcon.VolumeUp, FontAwesomeIcon.VolumeMute, true, PermType.YesNo);

        DrawSettingPair("Live Chat Garbler Lock", "LiveChatGarblerLocked", UserPairForPerms.UserPair.OtherGlobalPerms.LiveChatGarblerLocked,
            UserPairForPerms.UserPair.OtherEditAccessPerms.LiveChatGarblerLockedAllowed, FontAwesomeIcon.Lock, FontAwesomeIcon.Unlock, true, PermType.YesNo);

        DrawSettingPair("Toybox UI Lock", "LockToyboxUI", UserPairForPerms.UserPair.OtherGlobalPerms.LockToyboxUI,
            UserPairForPerms.UserPair.OtherEditAccessPerms.LockToyboxUIAllowed, FontAwesomeIcon.Lock, FontAwesomeIcon.Unlock, true, PermType.YesNo);

        /* ----------- GAG PERMISSIONS ----------- */
        ImGui.TextUnformatted("Gag Permissions");

        DrawSettingPair("Gag Item Auto-Equip", "ItemAutoEquip", UserPairForPerms.UserPair.OtherGlobalPerms.ItemAutoEquip,
            UserPairForPerms.UserPair.OtherEditAccessPerms.LiveChatGarblerActiveAllowed, FontAwesomeIcon.Microphone, FontAwesomeIcon.MicrophoneSlash, true, PermType.YesNo);

        DrawSettingPair("Max Gag Lock Duration", "MaxLockTime", UserPairForPerms.UserPair.OtherPairPerms.MaxLockTime,
            UserPairForPerms.UserPair.OtherEditAccessPerms.MaxLockTimeAllowed, FontAwesomeIcon.Clock, FontAwesomeIcon.Clock, false, PermType.TimeSpan);

        DrawSettingPair("Extended Lock Times", "ExtendedLockTimes", UserPairForPerms.UserPair.OtherPairPerms.ExtendedLockTimes,
            UserPairForPerms.UserPair.OtherEditAccessPerms.ExtendedLockTimesAllowed, FontAwesomeIcon.Clock, FontAwesomeIcon.Clock, false, PermType.YesNo);

        /* ----------- RESTRAINT SET PERMISSIONS ----------- */
        ImGui.TextUnformatted("Restraint Set Permissions");

        DrawSettingPair("Restraint Set Applying", "ApplyRestraintSets", UserPairForPerms.UserPair.OtherPairPerms.ApplyRestraintSets,
            UserPairForPerms.UserPair.OtherEditAccessPerms.ApplyRestraintSetsAllowed, FontAwesomeIcon.Lock, FontAwesomeIcon.LockOpen, false, PermType.YesNo);

        DrawSettingPair("Restraint Set Locking", "LockRestraintSets", UserPairForPerms.UserPair.OtherPairPerms.LockRestraintSets,
            UserPairForPerms.UserPair.OtherEditAccessPerms.LockRestraintSetsAllowed, FontAwesomeIcon.Lock, FontAwesomeIcon.LockOpen, false, PermType.YesNo);

        DrawSettingPair("Max Restraint Set Lock Time", "MaxAllowedRestraintTime", UserPairForPerms.UserPair.OtherPairPerms.MaxAllowedRestraintTime,
            UserPairForPerms.UserPair.OtherEditAccessPerms.MaxAllowedRestraintTimeAllowed, FontAwesomeIcon.Clock, FontAwesomeIcon.Clock, false, PermType.TimeSpan);

        DrawSettingPair("Restraint Set Removing", "RemoveRestraintSets", UserPairForPerms.UserPair.OtherPairPerms.RemoveRestraintSets,
            UserPairForPerms.UserPair.OtherEditAccessPerms.RemoveRestraintSetsAllowed, FontAwesomeIcon.Trash, FontAwesomeIcon.Trash, false, PermType.YesNo);

        /* ----------- PUPPETEER PERMISSIONS ----------- */
        ImGui.TextUnformatted("Puppeteer Permissions");

        DrawSettingPair("Sit Commands Allowed", "AllowSitRequests", UserPairForPerms.UserPair.OtherPairPerms.AllowSitRequests,
            UserPairForPerms.UserPair.OtherEditAccessPerms.AllowSitRequestsAllowed, FontAwesomeIcon.Chair, FontAwesomeIcon.Chair, false, PermType.YesNo);

        DrawSettingPair("Motion Commands Allowed", "AllowMotionRequests", UserPairForPerms.UserPair.OtherPairPerms.AllowMotionRequests,
            UserPairForPerms.UserPair.OtherEditAccessPerms.AllowMotionRequestsAllowed, FontAwesomeIcon.Walking, FontAwesomeIcon.Walking, false, PermType.YesNo);

        DrawSettingPair("All Commands Allowed", "AllowAllRequests", UserPairForPerms.UserPair.OtherPairPerms.AllowAllRequests,
            UserPairForPerms.UserPair.OtherEditAccessPerms.AllowAllRequestsAllowed, FontAwesomeIcon.Walking, FontAwesomeIcon.Walking, false, PermType.YesNo);

        /* ----------- MOODLES PERMISSIONS ----------- */
        ImGui.TextUnformatted("Moodles Permissions");

        DrawSettingPair("Positive Status Types Allowed", "AllowPositiveStatusTypes", UserPairForPerms.UserPair.OtherPairPerms.AllowPositiveStatusTypes,
            UserPairForPerms.UserPair.OtherEditAccessPerms.AllowPositiveStatusTypesAllowed, FontAwesomeIcon.Plus, FontAwesomeIcon.Plus, false, PermType.YesNo);

        DrawSettingPair("Negative Status Types Allowed", "AllowNegativeStatusTypes", UserPairForPerms.UserPair.OtherPairPerms.AllowNegativeStatusTypes,
            UserPairForPerms.UserPair.OtherEditAccessPerms.AllowNegativeStatusTypesAllowed, FontAwesomeIcon.Minus, FontAwesomeIcon.Minus, false, PermType.YesNo);

        DrawSettingPair("Special Status Types Allowed", "AllowSpecialStatusTypes", UserPairForPerms.UserPair.OtherPairPerms.AllowSpecialStatusTypes,
            UserPairForPerms.UserPair.OtherEditAccessPerms.AllowSpecialStatusTypesAllowed, FontAwesomeIcon.Question, FontAwesomeIcon.Question, false, PermType.YesNo);

        DrawSettingPair($"Can Apply {UserPairForPerms.UserData.AliasOrUID}'s Moodles", "PairCanApplyOwnMoodlesToYou", UserPairForPerms.UserPair.OtherPairPerms.PairCanApplyOwnMoodlesToYou,
            UserPairForPerms.UserPair.OtherEditAccessPerms.PairCanApplyOwnMoodlesToYouAllowed, FontAwesomeIcon.User, FontAwesomeIcon.User, false, PermType.YesNo);

        DrawSettingPair($"Can Apply Your Moodles", "PairCanApplyYourMoodlesToYou", UserPairForPerms.UserPair.OtherPairPerms.PairCanApplyYourMoodlesToYou,
            UserPairForPerms.UserPair.OtherEditAccessPerms.PairCanApplyYourMoodlesToYouAllowed, FontAwesomeIcon.User, FontAwesomeIcon.User, false, PermType.YesNo);

        DrawSettingPair("Max Moodle Time", "MaxMoodleTime", UserPairForPerms.UserPair.OtherPairPerms.MaxMoodleTime,
            UserPairForPerms.UserPair.OtherEditAccessPerms.MaxMoodleTimeAllowed, FontAwesomeIcon.Clock, FontAwesomeIcon.Clock, false, PermType.TimeSpan);

        DrawSettingPair("Permanent Moodles Allowed", "AllowPermanentMoodles", UserPairForPerms.UserPair.OtherPairPerms.AllowPermanentMoodles,
            UserPairForPerms.UserPair.OtherEditAccessPerms.AllowPermanentMoodlesAllowed, FontAwesomeIcon.Clock, FontAwesomeIcon.Clock, false, PermType.YesNo);

        /* ----------- TOYBOX PERMISSIONS ----------- */
        ImGui.TextUnformatted("Toybox Permissions");

        DrawSettingPair("Toy State Changing", "ChangeToyState", UserPairForPerms.UserPair.OtherPairPerms.ChangeToyState,
            UserPairForPerms.UserPair.OtherEditAccessPerms.ChangeToyStateAllowed, FontAwesomeIcon.Cogs, FontAwesomeIcon.Cogs, false, PermType.YesNo);

        DrawSettingPair("Intensity Control", "CanControlIntensity", UserPairForPerms.UserPair.OtherPairPerms.CanControlIntensity,
            UserPairForPerms.UserPair.OtherEditAccessPerms.CanControlIntensityAllowed, FontAwesomeIcon.SlidersH, FontAwesomeIcon.SlidersH, false, PermType.YesNo);

        DrawSettingPair("Can use Vibrator Alarms", "VibratorAlarms", UserPairForPerms.UserPair.OtherPairPerms.VibratorAlarms,
            UserPairForPerms.UserPair.OtherEditAccessPerms.VibratorAlarmsAllowed, FontAwesomeIcon.Bell, FontAwesomeIcon.Bell, false, PermType.YesNo);

        DrawSettingPair("Realtime Vibe Remote", "CanUseRealtimeVibeRemote", UserPairForPerms.UserPair.OtherPairPerms.CanUseRealtimeVibeRemote,
            UserPairForPerms.UserPair.OtherEditAccessPerms.CanUseRealtimeVibeRemoteAllowed, FontAwesomeIcon.Wifi, FontAwesomeIcon.Wifi, false, PermType.YesNo);

        DrawSettingPair("Cab Execute Patterns", "CanExecutePatterns", UserPairForPerms.UserPair.OtherPairPerms.CanExecutePatterns,
            UserPairForPerms.UserPair.OtherEditAccessPerms.CanExecutePatternsAllowed, FontAwesomeIcon.Wifi, FontAwesomeIcon.Wifi, false, PermType.YesNo);

        DrawSettingPair("Can Execute Triggers", "CanExecuteTriggers", UserPairForPerms.UserPair.OtherPairPerms.CanExecuteTriggers,
            UserPairForPerms.UserPair.OtherEditAccessPerms.CanExecuteTriggersAllowed, FontAwesomeIcon.Wifi, FontAwesomeIcon.Wifi, false, PermType.YesNo);

        DrawSettingPair("Can Create Triggers", "CanCreateTriggers", UserPairForPerms.UserPair.OtherPairPerms.CanCreateTriggers,
            UserPairForPerms.UserPair.OtherEditAccessPerms.CanCreateTriggersAllowed, FontAwesomeIcon.Wifi, FontAwesomeIcon.Wifi, false, PermType.YesNo);

        DrawSettingPair("Can Send Triggers", "CanSendTriggers", UserPairForPerms.UserPair.OtherPairPerms.CanSendTriggers,
            UserPairForPerms.UserPair.OtherEditAccessPerms.CanSendTriggersAllowed, FontAwesomeIcon.Wifi, FontAwesomeIcon.Wifi, false, PermType.YesNo);

        /* ----------- HARDCORE PERMISSIONS ----------- */
        ImGui.TextUnformatted("Hardcore Permissions");

        DrawSettingPair($"{UserPairForPerms.UserData.AliasOrUID} forced to Follow", "IsForcedToFollow", UserPairForPerms.UserPair.OtherPairPerms.IsForcedToFollow,
            UserPairForPerms.UserPair.OtherPairPerms.AllowForcedFollow, FontAwesomeIcon.ArrowsAlt, FontAwesomeIcon.ArrowsAlt, false, PermType.YesNo);

        DrawSettingPair($"{UserPairForPerms.UserData.AliasOrUID} forced to Sit", "IsForcedToSit", UserPairForPerms.UserPair.OtherPairPerms.IsForcedToSit,
            UserPairForPerms.UserPair.OtherPairPerms.AllowForcedSit, FontAwesomeIcon.Chair, FontAwesomeIcon.Chair, false, PermType.YesNo);

        DrawSettingPair($"{UserPairForPerms.UserData.AliasOrUID} forced to Stay", "IsForcedToStay", UserPairForPerms.UserPair.OtherPairPerms.IsForcedToStay,
            UserPairForPerms.UserPair.OtherPairPerms.AllowForcedToStay, FontAwesomeIcon.User, FontAwesomeIcon.User, false, PermType.YesNo);

        DrawSettingPair($"{UserPairForPerms.UserData.AliasOrUID} blindfolded", "IsBlindfolded", UserPairForPerms.UserPair.OtherPairPerms.IsBlindfolded,
            UserPairForPerms.UserPair.OtherPairPerms.AllowBlindfold, FontAwesomeIcon.Blind, FontAwesomeIcon.Blind, false, PermType.YesNo);
    }

    /// <summary>
    /// The primary call for displaying a setting for the user pair or client permissions.
    /// </summary>
    /// <param name="label"> the label to display for the permission object </param>
    /// <param name="permissionKey"> the permission attribute we are displaying </param>
    /// <param name="currentState"> the current state of the permission attribute </param>
    /// <param name="canEdit"> if this permission is modifyable </param>
    /// <param name="iconOn"> icon to be displayed when permission is enabled </param>
    /// <param name="iconOff"> icon to be displayed when permission is disabled </param
    /// <param name="isGlobalPerm"> if it is a global perm. if false, it means it is a pair permission. </param>
    /// <param name="type"> what permission type it is (string, char, timespan, boolean) </param>
    private void DrawSettingPair(string label, string permissionKey, object currentState, bool canEdit,
        FontAwesomeIcon iconOn, FontAwesomeIcon iconOff, bool isGlobalPerm, PermType type)
    {
        string labelWithState = label;
        FontAwesomeIcon icon = iconOff;
        string tooltip = string.Empty;

        switch (type)
        {
            case PermType.YesNo:
                bool boolState = (bool)currentState;
                labelWithState = boolState ? "Disable " + label : "Enable " + label;
                icon = boolState ? iconOn : iconOff;
                tooltip = $"Changes {UserPairForPerms.UserData.AliasOrUID}'s {label} State to " + (boolState ? "Disabled" : "Enabled");
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
            DrawPermissionButtonPair(labelWithState, canEdit, icon, tooltip, permissionKey, isGlobalPerm, type);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to update permissions for {UserPairForPerms.UserData.AliasOrUID} :: {ex}");
        }
    }

    /// <summary>
    /// The function responsible for identifying the permission item based on the permission type. 
    /// It will call DrawPermissionPair inside this, drawing the item.
    /// </summary>
    /// <param name="label"> the label to display for the permission object </param>
    /// <param name="canEdit"> if this permission is modifyable </param>
    /// <param name="icon"> the icon to display to the left-hand side </param>
    /// <param name="tooltip"> the tooltip to display when the item is hovered over. </param>
    /// <param name="permissionKey"> the permission attribute we are displaying </param>
    /// <param name="type"> what permission type it is (string, char, timespan, boolean) </param>
    /// <param name="isGlobalPerm"> if it is a global perm. if false, it means it is a pair permission. </param>
    private void DrawPermissionButtonPair(string label, bool canEdit, FontAwesomeIcon icon,
        string tooltip, string permissionKey, bool isGlobalPerm, PermType type)
    {
        var _menuWidth = ImGui.GetContentRegionAvail().X - ImGui.GetFrameHeight();

        if (isGlobalPerm)
        {
            var permissions = UserPairForPerms.UserPair.OtherGlobalPerms;
            DrawPermissionPair(permissions, label, canEdit, icon, tooltip, permissionKey, type, _menuWidth, isGlobalPerm);
        }
        else
        {
            var permissions = UserPairForPerms.UserPair.OtherPairPerms;
            DrawPermissionPair(permissions, label, canEdit, icon, tooltip, permissionKey, type, _menuWidth, isGlobalPerm);
        }
    }

    /// <summary>
    /// Responsible for calling the correct display item based on the permission type and permission object
    /// </summary>
    /// <param name="permissions"> the permissions object that is either a globalpermission object or pair permission object </param>
    /// <param name="label"> the label to display for the permission object </param>
    /// <param name="canEdit"> if this permission is modifiable </param>
    /// <param name="icon"> the icon to display to the left-hand side </param>
    /// <param name="tooltip"> the tooltip to display when the item is hovered over. </param>
    /// <param name="permissionKey"> the permission attribute we are displaying </param>
    /// <param name="type"> what permission type it is (string, char, timespan, boolean) </param>
    /// <param name="_menuWidth"> the width of the item to display </param>
    /// <param name="isGlobalPerm"> if it is a global perm. if false, it means it is a pair permission. </param>
    private void DrawPermissionPair(object permissions, string label, bool canEdit, FontAwesomeIcon icon,
        string tooltip, string permissionKey, PermType type, float _menuWidth, bool isGlobalPerm)
    {
        bool _wasHovered = false;
        // if type is boolean
        if (type == PermType.YesNo)
        {
            bool newValueState = (bool)permissions.GetType().GetProperty(permissionKey)?.GetValue(permissions);
            if (_uiSharedService.IconTextButton(icon, label, _menuWidth, true, !canEdit))
            {
                SetPermissionPair(permissions, permissionKey, !newValueState, isGlobalPerm);
            }
            UiSharedService.AttachToolTip(tooltip);
        }
        else if (type == PermType.TimeSpan)
        {
            string timeSpanString = _uiSharedService.TimeSpanToString((TimeSpan)permissions.GetType().GetProperty(permissionKey)?.GetValue(permissions)!) ?? "0d0h0m0s";

            if (_uiSharedService.IconInputText(icon, label, "format 0d0h0m0s...", ref timeSpanString, 32, _menuWidth, true, !canEdit)) { /* Consume */ }
            // if deactivated set perm
            if (ImGui.IsItemDeactivatedAfterEdit() 
                && timeSpanString != _uiSharedService.TimeSpanToString((TimeSpan)permissions.GetType().GetProperty(permissionKey)?.GetValue(permissions)!)
                && canEdit)
            {
                if (_uiSharedService.TryParseTimeSpan(timeSpanString, out TimeSpan result))
                {
                    SetPermissionPair(permissions, permissionKey, result, isGlobalPerm);
                }
                else
                {
                    Logger.LogWarning("You tried to set an invalid timespan format. Please use the format 0d0h0m0s");
                    timeSpanString = _uiSharedService.TimeSpanToString((TimeSpan)permissions.GetType().GetProperty(permissionKey)?.GetValue(permissions)!) ?? "0d0h0m0s";
                }
            }
            else
            {
                timeSpanString = _uiSharedService.TimeSpanToString((TimeSpan)permissions.GetType().GetProperty(permissionKey)?.GetValue(permissions)!) ?? "0d0h0m0s";
            }
            // add tooltip
            UiSharedService.AttachToolTip(tooltip);
        }
        else if (type == PermType.String)
        {
            string inputStr = permissions.GetType().GetProperty(permissionKey)?.GetValue(permissions)?.ToString() ?? "EMPTY_FIELD";

            if(_uiSharedService.IconInputText(icon, label, "input here...", ref inputStr, 32, _menuWidth, true, !canEdit)) { /* Consume */ }
            // if deactivated set perm
            if (ImGui.IsItemDeactivatedAfterEdit() && canEdit)
            {
                if (inputStr != permissions.GetType().GetProperty(permissionKey)?.GetValue(permissions)?.ToString())
                {
                    SetPermissionPair(permissions, permissionKey, inputStr, isGlobalPerm);
                }
            }
            else
            {
                inputStr = permissions.GetType().GetProperty(permissionKey)?.GetValue(permissions)?.ToString() ?? "EMPTY_FIELD";
            }
            // add tooltip
            UiSharedService.AttachToolTip(tooltip);
        }
        else if (type == PermType.Char)
        {
            string inputChar = permissions.GetType().GetProperty(permissionKey)?.GetValue(permissions)?.ToString() ?? ".";

            if (_uiSharedService.IconInputText(icon, label, ".", ref inputChar, 1, _menuWidth, true, !canEdit)) { /* Consume */ }
            // if deactivated set perm
            if (ImGui.IsItemDeactivatedAfterEdit() && canEdit)
            {
                if (inputChar != permissions.GetType().GetProperty(permissionKey)?.GetValue(permissions)?.ToString())
                {
                    SetPermissionPair(permissions, permissionKey, inputChar, isGlobalPerm);
                }
            }
            else
            {
                inputChar = permissions.GetType().GetProperty(permissionKey)?.GetValue(permissions)?.ToString() ?? ".";
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
    private void SetPermissionPair(object permissions, string permissionKey, object newValue, bool isGlobalPerm)
    {
        // DO NOT UPDATE THIS HERE, WE WILL GET A CALLBACK FROM THE SERVER INFORMING US OF THE SUCCSS
        // permissions.GetType().GetProperty(permissionKey)?.SetValue(permissions, newValue);

        if (isGlobalPerm)
        {
            _ = _apiController.UserUpdateOtherGlobalPerm(new UserGlobalPermChangeDto(UserPairForPerms.UserData,
                new KeyValuePair<string, object>(permissionKey, newValue)));
        }
        else
        {
            _ = _apiController.UserUpdateOtherPairPerm(new UserPairPermChangeDto(UserPairForPerms.UserData,
                new KeyValuePair<string, object>(permissionKey, newValue)));
        }

        _logger.LogTrace($"Updated {UserPairForPerms.UserData.AliasOrUID}'s {permissionKey} to {newValue}");
    }



    /// <summary> The left side of the permissions row, containing the rows icon, and the help text menu </summary>
    private void DrawRowLeftSide()
    {
        // for now, we can set the userpairtext to empty
        string userPairText = string.Empty;

        // lets make sure that our grouped row is aligned to the frame padding
        ImGui.AlignTextToFramePadding();

        // if the user is offline, we will display the offline icon relative to pair status.
        if (!UserPairForPerms.IsOnline)
        {
            // display it red regardless, but
            using var _ = ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudRed);
            _uiSharedService.IconText(UserPairForPerms.IndividualPairStatus == IndividualPairStatus.OneSided // if one-sided
                ? FontAwesomeIcon.ArrowsLeftRight  // show the left-right arrows
                : FontAwesomeIcon.User);    // otherwise they are bidirectional, so display standard offline.
            userPairText = UserPairForPerms.UserData.AliasOrUID + " is offline";
        }
        // if the user is visible / present.
        else if (UserPairForPerms.IsVisible)
        {
            // display the green eye icon, since we can see them.
            _uiSharedService.IconText(FontAwesomeIcon.Eye, ImGuiColors.ParsedGreen);
            userPairText = UserPairForPerms.UserData.AliasOrUID + " is visible: " + UserPairForPerms.PlayerName
                + Environment.NewLine + "Click Icon to target player";
            if (ImGui.IsItemClicked())
            {
                Mediator.Publish(new TargetPairMessage(UserPairForPerms));
            }
        }
        else
        {
            using var _ = ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.HealerGreen);
            _uiSharedService.IconText(UserPairForPerms.IndividualPairStatus == IndividualPairStatus.Bidirectional
                ? FontAwesomeIcon.User : FontAwesomeIcon.Users);
            userPairText = UserPairForPerms.UserData.AliasOrUID + " is online";
        }

        if (UserPairForPerms.IndividualPairStatus == IndividualPairStatus.OneSided)
        {
            userPairText += UiSharedService.TooltipSeparator + "User has not added you back";
        }
        else if (UserPairForPerms.IndividualPairStatus == IndividualPairStatus.Bidirectional)
        {
            userPairText += UiSharedService.TooltipSeparator + "You are directly Paired";
        }

        UiSharedService.AttachToolTip(userPairText);

        ImGui.SameLine();

        var ySize = ImGui.GetCursorPosY();
        ImGui.SetWindowSize(new(400, ySize));
    }
}
