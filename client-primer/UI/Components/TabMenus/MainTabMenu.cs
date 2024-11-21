using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using GagSpeak.PlayerData.Pairs;
using GagSpeak.Services;
using GagSpeak.Services.Mediator;
using GagSpeak.Services.Tutorial;
using GagSpeak.WebAPI;
using ImGuiNET;
using Lumina.Excel.Sheets;
using System.Numerics;

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
    private readonly MainHub _apiHubMain;
    private readonly GagspeakMediator _mediator;
    private readonly PairManager _pairManager;
    private readonly UiSharedService _uiSharedService;
    private readonly TutorialService _guides;

    private SelectedTab _selectedTab = SelectedTab.Homepage;

    public MainTabMenu(GagspeakMediator mediator, MainHub apiHubMain,
        PairManager pairManager, UiSharedService uiSharedService, TutorialService tutorialService)
    {
        _mediator = mediator;
        _apiHubMain = apiHubMain;
        _pairManager = pairManager;
        _uiSharedService = uiSharedService;
        _guides = tutorialService;
    }

    public enum SelectedTab
    {
        None,
        Homepage,
        Whitelist,
        PatternHub,
        GlobalChat,
        MySettings,
    }

    public SelectedTab TabSelection
    {
        get => _selectedTab;
        set
        {
            _selectedTab = value;
            _mediator.Publish(new MainWindowTabChangeMessage(value));
        }
    }

    /// <summary>
    /// Draws out the bottom bar tab list for the main window UI
    /// </summary>
    public void Draw()
    {
        var pos = ImGui.GetWindowPos();
        var size = ImGui.GetWindowSize();
        // store information about the bottom bar, to draw our buttons at appropriate sizes
        var availableWidth = ImGui.GetWindowContentRegionMax().X - ImGui.GetWindowContentRegionMin().X;
        var spacing = ImGui.GetStyle().ItemSpacing;
        var buttonX = (availableWidth - (spacing.X * 4)) / 5f;
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
        _guides.OpenTutorial(TutorialType.MainUi, StepsMainUi.Homepage, pos, size);

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
        _guides.OpenTutorial(TutorialType.MainUi, StepsMainUi.ToWhitelistPage, pos, size, () => TabSelection = SelectedTab.Whitelist);
        _guides.OpenTutorial(TutorialType.MainUi, StepsMainUi.Whitelist, pos, size);

        // draw the pattern hub button
        ImGui.SameLine();
        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            var x = ImGui.GetCursorScreenPos();
            if (ImGui.Button(FontAwesomeIcon.Compass.ToIconString(), buttonSize))
            {
                TabSelection = SelectedTab.PatternHub;
            }

            ImGui.SameLine();
            var xAfter = ImGui.GetCursorScreenPos();
            if (TabSelection == SelectedTab.PatternHub)
                drawList.AddLine(x with { Y = x.Y + buttonSize.Y + spacing.Y },
                    xAfter with { Y = xAfter.Y + buttonSize.Y + spacing.Y, X = xAfter.X - spacing.X },
                    underlineColor, 2);
        }
        UiSharedService.AttachToolTip("Discover Patterns from the community!");
        _guides.OpenTutorial(TutorialType.MainUi, StepsMainUi.PatternHub, pos, size, () => TabSelection = SelectedTab.PatternHub);


        // draw the global chat button
        ImGui.SameLine();
        var xChat = ImGui.GetCursorScreenPos();
        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            if (ImGui.Button(FontAwesomeIcon.Comments.ToIconString(), buttonSize))
            {
                TabSelection = SelectedTab.GlobalChat;
            }
        }
        UiSharedService.AttachToolTip("Meet & Chat with others in a cross-region chat!");
        _guides.OpenTutorial(TutorialType.MainUi, StepsMainUi.ToGlobalChat, pos, size, () => TabSelection = SelectedTab.GlobalChat);


        // Calculate position for the message count text
        var messageCountPosition = new Vector2(xChat.X + buttonSize.X / 2, xChat.Y - spacing.Y);

        ImGui.SameLine();
        var xChatAfter = ImGui.GetCursorScreenPos();
        if (TabSelection == SelectedTab.GlobalChat)
            drawList.AddLine(xChat with { Y = xChat.Y + buttonSize.Y + spacing.Y },
                xChatAfter with { Y = xChatAfter.Y + buttonSize.Y + spacing.Y, X = xChatAfter.X - spacing.X },
                underlineColor, 2);

        // Assume `newMessageCount` is an integer holding the count of new messages
        if(DiscoverService.NewMessages > 0)
        {
            // Display new message count above the icon
            var messageText = DiscoverService.NewMessages > 99 ? "99+" : DiscoverService.NewMessages.ToString();
            UiSharedService.DrawOutlinedFont(drawList, messageText, messageCountPosition, UiSharedService.Color(ImGuiColors.ParsedGold), 0xFF000000, 1);
        }

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
        _guides.OpenTutorial(TutorialType.MainUi, StepsMainUi.ToAccountPage, pos, size, () => TabSelection = SelectedTab.MySettings);

        // advance to the new line and dispose of the button color.
        ImGui.NewLine();
        btncolor.Dispose();

        if (TabSelection != SelectedTab.None) ImGuiHelpers.ScaledDummy(3f);
        ImGui.Separator();
    }
}
