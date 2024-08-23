using GagSpeak.PlayerData.Data;
using GagSpeak.PlayerData.Handlers;
using GagSpeak.PlayerData.Pairs;
using GagSpeak.PlayerData.PrivateRooms;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using GagSpeak.Toybox.Services;
using GagSpeak.UI;
using GagSpeak.UI.Handlers;
using GagSpeak.UI.Permissions;
using GagSpeak.UI.Profile;
using GagSpeak.UI.UiRemote;
using GagSpeak.UpdateMonitoring;
using GagSpeak.WebAPI;

namespace GagSpeak.Services;

public class UiFactory
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly GagspeakMediator _gagspeakMediator;
    private readonly ApiController _apiController;
    private readonly UiSharedService _uiSharedService;
    private readonly ToyboxVibeService _vibeService;
    private readonly IdDisplayHandler _displayHandler;
    private readonly PairManager _pairManager;
    private readonly PlayerCharacterManager _playerManager;
    private readonly ToyboxRemoteService _remoteService;
    private readonly ServerConfigurationManager _serverConfigs;
    private readonly ProfileService _gagspeakProfileManager;
    private readonly OnFrameworkService _frameworkUtils;
    private readonly MoodlesService _moodlesService;
    private readonly VisiblePairManager _visiblePairManager;

    public UiFactory(ILoggerFactory loggerFactory, GagspeakMediator gagspeakMediator,
        ApiController apiController, UiSharedService uiSharedService, 
        ToyboxVibeService vibeService, IdDisplayHandler displayHandler, 
        PairManager pairManager, PlayerCharacterManager playerManager,
        ToyboxRemoteService remoteService, ServerConfigurationManager serverConfigs,
        ProfileService profileManager, OnFrameworkService frameworkUtils,
        MoodlesService moodlesService, VisiblePairManager visiblePairManager)
    {
        _loggerFactory = loggerFactory;
        _gagspeakMediator = gagspeakMediator;
        _apiController = apiController;
        _uiSharedService = uiSharedService;
        _vibeService = vibeService;
        _displayHandler = displayHandler;
        _pairManager = pairManager;
        _playerManager = playerManager;
        _remoteService = remoteService;
        _serverConfigs = serverConfigs;
        _gagspeakProfileManager = profileManager;
        _frameworkUtils = frameworkUtils;
        _moodlesService = moodlesService;
        _visiblePairManager = visiblePairManager;
    }

    public RemoteController CreateControllerRemote(PrivateRoom privateRoom)
    {
        return new RemoteController(_loggerFactory.CreateLogger<RemoteController>(), _gagspeakMediator,
            _uiSharedService, _vibeService, _remoteService, _apiController, privateRoom);
    }

    public StandaloneProfileUi CreateStandaloneProfileUi(Pair pair)
    {
        return new StandaloneProfileUi(_loggerFactory.CreateLogger<StandaloneProfileUi>(), _gagspeakMediator,
            _uiSharedService, _serverConfigs, _gagspeakProfileManager, _pairManager, pair);
    }

    // create a new instance window of the userpair permissions window every time a new pair is selected.
    public UserPairPermsSticky CreateStickyPairPerms(Pair pair, StickyWindowType drawType)
    {
        return new UserPairPermsSticky(_loggerFactory.CreateLogger<UserPairPermsSticky>(), _gagspeakMediator, pair, 
            drawType, _frameworkUtils, _playerManager, _displayHandler, _uiSharedService, _apiController, _pairManager,
            _moodlesService, _visiblePairManager);
    }
}
