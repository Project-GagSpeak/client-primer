using GagSpeak.Services.Mediator;
using ImGuiNET;

namespace GagSpeak.UI.UiWardrobe;

public class ActiveRestraintSet
{
    private readonly ILogger<ActiveRestraintSet> _logger;
    private readonly GagspeakMediator _mediator;
    private readonly UiSharedService _uiSharedService;

    public ActiveRestraintSet(ILogger<ActiveRestraintSet> logger, GagspeakMediator mediator,
        UiSharedService uiSharedService)
    {
        _logger = logger;
        _mediator = mediator;
        _uiSharedService = uiSharedService;
    }

    public void DrawActiveSet()
    {
        ImGui.Text("Active Restraint Set");
    }
}
