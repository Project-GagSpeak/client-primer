using GagSpeak.PlayerData.Data;
using GagSpeak.Services;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using GagSpeak.WebAPI;

namespace GagSpeak.PlayerData.Handlers;
/// <summary>
/// Should be a nice place to store a rapidly updating vibe intensity value while connecting to toybox servers to send the new intensities
/// </summary>
public class ToyboxVibeHandler : DisposableMediatorSubscriberBase
{
    private readonly ClientConfigurationManager _clientConfigs;
    private readonly PlayerCharacterManager _playerManager;
    private readonly ToyboxVibeService _vibeService;
    private readonly ApiController apiController;

    public ToyboxVibeHandler(ILogger<GagDataHandler> logger, GagspeakMediator mediator,
        ClientConfigurationManager clientConfiguration, PlayerCharacterManager playerManager,
        ToyboxVibeService vibeService) : base(logger, mediator)
    {
        _clientConfigs = clientConfiguration;
        _playerManager = playerManager;
        _vibeService = vibeService;


    }

    public int VibratorIntensity => _vibeService.VibratorIntensity;
}
