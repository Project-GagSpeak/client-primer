using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Utility;
using GagSpeak.ChatMessages;
using GagSpeak.GagspeakConfiguration;
using GagSpeak.GagspeakConfiguration.Models;
using GagSpeak.Interop.Ipc;
using GagSpeak.Interop.IpcHelpers.Penumbra;
using GagSpeak.PlayerData.Data;
using GagSpeak.PlayerData.Pairs;
using GagSpeak.Services;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using GagSpeak.UpdateMonitoring;
using GagSpeak.UpdateMonitoring.SpatialAudio.Managers;
using GagSpeak.UpdateMonitoring.SpatialAudio.Spawner;
using GagSpeak.WebAPI;
using GagspeakAPI.Data;
using GagspeakAPI.Data.Character;
using GagspeakAPI.Data.Enum;
using GagspeakAPI.Data.Permissions;
using GagspeakAPI.Dto.Permissions;
using ImGuiNET;
using OtterGui.Text;
using System.Globalization;
using System.Numerics;

namespace GagSpeak.UI;

public class SettingsUi : WindowMediatorSubscriberBase
{
    private readonly ApiController _apiController;
    private readonly PlayerCharacterManager _playerCharacterManager;
    private readonly IpcManager _ipcManager;
    private readonly OnFrameworkService _frameworkUtil;
    private readonly GagspeakConfigService _configService;
    private readonly PairManager _pairManager;
    private readonly ClientConfigurationManager _clientConfigs;
    private readonly ServerConfigurationManager _serverConfigs;
    private readonly SettingsHardcore _hardcoreSettingsUI;
    private readonly UiSharedService _uiShared;
    private readonly AvfxManager _avfxManager;
    private readonly VfxSpawns _vfxSpawns;
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
        ApiController apiController, GagspeakConfigService configService,
        PairManager pairManager, PlayerCharacterManager playerCharacterManager,
        ClientConfigurationManager clientConfigs, AvfxManager avfxManager,
        VfxSpawns vfxSpawns, ServerConfigurationManager serverConfigs,
        GagspeakMediator mediator, IpcManager ipcManager, SettingsHardcore hardcoreSettingsUI,
        OnFrameworkService frameworkUtil) : base(logger, mediator, "GagSpeak Settings")
    {
        _apiController = apiController;
        _playerCharacterManager = playerCharacterManager;
        _configService = configService;
        _pairManager = pairManager;
        _clientConfigs = clientConfigs;
        _avfxManager = avfxManager;
        _vfxSpawns = vfxSpawns;
        _serverConfigs = serverConfigs;
        _ipcManager = ipcManager;
        _frameworkUtil = frameworkUtil;
        _hardcoreSettingsUI = hardcoreSettingsUI;
        _uiShared = uiShared;
        AllowClickthrough = false;
        AllowPinning = false;

        // load the dropdown info
        LanguagesDialects = new Dictionary<string, string[]> {
            { "English", new string[] { "US", "UK" } },
            { "Spanish", new string[] { "Spain", "Mexico" } },
            { "French", new string[] { "France", "Quebec" } },
            { "Japanese", new string[] { "Japan" } }
        };
        _currentDialects = LanguagesDialects[_configService.Current.Language];
        _activeDialect = GetDialectFromConfigDialect();


        SizeConstraints = new WindowSizeConstraints()
        {
            MinimumSize = new Vector2(625, 400),
            MaximumSize = new Vector2(800, 2000),
        };

        Mediator.Subscribe<CharacterIpcDataCreatedMessage>(this, (msg) => LastCreatedCharacterData = msg.CharacterIPCData);
        Mediator.Subscribe<OpenSettingsUiMessage>(this, (_) => Toggle());
        Mediator.Subscribe<SwitchToIntroUiMessage>(this, (_) => IsOpen = false);
    }

    public CharacterIPCData? LastCreatedCharacterData { private get; set; }

    private ApiController ApiController => _uiShared.ApiController;
    private UserGlobalPermissions PlayerGlobalPerms => _playerCharacterManager.GlobalPerms!;
    private PiShockPermissions PlayerGlobalPiShock => _playerCharacterManager.GlobalPiShockPerms;

    // Everything below here is temporary until I figure out something better.
    public Dictionary<string, string[]> LanguagesDialects { get; init; } // Languages and Dialects for Chat Garbler.
    private string[] _currentDialects; // Array of Dialects for each Language
    private string _activeDialect; // Selected Dialect for Language
    private string _selectedAvfxFile;
    private string _selectedAvfxFile2;


    private string GetDialectFromConfigDialect()
    {
        switch (_configService.Current.LanguageDialect)
        {
            case "IPA_US": return "US";
            case "IPA_UK": return "UK";
            case "IPA_FRENCH": return "France";
            case "IPA_QUEBEC": return "Quebec";
            case "IPA_JAPAN": return "Japan";
            case "IPA_SPAIN": return "Spain";
            case "IPA_MEXICO": return "Mexico";
            default: return "US";
        }
    }

    private void SetConfigDialectFromDialect(string dialect)
    {
        switch (dialect)
        {
            case "US": _configService.Current.LanguageDialect = "IPA_US"; _configService.Save(); break;
            case "UK": _configService.Current.LanguageDialect = "IPA_UK"; _configService.Save(); break;
            case "France": _configService.Current.LanguageDialect = "IPA_FRENCH"; _configService.Save(); break;
            case "Quebec": _configService.Current.LanguageDialect = "IPA_QUEBEC"; _configService.Save(); break;
            case "Japan": _configService.Current.LanguageDialect = "IPA_JAPAN"; _configService.Save(); break;
            case "Spain": _configService.Current.LanguageDialect = "IPA_SPAIN"; _configService.Save(); break;
            case "Mexico": _configService.Current.LanguageDialect = "IPA_MEXICO"; _configService.Save(); break;
            default: _configService.Current.LanguageDialect = "IPA_US"; _configService.Save(); break;
        }
    }
    // Everything above here is temporary until I figure out something better.




    protected override void PreDrawInternal() { }
    protected override void PostDrawInternal() { }

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
        base.OnClose();
    }

    private void DrawGlobalSettings()
    {
        _lastTab = "Global";

        bool liveChatGarblerActive = PlayerGlobalPerms.LiveChatGarblerActive;
        bool liveChatGarblerLocked = PlayerGlobalPerms.LiveChatGarblerLocked;
        bool removeGagOnLockExpiration = _clientConfigs.GagspeakConfig.RemoveGagUponLockExpiration;

        bool wardrobeEnabled = PlayerGlobalPerms.WardrobeEnabled;
        bool itemAutoEquip = PlayerGlobalPerms.ItemAutoEquip;
        bool restraintSetAutoEquip = PlayerGlobalPerms.RestraintSetAutoEquip;
        bool restraintSetDisableWhenUnlocked = _clientConfigs.GagspeakConfig.DisableSetUponUnlock;
        RevertStyle RevertState = _clientConfigs.GagspeakConfig.RevertStyle;

        bool puppeteerEnabled = PlayerGlobalPerms.PuppeteerEnabled;
        string globalTriggerPhrase = PlayerGlobalPerms.GlobalTriggerPhrase;
        bool globalAllowSitRequests = PlayerGlobalPerms.GlobalAllowSitRequests;
        bool globalAllowMotionRequests = PlayerGlobalPerms.GlobalAllowMotionRequests;
        bool globalAllowAllRequests = PlayerGlobalPerms.GlobalAllowAllRequests;

        bool moodlesEnabled = PlayerGlobalPerms.MoodlesEnabled;

        bool toyboxEnabled = PlayerGlobalPerms.ToyboxEnabled;
        bool intifaceAutoConnect = _clientConfigs.GagspeakConfig.IntifaceAutoConnect;
        string intifaceConnectionAddr = _clientConfigs.GagspeakConfig.IntifaceConnectionSocket;
        bool vibeServerAutoConnect = _clientConfigs.GagspeakConfig.VibeServerAutoConnect;
        bool spatialVibratorAudio = PlayerGlobalPerms.SpatialVibratorAudio; // set here over client so that other players can reference if they should listen in or not.

        // pishock stuff.
        string piShockApiKey = _clientConfigs.GagspeakConfig.PiShockApiKey;
        string piShockUsername = _clientConfigs.GagspeakConfig.PiShockUsername;

        string globalShockCollarShareCode = PlayerGlobalPerms.GlobalShockShareCode;
        bool allowGlobalShockShockCollar = PlayerGlobalPiShock.AllowShocks;
        bool allowGlobalVibrateShockCollar = PlayerGlobalPiShock.AllowVibrations;
        bool allowGlobalBeepShockCollar = PlayerGlobalPiShock.AllowBeeps;
        int maxGlobalShockCollarIntensity = PlayerGlobalPiShock.MaxIntensity;
        TimeSpan maxGlobalShockDuration = PlayerGlobalPiShock.GetTimespanFromDuration();
        int maxGlobalVibrateDuration = (int)PlayerGlobalPerms.GlobalShockVibrateDuration.TotalSeconds;

        _uiShared.BigText("Gags");
        using (ImRaii.Disabled(liveChatGarblerLocked))
        {
            if (ImGui.Checkbox("Enable Live Chat Garbler", ref liveChatGarblerActive))
            {
                // Perform a mediator call that we have updated a permission.
                _ = _apiController.UserUpdateOwnGlobalPerm(new UserGlobalPermChangeDto(_apiController.PlayerUserData,
                    new KeyValuePair<string, object>("LiveChatGarblerActive", liveChatGarblerActive)));

            }
            _uiShared.DrawHelpText("If enabled, the Live Chat Garbler will garble your chat messages in-game. (This is done server-side, others will see it too)");
        }

        if (ImGui.Checkbox("Allow Gag Glamour's", ref itemAutoEquip))
        {
            PlayerGlobalPerms.ItemAutoEquip = itemAutoEquip;
            // if this creates a race condition down the line remove the above line.
            _ = _apiController.UserUpdateOwnGlobalPerm(new UserGlobalPermChangeDto(_apiController.PlayerUserData,
            new KeyValuePair<string, object>("ItemAutoEquip", itemAutoEquip)));
        }
        _uiShared.DrawHelpText("Allows Glamourer to bind your chosen Gag Glamour's upon becoming gagged!");

        if (ImGui.Checkbox("Remove Gag on Timer Padlock expiration.", ref removeGagOnLockExpiration))
        {
            _clientConfigs.GagspeakConfig.RemoveGagUponLockExpiration = removeGagOnLockExpiration;
            _clientConfigs.Save();
        }
        _uiShared.DrawHelpText("When a Gag is locked by a Timer, the Gag will be removed once the timer expires.");

        ImGui.Separator();
        _uiShared.BigText("Wardrobe");

        if (ImGui.Checkbox("Enable Wardrobe", ref wardrobeEnabled))
        {
            PlayerGlobalPerms.WardrobeEnabled = wardrobeEnabled;
            // if this creates a race condition down the line remove the above line.
            _ = _apiController.UserUpdateOwnGlobalPerm(new UserGlobalPermChangeDto(_apiController.PlayerUserData,
            new KeyValuePair<string, object>("WardrobeEnabled", wardrobeEnabled)));

        }
        _uiShared.DrawHelpText("If enabled, the all glamourer / penumbra / visual display information will become functional.");

        using (ImRaii.Disabled(!wardrobeEnabled))
        {
            using var indent = ImRaii.PushIndent();

            if (ImGui.Checkbox("Allow Restraint Sets to be Auto-Equipped", ref restraintSetAutoEquip))
            {
                PlayerGlobalPerms.RestraintSetAutoEquip = restraintSetAutoEquip;
                // if this creates a race condition down the line remove the above line.
                _ = _apiController.UserUpdateOwnGlobalPerm(new UserGlobalPermChangeDto(_apiController.PlayerUserData,
                new KeyValuePair<string, object>("RestraintSetAutoEquip", restraintSetAutoEquip)));

            }
            _uiShared.DrawHelpText("Allows Glamourer to bind restraint sets to your character.\nRestraint sets can be created in the Wardrobe Interface.");
        }

        if (ImGui.Checkbox("Disable Restraint's When Locks Expire", ref restraintSetDisableWhenUnlocked))
        {
            _clientConfigs.GagspeakConfig.DisableSetUponUnlock = restraintSetDisableWhenUnlocked;
            _clientConfigs.Save();
        }
        _uiShared.DrawHelpText("Let's the Active Restraint Set that is locked be automatically disabled when it's lock expires.");

        // draw out revert style selection
        _uiShared.DrawCombo($"Revert Style##Revert Type Style", 200f, Enum.GetValues<RevertStyle>(), (revertStyle) => revertStyle.ToString(),
        (i) =>
        {
            _clientConfigs.GagspeakConfig.RevertStyle = i;
            _clientConfigs.Save();
        }, _clientConfigs.GagspeakConfig.RevertStyle);



        ImGui.Separator();
        _uiShared.BigText("Puppeteer");

        if (ImGui.Checkbox("Enable Puppeteer", ref puppeteerEnabled))
        {
            PlayerGlobalPerms.PuppeteerEnabled = puppeteerEnabled;
            // if this creates a race condition down the line remove the above line.
            _ = _apiController.UserUpdateOwnGlobalPerm(new UserGlobalPermChangeDto(_apiController.PlayerUserData,
            new KeyValuePair<string, object>("PuppeteerEnabled", puppeteerEnabled)));

        }
        _uiShared.DrawHelpText("If enabled, the Puppeteer component will become functional.");

        using (ImRaii.Disabled(!puppeteerEnabled))
        {
            using var indent = ImRaii.PushIndent();

            ImGui.SetNextItemWidth(200 * ImGuiHelpers.GlobalScale);
            if (ImGui.InputText("Global Trigger Phrase", ref globalTriggerPhrase, 100, ImGuiInputTextFlags.EnterReturnsTrue))
            {
                PlayerGlobalPerms.GlobalTriggerPhrase = globalTriggerPhrase;
                // if this creates a race condition down the line remove the above line.
                _ = _apiController.UserUpdateOwnGlobalPerm(new UserGlobalPermChangeDto(_apiController.PlayerUserData,
                new KeyValuePair<string, object>("GlobalTriggerPhrase", globalTriggerPhrase)));

            }
            _uiShared.DrawHelpText("The global trigger phrase that will be used to trigger puppeteer commands.\n" +
                "LEAVE THIS FIELD BLANK TO HAVE NO GLOBAL TRIGGER PHRASE");

            if (ImGui.Checkbox("Globally Allow Sit Requests", ref globalAllowSitRequests))
            {
                PlayerGlobalPerms.GlobalAllowSitRequests = globalAllowSitRequests;
                // if this creates a race condition down the line remove the above line.
                _ = _apiController.UserUpdateOwnGlobalPerm(new UserGlobalPermChangeDto(_apiController.PlayerUserData,
                new KeyValuePair<string, object>("GlobalAllowSitRequests", globalAllowSitRequests)));

            }
            _uiShared.DrawHelpText("If enabled, the user will allow sit requests to be sent to them.");

            if (ImGui.Checkbox("Globally Allow Motion Requests", ref globalAllowMotionRequests))
            {
                PlayerGlobalPerms.GlobalAllowMotionRequests = globalAllowMotionRequests;
                // if this creates a race condition down the line remove the above line.
                _ = _apiController.UserUpdateOwnGlobalPerm(new UserGlobalPermChangeDto(_apiController.PlayerUserData,
                new KeyValuePair<string, object>("GlobalAllowMotionRequests", globalAllowMotionRequests)));

            }
            _uiShared.DrawHelpText("If enabled, the user will allow motion requests to be sent to them.");

            if (ImGui.Checkbox("Globally Allow All Requests", ref globalAllowAllRequests))
            {
                PlayerGlobalPerms.GlobalAllowAllRequests = globalAllowAllRequests;
                // if this creates a race condition down the line remove the above line.
                _ = _apiController.UserUpdateOwnGlobalPerm(new UserGlobalPermChangeDto(_apiController.PlayerUserData,
                new KeyValuePair<string, object>("GlobalAllowAllRequests", globalAllowAllRequests)));

            }
            _uiShared.DrawHelpText("If enabled, the user will allow all requests to be sent to them.");
        }


        ImGui.Separator();
        _uiShared.BigText("Moodles");

        if (ImGui.Checkbox("Enable Moodles", ref moodlesEnabled))
        {
            PlayerGlobalPerms.MoodlesEnabled = moodlesEnabled;
            // if this creates a race condition down the line remove the above line.
            _ = _apiController.UserUpdateOwnGlobalPerm(new UserGlobalPermChangeDto(_apiController.PlayerUserData,
            new KeyValuePair<string, object>("MoodlesEnabled", moodlesEnabled)));

        }
        _uiShared.DrawHelpText("If enabled, the moodles component will become functional.");



        ImGui.Separator();
        _uiShared.BigText("Toybox");

        if (ImGui.Checkbox("Enable Toybox", ref toyboxEnabled))
        {
            PlayerGlobalPerms.ToyboxEnabled = toyboxEnabled;
            // if this creates a race condition down the line remove the above line.
            _ = _apiController.UserUpdateOwnGlobalPerm(new UserGlobalPermChangeDto(_apiController.PlayerUserData,
            new KeyValuePair<string, object>("ToyboxEnabled", toyboxEnabled)));

        }
        _uiShared.DrawHelpText("If enabled, the toybox component will become functional.");


        if (ImGui.Checkbox("Automatically Connect to Intiface Central", ref intifaceAutoConnect))
        {
            _clientConfigs.GagspeakConfig.IntifaceAutoConnect = intifaceAutoConnect;
            _clientConfigs.Save();
        }
        _uiShared.DrawHelpText("Automatically connects to intiface central, or at least attempts to. Upon plugin startup.");

        ImGui.SetNextItemWidth(200f);
        if (ImGui.InputTextWithHint($"Server Address##ConnectionWSaddr", "Leave blank for default...", ref intifaceConnectionAddr, 100, ImGuiInputTextFlags.EnterReturnsTrue))
        {
            if (!intifaceConnectionAddr.Contains("ws://"))
            {
                intifaceConnectionAddr = "ws://localhost:12345";
            }
            else
            {
                _clientConfigs.GagspeakConfig.IntifaceConnectionSocket = intifaceConnectionAddr;
                _clientConfigs.Save();
            }
        }
        _uiShared.DrawHelpText($"Change the Intiface Server Address to a custom one if you desire!." +
            Environment.NewLine + "Leave blank to use the default address.");

        if (ImGui.Checkbox("Automatically Connect to Vibe Server", ref vibeServerAutoConnect))
        {
            _clientConfigs.GagspeakConfig.VibeServerAutoConnect = vibeServerAutoConnect;
            _clientConfigs.Save();
        }
        _uiShared.DrawHelpText("Connects to the Vibe Server automatically upon successful Main Server Connection.");

        if (ImGui.Checkbox("Use Spatial Vibrator Audio", ref spatialVibratorAudio))
        {
            PlayerGlobalPerms.SpatialVibratorAudio = spatialVibratorAudio;
            // if this creates a race condition down the line remove the above line.
            _ = _apiController.UserUpdateOwnGlobalPerm(new UserGlobalPermChangeDto(_apiController.PlayerUserData,
            new KeyValuePair<string, object>("SpatialVibratorAudio", spatialVibratorAudio)));
        }
        _uiShared.DrawHelpText("If enabled, you will emit vibrator audio while your sex toys are active to other paired players around you.");

        ImGui.Spacing();




        ImGui.SetNextItemWidth(250 * ImGuiHelpers.GlobalScale);
        if (ImGui.InputText("PiShock API Key", ref piShockApiKey, 100, ImGuiInputTextFlags.EnterReturnsTrue))
        {
            _clientConfigs.GagspeakConfig.PiShockApiKey = piShockApiKey;
            _clientConfigs.Save();
        }
        _uiShared.DrawHelpText("Required PiShock API Key to exist for any PiShock related interactions to work.");

        ImGui.SetNextItemWidth(250 * ImGuiHelpers.GlobalScale);
        if (ImGui.InputText("PiShock Username", ref piShockUsername, 100, ImGuiInputTextFlags.EnterReturnsTrue))
        {
            _clientConfigs.GagspeakConfig.PiShockUsername = piShockUsername;
            _clientConfigs.Save();
        }
        _uiShared.DrawHelpText("Required PiShock Username to exist for any PiShock related interactions to work.");


        ImGui.SetNextItemWidth(250 * ImGuiHelpers.GlobalScale - _uiShared.GetIconTextButtonSize(FontAwesomeIcon.Sync, "Refresh") - ImGui.GetStyle().ItemInnerSpacing.X);
        if (ImGui.InputText("##Global PiShock Share Code", ref globalShockCollarShareCode, 100, ImGuiInputTextFlags.EnterReturnsTrue))
        {
            PlayerGlobalPerms.GlobalShockShareCode = globalShockCollarShareCode;

            _ = _apiController.UserUpdateOwnGlobalPerm(new(_apiController.PlayerUserData,
            new KeyValuePair<string, object>("GlobalShockShareCode", globalShockCollarShareCode)));
        }
        ImUtf8.SameLineInner();
        if (_uiShared.IconTextButton(FontAwesomeIcon.Sync, "Refresh", null, false, DateTime.UtcNow - _lastRefresh < TimeSpan.FromSeconds(5)))
        {
            _lastRefresh = DateTime.UtcNow;
            _playerCharacterManager.UpdateGlobalPiShockPerms();
        }
        UiSharedService.AttachToolTip("Forces Global PiShock Share Code to grab latest data from the API and push it to other online pairs.");
        ImUtf8.SameLineInner();
        ImGui.TextUnformatted("PiShock Global Share Code");
        _uiShared.DrawHelpText("Global PiShock Share Code used for your connected ShockCollar." + Environment.NewLine + "NOTE:" + Environment.NewLine
            + "While this is a GLOBAL share code, only people you are in Hardcore mode with will have access to it.");

        ImGui.SetNextItemWidth(250 * ImGuiHelpers.GlobalScale);
        if(ImGui.SliderInt("Global Max Vibration Time", ref maxGlobalVibrateDuration, 0, 30))
        {
            PlayerGlobalPerms.GlobalShockVibrateDuration = TimeSpan.FromSeconds(maxGlobalVibrateDuration);
        }
        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            // Convert TimeSpan to ticks and send as UInt64
            ulong ticks = (ulong)PlayerGlobalPerms.GlobalShockVibrateDuration.Ticks;
            _ = _apiController.UserUpdateOwnGlobalPerm(new(_apiController.PlayerUserData,
            new KeyValuePair<string, object>("GlobalShockVibrateDuration", ticks)));
        }
        _uiShared.DrawHelpText("The maximum time in seconds that your shock collar can vibrate for.");

        // make this section readonly
        UiSharedService.ColorText("Global Shock Collar Permissions (Parsed From Share Code)", ImGuiColors.ParsedGold);
        using (ImRaii.Disabled(true))
        {
            using (ImRaii.Group())
            {
                ImGui.Checkbox("Shocks Allowed", ref allowGlobalShockShockCollar);
                ImGui.SameLine();
                ImGui.Checkbox("Vibrations Allowed", ref allowGlobalVibrateShockCollar);
                ImGui.SameLine();
                ImGui.Checkbox("Beeps Allowed", ref allowGlobalBeepShockCollar);
            }
            ImGui.TextUnformatted("Global Max Shock Intensity: ");
            ImGui.SameLine();
            UiSharedService.ColorText(maxGlobalShockCollarIntensity.ToString() + "%", ImGuiColors.ParsedGold);
            
            ImGui.TextUnformatted("Global Max Shock Duration: ");
            ImGui.SameLine();
            UiSharedService.ColorText(maxGlobalShockDuration.Seconds.ToString()+"."+maxGlobalShockDuration.Milliseconds.ToString()+"s", ImGuiColors.ParsedGold);
        }
    }
    private DateTime _lastRefresh = DateTime.MinValue;

    private void DrawPreferences()
    {
        _lastTab = "Preferences";

        _uiShared.BigText("Live Chat Garbler");

        ImGui.AlignTextToFramePadding();
        ImGui.Text("GagSpeak Channels:");
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Every selected channel from here becomes a channel that your direct chat garbler works in.");
        }
        ImGui.SameLine();
        // Create the language dropdown
        ImGui.SetNextItemWidth(ImGuiHelpers.GlobalScale * 65);
        string prevLang = _configService.Current.Language; // to only execute code to update data once it is changed
        if (ImGui.BeginCombo("##Language", _configService.Current.Language, ImGuiComboFlags.NoArrowButton))
        {
            foreach (var language in LanguagesDialects.Keys.ToArray())
            {
                bool isSelected = (_configService.Current.Language == language);
                if (ImGui.Selectable(language, isSelected))
                {
                    _configService.Current.Language = language;
                    _configService.Save();
                }
                if (isSelected)
                {
                    ImGui.SetItemDefaultFocus();
                }
            }
            ImGui.EndCombo();
        }
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Select the language you want to use for GagSpeak.");
        }
        //update if changed 
        if (prevLang != _configService.Current.Language)
        { // set the language to the newly selected language once it is changed
            _currentDialects = LanguagesDialects[_configService.Current.Language]; // update the dialects for the new language
            _activeDialect = _currentDialects[0]; // set the active dialect to the first dialect of the new language
            SetConfigDialectFromDialect(_activeDialect);
            _configService.Save();
        }
        ImGui.SameLine();
        // Create the dialect dropdown
        ImGui.SetNextItemWidth(ImGuiHelpers.GlobalScale * 55);
        string[] dialects = LanguagesDialects[_configService.Current.Language];
        string prevDialect = _activeDialect; // to only execute code to update data once it is changed
        if (ImGui.BeginCombo("##Dialect", _activeDialect, ImGuiComboFlags.NoArrowButton))
        {
            foreach (var dialect in dialects)
            {
                bool isSelected = (_activeDialect == dialect);
                if (ImGui.Selectable(dialect, isSelected))
                {
                    _activeDialect = dialect;
                }
                if (isSelected)
                {
                    ImGui.SetItemDefaultFocus();
                }
            }
            ImGui.EndCombo();
        }
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Select the Dialect you want to use for GagSpeak.");
        }
        //update if changed
        if (prevDialect != _activeDialect)
        { // set the dialect to the newly selected dialect once it is changed
            SetConfigDialectFromDialect(_activeDialect);
            _configService.Save();
        }

        // display the channels
        var i = 0;
        foreach (var e in ChatChannel.GetOrderedChannels())
        {
            // See if it is already enabled by default
            var enabled = _configService.Current.ChannelsGagSpeak.Contains(e);
            // Create a new line after every 4 columns
            if (i != 0 && (i == 4 || i == 7 || i == 11 || i == 15 || i == 19))
            {
                ImGui.NewLine();
                //i = 0;
            }
            // Move to the next row if it is LS1 or CWLS1
            if (e is ChatChannel.ChatChannels.LS1 or ChatChannel.ChatChannels.CWL1)
                ImGui.Separator();

            if (ImGui.Checkbox($"{e}", ref enabled))
            {
                // See If the UIHelpers.Checkbox is clicked, If not, add to the list of enabled channels, otherwise, remove it.
                if (enabled)
                {
                    // ensure that it is not already in the list first
                    if (!_configService.Current.ChannelsGagSpeak.Contains(e))
                    {
                        // if it doesn't exist, add it.
                        _configService.Current.ChannelsGagSpeak.Add(e);
                    }
                }
                else
                {
                    // try and remove the channel from the list.
                    _configService.Current.ChannelsGagSpeak.Remove(e);
                }
                // save config.
                _configService.Save();
            }

            ImGui.SameLine();
            i++;
        }

        ImGui.NewLine();
        ImGui.Separator();
        _uiShared.BigText("Puppeteer Allowed Channels");
        // display the channels
        var j = 0;
        foreach (var e in ChatChannel.GetOrderedChannels())
        {
            // See if it is already enabled by default
            var enabled = _configService.Current.ChannelsPuppeteer.Contains(e);

            // Create a new line after every 4 columns
            if (j != 0 && (j == 4 || j == 7 || j == 11 || j == 15 || j == 19))
                ImGui.NewLine();

            // Move to the next row if it is LS1 or CWLS1
            if (e is ChatChannel.ChatChannels.LS1 or ChatChannel.ChatChannels.CWL1)
                ImGui.Separator();

            if (ImGui.Checkbox($"{e}##{e}puppeteer", ref enabled))
            {
                if (enabled)
                {
                    if (!_configService.Current.ChannelsPuppeteer.Contains(e))
                    {
                        _configService.Current.ChannelsPuppeteer.Add(e);
                    }
                }
                else
                {
                    _configService.Current.ChannelsPuppeteer.Remove(e);
                }
                _configService.Save();
            }

            ImGui.SameLine();
            j++;
        }

        ImGui.NewLine();
        // the nicknames section
        ImGui.Separator();
        _uiShared.BigText("Nicknames");

        // see if the user wants to allow a popup to create nicknames upon adding a paired user
        var openPopupOnAddition = _configService.Current.OpenPopupOnAdd;
        if (ImGui.Checkbox("Open Nickname Popup when adding a GagSpeak user", ref openPopupOnAddition))
        {
            _configService.Current.OpenPopupOnAdd = openPopupOnAddition;
            _configService.Save();
        }
        _uiShared.DrawHelpText("When enabled, a popup will automatically display\nafter adding another user," +
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
        ImGui.SetNextItemWidth(200 * ImGuiHelpers.GlobalScale);
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

        // display title for account management
        _uiShared.BigText("Primary GagSpeak Account");

        // obtain our local content id
        var localContentId = _uiShared.PlayerLocalContentID;

        // obtain the primary account auth.
        var primaryAuth = _serverConfigs.CurrentServer.Authentications.FirstOrDefault(c => c.IsPrimary);
        if (primaryAuth == null)
        {
            UiSharedService.ColorText("No primary account setup to display", ImGuiColors.DPSRed);
            return;
        }
        else
        {
            DrawAccount(int.MaxValue, primaryAuth, primaryAuth.CharacterPlayerContentId == localContentId);
        }

        // display title for account management
        _uiShared.BigText("Secondary Accounts:");
        // now we need to display the rest of the secondary authentications of the primary account. In other words all other authentications.
        if (_serverConfigs.HasAnySecretKeys())
        {
            // fetch the list of additional authentications that are not the primary account.
            var secondaryAuths = _serverConfigs.CurrentServer.Authentications.Where(c => !c.IsPrimary).ToList();

            for (int i = 0; i < secondaryAuths.Count; i++)
            {
                DrawAccount(i, secondaryAuths[i], secondaryAuths[i].CharacterPlayerContentId == localContentId);
            }
        }
        else
        {
            UiSharedService.ColorText("No secondary accounts setup to display", ImGuiColors.DPSRed);
        }
    }

    public bool ShowKeyLabel = true;
    public int EditingIdx = -1;
    private void DrawAccount(int idx, Authentication account, bool isOnlineUser = false)
    {
        bool isPrimary = account.IsPrimary;
        // push rounding window corners
        using var windowRounding = ImRaii.PushStyle(ImGuiStyleVar.ChildRounding, 5f);
        // push a pink border color for the window border.
        using var borderColor = ImRaii.PushStyle(ImGuiStyleVar.WindowBorderSize, 1f);
        using var borderCol = ImRaii.PushColor(ImGuiCol.Border, isPrimary ? ImGuiColors.ParsedGold : ImGuiColors.ParsedPink);
        // push a less transparent very dark grey background color.
        using var bgColor = ImRaii.PushColor(ImGuiCol.ChildBg, new Vector4(0.25f, 0.2f, 0.2f, 0.4f));
        // create the child window.

        float height = ImGui.GetFrameHeight() * 3 + ImGui.GetStyle().ItemSpacing.Y * 2 + ImGui.GetStyle().WindowPadding.Y * 2;
        using var child = ImRaii.Child($"##AuthAccountListing"+idx+ account.CharacterPlayerContentId, new Vector2(ImGui.GetContentRegionAvail().X, height), true, ImGuiWindowFlags.ChildWindow);
        if (!child) return;

        using (var group = ImRaii.Group())
        {
            ImGui.AlignTextToFramePadding();
            _uiShared.IconText(FontAwesomeIcon.UserCircle);
            ImUtf8.SameLineInner();
            UiSharedService.ColorText(account.CharacterName, isPrimary ? ImGuiColors.ParsedGold : ImGuiColors.ParsedPink);
            UiSharedService.AttachToolTip("This Character's Name");

            // head over to the end to make the delete button.
            var isPrimaryIcon = _uiShared.GetIconData(FontAwesomeIcon.Fingerprint);

            ImGui.SameLine(ImGui.GetContentRegionAvail().X - _uiShared.GetIconTextButtonSize(FontAwesomeIcon.Trash, "Delete Account"));
            if (_uiShared.IconTextButton(FontAwesomeIcon.Trash, "Delete Account", null, true, // yes there must be a lot to determine if you can delete.
            (!(UiSharedService.CtrlPressed() && UiSharedService.ShiftPressed()) || !(_apiController.ServerAlive && _apiController.IsConnected && isOnlineUser)),
            "##Trash-" + account.CharacterPlayerContentId.ToString()))
            {
                _deleteAccountPopupModalShown = true;
                ImGui.OpenPopup("Delete your account?");
            }
            UiSharedService.AttachToolTip("Permanently remove the use of this secret key and registered account for this character."+Environment.NewLine
                + "You CANNOT RE-USE THIS SECRET KEY. IT IS BOUND TO THIS UID." + Environment.NewLine
                + "If you want to create a new account for this login, you must create a new key for it after removing.");
        }
        // next line:
        using (var group2 = ImRaii.Group())
        {
            ImGui.AlignTextToFramePadding();
            _uiShared.IconText(FontAwesomeIcon.Globe);
            ImUtf8.SameLineInner();
            UiSharedService.ColorText(_uiShared.WorldData[(ushort)account.WorldId], isPrimary ? ImGuiColors.ParsedGold : ImGuiColors.ParsedPink);
            UiSharedService.AttachToolTip("The Homeworld of this Character's Account");

            var isPrimaryIcon = _uiShared.GetIconData(FontAwesomeIcon.Fingerprint);
            var successfulConnection = _uiShared.GetIconData(FontAwesomeIcon.PlugCircleCheck);
            float rightEnd = ImGui.GetContentRegionAvail().X - successfulConnection.X - isPrimaryIcon.X - 2*ImGui.GetStyle().ItemInnerSpacing.X;
            ImGui.SameLine(rightEnd);
            _uiShared.BooleanToColoredIcon(account.IsPrimary, false, FontAwesomeIcon.Fingerprint, FontAwesomeIcon.Fingerprint, isPrimary ? ImGuiColors.ParsedGold : ImGuiColors.ParsedPink, ImGuiColors.DalamudGrey3);
            UiSharedService.AttachToolTip(account.IsPrimary ? "This is your Primary Gagspeak Account" : "This your secondary GagSpeak Account");
            _uiShared.BooleanToColoredIcon(account.SecretKey.HasHadSuccessfulConnection, true, FontAwesomeIcon.PlugCircleCheck, FontAwesomeIcon.PlugCircleXmark, ImGuiColors.ParsedGreen, ImGuiColors.DalamudGrey3);
            UiSharedService.AttachToolTip(account.SecretKey.HasHadSuccessfulConnection ? "Has Connected to servers with secret key successfully" : "Has not yet had a successful connection with this Key.");
        }

        // next line:
        using (var group3 = ImRaii.Group())
        {
            string keyDisplayText = ShowKeyLabel ? account.SecretKey.Label : account.SecretKey.Key;
            ImGui.AlignTextToFramePadding();
            _uiShared.IconText(FontAwesomeIcon.Key);
            if(ImGui.IsItemClicked())
            {
                ShowKeyLabel = !ShowKeyLabel;
            }
            UiSharedService.AttachToolTip("Secret Key for this account. (Insert by clicking the edit pen icon)");
            // we shoul draw an inputtext field here if we can edit it, and a text field if we cant.
            if(EditingIdx == idx)
            {
                ImUtf8.SameLineInner();
                ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - _uiShared.GetIconButtonSize(FontAwesomeIcon.PenSquare).X - ImGui.GetStyle().ItemSpacing.X);
                string key = account.SecretKey.Key;
                if (ImGui.InputTextWithHint("##SecondaryAuthKey"+account.CharacterPlayerContentId, "Paste Secret Key Here...", ref key, 64, ImGuiInputTextFlags.EnterReturnsTrue))
                {
                    _logger.LogInformation("This would have updated the secret key!");
                    if(account.SecretKey.Label.IsNullOrEmpty())
                    {
                        account.SecretKey.Label = "Alt Character Key for " + account.CharacterName + " on " + _uiShared.WorldData[(ushort)account.WorldId];
                    }
                    account.SecretKey.Key = key;
                    EditingIdx = -1;
                    _serverConfigs.Save();
                }
            }
            else
            {
                ImUtf8.SameLineInner();
                UiSharedService.ColorText(keyDisplayText, isPrimary ? ImGuiColors.ParsedGold : ImGuiColors.ParsedPink);
                if (ImGui.IsItemClicked())
                {
                    ImGui.SetClipboardText(account.SecretKey.Key);
                }
                UiSharedService.AttachToolTip("Click the friendly label to copy the actual secret key to clipboard");
            }

            if(idx != int.MaxValue)
            {
                var insertKey = _uiShared.GetIconData(FontAwesomeIcon.PenSquare);
                float rightEnd = ImGui.GetContentRegionAvail().X - insertKey.X;
                ImGui.SameLine(rightEnd);
                Vector4 col = account.SecretKey.HasHadSuccessfulConnection ? ImGuiColors.DalamudRed : ImGuiColors.DalamudGrey3;
                _uiShared.BooleanToColoredIcon(EditingIdx == idx, false, FontAwesomeIcon.PenSquare, FontAwesomeIcon.PenSquare, ImGuiColors.ParsedPink, col);
                if (ImGui.IsItemClicked() && !account.SecretKey.HasHadSuccessfulConnection)
                {
                    EditingIdx = EditingIdx == idx ? -1 : idx;
                }
                UiSharedService.AttachToolTip(account.SecretKey.HasHadSuccessfulConnection
                    ? "You cannot change a key that has been verified. This is your character's Key now."
                    : "Click to insert a provided secretKey");
            }
        }

        if (ImGui.BeginPopupModal("Delete your account?", ref _deleteAccountPopupModalShown, UiSharedService.PopupWindowFlags))
        {
            UiSharedService.TextWrapped("Be Deleting your primary GagSpeak account, all seconary users below will also be deleted.");
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
    }

    /// <summary> Displays the Debug section within the settings, where we can set our debug level </summary>
    private void DrawDebug()
    {
        _lastTab = "Debug";
        // display Debug Configuration in fat text
        _uiShared.BigText("Debug Configuration");

        // display the combo box for setting the log level we wish to have for our plugin
        _uiShared.DrawCombo("Log Level", 400, Enum.GetValues<LogLevel>(), (level) => level.ToString(), (level) =>
        {
            _configService.Current.LogLevel = level;
            _configService.Save();
        }, _configService.Current.LogLevel);

        bool logResourceManagement = _configService.Current.LogResourceManagement;
        bool logActionEffects = _configService.Current.LogActionEffects;
        bool logServerHealth = _configService.Current.LogServerConnectionHealth;

        if (ImGui.Checkbox("Log Resource Management", ref logResourceManagement))
        {
            _configService.Current.LogResourceManagement = logResourceManagement;
            _configService.Save();
        }
        _uiShared.DrawHelpText("Log vibrator audio source management to the debug log.");

        if (ImGui.Checkbox("Log Action Effect", ref logActionEffects))
        {
            _configService.Current.LogActionEffects = logActionEffects;
            _configService.Save();
        }
        _uiShared.DrawHelpText("Log the action effects received to your client from yourself or other players actions.");

        if (ImGui.Checkbox("Log Server Health", ref logServerHealth))
        {
            _configService.Current.LogServerConnectionHealth = logServerHealth;
            _configService.Save();
        }
        _uiShared.DrawHelpText("Log server connection health to the debug log.");

        _uiShared.BigText("Vibrator Audio Testing Beta");
        UiSharedService.ColorText("(WILL CRASH YOU IF NOT CORDY)", ImGuiColors.DPSRed);

        var avfxFiles = _avfxManager.GetAvfxFiles();
        var width = ImGui.GetContentRegionAvail().X / 3;
        using (var group = ImRaii.Group())
        {
            ImGui.SetNextItemWidth(width);
            if (ImGui.BeginCombo("Select AVFX File", _selectedAvfxFile))
            {
                foreach (var file in avfxFiles)
                {
                    if (ImGui.Selectable(file, file == _selectedAvfxFile))
                    {
                        _selectedAvfxFile = file;
                    }
                }
                ImGui.EndCombo();
            }
            ImGui.SetNextItemWidth(width);
            _vfxSpawns.DrawVfxRemove();
            ImGui.SetNextItemWidth(width);
            _vfxSpawns.DrawVfxSpawnOptions(_selectedAvfxFile, true, 1);
        }

        ImGui.SameLine();

        using (var group = ImRaii.Group())
        {
            ImGui.SetNextItemWidth(width);
            if (ImGui.BeginCombo("Select AVFX File2", _selectedAvfxFile2))
            {
                foreach (var file in avfxFiles)
                {
                    if (ImGui.Selectable(file, file == _selectedAvfxFile2))
                    {
                        _selectedAvfxFile2 = file;
                    }
                }
                ImGui.EndCombo();
            }
            ImGui.SetNextItemWidth(width);
            _vfxSpawns.DrawVfxRemove();
            ImGui.SetNextItemWidth(width);
            _vfxSpawns.DrawVfxSpawnOptions(_selectedAvfxFile2, true, 2);
        }
    }

    private void DrawPlayerCharacterDebug()
    {
        _lastTab = "Player Debug";
        // display Debug Configuration in fat text
        _uiShared.BigText("Player Character Debug Info:");
        if (LastCreatedCharacterData != null && ImGui.TreeNode("Last created character data"))
        {
            var json = JsonConvert.SerializeObject(LastCreatedCharacterData, Formatting.Indented);
            foreach (var line in json.Split('\n'))
            {
                ImGui.TextUnformatted($"{line}");
            }

            ImGui.TreePop();
        }

        if (_uiShared.IconTextButton(FontAwesomeIcon.Copy, "[DEBUG] Copy Last created Character Data to clipboard"))
        {
            if (LastCreatedCharacterData != null)
            {
                var json = JsonConvert.SerializeObject(LastCreatedCharacterData, Formatting.Indented);
                ImGui.SetClipboardText(json);
            }
            else
            {
                ImGui.SetClipboardText("ERROR: No created character data, cannot copy.");
            }
        }
        UiSharedService.AttachToolTip("Use this when reporting mods being rejected from the server.");


        // draw debug information for character information.
        if (ImGui.CollapsingHeader("Global Data")) { DrawGlobalInfo(); }
        if (ImGui.CollapsingHeader("Appearance Data")) { DrawAppearanceInfo(); }
        if (ImGui.CollapsingHeader("Wardrobe Data")) { _clientConfigs.DrawWardrobeInfo(); }
        if (ImGui.CollapsingHeader("AliasData")) { _clientConfigs.DrawAliasLists(); }
        if (ImGui.CollapsingHeader("Patterns Data")) { _clientConfigs.DrawPatternsInfo(); }
    }

    private void DrawPairsDebug()
    {
        _lastTab = "Pairs Debug";
        // display Debug Configuration in fat text
        _uiShared.BigText("Pairs Debug Info:");

        // Display additional info about the Pair Manager
        int totalPairs = _pairManager.DirectPairs.Count;
        int visibleUsersCount = _pairManager.GetVisibleUserCount();
        ImGui.Text($"Total Pairs: {totalPairs}");
        ImGui.Text($"Visible Users: {visibleUsersCount}");

        // Iterate through all client pairs in the PairManager
        foreach (var clientPair in _pairManager.DirectPairs)
        {
            if (ImGui.CollapsingHeader($"Pair: {clientPair.UserData.UID} || {_serverConfigs.GetNicknameForUid(clientPair.UserData.UID)}"))
            {
                ImGui.Text($"UserData UID: {clientPair.UserData.UID}");
                ImGui.Indent();

                // Accessing and displaying information from the Pair object
                ImGui.Text($"IsDirectlyPaired: {clientPair.IsDirectlyPaired}");
                ImGui.Text($"IsOneSidedPair: {clientPair.IsOneSidedPair}");
                ImGui.Text($"IsOnline: {clientPair.IsOnline}");
                ImGui.Text($"IsPaired: {clientPair.IsPaired}");
                ImGui.Text($"IsVisible: {clientPair.IsVisible}");
                ImGui.Text($"HasIPCData: {clientPair.LastReceivedIpcData == null}");
                ImGui.Text($"PlayerName: {clientPair.PlayerNameWithWorld ?? "N/A"}");

                if (clientPair.UserPairGlobalPerms != null)
                {
                    if (ImGui.CollapsingHeader($"{clientPair.UserData.UID}'s Global permissions || {_serverConfigs.GetNicknameForUid(clientPair.UserData.UID)}"))
                    {
                        ImGui.Text($"Safeword: {clientPair.UserPairGlobalPerms.Safeword}");
                        ImGui.Text($"SafewordUsed: {clientPair.UserPairGlobalPerms.SafewordUsed}");
                        ImGui.Text($"LiveChatGarblerActive: {clientPair.UserPairGlobalPerms.LiveChatGarblerActive}");
                        ImGui.Text($"LiveChatGarblerLocked: {clientPair.UserPairGlobalPerms.LiveChatGarblerLocked}");
                        ImGui.Separator();
                        ImGui.Text($"WardrobeEnabled: {clientPair.UserPairGlobalPerms.WardrobeEnabled}");
                        ImGui.Text($"ItemAutoEquip: {clientPair.UserPairGlobalPerms.ItemAutoEquip}");
                        ImGui.Text($"RestraintSetAutoEquip: {clientPair.UserPairGlobalPerms.RestraintSetAutoEquip}");
                        ImGui.Separator();
                        ImGui.Text($"PuppeteerEnabled: {clientPair.UserPairGlobalPerms.PuppeteerEnabled}");
                        ImGui.Text($"GlobalTriggerPhrase: {clientPair.UserPairGlobalPerms.GlobalTriggerPhrase}");
                        ImGui.Text($"GlobalAllowSitRequests: {clientPair.UserPairGlobalPerms.GlobalAllowSitRequests}");
                        ImGui.Text($"GlobalAllowMotionRequests: {clientPair.UserPairGlobalPerms.GlobalAllowMotionRequests}");
                        ImGui.Text($"GlobalAllowAllRequests: {clientPair.UserPairGlobalPerms.GlobalAllowAllRequests}");
                        ImGui.Separator();
                        ImGui.Text($"MoodlesEnabled: {clientPair.UserPairGlobalPerms.MoodlesEnabled}");
                        ImGui.Separator();
                        ImGui.Text($"ToyboxEnabled: {clientPair.UserPairGlobalPerms.ToyboxEnabled}");
                        ImGui.Text($"LockToyboxUI: {clientPair.UserPairGlobalPerms.LockToyboxUI}");
                        ImGui.Text($"ToyIsActive: {clientPair.UserPairGlobalPerms.ToyIsActive}");
                        ImGui.Text($"ToyIntensity: {clientPair.UserPairGlobalPerms.ToyIntensity}");
                        ImGui.Text($"SpatialVibratorAudio: {clientPair.UserPairGlobalPerms.SpatialVibratorAudio}");
                    }
                }
                if (clientPair.UserPairUniquePairPerms != null)
                {
                    if (ImGui.CollapsingHeader($"{clientPair.UserData.UID}'s Pair permissions || {_serverConfigs.GetNicknameForUid(clientPair.UserData.UID)}"))
                    {
                        ImGui.Text($"IsPaused: {clientPair.UserPairUniquePairPerms.IsPaused}");
                        ImGui.Text($"ExtendedLockTimes: {clientPair.UserPairUniquePairPerms.ExtendedLockTimes}");
                        ImGui.Text($"MaxLockTime: {clientPair.UserPairUniquePairPerms.MaxLockTime}");
                        ImGui.Text($"InHardcore: {clientPair.UserPairUniquePairPerms.InHardcore}");
                        ImGui.Separator();
                        ImGui.Text($"ApplyRestraintSets: {clientPair.UserPairUniquePairPerms.ApplyRestraintSets}");
                        ImGui.Text($"LockRestraintSets: {clientPair.UserPairUniquePairPerms.LockRestraintSets}");
                        ImGui.Text($"MaxAllowedRestraintTime: {clientPair.UserPairUniquePairPerms.MaxAllowedRestraintTime}");
                        ImGui.Text($"RemoveRestraintSets: {clientPair.UserPairUniquePairPerms.RemoveRestraintSets}");
                        ImGui.Separator();
                        ImGui.Text($"TriggerPhrase: {clientPair.UserPairUniquePairPerms.TriggerPhrase}");
                        ImGui.Text($"StartChar: {clientPair.UserPairUniquePairPerms.StartChar}");
                        ImGui.Text($"EndChar: {clientPair.UserPairUniquePairPerms.EndChar}");
                        ImGui.Text($"AllowSitRequests: {clientPair.UserPairUniquePairPerms.AllowSitRequests}");
                        ImGui.Text($"AllowMotionRequests: {clientPair.UserPairUniquePairPerms.AllowMotionRequests}");
                        ImGui.Text($"AllowAllRequests: {clientPair.UserPairUniquePairPerms.AllowAllRequests}");
                        ImGui.Separator();
                        ImGui.Text($"AllowPositiveStatusTypes: {clientPair.UserPairUniquePairPerms.AllowPositiveStatusTypes}");
                        ImGui.Text($"AllowNegativeStatusTypes: {clientPair.UserPairUniquePairPerms.AllowNegativeStatusTypes}");
                        ImGui.Text($"AllowSpecialStatusTypes: {clientPair.UserPairUniquePairPerms.AllowSpecialStatusTypes}");
                        ImGui.Text($"PairCanApplyOwnMoodlesToYou: {clientPair.UserPairUniquePairPerms.PairCanApplyOwnMoodlesToYou}");
                        ImGui.Text($"PairCanApplyYourMoodlesToYou: {clientPair.UserPairUniquePairPerms.PairCanApplyYourMoodlesToYou}");
                        ImGui.Text($"MaxMoodleTime: {clientPair.UserPairUniquePairPerms.MaxMoodleTime}");
                        ImGui.Text($"AllowPermanentMoodles: {clientPair.UserPairUniquePairPerms.AllowPermanentMoodles}");
                        ImGui.Separator();
                        ImGui.Text($"ChangeToyState: {clientPair.UserPairUniquePairPerms.CanToggleToyState}");
                        ImGui.Text($"CanUseVibeRemote: {clientPair.UserPairUniquePairPerms.CanUseVibeRemote}");
                        ImGui.Text($"CanToggleAlarms: {clientPair.UserPairUniquePairPerms.CanToggleAlarms}");
                        ImGui.Text($"CanSendAlarms: {clientPair.UserPairUniquePairPerms.CanSendAlarms}");
                        ImGui.Text($"CanExecutePatterns: {clientPair.UserPairUniquePairPerms.CanExecutePatterns}");
                        ImGui.Text($"CanStopPatterns: {clientPair.UserPairUniquePairPerms.CanStopPatterns}");
                        ImGui.Text($"CanToggleTriggers: {clientPair.UserPairUniquePairPerms.CanToggleTriggers}");
                        ImGui.Separator();
                        ImGui.Text($"AllowForcedFollow: {clientPair.UserPairUniquePairPerms.AllowForcedFollow}");
                        ImGui.Text($"IsForcedToFollow: {clientPair.UserPairUniquePairPerms.IsForcedToFollow}");
                        ImGui.Text($"AllowForcedSit: {clientPair.UserPairUniquePairPerms.AllowForcedSit}");
                        ImGui.Text($"IsForcedToSit: {clientPair.UserPairUniquePairPerms.IsForcedToSit}");
                        ImGui.Text($"AllowForcedToStay: {clientPair.UserPairUniquePairPerms.AllowForcedToStay}");
                        ImGui.Text($"IsForcedToStay: {clientPair.UserPairUniquePairPerms.IsForcedToStay}");
                        ImGui.Text($"AllowBlindfold: {clientPair.UserPairUniquePairPerms.AllowBlindfold}");
                        ImGui.Text($"ForceLockFirstPerson: {clientPair.UserPairUniquePairPerms.ForceLockFirstPerson}");
                        ImGui.Text($"IsBlindfolded: {clientPair.UserPairUniquePairPerms.IsBlindfolded}");
                    }
                }
                if (clientPair.UserPairEditAccess != null)
                {
                    if (ImGui.CollapsingHeader($"{clientPair.UserData.UID}'s Edit Access || {_serverConfigs.GetNicknameForUid(clientPair.UserData.UID)}"))
                    {
                        ImGui.Text("Live Chat Garbler Active Allowed: " + clientPair.UserPairEditAccess.LiveChatGarblerActiveAllowed);
                        ImGui.Text("Live Chat Garbler Locked Allowed: " + clientPair.UserPairEditAccess.LiveChatGarblerLockedAllowed);
                        ImGui.Text("Extended Lock Times Allowed: " + clientPair.UserPairEditAccess.ExtendedLockTimesAllowed);
                        ImGui.Text("Max Lock Time Allowed: " + clientPair.UserPairEditAccess.MaxLockTimeAllowed);
                        ImGui.Separator();
                        ImGui.Text("Wardrobe Enabled Allowed: " + clientPair.UserPairEditAccess.WardrobeEnabledAllowed);
                        ImGui.Text("Item Auto Equip Allowed: " + clientPair.UserPairEditAccess.ItemAutoEquipAllowed);
                        ImGui.Text("Restraint Set Auto Equip Allowed: " + clientPair.UserPairEditAccess.RestraintSetAutoEquipAllowed); ImGui.Text("Apply Restraint Sets Allowed: " + clientPair.UserPairEditAccess.ApplyRestraintSetsAllowed);
                        ImGui.Text("Lock Restraint Sets Allowed: " + clientPair.UserPairEditAccess.LockRestraintSetsAllowed);
                        ImGui.Text("Max Allowed Restraint Time Allowed: " + clientPair.UserPairEditAccess.MaxAllowedRestraintTimeAllowed);
                        ImGui.Text("Remove Restraint Sets Allowed: " + clientPair.UserPairEditAccess.RemoveRestraintSetsAllowed);
                        ImGui.Separator();
                        ImGui.Text("Puppeteer Enabled Allowed: " + clientPair.UserPairEditAccess.PuppeteerEnabledAllowed);
                        ImGui.Text("Allow Sit Requests Allowed: " + clientPair.UserPairEditAccess.AllowSitRequestsAllowed);
                        ImGui.Text("Allow Motion Requests Allowed: " + clientPair.UserPairEditAccess.AllowMotionRequestsAllowed);
                        ImGui.Text("Allow All Requests Allowed: " + clientPair.UserPairEditAccess.AllowAllRequestsAllowed);
                        ImGui.Separator();
                        ImGui.Text("Moodles Enabled Allowed: " + clientPair.UserPairEditAccess.MoodlesEnabledAllowed);
                        ImGui.Text("Allow Positive Status Types Allowed: " + clientPair.UserPairEditAccess.AllowPositiveStatusTypesAllowed);
                        ImGui.Text("Allow Negative Status Types Allowed: " + clientPair.UserPairEditAccess.AllowNegativeStatusTypesAllowed);
                        ImGui.Text("Allow Special Status Types Allowed: " + clientPair.UserPairEditAccess.AllowSpecialStatusTypesAllowed);
                        ImGui.Text("Pair Can Apply Own Moodles To You Allowed: " + clientPair.UserPairEditAccess.PairCanApplyOwnMoodlesToYouAllowed);
                        ImGui.Text("Pair Can Apply Your Moodles To You Allowed: " + clientPair.UserPairEditAccess.PairCanApplyYourMoodlesToYouAllowed);
                        ImGui.Text("Max Moodle Time Allowed: " + clientPair.UserPairEditAccess.MaxMoodleTimeAllowed);
                        ImGui.Text("Allow Permanent Moodles Allowed: " + clientPair.UserPairEditAccess.AllowPermanentMoodlesAllowed);
                        ImGui.Text("Allow Removing Moodles Allowed: " + clientPair.UserPairEditAccess.AllowRemovingMoodlesAllowed);
                        ImGui.Separator();
                        ImGui.Text("Toybox Enabled Allowed: " + clientPair.UserPairEditAccess.ToyboxEnabledAllowed);
                        ImGui.Text("Lock Toybox UI Allowed: " + clientPair.UserPairEditAccess.LockToyboxUIAllowed);
                        ImGui.Text("Spatial Vibrator Audio Allowed: " + clientPair.UserPairEditAccess.SpatialVibratorAudioAllowed);
                        ImGui.Text("Change Toy State Allowed: " + clientPair.UserPairEditAccess.CanToggleToyStateAllowed);
                        ImGui.Text("Can Use Realtime Vibe Remote Allowed: " + clientPair.UserPairEditAccess.CanUseVibeRemoteAllowed);
                        ImGui.Text("Can Toggle Alarms: " + clientPair.UserPairEditAccess.CanToggleAlarmsAllowed);
                        ImGui.Text("Can Send Alarms Allowed: " + clientPair.UserPairEditAccess.CanSendAlarmsAllowed);
                        ImGui.Text("Can Execute Patterns Allowed: " + clientPair.UserPairEditAccess.CanExecutePatternsAllowed);
                        ImGui.Text("Can Stop Patterns Allowed: "+ clientPair.UserPairEditAccess.CanStopPatternsAllowed);
                        ImGui.Text("Can Toggle Triggers Allowed: " + clientPair.UserPairEditAccess.CanToggleTriggersAllowed);
                    }
                }
                if (clientPair.LastReceivedAppearanceData != null)
                {
                    if (ImGui.CollapsingHeader($"{clientPair.UserData.UID}'s Appearance Data || {_serverConfigs.GetNicknameForUid(clientPair.UserData.UID)}"))
                    {
                        for (int i = 0; i < 3; i++)
                        {
                            ImGui.Text($"Slot{i}GagType: {clientPair.LastReceivedAppearanceData.GagSlots[i].GagType}");
                            ImGui.Text($"Slot{i}GagPadlock: {clientPair.LastReceivedAppearanceData.GagSlots[i].Padlock}");
                            ImGui.Text($"Slot{i}GagPassword: {clientPair.LastReceivedAppearanceData.GagSlots[i].Password}");
                            ImGui.Text($"Slot{i}GagTimer: {clientPair.LastReceivedAppearanceData.GagSlots[i].Timer}");
                            ImGui.Text($"Slot{i}GagAssigner: {clientPair.LastReceivedAppearanceData.GagSlots[i].Assigner}");
                            ImGui.Separator();
                        }
                    }
                }
                if (clientPair.LastReceivedWardrobeData != null)
                {
                    if (ImGui.CollapsingHeader($"{clientPair.UserData.UID}'s Wardrobe Data || {_serverConfigs.GetNicknameForUid(clientPair.UserData.UID)}"))
                    {
                        ImGui.Text($"OutfitList:");
                        ImGui.Indent();
                        foreach (var outfit in clientPair.LastReceivedWardrobeData.OutfitNames)
                        {
                            ImGui.Text($"{outfit}");
                        }
                        ImGui.Unindent();
                        ImGui.Text($"ActiveSetName: {clientPair.LastReceivedWardrobeData.ActiveSetName}");
                        ImGui.Text($"ActiveSetDescription: {clientPair.LastReceivedWardrobeData.ActiveSetDescription}");
                        ImGui.Text($"ActiveSetEnabledBy: {clientPair.LastReceivedWardrobeData.ActiveSetEnabledBy}");
                        ImGui.Text($"ActiveSetLockType: {clientPair.LastReceivedWardrobeData.Padlock}");
                        ImGui.Text($"ActiveSetLockPassword: {clientPair.LastReceivedWardrobeData.Password}");
                        ImGui.Text($"ActiveSetLockTime: {clientPair.LastReceivedWardrobeData.Timer}");
                        ImGui.Text($"ActiveSetLockedBy: {clientPair.LastReceivedWardrobeData.Assigner}");
                    }
                }
                if (clientPair.LastReceivedAliasData != null)
                {
                    if (ImGui.CollapsingHeader($"{clientPair.UserData.UID}'s Alias Data || {_serverConfigs.GetNicknameForUid(clientPair.UserData.UID)}"))
                    {
                        ImGui.Indent();
                        ImGui.Text($"Listening To: {clientPair.LastReceivedAliasData.CharacterName} @ {clientPair.LastReceivedAliasData.CharacterWorld}");
                        foreach (var alias in clientPair.LastReceivedAliasData.AliasList)
                        {
                            var tmptext = alias.Enabled ? "Enabled" : "Disabled";
                            ImGui.Text($"{tmptext} :: INPUT -> {alias.InputCommand}");
                            ImGui.Text($"OUTPUT -> {alias.OutputCommand}");
                        }
                        ImGui.Unindent();
                    }
                }
                if (clientPair.LastReceivedToyboxData != null)
                {
                    if (ImGui.CollapsingHeader($"{clientPair.UserData.UID}'s Pattern Data || {_serverConfigs.GetNicknameForUid(clientPair.UserData.UID)}"))
                    {
                        foreach (var pattern in clientPair.LastReceivedToyboxData.PatternList)
                        {
                            ImGui.Text("Identifier:" + pattern.Identifier);
                            ImGui.Text($"Pattern Name: {pattern.Name}");
                            ImGui.Text($"Pattern Description: {pattern.Description}");
                            ImGui.Text($"Pattern Duration: {pattern.Duration}");
                            ImGui.Text($"Pattern ShouldLoop: {pattern.ShouldLoop}");
                        }
                        ImGui.Text("ActivePatternGUID: " + clientPair.LastReceivedToyboxData.ActivePatternGuid);
                    }
                }

                if (clientPair.HasCachedPlayer)
                {
                    ImGui.Text($"OnlineUser UID: {clientPair.CachedPlayerOnlineDto.User.UID}");
                    ImGui.Text($"OnlineUser Alias: {clientPair.CachedPlayerOnlineDto.User.Alias}");
                    ImGui.Text($"OnlineUser Identifier: {clientPair.CachedPlayerOnlineDto.Ident}");
                    ImGui.Text($"HasCachedPlayer? : {clientPair.HasCachedPlayer}");
                    ImGui.Text(clientPair.CachedPlayerString());
                }
                else
                {
                    ImGui.Text("Player has no cached data");
                }
                ImGui.Unindent();
            }
        }
        // Note: Ensure that the _allClientPairs field in PairManager is accessible from SettingsUi.
        // You might need to adjust its access modifier or provide a public method/property to access it safely.
    }


    private void DrawGlobalInfo()
    {
        var globalPerms = _playerCharacterManager.GlobalPerms;
        if (globalPerms == null)
        {
            ImGui.Text("No global permissions available.");
            return;
        }
        ImGui.Text($"Safeword: {globalPerms.Safeword}");
        ImGui.Text($"SafewordUsed: {globalPerms.SafewordUsed}");
        ImGui.Text($"LiveChatGarblerActive: {globalPerms.LiveChatGarblerActive}");
        ImGui.Text($"LiveChatGarblerLocked: {globalPerms.LiveChatGarblerLocked}");
        ImGui.Separator();
        ImGui.Text($"WardrobeEnabled: {globalPerms.WardrobeEnabled}");
        ImGui.Text($"ItemAutoEquip: {globalPerms.ItemAutoEquip}");
        ImGui.Text($"RestraintSetAutoEquip: {globalPerms.RestraintSetAutoEquip}");
        ImGui.Separator();
        ImGui.Text($"PuppeteerEnabled: {globalPerms.PuppeteerEnabled}");
        ImGui.Text($"GlobalTriggerPhrase: {globalPerms.GlobalTriggerPhrase}");
        ImGui.Text($"GlobalAllowSitRequests: {globalPerms.GlobalAllowSitRequests}");
        ImGui.Text($"GlobalAllowMotionRequests: {globalPerms.GlobalAllowMotionRequests}");
        ImGui.Text($"GlobalAllowAllRequests: {globalPerms.GlobalAllowAllRequests}");
        ImGui.Separator();
        ImGui.Text($"MoodlesEnabled: {globalPerms.MoodlesEnabled}");
        ImGui.Separator();
        ImGui.Text($"ToyboxEnabled: {globalPerms.ToyboxEnabled}");
        ImGui.Text($"LockToyboxUI: {globalPerms.LockToyboxUI}");
        ImGui.Text($"ToyIsActive: {globalPerms.ToyIsActive}");
        ImGui.Text($"ToyIntensity: {globalPerms.ToyIntensity}");
        ImGui.Text($"SpatialVibratorAudio: {globalPerms.SpatialVibratorAudio}");
    }

    private void DrawAppearanceInfo()
    {
        var appearanceData = _playerCharacterManager.AppearanceData;
        if (appearanceData == null)
        {
            ImGui.Text("No appearance data available.");
            return;
        }
        for (int i = 0; i < 3; i++)
        {
            ImGui.Text($"Slot{i}GagType: {appearanceData!.GagSlots[i].GagType}");
            ImGui.Text($"Slot{i}GagPadlock: {appearanceData.GagSlots[i].Padlock}");
            ImGui.Text($"Slot{i}GagPassword: {appearanceData.GagSlots[i].Password}");
            ImGui.Text($"Slot{i}GagTimer: {appearanceData.GagSlots[i].Timer}");
            ImGui.Text($"Slot{i}GagAssigner: {appearanceData.GagSlots[i].Assigner}");
            ImGui.Separator();
        }
    }

    /// <summary>
    /// The actual function that draws the content of the settings window.
    /// </summary>
    private void DrawSettingsContent()
    {
        // check the current server state. If it is connected.
        if (ApiController.ServerState is ServerState.Connected)
        {
            // display the Server name, that it is available, and the number of users online.
            ImGui.TextUnformatted("Server is " + _serverConfigs.CurrentServer!.ServerName + ":");
            ImGui.SameLine();
            ImGui.TextColored(ImGuiColors.ParsedGreen, "Available");
            ImGui.SameLine();
            ImGui.TextUnformatted("(");
            ImGui.SameLine();
            ImGui.TextColored(ImGuiColors.ParsedGreen, ApiController.OnlineUsers.ToString(CultureInfo.InvariantCulture));
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

        if (ApiController.ServerState is ServerState.Connected)
        {
            // draw out the tab bar for us.
            if (ImGui.BeginTabBar("mainTabBar"))
            {
                if (ImGui.BeginTabItem("Global"))
                {
                    DrawGlobalSettings();
                    ImGui.EndTabItem();
                }
                if (ImGui.BeginTabItem("Hardcore"))
                {
                    _lastTab = "Hardcore";

                    _hardcoreSettingsUI.DrawHardcoreSettings();
                    ImGui.EndTabItem();
                }
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

                if (ImGui.BeginTabItem("Player Debug"))
                {
                    DrawPlayerCharacterDebug();
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("Pairs Debug"))
                {
                    DrawPairsDebug();
                    ImGui.EndTabItem();
                }

                ImGui.EndTabBar();
            }
        }
        else
        {
            if (ImGui.BeginTabBar("mainTabBar"))
            {
                if (ImGui.BeginTabItem("Account Management"))
                {
                    DrawAccountManagement();
                    ImGui.EndTabItem();
                }
                ImGui.EndTabBar();
            }
        }
    }
}
