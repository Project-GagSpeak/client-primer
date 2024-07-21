using Dalamud.Interface.Colors;
using Dalamud.Interface;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using GagSpeak.WebAPI;
using GagspeakAPI.Data.Enum;
using ImGuiNET;
using System.Globalization;
using Dalamud.Interface.Utility.Raii;

namespace GagSpeak.UI.UiToybox;

public class ToyboxVibeServer : DisposableMediatorSubscriberBase
{
    private readonly ApiController _apiController;
    private readonly UiSharedService _uiShared;
    private readonly ServerConfigurationManager _serverConfigs;

    public ToyboxVibeServer(ILogger<ToyboxVibeServer> logger, 
        GagspeakMediator mediator, ApiController apiController,
        UiSharedService uiShared, ServerConfigurationManager serverConfigs
        ) : base(logger, mediator)
    {
        _apiController = apiController;
        _uiShared = uiShared;
        _serverConfigs = serverConfigs;
    }

    public void DrawVibeServerPanel()
    {
        // draw the connection interface
        DrawToyboxServerStatus();

        ImGui.Text("Vibe Server InteractionsUI");
    }

    private void DrawToyboxServerStatus()
    {
        var buttonSize = _uiShared.GetIconButtonSize(FontAwesomeIcon.Link);
        var userCount = _apiController.ToyboxOnlineUsers.ToString(CultureInfo.InvariantCulture);
        var userSize = ImGui.CalcTextSize(userCount);
        var textSize = ImGui.CalcTextSize("Toybox Users Online");

        string shardConnection = $"GagSpeak Toybox Server";

        var shardTextSize = ImGui.CalcTextSize(shardConnection);
        var printShard = shardConnection != string.Empty;

        // if the server is connected, then we should display the server info
        if (_apiController.ToyboxServerState is ServerState.Connected)
        {
            // fancy math shit for clean display, adjust when moving things around
            ImGui.SetCursorPosX((ImGui.GetWindowContentRegionMin().X + UiSharedService.GetWindowContentRegionWidth()) / 2 - (userSize.X + textSize.X) / 2 - ImGui.GetStyle().ItemSpacing.X / 2);
            ImGui.TextColored(ImGuiColors.ParsedGreen, userCount);
            ImGui.SameLine();
            ImGui.TextUnformatted("Toybox Users Online");
        }
        // otherwise, if we are not connected, display that we aren't connected.
        else
        {
            ImGui.AlignTextToFramePadding();
            ImGui.SetCursorPosX((ImGui.GetWindowContentRegionMin().X
                + UiSharedService.GetWindowContentRegionWidth())
                / 2 - (ImGui.CalcTextSize("Not connected to the toybox server").X) / 2 - ImGui.GetStyle().ItemSpacing.X / 2);
            ImGui.TextColored(ImGuiColors.DalamudRed, "Not connected to the toybox server");
        }

        ImGui.SetCursorPosY(ImGui.GetCursorPosY() - ImGui.GetStyle().ItemSpacing.Y);
        ImGui.SetCursorPosX((ImGui.GetWindowContentRegionMin().X + UiSharedService.GetWindowContentRegionWidth()) / 2 - shardTextSize.X / 2);
        ImGui.TextUnformatted(shardConnection);
        ImGui.SameLine();

        // now we need to display the connection link button beside it.
        var color = UiSharedService.GetBoolColor(!_serverConfigs.CurrentServer!.ToyboxFullPause);
        var connectedIcon = !_serverConfigs.CurrentServer.ToyboxFullPause ? FontAwesomeIcon.Link : FontAwesomeIcon.Unlink;

        ImGui.SameLine(ImGui.GetWindowContentRegionMin().X + UiSharedService.GetWindowContentRegionWidth() - buttonSize.X);
        if (printShard)
        {
            // unsure what this is doing but we can find out lol
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() - ((userSize.Y + textSize.Y) / 2 + shardTextSize.Y) / 2 - ImGui.GetStyle().ItemSpacing.Y + buttonSize.Y / 2);
        }

        // if the server is reconnecting or disconnecting
        if (_apiController.ToyboxServerState is not (ServerState.Reconnecting or ServerState.Disconnecting))
        {
            // we need to turn the button from the connected link to the disconnected link.
            using (ImRaii.PushColor(ImGuiCol.Text, color))
            {
                // then display it
                if (_uiShared.IconButton(connectedIcon))
                {
                    // and toggle the fullpause for the current server, save the config, and recreate the connections,
                    // placing it into a disconnected state due to the full pause being active. (maybe change this later)
                    _serverConfigs.CurrentServer.ToyboxFullPause = !_serverConfigs.CurrentServer.ToyboxFullPause;
                    _serverConfigs.Save();
                    _ = _apiController.CreateToyboxConnection();
                }
            }
            // attach the tooltip for the connection / disconnection button)
            UiSharedService.AttachToolTip(!_serverConfigs.CurrentServer.ToyboxFullPause
                ? "Disconnect from Toybox Server" : "Connect to ToyboxServer");
        }

        // draw out the vertical slider.
        ImGui.Separator();
    }
}
