using GagSpeak.Services.Mediator;
using ImGuiNET;

namespace GagSpeak.UI.UiToybox;

public class ToyboxAlarmManager
{
    private readonly ILogger<ToyboxAlarmManager> _logger;
    private readonly GagspeakMediator _mediator;
    private readonly UiSharedService _uiSharedService;

    public ToyboxAlarmManager(ILogger<ToyboxAlarmManager> logger, GagspeakMediator mediator,
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
