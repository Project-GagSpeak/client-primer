using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using ImGuiNET;
using Gagspeak.API.Data.Enum;
using FFStreamViewer.WebAPI.PlayerData.Pairs;
using FFStreamViewer.WebAPI.Services;
using FFStreamViewer.WebAPI.Services.Mediator;
using FFStreamViewer.WebAPI;
using Microsoft.Extensions.Logging;
using FFStreamViewer.WebAPI.UI.Components.Popup;
using FFStreamViewer.WebAPI.GagspeakConfiguration;
using System.Numerics;
using FFStreamViewer.UI.Tabs.MediaTab;
using Dalamud.Interface;
using Dalamud.Interface.Colors;

namespace FFStreamViewer.WebAPI.UI;
/// <summary>
/// Contains functions relative to the paired users permissions for the client user.
/// </summary>
public partial class UserPairPermsSticky
{
    public void DrawPairPermsForClient()
    {

    }

    public void DrawClientPermsForPair()
    {
        /* ----------- GLOBAL SETTINGS ----------- */
        ImGui.TextUnformatted("Global Settings");

        // Pair's live chat garbler setting.
        var isDisableSounds = UserPairForPerms.UserPair!.OwnPermissions.IsDisableSounds();
        string disableSoundsText = isDisableSounds ? "Enable sound sync" : "Disable sound sync";
        var disableSoundsIcon = isDisableSounds ? FontAwesomeIcon.VolumeUp : FontAwesomeIcon.VolumeMute;
        if (_uiSharedService.IconTextButton(disableSoundsIcon, disableSoundsText, _menuWidth, true))
        {
            var permissions = _pair.UserPair.OwnPermissions;
            permissions.SetDisableSounds(!isDisableSounds);
            _ = _apiController.UserSetPairPermissions(new UserPermissionsDto(_pair.UserData, permissions));
        }
        UiSharedService.AttachToolTip("Changes sound sync permissions with this user." + (individual ? individualText : string.Empty));
    }

    /// <summary> The left side of the permissions row, containing the rows icon, and the help text menu </summary>
    private void DrawRowLeftSide()
    {
        // for now, we can set the userpairtext to empty
        string userPairText = string.Empty;

        // lets make sure that our grouped row is aligned to the frame padding
        ImGui.AlignTextToFramePadding();

        // if the user is offline, we will display the offline icon relative to pair status.
        if (!UserPairForPerms.IsOnline)
        {
            // display it red regardless, but
            using var _ = ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudRed);
            _uiSharedService.IconText(UserPairForPerms.IndividualPairStatus == IndividualPairStatus.OneSided // if one-sided
                ? FontAwesomeIcon.ArrowsLeftRight  // show the left-right arrows
                : FontAwesomeIcon.User);    // otherwise they are bidirectional, so display standard offline.
            userPairText = UserPairForPerms.UserData.AliasOrUID + " is offline";
        }
        // if the user is visible / present.
        else if (UserPairForPerms.IsVisible)
        {
            // display the green eye icon, since we can see them.
            _uiSharedService.IconText(FontAwesomeIcon.Eye, ImGuiColors.ParsedGreen);
            userPairText = UserPairForPerms.UserData.AliasOrUID + " is visible: " + UserPairForPerms.PlayerName 
                + Environment.NewLine + "Click Icon to target player";
            if (ImGui.IsItemClicked())
            {
                Mediator.Publish(new TargetPairMessage(UserPairForPerms));
            }
        }
        else
        {
            using var _ = ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.HealerGreen);
            _uiSharedService.IconText(UserPairForPerms.IndividualPairStatus == IndividualPairStatus.Bidirectional
                ? FontAwesomeIcon.User : FontAwesomeIcon.Users);
            userPairText = UserPairForPerms.UserData.AliasOrUID + " is online";
        }

        if (UserPairForPerms.IndividualPairStatus == IndividualPairStatus.OneSided)
        {
            userPairText += UiSharedService.TooltipSeparator + "User has not added you back";
        }
        else if (UserPairForPerms.IndividualPairStatus == IndividualPairStatus.Bidirectional)
        {
            userPairText += UiSharedService.TooltipSeparator + "You are directly Paired";
        }

        UiSharedService.AttachToolTip(userPairText);

        ImGui.SameLine();

    var ySize = ImGui.GetCursorPosY();
        ImGui.SetWindowSize(new(400, ySize));
    }
}
