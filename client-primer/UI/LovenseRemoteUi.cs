using GagSpeak.Services.Mediator;
using System.Numerics;

namespace GagSpeak.UI;

public class LovenseRemoteUI : WindowMediatorSubscriberBase
{
    private readonly UiSharedService _uiSharedService;

    public LovenseRemoteUI(ILogger<WardrobeUI> logger, GagspeakMediator mediator,
        UiSharedService uiSharedService) : base(logger, mediator, "Lovense Remote UI")
    {
        // define initial size of window and to not respect the close hotkey.
        this.SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(375, 330),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
        RespectCloseHotkey = false;
    }
    // perhaps migrate the opened selectable for the UIShared service so that other trackers can determine if they should refresh / update it or not.
    // (this is not yet implemented, but we can modify it later when we need to adapt)

    protected override void PreDrawInternal()
    {
        // include our personalized theme for this window here if we have themes enabled.
    }
    protected override void PostDrawInternal()
    {
        // include our personalized theme for this window here if we have themes enabled.
    }
    protected override void DrawInternal()
    {
        _uiSharedService.BigText("Lovense Remote Coming soon");
    }
}
