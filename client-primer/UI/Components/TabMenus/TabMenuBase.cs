using ImGuiNET;

namespace GagSpeak.UI.Components;
public abstract class TabMenuBase
{
    /// <summary>
    /// The type of the tab selection enum
    /// </summary>
    protected abstract Type TabSelectionType { get; }

    /// <summary>
    /// The currently selected tab
    /// </summary>
    public Enum SelectedTab { get; set; } = null;

    /// <summary>
    /// Draws out selectable list to determine what draws on the right half of the UI
    /// </summary>
    public void DrawSelectableTabMenu()
    {
        foreach (var window in Enum.GetValues(TabSelectionType))
        {
            if (window.ToString() == "None") continue;

            if (ImGui.Selectable($"{window}", window.Equals(SelectedTab)))
            {
                SelectedTab = (Enum)window;
            }
        }

        ImGui.Spacing();
    }
}

