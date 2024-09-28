namespace GagSpeak.UI.Components;

/// <summary> Tab Menu for the GagSetup UI </summary>
public class ToyboxTabMenu : TabMenuBase
{
    /// <summary> Defines the type of tab selection to use. </summary>
    protected override Type TabSelectionType => typeof(ToyboxTabs.Tabs);

    public ToyboxTabMenu() { }

    protected override string GetTabDisplayName(Enum tab)
    {
        if (tab is ToyboxTabs.Tabs toyboxTab)
        {
            return ToyboxTabs.GetTabName(toyboxTab);
        }

        return "Unknown"; // Fallback for tabs that don't match the expected type.
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
