using GagSpeak.Services.Mediator;
using ImGuiNET;

namespace GagSpeak.UI.UiToybox;

public class ManageTriggers
{
    private readonly ILogger<ManageTriggers> _logger;
    private readonly GagspeakMediator _mediator;
    private readonly UiSharedService _uiSharedService;

    public ManageTriggers(ILogger<ManageTriggers> logger, GagspeakMediator mediator,
        UiSharedService uiSharedService)
    {
        _logger = logger;
        _mediator = mediator;
        _uiSharedService = uiSharedService;
    }

    public void DrawTriggerManagerPanel()
    {
        ImGui.Text("Trigger Manager Panel");
    }
}
