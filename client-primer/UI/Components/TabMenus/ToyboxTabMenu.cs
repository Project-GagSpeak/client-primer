namespace GagSpeak.UI.Components;

/// <summary> Tab Menu for the GagSetup UI </summary>
public class ToyboxTabMenu : TabMenuBase
{
    /// <summary> Defines the type of tab selection to use. </summary>
    protected override Type TabSelectionType => typeof(ToyboxTabs.Tabs);

    public ToyboxTabMenu(UiSharedService uiShared) : base(uiShared) { }

    protected override string GetTabDisplayName(Enum tab)
    {
        if (tab is ToyboxTabs.Tabs toyboxTab)
        {
            return ToyboxTabs.GetTabName(toyboxTab);
        }

        return "Unknown"; // Fallback for tabs that don't match the expected type.
    }

    protected override bool IsTabDisabled(Enum tab)
    {
        if (tab is ToyboxTabs.Tabs toyboxTab)
            return toyboxTab == ToyboxTabs.Tabs.VibeServer;

        return false; // By default, no tabs are disabled
    }

    protected override string GetTabTooltip(Enum tab)
    {
        if (tab is ToyboxTabs.Tabs toyboxTab)
        {
            // Example: Provide tooltips for each tab
            return toyboxTab switch
            {
                ToyboxTabs.Tabs.ToyOverview => "Manage connections to your sex toys or simulated vibrator options.",
                ToyboxTabs.Tabs.VibeServer => "Create, invite, or join other private rooms, control other pairs sex toys live.--SEP--Under Construction during Open Beta",
                ToyboxTabs.Tabs.PatternManager => "Manage or upload your patterns.",
                ToyboxTabs.Tabs.TriggerManager => "Manage your triggers.",
                ToyboxTabs.Tabs.AlarmManager => "Manage your Alarms.",
                _ => string.Empty,
            };
        }

        return string.Empty; // By default, no tooltip is provided
    }
}

public static class ToyboxTabs
{
    public enum Tabs
    {
        ToyOverview, // manage connections, active toys, audio sounds, ext.
        VibeServer, // connect to hub, create groups, invite others, access instanced vibrators for each
        PatternManager, // manage, create, or send patterns.
        TriggerManager, // create a new trigger.
        AlarmManager, // manage, create, or send alarms.
    }

    public static string GetTabName(Tabs tab)
    {
        return tab switch
        {
            Tabs.ToyOverview => "Overview",
            Tabs.VibeServer => "Vibe Server",
            Tabs.PatternManager => "Patterns",
            Tabs.TriggerManager => "Triggers",
            Tabs.AlarmManager => "Alarms",
            _ => "None",
        };
    }
}
