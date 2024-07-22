using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Utility;
using GagSpeak.PlayerData.Handlers;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using GagSpeak.UI.UiRemote;
using GagSpeak.WebAPI;
using ImGuiNET;
using OtterGui.Text;
using PInvoke;
using System.Runtime.InteropServices;

namespace GagSpeak.UI.UiToybox;

public class ToyboxOverview
{
    private readonly ILogger<ToyboxOverview> _logger;
    private readonly GagspeakMediator _mediator;
    private readonly ApiController _apiController;
    private readonly UiSharedService _uiShared;
    private readonly ClientConfigurationManager _clientConfigs;
    private readonly ServerConfigurationManager _serverConfigs;
    /*    private readonly SoundPlayer _soundPlayer; // this is tmp until we find a better way (possibly via ingame SCD's)*/
    private readonly DeviceHandler _IntifaceHandler;

    public ToyboxOverview(ILogger<ToyboxOverview> logger,
        GagspeakMediator mediator, ApiController controller,
        UiSharedService uiSharedService,
        ClientConfigurationManager clientConfigs,
        ServerConfigurationManager serverConfigs,
        /*SoundPlayer soundPlayer,*/ DeviceHandler intifaceHandler)
    {
        _logger = logger;
        _mediator = mediator;
        _apiController = controller;
        _uiShared = uiSharedService;
        _clientConfigs = clientConfigs;
        _serverConfigs = serverConfigs;
        /*_soundPlayer = soundPlayer;*/
        _IntifaceHandler = intifaceHandler;

        IntifacePath = GetApplicationPath();
    }


    private string IntifacePath; // the path to intiface central.exe
    public void DrawOverviewPanel()
    {
        // draw the top display field for Intiface connectivity, similar to our other servers.
        DrawIntifaceConnectionStatus();
        // special case for the intiface connection, where if it is empty, we reset it to the default address.
        if (string.IsNullOrEmpty(_clientConfigs.GagspeakConfig.IntifaceConnectionSocket))
        {
            _clientConfigs.GagspeakConfig.IntifaceConnectionSocket = "ws://localhost:12345";
            _clientConfigs.Save();
        }
        // draw out the field for defining the intiface connection address
        string refName = _clientConfigs.GagspeakConfig.IntifaceConnectionSocket;

        ImGui.SetNextItemWidth(200f);
        if (ImGui.InputTextWithHint($"Server Address##ConnectionWSaddr", "Leave blank for default...",
            ref refName, 100, ImGuiInputTextFlags.EnterReturnsTrue))
        {
            // do not allow the change if it doesnt contain ws://
            if (!refName.Contains("ws://"))
            {
                refName = "ws://localhost:12345";

            }
            else
            {
                _clientConfigs.GagspeakConfig.IntifaceConnectionSocket = refName;
                _clientConfigs.Save();
                // TODO: Maybe publish a mediator message here that we changed the connectionsocket? (or refresh UI idk)
            }
        }
        UiSharedService.AttachToolTip($"Change the Intiface Server Address to a custom one if you desire!." +
                 Environment.NewLine + "Leave blank to use the default address.");

        // display the wide list of connected devices, along with if they are active or not, below some scanner options
        var textSize = ImGui.CalcTextSize("Scanner Status: ");
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted("Scanner Status: ");

        ImGui.SameLine(textSize.X + ImGui.GetStyle().ItemSpacing.X);
        var color = _IntifaceHandler.ScanningForDevices ? ImGuiColors.DalamudYellow : ImGuiColors.DalamudRed;
        using (ImRaii.PushColor(ImGuiCol.Text, color))
        {
            ImGui.TextUnformatted(_IntifaceHandler.ScanningForDevices ? "Scanning..." : "Idle");
        }

        ImGui.SameLine(textSize.X + ImGui.GetStyle().ItemSpacing.X + ImGui.CalcTextSize("Scanning...").X + ImGui.GetStyle().ItemSpacing.X);
        var width = _uiShared.GetIconTextButtonSize(FontAwesomeIcon.Search, "Start Scanning for Devices");
        if (_uiShared.IconButton(FontAwesomeIcon.Search))
        {
            // search scanning if we are not scanning, otherwise stop scanning.
            if (_IntifaceHandler.ScanningForDevices)
            {
                _IntifaceHandler.StopDeviceScanAsync().ConfigureAwait(false);
            }
            else
            {
                _IntifaceHandler.StartDeviceScanAsync().ConfigureAwait(false);
            }
        }

        if (_uiShared.IconTextButton(FontAwesomeIcon.TabletAlt, "Personal Remote"))
        {
            // open the personal remote window
            _mediator.Publish(new UiToggleMessage(typeof(RemotePersonal)));
        }

        // draw out the list of devices
        ImGui.Separator();
        _uiShared.BigText("Connected Devices");
        _IntifaceHandler.DrawDevicesTable();
    }

    private void DrawIntifaceConnectionStatus()
    {
        var windowPadding = ImGui.GetStyle().WindowPadding;
        // push the style var to supress the Y window padding.
        var buttonSize = _uiShared.GetIconButtonSize(FontAwesomeIcon.Link);
        var buttplugServerAddr = DeviceHandler.IntifaceClientName;
        var addrSize = ImGui.CalcTextSize(buttplugServerAddr);
        var textSize = ImGui.CalcTextSize("Connected");

        string intifaceConnectionStr = $"Intiface Central Connection";

        var addrTextSize = ImGui.CalcTextSize(intifaceConnectionStr);
        var printAddr = intifaceConnectionStr != string.Empty;

        // if the server is connected, then we should display the server info
        if (_IntifaceHandler.ConnectedToIntiface)
        {
            // fancy math shit for clean display, adjust when moving things around
            ImGui.SetCursorPosX((ImGui.GetWindowContentRegionMin().X + UiSharedService.GetWindowContentRegionWidth()) / 2 - (addrSize.X + textSize.X) / 2 - ImGui.GetStyle().ItemSpacing.X / 2);
            ImGui.TextColored(ImGuiColors.ParsedGreen, buttplugServerAddr);
            ImUtf8.SameLineInner();
            ImGui.TextUnformatted("Connected");

        }
        // otherwise, if we are not connected, display that we aren't connected.
        else
        {
            ImGui.AlignTextToFramePadding();
            ImGui.SetCursorPosX((ImGui.GetWindowContentRegionMin().X
                + UiSharedService.GetWindowContentRegionWidth())
                / 2 - (ImGui.CalcTextSize("No Active Client Connection").X) / 2 - ImGui.GetStyle().ItemSpacing.X / 2);
            ImGui.TextColored(ImGuiColors.DalamudRed, "No Active Client Connection");
        }

        ImGui.SetCursorPosY(ImGui.GetCursorPosY() - ImGui.GetStyle().ItemSpacing.Y);
        ImGui.SetCursorPosX((ImGui.GetWindowContentRegionMin().X + UiSharedService.GetWindowContentRegionWidth()) / 2 - addrTextSize.X / 2);
        ImGui.TextUnformatted(intifaceConnectionStr);
        ImGui.SameLine();

        // now we need to display the connection link button beside it.
        var color = UiSharedService.GetBoolColor(_IntifaceHandler.ConnectedToIntiface);
        var connectedIcon = !_IntifaceHandler.ConnectedToIntiface ? FontAwesomeIcon.Link : FontAwesomeIcon.Unlink;

        ImGui.SameLine(ImGui.GetWindowContentRegionMin().X + UiSharedService.GetWindowContentRegionWidth() - buttonSize.X);
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() - ((addrSize.Y + textSize.Y) / 2 + addrTextSize.Y) / 2 - ImGui.GetStyle().ItemSpacing.Y + buttonSize.Y / 2);

        // we need to turn the button from the connected link to the disconnected link.
        using (ImRaii.PushColor(ImGuiCol.Text, color))
        {
            if (_uiShared.IconButton(connectedIcon))
            {
                // if we are connected to intiface, then we should disconnect.
                if (_IntifaceHandler.ConnectedToIntiface)
                {
                    _IntifaceHandler.DisconnectFromIntifaceAsync();
                }
                // otherwise, we should connect to intiface.
                else
                {
                    _IntifaceHandler.ConnectToIntifaceAsync();
                }
            }
        }
        UiSharedService.AttachToolTip(_IntifaceHandler.ConnectedToIntiface
            ? "Disconnect from Intiface Central" : "Connect to Intiface Central");

        // go back to the far left, at the same height, and draw another button.
        var intifaceOpenIcon = FontAwesomeIcon.ArrowUpRightFromSquare;
        var intifaceIconSize = _uiShared.GetIconButtonSize(intifaceOpenIcon);

        ImGui.SameLine(ImGui.GetWindowContentRegionMin().X + windowPadding.X);
        ImGui.SetCursorPosY(ImGui.GetCursorPosY()
            - ((addrSize.Y + textSize.Y) / 2 + addrTextSize.Y) / 2
            - ImGui.GetStyle().ItemSpacing.Y + intifaceIconSize.Y / 2);

        if (_uiShared.IconButton(intifaceOpenIcon))
        {
            // search for the intiface celtral window
            IntPtr windowHandle = User32.FindWindow(null, "Intiface\u00AE Central");
            // if it's present, place it to the foreground
            if (windowHandle != IntPtr.Zero)
            {
                _logger.LogDebug("Intiface Central found, bringing to foreground.");
                User32.SetForegroundWindow(windowHandle);
            }
            // otherwise, start the process to open intiface central
            else if (!string.IsNullOrEmpty(IntifacePath) && File.Exists(IntifacePath))
            {
                _logger.LogInformation("Starting Intiface Central");
                Process.Start(IntifacePath);
            }
            // or just open the installer if it doesnt exist.
            else
            {
                _logger.LogWarning("Application not found, redirecting you to download installer.");
                Util.OpenLink("https://intiface.com/central/");
            }
        }
        UiSharedService.AttachToolTip("Opens Intiface Central on your PC for connection.\nIf application is not detected, opens a link to installer.");

        // draw out the vertical slider.
        ImGui.Separator();
    }


    /// <summary> Gets the application running path for Intiface Central.exe if installed.</summary>
    static string GetApplicationPath()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "IntifaceCentral", "intiface_central.exe");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // Adjust the path according to where the application resides on macOS
            string homePath = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
            return Path.Combine(homePath, "Applications", "IntifaceCentral", "intiface_central.app");
        }
        // Add more conditions here for other operating systems if necessary
        return null!;
    }

}
