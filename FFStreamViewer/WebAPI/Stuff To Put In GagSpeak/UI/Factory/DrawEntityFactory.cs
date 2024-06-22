using FFStreamViewer.WebAPI.PlayerData.Pairs;
using FFStreamViewer.WebAPI.Services.Mediator;
using FFStreamViewer.WebAPI.Services.ServerConfiguration;
using FFStreamViewer.WebAPI.UI.Components;
using FFStreamViewer.WebAPI.UI.Handlers;
using System.Collections.Immutable;

namespace FFStreamViewer.WebAPI.UI;

public class DrawEntityFactory
{
    private readonly ILogger<DrawEntityFactory> _logger;
    private readonly ApiController _apiController;
    private readonly GagspeakMediator _mediator;
    private readonly SelectPairForTagUi _selectPairForTagUi;
    private readonly ServerConfigurationManager _serverConfigurationManager;
    private readonly UiSharedService _uiSharedService;
    private readonly SelectTagForPairUi _selectTagForPairUi;
    private readonly TagHandler _tagHandler;
    private readonly IdDisplayHandler _uidDisplayHandler;

    public DrawEntityFactory(ILogger<DrawEntityFactory> logger, ApiController apiController, IdDisplayHandler uidDisplayHandler,
        SelectTagForPairUi selectTagForPairUi, GagspeakMediator mediator,
        TagHandler tagHandler, SelectPairForTagUi selectPairForTagUi,
        ServerConfigurationManager serverConfigurationManager, UiSharedService uiSharedService)
    {
        _logger = logger;
        _apiController = apiController;
        _uidDisplayHandler = uidDisplayHandler;
        _selectTagForPairUi = selectTagForPairUi;
        _mediator = mediator;
        _tagHandler = tagHandler;
        _selectPairForTagUi = selectPairForTagUi;
        _serverConfigurationManager = serverConfigurationManager;
        _uiSharedService = uiSharedService;
    }

    public DrawFolderTag CreateDrawTagFolder(string tag, List<Pair> filteredPairs, IImmutableList<Pair> allPairs)
    {
        return new(tag, filteredPairs.Select(u => CreateDrawPair(tag, u)).ToImmutableList(), allPairs,
            _tagHandler, _apiController, _selectPairForTagUi, _uiSharedService);
    }

    public DrawUserPair CreateDrawPair(string id, Pair user)
    {
        return new DrawUserPair(id + user.UserData.UID, user, _apiController, _uidDisplayHandler,
            _mediator, _selectTagForPairUi, _serverConfigurationManager, _uiSharedService);
    }
}
