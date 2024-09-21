using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using GagSpeak.WebAPI.Utils;
using GagspeakAPI.Data.Interfaces;
using GagspeakAPI.Enums;
using GagspeakAPI.Extensions;
using ImGuiNET;
using OtterGui.Text;
using System.Numerics;

namespace GagSpeak.UI.Permissions;

/// <summary>
/// Contains functions relative to the paired users permissions for the client user.
/// 
/// Yes its messy, yet it's long, but i functionalized it best i could for the insane 
/// amount of logic being performed without adding too much overhead.
/// </summary>
public partial class PairStickyUI
{
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
            Opened = Opened == ActiveActionButton.ApplyGag ? ActiveActionButton.None : ActiveActionButton.ApplyGag;
        }
        UiSharedService.AttachToolTip("Apply a Gag to " + PairUID + ". Click to view options.");
        if (Opened is ActiveActionButton.ApplyGag)
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
                        Opened = ActiveActionButton.None;
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
                : "Locked with a " + UserPairForPerms.LastReceivedAppearanceData!.GagSlots[_permActions.GagLayer].Padlock;
            if (_uiShared.IconTextButton(FontAwesomeIcon.Lock, DisplayText, WindowMenuWidth, true, disableLocking || (!disableLocking && !disableUnlocking) || !canUseGagFeatures))
            {
                Opened = Opened == ActiveActionButton.LockGag ? ActiveActionButton.None : ActiveActionButton.LockGag;
            }
            UiSharedService.AttachToolTip("Lock " + PairUID + "'s Gag. Click to view options.");
        }
        if (Opened is ActiveActionButton.LockGag)
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
                            newAppearance.GagSlots[_permActions.GagLayer].Padlock = onButtonPress.ToName();
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
                            _logger.LogDebug("Locking Gag with GagPadlock {0} on {1}", onButtonPress.ToName(), PairNickOrAliasOrUID);
                            Opened = ActiveActionButton.None;
                        }
                        catch (Exception e) { _logger.LogError("Failed to push updated appearance data: " + e.Message); }
                    }
                    _permActions.ResetInputs();
                });
                // draw password field combos.
                _permActions.DisplayPadlockFields(selected);
            }
            ImGui.Separator();
        }

        ////////// UNLOCK GAG //////////
        if (_uiShared.IconTextButton(FontAwesomeIcon.Unlock, ("Unlock " + PairUID + "'s Gag"), WindowMenuWidth, true, disableUnlocking || !canUseGagFeatures))
        {
            Opened = Opened == ActiveActionButton.UnlockGag ? ActiveActionButton.None : ActiveActionButton.UnlockGag;
        }
        UiSharedService.AttachToolTip("Unlock " + PairUID + "'s Gag. Click to view options.");
        if (Opened is ActiveActionButton.UnlockGag)
        {
            Padlocks selected = UserPairForPerms.LastReceivedAppearanceData.GagSlots[_permActions.GagLayer].Padlock.ToPadlock();
            float height = _permActions.ExpandLockHeightCheck(selected) ? ImGui.GetFrameHeight() * 2 + ImGui.GetStyle().ItemSpacing.Y : ImGui.GetFrameHeight();
            using (var actionChild = ImRaii.Child("GagUnlockChild", new Vector2(WindowMenuWidth, height), false))
            {
                if (!actionChild) return;

                bool disabled = selected == Padlocks.None || !canUseGagFeatures;
                // Draw combo
                float width = WindowMenuWidth - ImGui.GetStyle().ItemInnerSpacing.X - _uiShared.GetIconTextButtonSize(FontAwesomeIcon.Unlock, "Unlock");
                ImGui.SetNextItemWidth(width);

                using (ImRaii.Disabled(true))
                {
                    if (ImGui.BeginCombo("##DummyComboDisplayLockedSet", UserPairForPerms.LastReceivedAppearanceData.GagSlots[_permActions.GagLayer].Padlock ?? "Not Lock Active")) { ImGui.EndCombo(); }
                }

                ImUtf8.SameLineInner();
                if (_uiShared.IconTextButton(FontAwesomeIcon.Unlock, "Unlock", ImGui.GetContentRegionAvail().X, false, disabled))
                {
                    try
                    {
                        var newAppearance = UserPairForPerms.LastReceivedAppearanceData.DeepClone();
                        if (newAppearance == null) throw new Exception("Appearance data is null or unlock is invalid. not sending");
                        _logger.LogDebug("Verifying password for padlock: " + selected.ToName() + "with password " + _permActions.Password);
                        if (_permActions.PadlockVerifyUnlock<IPadlockable>(newAppearance.GagSlots[_permActions.GagLayer], selected, canUseOwnerLocks))
                        {
                            newAppearance.GagSlots[_permActions.GagLayer].Padlock = selected.ToName();
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
                            _logger.LogDebug("Unlocking Gag with GagPadlock {0} on {1}", selected.ToName(), PairNickOrAliasOrUID);
                            Opened = ActiveActionButton.None;
                        }
                        else
                        {
                            _logger.LogDebug("Invalid Password Validation");
                        }
                        _permActions.ResetInputs();
                    }
                    catch (Exception e) { _logger.LogError("Failed to push updated appearance data: " + e.Message); }
                }
                // draw password field combos.
                _permActions.DisplayPadlockFields(selected, true);
            }
            ImGui.Separator();
        }

        ////////// REMOVE GAG //////////
        if (_uiShared.IconTextButton(FontAwesomeIcon.TimesCircle, ("Remove " + PairUID + "'s Gag"), WindowMenuWidth, true,
        (disableRemoving || !canUseGagFeatures || disableLocking)))
        {
            Opened = Opened == ActiveActionButton.RemoveGag ? ActiveActionButton.None : ActiveActionButton.RemoveGag;
        }
        UiSharedService.AttachToolTip("Remove " + PairUID + "'s Gag. Click to view options.");
        if (Opened is ActiveActionButton.RemoveGag)
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
                        Opened = ActiveActionButton.None;
                    }
                    catch (Exception e) { _logger.LogError("Failed to push updated appearance data: " + e.Message); }
                }
            }
        }
        ImGui.Separator();
    }
}
