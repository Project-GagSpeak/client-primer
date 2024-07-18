using GagSpeak.Services.Mediator;
using ImGuiNET;

namespace GagSpeak.UI.UiWardrobe;

public class RestraintCosmetics
{
    private readonly ILogger<RestraintCosmetics> _logger;
    private readonly GagspeakMediator _mediator;
    private readonly UiSharedService _uiSharedService;

    public RestraintCosmetics(ILogger<RestraintCosmetics> logger, GagspeakMediator mediator,
        UiSharedService uiSharedService)
    {
        _logger = logger;
        _mediator = mediator;
        _uiSharedService = uiSharedService;
    }

    public void DrawCosmetics()
    {
        ImGui.Text("Wardrobe Cosmetics");
    }
}
