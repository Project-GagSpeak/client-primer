using GagSpeak.PlayerData.Pairs;
using GagSpeak.Services.Mediator;
using GagSpeak.UI.Components.UserPairList;
using GagSpeak.UI.Handlers;
using GagSpeak.UI.Permissions;
using GagSpeak.WebAPI;
using System.Collections.Immutable;

namespace GagSpeak.UI;

public class DrawEntityFactory
{
    private readonly ILogger<DrawEntityFactory> _logger;
    private readonly ILoggerFactory _loggerfactory;
    private readonly MainHub _apiHubMain;
    private readonly GagspeakMediator _mediator;
    private readonly SelectPairForTagUi _selectPairForTagUi;
    private readonly UiSharedService _uiSharedService;
    private readonly SelectTagForPairUi _selectTagForPairUi;
    private readonly TagHandler _tagHandler;
    private readonly IdDisplayHandler _uidDisplayHandler;

    public DrawEntityFactory(ILogger<DrawEntityFactory> logger, MainHub apiHubMain,
        IdDisplayHandler uidDisplayHandler, SelectTagForPairUi selectTagForPairUi, 
        GagspeakMediator mediator, ILoggerFactory loggerfactory, TagHandler tagHandler, 
        SelectPairForTagUi selectPairForTagUi, UiSharedService uiSharedService)
    {
        _loggerfactory = loggerfactory;
        _logger = logger;
        _apiHubMain = apiHubMain;
        _uidDisplayHandler = uidDisplayHandler;
        _selectTagForPairUi = selectTagForPairUi;
        _mediator = mediator;
        _tagHandler = tagHandler;
        _selectPairForTagUi = selectPairForTagUi;
        _uiSharedService = uiSharedService;
    }

    public DrawFolderTag CreateDrawTagFolder(string tag, List<Pair> filteredPairs, IImmutableList<Pair> allPairs)
    {
        return new(tag, filteredPairs.Select(u => CreateDrawPair(tag, u)).ToImmutableList(), allPairs,
            _tagHandler, _apiHubMain, _selectPairForTagUi, _uiSharedService, _logger);
    }

    public DrawUserPair CreateDrawPair(string id, Pair user)
    {
        return new DrawUserPair(_loggerfactory.CreateLogger<DrawUserPair>(), id + user.UserData.UID,
            user, _apiHubMain, _uidDisplayHandler, _mediator, _selectTagForPairUi, _uiSharedService);
    }
}
