using GagSpeak.Services.Mediator;
using GagSpeak.WebAPI;
using ImGuiNET;
using OtterGui;

namespace GagSpeak.UI.MainWindow;
public class MainUiPatternHub : DisposableMediatorSubscriberBase
{
    private readonly ApiController _apiController;
    private readonly UiSharedService _uiSharedService;

    public MainUiPatternHub(ILogger<MainUiPatternHub> logger,
        GagspeakMediator mediator, ApiController apiController,
        UiSharedService uiSharedService) : base(logger, mediator)
    {
        _apiController = apiController;
        _uiSharedService = uiSharedService;

        Mediator.Subscribe<DisconnectedMessage>(this, (_) =>
        {
            // clear all the pattern data
        });
    }

    public void DrawPatternHub()
    {
        // grab the content region of the current section
        var CurrentRegion = ImGui.GetContentRegionAvail();

        // center cursor
        ImGuiUtil.Center("Pattern Hub Coming Soon");
    }
}

