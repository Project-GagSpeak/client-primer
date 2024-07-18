using GagSpeak.Services.Mediator;
using ImGuiNET;

namespace GagSpeak.UI.UiToybox;

public class ManageAlarms
{
    private readonly ILogger<ManageAlarms> _logger;
    private readonly GagspeakMediator _mediator;
    private readonly UiSharedService _uiSharedService;

    public ManageAlarms(ILogger<ManageAlarms> logger, GagspeakMediator mediator,
        UiSharedService uiSharedService)
    {
        _logger = logger;
        _mediator = mediator;
        _uiSharedService = uiSharedService;
    }

    public void DrawAlarmManagerPanel()
    {
        ImGui.Text("Alarm Manager Panel");
    }
}
