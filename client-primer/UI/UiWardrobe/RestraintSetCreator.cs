using GagSpeak.Services.Mediator;
using ImGuiNET;

namespace GagSpeak.UI.UiWardrobe;

public class RestraintSetCreator
{
    private readonly ILogger<RestraintSetCreator> _logger;
    private readonly GagspeakMediator _mediator;
    private readonly UiSharedService _uiSharedService;

    public RestraintSetCreator(ILogger<RestraintSetCreator> logger, GagspeakMediator mediator,
        UiSharedService uiSharedService)
    {
        _logger = logger;
        _mediator = mediator;
        _uiSharedService = uiSharedService;
    }

    public void DrawRestraintSetCreator()
    {
        ImGui.Text("Create New Restraint Set");
    }
}
