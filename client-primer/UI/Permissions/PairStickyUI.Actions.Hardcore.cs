using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using GagspeakAPI.Data.Permissions;
using GagspeakAPI.Dto.Permissions;
using GagspeakAPI.Dto.Toybox;
using GagspeakAPI.Enums;
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
            Opened = Opened == ActiveActionButton.ShockAction ? ActiveActionButton.None : ActiveActionButton.ShockAction;
        }
        UiSharedService.AttachToolTip("Perform a Shock action to " + PairUID + "'s Shock Collar.");

        if (Opened is ActiveActionButton.ShockAction)
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
                        Opened = ActiveActionButton.None;
                    }
                }
                catch (Exception e) { _logger.LogError("Failed to push ShockCollar Shock message: " + e.Message); }
            }
            ImGui.Separator();
        }

        if (_uiShared.IconTextButton(FontAwesomeIcon.WaveSquare, "Vibrate " + PairNickOrAliasOrUID + "'s Shock Collar", WindowMenuWidth, true, false))
        {
            Opened = Opened == ActiveActionButton.VibrateAction ? ActiveActionButton.None : ActiveActionButton.VibrateAction;
        }
        UiSharedService.AttachToolTip("Perform a Vibrate action to " + PairUID + "'s Shock Collar.");

        if (Opened is ActiveActionButton.VibrateAction)
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
                        Opened = ActiveActionButton.None;
                    }
                }
                catch (Exception e) { _logger.LogError("Failed to push ShockCollar Vibrate message: " + e.Message); }
            }
            ImGui.Separator();
        }

        if (_uiShared.IconTextButton(FontAwesomeIcon.LandMineOn, "Beep " + PairNickOrAliasOrUID + "'s Shock Collar", WindowMenuWidth, true, !permissions.AllowBeeps))
        {
            Opened = Opened == ActiveActionButton.BeepAction ? ActiveActionButton.None : ActiveActionButton.BeepAction;
        }
        UiSharedService.AttachToolTip("Beep " + PairUID + "'s Shock Collar.");

        if (Opened is ActiveActionButton.BeepAction)
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
                        Opened = ActiveActionButton.None;
                    }
                }
                catch (Exception e) { _logger.LogError("Failed to push ShockCollar Beep message: " + e.Message); }
            }
            ImGui.Separator();
        }
    }
}
