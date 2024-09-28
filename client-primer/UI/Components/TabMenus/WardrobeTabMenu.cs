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
}

public static class WardrobeTabs
{
    public enum Tabs
    {
        ManageSets, // view the list of your sets and see combined overview.
        StruggleSim, // for trying to struggle out of your restraints.
        CursedLoot, // Cursed Bondage Loot.
        ManageMoodles, // Manage the permissions for your Moodles.
    }

    public static string GetTabName(Tabs tab)
    {
        return tab switch
        {
            Tabs.ManageSets => "Restraint Sets",
            Tabs.StruggleSim => "Struggle Sim",
            Tabs.CursedLoot => "Cursed Loot",
            Tabs.ManageMoodles => "Moodles",
            _ => "None",
        };
    }
}
