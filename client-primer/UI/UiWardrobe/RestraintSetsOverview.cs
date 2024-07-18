using GagSpeak.Services.Mediator;
using ImGuiNET;

namespace GagSpeak.UI.UiWardrobe;

public class RestraintSetsOverview
{
    private readonly ILogger<RestraintSetsOverview> _logger;
    private readonly GagspeakMediator _mediator;
    private readonly UiSharedService _uiSharedService;

    public RestraintSetsOverview(ILogger<RestraintSetsOverview> logger, GagspeakMediator mediator,
        UiSharedService uiSharedService)
    {
        _logger = logger;
        _mediator = mediator;
        _uiSharedService = uiSharedService;
    }

    public void DrawSetsOverview()
    {
        ImGui.Text("Restraint Sets Overview");
    }
}
