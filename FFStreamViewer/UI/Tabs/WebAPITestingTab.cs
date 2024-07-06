using System;
using Dalamud.Interface.Utility.Raii;
using ImGuiNET;
using OtterGui.Widgets;
using FFStreamViewer.Utils;
using Dalamud.Plugin.Services;
using Dalamud.Plugin;
using FFStreamViewer.WebAPI;
using System.Threading.Tasks;
using FFStreamViewer.WebAPI.SignalR.Utils;

namespace FFStreamViewer.UI.Tabs.MediaTab;
/// <summary> This class is used to handle the general tab for the FFStreamViewer plugin. </summary>
public class WebAPITestingTab : ITab, IDisposable
{
    private readonly    ILogger<WebAPITestingTab> _logger;
    private readonly    FFSV_Config             _config;            // the config for the plugin
    private readonly    IChatGui                _chat;              // the chat service for the plugin
    private readonly    IGameConfig             _gameConfig;        // the game config for the plugin
    private readonly    IClientState            _clientState;       // the client state for the plugin
    private readonly    ApiController           _apiController;     // the API controller for the plugin

    public WebAPITestingTab(ILogger<WebAPITestingTab> logger, FFSV_Config config, IChatGui chat,
        IGameConfig gameConfig, IClientState clientState, IDalamudPluginInterface dalamudPluginInterface, 
        ApiController apiController)
    {
        _logger = logger;
        _config = config;
        _chat = chat;
        _gameConfig = gameConfig;
        _clientState = clientState;
        _apiController = apiController;
    }

    /// <summary> This function is called when the tab is disposed. </summary>
    public void Dispose() { 
        // Unsubscribe from any events
    }

    /// <summary> This Function draws the content for the Media Tab </summary>
    public void DrawContent() {
        // Create a child for the Main Window (not sure if we need this without a left selection panel)
        using var child = ImRaii.Child("MainWindowChild");
        if (!child)
            return;
        // Draw the child grouping for the ConfigSettings Tab
        using (var child2 = ImRaii.Child("MediaTabChild")) {
            DrawHeader();
            DrawGeneral();
        }
    }

    private void DrawHeader()
        => WindowHeader.Draw("Web API Test Center", 0, ImGui.GetColorU32(ImGuiCol.FrameBg));

    /// <summary> This function draws the media tab contents 
    /// <para> FUTURE CONCEPT: Have a dropdown to spesify certain stream link types.
    /// However, this leads to complications </para> 
    /// </summary>
    private void DrawGeneral() {
        // define the message
        if (ImGui.Button("Connect To Server"))
        {
            // run the task
            _apiController.ConnectToServer();
            // log it
            _logger.LogInformation("Servers have been connected!", "Web API Testing");
        }
        ImGui.SameLine();
        if (ImGui.Button("Disconnect from the server"))
        {
            // do the thing
            _apiController.DisconnectFromServer();
            // let the user know the stream has been stopped
            _logger.LogInformation("Servers have been disconnected!", "Web API Testing");
        }

        // below this, we need to draw the video display
        ImGui.Text($"Display Name: {_apiController.DisplayName}");
        ImGui.Text($"Is Connected: {_apiController.IsConnected}");
        ImGui.Text($"Current Client version?: {_apiController.CurrentClientVersion}");
        ImGui.Separator();
        ImGui.Text($"Auth Failure Msg: {_apiController.AuthFailureMessage}");
        ImGui.Text($"Current Client Version: {_apiController.CurrentClientVersion}");
        ImGui.Text($"Display Name: {_apiController.DisplayName}");
        ImGui.Text($"Is Connected: {_apiController.IsConnected}");
        ImGui.Text($"Is on current version?: {_apiController.IsCurrentVersion}");
        ImGui.Text($"Total Online Users: {_apiController.OnlineUsers}");
        ImGui.Text($"Server Alive?: {_apiController.ServerAlive}");
        ImGui.Text($"Server State: {_apiController.ServerState}");
        ImGui.Text($"Server Info Dto: {_apiController.SystemInfoDto}");
        ImGui.Text($"User UID: {_apiController.UID}");
    }

    public ReadOnlySpan<byte> Label => "WebAPITesting"u8;
}
