using CheapLoc;
using Dalamud.Interface;
using GagSpeak.WebAPI;
using Lumina.Data.Parsing;
using System.Reflection;

namespace GagSpeak.Localization
{
    internal static class GSLoc
    {
        // the Introduction Module overviewing Terms of Service & Getting Started
        public static Intro Intro { get; set; } = new();

        // The common Module for the generic main UI elements.
        public static CoreUi CoreUi { get; set; } = new();

        // The primary Settings window strings.
        public static Settings Settings { get; set; } = new();
        
        // One additional Module element for each section would be nice.
        public static Orders Orders { get; set; } = new();
        public static Gags Gags { get; set; } = new();
        public static Wardrobe Wardrobe { get; set; } = new();
        public static Puppet Puppet { get; set; } = new();
        public static Toybox Toybox { get; set; } = new();

        public static void ReInitialize()
        {
            Intro = new Intro();
            CoreUi = new CoreUi();
            Settings = new Settings();
            Orders = new Orders();
            Gags = new Gags();
            Wardrobe = new Wardrobe();
            Puppet = new Puppet();
            Toybox = new Toybox();
        }
    }

    #region Intro
    public class Intro
    {
        public ToS ToS { get; set; } = new();
        public Register Register { get; set; } = new();
        public StartGuide StartGuide { get; set; } = new();
    }

    // Get to Last.
    public class ToS
    {
        public readonly string Title = Loc.Localize("ToS_Title", "ACTUAL LABEL HERE");
    }

    // Get to Last.
    public class Register
    {
        public readonly string Title = Loc.Localize("Register_Title", "ACTUAL LABEL HERE");
    }

    // Get to Last.
    public class StartGuide
    {
        public readonly string Title = Loc.Localize("StartGuide_Title", "ACTUAL LABEL HERE");
    }
    #endregion Intro

    #region CoreUi
    public class CoreUi
    {
        public Tabs Tabs { get; set; } = new();
        public MainUi MainUi { get; set; } = new();
        public Homepage Homepage { get; set; } = new();
        public Whitelist Whitelist { get; set; } = new();
        public Discover Discover { get; set; } = new();
        public Account Account { get; set; } = new();
        public Warnings Warnings { get; set; } = new();
    }

    public class Tabs
    {
        public readonly string Title = Loc.Localize("Tabs_Title", "ACTUAL LABEL HERE");
    }

    public class MainUi
    {
        public readonly string Title = Loc.Localize("MainUi_Title", "ACTUAL LABEL HERE");
    }

    public class Homepage
    {
        public readonly string Title = Loc.Localize("Homepage_Title", "ACTUAL LABEL HERE");
    }

    public class Whitelist
    {
        public readonly string Title = Loc.Localize("Whitelist_Title", "ACTUAL LABEL HERE");
    }

    public class Discover
    {
        public readonly string Title = Loc.Localize("Discover_Title", "ACTUAL LABEL HERE");
    }

    public class Account
    {
        public readonly string Title = Loc.Localize("Account_Title", "ACTUAL LABEL HERE");
    }

    public class Warnings
    {
        public readonly string Title = Loc.Localize("Warnings_Title", "ACTUAL LABEL HERE");
    }
    #endregion CoreUi

    #region Settings
    public class  Settings
    {
        public readonly string OptionalPlugins = Loc.Localize("Settings_OptionalPlugins", "Optional Plugins:");
        public readonly string PluginValid = Loc.Localize("Settings_PluginValid", "This Plugin is enabled and up to date!");
        public readonly string PluginInvalid = Loc.Localize("Settings_PluginInvalid", "This Plugin is not up to date, or GagSpeak has outdated API.");
        public readonly string AccountClaimText = Loc.Localize("Settings_AccountClaimText", "Claim your Account with the CK Discord Bot:");

        public readonly string TabsGlobal = Loc.Localize("Settings_TabsGlobal", "Global");
        public readonly string TabsHardcore = Loc.Localize("Settings_TabsHardcore", "Hardcore");
        public readonly string TabsPreferences = Loc.Localize("Settings_TabsPreferences", "Preferences");
        public readonly string TabsAccounts = Loc.Localize("Settings_TabsAccounts", "Account Management");

        public MainOptions MainOptions { get; set; } = new();
        public Hardcore Hardcore { get; set; } = new();
        public ForcedStay ForcedStay { get; set; } = new();
        public Preferences Preferences { get; set; } = new();
        public Accounts Accounts { get; set; } = new();
    }

    public class MainOptions
    {
        public readonly string HeaderGags = Loc.Localize("MainOptions_HeaderGags", "Gags");
        public readonly string HeaderWardrobe = Loc.Localize("MainOptions_HeaderWardrobe", "Wardrobe");
        public readonly string HeaderPuppet = Loc.Localize("MainOptions_HeaderPuppet", "Puppeteer");
        public readonly string HeaderToybox = Loc.Localize("MainOptions_HeaderToybox", "Toybox");

        public readonly string LiveChatGarbler = Loc.Localize("MainOptions_LiveChatGarbler", "Live Chat Garbler");
        public readonly string LiveChatGarblerTT = Loc.Localize("MainOptions_LiveChatGarblerTT", "Generates GagSpeak translated message when you talk in chat while gagged." +
                "--SEP--(This is done server-side, others will see it too)");

        public readonly string GagGlamours = Loc.Localize("MainOptions_GagGlamours", "Gag Glamours");
        public readonly string GagGlamoursTT = Loc.Localize("MainOptions_GagGlamoursTT", "Allows Glamourer to apply your Gag Glamour Items once gagged!");

        public readonly string GagPadlockTimer = Loc.Localize("MainOptions_GagPadlockTimer", "Expired Timers remove their Gag");
        public readonly string GagPadlockTimerTT = Loc.Localize("MainOptions_GagPadlockTimerTT", "If a gag is locked with a timer, upon the timers expiration, the Gag will be removed.");

        public readonly string WardrobeActive = Loc.Localize("MainOptions_WardrobeActive", "Wardrobe Features");
        public readonly string WardrobeActiveTT = Loc.Localize("MainOptions_WardrobeActiveTT", "If enabled, all appearance altaring effects will become functional.");

        public readonly string RestraintSetGlamour = Loc.Localize("MainOptions_RestraintSetGlamour", "Restraint Glamours");
        public readonly string RestraintSetGlamourTT = Loc.Localize("MainOptions_RestraintSetGlamourTT", "Allows Glamourer to bind restraint sets to your character." +
            "--SEP--Restraint sets can be created in the Wardrobe Interface.");

        public readonly string RestraintPadlockTimer = Loc.Localize("MainOptions_RestraintPadlockTimer", "Expired Timers Remove their Restraint");
        public readonly string RestraintPadlockTimerTT = Loc.Localize("MainOptions_RestraintPadlockTimerTT", "If a restraint is locked with a timer, upon the timers expiration, the Restraint will be removed.");

        public readonly string CursedLootActive = Loc.Localize("MainOptions_CursedLootActive", "Cursed Dungeon Loot");
        public readonly string CursedLootActiveTT = Loc.Localize("MainOptions_CursedLootActiveTT", "Provide the Cursed Loot Component with a list of sets to randomly apply." +
            "--SEP--When opening Dungeon Chests, there is a random chance to apply & lock a set." +
            "--SEP--Mimic Timer Locks are set in your defined range, and CANNOT be unlocked.");

        public readonly string MoodlesActive = Loc.Localize("MainOptions_MoodlesActive", "Allow Moodles");
        public readonly string MoodlesActiveTT = Loc.Localize("MainOptions_MoodlesActiveTT", "If enabled, all moodle effects will become functional.");

        public readonly string RevertSelectionLabel = Loc.Localize("MainOptions_RevertSelectionLabel", "On Safeword/Restraint Removal/Gag Removal:");

        public readonly string PuppeteerActive = Loc.Localize("MainOptions_PuppeteerActive", "Puppeteer Features");
        public readonly string PuppeteerActiveTT = Loc.Localize("MainOptions_PuppeteerActiveTT", "If enabled, all puppeteer features will become functional.");

        public readonly string GlobalTriggerPhrase = Loc.Localize("MainOptions_GlobalTriggerPhrase", "Global Trigger Phrase");
        public readonly string GlobalTriggerPhraseTT = Loc.Localize("MainOptions_GlobalTriggerPhraseTT", "Sets a global trigger phrase that will trigger your puppeteer." +
            "--SEP--This trigger phrase will work when said by ANYONE.");

        public readonly string GlobalAllowSit = Loc.Localize("MainOptions_GlobalAllowSit", "Globally Allow Sit Requests");
        public readonly string GlobalAllowSitTT = Loc.Localize("MainOptions_GlobalAllowSitTT", "Allows anyone to request a sit from you." +
            "--SEP--Limits the actions that ANYONE can make you do to just /sit, /groundsit, and /cpose (/changepose)");


        public readonly string GlobalAllowMotion = Loc.Localize("MainOptions_GlobalAllowMotion", "Globally Allow Motion Requests");
        public readonly string GlobalAllowMotionTT = Loc.Localize("MainOptions_GlobalAllowKneelTT", "Globally Allow Motion Requests" +
            "--SEP--Motion Requests mean any action that executes an emote or expression can be triggered by Puppeteer." +
            "--SEP--Limits the actions that ANYONE can make you do to only emotes and expressions.");

        public readonly string GlobalAllowAll = Loc.Localize("MainOptions_GlobalAllowAll", "Globally Allow All Requests");
        public readonly string GlobalAllowAllTT = Loc.Localize("MainOptions_GlobalAllowAllTT", "Allows anyone to request any action from you." +
            "--SEP--This includes all emotes, expressions, and any other action that can be triggered by the game." +
            "--SEP--USE WITH CAUTION: This is the most dangerous thing to grant (literally), as anyone can make you /logout, if desired.");

        public readonly string ToyboxActive = Loc.Localize("MainOptions_ToyboxActive", "Toybox Features");
        public readonly string ToyboxActiveTT = Loc.Localize("MainOptions_ToyboxActiveTT", "If enabled, all toybox features will become functional.");

        public readonly string IntifaceAutoConnect = Loc.Localize("MainOptions_IntifaceAutoConnect", "Auto-Connect to Intiface");
        public readonly string IntifaceAutoConnectTT = Loc.Localize("MainOptions_IntifaceAutoConnectTT", "Automatically connect to the Intiface Desktop App when GagSpeak is started.");

        public readonly string IntifaceAddressTT = Loc.Localize("MainOptions_IntifaceAddressTT", "Change the Intiface Server Address to a custom one if you desire!." +
            "--SEP--Leave blank to use the default address.");

        public readonly string VibeServerAutoConnect = Loc.Localize("MainOptions_VibeServerAutoConnect", "Auto-Connect to Vibe Server");
        public readonly string VibeServerAutoConnectTT = Loc.Localize("MainOptions_VibeServerAutoConnectTT", "Connect to GagSpeak VibeServers once connected to MainHub");

        public readonly string SpatialAudioActive = Loc.Localize("MainOptions_SpatialAudioActive", "Spatial Audio Features");
        public readonly string SpatialAudioActiveTT = Loc.Localize("MainOptions_SpatialAudioActiveTT", "Emits vibrator audio while your sex toys are active to other paired players around you." +
            "--SEP--Similarily, you will be able to hear other peoples vibrator audio emitting from their characters when they are vibed.");

        public readonly string PiShockKeyTT = Loc.Localize("MainOptions_PiShockKeyTT", "Required PiShock API Key to exist for any PiShock related interactions to work.");
        public readonly string PiShockUsernameTT = Loc.Localize("MainOptions_PiShockUsernameTT", "Username for the PiShock API Key.");
        public readonly string PiShockShareCodeRefreshTT = Loc.Localize("MainOptions_PiShockShareCodeRefreshTT", "Forces Global PiShock Share Code to grab latest data from the API and push it to other online pairs.");
        public readonly string PiShockShareCode = Loc.Localize("MainOptions_PiShockShareCode", "PiShock Global Share Code");
        public readonly string PiShockShareCodeTT = Loc.Localize("MainOptions_PiShockShareCodeTT", "Global PiShock Share Code used for your connected ShockCollar." +
            "--SEP--NOTE: Only people you are in Hardcore mode with will have access to it.");

        public readonly string PiShockVibeTime = Loc.Localize("MainOptions_PiShockVibeTime", "Global Max Vibration Time");
        public readonly string PiShockVibeTimeTT = Loc.Localize("MainOptions_PiShockVibeTimeTT", "The maximum time in seconds that your shock collar can vibrate for.");
        public readonly string PiShockPermsLabel = Loc.Localize("MainOptions_PiShockPermsLabel", "Global Shock Collar Permissions (Parsed From Share Code)");
        public readonly string PiShockAllowShocks = Loc.Localize("MainOptions_PiShockAllowShocks", "Allow Shocks");
        public readonly string PiShockAllowVibes = Loc.Localize("MainOptions_PiShockAllowVibes", "Allow Vibrations");
        public readonly string PiShockAllowBeeps = Loc.Localize("MainOptions_PiShockAllowBeeps", "Allow Beeps");
        public readonly string PiShockMaxShockIntensity = Loc.Localize("MainOptions_PiShockMaxShockIntensity", "Global Max Shock Intensity: ");
        public readonly string PiShockMaxShockDuration = Loc.Localize("MainOptions_PiShockMaxShockDuration", "Global Max Shock Duration: ");
    }

    public class Hardcore
    {
        public readonly string TabBlindfold = Loc.Localize("Hardcore_TabBlindfold", "Blindfold Item");
        public readonly string TabForcedStayFilters = Loc.Localize("Hardcore_TabForcedStayFilters", "Forced Stay Filters");
        
        public readonly string BlindfoldSlot = Loc.Localize("Hardcore_BlindfoldSlot", "Slot");
        public readonly string BlindfoldItem = Loc.Localize("Hardcore_BlindfoldItem", "Item");
        public readonly string BlindfoldDye = Loc.Localize("Hardcore_BlindfoldDye", "Dyes");
        public readonly string BlindfoldFirstPerson = Loc.Localize("Hardcore_BlindfoldFirstPerson", "Force First-Person");
        public readonly string BlindfoldFirstPersonTT = Loc.Localize("Hardcore_BlindfoldFirstPersonTT", "Force the First-Person view while blindfolded.");
        
        public readonly string BlindfoldTypeHeader = Loc.Localize("Hardcore_BlindfoldTypeHeader", "Blindfold Type");
        public readonly string LaceStyle = Loc.Localize("Hardcore_LaceStyle", "Lace Style");

        public readonly string AddNodeLastSeenTT = Loc.Localize("Hardcore_AddNodeLastSeenTT", "Add last interacted node to list." +
            "--SEP--Note: Auto-selecting yes is not an allowed option");
        public readonly string AddNodeNewTT = Loc.Localize("Hardcore_AddNodeNew", "Add a new TextNode to the ForcedStay Prompt List.");
        public readonly string AddNodeNewChamberTT = Loc.Localize("Hardcore_AddNodeNewChamber", "Add a new ChamberNode to the ForcedStay Prompt List.");
        public readonly string ChamberAutoMoveTT = Loc.Localize("Hardcore_ChamberAutoMoveTT", "Automatically move to the Chambers after entering an estate while this is enabled.");
    }

    public class ForcedStay
    {
        public readonly string LeaveAPTFriendly = Loc.Localize("ForcedStay_LeaveAPTFriendly", "[ForcedStay] Prevent Apartment Leaving");
        public readonly string LeaveAPTName = Loc.Localize("ForcedStay_LeaveAPTName", "Exit");
        public readonly string LeaveAPTOption = Loc.Localize("ForcedStay_LeaveAPTOption", "Cancel");

        public readonly string LeaveChamberFriendly = Loc.Localize("ForcedStay_LeaveChamberFriendly", "[ForcedStay] Prevent Chamber Leaving");
        public readonly string LeaveChamberName = Loc.Localize("ForcedStay_LeaveChamberName", "Exit");
        public readonly string LeaveChamberLabel = Loc.Localize("ForcedStay_LeaveChamberLabel", "What would you like to do?");
        public readonly string LeaveChamberOption = Loc.Localize("ForcedStay_LeaveChamberOption", "Nothing.");

        public readonly string LeaveEstateFriendly = Loc.Localize("ForcedStay_LeaveEstateFriendly", "[ForcedStay] Prevent Estate Leaving");
        public readonly string LeaveEstateName = Loc.Localize("ForcedStay_LeaveEstateName", "Exit");
        public readonly string LeaveEstateLabel = Loc.Localize("ForcedStay_LeaveEstateLabel", "Leave the estate hall?");
        public readonly string LeaveEstateOption = Loc.Localize("ForcedStay_LeaveEstateOption", "No");

        public readonly string EnterEstateFriendly = Loc.Localize("ForcedStay_EnterEstateFriendly", "[ForcedStay] Auto-Enter Estate's (Prevent Logout Escape)");
        public readonly string EnterEstateName = Loc.Localize("ForcedStay_EnterEstateName", "Entrance");
        public readonly string EnterEstateLabel = Loc.Localize("ForcedStay_EnterEstateLabel", "Enter the estate hall?");
        public readonly string EnterEstateOption = Loc.Localize("ForcedStay_EnterEstateOption", "Yes");

        public readonly string EnterAPTOneFriendly = Loc.Localize("ForcedStay_EnterAPTOneFriendly", "[ForcedStay] Open Apartment Menu (1/3)");
        public readonly string EnterAPTOneName = Loc.Localize("ForcedStay_EnterAPTOneName", "Apartment Building Entrance");
        public readonly string EnterAPTOneOption = Loc.Localize("ForcedStay_EnterAPTOneOption", "Go to specified apartment");

        public readonly string EnterAPTTwoFriendly = Loc.Localize("ForcedStay_EnterAPTTwoFriendly", "[ForcedStay] Select Apartment Room (2/3)");
        public readonly string EnterAPTTwoName = Loc.Localize("ForcedStay_EnterAPTTwoName", "Apartment Building Entrance");

        public readonly string EnterAPTThreeFriendly = Loc.Localize("ForcedStay_EnterAPTThreeFriendly", "[ForcedStay] Enter Selected Apartment (3/3)");
        public readonly string EnterAPTThreeName = Loc.Localize("ForcedStay_EnterAPTThreeName", "Apartment Building Entrance");
        public readonly string EnterAPTThreeLabel = Loc.Localize("ForcedStay_EnterAPTThreeLabel", @"/^Enter .+?'s room\\?$/");
        public readonly string EnterAPTThreeOption = Loc.Localize("ForcedStay_EnterAPTThreeOption", "Yes");

        public readonly string EnterFCOneFriendly = Loc.Localize("ForcedStay_EnterFCOneFriendly", "[ForcedStay] Open FC Chambers Menu (1/3)");
        public readonly string EnterFCOneName = Loc.Localize("ForcedStay_EnterFCOneName", "Entrance to Additional Chambers");
        public readonly string EnterFCOneOption = Loc.Localize("ForcedStay_EnterFCOneOption", "Move to specified private chambers");

        public readonly string EnterFCTwoFriendly = Loc.Localize("ForcedStay_EnterFCTwoFriendly", "[ForcedStay] Select FC Chamber Room (2/3)");
        public readonly string EnterFCTwoName = Loc.Localize("ForcedStay_EnterFCTwoName", "Entrance to Additional Chambers");

        public readonly string EnterFCThreeFriendly = Loc.Localize("ForcedStay_EnterFCThreeFriendly", "[ForcedStay] Enter Selected Chamber (3/3)");
        public readonly string EnterFCThreeName = Loc.Localize("ForcedStay_EnterFCThreeName", "Apartment Building Entrance");
        public readonly string EnterFCThreeLabel = Loc.Localize("ForcedStay_EnterFCThreeLabel", @"/^Enter .+?'s room\\?$/");
        public readonly string EnterFCThreeOption = Loc.Localize("ForcedStay_EnterFCThreeOption", "Yes");
    }

    public class Preferences
    {
        public readonly string LangDialectLabel = Loc.Localize("Preferences_LangLabel", "Language & Dialect:");
        public readonly string LangTT = Loc.Localize("Preferences_LangTT", "Select the language for GagSpeak's Chat Garbler work with.");
        public readonly string DialectTT = Loc.Localize("Preferences_DialectTT", "Select the dialect for GagSpeak's Chat Garbler work with.");
        public readonly string HeaderPuppet = Loc.Localize("Preferences_HeaderPuppet", "Puppeteer Channels");

        public readonly string HeaderNicks = Loc.Localize("Preferences_HeaderNicks", "Nicknames");
        public readonly string NickPopupLabel = Loc.Localize("Preferences_NickPopupLabel", "Show Nickname Popup");
        public readonly string NickPopupTT = Loc.Localize("Preferences_NickPopupTT", "Adds a popup to set an added pairs nickname automatically.");

        // UI Preferences Section
        public readonly string HeaderUiPrefs = Loc.Localize("Preferences_HeaderUiPrefs", "UI Preferences");

        public readonly string EnableDtrLabel = Loc.Localize("Preferences_EnableDtrEntryLabel", "Display status and visible pair count in Server Info Bar");
        public readonly string EnableDtrTT = Loc.Localize("Preferences_EnableDtrEntryTT", "Adds a GagSpeak connection status & visible pair count in the Server Info Bar.");

        public readonly string PrivacyRadarLabel = Loc.Localize("Preferences_PrivacyRadarLabel", "Privacy Radar DTR Entry");
        public readonly string PrivacyRadarTT = Loc.Localize("Preferences_PrivacyRadarTT", "Display any Non-GagSpeak pairs in render range for privacy during kinky RP.");

        public readonly string ActionsNotifLabel = Loc.Localize("Preferences_ActionsNotifLabel", "Actions Notifier DTR Entry");
        public readonly string ActionsNotifTT = Loc.Localize("Preferences_ActionsNotifTT", "Display a bell in the DTR bar when your pairs use an action on you.");

        public readonly string VibeStatusLabel = Loc.Localize("Preferences_VibeStatusLabel", "Vibe Status DTR Entry");
        public readonly string VibeStatusTT = Loc.Localize("Preferences_VibeStatusTT", "Display a vibe icon in the DTR bar when you have an active toy running.");

        public readonly string ShowVisibleSeparateLabel = Loc.Localize("Preferences_ShowVisibleSeparateLabel", "Show separate Visible group");
        public readonly string ShowVisibleSeparateTT = Loc.Localize("Preferences_ShowVisibleSeparateTT", "Creates an additional dropdown for all paired users in your render range.");

        public readonly string ShowOfflineSeparateLabel = Loc.Localize("Preferences_ShowOfflineSeparateLabel", "Show separate Offline group");
        public readonly string ShowOfflineSeparateTT = Loc.Localize("Preferences_ShowOfflineSeparateTT", "Creates an additional dropdown for all paired users that are currently offline.");

        public readonly string PreferNicknamesLabel = Loc.Localize("Preferences_PreferNicknamesLabel", "Prefer nicks over character names for visible pairs");
        public readonly string PreferNicknamesTT = Loc.Localize("Preferences_PreferNicknamesTT", "Displays the nick you set for a pair instead of their characters name.");

        public readonly string ShowProfilesLabel = Loc.Localize("Preferences_ShowProfilesLabel", "Show GagSpeak Profiles on Hover");
        public readonly string ShowProfilesTT = Loc.Localize("Preferences_ShowProfilesTT", "Displays the configured user profile after a set delay when hovering over a player.");

        public readonly string ProfileDelayLabel = Loc.Localize("Preferences_ProfileDelayLabel", "Hover Delay");
        public readonly string ProfileDelayTT = Loc.Localize("Preferences_ProfileDelayTT", "Sets the delay before a profile is displayed on hover.");

        public readonly string ContextMenusLabel = Loc.Localize("Preferences_ShowContextMenusLabel", "Enables right-click context menus for visible pairs.");
        public readonly string ContextMenusTT = Loc.Localize("Preferences_ShowContextMenusTT", "Adds Quick Actions to visible Pairs Context Menu." +
            "--SEP--Context Menus are what display when you right click a player to examine them." +
            "--SEP--This option adds shortcuts to the pairs KinkPlate™ or to open their Pair Actions");

        // Notifications Section
        public readonly string HeaderNotifications = Loc.Localize("Preferences_HeaderNotifications", "Notifications");
        public readonly string ZoneChangeWarnLabel = Loc.Localize("Preferences_ZoneChangeWarnLabel", "Live Chat Garbler Warnings (On Zone Change)");
        public readonly string ZoneChangeWarnTT = Loc.Localize("Preferences_ZoneChangeWarnTT", "Displays a chat message & notification when changing zones if gagged with live chat garbler on." +
            "--SEP--Useful to prevent accidental muffled statements in unwanted chats.");
        
        public readonly string ConnectedNotifLabel = Loc.Localize("Preferences_ConnectedNotifLabel", "Enable Connection Notifications");
        public readonly string ConnectedNotifTT = Loc.Localize("Preferences_ConnectedNotifTT", "Displays a Notification during server connection changes." +
            "--SEP--Notifys you when you: Connect, Are Disconnected, Lost Connection, Have Reconnected");
        
        public readonly string OnlineNotifLabel = Loc.Localize("Preferences_OnlineNotifLabel", "Enable Online Pair Notifications");
        public readonly string OnlineNotifTT = Loc.Localize("Preferences_OnlineNotifTT", "Displays a info notification whenever a pair goes online.");

        public readonly string LimitForNicksLabel = Loc.Localize("Preferences_LimitForNicksLabel", "Limit Online Notifications to Nicked Pairs");
        public readonly string LimitForNicksTT = Loc.Localize("Preferences_LimitForNicksTT", "Limit the notifier to only show for nicknamed pairs.");
    }

    public class Accounts
    {
        public readonly string PrimaryLabel = Loc.Localize("Accounts_PrimaryLabel", "Primary Account:");
        public readonly string SecondaryLabel = Loc.Localize("Accounts_SecondaryLabel", "Alt Accounts:");
        public readonly string NoSecondaries = Loc.Localize("Accounts_NoSecondaries", "No secondary accounts to display." + 
            "\nEach Account is bound to 1 Character. To get alt account keys, register with the CK Discord Bot.");
        
        public readonly string CharaNameLabel = Loc.Localize("Accounts_CharaNameLabel", "The Account Character's Name");
        public readonly string CharaWorldLabel = Loc.Localize("Accounts_CharaWorldLabel", "The Account Character's World");
        public readonly string CharaKeyLabel = Loc.Localize("Accounts_CharaKeyLabel", "The Account Secret Key");
        
        public readonly string DeleteButtonLabel = Loc.Localize("Accounts_DeleteButtonLabel", "Delete Account");
        public readonly string DeleteButtonDisabledTT = Loc.Localize("Accounts_DeleteButtonDisabledTT", "Cannot remove this account as it is not yet registered");
        public readonly string DeleteButtonTT = Loc.Localize("Accounts_DeleteButtonTT", "Permanently Remove this account from GagSpeak services." +
            "--SEP--WARNING: Once account is deleted, the secret key used with it will not be reusable." +
            "--SEP--If you want to create a fresh account for the logged in character, you will need to obtain a new key for it." +
            "--SEP--(Clicking this button will pull up a confirmation menu, will not happen right away)");
        public readonly string DeleteButtonPrimaryTT = Loc.Localize("Accounts_DeleteButtonPrimaryTT", "--SEP----COL--IF YOU REMOVE THIS ACCOUNT ALL ALT ACCOUNTS WILL BE DELETED AS WELL");
        
        public readonly string FingerprintPrimary = Loc.Localize("Accounts_FingerprintPrimary", "This is your Primary GagSpeak Account.");
        public readonly string FingerprintSecondary = Loc.Localize("Accounts_FingerprintSecondary", "This is one of your GagSpeak Alt Accounts.");
        
        public readonly string SuccessfulConnection = Loc.Localize("Accounts_SuccessfulConnection", "Has established successful connection to GagSpeak Servers with inserted Key." +
            "--SEP--This Secret Key is now bound to this character and cannot be removed unless the account is deleted.");
        public readonly string NoSuccessfulConnection = Loc.Localize("Accounts_NoSuccessfulConnection", "No connection has been established with the secret key for this account.");
        public readonly string EditKeyAllowed = Loc.Localize("Accounts_EditKeyAllowed", "Toggle display of Secret Key Field");
        public readonly string EditKeyNotAllowed = Loc.Localize("Accounts_EditKeyNotAllowed", "Can't change a key that's been verified. This character is now account-bound.");
        public readonly string CopyKeyToClipboard = Loc.Localize("Accounts_CopyKeyToClipboard", "Click to copy the actual secret key to your clipboard!");

        public readonly string RemoveAccountPrimaryWarning = Loc.Localize("Accounts_RemoveAccountPrimaryWarning", "Be Deleting your primary GagSpeak account, all secondary users below will also be deleted.");
        public readonly string RemoveAccountWarning = Loc.Localize("Accounts_RemoveAccountWarning", "Your UID will be removed from all pairing lists.\nYou won't be able to reuse this secret key.");
        public readonly string RemoveAccountConfirm = Loc.Localize("Accounts_RemoveAccountConfirm", "Are you sure you want to remove this account?");
    }
    #endregion Settings

    #region Orders
    public class Orders
    {
        // Nothing here atm.
    }
    #endregion Orders

    #region Gags
    public class Gags
    {
        public ActiveGags ActiveGags { get; set; } = new();
        public GagStorage GagStorage { get; set; } = new();
    }

    public class ActiveGags
    {
        public readonly string Title = Loc.Localize("ActiveGags_Title", "ACTUAL LABEL HERE");
    }

    public class GagStorage
    {
        public readonly string Title = Loc.Localize("GagStorage_Title", "ACTUAL LABEL HERE");
    }
    #endregion Gags

    #region Wardrobe
    public class Wardrobe
    {
        public Restraints Restraints { get; set; } = new();
        public CursedLoot CursedLoot { get; set; } = new();
        public Moodles Moodles { get; set; } = new();
    }

    public class Restraints
    {
        public readonly string Title = Loc.Localize("Restraints_Title", "ACTUAL LABEL HERE");
    }

    public class CursedLoot
    {
        public readonly string Title = Loc.Localize("CursedLoot_Title", "ACTUAL LABEL HERE");
    }

    public class Moodles
    {
        public readonly string Title = Loc.Localize("Moodles_Title", "ACTUAL LABEL HERE");
    }
    #endregion Wardrobe

    #region Puppet
    public class Puppet
    {
        // just put everything in here probably, its small enough.
    }
    #endregion Puppet

    #region Toybox
    public class Toybox
    {
        public Overview Overview { get; set; } = new();
        public VibeServer VibeServer { get; set; } = new();
        public Patterns Patterns { get; set; } = new();
        public Triggers Triggers { get; set; } = new();
        public Alarms Alarms { get; set; } = new();
    }

    public class Overview
    {
        public readonly string Title = Loc.Localize("Overview_Title", "ACTUAL LABEL HERE");
    }

    public class VibeServer
    {
        public readonly string Title = Loc.Localize("VibeServer_Title", "ACTUAL LABEL HERE");
    }

    public class Patterns
    {
        public readonly string Title = Loc.Localize("Patterns_Title", "ACTUAL LABEL HERE");
    }

    public class Triggers
    {
        public readonly string Title = Loc.Localize("Triggers_Title", "ACTUAL LABEL HERE");
    }

    public class Alarms
    {
        public readonly string Title = Loc.Localize("Alarms_Title", "ACTUAL LABEL HERE");
    }
    #endregion Toybox
}
