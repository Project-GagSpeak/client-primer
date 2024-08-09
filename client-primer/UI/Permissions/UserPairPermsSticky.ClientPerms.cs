using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Utility;
using GagspeakAPI.Dto.Permissions;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets2;
using System.Security;

namespace GagSpeak.UI.Permissions;

/// <summary>
/// CLIENT PERMS PARTIAL CLASS
/// </summary>
public partial class UserPairPermsSticky
{
    // This is where we both view our current settings for the pair,
    // and the levels of access we have granted them control over.
    //
    // For each row, to the left will be an icon. Displaying the status relative to the state.
    //
    // beside it will be the current UserPairForPerms.UserPair.OwnPairPerms we have set for them.
    // 
    // to the far right will be a interactable checkbox, this will display if we allow
    // this pair to have control over this option or not.
    public void DrawClientPermsForPair()
    {
        /* ----------- GLOBAL SETTINGS ----------- */
        ImGui.TextUnformatted("Global Settings");

        DrawOwnSetting("LiveChatGarblerActive", "LiveChatGarblerActiveAllowed", // permission name and permission access name
            _playerManager.GlobalPerms.LiveChatGarblerActive ? "Live Chat Garbler Active" : "Live Chat Garbler Inactive", // label
            _playerManager.GlobalPerms.LiveChatGarblerActive ? FontAwesomeIcon.MicrophoneSlash : FontAwesomeIcon.Microphone, // icon
            _playerManager.GlobalPerms.LiveChatGarblerActive ? "Disable the Live Chat Garbler. [Global]" : "Enable the Live Chat Garbler. [Global]", // tooltip
            UserPairForPerms.UserPairOwnUniquePairPerms.InHardcore || _playerManager.GlobalPerms.LiveChatGarblerLocked, // Disable condition
            PermissionType.Global, PermissionValueType.YesNo); // permission type and value type

        DrawOwnSetting("LiveChatGarblerLocked", "LiveChatGarblerLockedAllowed",
            _playerManager.GlobalPerms.LiveChatGarblerLocked ? "Chat Garbler Locked" : "Chat Garbler Unlocked",
            _playerManager.GlobalPerms.LiveChatGarblerLocked ? FontAwesomeIcon.Key : FontAwesomeIcon.UnlockAlt,
            _playerManager.GlobalPerms.LiveChatGarblerLocked ? "Chat Garbler has been locked! [Global]" : "Chat Garbler is not currently locked. [Global]",
            true,
            PermissionType.Global, PermissionValueType.YesNo);

        DrawOwnSetting("LockToyboxUI", "LockToyboxUIAllowed",
            _playerManager.GlobalPerms.LockToyboxUI ? "Toybox Interactions Locked" : "Toybox Interactions Available",
            _playerManager.GlobalPerms.LockToyboxUI ? FontAwesomeIcon.Box : FontAwesomeIcon.BoxOpen,
            _playerManager.GlobalPerms.LockToyboxUI ? "Remove active Toybox feature restrictions [Global]" : "Restrict your toybox features. [Global]",
            UserPairForPerms.UserPairOwnUniquePairPerms.InHardcore,
            PermissionType.Global, PermissionValueType.YesNo);

        ImGui.Separator();
        /* ----------- GAG PERMISSIONS ----------- */
        ImGui.TextUnformatted("Gag Permissions");

        DrawOwnSetting("GagFeatures", "GagFeaturesAllowed",
            UserPairForPerms.UserPairOwnUniquePairPerms.GagFeatures ? "Allowing Gag Interactions" : "Preventing Gag Interactions",
            UserPairForPerms.UserPairOwnUniquePairPerms.GagFeatures ? FontAwesomeIcon.CheckCircle : FontAwesomeIcon.Ban,
            UserPairForPerms.UserPairOwnUniquePairPerms.GagFeatures ? 
                $"Prevent {PairNickOrAliasOrUID} from Applying, Locking, and Removing Gags" : $"Allow {PairNickOrAliasOrUID} to Apply, Lock, and Remove Gags.",
            UserPairForPerms.UserPairOwnUniquePairPerms.InHardcore,
            PermissionType.UniquePairPerm, PermissionValueType.YesNo);

        DrawOwnSetting("ItemAutoEquip", "ItemAutoEquipAllowed",
            _playerManager.GlobalPerms.ItemAutoEquip ? "Auto-Equip Gag Glamour's" : "No Gag Glamour Auto-Equip",
            _playerManager.GlobalPerms.ItemAutoEquip ? FontAwesomeIcon.Surprise : FontAwesomeIcon.Ban,
            _playerManager.GlobalPerms.ItemAutoEquip ? "Disable Auto-Equip for Gag Glamour's. [Global]" : "Enable Auto-Equip for Gag Glamour's. [Global]",
            UserPairForPerms.UserPairOwnUniquePairPerms.InHardcore,
            PermissionType.Global, PermissionValueType.YesNo);

        DrawOwnSetting("MaxLockTime", "MaxLockTimeAllowed",
            "Max Time",
            FontAwesomeIcon.HourglassHalf,
            $"Max time {PairNickOrAliasOrUID} can lock your gags for.",
            true,
            PermissionType.UniquePairPerm, PermissionValueType.TimeSpan);

        DrawOwnSetting("ExtendedLockTimes", "ExtendedLockTimesAllowed",
            UserPairForPerms.UserPairOwnUniquePairPerms.ExtendedLockTimes ? "Allowing Extended Lock Times" : "Preventing Extended Lock Times",
            UserPairForPerms.UserPairOwnUniquePairPerms.ExtendedLockTimes ? FontAwesomeIcon.Stopwatch : FontAwesomeIcon.Ban,
            UserPairForPerms.UserPairOwnUniquePairPerms.ExtendedLockTimes ? 
                $"Prevent {PairNickOrAliasOrUID} from setting locks longer than 1 hour." : $"Allow {PairNickOrAliasOrUID} to set locks longer than 1 hour.",
            UserPairForPerms.UserPairOwnUniquePairPerms.InHardcore,
            PermissionType.UniquePairPerm, PermissionValueType.YesNo);

        DrawOwnSetting("OwnerLocks", "OwnerLocksAllowed",
            UserPairForPerms.UserPairOwnUniquePairPerms.OwnerLocks ? "Allowing Owner Padlocks" : "Preventing Owner Padlocks",
            UserPairForPerms.UserPairOwnUniquePairPerms.OwnerLocks ? FontAwesomeIcon.CheckCircle : FontAwesomeIcon.Ban,
            UserPairForPerms.UserPairOwnUniquePairPerms.OwnerLocks ? $"Prevent {PairNickOrAliasOrUID} from using Owner Padlocks." : $"Allow {PairNickOrAliasOrUID} to use Owner Padlocks.",
            UserPairForPerms.UserPairOwnUniquePairPerms.InHardcore,
            PermissionType.UniquePairPerm, PermissionValueType.YesNo);

        ImGui.Separator();
        /* ----------- RESTRAINT SET PERMISSIONS ----------- */
        ImGui.TextUnformatted("Restraint Set Permissions");

        DrawOwnSetting("RestraintSetAutoEquip", "RestraintSetAutoEquipAllowed",
            _playerManager.GlobalPerms.RestraintSetAutoEquip ? "Restraint Set Glamour's active" : "Restraint Set Glamour's inactive",
            _playerManager.GlobalPerms.RestraintSetAutoEquip ? FontAwesomeIcon.ShopLock : FontAwesomeIcon.ShopSlash,
            _playerManager.GlobalPerms.RestraintSetAutoEquip ? "Disable Restraint Set Glamour's. [Global]" : "Enable Restraint Set Glamour's. [Global]",
            UserPairForPerms.UserPairOwnUniquePairPerms.InHardcore,
            PermissionType.Global, PermissionValueType.YesNo);

        DrawOwnSetting("ApplyRestraintSets", "ApplyRestraintSetsAllowed",
            UserPairForPerms.UserPairOwnUniquePairPerms.ApplyRestraintSets ? "Apply Restraint Sets Allowed" : "Preventing Restraint Set Application",
            UserPairForPerms.UserPairOwnUniquePairPerms.ApplyRestraintSets ? FontAwesomeIcon.Tshirt : FontAwesomeIcon.Ban,
            UserPairForPerms.UserPairOwnUniquePairPerms.ApplyRestraintSets ? $"Prevent {PairNickOrAliasOrUID} from applying restraint sets." : $"Allow {PairNickOrAliasOrUID} to apply your restraint sets.",
            UserPairForPerms.UserPairOwnUniquePairPerms.InHardcore,
            PermissionType.UniquePairPerm, PermissionValueType.YesNo);

        DrawOwnSetting("LockRestraintSets", "LockRestraintSetsAllowed",
            UserPairForPerms.UserPairOwnUniquePairPerms.LockRestraintSets ? "Allowing Restraint Set Locking" : "Preventing Restraint Set Locking",
            UserPairForPerms.UserPairOwnUniquePairPerms.LockRestraintSets ? FontAwesomeIcon.Lock : FontAwesomeIcon.Ban,
            UserPairForPerms.UserPairOwnUniquePairPerms.LockRestraintSets ? $"Prevent {PairNickOrAliasOrUID} from locking your restraint sets." : $"Allow {PairNickOrAliasOrUID} to lock your restraint sets.",
            UserPairForPerms.UserPairOwnUniquePairPerms.InHardcore,
            PermissionType.UniquePairPerm, PermissionValueType.YesNo);

        DrawOwnSetting("MaxAllowedRestraintTime", "MaxAllowedRestraintTimeAllowed",
            "Max Time",
            FontAwesomeIcon.HourglassHalf,
            $"Max time {PairNickOrAliasOrUID} can lock your restraint sets for.",
            true,
            PermissionType.UniquePairPerm, PermissionValueType.TimeSpan);

        DrawOwnSetting("UnlockRestraintSets", "UnlockRestraintSetsAllowed",
            UserPairForPerms.UserPairOwnUniquePairPerms.UnlockRestraintSets ? "Allowing Restraint Set Unlocking" : "Preventing Restraint Set Unlocking",
            UserPairForPerms.UserPairOwnUniquePairPerms.UnlockRestraintSets ? FontAwesomeIcon.LockOpen : FontAwesomeIcon.Ban,
            UserPairForPerms.UserPairOwnUniquePairPerms.UnlockRestraintSets ? $"Prevent {PairNickOrAliasOrUID} from unlocking your restraint sets." : $"Allow {PairNickOrAliasOrUID} to unlock your restraint sets.",
            UserPairForPerms.UserPairOwnUniquePairPerms.InHardcore,
            PermissionType.UniquePairPerm, PermissionValueType.YesNo);

        DrawOwnSetting("RemoveRestraintSets", "RemoveRestraintSetsAllowed",
            UserPairForPerms.UserPairOwnUniquePairPerms.RemoveRestraintSets ? "Allowing Restraint Set Removal" : "Preventing Restraint Set Removal",
            UserPairForPerms.UserPairOwnUniquePairPerms.RemoveRestraintSets ? FontAwesomeIcon.Female : FontAwesomeIcon.Ban,
            UserPairForPerms.UserPairOwnUniquePairPerms.RemoveRestraintSets ? $"Prevent {PairNickOrAliasOrUID} from removing your restraint sets." : $"Allow {PairNickOrAliasOrUID} to remove your restraint sets.",
            UserPairForPerms.UserPairOwnUniquePairPerms.InHardcore,
            PermissionType.UniquePairPerm, PermissionValueType.YesNo);

        ImGui.Separator();

        /* ----------- PUPPETEER PERMISSIONS ----------- */
        ImGui.TextUnformatted("Puppeteer Permissions");

        DrawOwnSetting("AllowSitRequests", "AllowSitRequestsAllowed",
            UserPairForPerms.UserPairOwnUniquePairPerms.AllowSitRequests? "Allowing Sit Requests" : "Preventing Sit Requests",
            UserPairForPerms.UserPairOwnUniquePairPerms.AllowSitRequests ? FontAwesomeIcon.Chair : FontAwesomeIcon.Ban,
            UserPairForPerms.UserPairOwnUniquePairPerms.AllowSitRequests ? $"Prevent {PairNickOrAliasOrUID} from forcing "+
                "you to /sit (different from hardcore)" : $"Let {PairNickOrAliasOrUID} forcing you to /sit or /groundsit.",
            UserPairForPerms.UserPairOwnUniquePairPerms.InHardcore,
            PermissionType.UniquePairPerm, PermissionValueType.YesNo);

        DrawOwnSetting("AllowMotionRequests", "AllowMotionRequestsAllowed",
            UserPairForPerms.UserPairOwnUniquePairPerms.AllowMotionRequests ? "Allowing Motion Requests" : "Preventing Motion Requests",
            UserPairForPerms.UserPairOwnUniquePairPerms.AllowMotionRequests ? FontAwesomeIcon.Walking : FontAwesomeIcon.Ban,
            UserPairForPerms.UserPairOwnUniquePairPerms.AllowMotionRequests ? $"Prevent {PairNickOrAliasOrUID} from forcing you to do expressions "+
                "and emotes." : $"Let {PairNickOrAliasOrUID} force you to do expressions and emotes.",
            UserPairForPerms.UserPairOwnUniquePairPerms.InHardcore,
            PermissionType.UniquePairPerm, PermissionValueType.YesNo);

        DrawOwnSetting("AllowAllRequests", "AllowAllRequestsAllowed",
            UserPairForPerms.UserPairOwnUniquePairPerms.AllowAllRequests ? "Allowing All Requests" : "Preventing All Requests",
            UserPairForPerms.UserPairOwnUniquePairPerms.AllowAllRequests ? FontAwesomeIcon.Lock : FontAwesomeIcon.Unlock,
            UserPairForPerms.UserPairOwnUniquePairPerms.AllowAllRequests ? $"Prevent {PairNickOrAliasOrUID} from forcing you to do anything." : $"Let {PairNickOrAliasOrUID} force you to do anything.",
            UserPairForPerms.UserPairOwnUniquePairPerms.InHardcore,
            PermissionType.UniquePairPerm, PermissionValueType.YesNo);

        ImGui.Separator();
        /* ----------- MOODLES PERMISSIONS ----------- */
        ImGui.TextUnformatted("Moodles Permissions");

         DrawOwnSetting("AllowPositiveStatusTypes", "AllowPositiveStatusTypesAllowed",
            UserPairForPerms.UserPairOwnUniquePairPerms.AllowPositiveStatusTypes ? "Allow Applying Positive Moodles" : "Positive Moodles Disallowed",
            UserPairForPerms.UserPairOwnUniquePairPerms.AllowPositiveStatusTypes ? FontAwesomeIcon.SmileBeam : FontAwesomeIcon.Ban,
            UserPairForPerms.UserPairOwnUniquePairPerms.AllowPositiveStatusTypes ? $"Prevent {PairNickOrAliasOrUID} from applying moodles with a positive "+
                "status." : $"Allow {PairNickOrAliasOrUID} to apply moodles with a positive status.",
            UserPairForPerms.UserPairOwnUniquePairPerms.InHardcore,
            PermissionType.UniquePairPerm, PermissionValueType.YesNo);

        DrawOwnSetting("AllowNegativeStatusTypes", "AllowNegativeStatusTypesAllowed",
            UserPairForPerms.UserPairOwnUniquePairPerms.AllowNegativeStatusTypes ? "Allow Applying Negative Moodles" : "Negative Moodles Disallowed",
            UserPairForPerms.UserPairOwnUniquePairPerms.AllowNegativeStatusTypes ? FontAwesomeIcon.FrownOpen : FontAwesomeIcon.Ban,
            UserPairForPerms.UserPairOwnUniquePairPerms.AllowNegativeStatusTypes ? $"Prevent {PairNickOrAliasOrUID} from applying moodles with a negative "+
                "status." : $"Allow {PairNickOrAliasOrUID} to apply moodles with a negative status.",
            UserPairForPerms.UserPairOwnUniquePairPerms.InHardcore,
            PermissionType.UniquePairPerm, PermissionValueType.YesNo);

        DrawOwnSetting("AllowSpecialStatusTypes", "AllowSpecialStatusTypesAllowed",
            UserPairForPerms.UserPairOwnUniquePairPerms.AllowSpecialStatusTypes ? "Allow Applying Special Moodles" : "Special Moodles Disallowed",
            UserPairForPerms.UserPairOwnUniquePairPerms.AllowSpecialStatusTypes ? FontAwesomeIcon.WandMagicSparkles : FontAwesomeIcon.Ban,
            UserPairForPerms.UserPairOwnUniquePairPerms.AllowSpecialStatusTypes ? $"Prevent {PairNickOrAliasOrUID} from applying moodles with a special "+
                "status." : $"Allow {PairNickOrAliasOrUID} to apply moodles with a special status.",
            UserPairForPerms.UserPairOwnUniquePairPerms.InHardcore,
            PermissionType.UniquePairPerm, PermissionValueType.YesNo);

        DrawOwnSetting("PairCanApplyOwnMoodlesToYou", "PairCanApplyOwnMoodlesToYouAllowed",
            UserPairForPerms.UserPairOwnUniquePairPerms.PairCanApplyOwnMoodlesToYou ? $"Allow Application of Pair's Moodles." : $"Prevent Application of Pair's Moodles.",
            UserPairForPerms.UserPairOwnUniquePairPerms.PairCanApplyOwnMoodlesToYou ? FontAwesomeIcon.PersonArrowUpFromLine : FontAwesomeIcon.Ban,
            UserPairForPerms.UserPairOwnUniquePairPerms.PairCanApplyOwnMoodlesToYou ? $"Allow {PairNickOrAliasOrUID} to apply their own Moodles onto " +
                "you." : $"Prevent {PairNickOrAliasOrUID} from applying their own Moodles onto you.",
            UserPairForPerms.UserPairOwnUniquePairPerms.InHardcore,
            PermissionType.UniquePairPerm, PermissionValueType.YesNo);

        DrawOwnSetting("PairCanApplyYourMoodlesToYou", "PairCanApplyYourMoodlesToYouAllowed",
            UserPairForPerms.UserPairOwnUniquePairPerms.PairCanApplyYourMoodlesToYou ? $"Allow Application of Your Moodles." : $"Prevent Application of Your Moodles.",
            UserPairForPerms.UserPairOwnUniquePairPerms.PairCanApplyYourMoodlesToYou ? FontAwesomeIcon.PersonArrowDownToLine : FontAwesomeIcon.Ban,
            UserPairForPerms.UserPairOwnUniquePairPerms.PairCanApplyYourMoodlesToYou ? $"Allow {PairNickOrAliasOrUID} to apply your Moodles onto " +
                "you." : $"Prevent {PairNickOrAliasOrUID} from applying your Moodles onto you.",
            UserPairForPerms.UserPairOwnUniquePairPerms.InHardcore,
            PermissionType.UniquePairPerm, PermissionValueType.YesNo);

        DrawOwnSetting("MaxMoodleTime", "MaxMoodleTimeAllowed",
            "Max Duration",
            FontAwesomeIcon.HourglassHalf,
            $"Max time {PairNickOrAliasOrUID} can apply moodles to you for.",
            true,
            PermissionType.UniquePairPerm, PermissionValueType.TimeSpan);

        DrawOwnSetting("AllowPermanentMoodles", "AllowPermanentMoodlesAllowed",
            UserPairForPerms.UserPairOwnUniquePairPerms.AllowPermanentMoodles ? "Allow Permanent Moodles" : "Prevent Permanent Moodles",
            UserPairForPerms.UserPairOwnUniquePairPerms.AllowPermanentMoodles ? FontAwesomeIcon.Infinity : FontAwesomeIcon.Ban,
            UserPairForPerms.UserPairOwnUniquePairPerms.AllowPermanentMoodles ? $"Prevent {PairNickOrAliasOrUID} from applying permanent moodles to you." : $"Allow {PairNickOrAliasOrUID} to apply permanent moodles to you.",
            UserPairForPerms.UserPairOwnUniquePairPerms.InHardcore,
            PermissionType.UniquePairPerm, PermissionValueType.YesNo);

        DrawOwnSetting("AllowRemovingMoodles", "AllowRemovingMoodlesAllowed",
            UserPairForPerms.UserPairOwnUniquePairPerms.AllowRemovingMoodles ? "Allow Removing Moodles" : "Prevent Removing Moodles",
            UserPairForPerms.UserPairOwnUniquePairPerms.AllowRemovingMoodles ? FontAwesomeIcon.Eraser : FontAwesomeIcon.Ban,
            UserPairForPerms.UserPairOwnUniquePairPerms.AllowRemovingMoodles ? $"Prevent {PairNickOrAliasOrUID} from removing your moodles." : $"Allow {PairNickOrAliasOrUID} to remove your moodles.",
            UserPairForPerms.UserPairOwnUniquePairPerms.InHardcore,
            PermissionType.UniquePairPerm, PermissionValueType.YesNo);

        ImGui.Separator();
        /* ----------- TOYBOX PERMISSIONS ----------- */
        ImGui.TextUnformatted("Toybox Permissions");

        DrawOwnSetting("ChangeToyState", "ChangeToyStateAllowed",
            UserPairForPerms.UserPairOwnUniquePairPerms.ChangeToyState ? "Allow Toggling Vibes" : "Preventing Toggling Vibes",
            UserPairForPerms.UserPairOwnUniquePairPerms.ChangeToyState ? FontAwesomeIcon.PowerOff : FontAwesomeIcon.Ban,
            UserPairForPerms.UserPairOwnUniquePairPerms.ChangeToyState ? $"Prevent {PairNickOrAliasOrUID} from toggling your toys." : $"Allow {PairNickOrAliasOrUID} to toggle your toys.",
            UserPairForPerms.UserPairOwnUniquePairPerms.InHardcore,
            PermissionType.UniquePairPerm, PermissionValueType.YesNo);

        // this kinda conflicts with the whole visible users permission but maybe rework later idk.
        DrawOwnSetting("VibratorAlarms", "VibratorAlarmsAllowed",
            UserPairForPerms.UserPairOwnUniquePairPerms.VibratorAlarms ? "Allow Viewing Alarms" : "Prevent Viewing Alarms ",
            UserPairForPerms.UserPairOwnUniquePairPerms.VibratorAlarms ? FontAwesomeIcon.Eye : FontAwesomeIcon.EyeSlash,
            UserPairForPerms.UserPairOwnUniquePairPerms.VibratorAlarms ? $"Prevent {PairNickOrAliasOrUID} from viewing your alarms." : $"Allow {PairNickOrAliasOrUID} to view your alarms.",
            UserPairForPerms.UserPairOwnUniquePairPerms.InHardcore,
            PermissionType.UniquePairPerm, PermissionValueType.YesNo);

        DrawOwnSetting("VibratorAlarmsToggle", "VibratorAlarmsToggleAllowed",
            UserPairForPerms.UserPairOwnUniquePairPerms.VibratorAlarmsToggle ? "Allow Toggling Alarms" : "Prevent Toggling Alarms",
            UserPairForPerms.UserPairOwnUniquePairPerms.VibratorAlarmsToggle ? FontAwesomeIcon.Lock : FontAwesomeIcon.Unlock,
            UserPairForPerms.UserPairOwnUniquePairPerms.VibratorAlarmsToggle ? $"Prevent {PairNickOrAliasOrUID} from toggling your alarms." : $"Allow {PairNickOrAliasOrUID} to toggle your alarms.",
            UserPairForPerms.UserPairOwnUniquePairPerms.InHardcore,
            PermissionType.UniquePairPerm, PermissionValueType.YesNo);

        DrawOwnSetting("CanExecutePatterns", "CanExecutePatternsAllowed",
            UserPairForPerms.UserPairOwnUniquePairPerms.CanExecutePatterns ? "Allow Pattern Execution" : "Prevent Pattern Execution",
            UserPairForPerms.UserPairOwnUniquePairPerms.CanExecutePatterns ? FontAwesomeIcon.Lock : FontAwesomeIcon.Unlock,
            UserPairForPerms.UserPairOwnUniquePairPerms.CanExecutePatterns ? $"Prevent {PairNickOrAliasOrUID} from executing patterns." : $"Allow {PairNickOrAliasOrUID} to execute patterns.",
            UserPairForPerms.UserPairOwnUniquePairPerms.InHardcore,
            PermissionType.UniquePairPerm, PermissionValueType.YesNo);

        DrawOwnSetting("CanExecuteTriggers", "CanExecuteTriggersAllowed",
            UserPairForPerms.UserPairOwnUniquePairPerms.CanExecuteTriggers ? "Allow Toggling Triggers" : "Prevent Toggling Triggers",
            UserPairForPerms.UserPairOwnUniquePairPerms.CanExecuteTriggers ? FontAwesomeIcon.Lock : FontAwesomeIcon.Unlock,
            UserPairForPerms.UserPairOwnUniquePairPerms.CanExecuteTriggers ? $"Prevent {PairNickOrAliasOrUID} from toggling your triggers." : $"Allow {PairNickOrAliasOrUID} to toggle your triggers.",
            UserPairForPerms.UserPairOwnUniquePairPerms.InHardcore,
            PermissionType.UniquePairPerm, PermissionValueType.YesNo);

        DrawOwnSetting("CanSendTriggers", "CanSendTriggersAllowed",
            UserPairForPerms.UserPairOwnUniquePairPerms.CanSendTriggers ? "Allow Sending Triggers" : "Prevent Sending Triggers",
            UserPairForPerms.UserPairOwnUniquePairPerms.CanSendTriggers ? FontAwesomeIcon.Lock : FontAwesomeIcon.Unlock,
            UserPairForPerms.UserPairOwnUniquePairPerms.CanSendTriggers ? $"Prevent {PairNickOrAliasOrUID} from sending triggers." : $"Allow {PairNickOrAliasOrUID} to send triggers.",
            UserPairForPerms.UserPairOwnUniquePairPerms.InHardcore,
            PermissionType.UniquePairPerm, PermissionValueType.YesNo);

        ImGui.Separator();
        /* ----------- HARDCORE PERMISSIONS ----------- */
        ImGui.TextUnformatted("Hardcore Permissions");

        DrawOwnSetting("InHardcore", string.Empty,
            UserPairForPerms.UserPairOwnUniquePairPerms.InHardcore ? $"In Hardcore Mode for {PairNickOrAliasOrUID}" : $"Not in Hardcore Mode.",
            UserPairForPerms.UserPairOwnUniquePairPerms.InHardcore ? FontAwesomeIcon.Lock : FontAwesomeIcon.Unlock,
            UserPairForPerms.UserPairOwnUniquePairPerms.InHardcore ? $"Disable Hardcore Mode for {PairNickOrAliasOrUID}." : $"Enable Hardcore Mode for {PairNickOrAliasOrUID}.",
            UserPairForPerms.UserPairOwnUniquePairPerms.InHardcore,
            PermissionType.UniquePairPerm, PermissionValueType.YesNo);

        DrawOwnSetting("IsForcedToFollow", "AllowForcedFollow",
            UserPairForPerms.UserPairOwnUniquePairPerms.IsForcedToFollow ? "Currently Forced to Follow" : "Not Forced to Follow",
            UserPairForPerms.UserPairOwnUniquePairPerms.IsForcedToFollow ? FontAwesomeIcon.Walking : FontAwesomeIcon.Ban,
            UserPairForPerms.UserPairOwnUniquePairPerms.IsForcedToFollow ? $"You are currently being forced to follow {PairNickOrAliasOrUID}" : $"{PairNickOrAliasOrUID} is not currently forcing you to follow them.",
            true, // do not allow user to change this permission.
            PermissionType.UniquePairPerm, PermissionValueType.YesNo);

        DrawOwnSetting("IsForcedToSit", "AllowForcedSit",
            UserPairForPerms.UserPairOwnUniquePairPerms.IsForcedToSit ? "Currently Forced to Sit" : "Not Forced to Sit",
            UserPairForPerms.UserPairOwnUniquePairPerms.IsForcedToSit ? FontAwesomeIcon.Chair : FontAwesomeIcon.Ban,
            UserPairForPerms.UserPairOwnUniquePairPerms.IsForcedToSit ? $"You are currently being forced to sit by {PairNickOrAliasOrUID}" : $"{PairNickOrAliasOrUID} is not currently forcing you to sit.",
            true, // do not allow user to change this permission.
            PermissionType.UniquePairPerm, PermissionValueType.YesNo);

        DrawOwnSetting("IsForcedToStay", "AllowForcedToStay",
            UserPairForPerms.UserPairOwnUniquePairPerms.IsForcedToStay ? "Currently Forced to Stay" : "Not Forced to Stay",
            UserPairForPerms.UserPairOwnUniquePairPerms.IsForcedToStay ? FontAwesomeIcon.Lock : FontAwesomeIcon.Unlock,
            UserPairForPerms.UserPairOwnUniquePairPerms.IsForcedToStay ? $"You are currently being forced to stay by {PairNickOrAliasOrUID}" : $"{PairNickOrAliasOrUID} is not currently forcing you to stay.",
            true, // do not allow user to change this permission.
            PermissionType.UniquePairPerm, PermissionValueType.YesNo);

        DrawOwnSetting("IsBlindfolded", "AllowBlindfold",
            UserPairForPerms.UserPairOwnUniquePairPerms.IsBlindfolded ? "Currently Blindfolded" : "Not Blindfolded",
            UserPairForPerms.UserPairOwnUniquePairPerms.IsBlindfolded ? FontAwesomeIcon.Blind : FontAwesomeIcon.EyeSlash,
            UserPairForPerms.UserPairOwnUniquePairPerms.IsBlindfolded ? $"You are currently blindfolded by {PairNickOrAliasOrUID}" : $"{PairNickOrAliasOrUID} is not currently blindfolding you.",
            true, // do not allow user to change this permission.
            PermissionType.UniquePairPerm, PermissionValueType.YesNo);
    }

    /// <summary>
    /// The primary call for displaying a setting for the client permissions.
    /// </summary>
    /// <param name="permissionName"> The name of the unique pair perm in string format. </param>
    /// <param name="permissionAccessName"> The name of the pair perm edit access in string format </param>
    /// <param name="textLabel"> The text to display beside the icon </param>
    /// <param name="icon"> The icon to display to the left of the text. </param>
    /// <param name="canChange"> If the permission (not edit access) can be changed. </param>
    /// <param name="tooltipStr"> the tooltip to display when hovered. </param>
    /// <param name="permissionType"> If the permission is a global perm, unique pair perm, or access permission. </param>
    /// <param name="permissionValueType"> what permission type it is (string, char, timespan, boolean) </param>
    /// <param name="pairPermAccessCurState"> The current state of the unique pair perms respective edit access. </param>
    private void DrawOwnSetting(string permissionName, string permissionAccessName, string textLabel, FontAwesomeIcon icon,
        string tooltipStr, bool canChange, PermissionType permissionType, PermissionValueType permissionValueType)
    {
        try
        {
            switch(permissionType)
            {
                case PermissionType.Global:
                    DrawOwnPermission(permissionType, _playerManager.GlobalPerms, textLabel, icon, tooltipStr, canChange,
                        permissionName, permissionValueType, permissionAccessName);
                    break;
                case PermissionType.UniquePairPerm:
                    DrawOwnPermission(permissionType, UserPairForPerms.UserPairOwnUniquePairPerms, textLabel, icon, tooltipStr, canChange,
                        permissionName, permissionValueType, permissionAccessName);
                    break;
                // this case should technically never be called for this particular instance.
                case PermissionType.UniquePairPermEditAccess:
                    DrawOwnPermission(permissionType, UserPairForPerms.UserPairOwnEditAccess, textLabel, icon, tooltipStr, canChange,
                        permissionName, permissionValueType, permissionAccessName);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to update permissions for {_uiShared.ApiController.PlayerUserData.AliasOrUID} :: {ex}");
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
    private void DrawOwnPermission(PermissionType permissionType, object permissionSet, string label,
        FontAwesomeIcon icon, string tooltip, bool isLocked, string permissionName, PermissionValueType type,
        string permissionAccessName)
    {

        // firstly, if the permission value type is a boolean, then process handling the change as a true/false.
        if (type == PermissionValueType.YesNo)
        {
            // localize the object as a boolean value from its property name.
            bool currValState = (bool)permissionSet.GetType().GetProperty(permissionName)?.GetValue(permissionSet)!;
            // draw the iconTextButton and checkbox beside it. Because we are in control, unless in hardcore, this should never be disabled.
            using (var group = ImRaii.Group())
            {
                // have a special case, where we mark the button as disabled if _playerManager.GlobalPerms.LiveChatGarblerLocked is true
                if (_uiShared.IconTextButton(icon, label, IconButtonTextWidth, true, isLocked))
                {
                    SetOwnPermission(permissionType, permissionName, !currValState);
                }
                UiSharedService.AttachToolTip(tooltip);
                if (!permissionAccessName.IsNullOrEmpty()) // only display checkbox if we should.
                {
                    ImGui.SameLine(IconButtonTextWidth);
                    if (permissionAccessName != "AllowForcedFollow" && permissionAccessName != "AllowForcedSit" && permissionAccessName != "AllowForcedToStay" && permissionAccessName != "AllowBlindfold")
                    {
                        bool refState = (bool)UserPairForPerms.UserPairOwnEditAccess.GetType().GetProperty(permissionAccessName)?.GetValue(UserPairForPerms.UserPairOwnEditAccess)!;
                        if (ImGui.Checkbox("##" + permissionAccessName, ref refState))
                        {
                            // if the new state is not the same as the current state, then we should update the permission access.
                            if (refState != (bool)UserPairForPerms.UserPairOwnEditAccess.GetType().GetProperty(permissionAccessName)?.GetValue(UserPairForPerms.UserPairOwnEditAccess)!)
                                SetOwnPermission(PermissionType.UniquePairPermEditAccess, permissionAccessName, refState);
                        }
                        UiSharedService.AttachToolTip(refState
                            ? ("Revoke " + UserPairForPerms.GetNickname() ?? UserPairForPerms.UserData.AliasOrUID + "'s control over this permission.")
                            : ("Grant " + UserPairForPerms.GetNickname() ?? UserPairForPerms.UserData.AliasOrUID) + " control over this permission, allowing them to change " +
                               "what you've set for them at will.");
                    }
                    else
                    {
                        bool refState = (bool)UserPairForPerms.UserPairOwnUniquePairPerms.GetType().GetProperty(permissionAccessName)?.GetValue(UserPairForPerms.UserPairOwnUniquePairPerms)!;
                        if (ImGui.Checkbox("##" + permissionAccessName, ref refState))
                        {
                            // if the new state is not the same as the current state, then we should update the permission access.
                            if (refState != (bool)UserPairForPerms.UserPairOwnUniquePairPerms.GetType().GetProperty(permissionAccessName)?.GetValue(UserPairForPerms.UserPairOwnUniquePairPerms)!)
                                SetOwnPermission(PermissionType.UniquePairPerm, permissionAccessName, refState);
                        }
                        UiSharedService.AttachToolTip(refState
                            ? ("Revoke " + UserPairForPerms.GetNickname() ?? UserPairForPerms.UserData.AliasOrUID + "'s control over this permission.")
                            : ("Grant " + UserPairForPerms.GetNickname() ?? UserPairForPerms.UserData.AliasOrUID) + " control over this permission, allowing them to change " +
                               "what you've set for them at will.");
                    }
                }
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
                if (_uiShared.IconInputText(id, icon, label, "format 0d0h0m0s...", ref timeSpanString, 32, IconButtonTextWidth * .55f, true, false)) { }
                // Set the permission once deactivated. If invalid, set to default.
                if (ImGui.IsItemDeactivatedAfterEdit()
                    && timeSpanString != _uiShared.TimeSpanToString((TimeSpan)permissionSet.GetType().GetProperty(permissionName)?.GetValue(permissionSet)!))
                {
                    // attempt to parse the string back into a valid timespan.
                    if (_uiShared.TryParseTimeSpan(timeSpanString, out TimeSpan result))
                    {
                        SetOwnPermission(permissionType, permissionName, result);
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
                if (!permissionAccessName.IsNullOrEmpty()) // only display checkbox if we should.
                {
                    ImGui.SameLine(IconButtonTextWidth);
                    bool refState = (bool)UserPairForPerms.UserPairOwnEditAccess.GetType().GetProperty(permissionAccessName)?.GetValue(UserPairForPerms.UserPairOwnEditAccess)!;
                    if (ImGui.Checkbox("##" + permissionAccessName, ref refState))
                    {
                        // if the new state is not the same as the current state, then we should update the permission access.
                        if (refState != (bool)UserPairForPerms.UserPairOwnEditAccess.GetType().GetProperty(permissionAccessName)?.GetValue(UserPairForPerms.UserPairOwnEditAccess)!)
                            SetOwnPermission(PermissionType.UniquePairPermEditAccess, permissionAccessName, refState);
                    }
                    UiSharedService.AttachToolTip(refState
                        ? ("Revoke " + UserPairForPerms.GetNickname() ?? UserPairForPerms.UserData.AliasOrUID + "'s control over this permission.")
                        : ("Grant " + UserPairForPerms.GetNickname() ?? UserPairForPerms.UserData.AliasOrUID) + " control over this permission, allowing them to change " +
                           "what you've set for them at will.");
                }
            }
        }
    }

    /// <summary>
    /// Send the updated permission we made for ourselves to the server.
    /// </summary>
    /// <param name="permissionType"> If Global, UniquePairPerm, or EditAccessPerm. </param>
    /// <param name="permissionName"> the attribute of the object we are changing</param>
    /// <param name="newValue"> New value to set. </param>
    private void SetOwnPermission(PermissionType permissionType, string permissionName, object newValue)
    {
        // Call the update to the server.
        switch (permissionType)
        {
            case PermissionType.Global:
                {
                    _logger.LogTrace($"Updated own global permission: {permissionName} to {newValue}");
                    _ = _apiController.UserUpdateOwnGlobalPerm(new UserGlobalPermChangeDto(_apiController.PlayerUserData,
                        new KeyValuePair<string, object>(permissionName, newValue)));
                }
                break;
            case PermissionType.UniquePairPerm:
                {
                    _logger.LogTrace($"Updated own pair permission: {permissionName} to {newValue}");
                    _ = _apiController.UserUpdateOwnPairPerm(new UserPairPermChangeDto(UserPairForPerms.UserData,
                        new KeyValuePair<string, object>(permissionName, newValue)));
                }
                break;
            // this case should technically never be called for this particular instance.
            case PermissionType.UniquePairPermEditAccess:
                {
                    _logger.LogTrace($"Updated own edit access permission: {permissionName} to {newValue}");
                    _ = _apiController.UserUpdateOwnPairPermAccess(new UserPairAccessChangeDto(UserPairForPerms.UserData,
                        new KeyValuePair<string, object>(permissionName, newValue)));
                }
                break;
        }
    }
}
