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
using GagspeakAPI.Enums;
using GagspeakAPI.Dto.Connection;
using Glamourer.Api.Enums;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using System.Numerics;
using GagspeakAPI.Data;
using GagSpeak.PlayerData.PrivateRooms;
using GagspeakAPI.Dto.Toybox;
using GagspeakAPI.Dto.IPC;
using GagspeakAPI.Data.Permissions;
using GagSpeak.UI.Components;
using GagSpeak.Achievements;

namespace GagSpeak.Services.Mediator;

#pragma warning disable MA0048, S2094

/* ------------------ MESSAGE RELATED RECORDS ------------------ */
public record NotificationMessage(string Title, string Message, NotificationType Type, TimeSpan? TimeShownOnScreen = null) : MessageBase;
public record EventMessage(Event Event) : MessageBase;


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
public record OpenPrivateRoomRemote(PrivateRoom PrivateRoom) : MessageBase; // unique for each private room.


/* ------------- DALAMUD FRAMEWORK UPDATE RECORDS ------------- */
public record DalamudLoginMessage : MessageBase; // record indicating the moment the client logs into the game instance.
public record DalamudLogoutMessage : MessageBase; // record indicating the moment the client logs out of the game instance.
public record FrameworkUpdateMessage : SameThreadMessage; // a message indicating the need for a framework update.
public record DelayedFrameworkUpdateMessage : SameThreadMessage; // a message indicating the need for a delayed framework update.
public record GPoseStartMessage : MessageBase ; // a message indicating the start of gpose.
public record GPoseEndMessage : MessageBase; // a message indicating the end of gpose.
public record CutsceneBeginMessage : MessageBase;
public record CutsceneEndMessage : MessageBase; // helps us know when to reapply data like moodles.
public record ZoneSwitchStartMessage : MessageBase; // know when we are beginning to switch zones
public record ZoneSwitchEndMessage : MessageBase; // know when we have finished switching zones
public record CommendationsIncreasedMessage(int amount) : MessageBase;

/* ------------------ PLAYER DATA RELATED RECORDS------------------ */
public record UpdateAllOnlineWithCompositeMessage : MessageBase; // for updating all online pairs with composite data.
public record PairWentOnlineMessage(UserData UserData) : MessageBase; // a message indicating a pair has gone online.
public record PairHandlerVisibleMessage(PairHandler Player) : MessageBase; // a message indicating the visibility of a pair handler.
public record OpenUserPairPermissions(Pair? Pair, StickyWindowType PermsWindowType, bool ForceOpenMainUI) : MessageBase; // fired upon request to open the permissions window for a pair
public record TargetPairMessage(Pair Pair) : MessageBase; // called when publishing a targeted pair connection (see UI)
public record CyclePauseMessage(UserData UserData) : MessageBase; // for cycling the paused state of self
public record CreateCacheForObjectMessage(GameObjectHandler ObjectToCreateFor) : MessageBase;
public record ClearCacheForObjectMessage(GameObjectHandler ObjectToCreateFor) : MessageBase; // called when we should clear a gameobject from cache creation service.
public record MufflerLanguageChanged : MessageBase; // called whenever the client language changes to a new language.

/* ------------- PLAYER DATA MODULE INTERACTIONS --------- */
public record UpdateActiveGags(TaskCompletionSource<bool>? CompletionTaskSource = null) : MessageBase;
public record ActiveGagsUpdated : MessageBase;
public record ActiveLocksUpdated : MessageBase;
public record GagTypeChanged(GagType NewGagType, GagLayer Layer, bool SelfApplied = false) : MessageBase; // called whenever the client changes their gag type.
public record GagLockToggle(PadlockData PadlockInfo, NewState newGagLockState, bool SelfApplied = false) : MessageBase; // called whenever the client changes their padlock.
public record TooltipSetItemToRestraintSetMessage(EquipSlot Slot, EquipItem Item) : MessageBase;
public record HelmetStateChangedMessage(bool ChangedState) : MessageBase; // called whenever the client changes their helmet state.
public record VisorStateChangedMessage(bool ChangedState) : MessageBase; // called whenever the client changes their visor state.

////////////// WARDROBE RELATED RECORDS //////////////
public record RestraintSetToggleModsMessage(int SetIdx, NewState State, TaskCompletionSource<bool>? ModToggleTask = null) : MessageBase; 
public record RestraintSetToggleMoodlesMessage(int SetIdx, NewState State, TaskCompletionSource<bool>? MoodlesTask = null) : MessageBase;
public record RestraintSetToggleHardcoreTraitsMessage(int SetIdx, string AssignerUID, NewState State, TaskCompletionSource<bool>? HardcoreTraitsTask = null) : MessageBase;
public record RestraintSetToggledMessage(int SetIdx, string AssignerUID, NewState State, bool pushChanges, TaskCompletionSource<bool>? GlamourChangeTask = null) : MessageBase; 
public record HardcoreForcedToFollowMessage(Pair Pair, NewState State) : MessageBase;
public record HardcoreForcedToSitMessage(Pair Pair, NewState State) : MessageBase; 
public record HardcoreForcedToKneelMessage(Pair Pair, NewState State) : MessageBase;
public record HardcoreForcedToStayMessage(Pair Pair, NewState State) : MessageBase;
public record HardcoreForcedBlindfoldMessage(Pair Pair, NewState State) : MessageBase;
public record HardcoreRemoveBlindfoldMessage : MessageBase;
public record HardcoreUpdatedShareCodeForPair(Pair pair, string ShareCode) : MessageBase;
public record MovementRestrictionChangedMessage(MovementRestrictionType Type, NewState NewState) : MessageBase;
public record MoodlesPermissionsUpdated(string NameWithWorld) : MessageBase;

////////////// PUPPETEER RELATED RECORDS //////////////
public record UpdateChatListeners : MessageBase; // for updating the chat listeners.
public record UpdateCharacterListenerForUid(string Uid, string CharName, string CharWorld) : MessageBase;


////////////// TOYBOX RELATED RECORDS //////////////
public record VfxActorRemoved(IntPtr data) : MessageBase;
public record ToyScanStarted : MessageBase; // for when the toybox scan is started.
public record ToyScanFinished : MessageBase; // for when the toybox scan is finished.
public record VibratorModeToggled(VibratorMode VibratorMode) : MessageBase; // for when the vibrator mode is toggled.
public record ToyDeviceAdded(ButtplugClientDevice Device) : MessageBase; // for when a device is added.
public record ToyDeviceRemoved(ButtplugClientDevice Device) : MessageBase; // for when a device is removed.
public record ButtplugClientDisconnected : MessageBase; // for when the buttplug client disconnects.
public record ToyboxActiveDeviceChangedMessage(int DeviceIndex) : MessageBase; 
public record PlaybackStateToggled(Guid PatternId, NewState NewState) : MessageBase; // for when a pattern is activated.
public record PatternRemovedMessage(Guid PatternId) : MessageBase; // for when a pattern is removed.
public record TriggersModifiedMessage : MessageBase;
public record ExecuteHealthPercentTriggerMessage(HealthPercentTrigger Trigger) : MessageBase;


/* ------------------ PLAYERDATA CLIENTSIDE PERMISSION HANDLING ------------------- */
public record ClientGlobalPermissionChanged(string Permission, object Value) : MessageBase; // for when a client global permission is changed.
public record ClientOwnPairPermissionChanged(Pair Pair, string Permission, object Value) : MessageBase; // for when a client pair permission is changed.
public record ClientOwnPairPermissionAccessChanged(Pair Pair, string Permission, object Value) : MessageBase; // for when a client pair permission is changed.
public record ClientOtherPairPermissionChanged(Pair Pair, string Permission, object Value) : MessageBase; // for when a client pair permission is changed
public record ClientOtherPairPermissionAccessChanged(Pair Pair, string Permission, object Value) : MessageBase; // for when a client pair permission is changed
public record PlayerCharIpcChanged(DataUpdateKind UpdateKind) : MessageBase;
public record PlayerCharAppearanceChanged(DataUpdateKind UpdateKind) : MessageBase;
public record PlayerCharWardrobeChanged(DataUpdateKind UpdateKind) : MessageBase;
public record PlayerCharAliasChanged(string UpdatedPairUID, DataUpdateKind UpdateKind) : MessageBase;
public record PlayerCharToyboxChanged(DataUpdateKind UpdateKind) : MessageBase;


/* ------------------ IPC HANDLER RECORDS------------------ */
public record PenumbraInitializedMessage : MessageBase;
public record PenumbraDisposedMessage : MessageBase;
public record UpdateGlamourGagsMessage(NewState NewState, GagLayer Layer, GagType GagType): MessageBase;
public record UpdateGlamourRestraintsMessage(NewState NewState, TaskCompletionSource<bool>? CompletionTaskSource = null) : MessageBase; // Restraint set updates.
public record UpdateGlamourBlindfoldMessage(NewState NewState, string AssignerName) : MessageBase; // Blindfold updates.
public record MoodlesReady : MessageBase;
public record GlamourerReady : MessageBase;
public record CustomizeReady : MessageBase;
public record CustomizeDispose : MessageBase;
public record MoodlesStatusManagerChangedMessage(IntPtr Address) : MessageBase; // when our status manager changes.
public record MoodlesStatusModified(Guid Guid) : MessageBase; // when we change one of our moodles settings.
public record MoodlesPresetModified(Guid Guid) : MessageBase; // when we change one of our moodles presets.
public record MoodlesApplyStatusToPair(ApplyMoodlesByStatusDto StatusDto) : MessageBase;
public record MoodlesUpdateNotifyMessage : MessageBase; // for pinging the moodles.


/* ----------------- Character Cache Creation Records ----------------- */
public record CharacterDataCreatedMessage(CharacterIPCData CharacterData) : MessageBase; // TODO: See how to remove this?
public record CharacterIpcDataCreatedMessage(CharacterIPCData CharacterIPCData, DataUpdateKind UpdateKind) : SameThreadMessage;
public record CharacterAppearanceDataCreatedMessage(CharacterAppearanceData CharacterAppearanceData, DataUpdateKind UpdateKind) : SameThreadMessage;
public record CharacterWardrobeDataCreatedMessage(CharacterWardrobeData CharacterWardrobeData, DataUpdateKind UpdateKind) : SameThreadMessage;
public record CharacterAliasDataCreatedMessage(CharacterAliasData CharacterAliasData, UserData userData, DataUpdateKind UpdateKind) : SameThreadMessage;
public record CharacterToyboxDataCreatedMessage(CharacterToyboxData CharacterToyboxData, DataUpdateKind UpdateKind) : SameThreadMessage;
public record CharacterPiShockPermDataCreatedMessage(string ShareCode, PiShockPermissions ShockPermsForPair, UserData UserData, DataUpdateKind UpdateKind) : SameThreadMessage;
public record CharacterPiShockGlobalPermDataUpdatedMessage(PiShockPermissions GlobalShockPermissions, DataUpdateKind UpdateKind) : SameThreadMessage;

public record GameObjectHandlerCreatedMessage(GameObjectHandler GameObjectHandler, bool OwnedObject) : MessageBase;
public record GameObjectHandlerDestroyedMessage(GameObjectHandler GameObjectHandler, bool OwnedObject) : MessageBase;


/* ------------------ USER INTERFACE (UI) RECORDS------------------ */
public enum ToggleType { Toggle, Show, Hide }
public record RefreshUiMessage : MessageBase; // a message indicating the need to refresh the UI.
public record UiToggleMessage(Type UiType, ToggleType ToggleType = ToggleType.Toggle) : MessageBase; // For toggling the UI.
public record SwitchToIntroUiMessage : MessageBase; // indicates that we are in the introduction UI.
public record SwitchToMainUiMessage : MessageBase; // indicates we are in the main UI.
public record OpenSettingsUiMessage : MessageBase; // indicates we are in the settings UI.
public record MainWindowTabChangeMessage(MainTabMenu.SelectedTab NewTab) : MessageBase; // for changing the main window tab.
public record ClosedMainUiMessage : MessageBase; // indicates the main UI has been closed.
public record RemoveWindowMessage(WindowMediatorSubscriberBase Window) : MessageBase; // fired upon request to remove a window from the UI service.
public record CompactUiChange(Vector2 Size, Vector2 Position) : MessageBase; // fired whenever we change the window size or position
public record ProfileOpenStandaloneMessage(Pair Pair) : MessageBase; // for opening the profile standlone window.
public record ProfilePopoutToggle(Pair? Pair) : MessageBase; // toggles the profile popout window for a paired client.
public record ClearProfileDataMessage(UserData? UserData = null) : MessageBase; // a message indicating the need to clear profile data.
public record ReportGagSpeakProfileMessage(Pair PairToReport) : MessageBase; // for reporting a GagSpeak profile.
public record VerificationPopupMessage(VerificationDto VerificationCode) : MessageBase; // indicating that we have received a verification code popup.
public record PatternSavePromptMessage(List<byte> StoredData, TimeSpan Duration) : MessageBase; // prompts the popup and passes in savedata
public record BlindfoldUiTypeChange(BlindfoldType NewType) : MessageBase; // for changing blindfold type.
public record ToggleDtrBarMessage : MessageBase;

/* -------------------- DISCOVER TAB RECORDS -------------------- */
public record GlobalChatMessage(GlobalChatMessageDto ChatMessage, bool FromSelf) : MessageBase;
public record SafewordUsedMessage : MessageBase; // for when the safeword is used.
public record SafewordHardcoreUsedMessage : MessageBase; // for when the hardcore safeword is used.

/* --------------------- COSMETICS & ACHIEVEMENTS RECORDS --------------------- */
public record AchievementProgressMessage<T>(AchievementType Component, string AchievementName, T NewProgressMade) : MessageBase; // for updating achievement progress.


#pragma warning restore S2094, MA0048
