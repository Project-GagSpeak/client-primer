using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin;
using GagSpeak.Services.Mediator;
using GagSpeak.UI.UiGagSetup;
using GagSpeak.UI.UiOrders;
using GagSpeak.UI.UiPuppeteer;
using GagSpeak.UI.UiRemote;
using GagSpeak.UI.UiToybox;
using GagSpeak.UI.UiWardrobe;
using GagSpeak.UpdateMonitoring;
using GagSpeak.Utils;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using OtterGui.Classes;
using System.Numerics;

namespace GagSpeak.UI.MainWindow;

/// <summary> 
/// Partial class responsible for drawing the homepage element of the main UI.
/// The homepage will provide the player with links to open up other windows in the plugin via components.
/// </summary>
public class MainUiHomepage : DisposableMediatorSubscriberBase
{
    private readonly UiSharedService _uiShared;
    private readonly EmoteMonitor _emoteMonitor;
    public MainUiHomepage(ILogger<MainUiHomepage> logger, GagspeakMediator mediator,
        UiSharedService uiSharedService, EmoteMonitor emoteMonitor) : base(logger, mediator)
    {
        _uiShared = uiSharedService;
        _emoteMonitor = emoteMonitor;
    }

    public float DrawHomepageSection(IDalamudPluginInterface pi)
    {
        // get the width of the window content region we set earlier
        var _windowContentWidth = UiSharedService.GetWindowContentRegionWidth();
        float pairlistEnd = 0;

        var availableWidth = ImGui.GetWindowContentRegionMax().X - ImGui.GetWindowContentRegionMin().X;
        var spacing = ImGui.GetStyle().ItemSpacing;

        // span the height of the pair list to be the height of the window minus the transfer section, which we are removing later anyways.
        var ySize = ImGui.GetWindowContentRegionMax().Y - ImGui.GetWindowContentRegionMin().Y
            + ImGui.GetTextLineHeight() - ImGui.GetStyle().WindowPadding.Y - ImGui.GetStyle().WindowBorderSize - ImGui.GetCursorPosY();

        // begin the list child, with no border and of the height calculated above
        ImGui.BeginChild("homepageModuleListings", new Vector2(_windowContentWidth, ySize), border: false);

        // draw the buttons (basic for now) for access to other windows
        DrawHomepageModules(availableWidth, spacing.X, pi);

        // then end the list child
        ImGui.EndChild();

        // fetch the cursor position where the footer is
        pairlistEnd = ImGui.GetCursorPosY();

        // return a push to the footer to know where to draw our bottom tab bar
        return ImGui.GetCursorPosY() - pairlistEnd - ImGui.GetTextLineHeight();
    }

    /// <summary>
    /// Draws the list of pairs belonging to the client user.
    /// </summary>
    private void DrawHomepageModules(float availableWidth, float spacingX, IDalamudPluginInterface pi)
    {
        // get the width of the window content region we set earlier
        var _windowContentWidth = UiSharedService.GetWindowContentRegionWidth();
        var _spacingX = ImGui.GetStyle().ItemSpacing.X;
        var buttonX = (availableWidth - spacingX);

        // My Remote
        if (_uiShared.IconTextButton(FontAwesomeIcon.WaveSquare, "Lovense Remote Interface", buttonX))
        {
            // possibly use a factory to generate pair-unique remotes, or, if you want to incorporate multiple use functionality,
            // you can configure users in the remote menu.
            Mediator.Publish(new UiToggleMessage(typeof(RemotePersonal)));
            UnlocksEventManager.AchievementEvent(UnlocksEvent.RemoteOpened);
        }
        UiSharedService.AttachToolTip("Use your personal Lovense Remote to send vibrations to yourself or other pairs ");

        // Opens the Orders Module UI
        if (_uiShared.IconTextButton(FontAwesomeIcon.ClipboardList, "Orders Interface", buttonX))
        {
            Mediator.Publish(new UiToggleMessage(typeof(OrdersUI)));
        }

        // Opens the Gags Status Interface UI
        if (_uiShared.IconTextButton(FontAwesomeIcon.CommentSlash, "Gags Interface", buttonX))
        {
            Mediator.Publish(new UiToggleMessage(typeof(GagSetupUI)));
        }

        // Opens the Wardrobe Module UI
        if (_uiShared.IconTextButton(FontAwesomeIcon.ToiletPortable, "Wardrobe Interface", buttonX))
        {
            Mediator.Publish(new UiToggleMessage(typeof(WardrobeUI)));
        }

        // Opens the Puppeteer Module UI
        if (_uiShared.IconTextButton(FontAwesomeIcon.PersonHarassing, "Puppeteer Interface", buttonX))
        {
            Mediator.Publish(new UiToggleMessage(typeof(PuppeteerUI)));
        }

        // Opens the Toybox Module UI
        if (_uiShared.IconTextButton(FontAwesomeIcon.BoxOpen, "Toybox Interface", buttonX))
        {
            Mediator.Publish(new UiToggleMessage(typeof(ToyboxUI)));
        }

        // Opens the Achievements Window
        if (_uiShared.IconTextButton(FontAwesomeIcon.Trophy, "Achievements", buttonX))
        {
            Mediator.Publish(new UiToggleMessage(typeof(AchievementsUI)));
        }
    }
}
