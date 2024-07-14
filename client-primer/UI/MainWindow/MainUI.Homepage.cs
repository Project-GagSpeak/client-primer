using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Utility;
using GagSpeak.PlayerData.Pairs;
using GagSpeak.Services.Mediator;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.WebAPI.Utils;
using GagSpeak.UI.Components;
using GagSpeak.UI.Handlers;
using ImGuiNET;
using System.Collections.Immutable;
using System.Globalization;
using System.Numerics;
using System.Reflection;
using OtterGuiInternal.Structs;
using GagSpeak.UI.Permissions;
using GagspeakAPI.Data.Enum;
using GagSpeak.GagspeakConfiguration;
using GagSpeak;
using GagSpeak.UI;
using GagSpeak.WebAPI;
using static FFXIVClientStructs.FFXIV.Client.LayoutEngine.LayoutManager;
using GagSpeak.UI.Profile;

namespace GagSpeak.UI.MainWindow;

/// <summary> 
/// Partial class responsible for drawing the homepage element of the main UI.
/// The homepage will provide the player with links to open up other windows in the plugin via components.
/// </summary>
public partial class MainWindowUI
{
    /// <summary>
    /// Main Draw function for the Whitelist/Contacts tab of the main UI
    /// </summary>
    private float DrawHomepageSection()
    {
        // get the width of the window content region we set earlier
        _windowContentWidth = UiSharedService.GetWindowContentRegionWidth();
        float pairlistEnd = 0;

        var availableWidth = ImGui.GetWindowContentRegionMax().X - ImGui.GetWindowContentRegionMin().X;
        var spacing = ImGui.GetStyle().ItemSpacing;

        // span the height of the pair list to be the height of the window minus the transfer section, which we are removing later anyways.
        var ySize = _tabBarHeight == 0
            ? 1
            : ImGui.GetWindowContentRegionMax().Y - ImGui.GetWindowContentRegionMin().Y
                + ImGui.GetTextLineHeight() - ImGui.GetStyle().WindowPadding.Y - ImGui.GetStyle().WindowBorderSize - _tabBarHeight - ImGui.GetCursorPosY();

        // begin the list child, with no border and of the height calculated above
        ImGui.BeginChild("homepageModuleListings", new Vector2(_windowContentWidth, ySize), border: false);

        // draw the buttons (basic for now) for access to other windows
        DrawHomepageModules(availableWidth, spacing.X);

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
    private void DrawHomepageModules(float availableWidth, float spacingX)
    {
        var buttonX = (availableWidth - spacingX) / 2f;

        // My Remote
        if (_uiSharedService.IconTextButton(FontAwesomeIcon.WaveSquare, "Lovense Remote Interface", buttonX))
        {
            // possibly use a factory to generate pair-unique remotes, or, if you want to incorporate multiple use functionality,
            // you can configure users in the remote menu.
            Mediator.Publish(new UiToggleMessage(typeof(LovenseRemoteUI)));
        }
        UiSharedService.AttachToolTip("Use your personal Lovense Remote to send vibrations to yourself or other pairs ");
        
        // Opens the Orders Module UI
        ImGui.SameLine();
        if (_uiSharedService.IconTextButton(FontAwesomeIcon.ClipboardList, "Orders Interface", buttonX))
        {
            Mediator.Publish(new UiToggleMessage(typeof(OrdersUI)));
        }
        UiSharedService.AttachToolTip("View your active Orders or setup orders for others");

        // Opens the Gags Status Interface UI
        ImGui.SameLine();
        if (_uiSharedService.IconTextButton(FontAwesomeIcon.CommentSlash, "Gags Interface", buttonX))
        {
            Mediator.Publish(new UiToggleMessage(typeof(GagSetupUI)));
        }
        UiSharedService.AttachToolTip("View and analyze your generated character data");

        // Opens the Wardrobe Module UI
        ImGui.SameLine();
        if (_uiSharedService.IconTextButton(FontAwesomeIcon.ToiletPortable, "Wardrobe Interface", buttonX))
        {
            Mediator.Publish(new UiToggleMessage(typeof(WardrobeUI)));
        }
        UiSharedService.AttachToolTip("View and analyze your generated character data");

        // Opens the Puppeteer Module UI
        ImGui.SameLine();
        if (_uiSharedService.IconTextButton(FontAwesomeIcon.PersonHarassing, "Puppeteer Interface", buttonX))
        {
            Mediator.Publish(new UiToggleMessage(typeof(PuppeteerUI)));
        }
        UiSharedService.AttachToolTip("View and analyze your generated character data");

        // Opens the Toybox Module UI
        ImGui.SameLine();
        if (_uiSharedService.IconTextButton(FontAwesomeIcon.BoxOpen, "Toybox Interface", buttonX))
        {
            Mediator.Publish(new UiToggleMessage(typeof(ToyboxUI)));
        }
        UiSharedService.AttachToolTip("View and analyze your generated character data");
    }
}
