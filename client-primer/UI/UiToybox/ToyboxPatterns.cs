using GagSpeak.Services.Mediator;
using ImGuiNET;

namespace GagSpeak.UI.UiToybox;

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

    public void DrawPatternManagerPanel()
    {
        ImGui.Text("Pattern Manager");
    }
}
