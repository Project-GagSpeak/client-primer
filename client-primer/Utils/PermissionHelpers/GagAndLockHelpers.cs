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
using static GagspeakAPI.Data.Enum.GagList;

namespace GagSpeak.Utils.PermissionHelpers;

/// <summary>
/// Various helper functions for the Permissions window.
/// </summary>
public static class GagAndLockPairkHelpers
{
    // Gag Action Variables Begin
    private static string GagSearchString = string.Empty;
    private static Dictionary<string, PairState> pairStates = new Dictionary<string, PairState>();

    public static bool ShouldExpandPasswordWindow(string pairAliasUID)
        => GetOrCreatePairState(pairAliasUID).IsExpanded;

    public static int GetSelectedLayer(string pairAliasUID)
        => GetOrCreatePairState(pairAliasUID).SelectedLayer;


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
        public int SelectedLayer = 0;
        public GagType SelectedGag = GagType.None;
        public Padlocks SelectedPadlock = Padlocks.None;
        public string Password = string.Empty;
        public string Timer = string.Empty;
    }

    // TODO: Make this accept only UID so alias doesn't fuck it up.

    public static void DrawGagLayerSelection(float comboWidth, string pairAliasUID)
    {
        var state = GetOrCreatePairState(pairAliasUID);

        using (var gagLayerGroup = ImRaii.Group())
        {
            ImGui.SetNextItemWidth(comboWidth);
            ImGui.Combo("##GagLayerSelection", ref state.SelectedLayer, new string[] { "Layer 1", "Layer 2", "Layer 3" }, 3);
            UiSharedService.AttachToolTip("Select the layer to apply a Gag to.");
        }
    }

    public static void DrawGagApplyWindow(Pair userPairForPerms, float comboWidth, string pairAliasUID, ILogger logger,
        UiSharedService uiShared, ApiController apiController, out bool success)
    {
        // grab the state storage for this pair.
        var state = GetOrCreatePairState(pairAliasUID);

        // now display the dropdown for the gag selection
        uiShared.DrawComboSearchable("Gag Type for Pair " + pairAliasUID, comboWidth, ref GagSearchString,
            Enum.GetValues<GagType>(), (gag) => gag.GetGagAlias(), false,
            (i) =>
            {
                // locate the GagData that matches the alias of i
                state.SelectedGag = AliasToGagTypeMap[i.GetGagAlias()];
            }, state.SelectedGag);
        UiSharedService.AttachToolTip("Select the gag to apply to the pair.");

        ImUtf8.SameLineInner();

        if (ImGui.Button("Apply Gag", ImGui.GetContentRegionAvail()))
        {
            // apply the selected gag. (POSSIBLY TODO: Rework the PushApperance Data to only push a single property to avoid conflicts.)
            var newAppearance = userPairForPerms.LastReceivedAppearanceData.DeepClone();
            if (newAppearance == null) throw new Exception("Appearance data is null, not sending");
            logger.LogInformation("Pushing updated Appearance Data pair and recipients");

            try
            {
                switch (state.SelectedLayer)
                {
                    case 0:
                        newAppearance.SlotOneGagType = state.SelectedGag.GetGagAlias();
                        _ = apiController.UserPushPairDataAppearanceUpdate(new OnlineUserCharaAppearanceDataDto
                            (userPairForPerms.UserData, newAppearance, DataUpdateKind.AppearanceGagAppliedLayerOne));
                        success = true;
                        break;
                    case 1:
                        newAppearance.SlotTwoGagType = state.SelectedGag.GetGagAlias();
                        _ = apiController.UserPushPairDataAppearanceUpdate(new OnlineUserCharaAppearanceDataDto
                            (userPairForPerms.UserData, newAppearance, DataUpdateKind.AppearanceGagAppliedLayerTwo));
                        success = true;
                        break;
                    case 2:
                        newAppearance.SlotThreeGagType = state.SelectedGag.GetGagAlias();
                        _ = apiController.UserPushPairDataAppearanceUpdate(new OnlineUserCharaAppearanceDataDto
                            (userPairForPerms.UserData, newAppearance, DataUpdateKind.AppearanceGagAppliedLayerThree));
                        success = true;
                        break;
                    default:
                        logger.LogWarning("Invalid layer selected: {SelectedLayer}", state.SelectedLayer);
                        success = false;
                        return;
                }
                logger.LogTrace("Applied [{0}] to [{1}] on layer {2}", state.SelectedGag, pairAliasUID, state.SelectedLayer);
                return;
            }
            catch (Exception e)
            {
                logger.LogError(e, "Failed to push appearance data for {0}", pairAliasUID);
            }
        }
        UiSharedService.AttachToolTip("Apply the selected gag to " + pairAliasUID + " on gag layer" + (state.SelectedLayer + 1));
        success = false;
        return;
    }

    public static void DrawGagLockWindow(Pair userPairForPerms, float comboWidth, string pairAliasUID, ILogger logger,
    UiSharedService uiShared, ApiController apiController, out bool success)
    {
        var state = GetOrCreatePairState(pairAliasUID);

        using (var gagLockGroup = ImRaii.Group())
        {
            uiShared.DrawCombo($"##Lock Type {state.SelectedLayer}", 248 - uiShared.GetIconTextButtonSize(FontAwesomeIcon.Lock, "Lock"),
            Enum.GetValues<Padlocks>(), (padlock) => padlock.ToString(),
            (i) =>
            {
                state.SelectedPadlock = i;
            }, state.SelectedPadlock);

            ImGui.SameLine(0, 2);

            success = false;
            if (uiShared.IconTextButton(FontAwesomeIcon.Lock, "Lock"))
            {
                // apply the selected gag. (POSSIBLY TODO: Rework the PushApperance Data to only push a single property to avoid conflicts.)
                var newAppearance = userPairForPerms.LastReceivedAppearanceData.DeepClone();
                if (newAppearance == null) throw new Exception("Appearance data is null or lock is invalid., not sending");

                if (ValidatePasswordLock(state, uiShared, userPairForPerms, logger))
                {
                    logger.LogInformation("Pushing updated Appearance Data pair and recipients");

                    try
                    {
                        switch (state.SelectedLayer)
                        {
                            case 0:
                                newAppearance.SlotOneGagPadlock = state.SelectedPadlock.ToString();
                                newAppearance.SlotOneGagPassword = state.Password;
                                newAppearance.SlotOneGagTimer = UiSharedService.GetEndTimeUTC(state.Timer);
                                newAppearance.SlotOneGagAssigner = apiController.UID;
                                _ = apiController.UserPushPairDataAppearanceUpdate(new OnlineUserCharaAppearanceDataDto
                                    (userPairForPerms.UserData, newAppearance, DataUpdateKind.AppearanceGagLockedLayerOne));
                                success = true;
                                break;
                            case 1:
                                newAppearance.SlotTwoGagPadlock = state.SelectedPadlock.ToString();
                                newAppearance.SlotTwoGagPassword = state.Password;
                                newAppearance.SlotTwoGagTimer = UiSharedService.GetEndTimeUTC(state.Timer);
                                newAppearance.SlotTwoGagAssigner = apiController.UID;
                                _ = apiController.UserPushPairDataAppearanceUpdate(new OnlineUserCharaAppearanceDataDto
                                    (userPairForPerms.UserData, newAppearance, DataUpdateKind.AppearanceGagLockedLayerTwo));
                                success = true;
                                break;
                            case 2:
                                newAppearance.SlotThreeGagPadlock = state.SelectedPadlock.ToString();
                                newAppearance.SlotThreeGagPassword = state.Password;
                                newAppearance.SlotThreeGagTimer = UiSharedService.GetEndTimeUTC(state.Timer);
                                newAppearance.SlotThreeGagAssigner = apiController.UID;
                                _ = apiController.UserPushPairDataAppearanceUpdate(new OnlineUserCharaAppearanceDataDto
                                    (userPairForPerms.UserData, newAppearance, DataUpdateKind.AppearanceGagLockedLayerThree));
                                success = true;
                                break;
                            default:
                                logger.LogWarning("Invalid layer selected: {SelectedLayer}", state.SelectedLayer);
                                break;
                        }
                        logger.LogTrace("Applied Lock to {0}'s layer {1} [{2}] with password [{3}]", pairAliasUID, state.SelectedLayer, newAppearance.SlotOneGagType, state.Password);
                    }
                    catch (Exception e)
                    {
                        logger.LogError(e, "Failed to push appearance data for {0}", pairAliasUID);
                    }
                }
                else
                {
                    if (state.SelectedLayer == 0) logger.LogWarning("Failed to apply lock to {0}'s layer {1} [{2}] with locktype {3} and password [{4}]", pairAliasUID, state.SelectedLayer, newAppearance.SlotOneGagType, state.SelectedPadlock, state.Password);
                    if (state.SelectedLayer == 1) logger.LogWarning("Failed to apply lock to {0}'s layer {1} [{2}] with locktype {3} and password [{4}]", pairAliasUID, state.SelectedLayer, newAppearance.SlotTwoGagType, state.SelectedPadlock, state.Password);
                    if (state.SelectedLayer == 2) logger.LogWarning("Failed to apply lock to {0}'s layer {1} [{2}] with locktype {3} and password [{4}]", pairAliasUID, state.SelectedLayer, newAppearance.SlotThreeGagType, state.SelectedPadlock, state.Password);
                }
                // reset the password and timer
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
    }

    public static void DrawGagUnlockWindow(Pair userPairForPerms, float comboWidth, string pairAliasUID, ILogger logger,
    UiSharedService uiShared, ApiController apiController, out bool success)
    {
        var state = GetOrCreatePairState(pairAliasUID);

        using (var gagUnlockGroup = ImRaii.Group())
        {
            uiShared.DrawCombo($"##Unlock Type {state.SelectedLayer}", 248f - uiShared.GetIconTextButtonSize(FontAwesomeIcon.Lock, "Unlock"),
            Enum.GetValues<Padlocks>(), (padlock) => padlock.ToString(),
            (i) =>
            {
                state.SelectedPadlock = i;
            }, state.SelectedPadlock);

            ImGui.SameLine(0, 2);

            success = false;
            // draw the lock button
            if (uiShared.IconTextButton(FontAwesomeIcon.Unlock, "Unlock"))
            {
                if (ValidatePasswordUnlock(state, uiShared, userPairForPerms, apiController))
                {
                    // apply the selected gag. (POSSIBLY TODO: Rework the PushApperance Data to only push a single property to avoid conflicts.)
                    var newAppearance = userPairForPerms.LastReceivedAppearanceData.DeepClone();
                    if (newAppearance == null) throw new Exception("Appearance data is null or lock is invalid., not sending");
                    logger.LogInformation("Pushing updated Appearance Data pair and recipients");

                    try // The padlocks, passwords, and assigners are required for validation server-side, and set to none afterward.
                    {
                        switch (state.SelectedLayer)
                        {
                            case 0:
                                newAppearance.SlotOneGagPadlock = state.SelectedPadlock.ToString();
                                newAppearance.SlotOneGagPassword = state.Password;
                                newAppearance.SlotOneGagTimer = DateTimeOffset.UtcNow;
                                newAppearance.SlotOneGagAssigner = apiController.UID;
                                _ = apiController.UserPushPairDataAppearanceUpdate(new OnlineUserCharaAppearanceDataDto
                                    (userPairForPerms.UserData, newAppearance, DataUpdateKind.AppearanceGagUnlockedLayerOne));
                                logger.LogTrace("Applied Lock to {0}'s underlayer [{1}] with password [{2}]", pairAliasUID, state.SelectedGag, state.Password);
                                success = true;
                                break;
                            case 1:
                                newAppearance.SlotTwoGagPadlock = state.SelectedPadlock.ToString();
                                newAppearance.SlotTwoGagPassword = state.Password;
                                newAppearance.SlotTwoGagTimer = DateTimeOffset.UtcNow;
                                newAppearance.SlotTwoGagAssigner = apiController.UID;
                                _ = apiController.UserPushPairDataAppearanceUpdate(new OnlineUserCharaAppearanceDataDto
                                    (userPairForPerms.UserData, newAppearance, DataUpdateKind.AppearanceGagUnlockedLayerTwo));
                                logger.LogTrace("Applied Lock to {0}'s central layer [{1}] with password [{2}]", pairAliasUID, state.SelectedGag, state.Password);
                                success = true;
                                break;
                            case 2:
                                newAppearance.SlotThreeGagPadlock = state.SelectedPadlock.ToString();
                                newAppearance.SlotThreeGagPassword = state.Password;
                                newAppearance.SlotThreeGagTimer = DateTimeOffset.UtcNow;
                                newAppearance.SlotThreeGagAssigner = apiController.UID;
                                _ = apiController.UserPushPairDataAppearanceUpdate(new OnlineUserCharaAppearanceDataDto
                                    (userPairForPerms.UserData, newAppearance, DataUpdateKind.AppearanceGagUnlockedLayerThree));
                                logger.LogTrace("Applied Lock to {0}'s outermost layer [{1}] with password [{2}]", pairAliasUID, state.SelectedGag, state.Password);
                                success = true;
                                break;
                            default:
                                logger.LogWarning("Invalid layer selected: {SelectedLayer}", state.SelectedLayer);
                                break;
                        }
                        logger.LogTrace("Unlocked the lock on {0}'s layer {1} [{2}] with password [{3}]", pairAliasUID, state.SelectedLayer, state.SelectedGag, state.Password);
                    }
                    catch (Exception e)
                    {
                        logger.LogError(e, "Failed to push appearance data for {0}", pairAliasUID);
                    }
                }
                else
                {
                    success = false;
                }
                // reset the password and timer
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
    }

    public static void DrawGagRemoveWindow(Pair userPairForPerms, float comboWidth, string pairAliasUID, ILogger logger,
    UiSharedService uiShared, ApiController apiController, out bool success)
    {
        // grab the state storage for this pair.
        var state = GetOrCreatePairState(pairAliasUID);


        success = false;
        if (ImGui.Button("Remove Gag", ImGui.GetContentRegionAvail()))
        {
            // apply the selected gag. (POSSIBLY TODO: Rework the PushApperance Data to only push a single property to avoid conflicts.)
            var newAppearance = userPairForPerms.LastReceivedAppearanceData.DeepClone();
            if (newAppearance == null) throw new Exception("Appearance data is null, not sending");
            logger.LogInformation("Pushing updated Appearance Data pair and recipients");

            try
            {
                switch (state.SelectedLayer)
                {
                    case 0:
                        newAppearance.SlotOneGagType = GagType.None.GetGagAlias();
                        _ = apiController.UserPushPairDataAppearanceUpdate(new OnlineUserCharaAppearanceDataDto
                            (userPairForPerms.UserData, newAppearance, DataUpdateKind.AppearanceGagRemovedLayerOne));
                        success = true;
                        break;
                    case 1:
                        newAppearance.SlotTwoGagType = GagType.None.GetGagAlias();
                        _ = apiController.UserPushPairDataAppearanceUpdate(new OnlineUserCharaAppearanceDataDto
                            (userPairForPerms.UserData, newAppearance, DataUpdateKind.AppearanceGagRemovedLayerTwo));
                        success = true;
                        break;
                    case 2:
                        newAppearance.SlotThreeGagType = GagType.None.GetGagAlias();
                        _ = apiController.UserPushPairDataAppearanceUpdate(new OnlineUserCharaAppearanceDataDto
                            (userPairForPerms.UserData, newAppearance, DataUpdateKind.AppearanceGagRemovedLayerThree));
                        success = true;
                        break;
                    default:
                        logger.LogWarning("Invalid layer selected: {SelectedLayer}", state.SelectedLayer);
                        break;
                }
                logger.LogTrace("Removed [{0}] from [{1}] on layer {2}", state.SelectedGag, pairAliasUID, state.SelectedLayer);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Failed to push appearance data for {0}", pairAliasUID);
            }
        }
        UiSharedService.AttachToolTip("Remove actively equipped gag from " + pairAliasUID + " on layer " + (state.SelectedLayer + 1));
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
                if(uiShared.TryParseTimeSpan(state.Timer, out var test))
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
                switch (state.SelectedLayer)
                {
                    case 0: return uiShared.ValidateCombination(state.Password) && state.Password == userPairForPerms.LastReceivedAppearanceData.SlotOneGagPassword;
                    case 1: return uiShared.ValidateCombination(state.Password) && state.Password == userPairForPerms.LastReceivedAppearanceData.SlotTwoGagPassword;
                    case 2: return uiShared.ValidateCombination(state.Password) && state.Password == userPairForPerms.LastReceivedAppearanceData.SlotThreeGagPassword;
                }
                break;
            case Padlocks.PasswordPadlock:
            case Padlocks.TimerPasswordPadlock:
                switch (state.SelectedLayer)
                {
                    case 0: return uiShared.ValidatePassword(state.Password) && state.Password == userPairForPerms.LastReceivedAppearanceData.SlotOneGagPassword;
                    case 1: return uiShared.ValidatePassword(state.Password) && state.Password == userPairForPerms.LastReceivedAppearanceData.SlotTwoGagPassword;
                    case 2: return uiShared.ValidatePassword(state.Password) && state.Password == userPairForPerms.LastReceivedAppearanceData.SlotThreeGagPassword;
                }
                break;
            case Padlocks.OwnerPadlock:
            case Padlocks.OwnerTimerPadlock:
                switch (state.SelectedLayer)
                {
                    case 0: return userPairForPerms.UserPairUniquePairPerms.OwnerLocks && apiController.UID == userPairForPerms.LastReceivedAppearanceData.SlotOneGagAssigner;
                    case 1: return userPairForPerms.UserPairUniquePairPerms.OwnerLocks && apiController.UID == userPairForPerms.LastReceivedAppearanceData.SlotTwoGagAssigner;
                    case 2: return userPairForPerms.UserPairUniquePairPerms.OwnerLocks && apiController.UID == userPairForPerms.LastReceivedAppearanceData.SlotThreeGagAssigner;
                }
                break;
        }
        return false;
    }
}
