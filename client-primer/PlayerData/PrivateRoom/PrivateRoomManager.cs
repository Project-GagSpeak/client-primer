using GagSpeak.PlayerData.Factories;
using GagSpeak.Services.Events;
using GagSpeak.Services.Mediator;
using GagSpeak.WebAPI;
using GagspeakAPI.Data;
using GagspeakAPI.Data.Comparer;
using GagspeakAPI.Dto.Connection;
using GagspeakAPI.Dto.Toybox;
using GagspeakAPI.Dto.User;
using Microsoft.VisualBasic.ApplicationServices;

namespace GagSpeak.PlayerData.PrivateRooms;

/// <summary>
/// Manages the activity of the currently joined Private Room.
/// </summary>
public sealed class PrivateRoomManager : DisposableMediatorSubscriberBase
{
    private readonly ILogger<PrivateRoomManager> _logger;
    private readonly PrivateRoomFactory _roomFactory;
    private readonly ConcurrentDictionary<string, PrivateRoom> _rooms;
    private Lazy<List<PrivateRoom>> _privateRoomsInternal;
    private readonly List<RoomInviteDto> _roomInvites;

    public PrivateRoomManager(ILogger<PrivateRoomManager> logger, GagspeakMediator mediator,
        PrivateRoomFactory roomFactory) : base(logger, mediator)
    {
        _logger = logger;
        _roomFactory = roomFactory;
        _rooms = new(StringComparer.Ordinal);
        _roomInvites = [];

        _privateRoomsInternal = DirectRoomsLazy();
    }

    public List<PrivateRoom> PrivateRooms => _privateRoomsInternal.Value;
    public PrivateRoom? LastAddedRoom { get; private set; }
    public RoomInviteDto? LastRoomInvite { get; private set; }
    public bool ClientInAnyRoom { get; private set; } = false;

    public void AddRoom(RoomInfoDto roomInfo)
    {
        // dont create if it already exists.
        if(!_rooms.ContainsKey(roomInfo.RoomHost))
        {
            // otherwise, create the room.
            _rooms[roomInfo.NewRoomName] = _roomFactory.Create(roomInfo);
        }
        else
        {
            Logger.LogWarning("Room {room} already exists, skipping creation", roomInfo.RoomHost);
            // TODO: maybe apply last stored room data or something?
        }
        RecreateLazy();
    }

    // generic AddRoom method, called whenever we create a new room or join an existing one.
    public void AddRoom(RoomInfoDto roomInfo, bool addToLastAddedRoom = true)
    {
        // dont create if it already exists.
        if (!_rooms.ContainsKey(roomInfo.RoomHost))
        {
            // otherwise, create the room.
            _rooms[roomInfo.NewRoomName] = _roomFactory.Create(roomInfo);
        }
        else
        {
            Logger.LogWarning("Room {room} already exists, skipping creation", roomInfo.RoomHost);
            addToLastAddedRoom = false;
        }

        if(addToLastAddedRoom)
            LastAddedRoom = _rooms[roomInfo.NewRoomName];
        
        Logger.LogWarning("Room {room} already exists, skipping creation", roomInfo.RoomHost);
        // TODO: maybe apply last stored room data or something?
        
        RecreateLazy();
    }

    // for removing a room from the list of rooms.
    public void RemoveRoom(string roomName)
    {
        if (_rooms.TryGetValue(roomName, out var privateRoom))
        {
            // try and remove it from the list of rooms.
            _rooms.TryRemove(roomName, out _);
        }
        // recreate the lazy list of rooms.
        RecreateLazy();
    }

    public void InviteRecieved(RoomInviteDto latestRoomInvite)
    {
        Logger.LogDebug("Invite Received to join room {room}", latestRoomInvite.RoomName);
        // add the invite to the list of room invites.
        _roomInvites.Add(latestRoomInvite);
        // set the last room invite to the latest invite.
        LastRoomInvite = latestRoomInvite;
    }

    // for whenever we either create a new room, or join an existing one.
    public void ClientJoinRoom(RoomInfoDto roomInfo, bool SetClientInRoom = true)
    {
        // see if the _apiController.PlayerUser (client) is present in any other rooms currently
        if (ClientInAnyRoom)
        {
            Logger.LogWarning("Client is already in a room, unable to join another.");
            return;
        }

        // if we are able to join a new room, first see if we already have the room stored
        if (_rooms.TryGetValue(roomInfo.NewRoomName, out var privateRoom))
        {
            // if the room already exists, repopulate its room participants with everyone and join it
            Logger.LogInformation("Room {room} was already cached. Repopulating host and online users!", roomInfo.NewRoomName);
            // mark the room as Active
        }
        else
        {
            // if we don't already have the room cached, create the room.
            AddRoom(roomInfo);
            Logger.LogInformation("Creating new {room}", roomInfo.NewRoomName);
        }
    }

    // for adding a participant to a room, or marking them as active if they are already in the room.
    public void AddParticipantToRoom(RoomParticipantDto dto, bool addToLastParticipant = true)
    {
        // see if the room they are in is in any rooms we have added.
        if (_rooms.TryGetValue(dto.RoomName, out var privateRoom))
        {
            // if the participant is already in the room, apply the last received data to the participant.
            if (privateRoom.IsUserInRoom(dto.User))
            {
                Logger.LogDebug("User {user} found in participants, marking as active.", dto.User);
                return;
            }
            // user was not already stored, but room did exist, so add them to the room.
            privateRoom.AddParticipantToRoom(dto.User, addToLastParticipant);
        }
        else
        {
            Logger.LogWarning("Room {room} not found, unable to add participant.", dto.RoomName);
        }
    }

    // for when the participant themselves left the room. remove them from it.
    public void ParticipantLeftRoom(RoomParticipantDto dto)
    {
        // locate the room the participant should be removed from if it exists, and remove them.
        if (_rooms.TryGetValue(dto.RoomName, out var privateRoom))
        {
            // if the room exists, remove the participant from the room. (because they were the ones to do it.
            privateRoom.RemoveRoomParticipant(dto);
        }
        else
        {
            Logger.LogWarning("Room {room} not found, unable to remove participant.", dto.RoomName);
        }
    }

    public void AddChatMessage(RoomMessageDto message)
    {
        // locate the room the message should be sent to if it exists, and send it.
        if (_rooms.TryGetValue(message.RoomName, out var privateRoom))
        {
            // if the room exists, add the message to the room.
            privateRoom.AddChatMessage(message);
        }
        else
        {
            Logger.LogWarning("Room {room} not found, unable to add message.", message.RoomName);
        }
    }

    // helper function to see if the user is an active participant in any other rooms.
    public bool IsUserInAnyRoom(UserData user) => _rooms.Values.Any(room => room.IsUserInRoom(user));

    // marks a room as inactive
    public void MarkRoomInactive(string RoomName)
    {
        if (!_rooms.ContainsKey(RoomName)) throw new InvalidOperationException("No Room found matching" + RoomName);
        if (_rooms.TryGetValue(RoomName, out var privateRoom))
        {
            privateRoom.MarkInactive();
        }

        RecreateLazy();
    }

    public void ClientLeaveRoom(string roomName, bool ClientLeavingRoom = true)
    {
        if (!_rooms.ContainsKey(roomName))
        {
            Logger.LogWarning("Room {room} not found, unable to leave.", roomName);
        }
        else
        {
            // set ClientInAnyRoom to false.
            ClientInAnyRoom = false;
            // remove the client from the list of participants in the room. (TODO)

        }
    }


    // when the client leaves a room, should push a UserLeaveRoom message to the server.
    public void RoomClosedByHost(string RoomName)
    {
        // the host (either yourself or another host of a room you joined) closed their room, so remove it.
        if (!_rooms.ContainsKey(RoomName)) throw new InvalidOperationException("No Room found matching" + RoomName);

        // dispose and remove the room from the room list
        if (_rooms.TryGetValue(RoomName, out var privateRoom))
        {
            // try and remove it from the list of rooms.
            _rooms.TryRemove(RoomName, out _);
        }
        else
        {
            Logger.LogWarning("Room {room} not found, unable to leave.", RoomName);
        }
        RecreateLazy();
    }

    /// <summary> Retrieves a participant's device information push data. </summary>
    public void ReceiveParticipantDeviceData(UserCharaDeviceInfoMessageDto dto)
    {
        // if the roomname is not in our list of active rooms, throw invalid operation
        if (!_rooms.TryGetValue(dto.RoomName, out var privateRoom)) throw new InvalidOperationException("Room being applied to is not your room!");

        // if the userdata is not an active participant in the room, throw invalid operation
        if (!privateRoom.IsUserInRoom(dto.User)) throw new InvalidOperationException("User not found in room!");

        // otherwise, update their device information.
        privateRoom.ReceiveParticipantDeviceData(dto);
    }

    /// <summary> Applies a device update to your active devices. </summary>
    public void ApplyDeviceUpdate(UpdateDeviceDto dto)
    {
        // if the roomname is not in our list of active rooms, throw invalid operation
        if (!_rooms.TryGetValue(dto.RoomName, out var privateRoom)) throw new InvalidOperationException("Room being applied to is not your room!");

        // if the person applying is not the room host, throw invalid operation
        if (privateRoom.RoomInfo?.RoomHost != dto.User.AliasOrUID) throw new InvalidOperationException("Only the room host can update devices!");

        // Apply Device update
        Logger.LogDebug("Applying Device Update from {user}", dto.User);
        // TODO: Inject this logic, currently participant name system is fucked up.
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        ClearAllRooms();
    }


    /// <summary> Clears all participants from the private room. </summary>
    public void ClearAllRooms()
    {
        Logger.LogDebug("Clearing all Rooms from room manager");
        DisposeRooms();
        _rooms.Clear();
        RecreateLazy();
    }

    private void DisposeRooms()
    {
        Logger.LogDebug("Disposing all Rooms");
        // mark all rooms as inactive
        Parallel.ForEach(_rooms, item =>
        {
            item.Value.MarkInactive();
        });

        RecreateLazy();
    }

    /// <summary> The lazy list of room participants. </summary>
    private Lazy<List<PrivateRoom>> DirectRoomsLazy() => new(() => _rooms.Select(k => k.Value).ToList());


    /// <summary> Recreates the lazy list of room participants lazy style. </summary>
    private void RecreateLazy()
    {
        _privateRoomsInternal = DirectRoomsLazy();
        Mediator.Publish(new RefreshUiMessage());
    }
}
