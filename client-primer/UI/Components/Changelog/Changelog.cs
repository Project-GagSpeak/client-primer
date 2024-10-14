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
        VersionEntry(0, 9, 3, 6)
            .RegisterMain("Properly Syncronize Achievement Data on Connection, Reconnection, Pause, Logout, Game Close, and Unhandled Exceptions.");
        VersionEntry(0, 9, 3, 5)
            .RegisterBugfix("Fixed the damn timer padlock bug. I was missing one conditional *cries*");
        VersionEntry(0, 9, 3, 4)
            .RegisterFeature("You can now shift + middle click to set items from penumbra to expanded cursed item windows.");
        VersionEntry(0, 9, 3, 3)
            .RegisterMain("Gag Padlock Images now properly display")
            .RegisterFeature("All Moodles assignments and recalculations have been reworked to support layered bondage.")
            .RegisterFeature("Moodles have been heavily reworked for maximum precision.")
            .RegisterBugfix("Fixed issues where moodles would not properly be removed when they should have been.");
        VersionEntry(0, 9, 3, 2)
            .RegisterBugfix("Cursed Items now toggle the attached mod if they should.");
        VersionEntry(0, 9, 3, 1)
            .RegisterFeature("Added in the permissions for devotional lock allowances")
            .RegisterQol("Added images for devotionalLocks and Mimic Locks");
        VersionEntry(0, 9, 3, 0)
            .RegisterMain("Cursed Loot Gags are now functional.")
            .RegisterMain("Majority of padlock fixes have been made.")
            .RegisterFeature("DEVOTIONAL PADLOCKS are now a new type of padlock.")
            .RegisterFeature("Mimic Padlocks are now a kind of padlock that can be applied by Mimics")
            .RegisterQol("Your Gag's Lock in the ActiveGagsPanel is now colored base on the padlock kind.")
            .RegisterBugfix("Your game no longer will crash when opening a coffer with no cursed items in your pool.")
            .RegisterBugfix("Padlocks such as Owner Padlocks can now properly be removed.")
            .RegisterBugfix("Padlocks applied by other players can now be removed from active gags panel if they normally should be.");
        VersionEntry(0, 9, 2, 0)
            .RegisterMain("Cursed loot functionality has been readded to the plugin.")
            .RegisterMain("The Appearance Updater has recieved an overhaul to work better.")
            .RegisterFeature("Cursed Loot UI Has been completely revamped for individual items.")
            .RegisterFeature("Cursed Loot can be either gags or equipment items. (Gags Non-Functional ATM)")
            .RegisterFeature("Appearance Updates have been overhauled and are now 15x faster than before.")
            .RegisterFeature("Application inconsistancies have been fixed.")
            .RegisterQol("Updates happen now as: RESTRAINT -> GAG -> CURSED ITEM -> BLINDFOLD")
            .RegisterBugfix("Fixed cases where some calls updated things out of order.")
            .RegisterBugfix("Fixed blindfolds taking forever to apply.")
            .RegisterBugfix("Fixed game crashing on closing game");
        VersionEntry(0, 9, 1, 0)
            .RegisterMain("I HIGHLY RECOMMEND YOU SET YOUR CURSED LOOT CHANCE TO 0%")
            .RegisterFeature("New overhaul rework for cursed loot being implemented. Still IN PROGRESS.")
            .RegisterFeature("You should be able to set up cursed items now in the cursed loot tab. Report any bugs you find.");
        VersionEntry(0, 9, 0, 2)
            .RegisterMain("Added the rest of the event triggers into the achievement monitors.")
            .RegisterFeature("Achievements now accurately display progression like they should.")
            .RegisterBugfix("Fixed bugs where satisfying certain conditions caused crashes, these should now be handled within the event manager to prevent crashes.");
        VersionEntry(0, 9, 0, 1)
            .RegisterMain("WARNING: 0.9.0.0 CURRENTLY INTRODUCED SOME DX11 CRASHES FROM PERFORMING CERTAIN ACTIONS.")
            .RegisterFeature("I've done my best to catch all conditions that trigger dx11 crashes and contained them.")
            .RegisterFeature("I've plugged in more event handles for triggers on achievement conditions.")
            .RegisterFeature("I've applied trigger conditions for 20/37 total triggers, will do more when i wake up tomorrow.")
            .RegisterBugfix("There are a lots, i know. Im panicking because of the deadline for open beta. Be gentle. Thanks.");
        VersionEntry(0, 9, 0, 0)
            .RegisterMain("Achievements are now implemented into GagSpeak.")
            .RegisterFeature("The Achievements Menu has been added to the homepage.")
            .RegisterFeature("Achievements sync with the server every 30m, and retain data through server interrupts and restarts.")
            .RegisterQol("Note: Most Achievements don't work yet, i need to heavily test out the actions with other beta testers to get it right");
        VersionEntry(0, 8, 8, 2)
            .RegisterBugfix("Fixed crash when getting cursed loot due to a bad index match.");
        VersionEntry(0,8,8,0)
            .RegisterMain("Cursed Bondage Loot (Cursed Dungeon Loot) Is now a component of the Wardrobe Module.")
            .RegisterFeature("Assign a pool of restraint sets that Cursed Loot can use when functioning.")
            .RegisterFeature("Define the lower and upper lock period limits for customization.")
            .RegisterFeature("Bind defined gags to each active cursed set in the pool.")
            .RegisterFeature("Adjust the randomization / frequency Cursed Loot appears.")
            .RegisterFeature("Relish in gambling your freedom away with the new Cursed Loot system.~ ♥")
            .RegisterQol("Improved some backend logic for increased framework performance times.");
        VersionEntry(0, 8, 7, 6)
            .RegisterMain("Warning this version is very cursed. Mainly meant for debugging while i sleep.")
            .RegisterFeature("Some debugging variables spawn in the homepage to help with some achievement scouting.");
        VersionEntry(0, 8, 7, 5)
            .RegisterMain("Hardcore Blindfolds now function properly.")
            .RegisterFeature("WARNING: DO NOT TRY FORCED TO STAY OR FORCED TO SIT JUST QUITE YET THEY ARE UNTESTED.");
        VersionEntry(0, 8, 7, 4)
            .RegisterMain("Adding a testing interaction of a hardcore chat restrictor to the homepage.");
        VersionEntry(0, 8, 7, 3)
            .RegisterMain("This bug was such a big deal its now being posted as a Main Entry." + Environment.NewLine
            + "FINALLY fixed the bug where GagSpeak would rapidly update glamour endlessly, due to completing an update on the same tick framework tick")
            .RegisterFeature("Added framework foundation for cosmetic achievements.")
            .RegisterBugfix("Fixed Gags not swapping properly anymore with the new server restructure.")
            .RegisterBugfix("Fixed Gag Swaps not properly unequipping gags on a gag swap.");
        VersionEntry(0, 8, 7, 2)
            .RegisterFeature("Added a quick Open Actions Button for visible pair context menus.");
        VersionEntry(0, 8, 7, 1)
            .RegisterBugfix("Fixed Moodles dropdowns globally increasing the effected size of dropdowns.");
        VersionEntry(0, 8, 7, 0)
            .RegisterMain("Added a more Advanced Logger System that adds logger filter categories beyond provided ones.")
            .RegisterFeature("Switch on or off the individual logger categories in the settings window for helpful debugging.")
            .RegisterQol("Added an all on / all off button to auto assign all recommended logger filters or turn them all off.")
            .RegisterQol("Reorganized the asset folders into their own directories for less messy output navigation.");
        VersionEntry(0, 8, 6, 2)
            .RegisterMain("Logic for handling puppeteer trigger phrases should be mostly corrected now.")
            .RegisterQol("The main puppeteer permission options have been added as buttons in the trigger phrase box.");
        VersionEntry(0, 8, 6, 0)
            .RegisterMain("Gag and Restraint Set Actions now have correct logic. Major Debugging Part 1")
            .RegisterFeature("The Puppeteer UI has been reworked to a cleaner design layout.")
            .RegisterQol("You can now interact with the user list items without needing to hover the player name text itself.")
            .RegisterQol("Puppeteer userPair selectors now display which pair is selected.");
        VersionEntry(0, 8, 5, 2)
            .RegisterMain("Clicking on the Privacy DTR bar opens a mini-window to click on player names to view locations.");
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
