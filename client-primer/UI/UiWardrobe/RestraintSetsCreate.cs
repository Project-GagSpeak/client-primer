using GagSpeak.Services.Mediator;
using ImGuiNET;

namespace GagSpeak.UI.UiWardrobe;

public class RestraintSetCreate
{
    private readonly ILogger<RestraintSetCreate> _logger;
    private readonly GagspeakMediator _mediator;
    private readonly UiSharedService _uiSharedService;

    public RestraintSetCreate(ILogger<RestraintSetCreate> logger, GagspeakMediator mediator,
        UiSharedService uiSharedService)
    {
        _logger = logger;
        _mediator = mediator;
        _uiSharedService = uiSharedService;
    }

    public void DrawRestraintSetCreate()
    {
        ImGui.Text("Create Restraint Set");
    }
}
