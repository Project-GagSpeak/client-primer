using GagSpeak.PlayerData.Handlers;

namespace GagSpeak.UI.Components;

/// <summary> Tab Menu for the Wardrobe UI </summary>
public class WardrobeTabMenu : TabMenuBase
{
    private readonly WardrobeHandler _wardrobeHandler;

    /// <summary> Defines the type of tab selection to use. </summary>
    protected override Type TabSelectionType => typeof(WardrobeTabs.Tabs);

    public WardrobeTabMenu(WardrobeHandler wardrobeHandler)
    {
        _wardrobeHandler = wardrobeHandler;
    }
    protected override string GetTabDisplayName(Enum tab)
    {
        if (tab is WardrobeTabs.Tabs wardrobeTab)
        {
            return WardrobeTabs.GetTabName(wardrobeTab);
        }

        return "Unknown"; // Fallback for tabs that don't match the expected type.
    }

    protected override bool ShouldDisplayTab(Enum tab)
    {
        if (tab is WardrobeTabs.Tabs.ActiveSet && tab.Equals(WardrobeTabs.Tabs.ActiveSet))
        {
            // Hide "Active Set" tab if ActiveSet is null.
            return _wardrobeHandler.ActiveSet != null;
        }
        return base.ShouldDisplayTab(tab);
    }

}

public static class WardrobeTabs
{
    public enum Tabs
    {
        ActiveSet, // lets you see info about the active set
        ManageSets, // view the list of your sets and see combined overview.
        StruggleSim, // for trying to struggle out of your restraints.
        ManageMoodles, // Manage the permissions for your Moodles.
        Cosmetics, // lets you add custom effects to restraint set display in profile.
    }

    public static string GetTabName(Tabs tab)
    {
        return tab switch
        {
            Tabs.ActiveSet => "Active Set",
            Tabs.ManageSets => "Restraint Sets",
            Tabs.StruggleSim => "Struggle Sim",
            Tabs.ManageMoodles => "Moodles",
            Tabs.Cosmetics  => "Outfit Cosmetics",
            _ => "None",
        };
    }
}
