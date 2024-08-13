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
using GagSpeak.PlayerData.PrivateRooms;
using GagspeakAPI.Dto.Toybox;

namespace GagSpeak.Services.Mediator;

#pragma warning disable MA0048 // File name must match type name
#pragma warning disable S2094

/* ------------------ MESSAGE RELATED RECORDS ------------------ */
public record NotificationMessage // the record indicating a notification message that should be send to the client.
    (string Title, string Message, NotificationType Type, TimeSpan? TimeShownOnScreen = null) : MessageBase;


/* ------------------ EVENT LOGGER RECORDS ------------------ */
public record EventMessage(Event Event) : MessageBase; // an event message for logging purposes. (used in APIController, see there for ref


/* ------------------ MAIN HUB RECORDS ------------------ */
public record DisconnectedMessage : SameThreadMessage; // indicating a disconnection message from the server.
public record HubReconnectingMessage(Exception? Exception) : SameThreadMessage; // indicating the moment the hub is reconnecting.
public record HubReconnectedMessage(string? Arg) : SameThreadMessage; // indicating the moment the hub has reconnected.
public record HubClosedMessage(Exception? Exception) : SameThreadMessage; // indicating the moment the hub has closed.
public record ConnectedMessage(ConnectionDto Connection) : MessageBase; // message published upon successful connection to the 
public record OnlinePairsLoadedMessage : MessageBase; // message published completion of loading all online pairs for the client.


/* ------------------ TOYBOX HUB RECORDS ------------------ */
public record ToyboxDisconnectedMessage : SameThreadMessage; // indicating a disconnection message from the server.
public record ToyboxHubReconnectingMessage(Exception? Exception) : SameThreadMessage;
public record ToyboxHubReconnectedMessage(string? Arg) : SameThreadMessage;
public record ToyboxHubClosedMessage(Exception? Exception) : SameThreadMessage;
public record ToyboxConnectedMessage(ToyboxConnectionDto Connection) : MessageBase;
public record ToyboxPrivateRoomJoined(string RoomName) : MessageBase; // when our player joins a private room.
public record ToyboxPrivateRoomLeft(string RoomName) : MessageBase; // when our player leaves a private room.

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
public record PairWentOnlineMessage(UserData UserData) : MessageBase; // a message indicating a pair has gone online.
public record PairHandlerVisibleMessage(PairHandler Player) : MessageBase; // a message indicating the visibility of a pair handler.
public record OpenUserPairPermissions(Pair? Pair, StickyWindowType PermsWindowType) : MessageBase; // fired upon request to open the permissions window for a pair
public record TargetPairMessage(Pair Pair) : MessageBase; // called when publishing a targeted pair connection (see UI)
public record UpdateDisplayWithPair(Pair Pair) : MessageBase; // called when we need to update the display with a pair.
public record CyclePauseMessage(UserData UserData) : MessageBase; // for cycling the paused state of self
public record CreateCacheForObjectMessage(GameObjectHandler ObjectToCreateFor) : MessageBase;
public record ClearCacheForObjectMessage(GameObjectHandler ObjectToCreateFor) : MessageBase; // called when we should clear a gameobject from cache creation service.
public record MufflerLanguageChanged : MessageBase; // called whenever the client language changes to a new language.

/* ------------- PLAYER DATA MODULE INTERACTIONS --------- */
public record UpdateActiveGags : MessageBase;
public record ActiveGagsUpdated : MessageBase;
public record ActiveLocksUpdated : MessageBase;
public record GagTypeChanged(GagList.GagType NewGagType, GagLayer Layer) : MessageBase; // called whenever the client changes their gag type.
public record GagLockToggle(PadlockData PadlockInfo, bool Unlocking, bool pushChanges) : MessageBase; // called whenever the client changes their padlock.
public record TooltipSetItemToRestraintSetMessage(EquipSlot Slot, EquipItem Item) : MessageBase; // TODO: ADD implementation for this.


#region PLAYERDATA WARDROBE HANDLER RECORDS
public record RestraintSetToggledMessage(int SetIdx, string AssignerUID, UpdatedNewState State, bool isHardcoreSet, bool pushChanges) : MessageBase; // whenever the restraint set is toggled.
public record HardcoreForcedToFollowMessage(Pair Pair, UpdatedNewState State) : MessageBase;
public record HardcoreForcedToSitMessage(Pair Pair, UpdatedNewState State) : MessageBase; 
public record HardcoreForcedToStayMessage(Pair Pair, UpdatedNewState State) : MessageBase;
public record HardcoreForcedBlindfoldMessage(Pair Pair, UpdatedNewState State) : MessageBase;
#endregion PLAYERDATA WARDROBE HANDLER RECORDS

#region PLAYERDATA TOYBOX HANDLER RECORDS
public record VfxActorRemoved(IntPtr data) : MessageBase;
public record ToyScanStarted : MessageBase; // for when the toybox scan is started.
public record ToyScanFinished : MessageBase; // for when the toybox scan is finished.
public record VibratorModeToggled(VibratorMode VibratorMode) : MessageBase; // for when the vibrator mode is toggled.
public record ToyDeviceAdded(ButtplugClientDevice Device) : MessageBase; // for when a device is added.
public record ToyDeviceRemoved(ButtplugClientDevice Device) : MessageBase; // for when a device is removed.
public record ButtplugClientDisconnected : MessageBase; // for when the buttplug client disconnects.
public record ToyboxActiveDeviceChangedMessage(int DeviceIndex) : MessageBase; // for when the active device is changed.public record PatternRemovedMessage(PatternData pattern) : MessageBase; // for when a pattern is removed.
public record PatternActivedMessage(int PatternIndex, string StartPoint, string PlaybackDuration) : MessageBase; // for when a pattern is activated.
public record PatternDeactivedMessage(int PatternIndex) : MessageBase; // for when a pattern is deactivated.
public record PatternRemovedMessage(PatternData pattern) : MessageBase; // for when a pattern is removed.
#endregion PLAYERDATA TOYBOX HANDLER RECORDS

/* ------------------ PLAYERDATA CLIENTSIDE PERMISSION HANDLING ------------------- */
public record ClientGlobalPermissionChanged(string Permission, object Value) : MessageBase; // for when a client global permission is changed.
public record ClientOwnPairPermissionChanged(Pair Pair, string Permission, object Value) : MessageBase; // for when a client pair permission is changed.
public record ClientOwnPairPermissionAccessChanged(Pair Pair, string Permission, object Value) : MessageBase; // for when a client pair permission is changed.
public record ClientOtherPairPermissionChanged(Pair Pair, string Permission, object Value) : MessageBase; // for when a client pair permission is changed
public record ClientOtherPairPermissionAccessChanged(Pair Pair, string Permission, object Value) : MessageBase; // for when a client pair permission is changed

/* ------------------ IPC HANDLER RECORDS------------------ */
public record PenumbraInitializedMessage : MessageBase;
public record PenumbraDisposedMessage : MessageBase;
public record UpdateGlamourMessage(GlamourUpdateType GenericUpdateType) : MessageBase; // for full refreshes on states.
public record UpdateGlamourGagsMessage(UpdatedNewState NewState, GagLayer Layer, GagList.GagType GagType, string AssignerName) : MessageBase; // client side notifier for visual changes.
public record UpdateGlamourRestraintsMessage(UpdatedNewState NewState) : MessageBase; // Restraint set updates.
public record UpdateGlamourBlindfoldMessage(UpdatedNewState NewState, string AssignerName) : MessageBase; // Blindfold updates.
public record CustomizeProfileChanged : MessageBase; // when a profile is changed in customize+

// Whenever we update our own data (callbacks from server are updated separately to avoid loops)
public record MoodlesMessage(IntPtr Address) : MessageBase; // indicated a moodles message was published.
public record PlayerCharIpcChanged(DataUpdateKind UpdateKind) : MessageBase;
public record PlayerCharAppearanceChanged(DataUpdateKind UpdateKind) : MessageBase;
public record PlayerCharWardrobeChanged(DataUpdateKind UpdateKind) : MessageBase;
public record PlayerCharAliasChanged(string UpdatedPairUID) : MessageBase;
public record PlayerCharToyboxChanged(DataUpdateKind UpdateKind) : MessageBase;

public record CharacterDataCreatedMessage(CharacterCompositeData CharacterData) : MessageBase;
public record CharacterIpcDataCreatedMessage(CharacterIPCData CharacterIPCData, DataUpdateKind UpdateKind) : SameThreadMessage;
public record CharacterAppearanceDataCreatedMessage(CharacterAppearanceData CharacterAppearanceData, DataUpdateKind UpdateKind) : SameThreadMessage;
public record CharacterWardrobeDataCreatedMessage(CharacterWardrobeData CharacterWardrobeData, DataUpdateKind UpdateKind) : SameThreadMessage;
public record CharacterAliasDataCreatedMessage(CharacterAliasData CharacterAliasData, UserData userData) : SameThreadMessage;
public record CharacterToyboxDataCreatedMessage(CharacterToyboxData CharacterToyboxData, DataUpdateKind UpdateKind) : SameThreadMessage;


/* ------------------ USER INTERFACE (UI) RECORDS------------------ */
public record RefreshUiMessage : MessageBase; // a message indicating the need to refresh the UI.
public record UiToggleMessage(Type UiType) : MessageBase; // For toggling the UI.
public record SwitchToIntroUiMessage : MessageBase; // indicates that we are in the introduction UI.
public record SwitchToMainUiMessage : MessageBase; // indicates we are in the main UI.
public record OpenSettingsUiMessage : MessageBase; // indicates we are in the settings UI.
public record ClosedMainUiMessage : MessageBase; // indicates the main UI has been closed.
public record RemoveWindowMessage(WindowMediatorSubscriberBase Window) : MessageBase; // fired upon request to remove a window from the UI service.
public record CompactUiChange(Vector2 Size, Vector2 Position) : MessageBase; // fired whenever we change the window size or position

public record GlobalChatMessage(GlobalChatMessageDto ChatMessage) : MessageBase;
public record OpenPrivateRoomRemote(PrivateRoom PrivateRoom) : MessageBase; // unique for each private room.
public record ProfileOpenStandaloneMessage(Pair Pair) : MessageBase; // for opening the profile standlone window.
public record ProfilePopoutToggle(Pair? Pair) : MessageBase; // toggles the profile popout window for a paired client.
public record ClearProfileDataMessage(UserData? UserData = null) : MessageBase; // a message indicating the need to clear profile data.
public record VerificationPopupMessage(VerificationDto VerificationCode) : MessageBase; // indicating that we have received a verification code popup.
public record PatternSavePromptMessage(List<byte> StoredData, string Duration) : MessageBase; // prompts the popup and passes in savedata
public record BlindfoldUiTypeChange(BlindfoldType NewType) : MessageBase; // for changing blindfold type.

#pragma warning restore S2094
#pragma warning restore MA0048 // File name must match type name
