using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Utility;
using GagSpeak.PlayerData.Pairs;
using GagSpeak.Services.Mediator;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.WebAPI.Utils;
using GagSpeak.UI.Components;
using GagSpeak.UI.Handlers;
using ImGuiNET;
using System.Collections.Immutable;
using System.Globalization;
using System.Numerics;
using System.Reflection;
using OtterGuiInternal.Structs;
using GagSpeak.UI.Permissions;
using GagspeakAPI.Data.Enum;
using GagSpeak.GagspeakConfiguration;
using GagSpeak;
using GagSpeak.UI;
using GagSpeak.WebAPI;
using Lumina.Excel.GeneratedSheets;
using Dalamud.Plugin;

namespace GagSpeak.UI.MainWindow;

// this can easily become the "contact list" tab of the "main UI" window.
public partial class MainWindowUI : WindowMediatorSubscriberBase
{
    private readonly UiSharedService _uiSharedService;
    private readonly ApiController _apiController;
    private readonly GagspeakConfigService _configService;
    private readonly PairManager _pairManager;
    private readonly ServerConfigurationManager _serverManager;
    private readonly MainTabMenu _tabMenu;
    private readonly UserPairPermsSticky _userPairPermissionsSticky;
    private readonly UserPairListHandler _userPairListHandler;
    private readonly IDalamudPluginInterface _pi;
    private Vector2 _lastPosition = Vector2.One;
    private Vector2 _lastSize = Vector2.One;
    private int _secretKeyIdx = -1;
    private float _tabBarHeight;
    private bool _wasOpen;
    private float _windowContentWidth;

    // for theme management
    public bool ThemePushed = false;

    public MainWindowUI(ILogger<MainWindowUI> logger, GagspeakMediator mediator,
        UiSharedService uiShared, ApiController apiController, GagspeakConfigService configService, 
        PairManager pairManager, ServerConfigurationManager serverManager,
        DrawEntityFactory drawEntityFactory, UserPairPermsSticky userpermssticky, 
        UserPairListHandler userPairListHandler, IDalamudPluginInterface pi) : base(logger, mediator, "###GagSpeakMainUI")
    {
        _apiController = apiController;
        _configService = configService;
        _pairManager = pairManager;
        _serverManager = serverManager;
        _userPairPermissionsSticky = userpermssticky;
        _userPairListHandler = userPairListHandler;
        _pi = pi;
        _uiSharedService = uiShared;

        // the bottomTabMenu
        _tabMenu = new MainTabMenu(Mediator, _apiController, _pairManager, _uiSharedService);

        AllowPinning = false;
        AllowClickthrough = false;
        TitleBarButtons = new()
        {
            new TitleBarButton()
            {
                Icon = FontAwesomeIcon.Cog,
                Click = (msg) =>
                {
                    Mediator.Publish(new UiToggleMessage(typeof(SettingsUi)));
                },
                IconOffset = new(2,1),
                ShowTooltip = () =>
                {
                    ImGui.BeginTooltip();
                    ImGui.Text("Open Gagspeak Settings");
                    ImGui.EndTooltip();
                }
            },
            new TitleBarButton()
            {
                Icon = FontAwesomeIcon.Book,
                Click = (msg) =>
                {
                    Mediator.Publish(new UiToggleMessage(typeof(EventViewerUI)));
                },
                IconOffset = new(2,1),
                ShowTooltip = () =>
                {
                    ImGui.BeginTooltip();
                    ImGui.Text("Open Gagspeak Event Viewer");
                    ImGui.EndTooltip();
                }
            }
        };

        // updates the draw folders by recollecting them, and updates the drawPair list of distinct draw pairs
        _userPairListHandler.UpdateDrawFoldersAndUserPairDraws();

        // display info about the folders
        var dev = "Dev Build";
        var ver = Assembly.GetExecutingAssembly().GetName().Version!;
        WindowName = $"GagSpeak {dev} ({ver.Major}.{ver.Minor}.{ver.Build}.{ver.Revision})###GagSpeakMainUI";

        Toggle();

        Mediator.Subscribe<SwitchToMainUiMessage>(this, (_) => IsOpen = true);
        Mediator.Subscribe<SwitchToIntroUiMessage>(this, (_) => IsOpen = false);
        // Mediator.Subscribe<CutsceneEndMessage>(this, (_) => UiSharedService_GposeEnd());
        Mediator.Subscribe<RefreshUiMessage>(this, (msg) =>
        {
            // update drawfolders
            _userPairListHandler.UpdateDrawFoldersAndUserPairDraws();
            // update the cog statuses
            UpdateShouldOpenStatus();
        });

        Mediator.Subscribe<OpenUserPairPermissions>(this, (msg) =>
        {
            logger.LogInformation("OpenUserPairPermission called for {0}", msg.Pair.UserData.AliasOrUID);

            // locate the DrawUserPair in the list where the pair matches the pair in it, and set that bool to true;
            UpdateShouldOpenStatus(msg.Pair, msg.PermsWindowType);

        });


        Flags |= ImGuiWindowFlags.NoDocking;

        SizeConstraints = new WindowSizeConstraints()
        {
            MinimumSize = new Vector2(325, 400),
            MaximumSize = new Vector2(325, 2000),
        };
    }

    protected override void PreDrawInternal()
    {
        // no config option yet, so it will always be active. When one is added, append "&& !_configOption.useTheme" to the if statement.
        if(!ThemePushed)
        {
            ImGui.PushStyleColor(ImGuiCol.TitleBg, new Vector4(0.01f, 0.07f, 0.01f, 1f));
            ImGui.PushStyleColor(ImGuiCol.TitleBgActive, new Vector4(0, 0.56f, 0.09f, 0.51f));

            ThemePushed = true;
        }
    }

    protected override void PostDrawInternal()
    {
        if (ThemePushed)
        {
            ImGui.PopStyleColor(2);
            ThemePushed = false;
        }
    }

    protected override void DrawInternal()
    {
        // get the width of the window content region we set earlier
        _windowContentWidth = UiSharedService.GetWindowContentRegionWidth();

        // if we are not on the current version, display it
        if (!_apiController.IsCurrentVersion)
        {
            var ver = _apiController.CurrentClientVersion;
            var unsupported = "UNSUPPORTED VERSION";
            // push the notice that we are unsupported
            using (_uiSharedService.UidFont.Push())
            {
                var uidTextSize = ImGui.CalcTextSize(unsupported);
                ImGui.SetCursorPosX((ImGui.GetWindowContentRegionMax().X + ImGui.GetWindowContentRegionMin().X) / 2 - uidTextSize.X / 2);
                ImGui.AlignTextToFramePadding();
                ImGui.TextColored(ImGuiColors.DalamudRed, unsupported);
            }
            // the wrapped text explanation 
            UiSharedService.ColorTextWrapped($"Your GagSpeak installation is out of date, the current version is {ver.Major}.{ver.Minor}.{ver.Build}. " +
                $"It is highly recommended to keep GagSpeak up to date. Open /xlplugins and update the plugin.", ImGuiColors.DalamudRed);
        }

        if(_apiController.ServerState is ServerState.NoSecretKey || _apiController.ServerState is ServerState.VersionMisMatch)
        {
            using (ImRaii.PushId("header")) DrawUIDHeader();
        }
        else
        {
            using (ImRaii.PushId("serverstatus")) DrawServerStatus();
        }
        // separate our UI once more.
        ImGui.Separator();

        // store a ref to the end of the content drawn.
        float menuComponentEnd = ImGui.GetCursorPosY();

        // if we are connected, draw out our menus based on the tab selection.
        // if we are connected to the server
        if (_apiController.ServerState is ServerState.Connected)
        {
            // draw the bottom tab bar
            using (ImRaii.PushId("MainMenuTabBar")) _tabMenu.Draw();

            // display content based on the tab selected
            switch (_tabMenu.TabSelection)
            {
                case MainTabMenu.SelectedTab.Homepage:
                    using (ImRaii.PushId("homepageComponent")) DrawHomepageSection(_pi);
                    break;
                case MainTabMenu.SelectedTab.Whitelist:
                    using (ImRaii.PushId("whitelistComponent")) DrawWhitelistSection();
                    break;
                case MainTabMenu.SelectedTab.Discover:
                    // using (ImRaii.PushId("discoverComponent")) DrawDiscoverSection();
                    break;
                case MainTabMenu.SelectedTab.MySettings:
                    // using (ImRaii.PushId("accountSettingsComponent")) DrawAccountSettingsSection();
                    break;
            }
        }

        var pos = ImGui.GetWindowPos();
        var size = ImGui.GetWindowSize();
        if (_lastSize != size || _lastPosition != pos)
        {
            _lastSize = size;
            _lastPosition = pos;
            Mediator.Publish(new CompactUiChange(_lastSize, _lastPosition));
        }
    }

    private void DrawUIDHeader()
    {
        // fetch the Uid Text of yourself
        var uidText = GetUidText();

        // push the big boi font for the UID
        using (_uiSharedService.UidFont.Push())
        {
            var uidTextSize = ImGui.CalcTextSize(uidText);
            ImGui.SetCursorPosX((ImGui.GetWindowContentRegionMax().X - ImGui.GetWindowContentRegionMin().X) / 2 - uidTextSize.X / 2);
            // display it, it should be green if connected and red when not.
            ImGui.TextColored(GetUidColor(), uidText);
        }

        // if we are connected
        if (_apiController.ServerState is ServerState.Connected)
        {
            UiSharedService.CopyableDisplayText(_apiController.DisplayName);

            // if the UID does not equal the display name
            if (!string.Equals(_apiController.DisplayName, _apiController.UID, StringComparison.Ordinal))
            {
                // grab the original text size for the UID in the api controller
                var origTextSize = ImGui.CalcTextSize(_apiController.UID);
                // adjust the cursor and redraw the UID (really not sure why this is here but we can trial and error later.
                ImGui.SetCursorPosX((ImGui.GetWindowContentRegionMax().X - ImGui.GetWindowContentRegionMin().X) / 2 - origTextSize.X / 2);
                ImGui.TextColored(GetUidColor(), _apiController.UID);
                // give it the same functionality.
                UiSharedService.CopyableDisplayText(_apiController.UID);
            }
        }
    }


    /// <summary>
    /// Helper function for drawing the current status of the server, including the number of people online.
    /// </summary>
    private void DrawServerStatus()
    {
        var buttonSize = _uiSharedService.GetIconButtonSize(FontAwesomeIcon.Link);
        var userCount = _apiController.OnlineUsers.ToString(CultureInfo.InvariantCulture);
        var userSize = ImGui.CalcTextSize(userCount);
        var textSize = ImGui.CalcTextSize("Users Online");

        var shardConnection = $"Main GagSpeak Server";

        var shardTextSize = ImGui.CalcTextSize(shardConnection);
        var printShard = shardConnection != string.Empty;

        // if the server is connected, then we should display the server info
        if (_apiController.ServerState is ServerState.Connected)
        {
            // fancy math shit for clean display, adjust when moving things around
            ImGui.SetCursorPosX((ImGui.GetWindowContentRegionMin().X + UiSharedService.GetWindowContentRegionWidth()) / 2 - (userSize.X + textSize.X) / 2 - ImGui.GetStyle().ItemSpacing.X / 2);
            ImGui.TextColored(ImGuiColors.ParsedGreen, userCount);
            ImGui.SameLine();
            ImGui.TextUnformatted("Users Online");
        }
        // otherwise, if we are not connected, display that we aren't connected.
        else
        {
            ImGui.AlignTextToFramePadding();
            ImGui.SetCursorPosX((ImGui.GetWindowContentRegionMin().X
                + UiSharedService.GetWindowContentRegionWidth())
                / 2 - ImGui.CalcTextSize("Not connected to any server").X / 2 - ImGui.GetStyle().ItemSpacing.X / 2);
            ImGui.TextColored(ImGuiColors.DalamudRed, "Not connected to any server");
        }

        ImGui.SetCursorPosY(ImGui.GetCursorPosY() - ImGui.GetStyle().ItemSpacing.Y);
        ImGui.SetCursorPosX((ImGui.GetWindowContentRegionMin().X + UiSharedService.GetWindowContentRegionWidth()) / 2 - shardTextSize.X / 2);
        ImGui.TextUnformatted(shardConnection);
        ImGui.SameLine();

        // now we need to display the connection link button beside it.
        var color = UiSharedService.GetBoolColor(!_serverManager.CurrentServer!.FullPause);
        var connectedIcon = !_serverManager.CurrentServer.FullPause ? FontAwesomeIcon.Link : FontAwesomeIcon.Unlink;

        ImGui.SameLine(ImGui.GetWindowContentRegionMin().X + UiSharedService.GetWindowContentRegionWidth() - buttonSize.X);
        if (printShard)
        {
            // unsure what this is doing but we can find out lol
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() - ((userSize.Y + textSize.Y) / 2 + shardTextSize.Y) / 2 - ImGui.GetStyle().ItemSpacing.Y + buttonSize.Y / 2);
        }

        // if the server is reconnecting or disconnecting
        if (_apiController.ServerState is not (ServerState.Reconnecting or ServerState.Disconnecting))
        {
            // we need to turn the button from the connected link to the disconnected link.
            using (ImRaii.PushColor(ImGuiCol.Text, color))
            {
                // then display it
                if (_uiSharedService.IconButton(connectedIcon))
                {
                    // and toggle the full pause for the current server, save the config, and recreate the connections,
                    // placing it into a disconnected state due to the full pause being active. (maybe change this later)
                    _serverManager.CurrentServer.FullPause = !_serverManager.CurrentServer.FullPause;
                    _serverManager.Save();
                    _ = _apiController.CreateConnections();
                }
            }
            // attach the tooltip for the connection / disconnection button)
            UiSharedService.AttachToolTip(!_serverManager.CurrentServer.FullPause ? "Disconnect from " + _serverManager.CurrentServer.ServerName
                                                                                  : "Connect to " + _serverManager.CurrentServer.ServerName);
        }
    }

    /// <summary> Retrieves the various server error messages based on the current server state.</summary>
    /// <returns> The error message of the server.</returns>
    private string GetServerError()
    {
        return _apiController.ServerState switch
        {
            ServerState.Connecting => "Attempting to connect to the server.",
            ServerState.Reconnecting => "Connection to server interrupted, attempting to reconnect to the server.",
            ServerState.Disconnected => "Currently disconnected from the GagSpeak server.",
            ServerState.Disconnecting => "Disconnecting from server",
            ServerState.Unauthorized => "Server Response: " + _apiController.AuthFailureMessage,
            ServerState.Offline => "The GagSpeak server is currently offline.",
            ServerState.VersionMisMatch => "Your plugin is out of date. Please update your plugin to fix.",
            ServerState.Connected => string.Empty,
            ServerState.NoSecretKey => "No secret key is set for this current character. To create UID's for your alt characters, be sure to claim your account in the CK discord.",
            _ => string.Empty
        };
    }

    /// <summary> Retrieves the various UID text color based on the current server state.</summary>
    /// <returns> The color of the UID text in Vector4 format .</returns>
    private Vector4 GetUidColor()
    {
        return _apiController.ServerState switch
        {
            ServerState.Connecting => ImGuiColors.DalamudYellow,
            ServerState.Reconnecting => ImGuiColors.DalamudRed,
            ServerState.Connected => ImGuiColors.ParsedGreen,
            ServerState.Disconnected => ImGuiColors.DalamudYellow,
            ServerState.Disconnecting => ImGuiColors.DalamudYellow,
            ServerState.Unauthorized => ImGuiColors.DalamudRed,
            ServerState.VersionMisMatch => ImGuiColors.DalamudRed,
            ServerState.Offline => ImGuiColors.DalamudRed,
            ServerState.NoSecretKey => ImGuiColors.DalamudYellow,
            _ => ImGuiColors.DalamudRed
        };
    }

    /// <summary> Retrieves the various UID text based on the current server state.</summary>
    /// <returns> The text of the UID.</returns>
    private string GetUidText()
    {
        return _apiController.ServerState switch
        {
            ServerState.Reconnecting => "Reconnecting",
            ServerState.Connecting => "Connecting",
            ServerState.Disconnected => "Disconnected",
            ServerState.Disconnecting => "Disconnecting",
            ServerState.Unauthorized => "Unauthorized",
            ServerState.VersionMisMatch => "Version mismatch",
            ServerState.Offline => "Unavailable",
            ServerState.NoSecretKey => "No Secret Key",
            ServerState.Connected => _apiController.DisplayName, // displays when connected, your UID
            _ => string.Empty
        };
    }


    // Would be nice to have a "DrawTabHeader" function here to automate the drawing of left and right buttons and titles per tab, since this is a partial class.
}
