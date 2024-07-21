using GagSpeak.PlayerData.Data;
using GagSpeak.Services;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using GagSpeak.WebAPI;

namespace GagSpeak.PlayerData.Handlers;
/// <summary>
/// Should be a nice place to store a rapidly updating vibe intensity value while connecting to toybox servers to send the new intensities
/// </summary>
public class ToyboxHandler : DisposableMediatorSubscriberBase
{
    private readonly ClientConfigurationManager _clientConfigs;
    private readonly PlayerCharacterManager _playerManager;
    private readonly ApiController apiController;

    public ToyboxHandler(ILogger<GagDataHandler> logger, GagspeakMediator mediator,
        ClientConfigurationManager clientConfiguration, PlayerCharacterManager playerManager) : base(logger, mediator)
    {
        _clientConfigs = clientConfiguration;
        _playerManager = playerManager;


    }
}
