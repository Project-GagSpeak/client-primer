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
        VersionEntry(0, 8, 5, 1)
            .RegisterMain("Client now properly communicates with server and discord bot for messages and reconnections.");
        VersionEntry(0, 8, 4, 0)
            .RegisterMain("Reworked entire backend for how Client Player Data is stored and synced.")
            .RegisterFeature("You can now 'REAPPLY' to game/Automation, retaining your customizations.")
            .RegisterQol("You can clone restraint sets now.");
        VersionEntry(0, 8, 3, 1)
            .RegisterMain("You can now Clone & Remove restraint sets from the selector menu.")
            .RegisterFeature("Added Button to import your actors current equipment into restraint Appearance.");
        VersionEntry(0, 8, 3, 0)
            .RegisterMain("Customize+ Gag Integration is now fully functional.")
            .RegisterMain("C+ Gags can have set priorities to prioritize one preset over another.")
            .RegisterFeature("A testing simulation of StruggleSim is added, but currently does nothing.")
            .RegisterQol("Presets automatically reapply when you try to disable them.");
        VersionEntry(0, 8, 2, 0)
            .RegisterMain("Action/Spell Triggers can now be used as a Trigger Type.\n(I stress tested it in pvp and alliance raids without crashing, so should be fine)")
            .RegisterMain("Health% Triggers can now be used as a Trigger Type.")
            .RegisterMain("Restraint Monitored Triggers can now be used as a Trigger Type. (Fire Triggers when certain sets are set to certain states [Applied, Locked, ext.])")
            .RegisterMain("GagState Monitored Triggers can now be used as a Trigger Type. (Fire Triggers when certain gags are set to certain states (Lock&Unlock do not work at the moment)")
            .RegisterMain("Added a new Trigger Type: Social Action. (Currently only supports DeathRollLoss)")
            .RegisterMain("Added new Shock Collars Action TriggerActionType. It's limitations reflect your global PiShock ShareCode.")
            .RegisterMain("Added new Restraint Set Application TriggerActionType.")
            .RegisterMain("Added new Gag Application TriggerActionType.")
            .RegisterMain("Added new Moodle Status Application TriggerActionType.")
            .RegisterMain("Added new Moodle Preset Application TriggerActionType.")
            .RegisterFeature("The Commands '/dr' (DeathRoll) and '/dr r' (DeathRoll Reply) are now implemented to help with DeathRoll games.")
            .RegisterQol("The Edit Trigger Header now displayed 'Edit Trigger' instead of the trigger name to avoid text going off the screen.")
            .RegisterQol("Additional Logging options have been added to the debug tab, to hide battery update check and alarm check logs.")
            .RegisterBugfix("(Partially Fixed) issue in where Dropdowns that displayed selected options, were not actually selected. (Still WIP on this)");
        VersionEntry(0, 8, 1, 3)
            .RegisterBugfix("Finally fixed the bug where GagSpeak crashed your game when accessing the Moodles page.")
            .RegisterBugfix("This bug occurred for players who had 0 presets made, and it tried to draw a blank combo box incorrectly.");
        VersionEntry(0, 8, 0, 0)
            .RegisterMain("PiShock Beta Integration is implemented.")
            .RegisterFeature("PiShock Options in the settings window have been appended.")
            .RegisterFeature("PiShock Global & Per-Pair options have been included in the permissions popout and GagSpeak Settings.")
            .RegisterFeature("DO NOT TEST THESE IF YOU ARE NOT WORKING DIRECTLY WITH MY ON DEVELOPMENT. STILL TESTING FEATURES.");
        VersionEntry(0, 7, 5, 1)
            .RegisterMain("Added Toybox Device Debugger")
            .RegisterFeature("Additional Buttons in toybox settings to auto connect to intiface & the vibe server upon server connection have been added.");
        VersionEntry(0, 7, 5, 0)
            .RegisterMain("Full backend restructure to permission Actions. May be buggy, please be patient.")
            .RegisterMain("Added Toybox interactions (ALL UNTESTED ATM)")
            .RegisterBugfix("Fixed /safeword sending your plugin state into limbo.");
        VersionEntry(0, 7, 4, 1)
            .RegisterMain("Force Headgear and Force Visor on gags now FINALLY WORK.");
        VersionEntry(0, 7, 4, 0)
            .RegisterMain("Updated Buttplug Package to v4.0")
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
