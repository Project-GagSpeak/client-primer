using GagspeakAPI.Data.Enum;
using Microsoft.Extensions.Logging;
using GagSpeak.GagspeakConfiguration.Models;
using GagSpeak.UI;
using GagSpeak.Hardcore.Movement;

namespace GagSpeak.GagspeakConfiguration.Configurations;

[Serializable]
public class GagspeakConfig : IGagspeakConfiguration
{
    public bool AcknowledgementUnderstood { get; set; } = false;       // if the user has acknowledged the terms of service
    public bool ButtonUsed { get; set; } = false;                      // if the user has used the button
    public bool AccountCreated { get; set; } = false;                   // if the user has created an account in the plugin
    public bool AccountClaimed { get; set; } = false;                   // if the user has claimed their account sucessfully.
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
    public NotificationLocation InfoNotification { get; set; } = NotificationLocation.Both;
    public NotificationLocation WarningNotification { get; set; } = NotificationLocation.Both;
    public NotificationLocation ErrorNotification { get; set; } = NotificationLocation.Both;

    // migrated from gagspeak information for client user. (stuff unessisary to be in the DB)
    public bool LiveGarblerZoneChangeWarn { get; set; }                 // if user wants to be warned about the live chat garbler on zone change
    public BlindfoldType BlindfoldStyle { get; set; }                   // the blindfold style the user is using
    public RevertStyle RevertStyle { get; set; }                        // how the user wants to revert their settings (can store locally?)
    public bool UsingSimulatedVibrator { get; set; }                    // if the user is using a simulated vibrator
    public string LanguageDialect { get; set; } = "IPA_US";             // the language dialect the user is using for MufflerCore
    public bool UsingLegacyControls { get; set; } = GameConfig.UiControl.GetBool("MoveMode"); // grabs our movement mode for the game.
    public int Version { get; set; } = 1;                               // the version of the config file
    public string IntifaceConnectionSocket { get; set; } = "ws://localhost:12345"; // connection link from plugin to intiface
}

