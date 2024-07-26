using GagSpeak.Services.Mediator;
using GagspeakAPI.Data;
using GagspeakAPI.Dto.Toybox;
using GagspeakAPI.Dto.User;

namespace GagSpeak.PlayerData.PrivateRooms;

/// <summary>
/// A class representing the participant in a private room.
/// </summary>
public class Participant
{
    private readonly ILogger<Participant> _logger;
    private readonly GagspeakMediator _mediator;
    public Participant(ILogger<Participant> logger, UserData user,
        GagspeakMediator mediator)
    {

        _logger = logger;
        _mediator = mediator;
        ParicipantUser = user;
    }

    public UserData ParicipantUser { get; set; }
    public List<UserCharaDeviceInfoMessageDto> ParticipantDevices { get; set; }

    // TODO: Rework method handles later.
    public void ApplyDeviceData(UserCharaDeviceInfoMessageDto dto)
    {
        if (ParticipantDevices == null)
        {
            ParticipantDevices = new();
        }
        ParticipantDevices.Add(dto);
    }

    public void RemoveDevice(UserCharaDeviceInfoMessageDto dto)
    {
        if (ParticipantDevices == null)
        {
            return;
        }
        ParticipantDevices.Remove(dto);
    }

    public void ClearDevices()
    {
        ParticipantDevices?.Clear();
    }


    public void MarkOffline()
    {
        _logger.LogInformation("Marking participant {UserData} as offline", ParicipantUser);
        ClearDevices();
    }
}
