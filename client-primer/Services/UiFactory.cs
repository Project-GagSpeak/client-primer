using GagSpeak.PlayerData.Handlers;
using GagSpeak.PlayerData.Pairs;
using GagSpeak.PlayerData.PrivateRooms;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using GagSpeak.Toybox.Services;
using GagSpeak.UI;
using GagSpeak.UI.Permissions;
using GagSpeak.UI.Profile;
using GagSpeak.UI.UiRemote;
using GagSpeak.WebAPI;

namespace GagSpeak.Services;

public class UiFactory
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly GagspeakMediator _gagspeakMediator;
    private readonly ApiController _apiController;
    private readonly UiSharedService _uiSharedService;
    private readonly DeviceHandler _deviceHandler;
    private readonly PairManager _pairManager;
    private readonly ToyboxRemoteService _remoteService;
    private readonly ServerConfigurationManager _serverConfigs;
    private readonly GagspeakProfileManager _gagspeakProfileManager;

    public UiFactory(ILoggerFactory loggerFactory, GagspeakMediator gagspeakMediator,
        ApiController apiController, UiSharedService uiSharedService, 
        DeviceHandler handler, PairManager pairManager,
        ToyboxRemoteService remoteService, ServerConfigurationManager serverConfigs,
        GagspeakProfileManager profileManager)
    {
        _loggerFactory = loggerFactory;
        _gagspeakMediator = gagspeakMediator;
        _apiController = apiController;
        _uiSharedService = uiSharedService;
        _deviceHandler = handler;
        _pairManager = pairManager;
        _remoteService = remoteService;
        _serverConfigs = serverConfigs;
        _gagspeakProfileManager = profileManager;
    }

    public RemoteController CreateControllerRemote(PrivateRoom privateRoom)
    {
        return new RemoteController(_loggerFactory.CreateLogger<RemoteController>(), _gagspeakMediator,
            _uiSharedService, _deviceHandler, _remoteService, _apiController, privateRoom);
    }

    public StandaloneProfileUi CreateStandaloneProfileUi(Pair pair)
    {
        return new StandaloneProfileUi(_loggerFactory.CreateLogger<StandaloneProfileUi>(), _gagspeakMediator,
            _uiSharedService, _serverConfigs, _gagspeakProfileManager, _pairManager, pair);
    }
}
