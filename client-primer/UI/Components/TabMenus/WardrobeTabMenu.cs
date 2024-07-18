namespace GagSpeak.UI.Components;

/// <summary> Tab Menu for the Wardrobe UI </summary>
public class WardrobeTabMenu : TabMenuBase
{

    /// <summary> Defines the type of tab selection to use. </summary>
    protected override Type TabSelectionType => typeof(WardrobeTabs.Tabs);

    public WardrobeTabMenu() { }

    protected override string GetTabDisplayName(Enum tab)
    {
        if (tab is WardrobeTabs.Tabs wardrobeTab)
        {
            return WardrobeTabs.GetTabName(wardrobeTab);
        }

        return "Unknown"; // Fallback for tabs that don't match the expected type.
    }
}

public static class WardrobeTabs
{
    public enum Tabs
    {
        ActiveSet, // lets you see info about the active set
        SetsOverview, // view the list of your sets and see combined overview.
        CreateNewSet, // interface for creating a new restraint set.
        ModifySet, // interface for editing an existing restraint set. (can heavily rip from add set)
        Cosmetics, // lets you add custom effects to restraint set display in profile.
    }

    public static string GetTabName(Tabs tab)
    {
        return tab switch
        {
            Tabs.ActiveSet => "Active Set",
            Tabs.SetsOverview => "My Outfits",
            Tabs.CreateNewSet => "Create Outfit",
            Tabs.ModifySet => "Edit Outfit",
            Tabs.Cosmetics  => "Outfit Cosmetics",
            _ => "None",
        };
    }
}
