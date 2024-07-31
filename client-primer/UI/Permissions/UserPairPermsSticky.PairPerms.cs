using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Utility;
using GagSpeak.Services.Mediator;
using GagspeakAPI.Data.Enum;
using GagspeakAPI.Dto.Permissions;
using ImGuiNET;
using System.Security;

namespace GagSpeak.UI.Permissions;

/// <summary>
/// Contains functions relative to the paired users permissions for the client user.
/// </summary>
public partial class UserPairPermsSticky
{
    public void DrawPairPermsForClient()
    {
        /* ----------- GLOBAL SETTINGS ----------- */
        ImGui.TextUnformatted("Global Settings");

        DrawOtherPairSetting("LiveChatGarblerActive", "LiveChatGarblerActiveAllowed", // permission name and permission access name
            UserPairForPerms.UserPairGlobalPerms.LiveChatGarblerActive ? "Disable Live Chat Garbler" : "Enable Live Chat Garbler", 
            UserPairForPerms.UserPairGlobalPerms.LiveChatGarblerActive ? FontAwesomeIcon.Microphone : FontAwesomeIcon.MicrophoneSlash,
            UserPairForPerms.UserPairGlobalPerms.LiveChatGarblerActive ? (PairNickOrAliasOrUID + "'s Live Chat Garbler is currently Active. Click to disable. [Global]")
                                                                       : (PairNickOrAliasOrUID + "'s Live Chat Garbler is currently Inactive. Click to enable. [Global]"),
            UserPairForPerms.UserPairEditAccess.LiveChatGarblerActiveAllowed,
            PermissionType.Global, PermissionValueType.YesNo); // permission type and value type

        DrawOtherPairSetting("LiveChatGarblerLocked", "LiveChatGarblerLockedAllowed",
            UserPairForPerms.UserPairGlobalPerms.LiveChatGarblerLocked ? "Lock the Live Chat Garbler" : "Unlock the Live Chat Garbler",
            UserPairForPerms.UserPairGlobalPerms.LiveChatGarblerLocked ? FontAwesomeIcon.UnlockAlt : FontAwesomeIcon.Key,
            UserPairForPerms.UserPairGlobalPerms.LiveChatGarblerLocked ? (PairNickOrAliasOrUID + "'s Live Chat Garbler is currently Locked. Click to unlock. [Global]")
                                                                       : (PairNickOrAliasOrUID + "'s Live Chat Garbler is currently Unlocked. Click to lock. [Global]"),
            UserPairForPerms.UserPairEditAccess.LiveChatGarblerLockedAllowed,
            PermissionType.Global, PermissionValueType.YesNo);

        DrawOtherPairSetting("LockToyboxUI", "LockToyboxUIAllowed",
            UserPairForPerms.UserPairGlobalPerms.LockToyboxUI ? "Unlock Toybox UI" : "Lock Toybox UI",
            UserPairForPerms.UserPairGlobalPerms.LockToyboxUI ? FontAwesomeIcon.BoxOpen : FontAwesomeIcon.Box,
            UserPairForPerms.UserPairGlobalPerms.LockToyboxUI ? (PairNickOrAliasOrUID + "'s Toybox Feature Access is currently restricted. Click to make accessible. [Global]")
                                                              : (PairNickOrAliasOrUID + "'s Toybox Feature Access is currently accessible. Click to restrict access. [Global]"),
            UserPairForPerms.UserPairEditAccess.LockToyboxUIAllowed,
            PermissionType.Global, PermissionValueType.YesNo);
        
        ImGui.Separator();
        /* ----------- GAG PERMISSIONS ----------- */
        ImGui.TextUnformatted("Gag Permissions");

        DrawOtherPairSetting("ItemAutoEquip", "ItemAutoEquipAllowed",
            UserPairForPerms.UserPairGlobalPerms.ItemAutoEquip ? "Disable Gag Glamour's" : "Enable Gag Glamour's",
            UserPairForPerms.UserPairGlobalPerms.ItemAutoEquip ? FontAwesomeIcon.Surprise : FontAwesomeIcon.MehBlank,
            UserPairForPerms.UserPairGlobalPerms.ItemAutoEquip ? (PairNickOrAliasOrUID + " currently has Gag Glamour's Active. Click to disable. [Global]")
                                                               : (PairNickOrAliasOrUID + " currently has Gag Glamour's Disabled. Click to enable. [Global]"),
            UserPairForPerms.UserPairEditAccess.ItemAutoEquipAllowed,
            PermissionType.Global, PermissionValueType.YesNo);

        DrawOtherPairSetting("MaxLockTime", "MaxLockTimeAllowed",
            "Max Lock Time",
            FontAwesomeIcon.HourglassHalf,
            $"Max time {PairNickOrAliasOrUID} can lock your gags for.",
            UserPairForPerms.UserPairEditAccess.MaxLockTimeAllowed,
            PermissionType.Global, PermissionValueType.TimeSpan);

        DrawOtherPairSetting("ExtendedLockTimes", "ExtendedLockTimesAllowed",
            UserPairForPerms.UserPairUniquePairPerms.ExtendedLockTimes ? "Disable Extended Lock Times." : "Enable Extended Lock Times",
            UserPairForPerms.UserPairUniquePairPerms.ExtendedLockTimes ? FontAwesomeIcon.Ban : FontAwesomeIcon.Stopwatch,
            UserPairForPerms.UserPairUniquePairPerms.ExtendedLockTimes ? (PairNickOrAliasOrUID + " is allowing you to set locks longer than 1 hour. Click to disable.")
                                                                       : (PairNickOrAliasOrUID + " is preventing you from setting locks for longer than 1 hour. Click to enable."),
            UserPairForPerms.UserPairEditAccess.ExtendedLockTimesAllowed,
            PermissionType.UniquePairPerm, PermissionValueType.YesNo);


        ImGui.Separator();
        /* ----------- RESTRAINT SET PERMISSIONS ----------- */
        ImGui.TextUnformatted("Restraint Set Permissions");

        DrawOtherPairSetting("RestraintSetAutoEquip", "WardrobeEnabledAllowed",
            UserPairForPerms.UserPairGlobalPerms.RestraintSetAutoEquip ? "Disable Restraint Set Glamour's" : "Enable Restraint Set Glamour's",
            UserPairForPerms.UserPairGlobalPerms.RestraintSetAutoEquip ? FontAwesomeIcon.Ban : FontAwesomeIcon.Tshirt,
            UserPairForPerms.UserPairGlobalPerms.RestraintSetAutoEquip ? (PairNickOrAliasOrUID + " has Restraint Set Glamour's Active. Click to disable.")
                                                                       : (PairNickOrAliasOrUID + " has Restraint Set Glamour's Inactive. Click to enable."),
            UserPairForPerms.UserPairEditAccess.RestraintSetAutoEquipAllowed,
            PermissionType.Global, PermissionValueType.YesNo);

        DrawOtherPairSetting("ApplyRestraintSets", "ApplyRestraintSetsAllowed",
            UserPairForPerms.UserPairUniquePairPerms.ApplyRestraintSets ? "Disable Restraint Set Application" : "Enable Restraint Set Application",
            UserPairForPerms.UserPairUniquePairPerms.ApplyRestraintSets ? FontAwesomeIcon.Ban : FontAwesomeIcon.Female,
            UserPairForPerms.UserPairUniquePairPerms.ApplyRestraintSets ? (PairNickOrAliasOrUID + " is allowing you to apply restraint sets. Click to disable.")
                                                                        : (PairNickOrAliasOrUID + " is preventing you from applying restraint sets. Click to enable."),
            UserPairForPerms.UserPairEditAccess.ApplyRestraintSetsAllowed,
            PermissionType.UniquePairPerm, PermissionValueType.YesNo);

        DrawOtherPairSetting("LockRestraintSets", "LockRestraintSetsAllowed",
            UserPairForPerms.UserPairUniquePairPerms.LockRestraintSets ? "Disable Allowing Restraint Set Locking" : "Enable Allowing Restraint Set Locking",
            UserPairForPerms.UserPairUniquePairPerms.LockRestraintSets ? FontAwesomeIcon.ShopSlash : FontAwesomeIcon.ShopLock,
            UserPairForPerms.UserPairUniquePairPerms.LockRestraintSets ? (PairNickOrAliasOrUID + " is allowing you to lock restraint sets. Click to disable.")
                                                                       : (PairNickOrAliasOrUID + " is preventing you from locking restraint sets. Click to enable."),
            UserPairForPerms.UserPairEditAccess.LockRestraintSetsAllowed,
            PermissionType.UniquePairPerm, PermissionValueType.YesNo);

        DrawOtherPairSetting("MaxAllowedRestraintTime", "MaxAllowedRestraintTimeAllowed",
            UserPairForPerms.UserPairUniquePairPerms.MaxAllowedRestraintTime == TimeSpan.Zero ? "Set Max Lock Time" : "Change Max Lock Time",
            FontAwesomeIcon.HourglassHalf,
            $"Max time you can lock {PairNickOrAliasOrUID}'s restraint sets for.",
            UserPairForPerms.UserPairEditAccess.MaxAllowedRestraintTimeAllowed,
            PermissionType.UniquePairPerm, PermissionValueType.TimeSpan);

        DrawOtherPairSetting("RemoveRestraintSets", "RemoveRestraintSetsAllowed",
            UserPairForPerms.UserPairUniquePairPerms.RemoveRestraintSets ? "Disable Restraint Set Removal" : "Enable Restraint Set Removal",
            UserPairForPerms.UserPairUniquePairPerms.RemoveRestraintSets ? FontAwesomeIcon.Ban : FontAwesomeIcon.Key,
            UserPairForPerms.UserPairUniquePairPerms.RemoveRestraintSets ? (PairNickOrAliasOrUID + " is allowing you to remove restraint sets. Click to disable.")
                                                                         : (PairNickOrAliasOrUID + " is preventing you from removing restraint sets. Click to enable."),
            UserPairForPerms.UserPairEditAccess.RemoveRestraintSetsAllowed,
            PermissionType.UniquePairPerm, PermissionValueType.YesNo);


        ImGui.Separator();
        /* ----------- PUPPETEER PERMISSIONS ----------- */
        ImGui.TextUnformatted("Puppeteer Permissions");

        DrawOtherPairSetting("AllowSitRequests", "AllowSitRequestsAllowed",
            UserPairForPerms.UserPairUniquePairPerms.AllowSitRequests ? "Disable Sit Requests" : "Enable Sit Requests",
            UserPairForPerms.UserPairUniquePairPerms.AllowSitRequests ? FontAwesomeIcon.Ban : FontAwesomeIcon.Chair,
            UserPairForPerms.UserPairUniquePairPerms.AllowSitRequests ? (PairNickOrAliasOrUID + " is allowing you to make them /sit. Click to disable.")
                                                                      : (PairNickOrAliasOrUID + " is preventing you from making them /sit. Click to enable."),
            UserPairForPerms.UserPairEditAccess.AllowSitRequestsAllowed,
            PermissionType.UniquePairPerm, PermissionValueType.YesNo);

        DrawOtherPairSetting("AllowMotionRequests", "AllowMotionRequestsAllowed",
            (UserPairForPerms.UserPairUniquePairPerms.AllowMotionRequests ? "Disable Motion Requests" : "Enable Motion Requests"),
            (UserPairForPerms.UserPairUniquePairPerms.AllowMotionRequests ? FontAwesomeIcon.Ban : FontAwesomeIcon.Walking),
            (UserPairForPerms.UserPairUniquePairPerms.AllowMotionRequests ? (PairNickOrAliasOrUID + " is allowing you to make them do expressions and emotes. Click to disable.")
                                                                          : (PairNickOrAliasOrUID + " is preventing you from making them do expressions and emotes. Click to enable.")),
            UserPairForPerms.UserPairEditAccess.AllowMotionRequestsAllowed,
            PermissionType.UniquePairPerm, PermissionValueType.YesNo);

        DrawOtherPairSetting("AllowAllRequests", "AllowAllRequestsAllowed",
            (UserPairForPerms.UserPairUniquePairPerms.AllowAllRequests ? "Disable All Requests" : "Enable All Requests"),
            (UserPairForPerms.UserPairUniquePairPerms.AllowAllRequests ? FontAwesomeIcon.Lock : FontAwesomeIcon.Unlock),
            (UserPairForPerms.UserPairUniquePairPerms.AllowAllRequests ? (PairNickOrAliasOrUID + " is allowing you to make them do anything. Click to disable.")
                                                                      : (PairNickOrAliasOrUID + " is preventing you from making them do anything. Click to enable.")),
            (UserPairForPerms.UserPairEditAccess.AllowAllRequestsAllowed),
            PermissionType.UniquePairPerm, PermissionValueType.YesNo);


        /* ----------- MOODLES PERMISSIONS ----------- *//*
        ImGui.TextUnformatted("Moodles Permissions");

        DrawOtherPairSetting("Positive Status Types Allowed", "AllowPositiveStatusTypes", UserPairForPerms.UserPair.OtherPairPerms.AllowPositiveStatusTypes,
            UserPairForPerms.UserPair.OtherEditAccessPerms.AllowPositiveStatusTypesAllowed, FontAwesomeIcon.Plus, FontAwesomeIcon.Plus, false, PermissionValueType.YesNo);

        DrawOtherPairSetting("Negative Status Types Allowed", "AllowNegativeStatusTypes", UserPairForPerms.UserPair.OtherPairPerms.AllowNegativeStatusTypes,
            UserPairForPerms.UserPair.OtherEditAccessPerms.AllowNegativeStatusTypesAllowed, FontAwesomeIcon.Minus, FontAwesomeIcon.Minus, false, PermissionValueType.YesNo);

        DrawOtherPairSetting("Special Status Types Allowed", "AllowSpecialStatusTypes", UserPairForPerms.UserPair.OtherPairPerms.AllowSpecialStatusTypes,
            UserPairForPerms.UserPair.OtherEditAccessPerms.AllowSpecialStatusTypesAllowed, FontAwesomeIcon.Question, FontAwesomeIcon.Question, false, PermissionValueType.YesNo);

        DrawOtherPairSetting($"Can Apply {UserPairForPerms.UserData.AliasOrUID}'s Moodles", "PairCanApplyOwnMoodlesToYou", UserPairForPerms.UserPair.OtherPairPerms.PairCanApplyOwnMoodlesToYou,
            UserPairForPerms.UserPair.OtherEditAccessPerms.PairCanApplyOwnMoodlesToYouAllowed, FontAwesomeIcon.User, FontAwesomeIcon.User, false, PermissionValueType.YesNo);

        DrawOtherPairSetting($"Can Apply Your Moodles", "PairCanApplyYourMoodlesToYou", UserPairForPerms.UserPair.OtherPairPerms.PairCanApplyYourMoodlesToYou,
            UserPairForPerms.UserPair.OtherEditAccessPerms.PairCanApplyYourMoodlesToYouAllowed, FontAwesomeIcon.User, FontAwesomeIcon.User, false, PermissionValueType.YesNo);

        DrawOtherPairSetting("Max Moodle Time", "MaxMoodleTime", UserPairForPerms.UserPair.OtherPairPerms.MaxMoodleTime,
            UserPairForPerms.UserPair.OtherEditAccessPerms.MaxMoodleTimeAllowed, FontAwesomeIcon.Clock, FontAwesomeIcon.Clock, false, PermissionValueType.TimeSpan);

        DrawOtherPairSetting("Permanent Moodles Allowed", "AllowPermanentMoodles", UserPairForPerms.UserPair.OtherPairPerms.AllowPermanentMoodles,
            UserPairForPerms.UserPair.OtherEditAccessPerms.AllowPermanentMoodlesAllowed, FontAwesomeIcon.Clock, FontAwesomeIcon.Clock, false, PermissionValueType.YesNo);

        *//* ----------- TOYBOX PERMISSIONS ----------- *//*
        ImGui.TextUnformatted("Toybox Permissions");

        DrawOtherPairSetting("Toy State Changing", "ChangeToyState", UserPairForPerms.UserPair.OtherPairPerms.ChangeToyState,
            UserPairForPerms.UserPair.OtherEditAccessPerms.ChangeToyStateAllowed, FontAwesomeIcon.Cogs, FontAwesomeIcon.Cogs, false, PermissionValueType.YesNo);

        DrawOtherPairSetting("Intensity Control", "CanControlIntensity", UserPairForPerms.UserPair.OtherPairPerms.CanControlIntensity,
            UserPairForPerms.UserPair.OtherEditAccessPerms.CanControlIntensityAllowed, FontAwesomeIcon.SlidersH, FontAwesomeIcon.SlidersH, false, PermissionValueType.YesNo);

        DrawOtherPairSetting("Can View Alarms", "VibratorAlarms", UserPairForPerms.UserPair.OtherPairPerms.VibratorAlarms,
            UserPairForPerms.UserPair.OtherEditAccessPerms.VibratorAlarmsAllowed, FontAwesomeIcon.Bell, FontAwesomeIcon.Bell, false, PermissionValueType.YesNo);

        DrawOtherPairSetting("Can Toggle Alarms", "VibratorAlarmsToggle", UserPairForPerms.UserPair.OtherPairPerms.VibratorAlarmsToggle,
    UserPairForPerms.UserPair.OtherEditAccessPerms.VibratorAlarmsToggleAllowed, FontAwesomeIcon.Wifi, FontAwesomeIcon.Wifi, false, PermissionValueType.YesNo);

        DrawOtherPairSetting("Realtime Vibe Remote", "CanUseRealtimeVibeRemote", UserPairForPerms.UserPair.OtherPairPerms.CanUseRealtimeVibeRemote,
            UserPairForPerms.UserPair.OtherEditAccessPerms.CanUseRealtimeVibeRemoteAllowed, FontAwesomeIcon.Wifi, FontAwesomeIcon.Wifi, false, PermissionValueType.YesNo);

        DrawOtherPairSetting("Cab Execute Patterns", "CanExecutePatterns", UserPairForPerms.UserPair.OtherPairPerms.CanExecutePatterns,
            UserPairForPerms.UserPair.OtherEditAccessPerms.CanExecutePatternsAllowed, FontAwesomeIcon.Wifi, FontAwesomeIcon.Wifi, false, PermissionValueType.YesNo);

        DrawOtherPairSetting("Can Execute Triggers", "CanExecuteTriggers", UserPairForPerms.UserPair.OtherPairPerms.CanExecuteTriggers,
            UserPairForPerms.UserPair.OtherEditAccessPerms.CanExecuteTriggersAllowed, FontAwesomeIcon.Wifi, FontAwesomeIcon.Wifi, false, PermissionValueType.YesNo);

        DrawOtherPairSetting("Can Send Triggers", "CanSendTriggers", UserPairForPerms.UserPair.OtherPairPerms.CanSendTriggers,
            UserPairForPerms.UserPair.OtherEditAccessPerms.CanSendTriggersAllowed, FontAwesomeIcon.Wifi, FontAwesomeIcon.Wifi, false, PermissionValueType.YesNo);

        *//* ----------- HARDCORE PERMISSIONS ----------- *//*
        ImGui.TextUnformatted("Hardcore Permissions");

        DrawOtherPairSetting($"{UserPairForPerms.UserData.AliasOrUID} forced to Follow", "IsForcedToFollow", UserPairForPerms.UserPair.OtherPairPerms.IsForcedToFollow,
            UserPairForPerms.UserPair.OtherPairPerms.AllowForcedFollow, FontAwesomeIcon.ArrowsAlt, FontAwesomeIcon.ArrowsAlt, false, PermissionValueType.YesNo);

        DrawOtherPairSetting($"{UserPairForPerms.UserData.AliasOrUID} forced to Sit", "IsForcedToSit", UserPairForPerms.UserPair.OtherPairPerms.IsForcedToSit,
            UserPairForPerms.UserPair.OtherPairPerms.AllowForcedSit, FontAwesomeIcon.Chair, FontAwesomeIcon.Chair, false, PermissionValueType.YesNo);

        DrawOtherPairSetting($"{UserPairForPerms.UserData.AliasOrUID} forced to Stay", "IsForcedToStay", UserPairForPerms.UserPair.OtherPairPerms.IsForcedToStay,
            UserPairForPerms.UserPair.OtherPairPerms.AllowForcedToStay, FontAwesomeIcon.User, FontAwesomeIcon.User, false, PermissionValueType.YesNo);

        DrawOtherPairSetting($"{UserPairForPerms.UserData.AliasOrUID} blindfolded", "IsBlindfolded", UserPairForPerms.UserPair.OtherPairPerms.IsBlindfolded,
            UserPairForPerms.UserPair.OtherPairPerms.AllowBlindfold, FontAwesomeIcon.Blind, FontAwesomeIcon.Blind, false, PermissionValueType.YesNo);*/
    }

    /// <summary>
    /// The primary call for displaying a setting for the client permissions.
    /// <para>
    /// These are formatted slightly different than the ClientPairPerms. Instead of having an interactable checkbox, 
    /// you'll see a colored lock/unlock icon. Red lock indicates they have not given you edit access, and green unlock means they have.
    /// </para>
    /// <para>
    /// Additionally, the condition for modifying the permission is not based on hardcore mode, but instead the edit access permission.
    /// </para>
    /// </summary>
    /// <param name="permissionName"> The name of the unique pair perm in string format. </param>
    /// <param name="permissionAccessName"> The name of the pair perm edit access in string format </param>
    /// <param name="textLabel"> The text to display beside the icon </param>
    /// <param name="icon"> The icon to display to the left of the text. </param>
    /// <param name="canChange"> If the permission (not edit access) can be changed. </param>
    /// <param name="tooltipStr"> the tooltip to display when hovered. </param>
    /// <param name="permissionType"> If the permission is a global perm, unique pair perm, or access permission. </param>
    /// <param name="permissionValueType"> what permission type it is (string, char, timespan, boolean) </param>
    private void DrawOtherPairSetting(string permissionName, string permissionAccessName, string textLabel, FontAwesomeIcon icon,
        string tooltipStr, bool canChange, PermissionType permissionType, PermissionValueType permissionValueType)
    {
        try
        {
            switch (permissionType)
            {
                case PermissionType.Global:
                    DrawOtherPairPermission(permissionType, UserPairForPerms.UserPairGlobalPerms, textLabel, icon, tooltipStr, canChange,
                        permissionName, permissionValueType);
                    break;
                case PermissionType.UniquePairPerm:
                    DrawOtherPairPermission(permissionType, UserPairForPerms.UserPairUniquePairPerms, textLabel, icon, tooltipStr, canChange,
                        permissionName, permissionValueType);
                    break;
                // this case should technically never be called for this particular instance.
                case PermissionType.UniquePairPermEditAccess:
                    DrawOtherPairPermission(permissionType, UserPairForPerms.UserPairEditAccess, textLabel, icon, tooltipStr, canChange,
                        permissionName, permissionValueType);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to update permissions :: {ex}");
        }
    }

    /// <summary>
    /// Responsible for calling the correct display item based on the permission type and permission object
    /// </summary>
    /// <param name="permissionType"> the type of permission we are displaying </param>
    /// <param name="permissionSet"> the permission object we are displaying </param>
    /// <param name="label"> the text label to display beside the icon </param>
    /// <param name="icon"> the icon to display beside the text </param>
    /// <param name="tooltip"> the tooltip to display when hovered </param>
    /// <param name="permissionName"> the name of the permission we are displaying </param>
    /// <param name="type"> the type of permission value we are displaying </param>
    /// <param name="permissionAccessName"> the name of the permission access we are displaying </param>
    private void DrawOtherPairPermission(PermissionType permissionType, object permissionSet, string label,
        FontAwesomeIcon icon, string tooltip, bool hasAccess, string permissionName, PermissionValueType type)
    {

        // firstly, if the permission value type is a boolean, then process handling the change as a true/false.
        if (type == PermissionValueType.YesNo)
        {
            // localize the object as a boolean value from its property name.
            bool currValState = (bool)permissionSet.GetType().GetProperty(permissionName)?.GetValue(permissionSet)!;
            // draw the iconTextButton and checkbox beside it. Because we are in control, unless in hardcore, this should never be disabled.
            using (var group = ImRaii.Group())
            {
                // have a special case, where we mark the button as disabled if UserPairForPerms.UserPairGlobalPerms.LiveChatGarblerLocked is true
                if (_uiShared.IconTextButton(icon, label, IconButtonTextWidth, true, !hasAccess))
                {
                    SetOtherPairPermission(permissionType, permissionName, !currValState);
                }
                UiSharedService.AttachToolTip(tooltip);
                // display the respective lock/unlock icon based on the edit access permission.
                _uiShared.BooleanToColoredIcon(hasAccess, true, FontAwesomeIcon.Unlock, FontAwesomeIcon.Lock);
                // attach tooltip to it.
                UiSharedService.AttachToolTip(!hasAccess
                    ? ("Only " + PairNickOrAliasOrUID + " may update this setting. (They have not given you override access)")
                    : (PairNickOrAliasOrUID + " has allowed you to override their permission state at will."));
            }
        }
        // next, handle it if it is a timespan value.
        if (type == PermissionValueType.TimeSpan)
        {
            // attempt to parse the timespan value to a string.
            string timeSpanString = _uiShared.TimeSpanToString((TimeSpan)permissionSet.GetType().GetProperty(permissionName)?.GetValue(permissionSet)!) ?? "0d0h0m0s";

            using (var group = ImRaii.Group())
            {
                // draw the iconTextButton and checkbox beside it. Because we are in control, unless in hardcore, this should never be disabled.
                if (_uiShared.IconInputText(icon, label, "format 0d0h0m0s...", ref timeSpanString, 32, IconButtonTextWidth*.5f, true, !hasAccess)) { }
                // Set the permission once deactivated. If invalid, set to default.
                if (ImGui.IsItemDeactivatedAfterEdit()
                    && timeSpanString != _uiShared.TimeSpanToString((TimeSpan)permissionSet.GetType().GetProperty(permissionName)?.GetValue(permissionSet)!))
                {
                    // attempt to parse the string back into a valid timespan.
                    if (_uiShared.TryParseTimeSpan(timeSpanString, out TimeSpan result))
                    {
                        SetOtherPairPermission(permissionType, permissionName, result);
                    }
                    else
                    {
                        // find some way to print this to the chat or something.
                        _logger.LogWarning("You tried to set an invalid timespan format. Please use the format 0d0h0m0s");
                        InteractionSuccessful = false;
                        timeSpanString = "0d0h0m0s";
                    }
                }
                UiSharedService.AttachToolTip(tooltip);
                // display the respective lock/unlock icon based on the edit access permission.
                _uiShared.BooleanToColoredIcon(hasAccess, true, FontAwesomeIcon.Lock, FontAwesomeIcon.Unlock);
                // attach tooltip to it.
                UiSharedService.AttachToolTip(!hasAccess
                    ? ("Only " + PairNickOrAliasOrUID + " may update this setting. (They have not given you override access)")
                    : (PairNickOrAliasOrUID + " has allowed you to override their permission state at will."));
            }
        }
    }

    /// <summary>
    /// Send the updated permission we made for ourselves to the server.
    /// </summary>
    /// <param name="permissionType"> If Global, UniquePairPerm, or EditAccessPerm. </param>
    /// <param name="permissionName"> the attribute of the object we are changing</param>
    /// <param name="newValue"> New value to set. </param>
    private void SetOtherPairPermission(PermissionType permissionType, string permissionName, object newValue)
    {
        // Call the update to the server.
        switch (permissionType)
        {
            case PermissionType.Global:
                {
                    _logger.LogTrace($"Updated Other pair's global permission: {permissionName} to {newValue}");
                    _ = _apiController.UserUpdateOtherGlobalPerm(new UserGlobalPermChangeDto(UserPairForPerms.UserData,
                        new KeyValuePair<string, object>(permissionName, newValue)));
                }
                break;
            case PermissionType.UniquePairPerm:
                {
                    _logger.LogTrace($"Updated other pair's unique pair permission: {permissionName} to {newValue}");
                    _ = _apiController.UserUpdateOtherPairPerm(new UserPairPermChangeDto(UserPairForPerms.UserData,
                        new KeyValuePair<string, object>(permissionName, newValue)));
                }
                break;
        }
    }
}
