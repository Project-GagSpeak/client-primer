using GagSpeak.PlayerData.Data;
using GagSpeak.PlayerData.Pairs;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using GagSpeak.Utils.ChatLog;
using GagSpeak.WebAPI;
using GagspeakAPI.Data.Enum;

namespace GagSpeak.Services;

// handles the global chat and pattern discovery social features.
public class PermissionPresetService : DisposableMediatorSubscriberBase
{
    private readonly ApiController _apiController;
    private readonly PlayerCharacterManager _playerManager;
    private readonly ClientConfigurationManager _clientConfigs;
    private readonly PairManager _pairManager; // might not need if we use a pair to pass in for this.

    public PermissionPresetService(ILogger<DiscoverService> logger, 
        GagspeakMediator mediator, ApiController apiController, 
        PlayerCharacterManager playerManager, ClientConfigurationManager clientConfigs, 
        PairManager pairManager) : base(logger, mediator)
    {
        _apiController = apiController;
        _playerManager = playerManager;
        _clientConfigs = clientConfigs;
        _pairManager = pairManager;

    }

    //public async Task Apply
}
