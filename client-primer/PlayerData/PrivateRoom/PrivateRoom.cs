using GagSpeak.PlayerData.Factories;
using GagSpeak.Services.Events;
using GagSpeak.Services.Mediator;
using GagspeakAPI.Data;
using GagspeakAPI.Data.Comparer;
using GagspeakAPI.Data.VibeServer;
using GagspeakAPI.Dto.Connection;
using GagspeakAPI.Dto.Toybox;
using GagspeakAPI.Dto.User;

namespace GagSpeak.PlayerData.PrivateRooms;

/// <summary>
/// Manages the activity of the currently joined Private Room.
/// </summary>
public class PrivateRoom
{
    private readonly ILogger<PrivateRoom> _logger;
    private readonly GagspeakMediator _mediator;
    private readonly ParticipantFactory _participantFactory;
    private readonly ConcurrentDictionary<string, Participant> _participants; // UserUID, Participant
    // create a lazy list of the participants in the room.
    private Lazy<List<Participant>> _directParticipantsInternal;
    
    public PrivateRoom(ILogger<PrivateRoom> logger, GagspeakMediator mediator,
        ParticipantFactory participantFactory, RoomInfoDto roomInfo)
    {
        _participantFactory = participantFactory;
        _logger = logger;
        _mediator = mediator;
        _participants = new(StringComparer.Ordinal);
        LastReceivedRoomInfo = roomInfo;

        // initialize the list of participants in the room.
        foreach (var participant in roomInfo.ConnectedUsers)
        {
            _participants[participant.UserUID] = _participantFactory.Create(participant);
        }
        
        // initialize the lazy list of participants.
        _directParticipantsInternal = DirectParticipantsLazy();
    }

    /// <summary>
    /// contains all information related to the room. Heaviest to call Data-wise.
    /// 
    /// NEVER TRUST THIS ROOMS PARTICIPANTS LIST, ALWAYS USE THE PARTICIPANTS PROPERTY. (real list updates lots)
    /// </summary>
    public RoomInfoDto? LastReceivedRoomInfo { get; private set; }

    public Participant HostParticipant => Participants.First(p => p.User.UserUID == LastReceivedRoomInfo?.RoomHost.UserUID);

    public List<RoomMessageDto> ChatHistory { get; private set; }
    public string RoomName => LastReceivedRoomInfo?.NewRoomName ?? "No Room Name";
    public List<Participant> Participants => _directParticipantsInternal.Value;

    public void AddParticipantToRoom(PrivateRoomUser newUser, bool addToLastAddedParticipant = true)
    {
        _logger.LogTrace("Scanning all participants to see if added user already exists");
        // if the user is not in the room's participant list, create a new participant for them.
        if (!_participants.ContainsKey(newUser.UserUID))
        {
            _logger.LogDebug("User {user} not found in participants, creating new participant", newUser);
            // create a new participant object for the user through the participant factory
            _participants[newUser.UserUID] = _participantFactory.Create(newUser);
        }
        // if the user is in the room's participant list, apply the last received data to the participant.
        else
        {
            _logger.LogDebug("User {user} found in participants, applying last received data instead.", newUser);
            // apply the last received data to the participant.
            _participants[newUser.UserUID].User = newUser;
        }
    }

    // marks the room as inactive, clearing any cached data while still
    // keeping reference data as to not dispose it entirely.
    public void MarkInactive(PrivateRoomUser userToMark)
    {
        if (_participants.TryGetValue(userToMark.UserUID, out var participant))
        {
            // mark them as offline, after modifying their data.
            // (maybe include the below two lines inside the participant class?)
            _participants[userToMark.UserUID].User.ActiveInRoom = false;
            _participants[participant.User.UserUID].User.VibeAccess = userToMark.VibeAccess;
            participant.MarkOffline();
        }
        RecreateLazy();
    }

    // completely removes the user from the room's participant list. and RoomInfo.
    public void RemoveRoomParticipant(PrivateRoomUser dto)
    {
        if (_participants.TryGetValue(dto.UserUID, out var participant))
        {
            // clear stored information
            participant.MarkOffline();
            // try and remove the participant from the room
            _participants.TryRemove(dto.UserUID, out _);
        }
        RecreateLazy();
    }

    public void ParticipantUpdate(PrivateRoomUser dto)
    {
        // if the user in the Dto is not in our private room's participant list, throw an exception.
        if (!_participants.TryGetValue(dto.UserUID, out var participant)) throw new InvalidOperationException("No user found for " + dto.ChatAlias);

        // apply the update to the participant
        _participants[dto.UserUID].User = dto;

        RecreateLazy();
    }

    public void UpdateRoomInfo(RoomInfoDto dto)
    {
        // update the last received room info
        LastReceivedRoomInfo = dto;

        // update the participants in the room
        foreach (var participant in dto.ConnectedUsers)
        {
            // if the participant is not equal to the stored participant with the same UID, update it.
            if (!_participants.TryGetValue(participant.UserUID, out var storedParticipant) 
             || !storedParticipant.User.Equals(participant))
            {
                // this means the participant is not in the room, so add them.
                AddParticipantToRoom(participant);
            }
            else
            {
                // the participant is already in the room, so update their data with the latest
                storedParticipant.User = participant;
            }
        }

        RecreateLazy();
    }

    // fetch a PrivateRoomUser in the participant of the provided userUID.
    public PrivateRoomUser GetParticipant(string userUID) => _participants[userUID].User;

    // create a concatinating string of all participants chat aliases, seperated by a common,
    public string GetParticipantList() => string.Join(", ", Participants.Select(p => p.User.ChatAlias));

    // get how many participants in the room have their PrivateRoomUser.ActiveInRoom set to true.
    public int GetActiveParticipants() => _participants.Count(p => p.Value.User.ActiveInRoom);

    // see if a particular Participant at the given UserUID key exists in the room with InRoom as true.
    public bool IsParticipantInRoom(string userUID) => _participants.TryGetValue(userUID, out var participant) && participant.User.ActiveInRoom;

    public void AddChatMessage(RoomMessageDto message) => ChatHistory.Add(message);

    /// <summary> Retrieves a participant's device information push data. </summary>
    public void ReceiveParticipantDeviceData(UserCharaDeviceInfoMessageDto dto)
    {
        // if the user in the Dto is not in our private room's participant list, throw an exception.
        if (!_participants.TryGetValue(dto.User.UserUID, out var participant)) throw new InvalidOperationException("No user found for " + dto.User);

        // append the device data to the user's device information list.
        _participants[dto.User.UserUID].ApplyDeviceData(dto);
    }

    public void ApplyDeviceUpdate(UpdateDeviceDto dto)
    {
        // next, see if they are the current room host, as only the host is allowed to update your devices.
        // TODO: Inject this logic, currently participant name system is fucked up.

        // insure the roomname the update is being applied to matches the name of the Room
        if (dto.RoomName != RoomName) throw new InvalidOperationException("Room being applied to is not your room!");
        
        // If reached here, apply the update to your connected devices.
        _logger.LogDebug("Applying Device Update from {user}", dto.User);
        // TODO: Inject this logic to update the active devices using the device handler.
    }

    // helper function to see if a particular userData is a particpant in the room
    public bool IsUserInRoom(string userUID) => _participants.ContainsKey(userUID);

    protected void Dispose(bool disposing)
    {
        if(disposing)
        {
            DisposeParticipants();
        }
    }

    private Lazy<List<Participant>> DirectParticipantsLazy() => new(() => _participants.Select(k => k.Value).ToList());

    private void DisposeParticipants()
    {
        // log the action about to occur
        _logger.LogDebug("Disposing all Participants of the Private Room");
        Parallel.ForEach(_participants, item =>
        {
            item.Value.MarkOffline();
        });
        RecreateLazy();
    }

    private void RecreateLazy()
    {
        _directParticipantsInternal = DirectParticipantsLazy();
        _mediator.Publish(new RefreshUiMessage());
    }
}
