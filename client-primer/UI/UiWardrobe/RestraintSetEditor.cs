using GagSpeak.Services.Mediator;
using ImGuiNET;

namespace GagSpeak.UI.UiWardrobe;

public class RestraintSetEditor
{
    private readonly ILogger<RestraintSetEditor> _logger;
    private readonly GagspeakMediator _mediator;
    private readonly UiSharedService _uiSharedService;

    public RestraintSetEditor(ILogger<RestraintSetEditor> logger, GagspeakMediator mediator,
        UiSharedService uiSharedService)
    {
        _logger = logger;
        _mediator = mediator;
        _uiSharedService = uiSharedService;
    }

    public void DrawRestraintSetEditor()
    {
        ImGui.Text("Restraint Set Editor");
    }
}
