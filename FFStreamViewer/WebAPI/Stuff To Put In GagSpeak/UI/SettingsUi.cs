using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Utility;
using ImGuiNET;
using Gagspeak.API.Data;
using Gagspeak.API.Data.Comparer;
using FFStreamViewer.WebAPI.Interop.Ipc;
using FFStreamViewer.WebAPI.GagspeakConfiguration;
using FFStreamViewer.WebAPI.GagspeakConfiguration.Models;
using FFStreamViewer.WebAPI.PlayerData.Handlers;
using FFStreamViewer.WebAPI.PlayerData.Pairs;
using FFStreamViewer.WebAPI.Services;
using FFStreamViewer.WebAPI.Services.Mediator;
using FFStreamViewer.WebAPI.Services.ServerConfiguration;
using FFStreamViewer.WebAPI;
using FFStreamViewer.WebAPI.SignalR.Utils;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Globalization;
using System.Numerics;
using System.Text.Json;
using FFStreamViewer.WebAPI.UI;

namespace FFStreamViewer.WebAPI.UI;

public class SettingsUi : WindowMediatorSubscriberBase
{
    private readonly ApiController _apiController;
    private readonly IpcManager _ipcManager;
    private readonly OnFrameworkService _frameworkUtil;
    private readonly GagspeakConfigService _configService;
    private readonly PairManager _pairManager;
    private readonly ServerConfigurationManager _serverConfigurationManager;
    private readonly UiSharedService _uiShared;
    private bool _deleteAccountPopupModalShown = false;
    private bool _deleteFilesPopupModalShown = false;
    private string _exportDescription = string.Empty;
    private string _lastTab = string.Empty;
    private bool? _notesSuccessfullyApplied = null;
    private bool _overwriteExistingLabels = false;
    private bool _readClearCache = false;
    private bool _readExport = false;
    private bool _wasOpen = false;
    private CancellationTokenSource? _validationCts;

    public SettingsUi(ILogger<SettingsUi> logger, UiSharedService uiShared, 
        GagspeakConfigService configService, PairManager pairManager, 
        ServerConfigurationManager serverConfigurationManager, GagspeakMediator mediator, 
        ApiController apiController, IpcManager ipcManager,
        OnFrameworkService frameworkUtil) : base(logger, mediator, "GagSpeak Settings")
    {
        _configService = configService;
        _pairManager = pairManager;
        _serverConfigurationManager = serverConfigurationManager;
        _apiController = apiController;
        _ipcManager = ipcManager;
        _frameworkUtil = frameworkUtil;
        _uiShared = uiShared;
        AllowClickthrough = false;
        AllowPinning = false;

        SizeConstraints = new WindowSizeConstraints()
        {
            MinimumSize = new Vector2(800, 400),
            MaximumSize = new Vector2(800, 2000),
        };

        Mediator.Subscribe<OpenSettingsUiMessage>(this, (_) => Toggle());
        Mediator.Subscribe<SwitchToIntroUiMessage>(this, (_) => IsOpen = false);
    }

    private ApiController ApiController => _uiShared.ApiController;

    /// <summary>
    /// The internal draw method for the settings UI window.
    /// <para> Will draw display the optional plugins section, then display settings content </para>
    /// </summary>
    protected override void DrawInternal()
    {
        _ = _uiShared.DrawOtherPluginState();

        DrawSettingsContent();
    }

    /// <summary> Helper function to tell the settings menu to no longer edit the tracker position on close. </summary>
    public override void OnClose()
    {
        _uiShared.EditTrackerPosition = false;
        base.OnClose();
    }

    /// <summary> Displays the Debug section within the settings, where we can set our debug level </summary>
    private void DrawDebug()
    {
        _lastTab = "Debug";
        // display Debug Configuration in fat text
        _uiShared.BigText("Debug Configuration");

        // display the combo box for setting the log level we wish to have for our plugin
        _uiShared.DrawCombo("Log Level", Enum.GetValues<LogLevel>(), (level) => level.ToString(), (level) =>
        {
            _configService.Current.LogLevel = level;
            _configService.Save();
        }, _configService.Current.LogLevel);


        // draw out our pair manager
        // Start of the Pair Manager section
        ImGui.Text("Pair Manager:");

        // Display additional info about the Pair Manager
        int totalPairs = _pairManager.ClientPairs.Count;
        int visibleUsersCount = _pairManager.GetVisibleUserCount();
        ImGui.Text($"Total Pairs: {totalPairs}");
        ImGui.Text($"Visible Users: {visibleUsersCount}");

        // Iterate through all client pairs in the PairManager
        foreach (var clientPair in _pairManager.ClientPairs)
        {
            // Display the UserData's UID
            ImGui.Text($"UserData UID: {clientPair.Key.UID}");
            ImGui.Indent();

            // Accessing and displaying information from the Pair object
            var pair = clientPair.Value;
            ImGui.Text($"IsDirectlyPaired: {pair.IsDirectlyPaired}");
            ImGui.Text($"IsOneSidedPair: {pair.IsOneSidedPair}");
            ImGui.Text($"IsOnline: {pair.IsOnline}");
            ImGui.Text($"IsPaired: {pair.IsPaired}");
            ImGui.Text($"IsVisible: {pair.IsVisible}");
            ImGui.Text($"PlayerName: {pair.PlayerName ?? "N/A"}");

            // Accessing the PairHandler object to output its information
            if (pair.HasCachedPlayer)
            {
                var pairHandler = pair.PlayerPairHandler;
                // Display basic information about the online user
                ImGui.Text($"Online User ID: {pairHandler.OnlineUser.User.UID}");
                ImGui.Text($"Online User Alias: {pairHandler.OnlineUser.User.AliasOrUID}");
                // Display information about the player character
                ImGui.Text($"Player Character Address: {pairHandler.PlayerCharacter}");
                ImGui.Text($"Player Character ID: {pairHandler.PlayerCharacterId}");
                ImGui.Text($"Player Name: {pairHandler.PlayerName ?? "N/A"}");
                ImGui.Text($"Player Name Hash: {pairHandler.PlayerNameHash}");
                ImGui.Text($"IsVisible (Handler): {pairHandler?.IsVisible.ToString() ?? "N/A"}");
            }
            else
            {
                ImGui.Text("PairHandler: N/A");
            }

            ImGui.Unindent();
        }


        // Note: Ensure that the _allClientPairs field in PairManager is accessible from SettingsUi.
        // You might need to adjust its access modifier or provide a public method/property to access it safely.

    }

    private void DrawPreferences()
    {
        _lastTab = "Plugin Preferences";

        // the nicknames section
        _uiShared.BigText("Nicknames");
        
        // see if the user wants to allow a popup to create nicknames upon adding a paired user
        var openPopupOnAddition = _configService.Current.OpenPopupOnAdd;
        if (ImGui.Checkbox("Open Nickname Popup when adding a GagSpeak user", ref openPopupOnAddition))
        {
            _configService.Current.OpenPopupOnAdd = openPopupOnAddition;
            _configService.Save();
        }
        _uiShared.DrawHelpText("When enabled, a popup will automatically display after adding another user,"+
            "allowing you to enter a nickname for them.");

        // form a separator for the UI
        ImGui.Separator();
        _uiShared.BigText("UI Preferences");
        // preset some variables to grab from our config service.
        var enableDtrEntry = _configService.Current.EnableDtrEntry;
        var showUidInDtrTooltip = _configService.Current.ShowUidInDtrTooltip;
        var preferNoteInDtrTooltip = _configService.Current.PreferNicknameInDtrTooltip;

        var preferNicknamesInsteadOfName = _configService.Current.PreferNicknamesOverNamesForVisible;
        var showVisibleSeparate = _configService.Current.ShowVisibleUsersSeparately;
        var showOfflineSeparate = _configService.Current.ShowOfflineUsersSeparately;

        var showProfiles = _configService.Current.ProfilesShow;
        var profileDelay = _configService.Current.ProfileDelay;
        var profileOnRight = _configService.Current.ProfilePopoutRight;

        if (ImGui.Checkbox("Display status and visible pair count in Server Info Bar", ref enableDtrEntry))
        {
            _configService.Current.EnableDtrEntry = enableDtrEntry;
            _configService.Save();
        }
        _uiShared.DrawHelpText("Adds a GagSpeak connection status & visible pair count in the Server Info Bar.");

        // If the Enable DTR entry is checked, we can make these options selectable, but if it isn't, disable them
        using (ImRaii.Disabled(!enableDtrEntry))
        {
            // set the tooltip options for these entries
            using var indent = ImRaii.PushIndent();
            if (ImGui.Checkbox("Show visible character's UID in tooltip", ref showUidInDtrTooltip))
            {
                _configService.Current.ShowUidInDtrTooltip = showUidInDtrTooltip;
                _configService.Save();
            }

            if (ImGui.Checkbox("Prefer set nicknames over the player's name in tooltip", ref preferNoteInDtrTooltip))
            {
                _configService.Current.PreferNicknameInDtrTooltip = preferNoteInDtrTooltip;
                _configService.Save();
            }
        }

        // determines if they allow categorizing visible users in a separate dropdown.
        if (ImGui.Checkbox("Show separate Visible group", ref showVisibleSeparate))
        {
            _configService.Current.ShowVisibleUsersSeparately = showVisibleSeparate;
            _configService.Save();
            Mediator.Publish(new RefreshUiMessage());
        }
        _uiShared.DrawHelpText("Creates an additional dropdown for all paired users in your render range.");

        // determines if they allow categorizing offline users in a separate dropdown.
        if (ImGui.Checkbox("Show separate Offline group", ref showOfflineSeparate))
        {
            _configService.Current.ShowOfflineUsersSeparately = showOfflineSeparate;
            _configService.Save();
            Mediator.Publish(new RefreshUiMessage());
        }
        _uiShared.DrawHelpText("Creates an additional dropdown for all paired users that are currently offline.");

        // if you prefer viewing the Nickname you set for a player over the 
        if (ImGui.Checkbox("Prefer notes over player names for visible players", ref preferNicknamesInsteadOfName))
        {
            _configService.Current.PreferNicknamesOverNamesForVisible = preferNicknamesInsteadOfName;
            _configService.Save();
            Mediator.Publish(new RefreshUiMessage());
        }
        _uiShared.DrawHelpText("If you set a note for a player it will be shown instead of the player name");

        // if we want to automatically open GagSpeak profiles on Hover.
        if (ImGui.Checkbox("Show GagSpeak Profiles on Hover", ref showProfiles))
        {
            Mediator.Publish(new ClearProfileDataMessage());
            _configService.Current.ProfilesShow = showProfiles;
            _configService.Save();
        }
        _uiShared.DrawHelpText("This will show the configured user profile after a set delay");
        
        ImGui.Indent(); // see if we want to pop the profiles out to the right of the menu
        if (!showProfiles) ImGui.BeginDisabled();
        if (ImGui.Checkbox("Popout profiles on the right", ref profileOnRight))
        {
            _configService.Current.ProfilePopoutRight = profileOnRight;
            _configService.Save();
            Mediator.Publish(new CompactUiChange(Vector2.Zero, Vector2.Zero));
        }
        _uiShared.DrawHelpText("Will show profiles on the right side of the main UI");

        // how long we should need to hover over it in order for the profile to display?
        if (ImGui.SliderFloat("Hover Delay", ref profileDelay, 1, 5))
        {
            _configService.Current.ProfileDelay = profileDelay;
            _configService.Save();
        }
        _uiShared.DrawHelpText("Delay until the profile should be displayed");
        if (!showProfiles) ImGui.EndDisabled();
        ImGui.Unindent();

        /* --------------- Seperator for moving onto the Notifications Section ----------- */
        ImGui.Separator();
        var onlineNotifs = _configService.Current.ShowOnlineNotifications;
        var onlineNotifsPairsOnly = _configService.Current.ShowOnlineNotificationsOnlyForIndividualPairs;
        var onlineNotifsNamedOnly = _configService.Current.ShowOnlineNotificationsOnlyForNamedPairs;
        
        _uiShared.BigText("Notifications");

        // see if they want to be notified when an online paired user connects to GagSpeak
        _uiShared.DrawHelpText("Enabling this will not show any \"Warning\" labeled messages for missing optional plugins.");
        if (ImGui.Checkbox("Enable online notifications", ref onlineNotifs))
        {
            _configService.Current.ShowOnlineNotifications = onlineNotifs;
            _configService.Save();
        }
        _uiShared.DrawHelpText("Enabling this will show a small notification (type: Info) in the bottom right corner when pairs go online.");
        // if you want to toggle it for only direct pairs, or only for named direct pairs.
        using var disabled = ImRaii.Disabled(!onlineNotifs);
        if (ImGui.Checkbox("Notify only for individual pairs", ref onlineNotifsPairsOnly))
        {
            _configService.Current.ShowOnlineNotificationsOnlyForIndividualPairs = onlineNotifsPairsOnly;
            _configService.Save();
        }
        _uiShared.DrawHelpText("Enabling this will only show online notifications (type: Info) for individual pairs.");
        if (ImGui.Checkbox("Notify only for named pairs", ref onlineNotifsNamedOnly))
        {
            _configService.Current.ShowOnlineNotificationsOnlyForNamedPairs = onlineNotifsNamedOnly;
            _configService.Save();
        }
        _uiShared.DrawHelpText("Enabling this will only show online notifications (type: Info) for pairs where you have set an individual note.");
    }

    /// <summary>
    /// Helper for drawing out the account management tab.
    /// </summary>
    private void DrawAccountManagement()
    {
        _lastTab = "Account Management";
        if (ApiController.ServerAlive)
        {
            // display title for account management
            _uiShared.BigText("Manage GagSpeak Account");

            // beside it, allow user to delete their account. Warn them that the action is not irreversible in the configuration window.
            ImGui.SameLine();
            if (ImGui.Button("Delete account"))
            {
                _deleteAccountPopupModalShown = true;
                ImGui.OpenPopup("Delete your account?");
            }

            _uiShared.DrawHelpText("Completely deletes your account and all uploaded files to the service.");

            if (ImGui.BeginPopupModal("Delete your account?", ref _deleteAccountPopupModalShown, UiSharedService.PopupWindowFlags))
            {
                UiSharedService.TextWrapped(
                    "Your account and all associated files and data on the service will be deleted.");
                UiSharedService.TextWrapped("Your UID will be removed from all pairing lists.");
                ImGui.TextUnformatted("Are you sure you want to continue?");
                ImGui.Separator();
                ImGui.Spacing();

                var buttonSize = (ImGui.GetWindowContentRegionMax().X - ImGui.GetWindowContentRegionMin().X -
                                  ImGui.GetStyle().ItemSpacing.X) / 2;

                if (ImGui.Button("Delete account", new Vector2(buttonSize, 0)))
                {
                    _ = Task.Run(ApiController.UserDelete);
                    _deleteAccountPopupModalShown = false;
                    Mediator.Publish(new SwitchToIntroUiMessage());
                }

                ImGui.SameLine();

                if (ImGui.Button("Cancel##cancelDelete", new Vector2(buttonSize, 0)))
                {
                    _deleteAccountPopupModalShown = false;
                }

                UiSharedService.SetScaledWindowSize(325);
                ImGui.EndPopup();
            }
            ImGui.Separator();
        }

        // display the list of characters our keys are bound to, their UID's and the place to insert the key for them
        if(_serverConfigurationManager.CurrentServer.SecretKeys.Any())
        {
            UiSharedService.ColorTextWrapped("Characters listed here will automatically connect to the selected Gagspeak service with the settings as provided below." +
                " Make sure to enter the character names correctly or use the 'Add current character' button at the bottom.", ImGuiColors.DalamudYellow);
            int i = 0;
            // for each character in the authentications
            foreach (var item in _serverConfigurationManager.CurrentServer.Authentications.ToList())
            {
                // push the ID for the tree node.
                using var charaId = ImRaii.PushId("selectedCharaprofile" + i);

                ImGui.BeginDisabled();
                try
                {
                    // get the name of the character
                    var charaName = item.CharacterName;
                    ImGui.InputText("##CharaName" + charaName + i, ref charaName, 32);

                    ImGui.SameLine();
                    // grab the world the player is in.
                    var worldIdx = (ushort)item.WorldId;
                    var worldName = _uiShared.WorldData[worldIdx];
                    ImGui.InputText("##CharaWorld" + charaName + i, ref worldName, 32);
                }
                finally
                {
                    ImGui.EndDisabled();
                }

                // draw a button to remove the character
                if (_uiShared.IconTextButton(FontAwesomeIcon.Trash, "Remove Profile from Account") && UiSharedService.CtrlPressed())
                    _serverConfigurationManager.RemoveCharacterFromServer(i, item);
                UiSharedService.AttachToolTip("Hold CTRL to delete this entry.");

                // fetch the secret key
                var secretKeyIdx = item.SecretKeyIdx;
                var keys = _serverConfigurationManager.CurrentServer.SecretKeys;
                if (!keys.TryGetValue(secretKeyIdx, out var secretKey))
                {
                    secretKey = new();
                }
                // allow player to configure the secret key for the character
                var key = secretKey.Key;
                if (ImGui.InputText("Secret Key", ref key, 64))
                {
                    keys[secretKeyIdx].Key = key;
                    _serverConfigurationManager.Save();
                }

                i++;
            }

            ImGui.Separator();
            // if authentication does not currently exist for the character logged in right now, display a button to add it.
            if (!_serverConfigurationManager.CurrentServer.Authentications.Exists(c => string.Equals(c.CharacterName, _uiShared.PlayerName, StringComparison.Ordinal)
                && c.WorldId == _uiShared.WorldId))
            {
                // display the button if the conditions are met.
                if (_uiShared.IconTextButton(FontAwesomeIcon.User, "Add current character"))
                {
                    // only keep this true if we set the secret key right above this in a confirmation popup asking for the secret key to add for the appended user.
                    // we will also do a check in the database so see if both the key already exists, and if the primaryUID of that person is the account owners UID.
                    _serverConfigurationManager.AddCurrentCharacterToServer(true);
                }
            }
        }
        else
        {
            UiSharedService.ColorTextWrapped("You need to add a Secret Key first before adding Characters.", ImGuiColors.DalamudYellow);
        }
    }

    /// <summary>
    /// The actual function that draws the content of the settings window.
    /// </summary>
    private void DrawSettingsContent()
    {
        // check the current server state. If it is connected.
        if (_apiController.ServerState is ServerState.Connected)
        {
            // display the Server name, that it is available, and the number of users online.
            ImGui.TextUnformatted("Server is " + _serverConfigurationManager.CurrentServer!.ServerName + ":");
            ImGui.SameLine();
            ImGui.TextColored(ImGuiColors.ParsedGreen, "Available");
            ImGui.SameLine();
            ImGui.TextUnformatted("(");
            ImGui.SameLine();
            ImGui.TextColored(ImGuiColors.ParsedGreen, _apiController.OnlineUsers.ToString(CultureInfo.InvariantCulture));
            ImGui.SameLine();
            ImGui.TextUnformatted("Users Online");
            ImGui.SameLine();
            ImGui.TextUnformatted(")");
        }

        // align the next text the frame padding of the button in the same line.
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted("Claim your Account with the CK Discord Bot:");
        ImGui.SameLine();
        if (ImGui.Button("CK Discord"))
        {
            Util.OpenLink("https://discord.gg/kinkporium");
        }
        // draw our seperator
        ImGui.Separator();
        // draw out the tab bar for us.
        if (ImGui.BeginTabBar("mainTabBar"))
        {
            if (ImGui.BeginTabItem("Preferences"))
            {
                DrawPreferences();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Account Management"))
            {
                DrawAccountManagement();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Debug"))
            {
                DrawDebug();
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }
    }
}
