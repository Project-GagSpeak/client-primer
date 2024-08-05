using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Utility;
using GagSpeak.Services.Mediator;
using GagspeakAPI.Data.Enum;
using GagspeakAPI.Dto.Permissions;
using ImGuiNET;
using System.Security;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TaskbarClock;

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
            UserPairForPerms.UserPairGlobalPerms.LiveChatGarblerActive ? (PairAliasOrUID+"'s Chat Garbler is Active") : (PairAliasOrUID+"'s Chat Garbler is Inactive"),
            UserPairForPerms.UserPairGlobalPerms.LiveChatGarblerActive ? FontAwesomeIcon.MicrophoneSlash : FontAwesomeIcon.Microphone,
            UserPairForPerms.UserPairEditAccess.LiveChatGarblerActiveAllowed ? ("Press to Toggle" + PairNickOrAliasOrUID + "'s permission state. [Global]") : ("You Can't Change " + PairNickOrAliasOrUID + "'s Permission here."),
            UserPairForPerms.UserPairEditAccess.LiveChatGarblerActiveAllowed,
            PermissionType.Global, PermissionValueType.YesNo); // permission type and value type

        DrawOtherPairSetting("LiveChatGarblerLocked", "LiveChatGarblerLockedAllowed",
            UserPairForPerms.UserPairGlobalPerms.LiveChatGarblerLocked ? (PairAliasOrUID + "'s Chat Garbler is Locked") : (PairAliasOrUID + "'s Chat Garbler is Unlocked"),
            UserPairForPerms.UserPairGlobalPerms.LiveChatGarblerLocked ? FontAwesomeIcon.Key : FontAwesomeIcon.UnlockAlt,
            UserPairForPerms.UserPairEditAccess.LiveChatGarblerLockedAllowed ? ("Press to Toggle" + PairNickOrAliasOrUID + "'s permission state. [Global]") : ("You Can't Change " + PairNickOrAliasOrUID + "'s Permission here."),
            UserPairForPerms.UserPairEditAccess.LiveChatGarblerLockedAllowed,
            PermissionType.Global, PermissionValueType.YesNo);

        DrawOtherPairSetting("LockToyboxUI", "LockToyboxUIAllowed",
            UserPairForPerms.UserPairGlobalPerms.LockToyboxUI ? (PairAliasOrUID + "'s Toybox UI is Restricted") : (PairAliasOrUID + "'s Toybox UI is Accessible"),
            UserPairForPerms.UserPairGlobalPerms.LockToyboxUI ? FontAwesomeIcon.Box : FontAwesomeIcon.BoxOpen,
            UserPairForPerms.UserPairEditAccess.LockToyboxUIAllowed ? ("Press to Toggle" + PairNickOrAliasOrUID + "'s permission state. [Global]") : ("You Can't Change " + PairNickOrAliasOrUID + "'s Permission here."),
            UserPairForPerms.UserPairEditAccess.LockToyboxUIAllowed,
            PermissionType.Global, PermissionValueType.YesNo);
        
        ImGui.Separator();
        /* ----------- GAG PERMISSIONS ----------- */
        ImGui.TextUnformatted("Gag Permissions");


        DrawOtherPairSetting("GagFeatures", "GagFeaturesAllowed",
            UserPairForPerms.UserPairUniquePairPerms.GagFeatures ? (PairAliasOrUID + " enabled Gag Interactions") : (PairAliasOrUID + " disabled Gag Interactions"),
            UserPairForPerms.UserPairUniquePairPerms.GagFeatures ? FontAwesomeIcon.CheckCircle : FontAwesomeIcon.Ban,
            UserPairForPerms.UserPairEditAccess.GagFeaturesAllowed ? ("Press to Toggle" + PairNickOrAliasOrUID + "'s permission state.") : ("You Can't Change " + PairNickOrAliasOrUID + "'s Permission here."),
            UserPairForPerms.UserPairEditAccess.GagFeaturesAllowed,
            PermissionType.UniquePairPerm, PermissionValueType.YesNo);

        DrawOtherPairSetting("ItemAutoEquip", "ItemAutoEquipAllowed",
            UserPairForPerms.UserPairGlobalPerms.ItemAutoEquip ? (PairAliasOrUID + " has Gag Glamours Enabled") : (PairAliasOrUID + " has Gag Glamours Disabled"),
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
            UserPairForPerms.UserPairUniquePairPerms.ExtendedLockTimes ? (PairAliasOrUID + " allows Extended Locks") : (PairAliasOrUID + " prevents Extended Locks"),
            UserPairForPerms.UserPairUniquePairPerms.ExtendedLockTimes ? FontAwesomeIcon.Stopwatch : FontAwesomeIcon.Ban,
            UserPairForPerms.UserPairEditAccess.ExtendedLockTimesAllowed ? ("Press to Toggle" + PairNickOrAliasOrUID + "'s permission state.") : ("You Can't Change " + PairNickOrAliasOrUID + "'s Permission here."),
            UserPairForPerms.UserPairEditAccess.ExtendedLockTimesAllowed,
            PermissionType.UniquePairPerm, PermissionValueType.YesNo);

        DrawOtherPairSetting("OwnerLocks", "OwnerLocksAllowed",
            UserPairForPerms.UserPairUniquePairPerms.OwnerLocks ? (PairAliasOrUID + " allows Owner Locks") : (PairAliasOrUID + " prevents Owner Locks"),
            UserPairForPerms.UserPairUniquePairPerms.OwnerLocks ? FontAwesomeIcon.Lock : FontAwesomeIcon.Ban,
            UserPairForPerms.UserPairEditAccess.OwnerLocksAllowed ? ("Press to Toggle" + PairNickOrAliasOrUID + "'s permission state.") : ("You Can't Change " + PairNickOrAliasOrUID + "'s Permission here."),
            UserPairForPerms.UserPairEditAccess.OwnerLocksAllowed,
            PermissionType.UniquePairPerm, PermissionValueType.YesNo);


        ImGui.Separator();
        /* ----------- RESTRAINT SET PERMISSIONS ----------- */
        ImGui.TextUnformatted("Restraint Set Permissions");

        // Rewrite all of the below functions but using the format above
        DrawOtherPairSetting("RestraintSetAutoEquip", "RestraintSetAutoEquipAllowed",
            UserPairForPerms.UserPairGlobalPerms.RestraintSetAutoEquip ? (PairAliasOrUID + " has Restraint Glamours Enabled") : (PairAliasOrUID + " has Restraint Glamours Disabled"),
            UserPairForPerms.UserPairGlobalPerms.RestraintSetAutoEquip ? FontAwesomeIcon.Tshirt : FontAwesomeIcon.Ban,
            UserPairForPerms.UserPairEditAccess.RestraintSetAutoEquipAllowed ? ("Press to Toggle" + PairNickOrAliasOrUID + "'s permission state.") : ("You Can't Change " + PairNickOrAliasOrUID + "'s Permission here."),
            UserPairForPerms.UserPairEditAccess.RestraintSetAutoEquipAllowed,
            PermissionType.Global, PermissionValueType.YesNo);

        DrawOtherPairSetting("ApplyRestraintSets", "ApplyRestraintSetsAllowed",
            UserPairForPerms.UserPairUniquePairPerms.ApplyRestraintSets ? (PairAliasOrUID + " allows Applying Restraints") : (PairAliasOrUID + " prevents Applying Restraints"),
            UserPairForPerms.UserPairUniquePairPerms.ApplyRestraintSets ? FontAwesomeIcon.Female : FontAwesomeIcon.Ban,
            UserPairForPerms.UserPairEditAccess.ApplyRestraintSetsAllowed ? ("Press to Toggle" + PairNickOrAliasOrUID + "'s permission state.") : ("You Can't Change " + PairNickOrAliasOrUID + "'s Permission here."),
            UserPairForPerms.UserPairEditAccess.ApplyRestraintSetsAllowed,
            PermissionType.UniquePairPerm, PermissionValueType.YesNo);

        DrawOtherPairSetting("LockRestraintSets", "LockRestraintSetsAllowed",
            UserPairForPerms.UserPairUniquePairPerms.LockRestraintSets ? (PairAliasOrUID + " allows Locking Restraints") : (PairAliasOrUID + " prevents Locking Restraints"),
            UserPairForPerms.UserPairUniquePairPerms.LockRestraintSets ? FontAwesomeIcon.ShopLock : FontAwesomeIcon.ShopSlash,
            UserPairForPerms.UserPairEditAccess.LockRestraintSetsAllowed ? ("Press to Toggle" + PairNickOrAliasOrUID + "'s permission state.") : ("You Can't Change " + PairNickOrAliasOrUID + "'s Permission here."),
            UserPairForPerms.UserPairEditAccess.LockRestraintSetsAllowed,
            PermissionType.UniquePairPerm, PermissionValueType.YesNo);

        DrawOtherPairSetting("MaxAllowedRestraintTime", "MaxAllowedRestraintTimeAllowed",
            UserPairForPerms.UserPairUniquePairPerms.MaxAllowedRestraintTime == TimeSpan.Zero ? "Set Max Lock Time" : "Change Max Lock Time",
            FontAwesomeIcon.HourglassHalf,
            UserPairForPerms.UserPairEditAccess.MaxAllowedRestraintTimeAllowed ? ("Press to Toggle" + PairNickOrAliasOrUID + "'s permission state.") : ("You Can't Change " + PairNickOrAliasOrUID + "'s Permission here."),
            UserPairForPerms.UserPairEditAccess.MaxAllowedRestraintTimeAllowed,
            PermissionType.UniquePairPerm, PermissionValueType.TimeSpan);

        DrawOtherPairSetting("RemoveRestraintSets", "RemoveRestraintSetsAllowed",
            UserPairForPerms.UserPairUniquePairPerms.RemoveRestraintSets ? (PairAliasOrUID + " allows Removing Restraints") : (PairAliasOrUID + " prevents Removing Restraints"),
            UserPairForPerms.UserPairUniquePairPerms.RemoveRestraintSets ? FontAwesomeIcon.Key : FontAwesomeIcon.Ban,
            UserPairForPerms.UserPairEditAccess.RemoveRestraintSetsAllowed ? ("Press to Toggle" + PairNickOrAliasOrUID + "'s permission state.") : ("You Can't Change " + PairNickOrAliasOrUID + "'s Permission here."),
            UserPairForPerms.UserPairEditAccess.RemoveRestraintSetsAllowed,
            PermissionType.UniquePairPerm, PermissionValueType.YesNo);

        ImGui.Separator();
        /* ----------- PUPPETEER PERMISSIONS ----------- */
        ImGui.TextUnformatted("Puppeteer Permissions");

        DrawOtherPairSetting("AllowSitRequests", "AllowSitRequestsAllowed",
            UserPairForPerms.UserPairUniquePairPerms.AllowSitRequests ? (PairAliasOrUID + " allows Sit Requests") : (PairAliasOrUID + " prevents Sit Requests"),
            UserPairForPerms.UserPairUniquePairPerms.AllowSitRequests ? FontAwesomeIcon.Chair : FontAwesomeIcon.Ban,
            UserPairForPerms.UserPairEditAccess.AllowSitRequestsAllowed ? ("Press to Toggle" + PairNickOrAliasOrUID + "'s permission state.") : ("You Can't Change " + PairNickOrAliasOrUID + "'s Permission here."),
            UserPairForPerms.UserPairEditAccess.AllowSitRequestsAllowed,
            PermissionType.UniquePairPerm, PermissionValueType.YesNo);

        DrawOtherPairSetting("AllowMotionRequests", "AllowMotionRequestsAllowed",
            UserPairForPerms.UserPairUniquePairPerms.AllowMotionRequests ? (PairAliasOrUID + " allows Motion Requests") : (PairAliasOrUID + " prevents Motion Requests"),
            UserPairForPerms.UserPairUniquePairPerms.AllowMotionRequests ? FontAwesomeIcon.Walking : FontAwesomeIcon.Ban,
            UserPairForPerms.UserPairEditAccess.AllowMotionRequestsAllowed ? ("Press to Toggle" + PairNickOrAliasOrUID + "'s permission state.") : ("You Can't Change " + PairNickOrAliasOrUID + "'s Permission here."),
            UserPairForPerms.UserPairEditAccess.AllowMotionRequestsAllowed,
            PermissionType.UniquePairPerm, PermissionValueType.YesNo);

        DrawOtherPairSetting("AllowAllRequests", "AllowAllRequestsAllowed",
            UserPairForPerms.UserPairUniquePairPerms.AllowAllRequests ? (PairAliasOrUID + " allows All Requests") : (PairAliasOrUID + " prevents All Requests"),
            UserPairForPerms.UserPairUniquePairPerms.AllowAllRequests ? FontAwesomeIcon.Unlock : FontAwesomeIcon.Lock,
            UserPairForPerms.UserPairEditAccess.AllowAllRequestsAllowed ? ("Press to Toggle" + PairNickOrAliasOrUID + "'s permission state.") : ("You Can't Change " + PairNickOrAliasOrUID + "'s Permission here."),
            UserPairForPerms.UserPairEditAccess.AllowAllRequestsAllowed,
            PermissionType.UniquePairPerm, PermissionValueType.YesNo);

        ImGui.Separator();
        /* ----------- MOODLES PERMISSIONS ----------- */
        ImGui.TextUnformatted("Moodles Permissions");

        DrawOtherPairSetting("AllowPositiveStatusTypes", "AllowPositiveStatusTypesAllowed",
            UserPairForPerms.UserPairUniquePairPerms.AllowPositiveStatusTypes ? (PairAliasOrUID + " allows Positive Moodles") : (PairAliasOrUID + " prevents Positive Moodles"),
            UserPairForPerms.UserPairUniquePairPerms.AllowPositiveStatusTypes ? FontAwesomeIcon.SmileBeam : FontAwesomeIcon.Ban,
            UserPairForPerms.UserPairEditAccess.AllowPositiveStatusTypesAllowed ? ("Press to Toggle " + PairNickOrAliasOrUID + "'s permission state.") : ("You Can't Change " + PairNickOrAliasOrUID + "'s Permission here."),
            UserPairForPerms.UserPairEditAccess.AllowPositiveStatusTypesAllowed,
            PermissionType.UniquePairPerm, PermissionValueType.YesNo);

        DrawOtherPairSetting("AllowNegativeStatusTypes", "AllowNegativeStatusTypesAllowed",
            UserPairForPerms.UserPairUniquePairPerms.AllowNegativeStatusTypes ? (PairAliasOrUID + " allows Negative Moodles") : (PairAliasOrUID + " prevents Negative Moodles"),
            UserPairForPerms.UserPairUniquePairPerms.AllowNegativeStatusTypes ? FontAwesomeIcon.FrownOpen : FontAwesomeIcon.Ban,
            UserPairForPerms.UserPairEditAccess.AllowNegativeStatusTypesAllowed ? ("Press to Toggle " + PairNickOrAliasOrUID + "'s permission state.") : ("You Can't Change " + PairNickOrAliasOrUID + "'s Permission here."),
            UserPairForPerms.UserPairEditAccess.AllowNegativeStatusTypesAllowed,
            PermissionType.UniquePairPerm, PermissionValueType.YesNo);

        DrawOtherPairSetting("AllowSpecialStatusTypes", "AllowSpecialStatusTypesAllowed",
            UserPairForPerms.UserPairUniquePairPerms.AllowSpecialStatusTypes ? (PairAliasOrUID + " allows Special Moodles") : (PairAliasOrUID + " prevents Special Moodles"),
            UserPairForPerms.UserPairUniquePairPerms.AllowSpecialStatusTypes ? FontAwesomeIcon.WandMagicSparkles : FontAwesomeIcon.Ban,
            UserPairForPerms.UserPairEditAccess.AllowSpecialStatusTypesAllowed ? ("Press to Toggle " + PairNickOrAliasOrUID + "'s permission state.") : ("You Can't Change " + PairNickOrAliasOrUID + "'s Permission here."),
            UserPairForPerms.UserPairEditAccess.AllowSpecialStatusTypesAllowed,
            PermissionType.UniquePairPerm, PermissionValueType.YesNo);

        DrawOtherPairSetting("PairCanApplyOwnMoodlesToYou", "PairCanApplyOwnMoodlesToYouAllowed",
            UserPairForPerms.UserPairUniquePairPerms.PairCanApplyOwnMoodlesToYou ? (PairAliasOrUID + " allows applying your Moodles") : (PairAliasOrUID + " prevents applying your Moodles"),
            UserPairForPerms.UserPairUniquePairPerms.PairCanApplyOwnMoodlesToYou ? FontAwesomeIcon.PersonArrowUpFromLine : FontAwesomeIcon.Ban,
            UserPairForPerms.UserPairEditAccess.PairCanApplyOwnMoodlesToYouAllowed ? ("Press to Toggle " + PairNickOrAliasOrUID + "'s permission state.") : ("You Can't Change " + PairNickOrAliasOrUID + "'s Permission here."),
            UserPairForPerms.UserPairEditAccess.PairCanApplyOwnMoodlesToYouAllowed,
            PermissionType.UniquePairPerm, PermissionValueType.YesNo);

        DrawOtherPairSetting("PairCanApplyYourMoodlesToYou", "PairCanApplyYourMoodlesToYouAllowed",
            UserPairForPerms.UserPairUniquePairPerms.PairCanApplyYourMoodlesToYou ? (PairAliasOrUID + " allows applying their Moodles") : (PairAliasOrUID + " prevents applying their Moodles"),
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
            UserPairForPerms.UserPairUniquePairPerms.AllowPermanentMoodles ? (PairAliasOrUID + " allows Permanent Moodles") : (PairAliasOrUID + " prevents Permanent Moodles"),
            UserPairForPerms.UserPairUniquePairPerms.AllowPermanentMoodles ? FontAwesomeIcon.Infinity : FontAwesomeIcon.Ban,
            UserPairForPerms.UserPairEditAccess.AllowPermanentMoodlesAllowed ? ("Press to Toggle " + PairNickOrAliasOrUID + "'s permission state.") : ("You Can't Change " + PairNickOrAliasOrUID + "'s Permission here."),
            UserPairForPerms.UserPairEditAccess.AllowPermanentMoodlesAllowed,
            PermissionType.UniquePairPerm, PermissionValueType.YesNo);

        DrawOtherPairSetting("AllowRemovingMoodles", "AllowRemovingMoodlesAllowed",
            UserPairForPerms.UserPairUniquePairPerms.AllowRemovingMoodles ? (PairAliasOrUID + " allowing Removal of Moodles") : (PairAliasOrUID + " prevents Removal of Moodles"),
            UserPairForPerms.UserPairUniquePairPerms.AllowRemovingMoodles ? FontAwesomeIcon.Eraser : FontAwesomeIcon.Ban,
            UserPairForPerms.UserPairEditAccess.AllowRemovingMoodlesAllowed ? ("Press to Toggle " + PairNickOrAliasOrUID + "'s permission state.") : ("You Can't Change " + PairNickOrAliasOrUID + "'s Permission here."),
            UserPairForPerms.UserPairEditAccess.AllowRemovingMoodlesAllowed,
            PermissionType.UniquePairPerm, PermissionValueType.YesNo);

        ImGui.Separator();
        /* ----------- TOYBOX PERMISSIONS ----------- */
        ImGui.TextUnformatted("Toybox Permissions");

        DrawOtherPairSetting("ChangeToyState", "ChangeToyStateAllowed",
            UserPairForPerms.UserPairUniquePairPerms.ChangeToyState ? (PairAliasOrUID + " allows Toy State Changing") : (PairAliasOrUID + " prevents Toy State Changing"),
            UserPairForPerms.UserPairUniquePairPerms.ChangeToyState ? FontAwesomeIcon.PowerOff : FontAwesomeIcon.Ban,
            UserPairForPerms.UserPairEditAccess.ChangeToyStateAllowed ? ("Press to Toggle " + PairNickOrAliasOrUID + "'s permission state.") : ("You Can't Change " + PairNickOrAliasOrUID + "'s Permission here."),
            UserPairForPerms.UserPairEditAccess.ChangeToyStateAllowed,
            PermissionType.UniquePairPerm, PermissionValueType.YesNo);

        DrawOtherPairSetting("VibratorAlarms", "VibratorAlarmsAllowed",
            UserPairForPerms.UserPairUniquePairPerms.VibratorAlarms ? (PairAliasOrUID + " allows Alarms Viewing") : (PairAliasOrUID + " prevents Alarm Viewing"),
            UserPairForPerms.UserPairUniquePairPerms.VibratorAlarms ? FontAwesomeIcon.Clock : FontAwesomeIcon.Ban,
            UserPairForPerms.UserPairEditAccess.VibratorAlarmsAllowed ? ("Press to Toggle " + PairNickOrAliasOrUID + "'s permission state.") : ("You Can't Change " + PairNickOrAliasOrUID + "'s Permission here."),
            UserPairForPerms.UserPairEditAccess.VibratorAlarmsAllowed,
            PermissionType.UniquePairPerm, PermissionValueType.YesNo);

        DrawOtherPairSetting("VibratorAlarmsToggle", "VibratorAlarmsToggleAllowed",
            UserPairForPerms.UserPairUniquePairPerms.VibratorAlarmsToggle ? (PairAliasOrUID + " allows Alarm Toggling") : (PairAliasOrUID + " prevents Alarm Toggling"),
            UserPairForPerms.UserPairUniquePairPerms.VibratorAlarmsToggle ? FontAwesomeIcon.Bell : FontAwesomeIcon.BellSlash,
            UserPairForPerms.UserPairEditAccess.VibratorAlarmsToggleAllowed ? ("Press to Toggle " + PairNickOrAliasOrUID + "'s permission state.") : ("You Can't Change " + PairNickOrAliasOrUID + "'s Permission here."),
            UserPairForPerms.UserPairEditAccess.VibratorAlarmsToggleAllowed,
            PermissionType.UniquePairPerm, PermissionValueType.YesNo);

        DrawOtherPairSetting("CanExecutePatterns", "CanExecutePatternsAllowed",
            UserPairForPerms.UserPairUniquePairPerms.CanExecutePatterns ? (PairAliasOrUID + " allows Pattern Execution") : (PairAliasOrUID + " prevents Pattern Execution"),
            UserPairForPerms.UserPairUniquePairPerms.CanExecutePatterns ? FontAwesomeIcon.LandMineOn : FontAwesomeIcon.Ban,
            UserPairForPerms.UserPairEditAccess.CanExecutePatternsAllowed ? ("Press to Toggle " + PairNickOrAliasOrUID + "'s permission state.") : ("You Can't Change " + PairNickOrAliasOrUID + "'s Permission here."),
            UserPairForPerms.UserPairEditAccess.CanExecutePatternsAllowed,
            PermissionType.UniquePairPerm, PermissionValueType.YesNo);

        DrawOtherPairSetting("CanExecuteTriggers", "CanExecuteTriggersAllowed",
            UserPairForPerms.UserPairUniquePairPerms.CanExecuteTriggers ? (PairAliasOrUID + " allows Toggling Triggers") : (PairAliasOrUID + " prevents Toggling Triggers"),
            UserPairForPerms.UserPairUniquePairPerms.CanExecuteTriggers ? FontAwesomeIcon.FileMedicalAlt : FontAwesomeIcon.FileExcel,
            UserPairForPerms.UserPairEditAccess.CanExecuteTriggersAllowed ? ("Press to Toggle " + PairNickOrAliasOrUID + "'s permission state.") : ("You Can't Change " + PairNickOrAliasOrUID + "'s Permission here."),
            UserPairForPerms.UserPairEditAccess.CanExecuteTriggersAllowed,
            PermissionType.UniquePairPerm, PermissionValueType.YesNo);

        DrawOtherPairSetting("CanSendTriggers", "CanSendTriggersAllowed",
            UserPairForPerms.UserPairUniquePairPerms.CanSendTriggers ? (PairAliasOrUID + " allows sending Triggers") : (PairAliasOrUID + " prevents sending Triggers"),
            UserPairForPerms.UserPairUniquePairPerms.CanSendTriggers ? FontAwesomeIcon.FileExport : FontAwesomeIcon.FileExcel,
            UserPairForPerms.UserPairEditAccess.CanSendTriggersAllowed ? ("Press to Toggle " + PairNickOrAliasOrUID + "'s permission state.") : ("You Can't Change " + PairNickOrAliasOrUID + "'s Permission here."),
            UserPairForPerms.UserPairEditAccess.CanSendTriggersAllowed,
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
