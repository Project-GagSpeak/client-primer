using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using GagSpeak.PlayerData.Data;
using GagSpeak.Services;
using GagSpeak.Services.Mediator;
using GagSpeak.WebAPI;
using GagspeakAPI.Dto.Toybox;
using ImGuiNET;
using OtterGui;
using OtterGui.Text;
using System.Numerics;

namespace GagSpeak.UI.MainWindow;

// this can easily become the "contact list" tab of the "main UI" window.
public class MainUiChat : DisposableMediatorSubscriberBase
{
    private readonly MainHub _apiHubMain;
    private readonly PlayerCharacterData _playerManager;
    private readonly GagManager _gagManager;
    private readonly UiSharedService _uiSharedService;
    private readonly DiscoverService _discoveryService;

    public MainUiChat(ILogger<MainUiChat> logger, GagspeakMediator mediator, 
        MainHub apiHubMain, PlayerCharacterData playerManager, GagManager gagManager,
        UiSharedService uiSharedService, DiscoverService discoverService) 
        : base(logger, mediator)
    {
        _apiHubMain = apiHubMain;
        _playerManager = playerManager;
        _gagManager = gagManager;
        _uiSharedService = uiSharedService;
        _discoveryService = discoverService;
    }

    private string NextChatMessage = string.Empty;

    /// <summary> Main Draw function for this tab </summary>
    public void DrawDiscoverySection()
    {
        // stuff
        DrawGlobalChatlog();
    }
    private bool shouldFocusChatInput = false;
    private bool showMessagePreview = false;

    private void DrawGlobalChatlog()
    {
        // grab the content region of the current section
        var CurrentRegion = ImGui.GetContentRegionAvail();
        ImGuiUtil.Center("Global GagSpeak Chat");


        FontAwesomeIcon Icon = DiscoverService.GlobalChat.Autoscroll ? FontAwesomeIcon.ArrowDownUpLock: FontAwesomeIcon.ArrowDownUpAcrossLine;
        ImGui.Separator();

        // Calculate the height for the chat log, leaving space for the input text field
        float inputTextHeight = ImGui.GetFrameHeightWithSpacing();
        float chatLogHeight = CurrentRegion.Y - inputTextHeight;

        // Create a child for the chat log
        using (var chatlogChild = ImRaii.Child($"###ChatlogChild", new Vector2(CurrentRegion.X, chatLogHeight - inputTextHeight), false))
        {
            DiscoverService.GlobalChat.PrintChatLogHistory();
        }

        // Now draw out the input text field
        var nextMessageRef = NextChatMessage;

        // Set keyboard focus to the chat input box if needed
        if (shouldFocusChatInput)
        {
            ImGui.SetKeyboardFocusHere(0);
            shouldFocusChatInput = false;
        }

        // Set width for input box and create it with a hint
        ImGui.SetNextItemWidth(CurrentRegion.X - _uiSharedService.GetIconButtonSize(Icon).X - ImGui.GetStyle().ItemInnerSpacing.X);
        if (ImGui.InputTextWithHint("##ChatInputBox", "chat message here...", ref nextMessageRef, 300))
        {
            // Update stored message
            NextChatMessage = nextMessageRef;
        }

        // Check if the input text field is focused and Enter is pressed
        if (ImGui.IsItemFocused() && ImGui.IsKeyPressed(ImGuiKey.Enter))
        {
            shouldFocusChatInput = true;

            // If message is empty, return
            if (string.IsNullOrWhiteSpace(NextChatMessage))
                return;

            // Process message if gagged
            if (_playerManager.IsPlayerGagged)
                NextChatMessage = _gagManager.ProcessMessage(NextChatMessage);

            // Send message to the server
            Logger.LogTrace($"Sending Message: {NextChatMessage}");
            _apiHubMain.SendGlobalChat(new GlobalChatMessageDto(MainHub.PlayerUserData, NextChatMessage)).ConfigureAwait(false);

            // Clear message and trigger achievement event
            NextChatMessage = string.Empty;
            UnlocksEventManager.AchievementEvent(UnlocksEvent.GlobalSent);
        }

        // Update preview display based on input field activity
        showMessagePreview = ImGui.IsItemActive();

        // Toggle AutoScroll functionality
        ImUtf8.SameLineInner();
        if (_uiSharedService.IconButton(Icon))
            DiscoverService.GlobalChat.Autoscroll = !DiscoverService.GlobalChat.Autoscroll;
        UiSharedService.AttachToolTip("Toggles the AutoScroll Functionality (Current: " + (DiscoverService.GlobalChat.Autoscroll ? "Enabled" : "Disabled") + ")");

        // Draw the text wrap box if the user is typing
        if (showMessagePreview && !string.IsNullOrWhiteSpace(NextChatMessage))
        {
            DrawTextWrapBox(NextChatMessage, CurrentRegion);
        }

    }

    private void DrawTextWrapBox(string message, Vector2 currentRegion)
    {
        var drawList = ImGui.GetWindowDrawList();
        var padding = new Vector2(5, 2);

        // Set the wrap width based on the available region
        var wrapWidth = currentRegion.X - padding.X * 2;

        // Estimate text size with wrapping
        var textSize = ImGui.CalcTextSize(message, wrapWidth: wrapWidth);

        // Calculate the height of a single line for the given wrap width
        float singleLineHeight = ImGui.CalcTextSize("A").Y;
        int lineCount = (int)Math.Ceiling(textSize.Y / singleLineHeight);

        // Calculate the total box size based on line count
        var boxSize = new Vector2(currentRegion.X, lineCount * singleLineHeight + padding.Y * 2);

        // Position the box above the input, offset by box height
        var boxPos = ImGui.GetCursorScreenPos() - new Vector2(0, boxSize.Y + 30);

        // Draw semi-transparent background
        drawList.AddRectFilled(boxPos, boxPos + boxSize, ImGui.GetColorU32(new Vector4(0.973f, 0.616f, 0.839f, 0.490f)), 5);

        // Begin a child region for the wrapped text
        ImGui.SetCursorScreenPos(boxPos + padding);
        using (ImRaii.Child("##TextWrapBox", new Vector2(wrapWidth, boxSize.Y), false, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
        {
            ImGui.PushTextWrapPos(ImGui.GetCursorPos().X + wrapWidth);
            ImGui.TextWrapped(message);
            ImGui.PopTextWrapPos();
        }

        // Reset cursor to avoid overlap
        ImGui.SetCursorScreenPos(boxPos + new Vector2(0, boxSize.Y + 5));
    }
}

