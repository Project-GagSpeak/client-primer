using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using GagSpeak.PlayerData.Pairs;
using GagSpeak.Services.Mediator;
using GagSpeak.WebAPI;
using ImGuiNET;
using System.Numerics;

namespace GagSpeak.UI.Components;

/// <summary>
/// The horizontal tab menu for the Achievements UI Display.
/// </summary>
public class AchievementTabsMenu
{
    private readonly GagspeakMediator _mediator;
    private readonly UiSharedService _uiSharedService;

    private SelectedTab _selectedTab = SelectedTab.Generic;

    public AchievementTabsMenu(GagspeakMediator mediator, UiSharedService uiSharedService)
    {
        _mediator = mediator;
        _uiSharedService = uiSharedService;
    }

    public enum SelectedTab
    {
        None,
        Generic,
        Orders,
        Gags,
        Wardrobe,
        Puppeteer,
        Toybox,
        Hardcore,
        Remotes,
        Secrets,
    }

    public SelectedTab TabSelection
    {
        get => _selectedTab;
        set
        {
            _selectedTab = value;
            _mediator.Publish(new AchievementWindowTabChangeMessage(value));
        }
    }

    public void Draw()
    {
        // store information about the bottom bar, to draw our buttons at appropriate sizes
        var availableWidth = ImGui.GetWindowContentRegionMax().X - ImGui.GetWindowContentRegionMin().X;
        var spacing = ImGui.GetStyle().ItemSpacing;
        var buttonX = (availableWidth - (spacing.X * 8)) / 9f;
        var buttonY = _uiSharedService.GetIconButtonSize(FontAwesomeIcon.Pause).Y;
        var buttonSize = new Vector2(buttonX, buttonY);
        var drawList = ImGui.GetWindowDrawList();
        var underlineColor = ImGui.GetColorU32(ImGuiCol.Separator);
        var btncolor = ImRaii.PushColor(ImGuiCol.Button, ImGui.ColorConvertFloat4ToU32(new(0, 0, 0, 0)));

        ImGuiHelpers.ScaledDummy(spacing.Y / 2f);

        DrawTabButton(FontAwesomeIcon.Book, buttonSize, "General", SelectedTab.Generic, TabSelection, drawList, spacing, underlineColor);
        DrawTabButton(FontAwesomeIcon.ClipboardList, buttonSize, "Orders", SelectedTab.Orders, TabSelection, drawList, spacing, underlineColor);
        DrawTabButton(FontAwesomeIcon.CommentDots, buttonSize, "Gags", SelectedTab.Gags, TabSelection, drawList, spacing, underlineColor);
        DrawTabButton(FontAwesomeIcon.ToiletPortable, buttonSize, "Wardrobe", SelectedTab.Wardrobe, TabSelection, drawList, spacing, underlineColor);
        DrawTabButton(FontAwesomeIcon.PersonHarassing, buttonSize, "Puppeteer", SelectedTab.Puppeteer, TabSelection, drawList, spacing, underlineColor);
        DrawTabButton(FontAwesomeIcon.BoxOpen, buttonSize, "Toybox", SelectedTab.Toybox, TabSelection, drawList, spacing, underlineColor);
        DrawTabButton(FontAwesomeIcon.Lock, buttonSize, "Hardcore", SelectedTab.Hardcore, TabSelection, drawList, spacing, underlineColor);
        DrawTabButton(FontAwesomeIcon.Mobile, buttonSize, "Remotes", SelectedTab.Remotes, TabSelection, drawList, spacing, underlineColor);
        DrawTabButton(FontAwesomeIcon.Vault, buttonSize, "Secrets", SelectedTab.Secrets, TabSelection, drawList, spacing, underlineColor);

        // advance to the new line and dispose of the button color.
        ImGui.NewLine();
        btncolor.Dispose();

        if (TabSelection != SelectedTab.None) ImGuiHelpers.ScaledDummy(3f);
        ImGui.Separator();
    }

    private void DrawTabButton(FontAwesomeIcon icon, Vector2 buttonSize, string toolTip, SelectedTab targetTab, 
        SelectedTab currentTab, ImDrawListPtr drawList, Vector2 spacing, uint underlineColor)
    {
        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            // Get initial cursor position before drawing the button
            var initialCursorPos = ImGui.GetCursorScreenPos();

            // Draw the button and update the selected tab if clicked
            if (ImGui.Button(icon.ToIconString(), buttonSize))
                TabSelection = targetTab;

            // Same line positioning for additional UI elements
            ImGui.SameLine();

            // Get cursor position after drawing the button
            var finalCursorPos = ImGui.GetCursorScreenPos();

            // If this tab is selected, draw an underline below the button
            if (currentTab == targetTab)
            {
                drawList.AddLine(
                    initialCursorPos with { Y = initialCursorPos.Y + buttonSize.Y + spacing.Y },
                    finalCursorPos with { Y = finalCursorPos.Y + buttonSize.Y + spacing.Y, X = finalCursorPos.X - spacing.X },
                    underlineColor,
                    2);
            }
        }
        // Draw tooltip if applicable
        UiSharedService.AttachToolTip(toolTip);
    }
}
