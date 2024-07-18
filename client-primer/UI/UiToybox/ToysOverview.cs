using GagSpeak.Services.Mediator;
using ImGuiNET;

namespace GagSpeak.UI.UiToybox;

public class ToyboxOverview
{
    private readonly ILogger<ToyboxOverview> _logger;
    private readonly GagspeakMediator _mediator;
    private readonly UiSharedService _uiSharedService;

    public ToyboxOverview(ILogger<ToyboxOverview> logger, GagspeakMediator mediator,
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
