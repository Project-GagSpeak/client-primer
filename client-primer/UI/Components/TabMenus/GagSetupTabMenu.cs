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
