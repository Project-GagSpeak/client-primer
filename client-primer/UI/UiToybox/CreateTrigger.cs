using GagSpeak.Services.Mediator;
using ImGuiNET;

namespace GagSpeak.UI.UiToybox;

public class CreateTrigger
{
    private readonly ILogger<CreateTrigger> _logger;
    private readonly GagspeakMediator _mediator;
    private readonly UiSharedService _uiSharedService;

    public CreateTrigger(ILogger<CreateTrigger> logger, GagspeakMediator mediator,
        UiSharedService uiSharedService)
    {
        _logger = logger;
        _mediator = mediator;
        _uiSharedService = uiSharedService;
    }

    public void DrawCreateTriggerPanel()
    {
        ImGui.Text("Create Trigger Panel");
    }
}
