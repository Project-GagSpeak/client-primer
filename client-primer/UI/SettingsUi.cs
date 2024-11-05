using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Utility;
using GagSpeak.ChatMessages;
using GagSpeak.GagspeakConfiguration;
using GagSpeak.GagspeakConfiguration.Models;
using GagSpeak.Interop.Ipc;
using GagSpeak.PlayerData.Data;
using GagSpeak.PlayerData.Pairs;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using GagSpeak.UpdateMonitoring;
using GagSpeak.UpdateMonitoring.SpatialAudio.Managers;
using GagSpeak.UpdateMonitoring.SpatialAudio.Spawner;
using GagSpeak.Utils;
using GagSpeak.WebAPI;
using GagspeakAPI.Data.Character;
using GagspeakAPI.Data.Permissions;
using GagspeakAPI.Dto.Permissions;
using ImGuiNET;
using OtterGui.Text;
using Penumbra.GameData.Enums;
using System.Globalization;
using System.Numerics;

namespace GagSpeak.UI;

public class SettingsUi : WindowMediatorSubscriberBase
{
    private readonly MainHub _apiHubMain;
    private readonly PlayerCharacterData _playerCharacterManager;
    private readonly IpcManager _ipcManager;
    private readonly OnFrameworkService _frameworkUtil;
    private readonly GagspeakConfigService _configService;
    private readonly PairManager _pairManager;
    private readonly ClientConfigurationManager _clientConfigs;
    private readonly ServerConfigurationManager _serverConfigs;
    private readonly SettingsHardcore _hardcoreSettingsUI;
    private readonly UiSharedService _uiShared;
    private readonly PiShockProvider _shockProvider;
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
        MainHub apiHubMain, GagspeakConfigService configService,
        PairManager pairManager, PlayerCharacterData playerCharacterManager,
        ClientConfigurationManager clientConfigs, PiShockProvider shockProvider,
        AvfxManager avfxManager, VfxSpawns vfxSpawns, ServerConfigurationManager serverConfigs,
        GagspeakMediator mediator, IpcManager ipcManager, SettingsHardcore hardcoreSettingsUI,
        OnFrameworkService frameworkUtil) : base(logger, mediator, "GagSpeak Settings")
    {
        _apiHubMain = apiHubMain;
        _playerCharacterManager = playerCharacterManager;
        _configService = configService;
        _pairManager = pairManager;
        _clientConfigs = clientConfigs;
        _shockProvider = shockProvider;
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

        Mediator.Subscribe<CharacterIpcDataCreatedMessage>(this, (msg) => LastCreatedCharacterData = msg.CharaIPCData);
        Mediator.Subscribe<OpenSettingsUiMessage>(this, (_) => Toggle());
        Mediator.Subscribe<SwitchToIntroUiMessage>(this, (_) => IsOpen = false);
    }

    public CharaIPCData? LastCreatedCharacterData { private get; set; }
    private UserGlobalPermissions PlayerGlobalPerms => _playerCharacterManager.GlobalPerms!;

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
        bool cursedDungeonLoot = _clientConfigs.GagspeakConfig.CursedDungeonLoot;
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
        bool allowGlobalShockShockCollar = PlayerGlobalPerms.AllowShocks;
        bool allowGlobalVibrateShockCollar = PlayerGlobalPerms.AllowVibrations;
        bool allowGlobalBeepShockCollar = PlayerGlobalPerms.AllowBeeps;
        int maxGlobalShockCollarIntensity = PlayerGlobalPerms.MaxIntensity;
        TimeSpan maxGlobalShockDuration = PlayerGlobalPerms.GetTimespanFromDuration();
        int maxGlobalVibrateDuration = (int)PlayerGlobalPerms.GlobalShockVibrateDuration.TotalSeconds;

        _uiShared.BigText("Gags");
        using (ImRaii.Disabled(liveChatGarblerLocked))
        {
            if (ImGui.Checkbox("Enable Live Chat Garbler", ref liveChatGarblerActive))
            {
                // Perform a mediator call that we have updated a permission.
                _ = _apiHubMain.UserUpdateOwnGlobalPerm(new UserGlobalPermChangeDto(MainHub.PlayerUserData,
                    new KeyValuePair<string, object>("LiveChatGarblerActive", liveChatGarblerActive), MainHub.PlayerUserData));

            }
            _uiShared.DrawHelpText("If enabled, the Live Chat Garbler will garble your chat messages in-game. (This is done server-side, others will see it too)");
        }

        if (ImGui.Checkbox("Allow Gag Glamour's", ref itemAutoEquip))
        {
            PlayerGlobalPerms.ItemAutoEquip = itemAutoEquip;
            // if this creates a race condition down the line remove the above line.
            _ = _apiHubMain.UserUpdateOwnGlobalPerm(new UserGlobalPermChangeDto(MainHub.PlayerUserData,
            new KeyValuePair<string, object>("ItemAutoEquip", itemAutoEquip), MainHub.PlayerUserData));
            // perform recalculations to our cache.
            Mediator.Publish(new AppearanceImpactingSettingChanged());
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
            _ = _apiHubMain.UserUpdateOwnGlobalPerm(new UserGlobalPermChangeDto(MainHub.PlayerUserData,
            new KeyValuePair<string, object>("WardrobeEnabled", wardrobeEnabled), MainHub.PlayerUserData));

        }
        _uiShared.DrawHelpText("If enabled, the all glamourer / penumbra / visual display information will become functional.");

        using (ImRaii.Disabled(!wardrobeEnabled))
        {
            if (ImGui.Checkbox("Allow Restraint Sets to be Auto-Equipped", ref restraintSetAutoEquip))
            {
                PlayerGlobalPerms.RestraintSetAutoEquip = restraintSetAutoEquip;
                // if this creates a race condition down the line remove the above line.
                _ = _apiHubMain.UserUpdateOwnGlobalPerm(new UserGlobalPermChangeDto(MainHub.PlayerUserData,
                new KeyValuePair<string, object>("RestraintSetAutoEquip", restraintSetAutoEquip), MainHub.PlayerUserData));
                // perform recalculations to our cache.
                Mediator.Publish(new AppearanceImpactingSettingChanged());
            }
            _uiShared.DrawHelpText("Allows Glamourer to bind restraint sets to your character.\nRestraint sets can be created in the Wardrobe Interface.");

            if (ImGui.Checkbox("Disable Restraint's When Locks Expire", ref restraintSetDisableWhenUnlocked))
            {
                _clientConfigs.GagspeakConfig.DisableSetUponUnlock = restraintSetDisableWhenUnlocked;
                _clientConfigs.Save();
            }
            _uiShared.DrawHelpText("Let's the Active Restraint Set that is locked be automatically disabled when it's lock expires.");

            if (ImGui.Checkbox("Enable Cursed Dungeon Loot", ref cursedDungeonLoot))
            {
                _clientConfigs.GagspeakConfig.CursedDungeonLoot = cursedDungeonLoot;
                _clientConfigs.Save();
            }
            _uiShared.DrawHelpText("Provide the Cursed Loot Component with a list of sets to randomly apply." + Environment.NewLine
                + "When opening Dungeon Chests, there is a random chance to apply & lock a set." + Environment.NewLine
                + "Mimic Timer Locks are set in your defined range, and CANNOT be unlocked.");


            if (ImGui.Checkbox("Enable Moodles", ref moodlesEnabled))
            {
                PlayerGlobalPerms.MoodlesEnabled = moodlesEnabled;
                // if this creates a race condition down the line remove the above line.
                _ = _apiHubMain.UserUpdateOwnGlobalPerm(new UserGlobalPermChangeDto(MainHub.PlayerUserData,
                new KeyValuePair<string, object>("MoodlesEnabled", moodlesEnabled), MainHub.PlayerUserData));
                // perform recalculations to our cache.
                Mediator.Publish(new AppearanceImpactingSettingChanged());

            }
            _uiShared.DrawHelpText("If enabled, the moodles component will become functional.");
        }

        // draw out revert style selection
        ImGui.Spacing();
        ImGui.TextUnformatted("On Safeword/Restraint Disable:");
        // Draw radio buttons for each RevertStyle enum value
        foreach (RevertStyle style in Enum.GetValues(typeof(RevertStyle)))
        {
            string label = (style is RevertStyle.RevertToGame or RevertStyle.RevertToAutomation) ? "Revert" : "Reapply";

            bool isSelected = _clientConfigs.GagspeakConfig.RevertStyle == style;
            if (ImGui.RadioButton(label + "##" + style.ToString(), isSelected))
            {
                _clientConfigs.GagspeakConfig.RevertStyle = style;
                _clientConfigs.Save();
            }
            ImUtf8.SameLineInner();
            ImGui.TextUnformatted(style.ToName());
            _uiShared.DrawHelpText(style.ToHelpText());
        }


        ImGui.Separator();
        _uiShared.BigText("Puppeteer");

        if (ImGui.Checkbox("Enable Puppeteer", ref puppeteerEnabled))
        {
            PlayerGlobalPerms.PuppeteerEnabled = puppeteerEnabled;
            // if this creates a race condition down the line remove the above line.
            _ = _apiHubMain.UserUpdateOwnGlobalPerm(new UserGlobalPermChangeDto(MainHub.PlayerUserData,
            new KeyValuePair<string, object>("PuppeteerEnabled", puppeteerEnabled), MainHub.PlayerUserData));

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
                _ = _apiHubMain.UserUpdateOwnGlobalPerm(new UserGlobalPermChangeDto(MainHub.PlayerUserData,
                new KeyValuePair<string, object>("GlobalTriggerPhrase", globalTriggerPhrase), MainHub.PlayerUserData));

            }
            _uiShared.DrawHelpText("The global trigger phrase that will be used to trigger puppeteer commands.\n" +
                "LEAVE THIS FIELD BLANK TO HAVE NO GLOBAL TRIGGER PHRASE");

            if (ImGui.Checkbox("Globally Allow Sit Requests", ref globalAllowSitRequests))
            {
                PlayerGlobalPerms.GlobalAllowSitRequests = globalAllowSitRequests;
                // if this creates a race condition down the line remove the above line.
                _ = _apiHubMain.UserUpdateOwnGlobalPerm(new UserGlobalPermChangeDto(MainHub.PlayerUserData,
                new KeyValuePair<string, object>("GlobalAllowSitRequests", globalAllowSitRequests), MainHub.PlayerUserData));

            }
            _uiShared.DrawHelpText("If enabled, the user will allow sit requests to be sent to them.");

            if (ImGui.Checkbox("Globally Allow Motion Requests", ref globalAllowMotionRequests))
            {
                PlayerGlobalPerms.GlobalAllowMotionRequests = globalAllowMotionRequests;
                // if this creates a race condition down the line remove the above line.
                _ = _apiHubMain.UserUpdateOwnGlobalPerm(new UserGlobalPermChangeDto(MainHub.PlayerUserData,
                new KeyValuePair<string, object>("GlobalAllowMotionRequests", globalAllowMotionRequests), MainHub.PlayerUserData));

            }
            _uiShared.DrawHelpText("If enabled, the user will allow motion requests to be sent to them.");

            if (ImGui.Checkbox("Globally Allow All Requests", ref globalAllowAllRequests))
            {
                PlayerGlobalPerms.GlobalAllowAllRequests = globalAllowAllRequests;
                // if this creates a race condition down the line remove the above line.
                _ = _apiHubMain.UserUpdateOwnGlobalPerm(new UserGlobalPermChangeDto(MainHub.PlayerUserData,
                new KeyValuePair<string, object>("GlobalAllowAllRequests", globalAllowAllRequests), MainHub.PlayerUserData));

            }
            _uiShared.DrawHelpText("If enabled, the user will allow all requests to be sent to them.");
        }


        ImGui.Separator();
        _uiShared.BigText("Toybox");

        if (ImGui.Checkbox("Enable Toybox", ref toyboxEnabled))
        {
            PlayerGlobalPerms.ToyboxEnabled = toyboxEnabled;
            // if this creates a race condition down the line remove the above line.
            _ = _apiHubMain.UserUpdateOwnGlobalPerm(new UserGlobalPermChangeDto(MainHub.PlayerUserData,
            new KeyValuePair<string, object>("ToyboxEnabled", toyboxEnabled), MainHub.PlayerUserData));

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
            _ = _apiHubMain.UserUpdateOwnGlobalPerm(new UserGlobalPermChangeDto(MainHub.PlayerUserData,
            new KeyValuePair<string, object>("SpatialVibratorAudio", spatialVibratorAudio), MainHub.PlayerUserData));
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

            _ = _apiHubMain.UserUpdateOwnGlobalPerm(new(MainHub.PlayerUserData,
            new KeyValuePair<string, object>("GlobalShockShareCode", globalShockCollarShareCode), MainHub.PlayerUserData));
        }
        ImUtf8.SameLineInner();
        if (_uiShared.IconTextButton(FontAwesomeIcon.Sync, "Refresh", null, false, DateTime.UtcNow - _lastRefresh < TimeSpan.FromSeconds(5)))
        {
            _lastRefresh = DateTime.UtcNow;
            // Send Mediator Event to grab updated settings for pair.
            Task.Run(async () =>
            {
                if (_playerCharacterManager.CoreDataNull) return;
                var newPerms = await _shockProvider.GetPermissionsFromCode(_playerCharacterManager.GlobalPerms!.GlobalShockShareCode);
                // set the new permissions.
                _playerCharacterManager.GlobalPerms.AllowShocks = newPerms.AllowShocks;
                _playerCharacterManager.GlobalPerms.AllowVibrations = newPerms.AllowVibrations;
                _playerCharacterManager.GlobalPerms.AllowBeeps = newPerms.AllowBeeps;
                _playerCharacterManager.GlobalPerms.MaxDuration = newPerms.MaxDuration;
                _playerCharacterManager.GlobalPerms.MaxIntensity = newPerms.MaxIntensity;
                // update the permissions.
                _ = _apiHubMain.UserPushAllGlobalPerms(new(MainHub.PlayerUserData, _playerCharacterManager.GlobalPerms));
            });
        }
        UiSharedService.AttachToolTip("Forces Global PiShock Share Code to grab latest data from the API and push it to other online pairs.");
        ImUtf8.SameLineInner();
        ImGui.TextUnformatted("PiShock Global Share Code");
        _uiShared.DrawHelpText("Global PiShock Share Code used for your connected ShockCollar." + Environment.NewLine + "NOTE:" + Environment.NewLine
            + "While this is a GLOBAL share code, only people you are in Hardcore mode with will have access to it.");

        ImGui.SetNextItemWidth(250 * ImGuiHelpers.GlobalScale);
        if (ImGui.SliderInt("Global Max Vibration Time", ref maxGlobalVibrateDuration, 0, 30))
        {
            PlayerGlobalPerms.GlobalShockVibrateDuration = TimeSpan.FromSeconds(maxGlobalVibrateDuration);
        }
        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            // Convert TimeSpan to ticks and send as UInt64
            ulong ticks = (ulong)PlayerGlobalPerms.GlobalShockVibrateDuration.Ticks;
            _ = _apiHubMain.UserUpdateOwnGlobalPerm(new(MainHub.PlayerUserData,
            new KeyValuePair<string, object>("GlobalShockVibrateDuration", ticks), MainHub.PlayerUserData));
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
            UiSharedService.ColorText(maxGlobalShockDuration.Seconds.ToString() + "." + maxGlobalShockDuration.Milliseconds.ToString() + "s", ImGuiColors.ParsedGold);
        }
    }
    private DateTime _lastRefresh = DateTime.MinValue;

    private void DrawPreferences()
    {
        _lastTab = "Preferences";

        // change the column count to 2.
        float width = ImGui.GetContentRegionAvail().X / 2;
        ImGui.Columns(2, "PreferencesColumns", true);
        ImGui.SetColumnWidth(0, width);
        // go to first column.
        _uiShared.BigText("Live Chat Garbler");
        using (ImRaii.Group())
        {
            // display the channels
            var i = 0;
            foreach (var e in ChatChannel.GetOrderedChannels())
            {
                // See if it is already enabled by default
                var enabled = _configService.Current.ChannelsGagSpeak.Contains(e);
                // Create a new line after every 4 columns
                if (i != 0 && (i == 4 || i == 7 || i == 11 || i == 15 || i == 19))
                    ImGui.NewLine();

                if (ImGui.Checkbox($"{e}", ref enabled))
                {
                    if (enabled)
                    {
                        if (!_configService.Current.ChannelsGagSpeak.Contains(e))
                            _configService.Current.ChannelsGagSpeak.Add(e);
                    }
                    else
                    {
                        _configService.Current.ChannelsGagSpeak.Remove(e);
                    }
                    _configService.Save();
                }

                ImGui.SameLine();
                i++;
            }

            ImGui.NewLine();
            ImGui.AlignTextToFramePadding();
            ImGui.Text("Language & Dialect:");
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
                        ImGui.SetItemDefaultFocus();
                }
                ImGui.EndCombo();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Select the language you want to use for GagSpeak.");
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
                        _activeDialect = dialect;
                    if (isSelected)
                        ImGui.SetItemDefaultFocus();
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
        }

        ImGui.NextColumn();
        _uiShared.BigText("Puppeteer Channels");
        using (ImRaii.Group())
        {
            // display the channels
            var j = 0;
            foreach (var e in ChatChannel.GetOrderedChannels())
            {
                // See if it is already enabled by default
                var enabled = _configService.Current.ChannelsPuppeteer.Contains(e);

                // Create a new line after every 4 columns
                if (j != 0 && (j == 4 || j == 7 || j == 11 || j == 15 || j == 19))
                    ImGui.NewLine();

                if (ImGui.Checkbox($"{e}##{e}puppeteer", ref enabled))
                {
                    if (enabled)
                    {
                        if (!_configService.Current.ChannelsPuppeteer.Contains(e))
                            _configService.Current.ChannelsPuppeteer.Add(e);
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
        }

        // reset to 1 column.
        ImGui.Columns(1);

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
        var showContextMenus = _configService.Current.ContextMenusShow;

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
        if (ImGui.SliderFloat("Hover Delay", ref profileDelay, 0.3f, 5))
        {
            _configService.Current.ProfileDelay = profileDelay;
            _configService.Save();
        }
        _uiShared.DrawHelpText("Delay until the profile should be displayed");
        if (!showProfiles) ImGui.EndDisabled();
        ImGui.Unindent();

        if (ImGui.Checkbox("Show Context Menus for Visible Pairs", ref showContextMenus))
        {
            _configService.Current.ContextMenusShow = showContextMenus;
            _configService.Save();
        }
        _uiShared.DrawHelpText("If enabled, you will be able to right-click on visible pairs to access a context menu.");

        /* --------------- Separator for moving onto the Notifications Section ----------- */
        ImGui.Separator();
        var onlineNotifs = _configService.Current.ShowOnlineNotifications;
        var onlineNotifsPairsOnly = _configService.Current.ShowOnlineNotificationsOnlyForIndividualPairs;
        var onlineNotifsNamedOnly = _configService.Current.ShowOnlineNotificationsOnlyForNamedPairs;
        var liveGarblerZoneChangeWarn = _configService.Current.LiveGarblerZoneChangeWarn;

        _uiShared.BigText("Notifications");

        if (ImGui.Checkbox("Warn User if Live Chat Garbler is still active on Zone Change", ref liveGarblerZoneChangeWarn))
        {
            _configService.Current.LiveGarblerZoneChangeWarn = liveGarblerZoneChangeWarn;
            _configService.Save();
        }
        _uiShared.DrawHelpText("Displays a notification to you if you change zones while your live garbler is still active." +
            Environment.NewLine + "Helpful for preventing any accidental muffled statements in unwanted chats~");

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
        using var child = ImRaii.Child($"##AuthAccountListing" + idx + account.CharacterPlayerContentId, new Vector2(ImGui.GetContentRegionAvail().X, height), true, ImGuiWindowFlags.ChildWindow);
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
            (!(KeyMonitor.CtrlPressed() && KeyMonitor.ShiftPressed()) || !(MainHub.IsServerAlive && MainHub.IsConnected && isOnlineUser)),
            "##Trash-" + account.CharacterPlayerContentId.ToString()))
            {
                _deleteAccountPopupModalShown = true;
                ImGui.OpenPopup("Delete your account?");
            }
            UiSharedService.AttachToolTip("Permanently remove the use of this secret key and registered account for this character." + Environment.NewLine
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
            float rightEnd = ImGui.GetContentRegionAvail().X - successfulConnection.X - isPrimaryIcon.X - 2 * ImGui.GetStyle().ItemInnerSpacing.X;
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
            if (ImGui.IsItemClicked())
            {
                ShowKeyLabel = !ShowKeyLabel;
            }
            UiSharedService.AttachToolTip("Secret Key for this account. (Insert by clicking the edit pen icon)");
            // we shoul draw an inputtext field here if we can edit it, and a text field if we cant.
            if (EditingIdx == idx)
            {
                ImUtf8.SameLineInner();
                ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - _uiShared.GetIconButtonSize(FontAwesomeIcon.PenSquare).X - ImGui.GetStyle().ItemSpacing.X);
                string key = account.SecretKey.Key;
                if (ImGui.InputTextWithHint("##SecondaryAuthKey" + account.CharacterPlayerContentId, "Paste Secret Key Here...", ref key, 64, ImGuiInputTextFlags.EnterReturnsTrue))
                {
                    _logger.LogInformation("This would have updated the secret key!");
                    if (account.SecretKey.Label.IsNullOrEmpty())
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

            if (idx != int.MaxValue)
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
            UiSharedService.TextWrapped("Be Deleting your primary GagSpeak account, all secondary users below will also be deleted.");
            UiSharedService.TextWrapped("Your UID will be removed from all pairing lists.");
            ImGui.TextUnformatted("Are you sure you want to continue?");
            ImGui.Separator();
            ImGui.Spacing();

            var buttonSize = (ImGui.GetWindowContentRegionMax().X - ImGui.GetWindowContentRegionMin().X -
                              ImGui.GetStyle().ItemSpacing.X) / 2;

            if (ImGui.Button("Delete account", new Vector2(buttonSize, 0)))
            {
                _ = Task.Run(_apiHubMain.UserDelete);
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
    private static readonly Dictionary<string, LoggerType[]> loggerSections = new Dictionary<string, LoggerType[]>
    {
        { "Main", new[] { LoggerType.Achievements, LoggerType.Mediator, LoggerType.GarblerCore } },
        { "Interop", new[] { LoggerType.IpcGagSpeak, LoggerType.IpcCustomize, LoggerType.IpcGlamourer, LoggerType.IpcMare, LoggerType.IpcMoodles, LoggerType.IpcPenumbra, LoggerType.Appearance } },
        { "Hardcore", new[] { LoggerType.HardcoreActions, LoggerType.HardcoreMovement, LoggerType.HardcorePrompt } },
        { "Player Data", new[] { LoggerType.GagManagement, LoggerType.PadlockManagement, LoggerType.ClientPlayerData, LoggerType.GameObjects, LoggerType.PairManagement, LoggerType.OnlinePairs, LoggerType.VisiblePairs, LoggerType.Restraints, LoggerType.Puppeteer } },
        { "Services", new[] { LoggerType.Notification, LoggerType.Profiles, LoggerType.Cosmetics, LoggerType.GlobalChat, LoggerType.ContextDtr, LoggerType.PatternHub, LoggerType.Safeword, LoggerType.CursedLoot } },
        { "Toybox", new[] { LoggerType.ToyboxDevices, LoggerType.ToyboxPatterns, LoggerType.ToyboxTriggers, LoggerType.ToyboxAlarms, LoggerType.VibeControl, LoggerType.PrivateRoom } },
        { "Update Monitoring", new[] { LoggerType.ChatDetours, LoggerType.ActionEffects, LoggerType.SpatialAudioController, LoggerType.SpatialAudioLogger } },
        { "UI", new[] { LoggerType.UiCore, LoggerType.UserPairDrawer, LoggerType.Permissions, LoggerType.Simulation } },
        { "WebAPI", new[] { LoggerType.PiShock, LoggerType.ApiCore, LoggerType.Callbacks, LoggerType.Health, LoggerType.HubFactory, LoggerType.JwtTokens, LoggerType.Textures } }
    };

    private void DrawLoggerSettings()
    {
        bool isFirstSection = true;

        // Iterate through each section in loggerSections
        foreach (var section in loggerSections)
        {
            // Begin a new group for the section
            using (ImRaii.Group())
            {
                // Calculate the number of checkboxes in the current section
                var checkboxes = section.Value;

                // Draw a custom line above the table to simulate the upper border
                var drawList = ImGui.GetWindowDrawList();
                var cursorPos = ImGui.GetCursorScreenPos();
                drawList.AddLine(new Vector2(cursorPos.X, cursorPos.Y), new Vector2(cursorPos.X + ImGui.GetContentRegionAvail().X, cursorPos.Y), ImGui.GetColorU32(ImGuiCol.Border));

                // Add some vertical spacing to position the table correctly
                ImGui.Dummy(new Vector2(0, 1));

                // Begin a new table for the checkboxes without any borders
                if (ImGui.BeginTable(section.Key, 4, ImGuiTableFlags.None))
                {
                    // Iterate through the checkboxes, managing columns and rows
                    for (int i = 0; i < checkboxes.Length; i++)
                    {
                        ImGui.TableNextColumn();

                        bool isEnabled = _configService.Current.LoggerFilters.Contains(checkboxes[i]);

                        if (ImGui.Checkbox(checkboxes[i].ToName(), ref isEnabled))
                        {
                            if (isEnabled)
                            {
                                _configService.Current.LoggerFilters.Add(checkboxes[i]);
                                LoggerFilter.AddAllowedCategory(checkboxes[i]);
                            }
                            else
                            {
                                _configService.Current.LoggerFilters.Remove(checkboxes[i]);
                                LoggerFilter.RemoveAllowedCategory(checkboxes[i]);
                            }
                            _configService.Save();
                        }
                    }

                    // Add "All On" and "All Off" buttons for the first section
                    if (isFirstSection)
                    {
                        ImGui.TableNextColumn();
                        if (ImGui.Button("All On"))
                        {
                            _configService.Current.LoggerFilters = LoggerFilter.GetAllRecommendedFilters();
                            _configService.Save();
                            LoggerFilter.AddAllowedCategories(_configService.Current.LoggerFilters);
                        }
                        ImUtf8.SameLineInner();
                        if (ImGui.Button("All Off"))
                        {
                            _configService.Current.LoggerFilters.Clear();
                            _configService.Current.LoggerFilters.Add(LoggerType.None);
                            _configService.Save();
                            LoggerFilter.ClearAllowedCategories();
                        }
                    }

                    ImGui.EndTable();
                }

                // Display a tooltip when hovering over any element in the group
                if (ImGui.IsItemHovered(ImGuiHoveredFlags.RectOnly))
                {
                    ImGui.BeginTooltip();
                    UiSharedService.ColorText(section.Key, ImGuiColors.ParsedGold);
                    ImGui.EndTooltip();
                }
            }

            // Mark that the first section has been processed
            isFirstSection = false;
        }

        // Ensure LoggerType.None is always included in the filtered categories
        if (!_configService.Current.LoggerFilters.Contains(LoggerType.None))
        {
            _configService.Current.LoggerFilters.Add(LoggerType.None);
            LoggerFilter.AddAllowedCategory(LoggerType.None);
        }
    }
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

        var logFilters = _configService.Current.LoggerFilters;

        // draw a collapsible tree node here to draw the logger settings:
        ImGui.Spacing();
        if (ImGui.TreeNode("Advanced Logger Filters (Only Edit if you know what you're doing!)"))
        {
            DrawLoggerSettings();
            ImGui.TreePop();
        }

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
                        ImGui.Text($"ForcedFollow: {clientPair.UserPairGlobalPerms.ForcedFollow}");
                        ImGui.Text($"ForcedEmoteState: {clientPair.UserPairGlobalPerms.ForcedEmoteState}");
                        ImGui.Text($"ForcedToStay: {clientPair.UserPairGlobalPerms.ForcedStay}");
                        ImGui.Text($"Blindfold: {clientPair.UserPairGlobalPerms.ForcedBlindfold}");
                        ImGui.Text($"HiddenChat: {clientPair.UserPairGlobalPerms.ChatBoxesHidden}");
                        ImGui.Text($"HiddenChatInput: {clientPair.UserPairGlobalPerms.ChatInputHidden}");
                        ImGui.Text($"BlockingChatInput: {clientPair.UserPairGlobalPerms.ChatInputBlocked}");
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
                        ImGui.Text($"AllowForcedSit: {clientPair.UserPairUniquePairPerms.AllowForcedSit}");
                        ImGui.Text($"AllowForcedEmoteState: {clientPair.UserPairUniquePairPerms.AllowForcedEmote}");
                        ImGui.Text($"AllowForcedToStay: {clientPair.UserPairUniquePairPerms.AllowForcedToStay}");
                        ImGui.Text($"AllowBlindfold: {clientPair.UserPairUniquePairPerms.AllowBlindfold}");
                        ImGui.Text($"AllowHiddenChat: {clientPair.UserPairUniquePairPerms.AllowHidingChatBoxes}");
                        ImGui.Text($"AllowHiddenChatInput: {clientPair.UserPairUniquePairPerms.AllowHidingChatInput}");
                        ImGui.Text($"AllowBlockingChatInput: {clientPair.UserPairUniquePairPerms.AllowChatInputBlocking}");
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
                        ImGui.Text("Can Stop Patterns Allowed: " + clientPair.UserPairEditAccess.CanStopPatternsAllowed);
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
                        ImGui.Text($"ActiveSetId: {clientPair.LastReceivedWardrobeData.ActiveSetId}");
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
                if (clientPair.LastReceivedLightStorage is not null)
                {
                    if (ImGui.CollapsingHeader($"{clientPair.UserData.UID}'s Light Storage Data || {_serverConfigs.GetNicknameForUid(clientPair.UserData.UID)}"))
                    {
                        ImGui.Text("Gags with Glamour's:");
                        ImGui.Indent();
                        foreach (var gag in clientPair.LastReceivedLightStorage.GagItems)
                            ImGui.Text($"Gag with Glamour: {gag.Key}");
                        ImGui.Unindent();
                        ImGui.Spacing();
                        ImGui.Text("Restraints:");
                        ImGui.Indent();
                        foreach (var restraint in clientPair.LastReceivedLightStorage.Restraints)
                            ImGui.Text("[Identifier: " + restraint.Identifier + "] [Name: " + restraint.Name + "] [AffectedSlots: " + restraint.AffectedSlots.Count + "]");
                        ImGui.Unindent();
                        ImGui.Spacing();
                        ImGui.Text("Blindfold Item Slot: " + (EquipSlot)clientPair.LastReceivedLightStorage.BlindfoldItem.Slot);
                        ImGui.Spacing();
                        ImGui.Text("Cursed Items: ");
                        ImGui.Indent();
                        foreach (var cursedItem in clientPair.LastReceivedLightStorage.CursedItems)
                            ImGui.Text("[Identifier: " + cursedItem.Identifier + "] [Name: " + cursedItem.Name + "]");
                        ImGui.Unindent();
                        ImGui.Spacing();
                        ImGui.Text("Patterns: ");
                        ImGui.Indent();
                        foreach (var pattern in clientPair.LastReceivedLightStorage.Patterns)
                            ImGui.Text("[Identifier: " + pattern.Identifier + "] [Name: " + pattern.Name + "]");
                        ImGui.Unindent();
                        ImGui.Spacing();
                        ImGui.Text("Alarms: ");
                        ImGui.Indent();
                        foreach (var alarm in clientPair.LastReceivedLightStorage.Alarms)
                            ImGui.Text("[Identifier: " + alarm.Identifier + "] [Name: " + alarm.Name + "]");
                        ImGui.Unindent();
                        ImGui.Spacing();
                        ImGui.Text("Triggers: ");
                        ImGui.Indent();
                        foreach (var trigger in clientPair.LastReceivedLightStorage.Triggers)
                            ImGui.Text("[Identifier: " + trigger.Identifier + "] [Name: " + trigger.Name + "]");
                        ImGui.Unindent();
                    }
                }
            }
        }
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
        ImGui.Text($"ForcedFollow: {globalPerms.ForcedFollow}");
        ImGui.Text($"ForcedEmoteState: {globalPerms.ForcedEmoteState}");
        ImGui.Text($"ForcedToStay: {globalPerms.ForcedStay}");
        ImGui.Text($"Blindfold: {globalPerms.ForcedBlindfold}");
        ImGui.Text($"HiddenChat: {globalPerms.ChatBoxesHidden}");
        ImGui.Text($"HiddenChatInput: {globalPerms.ChatInputHidden}");
        ImGui.Text($"BlockingChatInput: {globalPerms.ChatInputBlocked}");
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
        if (MainHub.IsConnected)
        {
            // display the Server name, that it is available, and the number of users online.
            ImGui.TextUnformatted("Server is " + _serverConfigs.CurrentServer!.ServerName + ":");
            ImGui.SameLine();
            ImGui.TextColored(ImGuiColors.ParsedGreen, "Available");
            ImGui.SameLine();
            ImGui.TextUnformatted("(");
            ImGui.SameLine();
            ImGui.TextColored(ImGuiColors.ParsedGreen, MainHub.MainOnlineUsers.ToString(CultureInfo.InvariantCulture));
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

        if (MainHub.IsConnected)
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
