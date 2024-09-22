using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Utility;
using GagSpeak.Services.Mediator;
using GagspeakAPI.Enums;
using GagspeakAPI.Dto.Permissions;
using ImGuiNET;
using System.Security;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TaskbarClock;

namespace GagSpeak.UI.Permissions;

/// <summary>
/// Contains functions relative to the paired users permissions for the client user.
/// </summary>
public partial class PairStickyUI
{
    public void DrawPairPermsForClient()
    {
        /* ----------- GLOBAL SETTINGS ----------- */
        ImGui.TextUnformatted("Global Settings");

        DrawOtherPairSetting("LiveChatGarblerActive", "LiveChatGarblerActiveAllowed", // permission name and permission access name
            UserPairForPerms.UserPairGlobalPerms.LiveChatGarblerActive ? (PairUID+"'s Chat Garbler is Active") : (PairUID+"'s Chat Garbler is Inactive"),
            UserPairForPerms.UserPairGlobalPerms.LiveChatGarblerActive ? FontAwesomeIcon.MicrophoneSlash : FontAwesomeIcon.Microphone,
            UserPairForPerms.UserPairEditAccess.LiveChatGarblerActiveAllowed ? ("Press to Toggle" + PairNickOrAliasOrUID + "'s permission state. [Global]") : ("You Can't Change " + PairNickOrAliasOrUID + "'s Permission here."),
            UserPairForPerms.UserPairEditAccess.LiveChatGarblerActiveAllowed,
            PermissionType.Global, PermissionValueType.YesNo); // permission type and value type

        DrawOtherPairSetting("LiveChatGarblerLocked", "LiveChatGarblerLockedAllowed",
            UserPairForPerms.UserPairGlobalPerms.LiveChatGarblerLocked ? (PairUID + "'s Chat Garbler is Locked") : (PairUID + "'s Chat Garbler is Unlocked"),
            UserPairForPerms.UserPairGlobalPerms.LiveChatGarblerLocked ? FontAwesomeIcon.Key : FontAwesomeIcon.UnlockAlt,
            UserPairForPerms.UserPairEditAccess.LiveChatGarblerLockedAllowed ? ("Press to Toggle" + PairNickOrAliasOrUID + "'s permission state. [Global]") : ("You Can't Change " + PairNickOrAliasOrUID + "'s Permission here."),
            UserPairForPerms.UserPairEditAccess.LiveChatGarblerLockedAllowed,
            PermissionType.Global, PermissionValueType.YesNo);

        DrawOtherPairSetting("LockToyboxUI", "LockToyboxUIAllowed",
            UserPairForPerms.UserPairGlobalPerms.LockToyboxUI ? (PairUID + "'s Toybox UI is Restricted") : (PairUID + "'s Toybox UI is Accessible"),
            UserPairForPerms.UserPairGlobalPerms.LockToyboxUI ? FontAwesomeIcon.Box : FontAwesomeIcon.BoxOpen,
            UserPairForPerms.UserPairEditAccess.LockToyboxUIAllowed ? ("Press to Toggle" + PairNickOrAliasOrUID + "'s permission state. [Global]") : ("You Can't Change " + PairNickOrAliasOrUID + "'s Permission here."),
            UserPairForPerms.UserPairEditAccess.LockToyboxUIAllowed,
            PermissionType.Global, PermissionValueType.YesNo);
        
        ImGui.Separator();
        /* ----------- GAG PERMISSIONS ----------- */
        ImGui.TextUnformatted("Gag Permissions");


        DrawOtherPairSetting("GagFeatures", "GagFeaturesAllowed",
            UserPairForPerms.UserPairUniquePairPerms.GagFeatures ? (PairUID + " enabled Gag Interactions") : (PairUID + " disabled Gag Interactions"),
            UserPairForPerms.UserPairUniquePairPerms.GagFeatures ? FontAwesomeIcon.CheckCircle : FontAwesomeIcon.Ban,
            UserPairForPerms.UserPairEditAccess.GagFeaturesAllowed ? ("Press to Toggle" + PairNickOrAliasOrUID + "'s permission state.") : ("You Can't Change " + PairNickOrAliasOrUID + "'s Permission here."),
            UserPairForPerms.UserPairEditAccess.GagFeaturesAllowed,
            PermissionType.UniquePairPerm, PermissionValueType.YesNo);

        DrawOtherPairSetting("ItemAutoEquip", "ItemAutoEquipAllowed",
            UserPairForPerms.UserPairGlobalPerms.ItemAutoEquip ? (PairUID + " has Gag Glamours Enabled") : (PairUID + " has Gag Glamours Disabled"),
            UserPairForPerms.UserPairGlobalPerms.ItemAutoEquip ? FontAwesomeIcon.Surprise : FontAwesomeIcon.MehBlank,
            UserPairForPerms.UserPairEditAccess.ItemAutoEquipAllowed ? ("Press to Toggle" + PairNickOrAliasOrUID + "'s permission state. [Global]") : ("You Can't Change " + PairNickOrAliasOrUID + "'s Permission here."),
            UserPairForPerms.UserPairEditAccess.ItemAutoEquipAllowed,
            PermissionType.Global, PermissionValueType.YesNo);

        DrawOtherPairSetting("MaxLockTime", "MaxLockTimeAllowed",
            UserPairForPerms.UserPairUniquePairPerms.MaxLockTime == TimeSpan.Zero ? "Set Max Lock Time" : "Change Max Lock Time",
            FontAwesomeIcon.HourglassHalf,
            $"Max time {PairNickOrAliasOrUID} can lock your gags for.",
            UserPairForPerms.UserPairEditAccess.MaxLockTimeAllowed,
            PermissionType.UniquePairPerm, PermissionValueType.TimeSpan);

        DrawOtherPairSetting("ExtendedLockTimes", "ExtendedLockTimesAllowed",
            UserPairForPerms.UserPairUniquePairPerms.ExtendedLockTimes ? (PairUID + " allows Extended Locks") : (PairUID + " prevents Extended Locks"),
            UserPairForPerms.UserPairUniquePairPerms.ExtendedLockTimes ? FontAwesomeIcon.Stopwatch : FontAwesomeIcon.Ban,
            UserPairForPerms.UserPairEditAccess.ExtendedLockTimesAllowed ? ("Press to Toggle" + PairNickOrAliasOrUID + "'s permission state.") : ("You Can't Change " + PairNickOrAliasOrUID + "'s Permission here."),
            UserPairForPerms.UserPairEditAccess.ExtendedLockTimesAllowed,
            PermissionType.UniquePairPerm, PermissionValueType.YesNo);

        DrawOtherPairSetting("OwnerLocks", "OwnerLocksAllowed",
            UserPairForPerms.UserPairUniquePairPerms.OwnerLocks ? (PairUID + " allows Owner Locks") : (PairUID + " prevents Owner Locks"),
            UserPairForPerms.UserPairUniquePairPerms.OwnerLocks ? FontAwesomeIcon.Lock : FontAwesomeIcon.Ban,
            UserPairForPerms.UserPairEditAccess.OwnerLocksAllowed ? ("Press to Toggle" + PairNickOrAliasOrUID + "'s permission state.") : ("You Can't Change " + PairNickOrAliasOrUID + "'s Permission here."),
            UserPairForPerms.UserPairEditAccess.OwnerLocksAllowed,
            PermissionType.UniquePairPerm, PermissionValueType.YesNo);


        ImGui.Separator();
        /* ----------- RESTRAINT SET PERMISSIONS ----------- */
        ImGui.TextUnformatted("Restraint Set Permissions");

        // Rewrite all of the below functions but using the format above
        DrawOtherPairSetting("RestraintSetAutoEquip", "RestraintSetAutoEquipAllowed",
            UserPairForPerms.UserPairGlobalPerms.RestraintSetAutoEquip ? (PairUID + " has Restraint Glamours Enabled") : (PairUID + " has Restraint Glamours Disabled"),
            UserPairForPerms.UserPairGlobalPerms.RestraintSetAutoEquip ? FontAwesomeIcon.Tshirt : FontAwesomeIcon.Ban,
            UserPairForPerms.UserPairEditAccess.RestraintSetAutoEquipAllowed ? ("Press to Toggle" + PairNickOrAliasOrUID + "'s permission state.") : ("You Can't Change " + PairNickOrAliasOrUID + "'s Permission here."),
            UserPairForPerms.UserPairEditAccess.RestraintSetAutoEquipAllowed,
            PermissionType.Global, PermissionValueType.YesNo);

        DrawOtherPairSetting("ApplyRestraintSets", "ApplyRestraintSetsAllowed",
            UserPairForPerms.UserPairUniquePairPerms.ApplyRestraintSets ? (PairUID + " allows Applying Restraints") : (PairUID + " prevents Applying Restraints"),
            UserPairForPerms.UserPairUniquePairPerms.ApplyRestraintSets ? FontAwesomeIcon.Female : FontAwesomeIcon.Ban,
            UserPairForPerms.UserPairEditAccess.ApplyRestraintSetsAllowed ? ("Press to Toggle" + PairNickOrAliasOrUID + "'s permission state.") : ("You Can't Change " + PairNickOrAliasOrUID + "'s Permission here."),
            UserPairForPerms.UserPairEditAccess.ApplyRestraintSetsAllowed,
            PermissionType.UniquePairPerm, PermissionValueType.YesNo);

        DrawOtherPairSetting("LockRestraintSets", "LockRestraintSetsAllowed",
            UserPairForPerms.UserPairUniquePairPerms.LockRestraintSets ? (PairUID + " allows Locking Restraints") : (PairUID + " prevents Locking Restraints"),
            UserPairForPerms.UserPairUniquePairPerms.LockRestraintSets ? FontAwesomeIcon.Lock : FontAwesomeIcon.ShopSlash,
            UserPairForPerms.UserPairEditAccess.LockRestraintSetsAllowed ? ("Press to Toggle" + PairNickOrAliasOrUID + "'s permission state.") : ("You Can't Change " + PairNickOrAliasOrUID + "'s Permission here."),
            UserPairForPerms.UserPairEditAccess.LockRestraintSetsAllowed,
            PermissionType.UniquePairPerm, PermissionValueType.YesNo);

        DrawOtherPairSetting("MaxAllowedRestraintTime", "MaxAllowedRestraintTimeAllowed",
            UserPairForPerms.UserPairUniquePairPerms.MaxAllowedRestraintTime == TimeSpan.Zero ? "Set Max Lock Time" : "Change Max Lock Time",
            FontAwesomeIcon.HourglassHalf,
            UserPairForPerms.UserPairEditAccess.MaxAllowedRestraintTimeAllowed ? ("Press to Toggle" + PairNickOrAliasOrUID + "'s permission state.") : ("You Can't Change " + PairNickOrAliasOrUID + "'s Permission here."),
            UserPairForPerms.UserPairEditAccess.MaxAllowedRestraintTimeAllowed,
            PermissionType.UniquePairPerm, PermissionValueType.TimeSpan);

        DrawOtherPairSetting("UnlockRestraintSets", "UnlockRestraintSetsAllowed",
            UserPairForPerms.UserPairUniquePairPerms.UnlockRestraintSets ? (PairUID + " allows Unlocking Restraints") : (PairUID + " prevents Unlocking Restraints"),
            UserPairForPerms.UserPairUniquePairPerms.UnlockRestraintSets ? FontAwesomeIcon.LockOpen : FontAwesomeIcon.Ban,
            UserPairForPerms.UserPairEditAccess.UnlockRestraintSetsAllowed ? ("Press to Toggle" + PairNickOrAliasOrUID + "'s permission state.") : ("You Can't Change " + PairNickOrAliasOrUID + "'s Permission here."),
            UserPairForPerms.UserPairEditAccess.UnlockRestraintSetsAllowed,
            PermissionType.UniquePairPerm, PermissionValueType.YesNo);

        DrawOtherPairSetting("RemoveRestraintSets", "RemoveRestraintSetsAllowed",
            UserPairForPerms.UserPairUniquePairPerms.RemoveRestraintSets ? (PairUID + " allows Removing Restraints") : (PairUID + " prevents Removing Restraints"),
            UserPairForPerms.UserPairUniquePairPerms.RemoveRestraintSets ? FontAwesomeIcon.Key : FontAwesomeIcon.Ban,
            UserPairForPerms.UserPairEditAccess.RemoveRestraintSetsAllowed ? ("Press to Toggle" + PairNickOrAliasOrUID + "'s permission state.") : ("You Can't Change " + PairNickOrAliasOrUID + "'s Permission here."),
            UserPairForPerms.UserPairEditAccess.RemoveRestraintSetsAllowed,
            PermissionType.UniquePairPerm, PermissionValueType.YesNo);

        ImGui.Separator();
        /* ----------- PUPPETEER PERMISSIONS ----------- */
        ImGui.TextUnformatted("Puppeteer Permissions");

        DrawOtherPairSetting("AllowSitRequests", "AllowSitRequestsAllowed",
            UserPairForPerms.UserPairUniquePairPerms.AllowSitRequests ? (PairUID + " allows Sit Requests") : (PairUID + " prevents Sit Requests"),
            UserPairForPerms.UserPairUniquePairPerms.AllowSitRequests ? FontAwesomeIcon.Chair : FontAwesomeIcon.Ban,
            UserPairForPerms.UserPairEditAccess.AllowSitRequestsAllowed ? ("Press to Toggle" + PairNickOrAliasOrUID + "'s permission state.") : ("You Can't Change " + PairNickOrAliasOrUID + "'s Permission here."),
            UserPairForPerms.UserPairEditAccess.AllowSitRequestsAllowed,
            PermissionType.UniquePairPerm, PermissionValueType.YesNo);

        DrawOtherPairSetting("AllowMotionRequests", "AllowMotionRequestsAllowed",
            UserPairForPerms.UserPairUniquePairPerms.AllowMotionRequests ? (PairUID + " allows Motion Requests") : (PairUID + " prevents Motion Requests"),
            UserPairForPerms.UserPairUniquePairPerms.AllowMotionRequests ? FontAwesomeIcon.Walking : FontAwesomeIcon.Ban,
            UserPairForPerms.UserPairEditAccess.AllowMotionRequestsAllowed ? ("Press to Toggle" + PairNickOrAliasOrUID + "'s permission state.") : ("You Can't Change " + PairNickOrAliasOrUID + "'s Permission here."),
            UserPairForPerms.UserPairEditAccess.AllowMotionRequestsAllowed,
            PermissionType.UniquePairPerm, PermissionValueType.YesNo);

        DrawOtherPairSetting("AllowAllRequests", "AllowAllRequestsAllowed",
            UserPairForPerms.UserPairUniquePairPerms.AllowAllRequests ? (PairUID + " allows All Requests") : (PairUID + " prevents All Requests"),
            UserPairForPerms.UserPairUniquePairPerms.AllowAllRequests ? FontAwesomeIcon.Unlock : FontAwesomeIcon.Lock,
            UserPairForPerms.UserPairEditAccess.AllowAllRequestsAllowed ? ("Press to Toggle" + PairNickOrAliasOrUID + "'s permission state.") : ("You Can't Change " + PairNickOrAliasOrUID + "'s Permission here."),
            UserPairForPerms.UserPairEditAccess.AllowAllRequestsAllowed,
            PermissionType.UniquePairPerm, PermissionValueType.YesNo);

        ImGui.Separator();
        /* ----------- MOODLES PERMISSIONS ----------- */
        ImGui.TextUnformatted("Moodles Permissions");

        DrawOtherPairSetting("AllowPositiveStatusTypes", "AllowPositiveStatusTypesAllowed",
            UserPairForPerms.UserPairUniquePairPerms.AllowPositiveStatusTypes ? (PairUID + " allows Positive Moodles") : (PairUID + " prevents Positive Moodles"),
            UserPairForPerms.UserPairUniquePairPerms.AllowPositiveStatusTypes ? FontAwesomeIcon.SmileBeam : FontAwesomeIcon.Ban,
            UserPairForPerms.UserPairEditAccess.AllowPositiveStatusTypesAllowed ? ("Press to Toggle " + PairNickOrAliasOrUID + "'s permission state.") : ("You Can't Change " + PairNickOrAliasOrUID + "'s Permission here."),
            UserPairForPerms.UserPairEditAccess.AllowPositiveStatusTypesAllowed,
            PermissionType.UniquePairPerm, PermissionValueType.YesNo);

        DrawOtherPairSetting("AllowNegativeStatusTypes", "AllowNegativeStatusTypesAllowed",
            UserPairForPerms.UserPairUniquePairPerms.AllowNegativeStatusTypes ? (PairUID + " allows Negative Moodles") : (PairUID + " prevents Negative Moodles"),
            UserPairForPerms.UserPairUniquePairPerms.AllowNegativeStatusTypes ? FontAwesomeIcon.FrownOpen : FontAwesomeIcon.Ban,
            UserPairForPerms.UserPairEditAccess.AllowNegativeStatusTypesAllowed ? ("Press to Toggle " + PairNickOrAliasOrUID + "'s permission state.") : ("You Can't Change " + PairNickOrAliasOrUID + "'s Permission here."),
            UserPairForPerms.UserPairEditAccess.AllowNegativeStatusTypesAllowed,
            PermissionType.UniquePairPerm, PermissionValueType.YesNo);

        DrawOtherPairSetting("AllowSpecialStatusTypes", "AllowSpecialStatusTypesAllowed",
            UserPairForPerms.UserPairUniquePairPerms.AllowSpecialStatusTypes ? (PairUID + " allows Special Moodles") : (PairUID + " prevents Special Moodles"),
            UserPairForPerms.UserPairUniquePairPerms.AllowSpecialStatusTypes ? FontAwesomeIcon.WandMagicSparkles : FontAwesomeIcon.Ban,
            UserPairForPerms.UserPairEditAccess.AllowSpecialStatusTypesAllowed ? ("Press to Toggle " + PairNickOrAliasOrUID + "'s permission state.") : ("You Can't Change " + PairNickOrAliasOrUID + "'s Permission here."),
            UserPairForPerms.UserPairEditAccess.AllowSpecialStatusTypesAllowed,
            PermissionType.UniquePairPerm, PermissionValueType.YesNo);

        DrawOtherPairSetting("PairCanApplyOwnMoodlesToYou", "PairCanApplyOwnMoodlesToYouAllowed",
            UserPairForPerms.UserPairUniquePairPerms.PairCanApplyOwnMoodlesToYou ? (PairUID + " allows applying your Moodles") : (PairUID + " prevents applying your Moodles"),
            UserPairForPerms.UserPairUniquePairPerms.PairCanApplyOwnMoodlesToYou ? FontAwesomeIcon.PersonArrowUpFromLine : FontAwesomeIcon.Ban,
            UserPairForPerms.UserPairEditAccess.PairCanApplyOwnMoodlesToYouAllowed ? ("Press to Toggle " + PairNickOrAliasOrUID + "'s permission state.") : ("You Can't Change " + PairNickOrAliasOrUID + "'s Permission here."),
            UserPairForPerms.UserPairEditAccess.PairCanApplyOwnMoodlesToYouAllowed,
            PermissionType.UniquePairPerm, PermissionValueType.YesNo);

        DrawOtherPairSetting("PairCanApplyYourMoodlesToYou", "PairCanApplyYourMoodlesToYouAllowed",
            UserPairForPerms.UserPairUniquePairPerms.PairCanApplyYourMoodlesToYou ? (PairUID + " allows applying their Moodles") : (PairUID + " prevents applying their Moodles"),
            UserPairForPerms.UserPairUniquePairPerms.PairCanApplyYourMoodlesToYou ? FontAwesomeIcon.PersonArrowDownToLine : FontAwesomeIcon.Ban,
            UserPairForPerms.UserPairEditAccess.PairCanApplyYourMoodlesToYouAllowed ? ("Press to Toggle " + PairNickOrAliasOrUID + "'s permission state.") : ("You Can't Change " + PairNickOrAliasOrUID + "'s Permission here."),
            UserPairForPerms.UserPairEditAccess.PairCanApplyYourMoodlesToYouAllowed,
            PermissionType.UniquePairPerm, PermissionValueType.YesNo);

        DrawOtherPairSetting("MaxMoodleTime", "MaxMoodleTimeAllowed",
            "Max Moodles Duration",
            FontAwesomeIcon.HourglassHalf,
            $"Max Duration a Moodle can be applied for.",
            UserPairForPerms.UserPairEditAccess.MaxMoodleTimeAllowed,
            PermissionType.UniquePairPerm, PermissionValueType.TimeSpan);

        DrawOtherPairSetting("AllowPermanentMoodles", "AllowPermanentMoodlesAllowed",
            UserPairForPerms.UserPairUniquePairPerms.AllowPermanentMoodles ? (PairUID + " allows Permanent Moodles") : (PairUID + " prevents Permanent Moodles"),
            UserPairForPerms.UserPairUniquePairPerms.AllowPermanentMoodles ? FontAwesomeIcon.Infinity : FontAwesomeIcon.Ban,
            UserPairForPerms.UserPairEditAccess.AllowPermanentMoodlesAllowed ? ("Press to Toggle " + PairNickOrAliasOrUID + "'s permission state.") : ("You Can't Change " + PairNickOrAliasOrUID + "'s Permission here."),
            UserPairForPerms.UserPairEditAccess.AllowPermanentMoodlesAllowed,
            PermissionType.UniquePairPerm, PermissionValueType.YesNo);

        DrawOtherPairSetting("AllowRemovingMoodles", "AllowRemovingMoodlesAllowed",
            UserPairForPerms.UserPairUniquePairPerms.AllowRemovingMoodles ? (PairUID + " allowing Removal of Moodles") : (PairUID + " prevents Removal of Moodles"),
            UserPairForPerms.UserPairUniquePairPerms.AllowRemovingMoodles ? FontAwesomeIcon.Eraser : FontAwesomeIcon.Ban,
            UserPairForPerms.UserPairEditAccess.AllowRemovingMoodlesAllowed ? ("Press to Toggle " + PairNickOrAliasOrUID + "'s permission state.") : ("You Can't Change " + PairNickOrAliasOrUID + "'s Permission here."),
            UserPairForPerms.UserPairEditAccess.AllowRemovingMoodlesAllowed,
            PermissionType.UniquePairPerm, PermissionValueType.YesNo);

        ImGui.Separator();
        /* ----------- TOYBOX PERMISSIONS ----------- */
        ImGui.TextUnformatted("Toybox Permissions");

        DrawOtherPairSetting("CanToggleToyState", "CanToggleToyStateAllowed",
            UserPairForPerms.UserPairUniquePairPerms.CanToggleToyState ? (PairUID + " allows Toy State Changing") : (PairUID + " prevents Toy State Changing"),
            UserPairForPerms.UserPairUniquePairPerms.CanToggleToyState ? FontAwesomeIcon.PowerOff : FontAwesomeIcon.Ban,
            UserPairForPerms.UserPairEditAccess.CanToggleToyStateAllowed ? ("Press to Toggle " + PairNickOrAliasOrUID + "'s permission state.") : ("You Can't Change " + PairNickOrAliasOrUID + "'s Permission here."),
            UserPairForPerms.UserPairEditAccess.CanToggleToyStateAllowed,
            PermissionType.UniquePairPerm, PermissionValueType.YesNo);

        DrawOtherPairSetting("CanUseVibeRemote", "CanUseVibeRemoteAllowed",
            UserPairForPerms.UserPairUniquePairPerms.CanUseVibeRemote ? (PairUID + " allows Vibe Control") : (PairUID + " prevents Vibe Control"),
            UserPairForPerms.UserPairUniquePairPerms.CanUseVibeRemote ? FontAwesomeIcon.Mobile : FontAwesomeIcon.Ban,
            UserPairForPerms.UserPairEditAccess.CanUseVibeRemoteAllowed ? ("Press to Toggle " + PairNickOrAliasOrUID + "'s permission state.") : ("You Can't Change " + PairNickOrAliasOrUID + "'s Permission here."),
            UserPairForPerms.UserPairEditAccess.CanUseVibeRemoteAllowed,
            PermissionType.UniquePairPerm, PermissionValueType.YesNo);

        DrawOtherPairSetting("CanToggleAlarms", "CanToggleAlarmsAllowed",
            UserPairForPerms.UserPairUniquePairPerms.CanToggleAlarms ? (PairUID + " allows Alarm Toggling") : (PairUID + " prevents Alarm Toggling"),
            UserPairForPerms.UserPairUniquePairPerms.CanToggleAlarms ? FontAwesomeIcon.Bell : FontAwesomeIcon.Ban,
            UserPairForPerms.UserPairEditAccess.CanToggleAlarmsAllowed ? ("Press to Toggle " + PairNickOrAliasOrUID + "'s permission state.") : ("You Can't Change " + PairNickOrAliasOrUID + "'s Permission here."),
            UserPairForPerms.UserPairEditAccess.CanToggleAlarmsAllowed,
            PermissionType.UniquePairPerm, PermissionValueType.YesNo);

        DrawOtherPairSetting("CanSendAlarms", "CanSendAlarmsAllowed",
            UserPairForPerms.UserPairUniquePairPerms.CanSendAlarms ? (PairUID + " allows sending Alarms") : (PairUID + " prevents sending Alarms"),
            UserPairForPerms.UserPairUniquePairPerms.CanSendAlarms ? FontAwesomeIcon.FileExport : FontAwesomeIcon.Ban,
            UserPairForPerms.UserPairEditAccess.CanSendAlarmsAllowed ? ("Press to Toggle " + PairNickOrAliasOrUID + "'s permission state.") : ("You Can't Change " + PairNickOrAliasOrUID + "'s Permission here."),
            UserPairForPerms.UserPairEditAccess.CanSendAlarmsAllowed,
            PermissionType.UniquePairPerm, PermissionValueType.YesNo);

        DrawOtherPairSetting("CanExecutePatterns", "CanExecutePatternsAllowed",
            UserPairForPerms.UserPairUniquePairPerms.CanExecutePatterns ? (PairUID + " allows Pattern Execution") : (PairUID + " prevents Pattern Execution"),
            UserPairForPerms.UserPairUniquePairPerms.CanExecutePatterns ? FontAwesomeIcon.LandMineOn : FontAwesomeIcon.Ban,
            UserPairForPerms.UserPairEditAccess.CanExecutePatternsAllowed ? ("Press to Toggle " + PairNickOrAliasOrUID + "'s permission state.") : ("You Can't Change " + PairNickOrAliasOrUID + "'s Permission here."),
            UserPairForPerms.UserPairEditAccess.CanExecutePatternsAllowed,
            PermissionType.UniquePairPerm, PermissionValueType.YesNo);

        DrawOtherPairSetting("CanStopPatterns", "CanStopPatternsAllowed",
            UserPairForPerms.UserPairUniquePairPerms.CanStopPatterns ? (PairUID + " allows stopping Patterns") : (PairUID + " prevents stopping Patterns"),
            UserPairForPerms.UserPairUniquePairPerms.CanStopPatterns ? FontAwesomeIcon.StopCircle : FontAwesomeIcon.Ban,
            UserPairForPerms.UserPairEditAccess.CanStopPatternsAllowed ? ("Press to Toggle " + PairNickOrAliasOrUID + "'s permission state.") : ("You Can't Change " + PairNickOrAliasOrUID + "'s Permission here."),
            UserPairForPerms.UserPairEditAccess.CanStopPatternsAllowed,
            PermissionType.UniquePairPerm, PermissionValueType.YesNo);

        DrawOtherPairSetting("CanToggleTriggers", "CanToggleTriggersAllowed",
            UserPairForPerms.UserPairUniquePairPerms.CanToggleTriggers ? (PairUID + " allows Toggling Triggers") : (PairUID + " prevents Toggling Triggers"),
            UserPairForPerms.UserPairUniquePairPerms.CanToggleTriggers ? FontAwesomeIcon.FileMedicalAlt : FontAwesomeIcon.Ban,
            UserPairForPerms.UserPairEditAccess.CanToggleTriggersAllowed ? ("Press to Toggle " + PairNickOrAliasOrUID + "'s permission state.") : ("You Can't Change " + PairNickOrAliasOrUID + "'s Permission here."),
            UserPairForPerms.UserPairEditAccess.CanToggleTriggersAllowed,
            PermissionType.UniquePairPerm, PermissionValueType.YesNo);
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
                var id = label + "##" + permissionName;
                // draw the iconTextButton and checkbox beside it. Because we are in control, unless in hardcore, this should never be disabled.
                if (_uiShared.IconInputText(id, icon, label, "format 0d0h0m0s...", ref timeSpanString, 32, IconButtonTextWidth*.5f, true, !hasAccess)) { }
                // Set the permission once deactivated. If invalid, set to default.
                if (ImGui.IsItemDeactivatedAfterEdit()
                    && timeSpanString != _uiShared.TimeSpanToString((TimeSpan)permissionSet.GetType().GetProperty(permissionName)?.GetValue(permissionSet)!))
                {
                    // attempt to parse the string back into a valid timespan.
                    if (_uiShared.TryParseTimeSpan(timeSpanString, out TimeSpan result))
                    {
                        ulong ticks = (ulong)result.Ticks;
                        SetOtherPairPermission(permissionType, permissionName, ticks);
                    }
                    else
                    {
                        // find some way to print this to the chat or something.
                        _logger.LogWarning("You tried to set an invalid timespan format. Please use the format 0d0h0m0s");
                        timeSpanString = "0d0h0m0s";
                    }
                }
                UiSharedService.AttachToolTip(tooltip);
                ImGui.SameLine(IconButtonTextWidth + ImGui.GetStyle().ItemSpacing.X);
                // display the respective lock/unlock icon based on the edit access permission.
                _uiShared.BooleanToColoredIcon(hasAccess, false, FontAwesomeIcon.Unlock, FontAwesomeIcon.Lock);
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
                    _logger.LogTrace($"Updated Other pair's global permission: {permissionName} to {newValue}", LoggerType.Permissions);
                    _ = _apiController.UserUpdateOtherGlobalPerm(new UserGlobalPermChangeDto(UserPairForPerms.UserData,
                        new KeyValuePair<string, object>(permissionName, newValue)));
                }
                break;
            case PermissionType.UniquePairPerm:
                {
                    _logger.LogTrace($"Updated other pair's unique pair permission: {permissionName} to {newValue}", LoggerType.Permissions);
                    _ = _apiController.UserUpdateOtherPairPerm(new UserPairPermChangeDto(UserPairForPerms.UserData,
                        new KeyValuePair<string, object>(permissionName, newValue)));
                }
                break;
        }
    }
}
