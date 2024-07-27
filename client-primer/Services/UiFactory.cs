using GagSpeak.PlayerData.Pairs;
using GagSpeak.Services.Mediator;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.UI;
using GagSpeak.UI.Components.Popup;
using GagSpeak;
using Microsoft.Extensions.Logging;
using GagSpeak.GagspeakConfiguration;
using GagSpeak.WebAPI;
using GagSpeak.UI.Profile;
using GagSpeak.UI.UiRemote;
using GagSpeak.PlayerData.Handlers;
using GagSpeak.Toybox.Services;
using GagSpeak.PlayerData.PrivateRooms;

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
    private readonly GagspeakConfigService _gagspeakConfigService;
    private readonly GagspeakProfileManager _gagspeakProfileManager;

    public UiFactory(ILoggerFactory loggerFactory, GagspeakMediator gagspeakMediator,
        ApiController apiController, UiSharedService uiSharedService, DeviceHandler handler,
        PairManager pairManager, ToyboxRemoteService remoteService, 
        GagspeakConfigService configService, ServerConfigurationManager serverConfigs, 
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
        _gagspeakConfigService = configService;
        _gagspeakProfileManager = profileManager;
    }

    public RemoteController CreateControllerRemote(PrivateRoom privateRoom)
    {
        return new RemoteController(_loggerFactory.CreateLogger<RemoteController>(), _gagspeakMediator,
            _uiSharedService, _remoteService, _deviceHandler, privateRoom);
    }

    public StandaloneProfileUi CreateStandaloneProfileUi(Pair pair)
    {
        return new StandaloneProfileUi(_loggerFactory.CreateLogger<StandaloneProfileUi>(), _gagspeakMediator,
            _uiSharedService, _serverConfigs, _gagspeakProfileManager, _pairManager, pair);
    }
}
