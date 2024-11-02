using Dalamud.Plugin.Services;
using GagSpeak.PlayerData.Data;
using GagSpeak.PlayerData.Pairs;
using GagSpeak.PlayerData.PrivateRooms;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using GagSpeak.Services.Textures;
using GagSpeak.Toybox.Services;
using GagSpeak.UI;
using GagSpeak.UI.Components;
using GagSpeak.UI.Handlers;
using GagSpeak.UI.Permissions;
using GagSpeak.UI.Profile;
using GagSpeak.UI.UiRemote;
using GagSpeak.UpdateMonitoring;
using GagSpeak.WebAPI;
using GagspeakAPI.Data;

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
    private readonly KinkPlateService _KinkPlateManager;
    private readonly OnFrameworkService _frameworkUtils;
    private readonly KinkPlateLight _kinkPlateLight;
    private readonly MoodlesService _moodlesService;
    private readonly PermissionPresetService _presetService;
    private readonly CosmeticService _cosmetics;
    private readonly TextureService _textures;
    private readonly PermActionsComponents _permActionHelpers;

    public UiFactory(ILoggerFactory loggerFactory, GagspeakMediator gagspeakMediator, MainHub apiHubMain, 
        ToyboxHub apiHubToybox, GagManager gagManager, UiSharedService uiSharedService, 
        ToyboxVibeService vibeService, IdDisplayHandler displayHandler, PairManager pairManager, 
        PlayerCharacterData playerManager, ToyboxRemoteService remoteService, KinkPlateLight kinkPlateLight,
        KinkPlateService profileManager, OnFrameworkService frameworkUtils, ClientConfigurationManager clientConfigs, 
        MoodlesService moodlesService, PermissionPresetService presetService, CosmeticService cosmetics, 
        TextureService textures, PermActionsComponents permActionHelpers)
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
        _KinkPlateManager = profileManager;
        _kinkPlateLight = kinkPlateLight;
        _frameworkUtils = frameworkUtils;
        _clientConfigs = clientConfigs;
        _moodlesService = moodlesService;
        _presetService = presetService;
        _cosmetics = cosmetics;
        _textures = textures;
        _permActionHelpers = permActionHelpers;
    }

    public RemoteController CreateControllerRemote(PrivateRoom privateRoom)
    {
        return new RemoteController(_loggerFactory.CreateLogger<RemoteController>(), _gagspeakMediator,
            _playerManager, _gagManager, _uiSharedService, _vibeService, _remoteService, _apiHubToybox, privateRoom);
    }

    public KinkPlateUI CreateStandaloneKinkPlateUi(Pair pair)
    {
        return new KinkPlateUI(_loggerFactory.CreateLogger<KinkPlateUI>(), _gagspeakMediator,
            _pairManager, _KinkPlateManager, _cosmetics, _textures, _uiSharedService, pair);
    }

    public KinkPlateLightUI CreateStandaloneKinkPlateLightUi(UserData pairUserData)
    {
        return new KinkPlateLightUI(_loggerFactory.CreateLogger<KinkPlateLightUI>(), _gagspeakMediator,
            _kinkPlateLight, _KinkPlateManager, _pairManager, _uiSharedService, pairUserData);
    }

    // create a new instance window of the userpair permissions window every time a new pair is selected.
    public PairStickyUI CreateStickyPairPerms(Pair pair, StickyWindowType drawType)
    {
        return new PairStickyUI(_loggerFactory.CreateLogger<PairStickyUI>(), _gagspeakMediator, pair,
            drawType, _frameworkUtils, _clientConfigs, _playerManager, _displayHandler, _uiSharedService,
            _apiHubMain, _pairManager, _moodlesService, _presetService, _permActionHelpers);
    }
}
