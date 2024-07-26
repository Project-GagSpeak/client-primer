using Dalamud.Interface.Colors;
using Dalamud.Interface;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using GagSpeak.WebAPI;
using GagspeakAPI.Data.Enum;
using ImGuiNET;
using System.Globalization;
using Dalamud.Interface.Utility.Raii;
using GagSpeak.PlayerData.PrivateRoom;
using GagspeakAPI.Dto.Toybox;
using Dalamud.Interface.Utility;
using GagSpeak.GagspeakConfiguration.Models;
using System.Numerics;

namespace GagSpeak.UI.UiToybox;

public class ToyboxPrivateRooms : DisposableMediatorSubscriberBase
{
    private readonly ApiController _apiController;
    private readonly PrivateRoomManager _privateRoomManager;
    private readonly UiSharedService _uiShared;
    private readonly ServerConfigurationManager _serverConfigs;

    public ToyboxPrivateRooms(ILogger<ToyboxPrivateRooms> logger, 
        GagspeakMediator mediator, ApiController apiController,
        PrivateRoomManager privateRoomManager, UiSharedService uiShared,
        ServerConfigurationManager serverConfigs) : base(logger, mediator)
    {
        _apiController = apiController;
        _privateRoomManager = privateRoomManager;
        _uiShared = uiShared;
        _serverConfigs = serverConfigs;
    }

    public bool HostPrivateRoomHovered = false;
    public bool HostingRoom = false;

    public void DrawVibeServerPanel()
    {
        // draw the connection interface
        DrawToyboxServerStatus();

        // below this, draw our toybox "Host Room" header. This 

        // draw out the header
        _uiShared.BigText("Private Rooms:");

        // display an option 
        DrawHostRoomHeader();
        DrawJoinRoomHeader();

        if (_apiController.ToyboxServerState == ServerState.Connected)
        {
            DrawRoomDetails();
            DrawRoomInteractions();
        }
    }

    private void DrawHostRoomHeader()
    {
        // use button wrounding
        using var rounding = ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, 12f);
        var startYpos = ImGui.GetCursorPosY();
        var iconSize = _uiShared.GetIconButtonSize(FontAwesomeIcon.Plus);
        Vector2 textSize;
        using (_uiShared.UidFont.Push())
        {
            textSize = ImGui.CalcTextSize("Host Private Room");
        }
        var centerYpos = (textSize.Y - iconSize.Y);
        var color = ImRaii.PushColor(ImGuiCol.ChildBg, ImGui.GetColorU32(ImGuiCol.FrameBgHovered), HostPrivateRoomHovered);
        using (ImRaii.Child("HostPrivateRoomHeader", new Vector2(UiSharedService.GetWindowContentRegionWidth(), iconSize.Y + (centerYpos - startYpos) * 2)))
        {
            // now calculate it so that the cursors Yposition centers the button in the middle height of the text
            ImGui.SameLine(10 * ImGuiHelpers.GlobalScale);
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + centerYpos);
            // draw out the icon button
            if (_uiShared.IconButton(FontAwesomeIcon.Plus))
            {
                // reset the createdAlarm to a new alarm, and set editing alarm to true
                CreatedAlarm = new Alarm();
                CreatingAlarm = true;
            }
            // now next to it we need to draw the header text
            ImGui.SameLine(10 * ImGuiHelpers.GlobalScale + iconSize.X + ImGui.GetStyle().ItemSpacing.X);
            ImGui.SetCursorPosY(startYpos);
            _uiShared.BigText("New Alarm");
        }
    }


    private void DrawRoomDetails()
    {
        var roomInfo = _privateRoomManager.RoomInfo;
        if (roomInfo == null)
        {
            ImGui.Text("No room connected.");
            return;
        }

        ImGui.Text($"Room Name: {roomInfo.NewRoomName}");
        ImGui.Text($"Participants: {roomInfo.ConnectedUsers.Count}");

        foreach (var participant in roomInfo.ConnectedUsers)
        {
            ImGui.Text($"- {participant.User.UID}");
        }
    }

    private void DrawRoomInteractions()
    {
        if (ImGui.Button("Send Message"))
        {
            // Example: Send a message to the room
            var messageDto = new RoomMessageDto
            {
                RoomName = _apiController.PrivateRoomManager.RoomName,
                Message = "Hello, everyone!"
            };
            _ = _apiController.UserSendMessageToRoom(messageDto);
        }

        if (ImGui.Button("Invite User"))
        {
            // Example: Invite a user to the room
            var inviteDto = new RoomInviteDto
            {
                RoomName = _apiController.PrivateRoomManager.RoomName,
                UserName = "UserToInvite"
            };
            _ = _apiController.UserRoomInvite(inviteDto);
        }

        if (ImGui.Button("Leave Room"))
        {
            _ = _apiController.UserLeaveRoom();
        }
    }

    private void DrawToyboxServerStatus()
    {
        var buttonSize = _uiShared.GetIconButtonSize(FontAwesomeIcon.Link);
        var userCount = _apiController.ToyboxOnlineUsers.ToString(CultureInfo.InvariantCulture);
        var userSize = ImGui.CalcTextSize(userCount);
        var textSize = ImGui.CalcTextSize("Toybox Users Online");

        string shardConnection = $"GagSpeak Toybox Server";

        var shardTextSize = ImGui.CalcTextSize(shardConnection);
        var printShard = shardConnection != string.Empty;

        // if the server is connected, then we should display the server info
        if (_apiController.ToyboxServerState is ServerState.Connected)
        {
            // fancy math shit for clean display, adjust when moving things around
            ImGui.SetCursorPosX((ImGui.GetWindowContentRegionMin().X + UiSharedService.GetWindowContentRegionWidth()) / 2 - (userSize.X + textSize.X) / 2 - ImGui.GetStyle().ItemSpacing.X / 2);
            ImGui.TextColored(ImGuiColors.ParsedGreen, userCount);
            ImGui.SameLine();
            ImGui.TextUnformatted("Toybox Users Online");
        }
        // otherwise, if we are not connected, display that we aren't connected.
        else
        {
            ImGui.AlignTextToFramePadding();
            ImGui.SetCursorPosX((ImGui.GetWindowContentRegionMin().X
                + UiSharedService.GetWindowContentRegionWidth())
                / 2 - (ImGui.CalcTextSize("Not connected to the toybox server").X) / 2 - ImGui.GetStyle().ItemSpacing.X / 2);
            ImGui.TextColored(ImGuiColors.DalamudRed, "Not connected to the toybox server");
        }

        ImGui.SetCursorPosY(ImGui.GetCursorPosY() - ImGui.GetStyle().ItemSpacing.Y);
        ImGui.SetCursorPosX((ImGui.GetWindowContentRegionMin().X + UiSharedService.GetWindowContentRegionWidth()) / 2 - shardTextSize.X / 2);
        ImGui.TextUnformatted(shardConnection);
        ImGui.SameLine();

        // now we need to display the connection link button beside it.
        var color = UiSharedService.GetBoolColor(!_serverConfigs.CurrentServer!.ToyboxFullPause);
        var connectedIcon = !_serverConfigs.CurrentServer.ToyboxFullPause ? FontAwesomeIcon.Link : FontAwesomeIcon.Unlink;

        ImGui.SameLine(ImGui.GetWindowContentRegionMin().X + UiSharedService.GetWindowContentRegionWidth() - buttonSize.X);
        if (printShard)
        {
            // unsure what this is doing but we can find out lol
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() - ((userSize.Y + textSize.Y) / 2 + shardTextSize.Y) / 2 - ImGui.GetStyle().ItemSpacing.Y + buttonSize.Y / 2);
        }

        // if the server is reconnecting or disconnecting
        if (_apiController.ToyboxServerState is not (ServerState.Reconnecting or ServerState.Disconnecting))
        {
            // we need to turn the button from the connected link to the disconnected link.
            using (ImRaii.PushColor(ImGuiCol.Text, color))
            {
                // then display it
                if (_uiShared.IconButton(connectedIcon))
                {
                    // and toggle the fullpause for the current server, save the config, and recreate the connections,
                    // placing it into a disconnected state due to the full pause being active. (maybe change this later)
                    _serverConfigs.CurrentServer.ToyboxFullPause = !_serverConfigs.CurrentServer.ToyboxFullPause;
                    _serverConfigs.Save();
                    _ = _apiController.CreateToyboxConnection();
                }
            }
            // attach the tooltip for the connection / disconnection button)
            UiSharedService.AttachToolTip(!_serverConfigs.CurrentServer.ToyboxFullPause
                ? "Disconnect from Toybox Server" : "Connect to ToyboxServer");
        }

        // draw out the vertical slider.
        ImGui.Separator();
    }
}
