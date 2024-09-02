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
        public Padlocks SelectedPadlock = Padlocks.None;
        public string Password = string.Empty;
        public string Timer = string.Empty;
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

        using (var gagLockGroup = ImRaii.Group())
        {
            uiShared.DrawCombo($"##RestraintSetLockType", 248 - uiShared.GetIconTextButtonSize(FontAwesomeIcon.Lock, "Lock"),
            Enum.GetValues<Padlocks>(), (padlock) => padlock.ToString(), (i) => { state.SelectedPadlock = i; }, state.SelectedPadlock);

            ImGui.SameLine(0, 2);

            success = false;
            if (uiShared.IconTextButton(FontAwesomeIcon.Lock, "Lock"))
            {
                var newWardrobeData = userPairForPerms.LastReceivedWardrobeData.DeepClone();
                if (newWardrobeData == null) throw new Exception("Wardrobe data is null, not sending");

                if (ValidatePasswordLock(state, uiShared, userPairForPerms, logger))
                {
                    logger.LogInformation("Pushing updated Wardrobe Data for pair to them and their and recipients");
                    try
                    {
                        newWardrobeData.WardrobeActiveSetPadLock = state.SelectedPadlock.ToString();
                        newWardrobeData.WardrobeActiveSetPassword = state.Password;
                        newWardrobeData.WardrobeActiveSetLockTime = UiSharedService.GetEndTimeUTC(state.Timer);
                        newWardrobeData.WardrobeActiveSetLockAssigner = apiController.UID;
                        _ = apiController.UserPushPairDataWardrobeUpdate(new OnlineUserCharaWardrobeDataDto
                            (userPairForPerms.UserData, newWardrobeData, DataUpdateKind.WardrobeRestraintLocked));
                        success = true;

                        logger.LogTrace("Locking {0} with {1} and password {2} for {3}", pairAliasUID,
                            state.SelectedPadlock, state.Password, UiSharedService.GetEndTimeUTC(state.Timer) - DateTimeOffset.UtcNow);
                    }
                    catch
                    {
                        logger.LogError("Failed to push wardrobe data for {0}", pairAliasUID);
                    }
                }
            }
            // display associated password field for padlock type.
            if (DisplayPadlockFields(state))
            {
                state.IsExpanded = true;
            }
            else
            {
                state.IsExpanded = false;
            }
        }
        UiSharedService.AttachToolTip("Lock the selected set for " + pairAliasUID + " for " + (UiSharedService.GetEndTimeUTC(state.Timer) - DateTimeOffset.UtcNow));
    }

    public static void DrawUnlockSet(Pair userPairForPerms, string pairAliasUID, ILogger logger,
    UiSharedService uiShared, ApiController apiController, out bool success)
    {
        var state = GetOrCreatePairState(pairAliasUID);

        using (var gagUnlockGroup = ImRaii.Group())
        {
            uiShared.DrawCombo($"##UnlockRestraintSetPadlocks", 248f - uiShared.GetIconTextButtonSize(FontAwesomeIcon.Lock, "Unlock"),
            Enum.GetValues<Padlocks>(), (padlock) => padlock.ToString(), (i) => { state.SelectedPadlock = i; }, state.SelectedPadlock);

            ImGui.SameLine(0, 2);

            success = false;
            if (uiShared.IconTextButton(FontAwesomeIcon.Unlock, "Unlock"))
            {
                if (ValidatePasswordUnlock(state, uiShared, userPairForPerms, apiController))
                {
                    var newWardrobeData = userPairForPerms.LastReceivedWardrobeData.DeepClone();
                    if (newWardrobeData == null) throw new Exception("Wardrobe data is null, not sending");
                    logger.LogInformation("Pushing updated Wardrobe Data for pair to them and their and recipients");
                    try
                    {
                        newWardrobeData.WardrobeActiveSetPadLock = Padlocks.None.ToString();
                        newWardrobeData.WardrobeActiveSetPassword = string.Empty;
                        newWardrobeData.WardrobeActiveSetLockTime = DateTimeOffset.MinValue;
                        newWardrobeData.WardrobeActiveSetLockAssigner = string.Empty;
                        _ = apiController.UserPushPairDataWardrobeUpdate(new OnlineUserCharaWardrobeDataDto
                            (userPairForPerms.UserData, newWardrobeData, DataUpdateKind.WardrobeRestraintUnlocked));
                        success = true;
                        logger.LogTrace("Unlocking {0} with {1} and password {2}", pairAliasUID, state.SelectedPadlock, state.Password);
                    }
                    catch
                    {
                        logger.LogError("Failed to push wardrobe data for {0}", pairAliasUID);
                    }
                }
                // reset password and timer.
                state.SelectedPadlock = Padlocks.None;
                state.Password = string.Empty;
                state.Timer = string.Empty;
            }
            // display associated password field for padlock type.
            if (DisplayPadlockFields(state))
            {
                state.IsExpanded = true;
            }
            else
            {
                state.IsExpanded = false;
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
                    (userPairForPerms.UserData, newWardrobe, DataUpdateKind.WardrobeRestraintDisabled));
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

    private static bool DisplayPadlockFields(PairState state)
    {
        switch (state.SelectedPadlock)
        {
            case Padlocks.CombinationPadlock:
                state.Password = DisplayInputField("##Combination_Input", "Enter 4 digit combination...", state.Password, 4);
                return true;
            case Padlocks.PasswordPadlock:
                state.Password = DisplayInputField("##Password_Input", "Enter password", state.Password, 20);
                return true;
            case Padlocks.TimerPasswordPadlock:
                state.Password = DisplayInputField("##Password_Input", "Enter password", state.Password, 20, 2 / 3f);
                ImGui.SameLine(0, 3);
                state.Timer = DisplayInputField("##Timer_Input", "Ex: 0h2m7s", state.Timer, 12, .325f);
                return true;
            case Padlocks.OwnerTimerPadlock:
                state.Timer = DisplayInputField("##Timer_Input", "Ex: 0h2m7s", state.Timer, 12);
                return true;
        }
        return false;
    }

    private static string DisplayInputField(string id, string hint, string value, uint maxLength, float widthRatio = 1f)
    {
        var result = value;
        ImGui.SetNextItemWidth(250 * widthRatio);
        if (ImGui.InputTextWithHint(id, hint, ref result, maxLength, ImGuiInputTextFlags.None))
            return result;
        return value;
    }

    private static bool ValidatePasswordLock(PairState state, UiSharedService uiShared, Pair userPairForPerms, ILogger logger)
    {
        var result = false;
        switch (state.SelectedPadlock)
        {
            case Padlocks.None:
                return false;
            case Padlocks.MetalPadlock:
            case Padlocks.FiveMinutesPadlock:
                return true;
            case Padlocks.CombinationPadlock:
                result = uiShared.ValidateCombination(state.Password);
                if (!result) logger.LogWarning("Invalid combination entered: {Password}", state.Password);
                return result;
            case Padlocks.PasswordPadlock:
                result = uiShared.ValidatePassword(state.Password);
                if (!result) logger.LogWarning("Invalid password entered: {Password}", state.Password);
                return result;
            case Padlocks.TimerPasswordPadlock:
                if (uiShared.TryParseTimeSpan(state.Timer, out var test))
                {
                    if (test > TimeSpan.FromHours(1) && !userPairForPerms.UserPairUniquePairPerms.ExtendedLockTimes)
                    {
                        logger.LogWarning("Attempted to lock for more than 1 hour without permission.");
                        return false;
                    }
                    result = uiShared.ValidatePassword(state.Password) && test > TimeSpan.Zero;
                }
                if (!result) logger.LogWarning("Invalid password or time entered: {Password} {Timer}", state.Password, state.Timer);
                return result;
            case Padlocks.OwnerPadlock:
                return userPairForPerms.UserPairUniquePairPerms.OwnerLocks;
            case Padlocks.OwnerTimerPadlock:
                var validTime = uiShared.TryParseTimeSpan(state.Timer, out var test2);
                if (!validTime)
                    return false;
                // Check if the TimeSpan is longer than one hour and extended locks are not allowed
                if (test2 > TimeSpan.FromHours(1) && !userPairForPerms.UserPairUniquePairPerms.ExtendedLockTimes)
                    return false;
                // return base case.
                return validTime && userPairForPerms.UserPairUniquePairPerms.OwnerLocks;
        }
        return false;
    }

    private static bool ValidatePasswordUnlock(PairState state, UiSharedService uiShared, Pair userPairForPerms, ApiController apiController)
    {
        switch (state.SelectedPadlock)
        {
            case Padlocks.None:
                return false;
            case Padlocks.MetalPadlock:
            case Padlocks.FiveMinutesPadlock:
                return true;
            case Padlocks.CombinationPadlock:
                return uiShared.ValidateCombination(state.Password) && state.Password == userPairForPerms.LastReceivedWardrobeData!.WardrobeActiveSetPassword;
            case Padlocks.PasswordPadlock:
            case Padlocks.TimerPasswordPadlock:
                return uiShared.ValidatePassword(state.Password) && state.Password == userPairForPerms.LastReceivedWardrobeData!.WardrobeActiveSetPassword;
            case Padlocks.OwnerPadlock:
            case Padlocks.OwnerTimerPadlock:
                return userPairForPerms.UserPairUniquePairPerms.OwnerLocks && apiController.UID == userPairForPerms.LastReceivedWardrobeData!.WardrobeActiveSetPassword;
        }
        return false;
    }
}
