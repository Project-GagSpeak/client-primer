using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using GagSpeak.PlayerData.Pairs;
using GagSpeak.UI;
using GagSpeak.WebAPI;
using GagSpeak.WebAPI.Utils;
using GagspeakAPI.Data.Enum;
using GagspeakAPI.Dto.Connection;
using ImGuiNET;
using OtterGui.Text;
using System.Configuration;
using static GagspeakAPI.Data.Enum.GagList;

namespace GagSpeak.Utils.PermissionHelpers;

/// <summary>
/// Various helper functions for the Permissions window.
/// </summary>
public static class WardrobeHelpers
{
    // Gag Action Variables Begin
    private static string WardrobeSearchString = string.Empty;
    private static Dictionary<string, PairState> pairStates = new Dictionary<string, PairState>();

    public static int GetSelectedSetIdx(string pairAliasUID)
        => GetOrCreatePairState(pairAliasUID).SelectedSetIdx;

    public static string GetSelectedSetName(string pairAliasUID)
        => GetOrCreatePairState(pairAliasUID).SelectedSetName;

    private static PairState GetOrCreatePairState(string pairAliasUID)
    {
        if (!pairStates.TryGetValue(pairAliasUID, out var state))
        {
            state = new PairState();
            pairStates[pairAliasUID] = state;
        }
        return state;
    }

    private class PairState
    {
        public bool IsExpanded = false;
        public int SelectedSetIdx = 0;
        public string SelectedSetName = string.Empty;
        public string LockDurationString = string.Empty;
    }

    // TODO: Make this accept only UID so alias doesn't fuck it up.

    public static void DrawRestraintSetSelection(Pair userPairForPerms, float comboWidth, string pairAliasUID, UiSharedService uiShared)
    {
        var state = GetOrCreatePairState(pairAliasUID);

        // now display the dropdown for the gag selection
        uiShared.DrawComboSearchable("Restraint Sets for" + pairAliasUID, comboWidth, ref WardrobeSearchString,
            userPairForPerms.LastReceivedWardrobeData!.OutfitNames, (i) => i, false,
        (i) =>
        {
            state.SelectedSetIdx = userPairForPerms.LastReceivedWardrobeData!.OutfitNames.IndexOf(i!);
            state.SelectedSetName = i!;

        }, state.SelectedSetName);
        UiSharedService.AttachToolTip("Select the Restraint Set to apply to " + pairAliasUID);
    }

    public static void DrawApplySet(Pair userPairForPerms, string pairAliasUID, ILogger logger,
        UiSharedService uiShared, ApiController apiController, out bool success)
    {
        var state = GetOrCreatePairState(pairAliasUID);

        success = false;
        if (uiShared.IconTextButton(FontAwesomeIcon.Female, "Apply Set", null, false, state.SelectedSetName == string.Empty))
        {
            // Apply the selected set, and compile new wardrobe data.
            var newWardrobe = userPairForPerms.LastReceivedWardrobeData.DeepClone();
            if (newWardrobe == null) throw new Exception("Wardrobe data is null, not sending");
            logger.LogInformation("Pushing updated Wardrobe Data pair and recipients");

            // update the details of the new Dto that should be used.
            newWardrobe.ActiveSetName = state.SelectedSetName;
            newWardrobe.ActiveSetEnabledBy = apiController.UID;

            try
            {
                _ = apiController.UserPushPairDataWardrobeUpdate(new OnlineUserCharaWardrobeDataDto
                    (userPairForPerms.UserData, newWardrobe, DataUpdateKind.WardrobeRestraintApplied));
                success = true;
                // clear the selected stuff
                state.SelectedSetName = string.Empty;
                state.SelectedSetIdx = 0;
            }
            catch
            {
                logger.LogError("Failed to push wardrobe data for {0}", pairAliasUID);
            }
        }
        UiSharedService.AttachToolTip(pairAliasUID + " will now be restrained by the selected set " + state.SelectedSetName);
    }

    public static void DrawLockRestraintSet(Pair userPairForPerms, float comboWidth, string pairAliasUID, ILogger logger,
        UiSharedService uiShared, ApiController apiController, out bool success)
    {
        var state = GetOrCreatePairState(pairAliasUID);


        success = false;
        // Draw out the timer field.
        state.LockDurationString = DisplayInputField("##Timer_Input", "Format Example: 0h2m7s", state.LockDurationString, 
            12, comboWidth - ImGui.GetStyle().ItemSpacing.X);

        // in same line,
        ImUtf8.SameLineInner();

        // parse time string to ensure valid time is set
        bool AllowApplyButton = uiShared.TryParseTimeSpan(state.LockDurationString, out var lockTimeSpan);
        if (lockTimeSpan > TimeSpan.FromHours(1) && !userPairForPerms.UserPairUniquePairPerms.ExtendedLockTimes)
        {
            logger.LogWarning("Attempted to lock for more than 1 hour without permission.");
            AllowApplyButton = false;
        }

        // Draw the lock button
        if (uiShared.IconTextButton(FontAwesomeIcon.Lock, "Lock Set", null, false))
        {
            // Apply the selected set, and compile new wardrobe data.
            var newWardrobe = userPairForPerms.LastReceivedWardrobeData.DeepClone();
            if (newWardrobe == null) throw new Exception("Wardrobe data is null, not sending");
            logger.LogInformation("Pushing updated Wardrobe Data pair and recipients");

            newWardrobe.ActiveSetIsLocked = true;
            newWardrobe.ActiveSetLockedBy = apiController.UID;
            newWardrobe.ActiveSetLockTime = UiSharedService.GetEndTimeUTC(state.LockDurationString);

            // send off to server
            _ = apiController.UserPushPairDataWardrobeUpdate(new OnlineUserCharaWardrobeDataDto
                (userPairForPerms.UserData, newWardrobe, DataUpdateKind.WardrobeRestraintLocked));
            success = true;

            // clear the selected stuff
            state.LockDurationString = string.Empty;
        }
        UiSharedService.AttachToolTip("Lock the selected set for " + pairAliasUID + " for " + lockTimeSpan);
    }

    public static void DrawUnlockSet(Pair userPairForPerms, string pairAliasUID, ILogger logger,
    UiSharedService uiShared, ApiController apiController, out bool success)
    {
        var state = GetOrCreatePairState(pairAliasUID);

        success = false;
        // the prior menu logic should handle this condition to not display when it shouldnt be used.
        if (uiShared.IconTextButton(FontAwesomeIcon.Unlock, "Unlock Set", ImGui.GetContentRegionAvail().X, false))
        {
            // Apply the selected set, and compile new wardrobe data.
            var newWardrobe = userPairForPerms.LastReceivedWardrobeData.DeepClone();
            if (newWardrobe == null) throw new Exception("Wardrobe data is null, not sending");
            logger.LogInformation("Pushing updated Wardrobe Data pair and recipients");

            newWardrobe.ActiveSetIsLocked = false;
            newWardrobe.ActiveSetLockedBy = string.Empty; // do not ref this here, as we have a ref for it on the user's activeState table.
            newWardrobe.ActiveSetLockTime = DateTimeOffset.UtcNow;

            try
            {
                _ = apiController.UserPushPairDataWardrobeUpdate(new OnlineUserCharaWardrobeDataDto
                    (userPairForPerms.UserData, newWardrobe, DataUpdateKind.WardrobeRestraintUnlocked));
                success = true;
            }
            catch
            {
                logger.LogError("Failed to push wardrobe data for {0}", pairAliasUID);
            }
        }
        UiSharedService.AttachToolTip("Unlock " + pairAliasUID + " from their currently locked restraint set");
    }

    public static void DrawRemoveSet(Pair userPairForPerms, string pairAliasUID, ILogger logger,
    UiSharedService uiShared, ApiController apiController, out bool success)
    {
        var state = GetOrCreatePairState(pairAliasUID);

        success = false;
        if (uiShared.IconTextButton(FontAwesomeIcon.Key, "Remove Set", ImGui.GetContentRegionAvail().X, false))
        {
            // Apply the selected set, and compile new wardrobe data.
            var newWardrobe = userPairForPerms.LastReceivedWardrobeData.DeepClone();
            if (newWardrobe == null) throw new Exception("Wardrobe data is null, not sending");
            logger.LogInformation("Pushing updated Wardrobe Data pair and recipients");

            newWardrobe.ActiveSetName = string.Empty;
            newWardrobe.ActiveSetEnabledBy = string.Empty;

            try
            {
                _ = apiController.UserPushPairDataWardrobeUpdate(new OnlineUserCharaWardrobeDataDto
                    (userPairForPerms.UserData, newWardrobe, DataUpdateKind.WardrobeRestraintRemoved));
                success = true;
                // clear the selected stuff
                state.SelectedSetName = string.Empty;
                state.SelectedSetIdx = 0;
            }
            catch
            {
                logger.LogError("Failed to push wardrobe data for {0}", pairAliasUID);
            }
        }
        UiSharedService.AttachToolTip(pairAliasUID + " will now be restrained by the selected set " + state.SelectedSetName);
    }



    private static string DisplayInputField(string id, string hint, string value, uint maxLength, float widthRatio = 1f)
    {
        var result = value;
        ImGui.SetNextItemWidth(250 * widthRatio);
        if (ImGui.InputTextWithHint(id, hint, ref result, maxLength, ImGuiInputTextFlags.None))
            return result;
        return value;
    }
}
