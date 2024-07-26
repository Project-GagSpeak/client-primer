using GagSpeak.PlayerData.Factories;
using GagSpeak.Services.Events;
using GagSpeak.Services.Mediator;
using GagspeakAPI.Data;
using GagspeakAPI.Data.Comparer;
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
    private readonly ConcurrentDictionary<UserData, Participant> _participants;
    // create a lazy list of the participants in the room.
    private Lazy<List<Participant>> _directParticipantsInternal;
    
    public PrivateRoom(ILogger<PrivateRoom> logger, GagspeakMediator mediator,
        ParticipantFactory participantFactory, RoomInfoDto roomInfo)
    {
        _participantFactory = participantFactory;
        _logger = logger;
        _mediator = mediator;
        _participants = new(UserDataComparer.Instance);
        RoomInfo = roomInfo;
    }

    public RoomInfoDto? RoomInfo = null;
    public List<RoomMessageDto> ChatHistory { get; private set; }
    public string RoomName => RoomInfo?.NewRoomName ?? "No Room Name";
    public List<Participant> Participants => _directParticipantsInternal.Value;

    public void AddParticipantToRoom(UserData newUser, bool addToLastAddedParticipant = true)
    {
        _logger.LogTrace("Scanning all participants to see if added user already exists");
        // if the user is not in the room's participant list, create a new participant for them.
        if (!_participants.ContainsKey(newUser))
        {
            _logger.LogDebug("User {user} not found in participants, creating new participant", newUser);
            // create a new participant object for the user through the participant factory
            _participants[newUser] = _participantFactory.Create(newUser);
        }
        // if the user is in the room's participant list, apply the last received data to the participant.
        else
        {
            _logger.LogDebug("User {user} found in participants, applying last received data instead.", newUser.User);
            // apply the last received data to the participant.
            _participants[newUser].ParicipantUser = newUser;
        }
    }

    public void RemoveRoomParticipant(UserDto dto)
    {
        if (_participants.TryGetValue(dto.User, out var participant))
        {
            // clear stored information
            participant.MarkOffline();
            // try and remove the participant from the room
            _participants.TryRemove(dto.User, out _);
        }
        RecreateLazy();
    }

    public void AddChatMessage(RoomMessageDto message) => ChatHistory.Add(message);

    /// <summary> Retrieves a participant's device information push data. </summary>
    public void ReceiveParticipantDeviceData(UserCharaDeviceInfoMessageDto dto)
    {
        // if the user in the Dto is not in our private room's participant list, throw an exception.
        if (!_participants.TryGetValue(dto.User, out var pair)) throw new InvalidOperationException("No user found for " + dto.User);

        // publish the event that a user was added to our private room as a new participant.
        _mediator.Publish(new EventMessage(new Event(pair.UserData, nameof(PrivateRoomManager), EventSeverity.Informational, "Received Character IPC Data")));

        // append the device data to the user's device information list.
        _participants[dto.User].ApplyDeviceData(dto);
    }

    public void ApplyDeviceUpdate(UpdateDeviceDto dto)
    {
        // firstly, see if the User in the update is in our rooms participant list.
        if (!_participants.TryGetValue(dto.User, out var pair)) throw new InvalidOperationException("No user found for " + dto.User);

        // next, see if they are the current room host, as only the host is allowed to update your devices.
        // TODO: Inject this logic, currently participant name system is fucked up.

        // insure the roomname the update is being applied to matches the name of the Room
        if (dto.RoomName != RoomName) throw new InvalidOperationException("Room being applied to is not your room!");
        
        // If reached here, apply the update to your connected devices.
        _logger.LogDebug("Applying Device Update from {user}", dto.User);
        // TODO: Inject this logic to update the active devices using the device handler.
    }

    // marks the room in an active state
    public void MarkActive()
    {

    }

    // marks the room as inactive, clearing any cached data while still
    // keeping reference data as to not dispose it entirely.
    public void MarkInactive()
    {
        try
        {
            // set any created objects via factories to null

            _creationSemaphore.Wait();
            _onlineUserIdentDto = null;
            LastReceivedCharacterData = null;
            var player = CachedPlayer;
            CachedPlayer = null;
            player?.Dispose();
        }
        finally
        {
            _creationSemaphore.Release();
        }
    }

    // helper function to see if a particular userData is a particpant in the room
    public bool IsUserInRoom(UserData participantUser) => _participants.ContainsKey(participantUser);

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
