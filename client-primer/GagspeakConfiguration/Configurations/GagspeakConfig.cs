using GagspeakAPI.Data.Enum;
using Microsoft.Extensions.Logging;
using GagSpeak.GagspeakConfiguration.Models;
using GagSpeak.UI;
using GagSpeak.Hardcore.Movement;
using GagSpeak.ChatMessages;
using GagspeakAPI.Data.VibeServer;
using GagSpeak.Hardcore;

namespace GagSpeak.GagspeakConfiguration.Configurations;

[Serializable]
public class GagspeakConfig : IGagspeakConfiguration
{
    public static int CurrentVersion => 2;
    public int Version { get; set; } = CurrentVersion;                  // Current Version of the GagSpeakConfig file.
    public Version? LastRunVersion { get; set; } = null;                // Tracks the last run assembly build of GagSpeak. Updated on each startup.

    public string LastUidLoggedIn { get; set; } = "";                   // the last UID the user logged in with
    
    public bool AcknowledgementUnderstood { get; set; } = false;        // if the user has acknowledged the terms of service
    public bool ButtonUsed { get; set; } = false;                       // if the user has used the button
    public bool AccountCreated { get; set; } = false;                   // if the user has created an account in the plugin
    public bool EnableDtrEntry { get; set; } = false;                   // enable the DTR entry
    public bool ShowUidInDtrTooltip { get; set; } = true;               // show the UID of a user in the tooltip
    public bool PreferNicknameInDtrTooltip { get; set; } = false;       // prefer nickname in DTR tooltip
    public bool PreferNicknamesOverNamesForVisible { get; set; } = false; // prefer nicknames over UID for visible users
    public bool ShowVisibleUsersSeparately { get; set; } = true;        // if we should show visible users seperate from online users (always be true)
    public bool ShowOfflineUsersSeparately { get; set; } = true;        // if we should show offline users in a seperate section
    public bool OpenPopupOnAdd { get; set; } = true;                    // if we should open a popup to set a nickname upon a bidirectional pair.
    public float ProfileDelay { get; set; } = 1.5f;                     // delay in seconds before showing the profile
    public bool ProfilePopoutRight { get; set; } = false;               // if profile should open upon hovering the name
    public bool ProfilesShow { get; set; } = true;                      // if we should see profiles on hover
    public bool ShowOnlineNotifications { get; set; } = false;          // if we should receive a notifacton when a paired user comes online.
    public bool ShowOnlineNotificationsOnlyForIndividualPairs { get; set; } = false; // only do it for the people you have paired
    public bool ShowOnlineNotificationsOnlyForNamedPairs { get; set; } = false; // only do it for the people you have paired and nicknamed
    public LogLevel LogLevel { get; set; } = LogLevel.Trace;            // the log level we want to see in /xllog
    public bool LogResourceManagement { get; set; } = false;            // if we should log the management of vibration audio on players
    public bool LogActionEffects { get; set; } = false;                 // if we should log the action effects of players
    public bool LogServerConnectionHealth { get; set; } = false;        // if we should log the health of the server connection
    public NotificationLocation InfoNotification { get; set; } = NotificationLocation.Both;
    public NotificationLocation WarningNotification { get; set; } = NotificationLocation.Both;
    public NotificationLocation ErrorNotification { get; set; } = NotificationLocation.Both;
    public List<ChatChannel.ChatChannels> ChannelsGagSpeak { get; set; } = []; // Which channels are GagSpeak translations happening in?
    public List<ChatChannel.ChatChannels> ChannelsPuppeteer { get; set; } = []; // which channels should puppeteer messages be scanning? 

    // migrated from gagspeak information for client user. (stuff unnecessary to be in the DB)
    public string Safeword { get; set; } = "";                               // the safeword the user has set
    public bool LiveGarblerZoneChangeWarn { get; set; } = false;                // if user wants to be warned about the live chat garbler on zone change
    public RevertStyle RevertStyle { get; set; } = RevertStyle.ToGameOnly;      // how the user wants to revert their settings (can store locally?)
    public bool DisableSetUponUnlock { get; set; } = false;                     // If we should remove a set whenever it is unlocked.
    public bool RemoveGagUponLockExpiration { get; set; } = false;              // If we should remove a gag upon lock expiration.
    public VibratorMode VibratorMode { get; set; } = VibratorMode.Actual;       // if the user is using a simulated vibrator
    public VibeSimType VibeSimAudio { get; set; } = VibeSimType.Quiet;          // the audio settings for the simulated vibrator
    public string Language { get; set; } = "English";                           // the language the user is using for MufflerCore
    public string LanguageDialect { get; set; } = "IPA_US";                     // the language dialect the user is using for MufflerCore
    public bool IntifaceAutoConnect { get; set; } = false;                      // if we should auto-connect to intiface
    public string IntifaceConnectionSocket { get; set; } = "ws://localhost:12345"; // connection link from plugin to intiface
    public bool VibeServerAutoConnect { get; set; } = false;                    // if we should auto-connect to the vibe server

    // hardcore stuff:
    public bool UsingLegacyControls { get; set; } = GameConfig.UiControl.GetBool("MoveMode"); // grabs our movement mode for the game.
    public BlindfoldType BlindfoldStyle { get; set; } = BlindfoldType.Sensual;  // the blindfold style the user is using
    public bool DisablePromptHooks { get; set; } = false;
    public TextFolderNode StoredEntriesFolder { get; private set; } = new TextFolderNode { Name = "ForcedDeclineList" };

}

