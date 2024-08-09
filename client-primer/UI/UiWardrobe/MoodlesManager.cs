using GagSpeak.PlayerData.Handlers;
using GagSpeak.Services.Mediator;
using ImGuiNET;

namespace GagSpeak.UI.UiWardrobe;

public class MoodlesManager : DisposableMediatorSubscriberBase
{
    private readonly UiSharedService _uiSharedService;
    private readonly WardrobeHandler _handler;

    public MoodlesManager(ILogger<MoodlesManager> logger,
        GagspeakMediator mediator, UiSharedService uiSharedService,
        WardrobeHandler handler) : base(logger, mediator)
    {
        _uiSharedService = uiSharedService;
        _handler = handler;
    }

    public void DrawMoodlesManager()
    {
        ImGui.Text("This Section is still in development, come back later!");
    }
}
