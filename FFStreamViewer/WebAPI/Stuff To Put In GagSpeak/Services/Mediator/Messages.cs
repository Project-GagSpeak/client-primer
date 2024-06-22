using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Interface.Internal.Notifications;
using FFStreamViewer.WebAPI.PlayerData.Handlers;
using FFStreamViewer.WebAPI.Services.Events;
using Gagspeak.API.Data.CharacterData;
using Gagspeak.API.Data;
using Gagspeak.API.Dto;
using FFStreamViewer.WebAPI.PlayerData.Pairs;
using System.Numerics;

namespace FFStreamViewer.WebAPI.Services.Mediator;

#pragma warning disable MA0048 // File name must match type name
#pragma warning disable S2094

// clarified final usage records:
public record NotificationMessage // the record indicating a notification message that should be send to the client.
    (string Title, string Message, NotificationType Type, TimeSpan? TimeShownOnScreen = null) : MessageBase;

public record DisconnectedMessage : SameThreadMessage; // indicating a disconnection message from the server.
public record HubReconnectingMessage(Exception? Exception) : SameThreadMessage; // indicating the moment the hub is reconnecting.
public record HubReconnectedMessage(string? Arg) : SameThreadMessage; // indicating the moment the hub has reconnected.
public record HubClosedMessage(Exception? Exception) : SameThreadMessage; // indicating the moment the hub has closed.
public record ConnectedMessage(ConnectionDto Connection) : MessageBase; // message published upon sucessful connection to the server
public record DalamudLoginMessage : MessageBase; // record indicating the moment the client logs into the game instance.
public record DalamudLogoutMessage : MessageBase; // record indicating the moment the client logs out of the game instance.
public record EventMessage(Event Event) : MessageBase; // an event message for logging purposes. (used in APIController, see there for ref
public record PairHandlerVisibleMessage(PairHandler Player) : MessageBase; // a message indicating the visibility of a pair handler.
public record RefreshUiMessage : MessageBase; // a message indicating the need to refresh the UI.
public record ClearProfileDataMessage(UserData? UserData = null) : MessageBase; // a message indicating the need to clear profile data.
public record VerificationPopupMessage(VerificationDto VerificationCode) : MessageBase; // indicating that we have received a verification code popup.
public record PriorityFrameworkUpdateMessage : SameThreadMessage; // a message indicating the need for a priority framework update.
public record FrameworkUpdateMessage : SameThreadMessage; // a message indicating the need for a framework update.
public record DelayedFrameworkUpdateMessage : SameThreadMessage; // a message indicating the need for a delayed framework update.
public record UiToggleMessage(Type UiType) : MessageBase; // For toggling the UI.
public record ProfileOpenStandaloneMessage(Pair Pair) : MessageBase; // for opening the profile standlone window.
public record CutsceneEndMessage : MessageBase; // helps us know when to reapply data like moodles.
public record CharacterDataCreatedMessage(CharacterData CharacterData) : SameThreadMessage; // indicates the creation of character data. (part of cache creation so may dump)
public record GameObjectHandlerCreatedMessage(GameObjectHandler GameObjectHandler, bool OwnedObject) : MessageBase; // whenever a gameobjecthandler for a pair is made
public record GameObjectHandlerDestroyedMessage(GameObjectHandler GameObjectHandler, bool OwnedObject) : MessageBase; // whenever a gameobjecthandler for a pair is destroyed
public record MoodlesMessage(IntPtr Address) : MessageBase; // indicated a moodles message was published.
public record SwitchToIntroUiMessage : MessageBase; // indicates that we are in the introduction UI.
public record SwitchToMainUiMessage : MessageBase; // indicates we are in the main UI.
public record OpenSettingsUiMessage : MessageBase; // indicates we are in the settings UI.
public record ZoneSwitchStartMessage : MessageBase; // know when we are beginning to switch zones
public record ZoneSwitchEndMessage : MessageBase; // know when we have finished switching zones
public record HaltScanMessage(string Source) : MessageBase; // know when we should stop scanning
public record ResumeScanMessage(string Source) : MessageBase; // know when we should resume scanning
public record OpenPermissionWindow(Pair Pair) : MessageBase; // fired upon request to open the permissions window for a pair
public record RemoveWindowMessage(WindowMediatorSubscriberBase Window) : MessageBase; // fired upon request to remove a window from the UI service.
public record ProfilePopoutToggle(Pair? Pair) : MessageBase; // toggles the profile popout window for a paired client.
public record TargetPairMessage(Pair Pair) : MessageBase; // called when publishing a targetted pair connection (see UI)
public record CompactUiChange(Vector2 Size, Vector2 Position) : MessageBase; // fired whenever we change the window size or position



/* Some methods that might be useful to integrate as events into the IPC in gagspeak
public record PenumbraModSettingChangedMessage : MessageBase;
public record PenumbraInitializedMessage : MessageBase;
public record PenumbraDisposedMessage : MessageBase;
public record PenumbraRedrawMessage(IntPtr Address, int ObjTblIdx, bool WasRequested) : SameThreadMessage;
public record GlamourerChangedMessage(IntPtr Address) : MessageBase;
*/

// gagspeak records:
/*
public record ClassJobChangedMessage(GameObjectHandler GameObjectHandler) : MessageBase;
public record CutsceneStartMessage : MessageBase;
public record GposeStartMessage : MessageBase;
public record GposeEndMessage : MessageBase;
public record CutsceneFrameworkUpdateMessage : SameThreadMessage;
public record PlayerChangedMessage(CharacterData Data) : MessageBase;
public record CharacterChangedMessage(GameObjectHandler GameObjectHandler) : MessageBase;
public record TransientResourceChangedMessage(IntPtr Address) : MessageBase;
public record CreateCacheForObjectMessage(GameObjectHandler ObjectToCreateFor) : MessageBase;
public record ClearCacheForObjectMessage(GameObjectHandler ObjectToCreateFor) : MessageBase;
public record CharacterDataAnalyzedMessage : MessageBase;
public record PenumbraStartRedrawMessage(IntPtr Address) : MessageBase;
public record PenumbraEndRedrawMessage(IntPtr Address) : MessageBase;
public record PlayerUploadingMessage(GameObjectHandler Handler, bool IsUploading) : MessageBase;
public record CyclePauseMessage(UserData UserData) : MessageBase;
public record CombatOrPerformanceStartMessage : MessageBase;
public record CombatOrPerformanceEndMessage : MessageBase;
public record PenumbraDirectoryChangedMessage(string? ModDirectory) : MessageBase;
public record PenumbraRedrawCharacterMessage(Character Character) : SameThreadMessage;*/
#pragma warning restore S2094
#pragma warning restore MA0048 // File name must match type name
/*
 *     private void DrawContent(ReadOnlySpan<Pointer<Texture>> textures)
    {
        var firstAvailable = true;
        DrawTabBar(textures, ref firstAvailable);

        if (firstAvailable)
            ImGui.TextUnformatted("No Editable Materials available.");
    }

    private void DrawWindow(ReadOnlySpan<Pointer<Texture>> textures)
    {
        var flags = ImGuiWindowFlags.NoFocusOnAppearing
          | ImGuiWindowFlags.NoCollapse
          | ImGuiWindowFlags.NoDecoration
          | ImGuiWindowFlags.NoResize;

        // Set position to the right of the main window when attached
        // The downwards offset is implicit through child position.
        if (config.KeepAdvancedDyesAttached)
        {
            var position = ImGui.GetWindowPos();
            position.X += ImGui.GetWindowSize().X + ImGui.GetStyle().WindowPadding.X;
            ImGui.SetNextWindowPos(position);
            flags |= ImGuiWindowFlags.NoMove;
        }

        var size = new Vector2(7 * ImGui.GetFrameHeight() + 3 * ImGui.GetStyle().ItemInnerSpacing.X + 300 * ImGuiHelpers.GlobalScale,
            18 * ImGui.GetFrameHeightWithSpacing() + ImGui.GetStyle().WindowPadding.Y + 2 * ImGui.GetStyle().ItemSpacing.Y);
        ImGui.SetNextWindowSize(size);

        var window = ImGui.Begin("###Glamourer Advanced Dyes", flags);
        try
        {
            if (window)
                DrawContent(textures);
        }
        finally
        {
            ImGui.End();
        }
    }

    public void Draw(Actor actor, ActorState state)
    {
        _actor = actor;
        _state = state;
        if (!ShouldBeDrawn())
            return;

        if (_drawIndex!.Value.TryGetTextures(actor, out var textures))
            DrawWindow(textures);
    }
*/
