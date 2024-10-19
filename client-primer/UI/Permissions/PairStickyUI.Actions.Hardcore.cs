using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using GagSpeak.PlayerData.Pairs;
using GagSpeak.WebAPI;
using GagspeakAPI.Data.Permissions;
using GagspeakAPI.Dto.Permissions;
using GagspeakAPI.Dto.Toybox;
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
    private void DrawHardcoreActions()
    {
        if(_playerManager.GlobalPerms is null)
            return;

        // conditions for disabled actions
        bool inRange = Vector3.Distance(_clientState.LocalPlayer?.Position ?? default, _clientState.LocalPlayer?.TargetObject?.Position ?? default) < 3;
        // Conditionals for hardcore interactions
        var clientGlobals = _playerManager.GlobalPerms;
        bool disableForceFollow = !inRange || !PairPerms.AllowForcedFollow || !UserPairForPerms.IsVisible || !_playerManager.GlobalPerms.CanToggleFollow(ApiController.UID);
        bool disableForceSit = !PairPerms.AllowForcedSit || !_playerManager.GlobalPerms.CanToggleSit(ApiController.UID);
        bool disableForceGroundSit = !PairPerms.AllowForcedSit || !_playerManager.GlobalPerms.CanToggleSit(ApiController.UID);
        bool disableForceToStay = !PairPerms.AllowForcedToStay || !_playerManager.GlobalPerms.CanToggleStay(ApiController.UID);
        bool disableBlindfoldToggle = !PairPerms.AllowBlindfold || !_playerManager.GlobalPerms.CanToggleBlindfold(ApiController.UID);
        bool disableChatVisibilityToggle = !PairPerms.AllowHidingChatboxes || !_playerManager.GlobalPerms.CanToggleChatHidden(ApiController.UID);
        bool disableChatInputVisibilityToggle = !PairPerms.AllowHidingChatInput || !_playerManager.GlobalPerms.CanToggleChatInputHidden(ApiController.UID);
        bool disableChatInputBlockToggle = !PairPerms.AllowChatInputBlocking || !_playerManager.GlobalPerms.CanToggleChatInputBlocked(ApiController.UID);

        var forceFollowIcon = PairGlobals.IsFollowing() ? FontAwesomeIcon.StopCircle : FontAwesomeIcon.PersonWalkingArrowRight;
        var forceFollowText = PairGlobals.IsFollowing() ? $"Have {PairNickOrAliasOrUID} stop following you." : $"Make {PairNickOrAliasOrUID} follow you.";
        if (_uiShared.IconTextButton(forceFollowIcon, forceFollowText, WindowMenuWidth, true, disableForceFollow))
        {
            string newStr = PairGlobals.IsFollowing() ? string.Empty : ApiController.UID + Globals.DevotedString;
            _ = _apiController.UserUpdateOtherGlobalPerm(new UserGlobalPermChangeDto(UserPairForPerms.UserData, new KeyValuePair<string, object>("ForcedFollow", newStr)));
        }

        var forceSitIcon = PairGlobals.IsSitting() ? FontAwesomeIcon.StopCircle : FontAwesomeIcon.Chair;
        var forceSitText = PairGlobals.IsSitting() ? $"Let {PairNickOrAliasOrUID} stand again." : $"Force {PairNickOrAliasOrUID} to sit.";
        if (_uiShared.IconTextButton(forceSitIcon, forceSitText, WindowMenuWidth, true, disableForceSit))
        {
            string newStr = PairGlobals.IsSitting() ? string.Empty : ApiController.UID + Globals.DevotedString;
            _ = _apiController.UserUpdateOtherGlobalPerm(new UserGlobalPermChangeDto(UserPairForPerms.UserData, new KeyValuePair<string, object>("ForcedSit", newStr)));
        }

        var forceGroundSitIcon = PairGlobals.IsSitting() ? FontAwesomeIcon.StopCircle : FontAwesomeIcon.Chair;
        var forceGroundSitText = PairGlobals.IsSitting() ? $"Let {PairNickOrAliasOrUID} stand again." : $"Force {PairNickOrAliasOrUID} to their knees.";
        if (_uiShared.IconTextButton(forceGroundSitIcon, forceGroundSitText, WindowMenuWidth, true, disableForceGroundSit))
        {
            string newStr = PairGlobals.IsSitting() ? string.Empty : ApiController.UID + Globals.DevotedString;
            _ = _apiController.UserUpdateOtherGlobalPerm(new UserGlobalPermChangeDto(UserPairForPerms.UserData, new KeyValuePair<string, object>("ForcedGroundsit", newStr)));
        }

        var forceToStayIcon = PairGlobals.IsStaying() ? FontAwesomeIcon.StopCircle : FontAwesomeIcon.HouseLock;
        var forceToStayText = PairGlobals.IsStaying() ? $"Release {PairNickOrAliasOrUID}." : $"Lock away {PairNickOrAliasOrUID}.";
        if (_uiShared.IconTextButton(forceToStayIcon, forceToStayText, WindowMenuWidth, true, disableForceToStay))
        {
            string newStr = PairGlobals.IsStaying() ? string.Empty : ApiController.UID + Globals.DevotedString;
            _ = _apiController.UserUpdateOtherGlobalPerm(new UserGlobalPermChangeDto(UserPairForPerms.UserData, new KeyValuePair<string, object>("ForcedStay", newStr)));
        }

        var toggleBlindfoldIcon = PairGlobals.IsBlindfolded() ? FontAwesomeIcon.StopCircle : FontAwesomeIcon.Mask;
        var toggleBlindfoldText = PairGlobals.IsBlindfolded() ? $"Remove {PairNickOrAliasOrUID}'s Blindfold." : $"Blindfold {PairNickOrAliasOrUID}.";
        if (_uiShared.IconTextButton(toggleBlindfoldIcon, toggleBlindfoldText, WindowMenuWidth, true, disableBlindfoldToggle))
        {
            string newStr = PairGlobals.IsBlindfolded() ? string.Empty : ApiController.UID + Globals.DevotedString;
            _ = _apiController.UserUpdateOtherGlobalPerm(new UserGlobalPermChangeDto(UserPairForPerms.UserData, new KeyValuePair<string, object>("ForcedBlindfold", newStr)));
        }

        var toggleChatboxIcon = PairGlobals.IsChatHidden() ? FontAwesomeIcon.StopCircle : FontAwesomeIcon.CommentSlash;
        var toggleChatboxText = PairGlobals.IsChatHidden() ? "Make " + PairNickOrAliasOrUID + "'s Chat Visible." : "Hide "+PairNickOrAliasOrUID+"'s Chat Window.";
        if (_uiShared.IconTextButton(toggleChatboxIcon, toggleChatboxText, WindowMenuWidth, true, disableChatVisibilityToggle))
        {
            string newStr = PairGlobals.IsChatHidden() ? string.Empty : ApiController.UID + Globals.DevotedString;
            _ = _apiController.UserUpdateOtherGlobalPerm(new UserGlobalPermChangeDto(UserPairForPerms.UserData, new KeyValuePair<string, object>("ChatboxesHidden", newStr)));
        }

        var toggleChatInputIcon = PairGlobals.IsChatInputHidden() ? FontAwesomeIcon.StopCircle : FontAwesomeIcon.CommentSlash;
        var toggleChatInputText = PairGlobals.IsChatInputHidden() ? "Make " + PairNickOrAliasOrUID + "'s Chat Input Visible." : "Hide "+PairNickOrAliasOrUID+"'s Chat Input.";
        if (_uiShared.IconTextButton(toggleChatInputIcon, toggleChatInputText, WindowMenuWidth, true, disableChatInputVisibilityToggle))
        {
            string newStr = PairGlobals.IsChatInputHidden() ? string.Empty : ApiController.UID + Globals.DevotedString;
            _ = _apiController.UserUpdateOtherGlobalPerm(new UserGlobalPermChangeDto(UserPairForPerms.UserData, new KeyValuePair<string, object>("ChatInputHidden", newStr)));
        }

        var toggleChatBlockingIcon = PairGlobals.IsChatInputBlocked() ? FontAwesomeIcon.StopCircle : FontAwesomeIcon.CommentDots;
        var toggleChatBlockingText = PairGlobals.IsChatInputBlocked() ? "Block "+PairNickOrAliasOrUID+"'s Chat Input." : "Reallow "+PairNickOrAliasOrUID+"'s Chat Input.";
        if (_uiShared.IconTextButton(toggleChatBlockingIcon, toggleChatBlockingText, WindowMenuWidth, true, disableChatInputBlockToggle))
        {
            string newStr = PairGlobals.IsChatInputBlocked() ? string.Empty : ApiController.UID + Globals.DevotedString;
            _ = _apiController.UserUpdateOtherGlobalPerm(new UserGlobalPermChangeDto(UserPairForPerms.UserData, new KeyValuePair<string, object>("ChatInputBlocked", newStr)));
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
        TimeSpan maxVibeDuration = (UserPairForPerms.LastPairPiShockPermsForYou.MaxIntensity != -1) ? PairPerms.MaxVibrateDuration : UserPairForPerms.UserPairGlobalPerms.GlobalShockVibrateDuration;
        string piShockShareCodePref = (UserPairForPerms.LastPairPiShockPermsForYou.MaxIntensity != -1) ? PairPerms.ShockCollarShareCode : UserPairForPerms.UserPairGlobalPerms.GlobalShockShareCode;

        if (_uiShared.IconTextButton(FontAwesomeIcon.BoltLightning, "Shock " + PairNickOrAliasOrUID + "'s Shock Collar", WindowMenuWidth, true, !permissions.AllowShocks))
        {
            Opened = Opened == InteractionType.ShockAction ? InteractionType.None : InteractionType.ShockAction;
        }
        UiSharedService.AttachToolTip("Perform a Shock action to " + PairUID + "'s Shock Collar.");

        if (Opened is InteractionType.ShockAction)
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
                        UnlocksEventManager.AchievementEvent(UnlocksEvent.ShockSent);
                        Opened = InteractionType.None;
                    }
                }
                catch (Exception e) { _logger.LogError("Failed to push ShockCollar Shock message: " + e.Message); }
            }
            ImGui.Separator();
        }

        if (_uiShared.IconTextButton(FontAwesomeIcon.WaveSquare, "Vibrate " + PairNickOrAliasOrUID + "'s Shock Collar", WindowMenuWidth, true, false))
        {
            Opened = Opened == InteractionType.VibrateAction ? InteractionType.None : InteractionType.VibrateAction;
        }
        UiSharedService.AttachToolTip("Perform a Vibrate action to " + PairUID + "'s Shock Collar.");

        if (Opened is InteractionType.VibrateAction)
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
                        Opened = InteractionType.None;
                    }
                }
                catch (Exception e) { _logger.LogError("Failed to push ShockCollar Vibrate message: " + e.Message); }
            }
            ImGui.Separator();
        }

        if (_uiShared.IconTextButton(FontAwesomeIcon.LandMineOn, "Beep " + PairNickOrAliasOrUID + "'s Shock Collar", WindowMenuWidth, true, !permissions.AllowBeeps))
        {
            Opened = Opened == InteractionType.BeepAction ? InteractionType.None : InteractionType.BeepAction;
        }
        UiSharedService.AttachToolTip("Beep " + PairUID + "'s Shock Collar.");

        if (Opened is InteractionType.BeepAction)
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
                        Opened = InteractionType.None;
                    }
                }
                catch (Exception e) { _logger.LogError("Failed to push ShockCollar Beep message: " + e.Message); }
            }
            ImGui.Separator();
        }
    }
}
