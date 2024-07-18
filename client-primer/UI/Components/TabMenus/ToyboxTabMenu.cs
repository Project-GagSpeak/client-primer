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
        ToyManager, // manage connections, active toys, audio sounds, ext.
        PatternManager, // manage, create, or send patterns.
        CreateTrigger, // create a new trigger.
        ManageTriggers, // manage trigger presets
        AlarmManager, // manage, create, or send alarms.
        ToyboxCosmetics, // lets you add custom display effects related to toybox actions.
    }

    public static string GetTabName(Tabs tab)
    {
        return tab switch
        {
            Tabs.ToyManager => "Toys Overview",
            Tabs.PatternManager => "Patterns",
            Tabs.CreateTrigger => "Create Trigger",
            Tabs.ManageTriggers => "Manage Triggers",
            Tabs.AlarmManager => "Alarms",
            Tabs.ToyboxCosmetics => "Toybox Cosmetics",
            _ => "None",
        };
    }
}
