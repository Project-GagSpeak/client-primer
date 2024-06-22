using FFStreamViewer.WebAPI.PlayerData.Pairs;
using FFStreamViewer.WebAPI.Services.Mediator;
using FFStreamViewer.WebAPI.Services.ServerConfiguration;
using FFStreamViewer.WebAPI.UI;
using FFStreamViewer.WebAPI.UI.Components.Popup;
using FFStreamViewer.WebAPI;
using Microsoft.Extensions.Logging;

namespace FFStreamViewer.WebAPI.Services;

public class UiFactory
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly GagspeakMediator _gagspeakMediator;
    private readonly ApiController _apiController;
    private readonly UiSharedService _uiSharedService;
    private readonly PairManager _pairManager;
    private readonly ServerConfigurationManager _serverConfigManager;
    private readonly GagspeakProfileManager _gagspeakProfileManager;

    public UiFactory(ILoggerFactory loggerFactory, GagspeakMediator gagspeakMediator, ApiController apiController,
        UiSharedService uiSharedService, PairManager pairManager, ServerConfigurationManager serverConfigManager,
        GagspeakProfileManager gagspeakProfileManager)
    {
        _loggerFactory = loggerFactory;
        _gagspeakMediator = gagspeakMediator;
        _apiController = apiController;
        _uiSharedService = uiSharedService;
        _pairManager = pairManager;
        _serverConfigManager = serverConfigManager;
        _gagspeakProfileManager = gagspeakProfileManager;
    }

/*    public SyncshellAdminUI CreateSyncshellAdminUi(GroupFullInfoDto dto)
    {
        return new SyncshellAdminUI(_loggerFactory.CreateLogger<SyncshellAdminUI>(), _gagspeakMediator,
            _apiController, _uiSharedService, _pairManager, dto, _performanceCollectorService);
    }

    public StandaloneProfileUi CreateStandaloneProfileUi(Pair pair)
    {
        return new StandaloneProfileUi(_loggerFactory.CreateLogger<StandaloneProfileUi>(), _gagspeakMediator,
            _uiSharedService, _serverConfigManager, _gagspeakProfileManager, _pairManager, pair, _performanceCollectorService);
    }

    public PermissionWindowUI CreatePermissionPopupUi(Pair pair)
    {
        return new PermissionWindowUI(_loggerFactory.CreateLogger<PermissionWindowUI>(), pair,
            _gagspeakMediator, _uiSharedService, _apiController, _performanceCollectorService);
    }*/
}
