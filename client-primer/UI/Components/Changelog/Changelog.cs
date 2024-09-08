namespace GagSpeak.UI.Components;
public class Changelog
{
    public List<VersionEntry> Versions { get; private set; } = new List<VersionEntry>();

    public Changelog()
    {
        // append the version information here.
        AddVersionData();
    }

    public VersionEntry VersionEntry(int versionMajor, int versionMinor, int minorUpdate, int updateImprovements)
    {
        var entry = new VersionEntry(versionMajor, versionMinor, minorUpdate, updateImprovements);
        Versions.Add(entry);
        return entry;
    }

    // Add Version Data here.
    private void AddVersionData()
    {
        VersionEntry(0, 7, 4, 0)
            .RegisterFeature("Added a new preset dropdown to bulk set permissions for pairs.")
            .RegisterFeature("DO NOT TEST THIS FEATURE YET, IT IS UNTESTED");
        VersionEntry(0, 7, 3, 3)
            .RegisterBugfix("I Forgot to update the api lol. Please see version 0.7.3.2 for update details.");
        VersionEntry(0, 7, 3, 2)
            .RegisterMain("Added further Preparations for trigger logic implementation on Sunday.")
            .RegisterQol("Optimized the ActionEffectMonitor to only perform logic on action types we care about, and discard the rest instantly. Leading to performance boost.");
        VersionEntry(0, 7, 3, 1)
            .RegisterFeature("Please refer to 0.7.3.0 for full major changes.")
            .RegisterQol("Author fields of downloaded patterns can no longer be edited.")
            .RegisterQol("Downloaded patterns publish buttons are disabled.")
            .RegisterBugfix("Fixed download button issue.")
            .RegisterBugfix("Fixed other minor bugs.");

        VersionEntry(0, 7, 3, 0)
            .RegisterMain("PatternHub Tab is now functional!")
            .RegisterMain("Toybox Patterns Tab now allows you to publish and unpublish patterns to the PatternHub.")
            .RegisterMain("PatternHub Likes, Downloads, and display boxes have been introduced.")
            .RegisterFeature("Upload up to a maximum of 10 patterns a week. (up to 20 for supporters)")
            .RegisterFeature("Like other users patterns and download them to your pattern library.")
            .RegisterFeature("Search for patterns based on various filters, and sort styles.")
            .RegisterQol("Patterns in the toybox tab now display a globe if they are published. (This can be a bit buggy if you remove them from your config, so until i add a new thing for that hang in there and deal with it i guess.")
            .RegisterQol("Popup buttons now have actual transparency, instead of faked transparency.")
            .RegisterBugfix("Fixed incorrect alignment in some parts throughout the plugin.");

        VersionEntry(0, 7, 2, 4)
            .RegisterQol("Lock and Unlock and Apply action dropdowns now properly disable and enable when they should. Alignment also fixed.")
            .RegisterQol("Lock Restraint Set action button now displays a persons locked restraint set while they are locked.")
            .RegisterBugfix("Fixed Owner Padlocks incorrectly checking against submitted passwords over player name matching.");

        VersionEntry(0, 7, 2, 3)
            .RegisterMain("Im a silly goober and tried to open the changelog before it was registered.");

        VersionEntry(0, 7, 2, 2)
            .RegisterMain("Added Changelog Window")
            .RegisterFeature("New PatternHub tab is available in the main window. (Currently has no functionality)")
            .RegisterQol("Helmet and Visor toggles are implemented, but will not work until Buttplug.IO updated to 3.0.2 (somepoint this weekend)")
            .RegisterBugfix("Fixed an issue where slots outside the helmet slot would not unequip the glamourerd item when unequipped via the active slot panel.");
    }
}
