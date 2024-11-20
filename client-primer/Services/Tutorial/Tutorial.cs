using Dalamud.Interface;
using Dalamud.Interface.Utility;
using GagSpeak.UI;
using ImGuiNET;
using OtterGui;
using System.Numerics;
using System.Runtime.CompilerServices;
using static OtterGui.Raii.ImRaii;

// A Modified take on OtterGui.Widgets.Tutorial.
// This iteration removes redundant buttons, adds detailed text, and sections.
namespace GagSpeak.Services.Tutorial;

public class Tutorial
{
    private readonly UiSharedService _uiShared;
    public Tutorial(UiSharedService uiShared) => _uiShared = uiShared;

    public record struct Step(string Name, string Info, string MoreInfo, bool Enabled);

    public uint HighlightColor { get; init; } = 0xFF20FFFF;
    public uint BorderColor { get; init; } = 0xD00000FF;
    public string PopupLabel { get; init; } = "Tutorial";
    public int CurrentStep { get; set; } = -1;

    private readonly List<Step> _steps = new();
    private int _waitFrames = 0;
    private bool _showDetails = false;

    public int EndStep => _steps.Count;
    public IReadOnlyList<Step> Steps => _steps;

    public Tutorial AddStep(string name, string info, string moreInfo)
    {
        _steps.Add(new Step(name, info, moreInfo, true));
        return this;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
    public void Open(int step)
    {
        if (step != CurrentStep)
            return;

        OpenWhenMatch();
        --_waitFrames;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
    public void Skip() => CurrentStep = NextId();

    private void OpenWhenMatch()
    {
        var step = Steps[CurrentStep];

        // Skip disabled tutorials.
        if (!step.Enabled)
        {
            CurrentStep = NextId();
            return;
        }

        if (_waitFrames > 0)
            --_waitFrames;
        else if (ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows) && !ImGui.IsPopupOpen(PopupLabel))
            ImGui.OpenPopup(PopupLabel);

        var windowPos = HighlightObject();
        DrawPopup(windowPos, step, NextId());
    }


    /// <summary>
    /// Appends to the DrawList foreground a rectangle surrounding the last drawn item.
    /// </summary>
    private Vector2 HighlightObject()
    {
        ImGui.SetScrollHereX();
        ImGui.SetScrollHereY();
        var offset = ImGuiHelpers.ScaledVector2(5, 4);
        var min = ImGui.GetItemRectMin() - offset;
        var max = ImGui.GetItemRectMax() + offset;
        ImGui.GetForegroundDrawList().PushClipRect(ImGui.GetWindowPos() - offset, ImGui.GetWindowPos() + ImGui.GetWindowSize() + offset);
        ImGui.GetForegroundDrawList().AddRect(min, max, HighlightColor, 5 * ImGuiHelpers.GlobalScale, ImDrawFlags.RoundCornersAll,
            2 * ImGuiHelpers.GlobalScale);
        ImGui.GetForegroundDrawList().PopClipRect();
        return max + new Vector2(ImGuiHelpers.GlobalScale);
    }

    /// <summary>
    /// The tutorial display.
    /// </summary>
    private void DrawPopup(Vector2 pos, Step step, int nextStepVal)
    {
        using var style = DefaultStyle()
            .Push(ImGuiStyleVar.PopupBorderSize, 2 * ImGuiHelpers.GlobalScale)
            .Push(ImGuiStyleVar.PopupRounding, 5 * ImGuiHelpers.GlobalScale);
        using var color = DefaultColors()
            .Push(ImGuiCol.Border, BorderColor)
            .Push(ImGuiCol.PopupBg, 0xFF000000);
        using var font = DefaultFont();
        // Prevent the window from opening outside of the screen.
        var size = ImGuiHelpers.ScaledVector2(350, 0);
        var diff = ImGui.GetWindowSize().X - size.X;
        pos.X = diff < 0 ? ImGui.GetWindowPos().X : Math.Clamp(pos.X, ImGui.GetWindowPos().X, ImGui.GetWindowPos().X + diff);

        // Ensure the header line is visible with a button to go to next.
        pos.Y = Math.Clamp(pos.Y, ImGui.GetWindowPos().Y + ImGui.GetFrameHeightWithSpacing(),
            ImGui.GetWindowPos().Y + ImGui.GetWindowSize().Y - ImGui.GetFrameHeightWithSpacing());

        ImGui.SetNextWindowPos(pos);
        ImGui.SetNextWindowSize(size);
        ImGui.SetNextWindowFocus();
        using var popup = Popup(PopupLabel, ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.Popup);
        if (!popup)
            return;

        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(step.Name);
        ImGui.SameLine(ImGui.GetContentRegionAvail().X - _uiShared.GetIconTextButtonSize(FontAwesomeIcon.ArrowCircleRight, "Next"));
        int? nextValue = _uiShared.IconTextButton(FontAwesomeIcon.ArrowCircleRight, "Next") ? nextStepVal : null;

        ImGui.Separator();
        ImGui.PushTextWrapPos();
        foreach (var text in step.Info.Split('\n', StringSplitOptions.TrimEntries))
        {
            if (text.Length == 0)
                ImGui.Spacing();
            else
                ImGui.TextUnformatted(text);
        }
        ImGui.PopTextWrapPos();

        // Draw the detailed description if the show detailed info button is active.

        ImGui.NewLine();
        nextValue = ImGui.Button("Skip Tutorial") ? EndStep : nextValue;
        ImGuiUtil.HoverTooltip("Skip through the rest of the guide, Completing this Tutorial.");
        ImGui.SameLine();
        ImGui.Checkbox("Show Details", ref _showDetails);
        ImGuiUtil.HoverTooltip("Shows additional details about each step for anyone curious.");

        if (_showDetails)
        {
            ImGui.Separator();
            ImGui.PushTextWrapPos();
            foreach (var text in step.MoreInfo.Split('\n', StringSplitOptions.TrimEntries))
            {
                if (text.Length == 0)
                    ImGui.Spacing();
                else
                    ImGui.TextUnformatted(text);
            }
            ImGui.PopTextWrapPos();
        }

        if (nextValue != null)
        {
            CurrentStep = nextValue.Value;
            _waitFrames = 2;
            ImGui.CloseCurrentPopup();
        }
    }

    private int NextId()
    {
        for (var i = CurrentStep + 1; i < EndStep; ++i)
        {
            if (Steps[i].Enabled)
                return i;
        }

        return EndStep;
    }

    // Obtain the current ID if it is enabled, otherwise the first enabled ID after it.
    public int CurrentEnabledId()
    {
        if (CurrentStep < 0)
            return -1;

        for (var i = CurrentStep; i < EndStep; ++i)
        {
            if (Steps[i].Enabled)
                return i;
        }

        return EndStep;
    }

    // Make sure you have as many tutorials registered as you intend to.
    public Tutorial EnsureSize(int size)
    {
        if (_steps.Count != size)
            throw new Exception("Tutorial size is incorrect.");

        return this;
    }
}
