using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
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
    private readonly ApiController _apiController;
    private readonly UiSharedService _uiSharedService;
    private readonly DiscoverService _discoveryService;

    public MainUiChat(ILogger<MainUiChat> logger,
        GagspeakMediator mediator, ApiController apiController,
        UiSharedService uiSharedService,
        DiscoverService discoverService) : base(logger, mediator)
    {
        _apiController = apiController;
        _uiSharedService = uiSharedService;
        _discoveryService = discoverService;

        Mediator.Subscribe<DisconnectedMessage>(this, (_) =>
        {
            // do stuff i guess.
        });
    }

    private string NextChatMessage = string.Empty;

    /// <summary> Main Draw function for this tab </summary>
    public void DrawDiscoverySection()
    {
        // stuff
        DrawGlobalChatlog();
    }
    private bool shouldFocusChatInput = false;

    private void DrawGlobalChatlog()
    {
        // grab the content region of the current section
        var CurrentRegion = ImGui.GetContentRegionAvail();

        // center cursor
        ImGuiUtil.Center("Global GagSpeak Chat");
        // icon to display
        FontAwesomeIcon Icon = _discoveryService.GagspeakGlobalChat.Autoscroll ? FontAwesomeIcon.ArrowDownUpLock: FontAwesomeIcon.ArrowDownUpAcrossLine;
        ImGui.Separator();

        // Calculate the height for the chat log, leaving space for the input text field
        float inputTextHeight = ImGui.GetFrameHeightWithSpacing();
        float chatLogHeight = CurrentRegion.Y - inputTextHeight;

        // Create a child for the chat log
        using (var chatlogChild = ImRaii.Child($"###ChatlogChild", new Vector2(CurrentRegion.X, chatLogHeight - inputTextHeight), false))
        {
            _discoveryService.GagspeakGlobalChat.PrintImgui();
        }

        // Now draw out the input text field
        var nextMessageRef = NextChatMessage;

        ImGui.SetNextItemWidth(CurrentRegion.X - _uiSharedService.GetIconButtonSize(Icon).X - ImGui.GetStyle().ItemInnerSpacing.X);
        //ImGui.SetKeyboardFocusHere();
        if (ImGui.InputTextWithHint("##ChatInputBox", "chat message here...", ref nextMessageRef, 300))
        {
            // Update the stored message
            NextChatMessage = nextMessageRef;
        }
        // Check if the input text field is focused and the Enter key is pressed
        if (ImGui.IsItemFocused() && ImGui.IsKeyPressed(ImGuiKey.Enter))
        {
            shouldFocusChatInput = true;
            // if the message content is empty, return
            if (string.IsNullOrWhiteSpace(NextChatMessage)) return;
            // Send the message to the server
            Logger.LogTrace($"Sending Message: {NextChatMessage}");
            _apiController.SendGlobalChat(new GlobalChatMessageDto(ApiController.PlayerUserData, NextChatMessage)).ConfigureAwait(false);
            NextChatMessage = string.Empty;
            // Give Achievement Progress for sending message:
            UnlocksEventManager.AchievementEvent(UnlocksEvent.GlobalSent);
        }
        if (shouldFocusChatInput)
        {
            ImGui.SetKeyboardFocusHere(-1);
            shouldFocusChatInput = false;
        }

        // Set focus to the input box if the flag is set
        ImUtf8.SameLineInner();
        if (_uiSharedService.IconButton(Icon))
        {
            _discoveryService.GagspeakGlobalChat.Autoscroll = !_discoveryService.GagspeakGlobalChat.Autoscroll;
        }
    }
}

