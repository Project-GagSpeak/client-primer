using GagSpeak.Services.Mediator;
using ImGuiNET;

namespace GagSpeak.UI.UiWardrobe;

public class ToyboxPatterns
{
    private readonly ILogger<ToyboxPatterns> _logger;
    private readonly GagspeakMediator _mediator;
    private readonly UiSharedService _uiSharedService;

    public ToyboxPatterns(ILogger<ToyboxPatterns> logger, GagspeakMediator mediator,
        UiSharedService uiSharedService)
    {
        _logger = logger;
        _mediator = mediator;
        _uiSharedService = uiSharedService;
    }

    public void DrawSetsOverview()
    {
        ImGui.Text("Restraints Overview");
    }
}
