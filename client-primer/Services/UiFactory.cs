using Dalamud.Plugin.Services;
using GagSpeak.PlayerData.Data;
using GagSpeak.PlayerData.Pairs;
using GagSpeak.PlayerData.PrivateRooms;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using GagSpeak.Toybox.Services;
using GagSpeak.UI;
using GagSpeak.UI.Components;
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
    private readonly MainHub _apiHubMain;
    private readonly ToyboxHub _apiHubToybox;
    private readonly GagManager _gagManager;
    private readonly UiSharedService _uiSharedService;
    private readonly ToyboxVibeService _vibeService;
    private readonly IdDisplayHandler _displayHandler;
    private readonly PairManager _pairManager;
    private readonly PlayerCharacterData _playerManager;
    private readonly ClientConfigurationManager _clientConfigs;
    private readonly ToyboxRemoteService _remoteService;
    private readonly ServerConfigurationManager _serverConfigs;
    private readonly ProfileService _gagspeakProfileManager;
    private readonly OnFrameworkService _frameworkUtils;
    private readonly MoodlesService _moodlesService;
    private readonly PermissionPresetService _presetService;
    private readonly PermActionsComponents _permActionHelpers;

    public UiFactory(ILoggerFactory loggerFactory, GagspeakMediator gagspeakMediator, MainHub apiHubMain, 
        ToyboxHub apiHubToybox, GagManager gagManager, UiSharedService uiSharedService, 
        ToyboxVibeService vibeService, IdDisplayHandler displayHandler, PairManager pairManager, 
        PlayerCharacterData playerManager, ToyboxRemoteService remoteService, 
        ServerConfigurationManager serverConfigs, ProfileService profileManager, OnFrameworkService frameworkUtils,
        ClientConfigurationManager clientConfigs, MoodlesService moodlesService, PermissionPresetService presetService, 
        PermActionsComponents permActionHelpers)
    {
        _loggerFactory = loggerFactory;
        _gagspeakMediator = gagspeakMediator;
        _apiHubMain = apiHubMain;
        _apiHubToybox = apiHubToybox;
        _gagManager = gagManager;
        _uiSharedService = uiSharedService;
        _vibeService = vibeService;
        _displayHandler = displayHandler;
        _pairManager = pairManager;
        _playerManager = playerManager;
        _remoteService = remoteService;
        _serverConfigs = serverConfigs;
        _gagspeakProfileManager = profileManager;
        _frameworkUtils = frameworkUtils;
        _clientConfigs = clientConfigs;
        _moodlesService = moodlesService;
        _presetService = presetService;
        _permActionHelpers = permActionHelpers;
    }

    public RemoteController CreateControllerRemote(PrivateRoom privateRoom)
    {
        return new RemoteController(_loggerFactory.CreateLogger<RemoteController>(), _gagspeakMediator,
            _playerManager, _gagManager, _uiSharedService, _vibeService, _remoteService, _apiHubToybox, privateRoom);
    }

    public KinkPlateUI CreateStandaloneProfileUi(Pair pair)
    {
        return new KinkPlateUI(_loggerFactory.CreateLogger<KinkPlateUI>(), _gagspeakMediator,
            _pairManager, _serverConfigs, _gagspeakProfileManager, _uiSharedService, pair);
    }

    // create a new instance window of the userpair permissions window every time a new pair is selected.
    public PairStickyUI CreateStickyPairPerms(Pair pair, StickyWindowType drawType)
    {
        return new PairStickyUI(_loggerFactory.CreateLogger<PairStickyUI>(), _gagspeakMediator, pair,
            drawType, _frameworkUtils, _clientConfigs, _playerManager, _displayHandler, _uiSharedService,
            _apiHubMain, _pairManager, _moodlesService, _presetService, _permActionHelpers);
    }
}
