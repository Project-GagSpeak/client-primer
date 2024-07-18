using GagSpeak.Services.Mediator;
using ImGuiNET;

namespace GagSpeak.UI.UiToybox;

public class ToysOverview
{
    private readonly ILogger<ToysOverview> _logger;
    private readonly GagspeakMediator _mediator;
    private readonly UiSharedService _uiSharedService;

    public ToysOverview(ILogger<ToysOverview> logger, GagspeakMediator mediator,
        UiSharedService uiSharedService)
    {
        _logger = logger;
        _mediator = mediator;
        _uiSharedService = uiSharedService;
    }

    public void DrawOverviewPanel()
    {
        ImGui.Text("Toys Overview");
    }
}
