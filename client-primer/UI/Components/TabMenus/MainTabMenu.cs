using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Utility;
using ImGuiNET;
using GagspeakAPI.Data.Enum;
using GagSpeak.PlayerData.Pairs;
using GagSpeak.Services.Mediator;
using GagSpeak;
using System.Numerics;
using GagSpeak.WebAPI;

namespace GagSpeak.UI.Components;

/// <summary>
/// Dictates which submenu tab is displayed within the main UI.
/// This will not internally draw anything besides the Icon, their labels, and selection state.
/// <para> 
/// Use the TabSelection variable to reference what tab is active when dictating what to draw 
/// </para>
/// </summary>
public class MainTabMenu
{
    private readonly ApiController _apiController;

    private readonly GagspeakMediator _mediator;

    private readonly PairManager _pairManager;
    private readonly UiSharedService _uiSharedService;
    private string _filter = string.Empty;

    private SelectedTab _selectedTab = SelectedTab.Whitelist;

    public MainTabMenu(GagspeakMediator mediator, ApiController apiController, 
        PairManager pairManager, UiSharedService uiSharedService)
    {
        _mediator = mediator;
        _apiController = apiController;
        _pairManager = pairManager;
        _uiSharedService = uiSharedService;
    }

    public enum SelectedTab
    {
        None,
        Homepage,
        Whitelist,
        Discover,
        MySettings,
    }

    private string Filter
    {
        get => _uiSharedService.SearchFilter;
        set
        {
            if (!string.Equals(_filter, value, StringComparison.OrdinalIgnoreCase))
            {
                _mediator.Publish(new RefreshUiMessage());
            }

            _filter = value;
        }
    }

    public SelectedTab TabSelection
    {
        get => _selectedTab; 
        set
        {
            if (_selectedTab == SelectedTab.Whitelist && value != SelectedTab.Whitelist)
            {
                Filter = string.Empty;
            }

            _selectedTab = value;
        }
    }

    /// <summary>
    /// Draws out the bottom bar tab list for the main window UI
    /// </summary>
    public void Draw()
    {
        // store information about the bottom bar, to draw our buttons at appropriate sizes
        var availableWidth = ImGui.GetWindowContentRegionMax().X - ImGui.GetWindowContentRegionMin().X;
        var spacing = ImGui.GetStyle().ItemSpacing;
        var buttonX = (availableWidth - (spacing.X * 4)) / 4f;
        var buttonY = _uiSharedService.GetIconButtonSize(FontAwesomeIcon.Pause).Y;
        var buttonSize = new Vector2(buttonX, buttonY);
        var drawList = ImGui.GetWindowDrawList();
        var underlineColor = ImGui.GetColorU32(ImGuiCol.Separator);
        var btncolor = ImRaii.PushColor(ImGuiCol.Button, ImGui.ColorConvertFloat4ToU32(new(0, 0, 0, 0)));

        ImGuiHelpers.ScaledDummy(spacing.Y / 2f);

        // Display the homepage button (reference this buttons comments as im not repeating them)
        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            // get current spot on window, and create the button icon
            var x = ImGui.GetCursorScreenPos();
            if (ImGui.Button(FontAwesomeIcon.Home.ToIconString(), buttonSize))
            {
                // if pressed, set tab selection to homepage.
                TabSelection = SelectedTab.Homepage;
            }
            // if this is the tab that is actively selected, we should draw a cute fancy line under it (or change its opacity to be brighter?)
            ImGui.SameLine();
            // grab updated cursor position, and draw the line
            var xAfter = ImGui.GetCursorScreenPos();
            if (TabSelection == SelectedTab.Homepage)
            {
                // to define a drawline, you need the left position, right position, color, and thickness.
                // for our case. x is the left position, xAfter is the right position, underlineColor is the color, and 2 is the thickness.
                // we adjust with the Y to make sure its below the button and not at the top.
                drawList.AddLine(
                    x with { Y = x.Y + buttonSize.Y + spacing.Y },
                    xAfter with { Y = xAfter.Y + buttonSize.Y + spacing.Y, X = xAfter.X - spacing.X },
                    underlineColor, 
                    2);
            }
        }
        UiSharedService.AttachToolTip("Homepage");

        // draw the whitelist button
        ImGui.SameLine();
        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            var x = ImGui.GetCursorScreenPos();
            if (ImGui.Button(FontAwesomeIcon.PeopleArrows.ToIconString(), buttonSize))
            {
                TabSelection = SelectedTab.Whitelist;
            }

            ImGui.SameLine();
            var xAfter = ImGui.GetCursorScreenPos();
            if (TabSelection == SelectedTab.Whitelist)
                drawList.AddLine(x with { Y = x.Y + buttonSize.Y + spacing.Y },
                    xAfter with { Y = xAfter.Y + buttonSize.Y + spacing.Y, X = xAfter.X - spacing.X },
                    underlineColor, 2);
        }
        UiSharedService.AttachToolTip("Whitelist");

        // draw the discoveries button
        ImGui.SameLine();
        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            var x = ImGui.GetCursorScreenPos();
            if (ImGui.Button(FontAwesomeIcon.Compass.ToIconString(), buttonSize))
            {
                TabSelection = SelectedTab.Discover;
            }

            ImGui.SameLine();
            var xAfter = ImGui.GetCursorScreenPos();
            if (TabSelection == SelectedTab.Discover)
                drawList.AddLine(x with { Y = x.Y + buttonSize.Y + spacing.Y },
                    xAfter with { Y = xAfter.Y + buttonSize.Y + spacing.Y, X = xAfter.X - spacing.X },
                    underlineColor, 2);
        }
        UiSharedService.AttachToolTip("Discover & Explore new opportunities!");

        // draw the discoveries button
        ImGui.SameLine();
        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            var x = ImGui.GetCursorScreenPos();
            if (ImGui.Button(FontAwesomeIcon.UserCircle.ToIconString(), buttonSize))
            {
                TabSelection = SelectedTab.MySettings;
            }

            ImGui.SameLine();
            var xAfter = ImGui.GetCursorScreenPos();
            if (TabSelection == SelectedTab.MySettings)
                drawList.AddLine(x with { Y = x.Y + buttonSize.Y + spacing.Y },
                    xAfter with { Y = xAfter.Y + buttonSize.Y + spacing.Y, X = xAfter.X - spacing.X },
                    underlineColor, 2);
        }
        UiSharedService.AttachToolTip("Account User Settings");

        // advance to the new line and dispose of the button color.
        ImGui.NewLine();
        btncolor.Dispose();

        if (TabSelection != SelectedTab.None) ImGuiHelpers.ScaledDummy(3f);
        ImGui.Separator();
    }
}
