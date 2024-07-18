using GagSpeak.Services.Mediator;
using ImGuiNET;

namespace GagSpeak.UI.UiToybox;

public class ToyboxTriggerManager
{
    private readonly ILogger<ToyboxTriggerManager> _logger;
    private readonly GagspeakMediator _mediator;
    private readonly UiSharedService _uiSharedService;

    public ToyboxTriggerManager(ILogger<ToyboxTriggerManager> logger, GagspeakMediator mediator,
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
