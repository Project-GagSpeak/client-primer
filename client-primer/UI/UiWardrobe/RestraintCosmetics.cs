using GagSpeak.Services.Mediator;
using ImGuiNET;

namespace GagSpeak.UI.UiWardrobe;

public class ToyboxCosmetics
{
    private readonly ILogger<ToyboxCosmetics> _logger;
    private readonly GagspeakMediator _mediator;
    private readonly UiSharedService _uiSharedService;

    public ToyboxCosmetics(ILogger<ToyboxCosmetics> logger, GagspeakMediator mediator,
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
