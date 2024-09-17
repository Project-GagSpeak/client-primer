using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Utility;
using GagSpeak.Utils;
using GagSpeak.WebAPI.Utils;
using GagspeakAPI.Data;
using GagspeakAPI.Data.Character;
using GagspeakAPI.Data.Enum;
using GagspeakAPI.Data.Interfaces;
using GagspeakAPI.Data.Permissions;
using GagspeakAPI.Dto.Connection;
using GagspeakAPI.Dto.Permissions;
using GagspeakAPI.Dto.Toybox;
using ImGuiNET;
using OtterGui.Text;
using ProjectGagspeakAPI.Data.VibeServer;
using System.Numerics;

namespace GagSpeak.UI.Permissions;

/// <summary>
/// Contains functions relative to the paired users permissions for the client user.
/// 
/// Yes its messy, yet it's long, but i functionalized it best i could for the insane 
/// amount of logic being performed without adding too much overhead.
/// </summary>
public partial class UserPairPermsSticky
{
    public void DrawPairActionFunctions()
    {
        /* ----------- GLOBAL SETTINGS ----------- */
        ImGui.TextUnformatted("Common Pair Functions");

        // draw the common client functions
        DrawCommonClientMenu();

        if (UserPairForPerms != null && UserPairForPerms.IsOnline)
        {
            // Online Pair Actions
            if (UserPairForPerms.LastReceivedAppearanceData != null)
            {
                ImGui.TextUnformatted("Gag Actions");
                DrawGagActions();
            }

            if (UserPairForPerms.LastReceivedWardrobeData != null)
            {
                ImGui.TextUnformatted("Wardrobe Actions");
                DrawWardrobeActions();
            }

            if (UserPairForPerms.LastReceivedAliasData != null)
            {
                ImGui.TextUnformatted("Puppeteer Actions");
                DrawPuppeteerActions();
            }

            if (UserPairForPerms.LastReceivedIpcData != null && UserPairForPerms.IsVisible)
            {
                ImGui.TextUnformatted("Moodles Actions");
                DrawMoodlesActions();
            }

            if (UserPairForPerms.LastReceivedToyboxData != null)
            {
                ImGui.TextUnformatted("Toybox Actions");
                DrawToyboxActions();
            }

            if (UserPairForPerms.UserPairUniquePairPerms.InHardcore)
            {
                ImGui.TextUnformatted("Hardcore Actions");
                DrawHardcoreActions();
            }

            if (UserPairForPerms.UserPairUniquePairPerms.InHardcore && (UniqueShockCollarPermsExist() || GlobalShockCollarPermsExist()))
            {
                ImGui.TextUnformatted("Hardcore Shock Collar Actions.");
                DrawHardcoreShockCollarActions();
            }
        }

        // individual Menu
        ImGui.TextUnformatted("Individual Pair Functions");
        DrawIndividualMenu();
    }

    private bool UniqueShockCollarPermsExist() => !UserPairForPerms.UserPairUniquePairPerms.ShockCollarShareCode.IsNullOrEmpty() && UserPairForPerms.LastPairPiShockPermsForYou.MaxIntensity != -1;
    private bool GlobalShockCollarPermsExist() => !UserPairForPerms.UserPairGlobalPerms.GlobalShockShareCode.IsNullOrEmpty() && UserPairForPerms.LastPairGlobalShockPerms.MaxIntensity != -1;

    private void DrawCommonClientMenu()
    {
        if (!UserPairForPerms.IsPaused)
        {
            if (_uiShared.IconTextButton(FontAwesomeIcon.User, "Open Profile", WindowMenuWidth, true))
            {
                _displayHandler.OpenProfile(UserPairForPerms);
                ImGui.CloseCurrentPopup();
            }
            UiSharedService.AttachToolTip("Opens the profile for this user in a new window");
        }
        if (UserPairForPerms.IsPaired)
        {
            var pauseIcon = UserPairForPerms.UserPair!.OwnPairPerms.IsPaused ? FontAwesomeIcon.Play : FontAwesomeIcon.Pause;
            var pauseText = UserPairForPerms.UserPair!.OwnPairPerms.IsPaused ? $"Unpause {UserPairForPerms.UserData.AliasOrUID}" : $"Pause {UserPairForPerms.UserData.AliasOrUID}";
            if (_uiShared.IconTextButton(pauseIcon, pauseText, WindowMenuWidth, true))
            {
                var perm = UserPairForPerms.UserPair!.OwnPairPerms;
                _ = _apiController.UserUpdateOwnPairPerm(new UserPairPermChangeDto(UserPairForPerms.UserData,
                    new KeyValuePair<string, object>("IsPaused", !perm.IsPaused)));
            }
            UiSharedService.AttachToolTip(!UserPairForPerms.UserPair!.OwnPairPerms.IsPaused
            ? "Pause pairing with " + UserPairForPerms.UserData.AliasOrUID
                : "Resume pairing with " + UserPairForPerms.UserData.AliasOrUID);
        }
        if (UserPairForPerms.IsVisible)
        {
            if (_uiShared.IconTextButton(FontAwesomeIcon.Sync, "Reload IPC data", WindowMenuWidth, true))
            {
                UserPairForPerms.ApplyLastReceivedIpcData(forced: true);
                ImGui.CloseCurrentPopup();
            }
            UiSharedService.AttachToolTip("This reapplies the latest data from Customize+ and Moodles");
        }

        ImGui.Separator();
    }

    #region GagActions
    private bool ShowGagList = false;
    private bool ShowGagLock = false;
    private bool ShowGagUnlock = false;
    private bool ShowGagRemove = false;
    private void DrawGagActions()
    {
        // draw the layer
        _permActions.DrawGagLayerSelection(ImGui.GetContentRegionAvail().X, UserPairForPerms.UserData.UID);

        bool canUseGagFeatures = UserPairForPerms.UserPairUniquePairPerms.GagFeatures;
        bool canUseOwnerLocks = UserPairForPerms.UserPairUniquePairPerms.OwnerLocks;
        bool disableLocking = UserPairForPerms.LastReceivedAppearanceData!.GagSlots[_permActions.GagLayer].GagType == GagType.None.GagName();
        bool disableUnlocking = UserPairForPerms.LastReceivedAppearanceData!.GagSlots[_permActions.GagLayer].Padlock == Padlocks.None.ToName();
        bool disableRemoving = !disableUnlocking;

        ////////// APPLY GAG //////////
        if (_uiShared.IconTextButton(FontAwesomeIcon.CommentDots, ("Apply a Gag to " + PairUID), WindowMenuWidth, true, !disableUnlocking || !canUseGagFeatures))
        {
            ShowGagList = !ShowGagList;
        }
        UiSharedService.AttachToolTip("Apply a Gag to " + PairUID + ". Click to view options.");
        if (ShowGagList)
        {
            using (var actionChild = ImRaii.Child("GagApplyChild", new Vector2(WindowMenuWidth, ImGui.GetFrameHeight()), false))
            {
                if (!actionChild) return;
                GagType selected = _permActions.GetSelectedItem<GagType>("ApplyGagForPairPermCombo", UserPairForPerms.UserData.UID);

                _permActions.DrawGenericComboButton(UserPairForPerms.UserData.UID, "ApplyGagForPairPermCombo", "Apply Gag",
                WindowMenuWidth, Enum.GetValues<GagType>(), (gag) => gag.GagName(), true, selected == GagType.None, false, GagType.None,
                FontAwesomeIcon.None, ImGuiComboFlags.None, (selected) => { _logger.LogDebug("Selected Gag: " + selected); },
                (onButtonPress) =>
                {
                    try
                    {
                        var newAppearance = UserPairForPerms.LastReceivedAppearanceData.DeepClone();
                        if (newAppearance == null) throw new Exception("Appearance data is null, not sending");
                        newAppearance.GagSlots[_permActions.GagLayer].GagType = onButtonPress.GagName();
                        DataUpdateKind updateKind = _permActions.GagLayer switch
                        {
                            0 => DataUpdateKind.AppearanceGagAppliedLayerOne,
                            1 => DataUpdateKind.AppearanceGagAppliedLayerTwo,
                            2 => DataUpdateKind.AppearanceGagAppliedLayerThree,
                            _ => throw new Exception("Invalid layer selected.")
                        };
                        _ = _apiController.UserPushPairDataAppearanceUpdate(new(UserPairForPerms.UserData, newAppearance, updateKind));
                        _logger.LogDebug("Applying Selected Gag {0} to {1}", onButtonPress.GagName(), UserPairForPerms.UserData.AliasOrUID);
                        ShowGagList = false;
                    }
                    catch (Exception e) { _logger.LogError("Failed to push updated appearance data: " + e.Message); }
                });
            }
            ImGui.Separator();
        }

        ////////// LOCK GAG //////////
        using (var color = ImRaii.PushColor(ImGuiCol.Text, disableUnlocking ? ImGuiColors.DalamudWhite : ImGuiColors.DalamudYellow))
        {
            string DisplayText = disableUnlocking ? "Lock " + PairUID + "'s Gag"
                : UserPairForPerms.LastReceivedAppearanceData!.GagSlots[_permActions.GagLayer].Padlock + " is on this Gag.";
            if (_uiShared.IconTextButton(FontAwesomeIcon.Lock, DisplayText, WindowMenuWidth, true, disableLocking || !canUseGagFeatures))
            {
                ShowGagLock = !ShowGagLock;
            }
            UiSharedService.AttachToolTip("Lock " + PairUID + "'s Gag. Click to view options.");
        }
        if (ShowGagLock)
        {

            Padlocks selected = _permActions.GetSelectedItem<Padlocks>("LockGagForPairPermCombo", UserPairForPerms.UserData.UID);
            float height = _permActions.ExpandLockHeightCheck(selected) ? ImGui.GetFrameHeight() * 2 + ImGui.GetStyle().ItemSpacing.Y : ImGui.GetFrameHeight();

            using (var actionChild = ImRaii.Child("GagLockChild", new Vector2(WindowMenuWidth, height), false))
            {
                if (!actionChild) return;

                bool disabled = selected == Padlocks.None || !canUseGagFeatures;
                // Draw combo
                _permActions.DrawGenericComboButton(UserPairForPerms.UserData.UID, "LockGagForPairPermCombo", "Lock",
                WindowMenuWidth, Enum.GetValues<Padlocks>(), (padlock) => padlock.ToName(), false, disabled, true, Padlocks.None,
                FontAwesomeIcon.Lock, ImGuiComboFlags.None, (selected) => { _logger.LogDebug("Selected Padlock: " + selected); },
                (onButtonPress) =>
                {
                    var newAppearance = UserPairForPerms.LastReceivedAppearanceData.DeepClone();
                    if (newAppearance == null) throw new Exception("Appearance data is null or lock is invalid., not sending");

                    if (_permActions.PadlockVerifyLock<IPadlockable>(newAppearance.GagSlots[_permActions.GagLayer], onButtonPress,
                    UserPairForPerms.UserPairUniquePairPerms.ExtendedLockTimes, canUseOwnerLocks))
                    {
                        try
                        {
                            newAppearance.GagSlots[_permActions.GagLayer].Padlock = onButtonPress.ToString();
                            newAppearance.GagSlots[_permActions.GagLayer].Password = _permActions.Password;
                            newAppearance.GagSlots[_permActions.GagLayer].Timer = UiSharedService.GetEndTimeUTC(_permActions.Timer);
                            newAppearance.GagSlots[_permActions.GagLayer].Assigner = _apiController.UID;
                            DataUpdateKind updateKind = _permActions.GagLayer switch
                            {
                                0 => DataUpdateKind.AppearanceGagLockedLayerOne,
                                1 => DataUpdateKind.AppearanceGagLockedLayerTwo,
                                2 => DataUpdateKind.AppearanceGagLockedLayerThree,
                                _ => throw new Exception("Invalid layer selected.")
                            };
                            _ = _apiController.UserPushPairDataAppearanceUpdate(new(UserPairForPerms.UserData, newAppearance, updateKind));
                            _logger.LogDebug("Locking Gag with GagPadlock {0} on {1}", onButtonPress.ToString(), PairNickOrAliasOrUID);
                            ShowGagLock = false;
                            // reset the password and timer
                            _permActions.Password = string.Empty;
                            _permActions.Timer = string.Empty;
                        }
                        catch (Exception e) { _logger.LogError("Failed to push updated appearance data: " + e.Message); }
                    }
                });
                // draw password field combos.
                _permActions.DisplayPadlockFields(selected);
            }
            ImGui.Separator();
        }

        ////////// UNLOCK GAG //////////
        if (_uiShared.IconTextButton(FontAwesomeIcon.Unlock, ("Unlock " + PairUID + "'s Gag"), WindowMenuWidth, true, disableUnlocking || !canUseGagFeatures))
        {
            ShowGagUnlock = !ShowGagUnlock;
        }
        UiSharedService.AttachToolTip("Unlock " + PairUID + "'s Gag. Click to view options.");
        if (ShowGagUnlock)
        {
            Padlocks selected = _permActions.GetSelectedItem<Padlocks>("UnlockGagForPairPermCombo", UserPairForPerms.UserData.UID);
            float height = _permActions.ExpandLockHeightCheck(selected) ? ImGui.GetFrameHeight() * 2 + ImGui.GetStyle().ItemSpacing.Y : ImGui.GetFrameHeight();
            using (var actionChild = ImRaii.Child("GagUnlockChild", new Vector2(WindowMenuWidth, height), false))
            {
                if (!actionChild) return;

                bool disabled = selected == Padlocks.None || !canUseGagFeatures;
                // Draw combo
                _permActions.DrawGenericComboButton(UserPairForPerms.UserData.UID, "UnlockGagForPairPermCombo", "Unlock",
                WindowMenuWidth, Enum.GetValues<Padlocks>(), (padlock) => padlock.ToName(), false, disabled, true, Padlocks.None,
                FontAwesomeIcon.Unlock, ImGuiComboFlags.None, (selected) => { _logger.LogDebug("Selected Padlock: " + selected); },
                (onButtonPress) =>
                {
                    try
                    {
                        var newAppearance = UserPairForPerms.LastReceivedAppearanceData.DeepClone();
                        if (newAppearance == null) throw new Exception("Appearance data is null or unlock is invalid. not sending");

                        if (_permActions.PadlockVerifyUnlock<IPadlockable>(newAppearance.GagSlots[_permActions.GagLayer], onButtonPress, canUseOwnerLocks))
                        {
                            newAppearance.GagSlots[_permActions.GagLayer].Padlock = onButtonPress.ToString();
                            newAppearance.GagSlots[_permActions.GagLayer].Password = _permActions.Password;
                            newAppearance.GagSlots[_permActions.GagLayer].Timer = DateTimeOffset.UtcNow;
                            newAppearance.GagSlots[_permActions.GagLayer].Assigner = _apiController.UID;
                            DataUpdateKind updateKind = _permActions.GagLayer switch
                            {
                                0 => DataUpdateKind.AppearanceGagUnlockedLayerOne,
                                1 => DataUpdateKind.AppearanceGagUnlockedLayerTwo,
                                2 => DataUpdateKind.AppearanceGagUnlockedLayerThree,
                                _ => throw new Exception("Invalid layer selected.")
                            };
                            _ = _apiController.UserPushPairDataAppearanceUpdate(new(UserPairForPerms.UserData, newAppearance, updateKind));
                            _logger.LogDebug("Unlocking Gag with GagPadlock {0} on {1}", onButtonPress.ToString(), PairNickOrAliasOrUID);
                            ShowGagUnlock = false;
                        }
                    }
                    catch (Exception e) { _logger.LogError("Failed to push updated appearance data: " + e.Message); }
                });
                // draw password field combos.
                _permActions.DisplayPadlockFields(selected);
            }
            ImGui.Separator();
        }

        ////////// REMOVE GAG //////////
        if (_uiShared.IconTextButton(FontAwesomeIcon.TimesCircle, ("Remove " + PairUID + "'s Gag"), WindowMenuWidth, true,
        (disableRemoving || !canUseGagFeatures || disableLocking)))
        {
            ShowGagRemove = !ShowGagRemove;
        }
        UiSharedService.AttachToolTip("Remove " + PairUID + "'s Gag. Click to view options.");
        if (ShowGagRemove)
        {
            using (var actionChild = ImRaii.Child("GagRemoveChild", new Vector2(WindowMenuWidth, ImGui.GetFrameHeight()), false))
            {
                if (!actionChild) return;

                if (ImGui.Button("Remove Gag", ImGui.GetContentRegionAvail()))
                {
                    try
                    {
                        var newAppearance = UserPairForPerms.LastReceivedAppearanceData.DeepClone();
                        if (newAppearance == null) throw new Exception("Appearance data is null, not sending");
                        newAppearance.GagSlots[_permActions.GagLayer].GagType = GagType.None.GagName();
                        DataUpdateKind updateKind = _permActions.GagLayer switch
                        {
                            0 => DataUpdateKind.AppearanceGagRemovedLayerOne,
                            1 => DataUpdateKind.AppearanceGagRemovedLayerTwo,
                            2 => DataUpdateKind.AppearanceGagRemovedLayerThree,
                            _ => throw new Exception("Invalid layer selected.")
                        };
                        _ = _apiController.UserPushPairDataAppearanceUpdate(new(UserPairForPerms.UserData, newAppearance, updateKind));
                        _logger.LogDebug("Removing Gag from {0}", PairNickOrAliasOrUID);
                        ShowGagRemove = false;
                    }
                    catch (Exception e) { _logger.LogError("Failed to push updated appearance data: " + e.Message); }
                }
            }
        }
        ImGui.Separator();
    }
    #endregion GagActions

    #region WardrobeActions

    private bool ShowSetApply = false;
    private bool ShowSetLock = false;
    private bool ShowSetUnlock = false;
    private bool ShowSetRemove = false;
    private void DrawWardrobeActions()
    {
        // verify that the unique pair perms and last recieved wardrobe data are not null.
        var lastWardrobeData = UserPairForPerms.LastReceivedWardrobeData;
        var pairUniquePerms = UserPairForPerms.UserPairUniquePairPerms;
        bool canUseOwnerLocks = pairUniquePerms.OwnerLocks;
        if (lastWardrobeData == null || pairUniquePerms == null) return;

        bool applyButtonDisabled = !pairUniquePerms.ApplyRestraintSets || lastWardrobeData.OutfitNames.Count <= 0;
        bool lockButtonDisabled = !pairUniquePerms.LockRestraintSets || lastWardrobeData.ActiveSetName == string.Empty || lastWardrobeData!.Padlock != Padlocks.None.ToName();
        bool unlockButtonDisabled = !pairUniquePerms.UnlockRestraintSets || lastWardrobeData.Padlock == "None";
        bool removeButtonDisabled = !pairUniquePerms.RemoveRestraintSets || lastWardrobeData.ActiveSetName == string.Empty;

        ////////// APPLY RESTRAINT SET //////////
        if (_uiShared.IconTextButton(FontAwesomeIcon.Handcuffs, "Apply Restraint Set", WindowMenuWidth, true, applyButtonDisabled))
        {
            ShowSetApply = !ShowSetApply;
        }
        UiSharedService.AttachToolTip("Applies a Restraint Set to " + UserPairForPerms.UserData.AliasOrUID + ". Click to select set.");
        if (ShowSetApply)
        {
            using (var actionChild = ImRaii.Child("SetApplyChild", new Vector2(WindowMenuWidth, ImGui.GetFrameHeight()), false))
            {
                if (!actionChild) return;

                string storedSelectedSet = _permActions.GetSelectedItem<string>("ApplyRestraintSetForPairPermCombo", UserPairForPerms.UserData.UID) ?? string.Empty;

                _permActions.DrawGenericComboButton(UserPairForPerms.UserData.UID, "ApplyRestraintSetForPairPermCombo", "Apply Set",
                WindowMenuWidth, lastWardrobeData.OutfitNames, (RSet) => RSet, true, storedSelectedSet == string.Empty, true, default,
                FontAwesomeIcon.Female, ImGuiComboFlags.None, (selected) => { _logger.LogDebug("Selected Restraint Set: " + selected); },
                (onButtonPress) =>
                {
                    try
                    {
                        var newWardrobe = UserPairForPerms.LastReceivedWardrobeData.DeepClone();
                        if (newWardrobe == null || onButtonPress == null) throw new Exception("Wardrobe data is null, not sending");

                        newWardrobe.ActiveSetName = onButtonPress;
                        newWardrobe.ActiveSetEnabledBy = _apiController.UID;
                        _ = _apiController.UserPushPairDataWardrobeUpdate(new(UserPairForPerms.UserData, newWardrobe, DataUpdateKind.WardrobeRestraintApplied));
                        _logger.LogDebug("Applying Restraint Set with GagPadlock {0} on {1}", onButtonPress.ToString(), PairNickOrAliasOrUID);
                        ShowSetApply = false;
                    }
                    catch (Exception e) { _logger.LogError("Failed to push updated Wardrobe data: " + e.Message); }
                });
            }
            ImGui.Separator();
        }

        ////////// LOCK RESTRAINT SET //////////
        string DisplayText = lockButtonDisabled ? "Lock Restraint Set" : lastWardrobeData.Padlock + " is on this Set.";
        // push text style
        using (var color = ImRaii.PushColor(ImGuiCol.Text, (lastWardrobeData.Padlock == Padlocks.None.ToName()) ? ImGuiColors.DalamudWhite : ImGuiColors.DalamudYellow))
        {
            if (_uiShared.IconTextButton(FontAwesomeIcon.Lock, DisplayText, WindowMenuWidth, true, lockButtonDisabled))
            {
                ShowSetLock = !ShowSetLock;
            }
        }
        UiSharedService.AttachToolTip("Locks the Restraint Set applied to " + UserPairForPerms.UserData.AliasOrUID + ". Click to view options.");
        if (ShowSetLock)
        {
            Padlocks selected = _permActions.GetSelectedItem<Padlocks>("LockRestraintSetForPairPermCombo", UserPairForPerms.UserData.UID);
            float height = _permActions.ExpandLockHeightCheck(selected)
                ? 3 * ImGui.GetFrameHeight() + 2 * ImGui.GetStyle().ItemSpacing.Y
                : 2 * ImGui.GetFrameHeight() + ImGui.GetStyle().ItemSpacing.Y;

            using (var actionChild = ImRaii.Child("SetLockChild", new Vector2(WindowMenuWidth, height), false))
            {
                if (!actionChild) return;

                bool disabled = selected == Padlocks.None || !pairUniquePerms.LockRestraintSets;

                if (ImGui.BeginCombo("##DummyComboDisplayLockedSet", UserPairForPerms.LastReceivedWardrobeData?.ActiveSetName ?? "Not Set Active")) { ImGui.EndCombo(); }
                // Draw combo
                _permActions.DrawGenericComboButton(UserPairForPerms.UserData.UID, "LockRestraintSetForPairPermCombo", "Lock Set",
                WindowMenuWidth, Enum.GetValues<Padlocks>(), (padlock) => padlock.ToName(), false, disabled, true, Padlocks.None,
                FontAwesomeIcon.Lock, ImGuiComboFlags.None, (selected) => { _logger.LogDebug("Selected Padlock: " + selected); },
                (onButtonPress) =>
                {
                    try
                    {
                        var newWardrobeData = lastWardrobeData.DeepClone();
                        if (newWardrobeData == null) throw new Exception("Wardrobe data is null, not sending");

                        if (_permActions.PadlockVerifyLock<IPadlockable>(newWardrobeData, onButtonPress, pairUniquePerms.ExtendedLockTimes, canUseOwnerLocks))
                        {
                            newWardrobeData.Padlock = onButtonPress.ToName();
                            newWardrobeData.Password = _permActions.Password;
                            newWardrobeData.Timer = UiSharedService.GetEndTimeUTC(_permActions.Timer);
                            newWardrobeData.Assigner = _apiController.UID;
                            _ = _apiController.UserPushPairDataWardrobeUpdate(new(UserPairForPerms.UserData, newWardrobeData, DataUpdateKind.WardrobeRestraintLocked));
                            _logger.LogDebug("Locking Restraint Set with GagPadlock {0} on {1}", onButtonPress.ToString(), PairNickOrAliasOrUID);
                            ShowSetLock = false;
                            // reset the password and timer
                            _permActions.Password = string.Empty;
                            _permActions.Timer = string.Empty;
                        }
                    }
                    catch (Exception e) { _logger.LogError("Failed to push updated Wardrobe data: " + e.Message); }
                });
                // draw password field combos.
                _permActions.DisplayPadlockFields(selected);
            }
            ImGui.Separator();
        }

        // draw the unlock restraint set button.
        if (_uiShared.IconTextButton(FontAwesomeIcon.Unlock, "Unlock Restraint Set", WindowMenuWidth, true, unlockButtonDisabled))
        {
            ShowSetUnlock = !ShowSetUnlock;
        }
        UiSharedService.AttachToolTip("Unlocks the Restraint Set applied to " + UserPairForPerms.UserData.AliasOrUID + ". Click to view options.");
        if (ShowSetUnlock)
        {
            Padlocks selected = _permActions.GetSelectedItem<Padlocks>("UnlockRestraintSetForPairPermCombo", UserPairForPerms.UserData.UID);
            float height = _permActions.ExpandLockHeightCheck(selected)
                ? 3 * ImGui.GetFrameHeight() + 2 * ImGui.GetStyle().ItemSpacing.Y
                : 2 * ImGui.GetFrameHeight() + ImGui.GetStyle().ItemSpacing.Y;
            using (var actionChild = ImRaii.Child("SetUnlockChild", new Vector2(WindowMenuWidth, height), false))
            {
                if (!actionChild) return;

                bool disabled = selected == Padlocks.None || !pairUniquePerms.UnlockRestraintSets;

                if (ImGui.BeginCombo("##DummyComboDisplayLockedSet", UserPairForPerms.LastReceivedWardrobeData?.ActiveSetName ?? "Not Set Active")) { ImGui.EndCombo(); }
                // Draw combo
                _permActions.DrawGenericComboButton(UserPairForPerms.UserData.UID, "UnlockRestraintSetForPairPermCombo", "Unlock Set",
                WindowMenuWidth, Enum.GetValues<Padlocks>(), (padlock) => padlock.ToName(), false, disabled, true, Padlocks.None,
                FontAwesomeIcon.Unlock, ImGuiComboFlags.None, (selected) => { _logger.LogDebug("Selected Padlock: " + selected); },
                (onButtonPress) =>
                {
                    try
                    {
                        var newWardrobeData = lastWardrobeData.DeepClone();
                        if (newWardrobeData == null) throw new Exception("Wardrobe data is null, not sending");

                        if (_permActions.PadlockVerifyUnlock<IPadlockable>(newWardrobeData, onButtonPress, canUseOwnerLocks))
                        {
                            newWardrobeData.Padlock = onButtonPress.ToName();
                            newWardrobeData.Password = _permActions.Password;
                            newWardrobeData.Timer = DateTimeOffset.UtcNow;
                            newWardrobeData.Assigner = _apiController.UID;
                            _ = _apiController.UserPushPairDataWardrobeUpdate(new(UserPairForPerms.UserData, newWardrobeData, DataUpdateKind.WardrobeRestraintUnlocked));
                            _logger.LogDebug("Unlocking Restraint Set with GagPadlock {0} on {1}", onButtonPress.ToName(), PairNickOrAliasOrUID);
                            ShowSetUnlock = false;
                            // reset the password and timer
                            _permActions.Password = string.Empty;
                            _permActions.Timer = string.Empty;
                        }
                    }
                    catch (Exception e) { _logger.LogError("Failed to push updated Wardrobe data: " + e.Message); }
                });
                // draw password field combos.
                _permActions.DisplayPadlockFields(selected);
            }
            ImGui.Separator();
        }

        // draw the remove restraint set button.
        if (_uiShared.IconTextButton(FontAwesomeIcon.TimesCircle, "Remove Restraint Set", WindowMenuWidth, true, removeButtonDisabled))
        {
            ShowSetRemove = !ShowSetRemove;
        }
        UiSharedService.AttachToolTip("Removes the Restraint Set applied to " + UserPairForPerms.UserData.AliasOrUID + ". Click to view options.");
        if (ShowSetRemove)
        {
            using (var actionChild = ImRaii.Child("SetRemoveChild", new Vector2(WindowMenuWidth, ImGui.GetFrameHeightWithSpacing()), false))
            {
                if (!actionChild) return;

                if (ImGui.Button("Remove Gag", ImGui.GetContentRegionAvail()))
                {
                    try
                    {
                        var newWardrobeData = lastWardrobeData.DeepClone();
                        if (newWardrobeData == null) throw new Exception("Wardrobe data is null, not sending");
                        newWardrobeData.ActiveSetName = string.Empty;
                        newWardrobeData.ActiveSetEnabledBy = string.Empty;
                        _ = _apiController.UserPushPairDataWardrobeUpdate(new(UserPairForPerms.UserData, newWardrobeData, DataUpdateKind.WardrobeRestraintDisabled));
                        _logger.LogDebug("Removing Restraint Set from {0}", PairNickOrAliasOrUID);
                        ShowSetRemove = false;
                    }
                    catch (Exception e) { _logger.LogError("Failed to push updated Wardrobe data: " + e.Message); }
                }
            }
        }
        ImGui.Separator();
    }
    #endregion WardrobeActions

    #region PuppeteerActions
    private void DrawPuppeteerActions()
    {
        // draw the Alias List popout ref button. (opens a popout window 
        if (_uiShared.IconTextButton(FontAwesomeIcon.Sync, "Update " + PairUID + " with your Name", WindowMenuWidth, true))
        {
            var name = _frameworkUtils.GetPlayerNameAsync().GetAwaiter().GetResult();
            var world = _frameworkUtils.GetHomeWorldIdAsync().GetAwaiter().GetResult();
            var worldName = _uiShared.WorldData[(ushort)world];
            // compile the alias data to send including our own name and world information, along with an empty alias list.
            var dataToPush = new CharacterAliasData()
            {
                CharacterName = name,
                CharacterWorld = worldName,
                AliasList = new List<AliasTrigger>()
            };

            _ = _apiController.UserPushPairDataAliasStorageUpdate(new OnlineUserCharaAliasDataDto
                (UserPairForPerms.UserData, dataToPush, DataUpdateKind.PuppeteerPlayerNameRegistered));
            _logger.LogDebug("Sent Puppeteer Name to " + UserPairForPerms.UserData.AliasOrUID);
        }
        UiSharedService.AttachToolTip("Sends your Name & World to this pair so their puppeteer will listen for messages from you.");
        ImGui.Separator();
    }
    #endregion PuppeteerActions

    #region MoodlesActions
    // All of these actions will only display relative to the various filters that the Moodle has applied.
    private bool ShowApplyPairMoodles = false;
    private bool ShowApplyOwnMoodles = false;
    private bool ShowApplyPairPresets = false;
    private bool ShowApplyOwnPresets = false;
    private bool ShowRemoveMoodles = false;
    private bool ShowClearMoodles = false;
    private void DrawMoodlesActions()
    {
        var lastIpcData = UserPairForPerms.LastReceivedIpcData;
        var pairUniquePerms = UserPairForPerms.UserPairUniquePairPerms;
        if (lastIpcData == null || pairUniquePerms == null) return;

        bool ApplyPairsMoodleToPairDisabled = !pairUniquePerms.PairCanApplyYourMoodlesToYou || lastIpcData.MoodlesStatuses.Count <= 0;
        bool ApplyOwnMoodleToPairDisabled = !pairUniquePerms.PairCanApplyOwnMoodlesToYou || LastCreatedCharacterData == null || LastCreatedCharacterData.MoodlesStatuses.Count <= 0;
        bool RemovePairsMoodlesDisabled = !pairUniquePerms.AllowRemovingMoodles || lastIpcData.MoodlesDataStatuses.Count <= 0;
        bool ClearPairsMoodlesDisabled = !pairUniquePerms.AllowRemovingMoodles || lastIpcData.MoodlesData == string.Empty;

        ////////// APPLY MOODLES FROM PAIR's LIST //////////
        if (_uiShared.IconTextButton(FontAwesomeIcon.PersonCirclePlus, "Apply a Moodle from their list", WindowMenuWidth, true, ApplyPairsMoodleToPairDisabled))
        {
            ShowApplyPairMoodles = !ShowApplyPairMoodles;
        }
        UiSharedService.AttachToolTip("Applies a Moodle from " + UserPairForPerms.UserData.AliasOrUID + "'s Moodles List to them.");
        if (ShowApplyPairMoodles)
        {
            using (var child = ImRaii.Child("ApplyPairMoodles", new Vector2(WindowMenuWidth, ImGui.GetFrameHeightWithSpacing()), false))
            {
                if (!child) return;
                float buttonWidth = WindowMenuWidth - ImGui.CalcTextSize("Apply").X - ImGui.GetStyle().ItemInnerSpacing.X - ImGui.GetStyle().ItemSpacing.X;

                if (UserPairForPerms.LastReceivedIpcData != null)
                {
                    MoodlesHelpers.DrawPairStatusSelection(UserPairForPerms.LastReceivedIpcData.MoodlesStatuses, buttonWidth, PairUID, PairNickOrAliasOrUID, _moodlesService, _logger);
                    ImUtf8.SameLineInner();
                    MoodlesHelpers.ApplyPairStatusButton(UserPairForPerms, _apiController, _logger, _frameworkUtils, _uiShared, out bool success);
                    if (success) ShowApplyPairMoodles = false;
                }
                else
                {
                    UiSharedService.ColorText(PairUID + " has no Moodles to apply.", ImGuiColors.ParsedOrange);
                }
            }
            ImGui.Separator();
        }

        ////////// APPLY PRESETS FROM PAIR's LIST //////////
        if (_uiShared.IconTextButton(FontAwesomeIcon.FileCirclePlus, "Apply a Preset from their list", WindowMenuWidth, true, ApplyPairsMoodleToPairDisabled))
        {
            ShowApplyPairPresets = !ShowApplyPairPresets;
        }
        UiSharedService.AttachToolTip("Applies a Preset from " + UserPairForPerms.UserData.AliasOrUID + "'s Presets List to them.");
        if (ShowApplyPairPresets)
        {
            using (var child = ImRaii.Child("ApplyPairPresets", new Vector2(WindowMenuWidth, ImGui.GetFrameHeightWithSpacing()), false))
            {
                if (!child) return;

                float buttonWidth = WindowMenuWidth - ImGui.CalcTextSize("Apply").X - ImGui.GetStyle().ItemInnerSpacing.X - ImGui.GetStyle().ItemSpacing.X;

                if (UserPairForPerms.LastReceivedIpcData != null)
                {
                    MoodlesHelpers.DrawPairPresetSelection(UserPairForPerms.LastReceivedIpcData, buttonWidth, PairUID, PairNickOrAliasOrUID, _uiShared, _logger);
                    ImUtf8.SameLineInner();
                    // validate the apply button
                    MoodlesHelpers.ApplyPairPresetButton(UserPairForPerms, _apiController, _logger, _frameworkUtils, _uiShared, out bool success);
                    if (success) ShowApplyPairPresets = false;
                }
                else
                {
                    UiSharedService.ColorText(PairUID + " has no Presets to apply.", ImGuiColors.ParsedOrange);
                }
            }
            ImGui.Separator();
        }

        ////////// APPLY MOODLES FROM OWN LIST //////////
        if (_uiShared.IconTextButton(FontAwesomeIcon.UserPlus, "Apply a Moodle from your list", WindowMenuWidth, true, ApplyOwnMoodleToPairDisabled))
        {
            ShowApplyOwnMoodles = !ShowApplyOwnMoodles;
        }
        UiSharedService.AttachToolTip("Applies a Moodle from your Moodles List to " + PairUID + ".");
        if (ShowApplyOwnMoodles)
        {
            using (var child = ImRaii.Child("ApplyOwnMoodles", new Vector2(WindowMenuWidth, ImGui.GetFrameHeightWithSpacing()), false))
            {
                if (!child) return;

                float buttonWidth = WindowMenuWidth - ImGui.CalcTextSize("Apply").X - ImGui.GetStyle().ItemInnerSpacing.X - ImGui.GetStyle().ItemSpacing.X;

                if (LastCreatedCharacterData != null)
                {
                    MoodlesHelpers.DrawOwnStatusSelection(LastCreatedCharacterData.MoodlesStatuses, buttonWidth, PairUID, PairNickOrAliasOrUID, _moodlesService, _logger);
                    ImUtf8.SameLineInner();
                    // validate the apply button
                    MoodlesHelpers.ApplyOwnStatusButton(UserPairForPerms, _apiController, _logger, _frameworkUtils, _uiShared, LastCreatedCharacterData, PairNickOrAliasOrUID, out bool success);
                    if (success) ShowApplyOwnMoodles = false;
                }
                else
                {
                    UiSharedService.ColorText("You have no Moodles to apply.", ImGuiColors.ParsedOrange);
                }
            }
            ImGui.Separator();
        }

        ////////// APPLY PRESETS FROM OWN LIST //////////
        if (_uiShared.IconTextButton(FontAwesomeIcon.FileCirclePlus, "Apply a Preset from your list", WindowMenuWidth, true, ApplyOwnMoodleToPairDisabled))
        {
            ShowApplyOwnPresets = !ShowApplyOwnPresets;
        }
        UiSharedService.AttachToolTip("Applies a Preset from your Presets List to " + PairUID + ".");

        if (ShowApplyOwnPresets)
        {
            using (var child = ImRaii.Child("ApplyOwnPresets", new Vector2(WindowMenuWidth, ImGui.GetFrameHeightWithSpacing()), false))
            {
                if (!child) return;

                float buttonWidth = WindowMenuWidth - ImGui.CalcTextSize("Apply").X - ImGui.GetStyle().ItemInnerSpacing.X - ImGui.GetStyle().ItemSpacing.X;

                if (LastCreatedCharacterData != null)
                {
                    MoodlesHelpers.DrawOwnPresetSelection(LastCreatedCharacterData, buttonWidth, PairUID, PairNickOrAliasOrUID, _uiShared, _logger);
                    ImUtf8.SameLineInner();
                    ImUtf8.SameLineInner();
                    // validate the apply button
                    MoodlesHelpers.ApplyOwnPresetButton(UserPairForPerms, _apiController, _logger, _frameworkUtils, _uiShared, LastCreatedCharacterData, PairNickOrAliasOrUID, out bool success);
                    if (success) ShowApplyOwnPresets = false;
                }
                else
                {
                    UiSharedService.ColorText("You have no Presets to apply.", ImGuiColors.ParsedOrange);
                }
            }
            ImGui.Separator();
        }


        ////////// REMOVE MOODLES //////////
        if (_uiShared.IconTextButton(FontAwesomeIcon.UserMinus, "Remove a Moodle from " + PairUID, WindowMenuWidth, true, RemovePairsMoodlesDisabled))
        {
            ShowRemoveMoodles = !ShowRemoveMoodles;
        }
        UiSharedService.AttachToolTip("Removes a Moodle from " + PairUID + "'s Statuses.");
        if (ShowRemoveMoodles)
        {
            using (var child = ImRaii.Child("RemoveMoodles", new Vector2(WindowMenuWidth, ImGui.GetFrameHeightWithSpacing()), false))
            {
                if (!child) return;

                float buttonWidth = WindowMenuWidth - ImGui.CalcTextSize("Remove").X - ImGui.GetStyle().ItemInnerSpacing.X - ImGui.GetStyle().ItemSpacing.X;

                MoodlesHelpers.DrawPairActiveStatusSelection(UserPairForPerms.LastReceivedIpcData!.MoodlesDataStatuses, buttonWidth, PairUID, PairNickOrAliasOrUID, _moodlesService, _logger);
                ImUtf8.SameLineInner();
                // validate the apply button
                MoodlesHelpers.RemoveMoodleButton(UserPairForPerms, _apiController, _logger, _frameworkUtils, _uiShared, out bool success);
                if (success) ShowRemoveMoodles = false;
            }
            ImGui.Separator();
        }

        ////////// CLEAR MOODLES //////////
        if (_uiShared.IconTextButton(FontAwesomeIcon.UserSlash, "Clear all Moodles from " + PairUID, WindowMenuWidth, true, ClearPairsMoodlesDisabled))
        {
            ShowClearMoodles = !ShowClearMoodles;
        }
        UiSharedService.AttachToolTip("Clears all Moodles from " + PairUID + "'s Statuses.");

        if (ShowClearMoodles)
        {
            using (var child = ImRaii.Child("ClearMoodles", new Vector2(WindowMenuWidth, ImGui.GetFrameHeightWithSpacing()), false))
            {
                if (!child) return;
                MoodlesHelpers.ClearMoodlesButton(UserPairForPerms, _apiController, _frameworkUtils, _uiShared, WindowMenuWidth, out bool success);
                if (success) ShowClearMoodles = false;
            }
        }
        ImGui.Separator();
    }
    #endregion MoodlesActions

    #region ToyboxActions
    // toggle toy is an instant button press.
    // open vibe remote is a instant button but only enabled when both ends are connected.
    private bool ShowPatternExecute = false;
    private bool ShowAlarmToggle = false;
    private bool ShowTriggerToggle = false;
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
        var disableAlarms = !pairUniquePerms.CanToggleAlarms;
        if (_uiShared.IconTextButton(FontAwesomeIcon.Clock, "Toggle " + PairUID + "'s Alarms", WindowMenuWidth, true, disableAlarms))
        {
            ShowAlarmToggle = !ShowAlarmToggle;
        }
        UiSharedService.AttachToolTip("Toggle " + PairUID + "'s Alarms.");
        if (ShowPatternExecute)
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
                        ShowAlarmToggle = false;
                    }
                    catch (Exception e) { _logger.LogError("Failed to push updated ToyboxPattern data: " + e.Message); }
                });
            }
            ImGui.Separator();
        }

        ////////// EXECUTE PATTERN ON PAIR'S TOY //////////
        var disablePatternButton = !pairUniquePerms.CanExecutePatterns || !UserPairForPerms.UserPairGlobalPerms.ToyIsActive;
        if (_uiShared.IconTextButton(FontAwesomeIcon.PlayCircle, ("Activate " + PairUID + "'s Patterns"), WindowMenuWidth, true, disablePatternButton))
        {
            ShowPatternExecute = !ShowPatternExecute;
        }
        UiSharedService.AttachToolTip("Play one of " + PairUID + "'s patterns to their active Toy.");
        if (ShowPatternExecute)
        {
            using (var actionChild = ImRaii.Child("PatternExecuteChild", new Vector2(WindowMenuWidth, ImGui.GetFrameHeight()), false))
            {
                if (!actionChild) return;

                PatternInfo storedPatternName = _permActions.GetSelectedItem<PatternInfo>("ExecutePatternForPairPermCombo", UserPairForPerms.UserData.UID) ?? new PatternInfo();
                bool disabledCondition = storedPatternName.Identifier == Guid.Empty || !lastToyboxData.PatternList.Any();

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
                        ShowPatternExecute = false;
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
            ShowTriggerToggle = !ShowTriggerToggle;
        }
        UiSharedService.AttachToolTip("Toggle the state of a trigger in " + PairUID + "'s triggerList.");
        if (ShowTriggerToggle)
        {
            using (var actionChild = ImRaii.Child("TriggerToggleChild", new Vector2(WindowMenuWidth, ImGui.GetFrameHeight()), false))
            {
                if (!actionChild) return;

                TriggerInfo selected = _permActions.GetSelectedItem<TriggerInfo>("ToggleTriggerForPairPermCombo", UserPairForPerms.UserData.UID) ?? new TriggerInfo();
                bool disabled = selected.Identifier == Guid.Empty || !lastToyboxData.TriggerList.Any();
                string buttonText = selected.Identifier == Guid.Empty ? "Enable Trigger" : "Disable Trigger";

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
                        var triggerToToggle = newToyboxData.TriggerList.IndexOf(onButtonPress);
                        if (triggerToToggle == -1) throw new Exception("Trigger not found in list.");

                        // toggle the alarm state.
                        newToyboxData.TriggerList[triggerToToggle].Enabled = !newToyboxData.TriggerList[triggerToToggle].Enabled;

                        _ = _apiController.UserPushPairDataToyboxUpdate(new(UserPairForPerms.UserData, newToyboxData, DataUpdateKind.ToyboxTriggerToggled));
                        _logger.LogDebug("Toggling Trigger {0} on {1}'s TriggerList", onButtonPress.Name, PairNickOrAliasOrUID);
                        ShowTriggerToggle = false;
                    }
                    catch (Exception e) { _logger.LogError("Failed to push updated ToyboxPattern data: " + e.Message); }
                });
            }
            ImGui.Separator();
        }
        ImGui.Separator();
    }

    private void DrawHardcoreActions()
    {
        // conditions for disabled actions
        bool playerTargeted = _clientState.LocalPlayer != null && _clientState.LocalPlayer.TargetObject != null;
        bool playerCloseEnough = playerTargeted && Vector3.Distance(_clientState.LocalPlayer?.Position ?? default, _clientState.LocalPlayer?.TargetObject?.Position ?? default) < 3;

        var forceFollowIcon = UserPairForPerms.UserPair!.OtherPairPerms.IsForcedToFollow ? FontAwesomeIcon.StopCircle : FontAwesomeIcon.PersonWalkingArrowRight;
        var forceFollowText = UserPairForPerms.UserPair!.OtherPairPerms.IsForcedToFollow ? $"Have {PairNickOrAliasOrUID} stop following you." : $"Make {PairNickOrAliasOrUID} follow you.";
        bool disableForceFollow = !playerCloseEnough || !playerTargeted || !UserPairForPerms.UserPairUniquePairPerms.AllowForcedFollow || !UserPairForPerms.IsVisible;
        if (_uiShared.IconTextButton(forceFollowIcon, forceFollowText, WindowMenuWidth, true, disableForceFollow))
        {
            var perm = UserPairForPerms.UserPair!.OtherPairPerms;
            _ = _apiController.UserUpdateOtherPairPerm(new UserPairPermChangeDto(UserPairForPerms.UserData, new KeyValuePair<string, object>("IsForcedToFollow", !perm.IsForcedToFollow)));
        }

        var forceSitIcon = UserPairForPerms.UserPair!.OtherPairPerms.IsForcedToSit ? FontAwesomeIcon.StopCircle : FontAwesomeIcon.Chair;
        var forceSitText = UserPairForPerms.UserPair!.OtherPairPerms.IsForcedToSit ? $"Let {PairNickOrAliasOrUID} stand again." : $"Force {PairNickOrAliasOrUID} to sit.";
        bool disableForceSit = !UserPairForPerms.UserPairUniquePairPerms.AllowForcedSit || UserPairForPerms.UserPairOwnUniquePairPerms.IsForcedToGroundSit;
        if (_uiShared.IconTextButton(forceSitIcon, forceSitText, WindowMenuWidth, true, disableForceSit))
        {
            var perm = UserPairForPerms.UserPair!.OtherPairPerms;
            _ = _apiController.UserUpdateOtherPairPerm(new UserPairPermChangeDto(UserPairForPerms.UserData, new KeyValuePair<string, object>("IsForcedToSit", !perm.IsForcedToSit)));
        }

        var forceGroundSitIcon = UserPairForPerms.UserPair!.OtherPairPerms.IsForcedToGroundSit ? FontAwesomeIcon.StopCircle : FontAwesomeIcon.Chair;
        var forceGroundSitText = UserPairForPerms.UserPair!.OtherPairPerms.IsForcedToGroundSit ? $"Let {PairNickOrAliasOrUID} stand again." : $"Force {PairNickOrAliasOrUID} to their knees.";
        bool disableForceGroundSit = !UserPairForPerms.UserPairUniquePairPerms.AllowForcedSit || UserPairForPerms.UserPairOwnUniquePairPerms.IsForcedToSit;
        if (_uiShared.IconTextButton(forceGroundSitIcon, forceGroundSitText, WindowMenuWidth, true, disableForceGroundSit))
        {
            var perm = UserPairForPerms.UserPair!.OtherPairPerms;
            _ = _apiController.UserUpdateOtherPairPerm(new UserPairPermChangeDto(UserPairForPerms.UserData, new KeyValuePair<string, object>("IsForcedToGroundSit", !perm.IsForcedToGroundSit)));
        }

        var forceToStayIcon = UserPairForPerms.UserPair!.OtherPairPerms.IsForcedToStay ? FontAwesomeIcon.StopCircle : FontAwesomeIcon.HouseLock;
        var forceToStayText = UserPairForPerms.UserPair!.OtherPairPerms.IsForcedToStay ? $"Release {PairNickOrAliasOrUID}." : $"Lock away {PairNickOrAliasOrUID}.";
        bool disableForceToStay = !UserPairForPerms.UserPairUniquePairPerms.AllowForcedToStay;
        if (_uiShared.IconTextButton(forceToStayIcon, forceToStayText, WindowMenuWidth, true, disableForceToStay))
        {
            var perm = UserPairForPerms.UserPair!.OtherPairPerms;
            _ = _apiController.UserUpdateOtherPairPerm(new UserPairPermChangeDto(UserPairForPerms.UserData, new KeyValuePair<string, object>("IsForcedToStay", !perm.IsForcedToStay)));
        }

        var toggleBlindfoldIcon = UserPairForPerms.UserPair!.OtherPairPerms.IsBlindfolded ? FontAwesomeIcon.StopCircle : FontAwesomeIcon.Mask;
        var toggleBlindfoldText = UserPairForPerms.UserPair!.OtherPairPerms.IsBlindfolded ? $"Remove {PairNickOrAliasOrUID}'s Blindfold." : $"Blindfold {PairNickOrAliasOrUID}.";
        bool disableBlindfoldToggle = !UserPairForPerms.UserPairUniquePairPerms.AllowBlindfold;
        if (_uiShared.IconTextButton(toggleBlindfoldIcon, toggleBlindfoldText, WindowMenuWidth, true, disableBlindfoldToggle))
        {
            var perm = UserPairForPerms.UserPair!.OtherPairPerms;
            _ = _apiController.UserUpdateOtherPairPerm(new UserPairPermChangeDto(UserPairForPerms.UserData, new KeyValuePair<string, object>("IsBlindfolded", !perm.IsBlindfolded)));
        }
        ImGui.Separator();
    }

    private bool ShowShockAction = false;
    private bool ShowVibrateAction = false;
    private bool ShowBeepAction = false;
    private int Intensity = 0;
    private int VibrateIntensity = 0;
    private float Duration = 0;
    private float VibeDuration = 0;
    private void DrawHardcoreShockCollarActions()
    {
        // the permissions to reference.
        PiShockPermissions permissions = (UserPairForPerms.LastPairPiShockPermsForYou.MaxIntensity != -1) ? UserPairForPerms.LastPairPiShockPermsForYou : UserPairForPerms.LastPairGlobalShockPerms;
        TimeSpan maxVibeDuration = (UserPairForPerms.LastPairPiShockPermsForYou.MaxIntensity != -1) ? UserPairForPerms.UserPairUniquePairPerms.MaxVibrateDuration : UserPairForPerms.UserPairGlobalPerms.GlobalShockVibrateDuration;
        string piShockShareCodePref = (UserPairForPerms.LastPairPiShockPermsForYou.MaxIntensity != -1) ? UserPairForPerms.UserPairUniquePairPerms.ShockCollarShareCode : UserPairForPerms.UserPairGlobalPerms.GlobalShockShareCode;

        if (_uiShared.IconTextButton(FontAwesomeIcon.BoltLightning, "Shock " + PairNickOrAliasOrUID + "'s Shock Collar", WindowMenuWidth, true, !permissions.AllowShocks))
        {
            ShowShockAction = !ShowShockAction;
        }
        UiSharedService.AttachToolTip("Perform a Shock action to " + PairUID + "'s Shock Collar.");

        if (ShowShockAction)
        {
            using (var actionChild = ImRaii.Child("ShockCollarActionChild", new Vector2(WindowMenuWidth, ImGui.GetFrameHeight() * 2 + ImGui.GetStyle().ItemSpacing.Y), false))
            {
                if (!actionChild) return;

                var width = WindowMenuWidth - ImGuiHelpers.GetButtonSize("Send Shock").X - ImGui.GetStyle().ItemInnerSpacing.X;

                ImGui.SetNextItemWidth(WindowMenuWidth);
                ImGui.SliderInt("##IntensitySliderRef" + PairNickOrAliasOrUID, ref Intensity, 0, permissions.MaxIntensity, "%d%%", ImGuiSliderFlags.None);
                ImGui.SetNextItemWidth(width);
                ImGui.SliderFloat("##DurationSliderRef" + PairNickOrAliasOrUID, ref Duration, 0.0f, ((float)permissions.GetTimespanFromDuration().TotalMilliseconds / 1000f), "%.1fs", ImGuiSliderFlags.None);
                ImUtf8.SameLineInner();
                try
                {
                    if (ImGui.Button("Send Shock##SendShockToShockCollar" + PairNickOrAliasOrUID))
                    {
                        int newMaxDuration;
                        if (Duration % 1 == 0 && Duration >= 1 && Duration <= 15) { newMaxDuration = (int)Duration; }
                        else { newMaxDuration = (int)(Duration * 1000); }

                        _logger.LogDebug("Sending Shock to Shock Collar with duration: " + newMaxDuration + "(milliseconds)");
                        _ = _apiController.UserShockActionOnPair(new ShockCollarActionDto(UserPairForPerms.UserData, 0, Intensity, newMaxDuration));
                    }
                }
                catch (Exception e) { _logger.LogError("Failed to push ShockCollar Shock message: " + e.Message); }
            }
            ImGui.Separator();
        }

        if (_uiShared.IconTextButton(FontAwesomeIcon.WaveSquare, "Vibrate " + PairNickOrAliasOrUID + "'s Shock Collar", WindowMenuWidth, true, false))
        {
            ShowVibrateAction = !ShowVibrateAction;
        }
        UiSharedService.AttachToolTip("Perform a Vibrate action to " + PairUID + "'s Shock Collar.");

        if (ShowVibrateAction)
        {
            using (var actionChild = ImRaii.Child("VibrateCollarActionChild", new Vector2(WindowMenuWidth, ImGui.GetFrameHeight() * 2 + ImGui.GetStyle().ItemSpacing.Y), false))
            {
                if (!actionChild) return;

                var width = WindowMenuWidth - ImGuiHelpers.GetButtonSize("Send Vibration").X - ImGui.GetStyle().ItemInnerSpacing.X;

                // draw a slider float that references the duration, going from 0.1f to 15f by a scaler of 0.1f that displays X.Xs
                ImGui.SetNextItemWidth(WindowMenuWidth);
                ImGui.SliderInt("##IntensitySliderRef" + PairNickOrAliasOrUID, ref VibrateIntensity, 0, 100, "%d%%", ImGuiSliderFlags.None);
                ImGui.SetNextItemWidth(width);
                ImGui.SliderFloat("##DurationSliderRef" + PairNickOrAliasOrUID, ref VibeDuration, 0.0f, ((float)maxVibeDuration.TotalMilliseconds / 1000f), "%.1fs", ImGuiSliderFlags.None);
                ImUtf8.SameLineInner();
                try
                {
                    if (ImGui.Button("Send Vibration##SendVibrationToShockCollar" + PairNickOrAliasOrUID))
                    {
                        int newMaxDuration;
                        if (VibeDuration % 1 == 0 && VibeDuration >= 1 && VibeDuration <= 15) { newMaxDuration = (int)VibeDuration; }
                        else { newMaxDuration = (int)(VibeDuration * 1000); }

                        _logger.LogDebug("Sending Vibration to Shock Collar with duration: " + newMaxDuration + "(milliseconds)");
                        _ = _apiController.UserShockActionOnPair(new ShockCollarActionDto(UserPairForPerms.UserData, 1, VibrateIntensity, newMaxDuration));
                    }
                }
                catch (Exception e) { _logger.LogError("Failed to push ShockCollar Vibrate message: " + e.Message); }
            }
            ImGui.Separator();
        }

        if (_uiShared.IconTextButton(FontAwesomeIcon.LandMineOn, "Beep " + PairNickOrAliasOrUID + "'s Shock Collar", WindowMenuWidth, true, !permissions.AllowBeeps))
        {
            ShowBeepAction = !ShowBeepAction;
        }
        UiSharedService.AttachToolTip("Beep " + PairUID + "'s Shock Collar.");

        if (ShowBeepAction)
        {
            using (var actionChild = ImRaii.Child("BeepCollarActionChild", new Vector2(WindowMenuWidth, ImGui.GetFrameHeight()), false))
            {
                if (!actionChild) return;

                var width = WindowMenuWidth - ImGuiHelpers.GetButtonSize("Send Beep").X - ImGui.GetStyle().ItemInnerSpacing.X;

                // draw a slider float that references the duration, going from 0.1f to 15f by a scaler of 0.1f that displays X.Xs
                ImGui.SetNextItemWidth(width);
                ImGui.SliderFloat("##DurationSliderRef" + PairNickOrAliasOrUID, ref VibeDuration, 0.1f, ((float)maxVibeDuration.TotalMilliseconds / 1000f), "%.1fs", ImGuiSliderFlags.None);
                ImUtf8.SameLineInner();
                try
                {
                    if (ImGui.Button("Send Beep##SendBeepToShockCollar" + PairNickOrAliasOrUID))
                    {
                        int newMaxDuration;
                        if (VibeDuration % 1 == 0 && VibeDuration >= 1 && VibeDuration <= 15) { newMaxDuration = (int)VibeDuration; }
                        else { newMaxDuration = (int)(VibeDuration * 1000); }
                        _logger.LogDebug("Sending Beep to Shock Collar with duration: " + newMaxDuration + "(note that values between 1 and 15 are full seconds)");
                        _ = _apiController.UserShockActionOnPair(new ShockCollarActionDto(UserPairForPerms.UserData, 2, Intensity, newMaxDuration));
                    }
                }
                catch (Exception e) { _logger.LogError("Failed to push ShockCollar Beep message: " + e.Message); }
            }
            ImGui.Separator();
        }



    }

    #endregion ToyboxActions

    private void DrawIndividualMenu()
    {
        var entryUID = UserPairForPerms.UserData.AliasOrUID;

        if (UserPairForPerms.IndividualPairStatus != GagspeakAPI.Data.Enum.IndividualPairStatus.None)
        {
            if (_uiShared.IconTextButton(FontAwesomeIcon.Trash, "Unpair Permanently", WindowMenuWidth, true) && UiSharedService.CtrlPressed())
            {
                _ = _apiController.UserRemovePair(new(UserPairForPerms.UserData));
            }
            UiSharedService.AttachToolTip("Hold CTRL and click to unpair permanently from " + entryUID);
        }
        else
        {
            if (_uiShared.IconTextButton(FontAwesomeIcon.Plus, "Pair individually", WindowMenuWidth, true))
            {
                _ = _apiController.UserAddPair(new(UserPairForPerms.UserData));
            }
            UiSharedService.AttachToolTip("Pair individually with " + entryUID);
        }
    }

}
