using Dalamud.Interface.Utility.Raii;
using GagSpeak.Services;
using GagSpeak.Services.Mediator;
using GagSpeak.WebAPI;
using GagspeakAPI.Dto.Toybox;
using ImGuiNET;
using OtterGui;
using System.Numerics;

namespace GagSpeak.UI.MainWindow;

// this can easily become the "contact list" tab of the "main UI" window.
public class MainUiDiscover : DisposableMediatorSubscriberBase
{
    private readonly ApiController _apiController;
    private readonly DiscoverService _discoveryService;

    public MainUiDiscover(ILogger<MainUiDiscover> logger,
        GagspeakMediator mediator, ApiController apiController, 
        DiscoverService discoverService) : base(logger, mediator)
    {
        _apiController = apiController;
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
    private void DrawGlobalChatlog()
    {
        // grab the content region of the current section
        var CurrentRegion = ImGui.GetContentRegionAvail();

        ImGuiUtil.Center("Global GagSpeak Chat");
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
        ImGui.SetNextItemWidth(CurrentRegion.X);
        if (ImGui.InputTextWithHint("##ChatInputBox", "chat message here...", ref nextMessageRef, 300))
        {
            // Update the stored message
            NextChatMessage = nextMessageRef;
        }

        // Check if the input text field is focused and the Enter key is pressed
        if (ImGui.IsItemFocused() && ImGui.IsKeyPressed(ImGuiKey.Enter))
        {
            // Send the message to the server
            Logger.LogTrace($"Sending Message: {NextChatMessage}");
            _apiController.SendGlobalChat(new GlobalChatMessageDto(_apiController.PlayerUserData, NextChatMessage)).ConfigureAwait(false);
            NextChatMessage = string.Empty;
        }
    }
}

