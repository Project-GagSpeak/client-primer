using Buttplug.Client;
using Dalamud.Interface.ImGuiNotification;
using GagSpeak.GagspeakConfiguration.Models;
using GagSpeak.PlayerData.Data;
using GagSpeak.PlayerData.Handlers;
using GagSpeak.PlayerData.Pairs;
using GagSpeak.Services.Events;
using GagSpeak.UI;
using GagSpeak.UI.Permissions;
using GagspeakAPI.Data;
using GagspeakAPI.Data.Character;
using GagspeakAPI.Data.Enum;
using GagspeakAPI.Dto.Connection;
using Glamourer.Api.Enums;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using System.Numerics;
using GagspeakAPI.Data.VibeServer;

namespace GagSpeak.Services.Mediator;

#pragma warning disable MA0048 // File name must match type name
#pragma warning disable S2094

/* ------------------ MESSAGE RELATED RECORDS ------------------ */
public record NotificationMessage // the record indicating a notification message that should be send to the client.
    (string Title, string Message, NotificationType Type, TimeSpan? TimeShownOnScreen = null) : MessageBase;


/* ------------------ EVENT LOGGER RECORDS ------------------ */
public record EventMessage(Event Event) : MessageBase; // an event message for logging purposes. (used in APIController, see there for ref


/* ------------------ MAIN HUB RECORDS ------------------ */
public record DisconnectedMessage(HubType hubType = HubType.MainHub) : SameThreadMessage; // indicating a disconnection message from the server.
public record HubReconnectingMessage(Exception? Exception) : SameThreadMessage; // indicating the moment the hub is reconnecting.
public record HubReconnectedMessage(string? Arg) : SameThreadMessage; // indicating the moment the hub has reconnected.
public record HubClosedMessage(Exception? Exception) : SameThreadMessage; // indicating the moment the hub has closed.
public record ConnectedMessage(ConnectionDto Connection) : MessageBase; // message published upon successful connection to the 
public record OnlinePairsLoadedMessage : MessageBase; // message published completion of loading all online pairs for the client.


/* ------------------ TOYBOX HUB RECORDS ------------------ */
public record ToyboxDisconnectedMessage(HubType hubType = HubType.MainHub) : SameThreadMessage; // indicating a disconnection message from the server.
public record ToyboxHubReconnectingMessage(Exception? Exception) : SameThreadMessage;
public record ToyboxHubReconnectedMessage(string? Arg) : SameThreadMessage;
public record ToyboxHubClosedMessage(Exception? Exception) : SameThreadMessage;
public record ToyboxConnectedMessage(ToyboxConnectionDto Connection) : MessageBase;


/* ------------- DALAMUD FRAMEWORK UPDATE RECORDS ------------- */
public record DalamudLoginMessage : MessageBase; // record indicating the moment the client logs into the game instance.
public record DalamudLogoutMessage : MessageBase; // record indicating the moment the client logs out of the game instance.
public record PriorityFrameworkUpdateMessage : SameThreadMessage; // a message indicating the need for a priority framework update.
public record FrameworkUpdateMessage : SameThreadMessage; // a message indicating the need for a framework update.
public record DelayedFrameworkUpdateMessage : SameThreadMessage; // a message indicating the need for a delayed framework update.
public record CutsceneEndMessage : MessageBase; // helps us know when to reapply data like moodles.
public record ZoneSwitchStartMessage : MessageBase; // know when we are beginning to switch zones
public record ZoneSwitchEndMessage : MessageBase; // know when we have finished switching zones
public record HaltScanMessage(string Source) : MessageBase; // know when we should stop scanning
public record ResumeScanMessage(string Source) : MessageBase; // know when we should resume scanning


/* ------------------ PLAYER DATA RELATED RECORDS------------------ */
public record PairHandlerVisibleMessage(PairHandler Player) : MessageBase; // a message indicating the visibility of a pair handler.
public record OpenUserPairPermissions(Pair? Pair, StickyWindowType PermsWindowType) : MessageBase; // fired upon request to open the permissions window for a pair
public record TargetPairMessage(Pair Pair) : MessageBase; // called when publishing a targeted pair connection (see UI)
public record UpdateDisplayWithPair(Pair Pair) : MessageBase; // called when we need to update the display with a pair.
public record CyclePauseMessage(UserData UserData) : MessageBase; // for cycling the paused state of self
public record GameObjectHandlerCreatedMessage(GameObjectHandler GameObjectHandler, bool OwnedObject) : MessageBase; // whenever a gameobjecthandler for a pair is made
public record GameObjectHandlerDestroyedMessage(GameObjectHandler GameObjectHandler, bool OwnedObject) : MessageBase; // whenever a gameobjecthandler for a pair is destroyed
public record CharacterDataCreatedMessage(CharacterCompositeData CharacterData) : SameThreadMessage; // indicates the creation of character data. (part of cache creation so may dump)
public record CreateCacheForObjectMessage(GameObjectHandler ObjectToCreateFor) : MessageBase; // called whene we create a new gameobject for the cache creation service.
public record ClearCacheForObjectMessage(GameObjectHandler ObjectToCreateFor) : MessageBase; // called when we should clear a gameobject from cache creation service.
public record MufflerLanguageChanged : MessageBase; // called whenever the client language changes to a new language.

/* ------------- PLAYER DATA MODULE INTERACTIONS --------- */
public record GagTypeChanged(GagList.GagType NewGagType, GagLayer Layer) : MessageBase; // called whenever the client changes their gag type.
public record ActiveGagTypesUpdated : MessageBase; // unsure if i'll ever need this.
public record GagLockToggle(PadlockData PadlockInfo, bool Unlocking) : MessageBase; // called whenever the client changes their padlock.
public record TooltipSetItemToRestraintSetMessage(EquipSlot Slot, EquipItem Item) : MessageBase; // for penumbra tooltip application to restraint set
public record AliasListUpdated(string UserUID) : MessageBase; // called whenever the client changes their alias list.
/* ----------------- PLAYERDATA WARDROBE HANDLER RECORDS ----------------- */
public record RestraintSetToggledMessage(UpdatedNewState State, int RestraintSetIndex, string AssignerUID) : MessageBase; // whenever the restraint set is toggled.
public record RestraintSetPropertyChanged(string UidPropertiesChangedFor) : MessageBase; // fired when property is changed for a particular user
public record RestraintSetAddedMessage(RestraintSet RestraintSetToAdd) : MessageBase; // A newly added restraint set
public record RestraintSetModified(int RestraintSetIndex) : MessageBase; // fired when a restraint set is modified.
public record RestraintSetRemovedMessage(int RestraintSetIndex) : MessageBase; // Set being removed
public record HardcoreRestraintSetDisabledMessage : MessageBase; // when a restraint set is removed.
public record HardcoreRestraintSetEnabledMessage : MessageBase; // when a restraint set is added.
public record BeginForcedToFollowMessage(Pair Pair) : MessageBase; // pair issuing the startup of the forced to follow command
public record EndForcedToFollowMessage(Pair Pair) : MessageBase;
public record BeginForcedToSitMessage(Pair Pair) : MessageBase; // pair issuing the startup of the forced to sit command
public record EndForcedToSitMessage(Pair Pair) : MessageBase;
public record BeginForcedToStayMessage(Pair Pair) : MessageBase; // pair issuing the startup of the forced to stay command
public record EndForcedToStayMessage(Pair Pair) : MessageBase;
public record BeginForcedBlindfoldMessage(Pair Pair) : MessageBase; // when a pair forces another to follow
public record EndForcedBlindfoldMessage(Pair Pair) : MessageBase;

/* ------------------ PLAYERDATA TOYBOX HANDLER RECORDS ------------------ */
public record ToyScanStarted : MessageBase; // for when the toybox scan is started.
public record ToyScanFinished : MessageBase; // for when the toybox scan is finished.
public record VibratorModeToggled(VibratorMode VibratorMode) : MessageBase; // for when the vibrator mode is toggled.
public record ToyDeviceAdded(ButtplugClientDevice Device) : MessageBase; // for when a device is added.
public record ToyDeviceRemoved(ButtplugClientDevice Device) : MessageBase; // for when a device is removed.
public record ButtplugClientDisconnected : MessageBase; // for when the buttplug client disconnects.
public record ToyboxActiveDeviceChangedMessage(int DeviceIndex) : MessageBase; // for when the active device is changed.
public record UpdateVibratorIntensity(int NewIntensity) : MessageBase; // for when the vibrator intensity is changed.
public record PatternAddedMessage(PatternData Pattern) : MessageBase; // for when a pattern is added.
public record PatternRemovedMessage(PatternData pattern) : MessageBase; // for when a pattern is removed.
public record PatternActivedMessage(int PatternIndex) : MessageBase; // for when a pattern is activated.
public record PatternDeactivedMessage(int PatternIndex) : MessageBase; // for when a pattern is deactivated.
public record PatternDataChanged(int PatternIndex) : MessageBase; // for when a pattern is changed.
public record AlarmAddedMessage(Alarm Alarm) : MessageBase; // When Alarm is added.
public record AlarmRemovedMessage : MessageBase; // for when an alarm is removed, force a recollection of pattern list.
public record AlarmActivated(int AlarmIndex) : MessageBase; // for when an alarm is activated.
public record AlarmDeactivated(int AlarmIndex) : MessageBase; // for when an alarm is deactivated.
public record AlarmDataChanged(int AlarmIndex) : MessageBase; // for when an alarm is changed.


/* ------------------ PLAYERDATA CLIENTSIDE PERMISSION HANDLING ------------------- */
public record ClientGlobalPermissionChanged(string Permission, object Value) : MessageBase; // for when a client global permission is changed.
public record ClientOwnPairPermissionChanged(Pair Pair, string Permission, object Value) : MessageBase; // for when a client pair permission is changed.
public record ClientOwnPairPermissionAccessChanged(Pair Pair, string Permission, object Value) : MessageBase; // for when a client pair permission is changed.
public record ClientOtherPairPermissionChanged(Pair Pair, string Permission, object Value) : MessageBase; // for when a client pair permission is changed
public record ClientOtherPairPermissionAccessChanged(Pair Pair, string Permission, object Value) : MessageBase; // for when a client pair permission is changed

/* ------------------ IPC HANDLER RECORDS------------------ */
public record MoodlesMessage(IntPtr Address) : MessageBase; // indicated a moodles message was published.
public record PenumbraInitializedMessage : MessageBase;
public record PenumbraDisposedMessage : MessageBase;
public record UpdateGlamourMessage(GlamourUpdateType GenericUpdateType) : MessageBase; // for full refreshes on states.
public record UpdateGlamourGagsMessage(UpdatedNewState NewState, GagLayer Layer, GagList.GagType GagType, string AssignerName) : MessageBase; // client side notifier for visual changes.
public record UpdateGlamourRestraintsMessage(UpdatedNewState NewState) : MessageBase; // Restraint set updates.
public record UpdateGlamourBlindfoldMessage(UpdatedNewState NewState, string AssignerName) : MessageBase; // Blindfold updates.
public record CustomizeProfileChanged : MessageBase; // when a profile is changed in customize+

public record PlayerCharIpcChanged(CharacterIPCData IPCData) : MessageBase; // for when the player character IPC data changes
public record PlayerCharAppearanceChanged(CharacterAppearanceData AppearanceData) : MessageBase; // called whenever a gag is changed on the player character
public record PlayerCharWardrobeChanged(CharacterWardrobeData WardrobeData) : MessageBase;  // called whenever client's wardrobe is changed.
public record PlayerCharAliasChanged(CharacterAliasData AliasData, string playerUID) : MessageBase;  // called whenever the player changes their alias list for another player
public record PlayerCharPatternChanged(CharacterPatternInfo PatternData) : MessageBase; // called whenever the player changes their pattern list for their vibrators

/* ------------------ USER INTERFACE (UI) RECORDS------------------ */
public record RefreshUiMessage : MessageBase; // a message indicating the need to refresh the UI.
public record UiToggleMessage(Type UiType) : MessageBase; // For toggling the UI.
public record SwitchToIntroUiMessage : MessageBase; // indicates that we are in the introduction UI.
public record SwitchToMainUiMessage : MessageBase; // indicates we are in the main UI.
public record OpenSettingsUiMessage : MessageBase; // indicates we are in the settings UI.
public record RemoveWindowMessage(WindowMediatorSubscriberBase Window) : MessageBase; // fired upon request to remove a window from the UI service.
public record CompactUiChange(Vector2 Size, Vector2 Position) : MessageBase; // fired whenever we change the window size or position

public record ProfileOpenStandaloneMessage(Pair Pair) : MessageBase; // for opening the profile standlone window.
public record ProfilePopoutToggle(Pair? Pair) : MessageBase; // toggles the profile popout window for a paired client.
public record ClearProfileDataMessage(UserData? UserData = null) : MessageBase; // a message indicating the need to clear profile data.
public record VerificationPopupMessage(VerificationDto VerificationCode) : MessageBase; // indicating that we have received a verification code popup.
public record PatternSavePromptMessage(List<byte> StoredData, string Duration) : MessageBase; // prompts the popup and passes in savedata
public record BlindfoldUiTypeChange(BlindfoldType NewType) : MessageBase; // for changing blindfold type.

#pragma warning restore S2094
#pragma warning restore MA0048 // File name must match type name
