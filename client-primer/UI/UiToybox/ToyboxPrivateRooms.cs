using Dalamud.Interface.Colors;
using Dalamud.Interface;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using GagSpeak.WebAPI;
using GagspeakAPI.Data.Enum;
using ImGuiNET;
using System.Globalization;
using Dalamud.Interface.Utility.Raii;
using GagSpeak.PlayerData.PrivateRooms;
using GagspeakAPI.Dto.Toybox;
using Dalamud.Interface.Utility;
using GagSpeak.GagspeakConfiguration.Models;
using System.Numerics;
using GagspeakAPI.Dto.Connection;
using System.Reflection.Metadata;
using OtterGui.Text;

namespace GagSpeak.UI.UiToybox;

public class ToyboxPrivateRooms : DisposableMediatorSubscriberBase
{
    private readonly ApiController _apiController;
    private readonly PrivateRoomManager _roomManager;
    private readonly UiSharedService _uiShared;
    private readonly ServerConfigurationManager _serverConfigs;

    public ToyboxPrivateRooms(ILogger<ToyboxPrivateRooms> logger, 
        GagspeakMediator mediator, ApiController apiController,
        PrivateRoomManager privateRoomManager, UiSharedService uiShared,
        ServerConfigurationManager serverConfigs) : base(logger, mediator)
    {
        _apiController = apiController;
        _roomManager = privateRoomManager;
        _uiShared = uiShared;
        _serverConfigs = serverConfigs;
    }

    private string _errorMessage = string.Empty;
    private DateTime _errorTime;

    public bool CreatingNewHostRoom { get; private set; } = false;
    public bool RoomCreatedSuccessful { get; private set; } = false;
    public bool HostPrivateRoomHovered { get; private set; } = false;
    private List<bool> JoinRoomItemsHovered = new List<bool>();

    public void DrawVibeServerPanel()
    {
        // draw the connection interface
        DrawToyboxServerStatus();

        // Draw the header for creating a host room
        if(CreatingNewHostRoom)
        {
            DrawCreatingHostRoomHeader();
            ImGui.Separator();
            DrawNewHostRoomDisplay();
        }
        else
        {
            DrawCreateHostRoomHeader();
            ImGui.Separator();
            DrawPrivateRoomMenu();


            // draw out all details about the current hosted room.
            if (_roomManager.ClientHostingAnyRoom)
            {
                ImGui.Text("Am I in any rooms?: " + _roomManager.ClientInAnyRoom);
                ImGui.Text("Hosted Room Details:");
                ImGui.Text("Room Name: " + _roomManager.ClientHostedRoomName);
                // draw out the participants
                var privateRoom = _roomManager.AllPrivateRooms.First(r => r.RoomName == _roomManager.ClientHostedRoomName);
                // draw out details about this room.
                ImGui.Text("Host UID: " + privateRoom.HostParticipant.User.UserUID);
                ImGui.Text("Host Alias: " + privateRoom.HostParticipant.User.ChatAlias);
                ImGui.Text("InRoom: " + privateRoom.HostParticipant.User.ActiveInRoom);
                ImGui.Text("Allow Vibes: " + privateRoom.HostParticipant.User.VibeAccess);
                // draw out the participants
                ImGui.Indent();
                foreach (var participant in privateRoom.Participants)
                {
                    ImGui.Text("User UID: " + participant.User.UserUID);
                    ImGui.Text("User Alias: " + participant.User.ChatAlias);
                    ImGui.Text("InRoom: " + participant.User.ActiveInRoom);
                    ImGui.Text("Allow Vibes: " + participant.User.VibeAccess);
                    ImGui.Separator();
                }
                ImGui.Unindent();

            }

        }
    }

    private void DrawCreateHostRoomHeader()
    {
        // Use button rounding
        using var rounding = ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, 12f);
        var startYpos = ImGui.GetCursorPosY();
        var iconSize = _uiShared.GetIconButtonSize(FontAwesomeIcon.Plus);
        var invitesSize = _uiShared.GetIconTextButtonSize(FontAwesomeIcon.Plus, "Invites");
        Vector2 textSize;
        using (_uiShared.UidFont.Push())
        {
            textSize = ImGui.CalcTextSize("Host Room");
        }
        var centerYpos = (textSize.Y - iconSize.Y);
        using (ImRaii.Child("CreateHostRoomHeader", new Vector2(UiSharedService.GetWindowContentRegionWidth(), iconSize.Y + ((startYpos+centerYpos) - startYpos) * 2)))
        {
            // set startYpos to 0
            startYpos = ImGui.GetCursorPosY();
            // Center the button vertically
            ImGui.SameLine(10 * ImGuiHelpers.GlobalScale);
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + centerYpos);

            // Draw the icon button. If room is created, this will turn into a trash bin for deletion.
            if (_roomManager.ClientHostingAnyRoom)
            {
                using (var disabled = ImRaii.Disabled(!UiSharedService.ShiftPressed()))
                {
                    if (_uiShared.IconButton(FontAwesomeIcon.Trash))
                    {
                        _apiController.PrivateRoomRemove(_roomManager.ClientHostedRoomName).ConfigureAwait(false);
                    }
                }
                UiSharedService.AttachToolTip("Delete Hosted Room");
            }
            else
            {
                if (_uiShared.IconButton(FontAwesomeIcon.Plus))
                {
                    CreatingNewHostRoom = true;
                }
            }

            // Draw the header text
            ImGui.SameLine(10 * ImGuiHelpers.GlobalScale + iconSize.X + ImGui.GetStyle().ItemSpacing.X);
            ImGui.SetCursorPosY(startYpos);
            _uiShared.BigText("Host Room");

            // Draw the "See Invites" button on the right
            ImGui.SameLine((ImGui.GetWindowContentRegionMax().X-ImGui.GetWindowContentRegionMin().X) 
                - invitesSize - 10 * ImGuiHelpers.GlobalScale);
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + centerYpos);

            if (_uiShared.IconTextButton(FontAwesomeIcon.Envelope, "Invites "))
            {
                // Handle button click
                Logger.LogInformation("See Invites button clicked");
            }
        }
    }

    private void DrawCreatingHostRoomHeader()
    {
        // Use button rounding
        using var rounding = ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, 12f);
        var startYpos = ImGui.GetCursorPosY();
        var iconSize = _uiShared.GetIconButtonSize(FontAwesomeIcon.PowerOff);
        Vector2 textSize;
        using (_uiShared.UidFont.Push())
        {
            textSize = ImGui.CalcTextSize("Setup Room");
        }
        var centerYpos = (textSize.Y - iconSize.Y);
        using (ImRaii.Child("PrivateRoomSetupHeader", new Vector2(UiSharedService.GetWindowContentRegionWidth(), iconSize.Y + ((startYpos + centerYpos) - startYpos) * 2)))
        {
            // set startYpos to 0
            startYpos = ImGui.GetCursorPosY();
            // Center the button vertically
            ImGui.SameLine(10 * ImGuiHelpers.GlobalScale);
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + centerYpos);
            // Draw the icon button
            if (_uiShared.IconButton(FontAwesomeIcon.ArrowLeft))
            {
                // reset the createdAlarm to a new alarm, and set editing alarm to true
                CreatingNewHostRoom = false;
            }
            UiSharedService.AttachToolTip("Exit Private Room Setup");
            
            // Draw the header text
            ImGui.SameLine(10 * ImGuiHelpers.GlobalScale + iconSize.X + ImGui.GetStyle().ItemSpacing.X);
            ImGui.SetCursorPosY(startYpos);
            _uiShared.BigText("Setup Room");

            // Draw the "See Invites" button on the right
            ImGui.SameLine((ImGui.GetWindowContentRegionMax().X - ImGui.GetWindowContentRegionMin().X)
                - iconSize.X - 10 * ImGuiHelpers.GlobalScale);
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + centerYpos);

            if (_uiShared.IconButton(FontAwesomeIcon.PowerOff))
            {
                // Handle button click
                Logger.LogInformation("See Invites button clicked");
            }
            UiSharedService.AttachToolTip("Startup your Private Room with the defined settings below");
        }
    }

    // local accessors for the private room creation
    public string NewHostNameRef = string.Empty;
    public string HostChatAlias = string.Empty;

    private void DrawNewHostRoomDisplay()
    {
        using (_uiShared.UidFont.Push())
        {
            UiSharedService.ColorText("Hosted Rooms Info:", ImGuiColors.ParsedPink);
        }
        UiSharedService.TextWrapped(" - You may only host ONE private room at a time."
            + Environment.NewLine + " - You can send user-pairs invites to UserPairs online in"
            + " the vibe server by clicking the hosted room after it is created."
            + Environment.NewLine + " - ANY Hosted room made is automatically removed 12 hours later."
            + Environment.NewLine + " - ONLY the host of the room can control other users vibrators."
            + Environment.NewLine + " - You can create another hosted room directly after removing the current one.");

        ImGui.Separator();
        var refString1 = NewHostNameRef;
        ImGui.InputTextWithHint("Room Name (ID)##HostRoomName", "Private Room Name...", ref refString1, 50);
        if(ImGui.IsItemDeactivatedAfterEdit())
        {
            NewHostNameRef = refString1;
        }

        var refString2 = HostChatAlias;
        ImGui.InputTextWithHint("Your Chat Alias##HostChatAlias", "Chat Alias...", ref refString2, 30);
        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            HostChatAlias = refString2;
        }

        // the button to create the room.
        if (_uiShared.IconTextButton(FontAwesomeIcon.Plus, "Create Private Room"))
        {
            // create the room
            try
            {
                // also log the success of the creation.
                RoomCreatedSuccessful = _apiController.PrivateRoomCreate(new RoomCreateDto(NewHostNameRef, HostChatAlias)).Result;
            }
            catch (Exception ex)
            {
                _errorMessage = ex.Message;
                _errorTime = DateTime.Now;
            }
        }
        // if there is an error message, display it
        if (!string.IsNullOrEmpty(_errorMessage) && (DateTime.Now - _errorTime).TotalSeconds < 3)
        {
            ImGui.TextColored(ImGuiColors.DalamudRed, _errorMessage);
        }
        // if the room creation was successful, set the room creation to false.
        if (RoomCreatedSuccessful)
        {
            CreatingNewHostRoom = false;
            RoomCreatedSuccessful = false;
        }
    }

    private void DrawPrivateRoomMenu()
    {
        // see if the manager has any rooms at all to display
        if (_roomManager.AllPrivateRooms.Count == 0)
        {
            ImGui.Text("No private rooms available.");
            return;
        }

        // if the size of the list is different than the size of the rooms in the room manager, recreate list
        if(JoinRoomItemsHovered.Count != _roomManager.AllPrivateRooms.Count-1)
        {
            JoinRoomItemsHovered = new List<bool>(_roomManager.AllPrivateRooms.Count);
            for (int i = 0; i < _roomManager.AllPrivateRooms.Count; i++)
            {
                JoinRoomItemsHovered.Clear();
                JoinRoomItemsHovered.AddRange(Enumerable.Repeat(false, _roomManager.AllPrivateRooms.Count));
            }
        }

        // Display error message if it has been less than 3 seconds since the error occurred
        if (!string.IsNullOrEmpty(_errorMessage) && (DateTime.Now - _errorTime).TotalSeconds < 3)
        {
            ImGui.TextColored(ImGuiColors.DalamudRed, _errorMessage);
        }

        var hostedRoom = _roomManager.AllPrivateRooms.First(r => r.RoomName == _roomManager.ClientHostedRoomName);
        // If currently hosting a room, draw the hosted room first
        if (_roomManager.ClientHostingAnyRoom)
        {
            // grab the PrivateRoom of the AllPrivateRooms list where the room name == ClientHostedRoomName
            DrawPrivateRoomSelectable(hostedRoom, true);
        }

        // Draw the rest of the rooms, excluding the hosted room
        int idx = 0;
        foreach (var room in _roomManager.AllPrivateRooms.Where(r => r != hostedRoom))
        {
            DrawPrivateRoomSelectable(room, false, idx);
            idx++;
        }
    }

    private void DrawPrivateRoomSelectable(PrivateRoom privateRoomRef, bool isHostedRoom, int idx = -1)
    {
        // define our sizes
        using var rounding = ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, 12f);

        // grab the room name.
        string roomType = isHostedRoom ? "[Hosted]" : "[Joined]";
        string roomName = privateRoomRef.RoomName;
        string participantsCountText = "[" + privateRoomRef.GetActiveParticipants() + "/" + privateRoomRef.Participants.Count + " Active]";

        // draw startposition in Y
        var startYpos = ImGui.GetCursorPosY();
        var joinedState = _uiShared.GetIconButtonSize(privateRoomRef.IsParticipantActiveInRoom(_roomManager.ClientUserUID)
            ? FontAwesomeIcon.DoorOpen : FontAwesomeIcon.DoorClosed);
        var roomTypeTextSize = ImGui.CalcTextSize(roomType);
        var roomNameTextSize = ImGui.CalcTextSize(roomName);
        var totalParticipantsTextSize = ImGui.CalcTextSize(participantsCountText);
        var participantAliasListSize = ImGui.CalcTextSize(privateRoomRef.GetParticipantList());

        using var color = ImRaii.PushColor(ImGuiCol.ChildBg, ImGui.GetColorU32(ImGuiCol.FrameBgHovered), (isHostedRoom ? HostPrivateRoomHovered : JoinRoomItemsHovered[idx]));
        using (ImRaii.Child($"##PreviewPrivateRoom{roomName}", new Vector2(UiSharedService.GetWindowContentRegionWidth(), ImGui.GetStyle().ItemSpacing.Y*2 + ImGui.GetFrameHeight()*2)))
        {
            // create a group for the bounding area
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + ImGui.GetStyle().ItemSpacing.Y);
            using (var group = ImRaii.Group())
            {
                // scooch over a bit like 5f
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 5f);
                // display the type.
                UiSharedService.ColorText(roomType, ImGuiColors.DalamudYellow);
                ImUtf8.SameLineInner();
                // display the room name
                ImGui.Text(roomName);
            }

            // now draw the lower section out.
            using (var group = ImRaii.Group())
            {
                // scooch over a bit like 5f
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 5f);
                // display the participants count
                UiSharedService.ColorText(participantsCountText, ImGuiColors.DalamudGrey2);
                ImUtf8.SameLineInner();
                UiSharedService.ColorText(privateRoomRef.GetParticipantList(), ImGuiColors.DalamudGrey3);

            }

            // this is cancer, but they deprecated the ContentRegionWidth, so what can ya do lol.
            ImGui.SameLine(ImGui.GetWindowContentRegionMin().X + UiSharedService.GetWindowContentRegionWidth() 
                - joinedState.X - ImGui.GetStyle().ItemSpacing.X);

            ImGui.SetCursorPosY(ImGui.GetCursorPosY() - (ImGui.GetFrameHeight() / 2));
            // draw out the icon button
            if (_uiShared.IconButton(privateRoomRef.IsParticipantActiveInRoom(_roomManager.ClientUserUID) 
                ? FontAwesomeIcon.DoorOpen : FontAwesomeIcon.DoorClosed))
            {
                try
                {
                    // set the enabled state of the alarm based on its current state so that we toggle it
                    if (privateRoomRef.IsParticipantActiveInRoom(_roomManager.ClientUserUID))
                    {
                        // leave the room
                        _apiController.PrivateRoomLeave(new RoomParticipantDto
                            (privateRoomRef.GetParticipant(_roomManager.ClientUserUID).User, roomName)).ConfigureAwait(false);
                    
                    }
                    else
                    {
                        // join the room
                        _apiController.PrivateRoomJoin(new RoomParticipantDto
                            (privateRoomRef.GetParticipant(_roomManager.ClientUserUID).User, roomName)).ConfigureAwait(false);
                    }
                    // toggle the state & early return so we don't access the child clicked button
                    return;
                }
                catch (Exception ex)
                {
                    _errorMessage = ex.Message;
                    _errorTime = DateTime.Now;
                }
            }
            UiSharedService.AttachToolTip(privateRoomRef.IsParticipantActiveInRoom(_roomManager.ClientUserUID)
                ? "Leave Room" : "Join Room");

            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + ImGui.GetStyle().ItemSpacing.Y);
        }
        // Check if the item is hovered and assign the hover state correctly
        if (isHostedRoom)
        {
            HostPrivateRoomHovered = ImGui.IsItemHovered();
        }
        else
        {
            JoinRoomItemsHovered[idx] = ImGui.IsItemHovered();
        }
        // action on clicky.
        if (ImGui.IsItemClicked())
        {
            // if we are currently joined in the private room, we can open the instanced remote.
            if (privateRoomRef.IsParticipantActiveInRoom(_roomManager.ClientUserUID))
            {
                // open the respective rooms remote.
                Mediator.Publish(new OpenPrivateRoomRemote(privateRoomRef));
            }
            else
            {
                // toggle the additional options display.
                Logger.LogInformation("You must be joined into the room to open the interface.");
            }
        }
        ImGui.Separator();
    }

    /* ---------------- Server Status Header Shit --------------------- */
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
