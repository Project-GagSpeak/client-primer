using ImGuiNET;

namespace GagSpeak.UI.Components;
public abstract class TabMenuBase
{
    /// <summary>
    /// The type of the tab selection enum
    /// </summary>
    protected abstract Type TabSelectionType { get; }

    /// <summary>
    /// The currently selected tab (Does so by storing the enum equivalent value of the selected item.
    /// </summary>
    public Enum SelectedTab { get; set; } = null!;

    /// <summary>
    /// Abstract method to get the display name of the tab
    /// </summary>
    protected abstract string GetTabDisplayName(Enum tab);

    /// <summary>
    /// Virtual boolean that determines if a particular tab should be displayed or not
    /// </summary>
    protected virtual bool ShouldDisplayTab(Enum tab)
    {
        // By default, all tabs are displayed. But can be configured to hide tabs at will.
        return true;
    }

    /// <summary>
    /// Draws out selectable list to determine what draws on the right half of the UI
    /// </summary>
    public void DrawSelectableTabMenu()
    {
        foreach (var window in Enum.GetValues(TabSelectionType))
        {
            if (window.ToString() == "None" || !ShouldDisplayTab((Enum)window)) continue;

            var displayName = GetTabDisplayName((Enum)window);

            if (ImGui.Selectable($"{displayName}", window.Equals(SelectedTab)))
            {
                SelectedTab = (Enum)window;
            }
        }
        ImGui.Spacing();
    }
}

