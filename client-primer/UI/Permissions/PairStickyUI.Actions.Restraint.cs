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
    private void DrawWardrobeActions()
    {
        // verify that the unique pair perms and last recieved wardrobe data are not null.
        var lastWardrobeData = UserPairForPerms.LastReceivedWardrobeData;
        var pairUniquePerms = UserPairForPerms.UserPairUniquePairPerms;
        bool canUseOwnerLocks = pairUniquePerms.OwnerLocks;
        if (lastWardrobeData == null || pairUniquePerms == null) return;

        bool applyButtonDisabled = !pairUniquePerms.ApplyRestraintSets || lastWardrobeData.OutfitNames.Count <= 0 || (lastWardrobeData.ActiveSetName != string.Empty && lastWardrobeData.Padlock != Padlocks.None.ToName());
        bool lockButtonDisabled = !pairUniquePerms.LockRestraintSets || lastWardrobeData.ActiveSetName == string.Empty || lastWardrobeData.Padlock != Padlocks.None.ToName();
        bool unlockButtonDisabled = !pairUniquePerms.UnlockRestraintSets || lastWardrobeData.Padlock == "None";
        bool removeButtonDisabled = !pairUniquePerms.RemoveRestraintSets || lastWardrobeData.ActiveSetName == string.Empty || lastWardrobeData.Padlock != Padlocks.None.ToName();

        ////////// APPLY RESTRAINT SET //////////
        if (_uiShared.IconTextButton(FontAwesomeIcon.Handcuffs, "Apply Restraint Set", WindowMenuWidth, true, applyButtonDisabled))
        {
            Opened = Opened == ActiveActionButton.ApplyRestraint ? ActiveActionButton.None : ActiveActionButton.ApplyRestraint;
        }
        UiSharedService.AttachToolTip("Applies a Restraint Set to " + UserPairForPerms.UserData.AliasOrUID + ". Click to select set.");
        if (Opened is ActiveActionButton.ApplyRestraint)
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
                        Opened = ActiveActionButton.ApplyRestraint;
                    }
                    catch (Exception e) { _logger.LogError("Failed to push updated Wardrobe data: " + e.Message); }
                });
            }
            ImGui.Separator();
        }

        ////////// LOCK RESTRAINT SET //////////
        string DisplayText = unlockButtonDisabled ? "Lock Restraint Set" : "Locked with a " + lastWardrobeData.Padlock;
        // push text style
        using (var color = ImRaii.PushColor(ImGuiCol.Text, (lastWardrobeData.Padlock == Padlocks.None.ToName()) ? ImGuiColors.DalamudWhite : ImGuiColors.DalamudYellow))
        {
            if (_uiShared.IconTextButton(FontAwesomeIcon.Lock, DisplayText, WindowMenuWidth, true, lockButtonDisabled))
            {
                Opened = Opened == ActiveActionButton.LockRestraint ? ActiveActionButton.None : ActiveActionButton.LockRestraint;
            }
        }
        UiSharedService.AttachToolTip("Locks the Restraint Set applied to " + UserPairForPerms.UserData.AliasOrUID + ". Click to view options.");
        if (Opened is ActiveActionButton.LockRestraint)
        {
            Padlocks selected = _permActions.GetSelectedItem<Padlocks>("LockRestraintSetForPairPermCombo", UserPairForPerms.UserData.UID);
            float height = _permActions.ExpandLockHeightCheck(selected)
                ? 3 * ImGui.GetFrameHeight() + 2 * ImGui.GetStyle().ItemSpacing.Y
                : 2 * ImGui.GetFrameHeight() + ImGui.GetStyle().ItemSpacing.Y;

            using (var actionChild = ImRaii.Child("SetLockChild", new Vector2(WindowMenuWidth, height), false))
            {
                if (!actionChild) return;

                bool disabled = selected == Padlocks.None || !pairUniquePerms.LockRestraintSets;

                using (ImRaii.Disabled(true))
                {
                    ImGui.SetNextItemWidth(WindowMenuWidth);
                    if (ImGui.BeginCombo("##DummyComboDisplayLockedSet", "Locking: [" + UserPairForPerms.LastReceivedWardrobeData?.ActiveSetName + "]" ?? "Not Set Active")) { ImGui.EndCombo(); }
                }

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
                            Opened = ActiveActionButton.None;
                            // reset the password and timer
                        }
                        _permActions.ResetInputs();
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
            Opened = Opened == ActiveActionButton.UnlockRestraint ? ActiveActionButton.None : ActiveActionButton.UnlockRestraint;
        }
        UiSharedService.AttachToolTip("Unlocks the Restraint Set applied to " + UserPairForPerms.UserData.AliasOrUID + ". Click to view options.");
        if (Opened is ActiveActionButton.UnlockRestraint)
        {
            Padlocks selected = UserPairForPerms.LastReceivedWardrobeData?.Padlock.ToPadlock() ?? Padlocks.None;
            float height = _permActions.ExpandLockHeightCheck(selected)
                ? 2 * ImGui.GetFrameHeight() + ImGui.GetStyle().ItemSpacing.Y
                : ImGui.GetFrameHeight();
            using (var actionChild = ImRaii.Child("SetUnlockChild", new Vector2(WindowMenuWidth, height), false))
            {
                if (!actionChild) return;

                bool disabled = selected == Padlocks.None || !pairUniquePerms.UnlockRestraintSets;
                // Draw combo
                float width = WindowMenuWidth - ImGui.GetStyle().ItemInnerSpacing.X - _uiShared.GetIconTextButtonSize(FontAwesomeIcon.Unlock, "Unlock");
                using (ImRaii.Disabled(true))
                {
                    ImGui.SetNextItemWidth(width);
                    if (ImGui.BeginCombo("##DummyComboDisplayLockedRestraintSet", UserPairForPerms.LastReceivedWardrobeData?.Padlock ?? "No Set Lock Active")) { ImGui.EndCombo(); }
                }
                ImUtf8.SameLineInner();
                if (_uiShared.IconTextButton(FontAwesomeIcon.Unlock, "Unlock", ImGui.GetContentRegionAvail().X, false, disabled))
                {
                    try
                    {
                        var newWardrobeData = lastWardrobeData.DeepClone();
                        if (newWardrobeData == null) throw new Exception("Wardrobe data is null, not sending");

                        if (_permActions.PadlockVerifyUnlock<IPadlockable>(newWardrobeData, selected, canUseOwnerLocks))
                        {
                            newWardrobeData.Padlock = selected.ToName();
                            newWardrobeData.Password = _permActions.Password;
                            newWardrobeData.Timer = DateTimeOffset.UtcNow;
                            newWardrobeData.Assigner = _apiController.UID;
                            _ = _apiController.UserPushPairDataWardrobeUpdate(new(UserPairForPerms.UserData, newWardrobeData, DataUpdateKind.WardrobeRestraintUnlocked));
                            _logger.LogDebug("Unlocking Restraint Set with GagPadlock {0} on {1}", selected.ToName(), PairNickOrAliasOrUID);
                            Opened = ActiveActionButton.None;
                        }
                        _permActions.ResetInputs();
                    }
                    catch (Exception e) { _logger.LogError("Failed to push updated Wardrobe data: " + e.Message); }
                };
                // draw password field combos.
                _permActions.DisplayPadlockFields(selected);
            }
            ImGui.Separator();
        }

        // draw the remove restraint set button.
        if (_uiShared.IconTextButton(FontAwesomeIcon.TimesCircle, "Remove Restraint Set", WindowMenuWidth, true, removeButtonDisabled))
        {
            Opened = Opened == ActiveActionButton.RemoveRestraint ? ActiveActionButton.None : ActiveActionButton.RemoveRestraint;
        }
        UiSharedService.AttachToolTip("Removes the Restraint Set applied to " + UserPairForPerms.UserData.AliasOrUID + ". Click to view options.");
        if (Opened is ActiveActionButton.RemoveRestraint)
        {
            using (var actionChild = ImRaii.Child("SetRemoveChild", new Vector2(WindowMenuWidth, ImGui.GetFrameHeightWithSpacing()), false))
            {
                if (!actionChild) return;

                if (ImGui.Button("Remove Restraint", ImGui.GetContentRegionAvail()))
                {
                    try
                    {
                        var newWardrobeData = lastWardrobeData.DeepClone();
                        if (newWardrobeData == null) throw new Exception("Wardrobe data is null, not sending");
                        newWardrobeData.ActiveSetName = string.Empty;
                        newWardrobeData.ActiveSetEnabledBy = string.Empty;
                        _ = _apiController.UserPushPairDataWardrobeUpdate(new(UserPairForPerms.UserData, newWardrobeData, DataUpdateKind.WardrobeRestraintDisabled));
                        _logger.LogDebug("Removing Restraint Set from {0}", PairNickOrAliasOrUID);
                        Opened = ActiveActionButton.None;
                    }
                    catch (Exception e) { _logger.LogError("Failed to push updated Wardrobe data: " + e.Message); }
                }
            }
        }
        ImGui.Separator();
    }
}
