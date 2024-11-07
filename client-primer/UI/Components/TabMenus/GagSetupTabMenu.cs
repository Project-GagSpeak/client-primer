namespace GagSpeak.UI.Components;

/// <summary> Tab Menu for the GagSetup UI </summary>
public class GagSetupTabMenu : TabMenuBase
{

    /// <summary> Defines the type of tab selection to use. </summary>
    protected override Type TabSelectionType => typeof(GagSetupTabs.Tabs);

    public GagSetupTabMenu(UiSharedService uiShared) : base(uiShared) { }

    protected override string GetTabDisplayName(Enum tab)
    {
        if (tab is GagSetupTabs.Tabs gagTab)
        {
            return GagSetupTabs.GetTabName(gagTab);
        }

        return "Unknown"; // Fallback for tabs that don't match the expected type.
    }

    protected override bool IsTabDisabled(Enum tab)
    {
        if (tab is GagSetupTabs.Tabs gagTab)
            return gagTab is GagSetupTabs.Tabs.LockPicker;

        return false; // By default, no tabs are disabled
    }

    protected override string GetTabTooltip(Enum tab)
    {
        if (tab is GagSetupTabs.Tabs gagTab)
        {
            // Example: Provide tooltips for each tab
            return gagTab switch
            {
                GagSetupTabs.Tabs.ActiveGags => "Overview of current active gags, and lock info, if any.",
                GagSetupTabs.Tabs.LockPicker => "A WIP Concept that is questionable on if it will ever be added.--SEP--WIP During Open Beta.",
                GagSetupTabs.Tabs.GagStorage => "Manage how your Gags are applied in various ways.",
                _ => string.Empty,
            };
        }

        return string.Empty; // By default, no tooltip is provided
    }
}

public static class GagSetupTabs
{
    public enum Tabs
    {
        ActiveGags,
        LockPicker,
        GagStorage,
    }

    public static string GetTabName(Tabs tab)
    {
        return tab switch
        {
            Tabs.ActiveGags => "Active Gags",
            Tabs.LockPicker => "Lock Picker",
            Tabs.GagStorage => "Gag Storage",
            _ => "None",
        };
    }
}
