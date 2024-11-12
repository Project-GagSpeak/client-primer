using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Utility;
using GagSpeak.ChatMessages;
using GagSpeak.GagspeakConfiguration;
using GagSpeak.GagspeakConfiguration.Models;
using GagSpeak.Interop.Ipc;
using GagSpeak.Localization;
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
    private bool ThemePushed = false;
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

        Flags = ImGuiWindowFlags.NoScrollbar;
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

    protected override void PreDrawInternal()
    {
        if (!ThemePushed)
        {
            ImGui.PushStyleColor(ImGuiCol.TitleBg, new Vector4(0.331f, 0.081f, 0.169f, .803f));
            ImGui.PushStyleColor(ImGuiCol.TitleBgActive, new Vector4(0.579f, 0.170f, 0.359f, 0.828f));

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
        _ = _uiShared.DrawOtherPluginState();
        DrawSettingsContent();
    }

    private DateTime _lastRefresh = DateTime.MinValue;
    private void DrawGlobalSettings()
    {
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

        _uiShared.GagspeakBigText(GSLoc.Settings.MainOptions.HeaderGags);
        using (ImRaii.Disabled(liveChatGarblerLocked))
        {
            if (ImGui.Checkbox(GSLoc.Settings.MainOptions.LiveChatGarbler, ref liveChatGarblerActive))
            {
                // Perform a mediator call that we have updated a permission.
                _ = _apiHubMain.UserUpdateOwnGlobalPerm(new UserGlobalPermChangeDto(MainHub.PlayerUserData,
                    new KeyValuePair<string, object>("LiveChatGarblerActive", liveChatGarblerActive), MainHub.PlayerUserData));

            }
            _uiShared.DrawHelpText(GSLoc.Settings.MainOptions.LiveChatGarblerTT);
        }

        if (ImGui.Checkbox(GSLoc.Settings.MainOptions.GagGlamours, ref itemAutoEquip))
        {
            PlayerGlobalPerms.ItemAutoEquip = itemAutoEquip;
            // if this creates a race condition down the line remove the above line.
            _ = _apiHubMain.UserUpdateOwnGlobalPerm(new UserGlobalPermChangeDto(MainHub.PlayerUserData,
            new KeyValuePair<string, object>("ItemAutoEquip", itemAutoEquip), MainHub.PlayerUserData));
            // perform recalculations to our cache.
            Mediator.Publish(new AppearanceImpactingSettingChanged());
        }
        _uiShared.DrawHelpText(GSLoc.Settings.MainOptions.GagGlamoursTT);

        if (ImGui.Checkbox(GSLoc.Settings.MainOptions.GagPadlockTimer, ref removeGagOnLockExpiration))
        {
            _clientConfigs.GagspeakConfig.RemoveGagUponLockExpiration = removeGagOnLockExpiration;
            _clientConfigs.Save();
        }
        _uiShared.DrawHelpText(GSLoc.Settings.MainOptions.GagPadlockTimerTT);

        ImGui.Separator();
        _uiShared.GagspeakBigText(GSLoc.Settings.MainOptions.HeaderWardrobe);

        if (ImGui.Checkbox(GSLoc.Settings.MainOptions.WardrobeActive, ref wardrobeEnabled))
        {
            PlayerGlobalPerms.WardrobeEnabled = wardrobeEnabled;
            _ = _apiHubMain.UserUpdateOwnGlobalPerm(new UserGlobalPermChangeDto(MainHub.PlayerUserData,
                new KeyValuePair<string, object>("WardrobeEnabled", wardrobeEnabled), MainHub.PlayerUserData));
            
            // if this creates a race condition down the line remove the above line.
            if (wardrobeEnabled is false)
            {
                // turn off all respective children as well and push the update.
                PlayerGlobalPerms.RestraintSetAutoEquip = false;
                _ = _apiHubMain.UserUpdateOwnGlobalPerm(new UserGlobalPermChangeDto(MainHub.PlayerUserData,
                new KeyValuePair<string, object>("RestraintSetAutoEquip", false), MainHub.PlayerUserData));
                // disable other options respective to it.
                _clientConfigs.GagspeakConfig.DisableSetUponUnlock = false;
                _clientConfigs.GagspeakConfig.CursedDungeonLoot = false;
                _clientConfigs.Save();
            }
        }
        _uiShared.DrawHelpText(GSLoc.Settings.MainOptions.WardrobeActiveTT);

        using (ImRaii.Disabled(!wardrobeEnabled))
        {
            if (ImGui.Checkbox(GSLoc.Settings.MainOptions.RestraintSetGlamour, ref restraintSetAutoEquip))
            {
                PlayerGlobalPerms.RestraintSetAutoEquip = restraintSetAutoEquip;
                // if this creates a race condition down the line remove the above line.
                _ = _apiHubMain.UserUpdateOwnGlobalPerm(new UserGlobalPermChangeDto(MainHub.PlayerUserData,
                new KeyValuePair<string, object>("RestraintSetAutoEquip", restraintSetAutoEquip), MainHub.PlayerUserData));
                // perform recalculations to our cache.
                Mediator.Publish(new AppearanceImpactingSettingChanged());
            }
            _uiShared.DrawHelpText(GSLoc.Settings.MainOptions.RestraintSetGlamourTT);

            if (ImGui.Checkbox(GSLoc.Settings.MainOptions.RestraintPadlockTimer, ref restraintSetDisableWhenUnlocked))
            {
                _clientConfigs.GagspeakConfig.DisableSetUponUnlock = restraintSetDisableWhenUnlocked;
                _clientConfigs.Save();
            }
            _uiShared.DrawHelpText(GSLoc.Settings.MainOptions.RestraintPadlockTimerTT);

            if (ImGui.Checkbox(GSLoc.Settings.MainOptions.CursedLootActive, ref cursedDungeonLoot))
            {
                _clientConfigs.GagspeakConfig.CursedDungeonLoot = cursedDungeonLoot;
                _clientConfigs.Save();
            }
            _uiShared.DrawHelpText(GSLoc.Settings.MainOptions.CursedLootActiveTT);
        }


        if (ImGui.Checkbox(GSLoc.Settings.MainOptions.MoodlesActive, ref moodlesEnabled))
        {
            PlayerGlobalPerms.MoodlesEnabled = moodlesEnabled;
            // if this creates a race condition down the line remove the above line.
            _ = _apiHubMain.UserUpdateOwnGlobalPerm(new UserGlobalPermChangeDto(MainHub.PlayerUserData,
            new KeyValuePair<string, object>("MoodlesEnabled", moodlesEnabled), MainHub.PlayerUserData));
            // perform recalculations to our cache.
            Mediator.Publish(new AppearanceImpactingSettingChanged());

        }
        _uiShared.DrawHelpText(GSLoc.Settings.MainOptions.MoodlesActiveTT);

        // draw out revert style selection
        ImGui.Spacing();
        ImGui.TextUnformatted(GSLoc.Settings.MainOptions.RevertSelectionLabel);
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
        _uiShared.GagspeakBigText(GSLoc.Settings.MainOptions.HeaderPuppet);

        if (ImGui.Checkbox(GSLoc.Settings.MainOptions.PuppeteerActive, ref puppeteerEnabled))
        {
            PlayerGlobalPerms.PuppeteerEnabled = puppeteerEnabled;
            // if this creates a race condition down the line remove the above line.
            _ = _apiHubMain.UserUpdateOwnGlobalPerm(new UserGlobalPermChangeDto(MainHub.PlayerUserData,
            new KeyValuePair<string, object>("PuppeteerEnabled", puppeteerEnabled), MainHub.PlayerUserData));

        }
        _uiShared.DrawHelpText(GSLoc.Settings.MainOptions.PuppeteerActiveTT);

        using (ImRaii.Disabled(!puppeteerEnabled))
        {
            using var indent = ImRaii.PushIndent();

            ImGui.SetNextItemWidth(200 * ImGuiHelpers.GlobalScale);
            if (ImGui.InputText(GSLoc.Settings.MainOptions.GlobalTriggerPhrase, ref globalTriggerPhrase, 100, ImGuiInputTextFlags.EnterReturnsTrue))
            {
                PlayerGlobalPerms.GlobalTriggerPhrase = globalTriggerPhrase;
                // if this creates a race condition down the line remove the above line.
                _ = _apiHubMain.UserUpdateOwnGlobalPerm(new UserGlobalPermChangeDto(MainHub.PlayerUserData,
                new KeyValuePair<string, object>("GlobalTriggerPhrase", globalTriggerPhrase), MainHub.PlayerUserData));

            }
            _uiShared.DrawHelpText(GSLoc.Settings.MainOptions.GlobalTriggerPhraseTT);

            if (ImGui.Checkbox(GSLoc.Settings.MainOptions.GlobalAllowSit, ref globalAllowSitRequests))
            {
                PlayerGlobalPerms.GlobalAllowSitRequests = globalAllowSitRequests;
                // if this creates a race condition down the line remove the above line.
                _ = _apiHubMain.UserUpdateOwnGlobalPerm(new UserGlobalPermChangeDto(MainHub.PlayerUserData,
                new KeyValuePair<string, object>("GlobalAllowSitRequests", globalAllowSitRequests), MainHub.PlayerUserData));

            }
            _uiShared.DrawHelpText(GSLoc.Settings.MainOptions.GlobalAllowSitTT);

            if (ImGui.Checkbox(GSLoc.Settings.MainOptions.GlobalAllowMotion, ref globalAllowMotionRequests))
            {
                PlayerGlobalPerms.GlobalAllowMotionRequests = globalAllowMotionRequests;
                // if this creates a race condition down the line remove the above line.
                _ = _apiHubMain.UserUpdateOwnGlobalPerm(new UserGlobalPermChangeDto(MainHub.PlayerUserData,
                new KeyValuePair<string, object>("GlobalAllowMotionRequests", globalAllowMotionRequests), MainHub.PlayerUserData));

            }
            _uiShared.DrawHelpText(GSLoc.Settings.MainOptions.GlobalAllowMotionTT);

            if (ImGui.Checkbox(GSLoc.Settings.MainOptions.GlobalAllowAll, ref globalAllowAllRequests))
            {
                PlayerGlobalPerms.GlobalAllowAllRequests = globalAllowAllRequests;
                // if this creates a race condition down the line remove the above line.
                _ = _apiHubMain.UserUpdateOwnGlobalPerm(new UserGlobalPermChangeDto(MainHub.PlayerUserData,
                new KeyValuePair<string, object>("GlobalAllowAllRequests", globalAllowAllRequests), MainHub.PlayerUserData));

            }
            _uiShared.DrawHelpText(GSLoc.Settings.MainOptions.GlobalAllowAllTT);
        }


        ImGui.Separator();
        _uiShared.GagspeakBigText(GSLoc.Settings.MainOptions.HeaderToybox);

        if (ImGui.Checkbox(GSLoc.Settings.MainOptions.ToyboxActive, ref toyboxEnabled))
        {
            PlayerGlobalPerms.ToyboxEnabled = toyboxEnabled;
            // if this creates a race condition down the line remove the above line.
            _ = _apiHubMain.UserUpdateOwnGlobalPerm(new UserGlobalPermChangeDto(MainHub.PlayerUserData,
            new KeyValuePair<string, object>("ToyboxEnabled", toyboxEnabled), MainHub.PlayerUserData));

        }
        _uiShared.DrawHelpText(GSLoc.Settings.MainOptions.ToyboxActiveTT);


        if (ImGui.Checkbox(GSLoc.Settings.MainOptions.IntifaceAutoConnect, ref intifaceAutoConnect))
        {
            _clientConfigs.GagspeakConfig.IntifaceAutoConnect = intifaceAutoConnect;
            _clientConfigs.Save();
        }
        _uiShared.DrawHelpText(GSLoc.Settings.MainOptions.IntifaceAutoConnectTT);

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
        _uiShared.DrawHelpText(GSLoc.Settings.MainOptions.IntifaceAddressTT);

        using (ImRaii.Disabled(true))
        {
            if (ImGui.Checkbox(GSLoc.Settings.MainOptions.VibeServerAutoConnect, ref vibeServerAutoConnect))
            {
                _clientConfigs.GagspeakConfig.VibeServerAutoConnect = vibeServerAutoConnect;
                _clientConfigs.Save();
            }
        }
        _uiShared.DrawHelpText(GSLoc.Settings.MainOptions.VibeServerAutoConnectTT);

        if (ImGui.Checkbox(GSLoc.Settings.MainOptions.SpatialAudioActive, ref spatialVibratorAudio))
        {
            PlayerGlobalPerms.SpatialVibratorAudio = spatialVibratorAudio;
            // if this creates a race condition down the line remove the above line.
            _ = _apiHubMain.UserUpdateOwnGlobalPerm(new UserGlobalPermChangeDto(MainHub.PlayerUserData,
            new KeyValuePair<string, object>("SpatialVibratorAudio", spatialVibratorAudio), MainHub.PlayerUserData));
        }
        _uiShared.DrawHelpText(GSLoc.Settings.MainOptions.SpatialAudioActiveTT);

        ImGui.Spacing();

        ImGui.SetNextItemWidth(250 * ImGuiHelpers.GlobalScale);
        if (ImGui.InputText("PiShock API Key", ref piShockApiKey, 100, ImGuiInputTextFlags.EnterReturnsTrue))
        {
            _clientConfigs.GagspeakConfig.PiShockApiKey = piShockApiKey;
            _clientConfigs.Save();
        }
        _uiShared.DrawHelpText(GSLoc.Settings.MainOptions.PiShockKeyTT);

        ImGui.SetNextItemWidth(250 * ImGuiHelpers.GlobalScale);
        if (ImGui.InputText("PiShock Username", ref piShockUsername, 100, ImGuiInputTextFlags.EnterReturnsTrue))
        {
            _clientConfigs.GagspeakConfig.PiShockUsername = piShockUsername;
            _clientConfigs.Save();
        }
        _uiShared.DrawHelpText(GSLoc.Settings.MainOptions.PiShockUsernameTT);


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
        UiSharedService.AttachToolTip(GSLoc.Settings.MainOptions.PiShockShareCodeRefreshTT);
        
        ImUtf8.SameLineInner();
        ImGui.TextUnformatted(GSLoc.Settings.MainOptions.PiShockShareCode);
        _uiShared.DrawHelpText(GSLoc.Settings.MainOptions.PiShockShareCodeTT);

        ImGui.SetNextItemWidth(250 * ImGuiHelpers.GlobalScale);
        if (ImGui.SliderInt(GSLoc.Settings.MainOptions.PiShockVibeTime, ref maxGlobalVibrateDuration, 0, 30))
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
        _uiShared.DrawHelpText(GSLoc.Settings.MainOptions.PiShockVibeTimeTT);

        // make this section readonly
        UiSharedService.ColorText(GSLoc.Settings.MainOptions.PiShockPermsLabel, ImGuiColors.ParsedGold);
        using (ImRaii.Disabled(true))
        {
            using (ImRaii.Group())
            {
                ImGui.Checkbox(GSLoc.Settings.MainOptions.PiShockAllowShocks, ref allowGlobalShockShockCollar);
                ImGui.SameLine();
                ImGui.Checkbox(GSLoc.Settings.MainOptions.PiShockAllowVibes, ref allowGlobalVibrateShockCollar);
                ImGui.SameLine();
                ImGui.Checkbox(GSLoc.Settings.MainOptions.PiShockAllowBeeps, ref allowGlobalBeepShockCollar);
            }
            ImGui.TextUnformatted(GSLoc.Settings.MainOptions.PiShockMaxShockIntensity);
            ImGui.SameLine();
            UiSharedService.ColorText(maxGlobalShockCollarIntensity.ToString() + "%", ImGuiColors.ParsedGold);

            ImGui.TextUnformatted(GSLoc.Settings.MainOptions.PiShockMaxShockDuration);
            ImGui.SameLine();
            UiSharedService.ColorText(maxGlobalShockDuration.Seconds.ToString() + "." + maxGlobalShockDuration.Milliseconds.ToString() + "s", ImGuiColors.ParsedGold);
        }
    }

    private void DrawChannelPreferences()
    {
        float width = ImGui.GetContentRegionAvail().X / 2;
        ImGui.Columns(2, "PreferencesColumns", true);
        ImGui.SetColumnWidth(0, width);
        // go to first column.
        _uiShared.GagspeakBigText("Live Chat Garbler");
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
            ImGui.Text(GSLoc.Settings.Preferences.LangDialectLabel);
            ImGui.SameLine();
            _uiShared.DrawCombo("##Language", 65, LanguagesDialects.Keys.ToArray(), (item) => item, (i) =>
            {
                if (i is null || i == _configService.Current.Language) return;
                _configService.Current.Language = i;
                _currentDialects = LanguagesDialects[_configService.Current.Language]; // update the dialects for the new language
                _activeDialect = _currentDialects[0]; // set the active dialect to the first dialect of the new language
                SetConfigDialectFromDialect(_activeDialect);
            }, _configService.Current.Language, flags: ImGuiComboFlags.NoArrowButton);
            UiSharedService.AttachToolTip(GSLoc.Settings.Preferences.LangTT);

            ImGui.SameLine();
            _uiShared.DrawCombo("##Dialect", 55, LanguagesDialects[_configService.Current.Language], (item) => item, (i) =>
            {
                if (i is null || i == _activeDialect) return;
                _activeDialect = i;
                SetConfigDialectFromDialect(_activeDialect);
            }, _activeDialect, flags: ImGuiComboFlags.NoArrowButton);
            UiSharedService.AttachToolTip(GSLoc.Settings.Preferences.DialectTT);
        }
    }
    private void DrawPreferences()
    {
        DrawChannelPreferences();

        ImGui.NextColumn();
        _uiShared.GagspeakBigText(GSLoc.Settings.Preferences.HeaderPuppet);
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
        ImGui.Columns(1);

        // the nicknames section
        ImGui.Separator();
        _uiShared.GagspeakBigText(GSLoc.Settings.Preferences.HeaderNicks);
        var openPopupOnAdd = _configService.Current.OpenPopupOnAdd;
        if (ImGui.Checkbox(GSLoc.Settings.Preferences.NickPopupLabel, ref openPopupOnAdd))
        {
            _configService.Current.OpenPopupOnAdd = openPopupOnAdd;
            _configService.Save();
        }
        _uiShared.DrawHelpText(GSLoc.Settings.Preferences.NickPopupTT);


        ImGui.Separator();
        _uiShared.GagspeakBigText(GSLoc.Settings.Preferences.HeaderUiPrefs);

        var enableDtrEntry = _configService.Current.EnableDtrEntry;
        var dtrPrivacyRadar = _configService.Current.ShowPrivacyRadar;
        var dtrActionNotifs = _configService.Current.ShowActionNotifs;
        var dtrVibeStatus = _configService.Current.ShowVibeStatus;

        var preferNicknamesInsteadOfName = _configService.Current.PreferNicknamesOverNames;
        var showVisibleSeparate = _configService.Current.ShowVisibleUsersSeparately;
        var showOfflineSeparate = _configService.Current.ShowOfflineUsersSeparately;

        var showProfiles = _configService.Current.ShowProfiles;
        var profileDelay = _configService.Current.ProfileDelay;
        var showContextMenus = _configService.Current.ShowContextMenus;

        if (ImGui.Checkbox(GSLoc.Settings.Preferences.EnableDtrLabel, ref enableDtrEntry))
        {
            _configService.Current.EnableDtrEntry = enableDtrEntry;
            if(enableDtrEntry is false)
            {
                _configService.Current.ShowPrivacyRadar = false;
                _configService.Current.ShowActionNotifs = false;
                _configService.Current.ShowVibeStatus = false;
            }
            _configService.Save();
        }
        _uiShared.DrawHelpText(GSLoc.Settings.Preferences.EnableDtrTT);

        using (ImRaii.Disabled(!enableDtrEntry))
        {
            ImGui.Indent();
            if (ImGui.Checkbox(GSLoc.Settings.Preferences.PrivacyRadarLabel, ref dtrPrivacyRadar))
            {
                _configService.Current.ShowPrivacyRadar = dtrPrivacyRadar;
                _configService.Save();
            }
            _uiShared.DrawHelpText(GSLoc.Settings.Preferences.PrivacyRadarTT);

            if (ImGui.Checkbox(GSLoc.Settings.Preferences.ActionsNotifLabel, ref dtrActionNotifs))
            {
                _configService.Current.ShowActionNotifs = dtrActionNotifs;
                _configService.Save();
            }
            _uiShared.DrawHelpText(GSLoc.Settings.Preferences.ActionsNotifTT);

            if (ImGui.Checkbox(GSLoc.Settings.Preferences.VibeStatusLabel, ref dtrVibeStatus))
            {
                _configService.Current.ShowVibeStatus = dtrVibeStatus;
                _configService.Save();
            }
            _uiShared.DrawHelpText(GSLoc.Settings.Preferences.VibeStatusTT);
            ImGui.Unindent();
        }

        if (ImGui.Checkbox(GSLoc.Settings.Preferences.ShowVisibleSeparateLabel, ref showVisibleSeparate))
        {
            _configService.Current.ShowVisibleUsersSeparately = showVisibleSeparate;
            _configService.Save();
            Mediator.Publish(new RefreshUiMessage());
        }
        _uiShared.DrawHelpText(GSLoc.Settings.Preferences.ShowVisibleSeparateTT);

        if (ImGui.Checkbox(GSLoc.Settings.Preferences.ShowOfflineSeparateLabel, ref showOfflineSeparate))
        {
            _configService.Current.ShowOfflineUsersSeparately = showOfflineSeparate;
            _configService.Save();
            Mediator.Publish(new RefreshUiMessage());
        }
        _uiShared.DrawHelpText(GSLoc.Settings.Preferences.ShowOfflineSeparateTT);

        if (ImGui.Checkbox(GSLoc.Settings.Preferences.PreferNicknamesLabel, ref preferNicknamesInsteadOfName))
        {
            _configService.Current.PreferNicknamesOverNames = preferNicknamesInsteadOfName;
            _configService.Save();
            Mediator.Publish(new RefreshUiMessage());
        }
        _uiShared.DrawHelpText(GSLoc.Settings.Preferences.PreferNicknamesTT);

        if (ImGui.Checkbox(GSLoc.Settings.Preferences.ShowProfilesLabel, ref showProfiles))
        {
            Mediator.Publish(new ClearProfileDataMessage());
            _configService.Current.ShowProfiles = showProfiles;
            _configService.Save();
        }
        _uiShared.DrawHelpText(GSLoc.Settings.Preferences.ShowProfilesTT);

        using (ImRaii.Disabled(!showProfiles))
        {
            ImGui.Indent();
            ImGui.SetNextItemWidth(200 * ImGuiHelpers.GlobalScale);
            if (ImGui.SliderFloat(GSLoc.Settings.Preferences.ProfileDelayLabel, ref profileDelay, 0.3f, 5))
            {
                _configService.Current.ProfileDelay = profileDelay;
                _configService.Save();
            }
            _uiShared.DrawHelpText(GSLoc.Settings.Preferences.ProfileDelayTT);
            ImGui.Unindent();
        }

        if (ImGui.Checkbox(GSLoc.Settings.Preferences.ContextMenusLabel, ref showContextMenus))
        {
            _configService.Current.ShowContextMenus = showContextMenus;
            _configService.Save();
        }
        _uiShared.DrawHelpText(GSLoc.Settings.Preferences.ContextMenusTT);

        /* --------------- Separator for moving onto the Notifications Section ----------- */
        ImGui.Separator();
        _uiShared.GagspeakBigText(GSLoc.Settings.Preferences.HeaderNotifications);

        var liveGarblerZoneChangeWarn = _configService.Current.LiveGarblerZoneChangeWarn;
        var serverConnectionNotifs = _configService.Current.NotifyForServerConnections;
        var onlineNotifs = _configService.Current.NotifyForOnlinePairs;
        var onlineNotifsNickLimited = _configService.Current.NotifyLimitToNickedPairs;

        if (ImGui.Checkbox(GSLoc.Settings.Preferences.ZoneChangeWarnLabel, ref liveGarblerZoneChangeWarn))
        {
            _configService.Current.LiveGarblerZoneChangeWarn = liveGarblerZoneChangeWarn;
            _configService.Save();
        }
        _uiShared.DrawHelpText(GSLoc.Settings.Preferences.ZoneChangeWarnTT);

        if (ImGui.Checkbox(GSLoc.Settings.Preferences.ConnectedNotifLabel, ref serverConnectionNotifs))
        {
            _configService.Current.NotifyForServerConnections = serverConnectionNotifs;
            _configService.Save();
        }
        _uiShared.DrawHelpText(GSLoc.Settings.Preferences.ConnectedNotifTT);

        if (ImGui.Checkbox(GSLoc.Settings.Preferences.OnlineNotifLabel, ref onlineNotifs))
        {
            _configService.Current.NotifyForOnlinePairs = onlineNotifs;
            if (!onlineNotifs) _configService.Current.NotifyLimitToNickedPairs = false;
            _configService.Save();
        }
        _uiShared.DrawHelpText(GSLoc.Settings.Preferences.OnlineNotifTT);

        using (ImRaii.Disabled(!onlineNotifs))
        {
            if (ImGui.Checkbox(GSLoc.Settings.Preferences.LimitForNicksLabel, ref onlineNotifsNickLimited))
            {
                _configService.Current.NotifyLimitToNickedPairs = onlineNotifsNickLimited;
                _configService.Save();
            }
            _uiShared.DrawHelpText(GSLoc.Settings.Preferences.LimitForNicksTT);
        }
    }
    private void DrawAccountManagement()
    {
        _uiShared.GagspeakBigText(GSLoc.Settings.Accounts.PrimaryLabel);
        var localContentId = _uiShared.PlayerLocalContentID;

        // obtain the primary account auth.
        var primaryAuth = _serverConfigs.CurrentServer.Authentications.FirstOrDefault(c => c.IsPrimary);
        if (primaryAuth is null) {
            UiSharedService.ColorText("No primary account setup to display", ImGuiColors.DPSRed);
            return;
        }
        DrawAccount(int.MaxValue, primaryAuth, primaryAuth.CharacterPlayerContentId == localContentId);

        // display title for account management
        _uiShared.GagspeakBigText(GSLoc.Settings.Accounts.SecondaryLabel);
        // now we need to display the rest of the secondary authentications of the primary account. In other words all other authentications.
        if (_serverConfigs.HasAnySecretKeys())
        {
            // fetch the list of additional authentications that are not the primary account.
            var secondaryAuths = _serverConfigs.CurrentServer.Authentications.Where(c => !c.IsPrimary).ToList();

            for (int i = 0; i < secondaryAuths.Count; i++)
                DrawAccount(i, secondaryAuths[i], secondaryAuths[i].CharacterPlayerContentId == localContentId);
            return;
        }
        UiSharedService.ColorText(GSLoc.Settings.Accounts.NoSecondaries, ImGuiColors.DPSRed);
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
            UiSharedService.AttachToolTip(GSLoc.Settings.Accounts.CharaNameLabel);

            // head over to the end to make the delete button.
            var isPrimaryIcon = _uiShared.GetIconData(FontAwesomeIcon.Fingerprint);

            var allowDelete = (!(KeyMonitor.CtrlPressed() && KeyMonitor.ShiftPressed()) || !(MainHub.IsServerAlive && MainHub.IsConnected && isOnlineUser));
            ImGui.SameLine(ImGui.GetContentRegionAvail().X - _uiShared.GetIconTextButtonSize(FontAwesomeIcon.Trash, GSLoc.Settings.Accounts.DeleteButtonLabel));

            var canDelete = account.SecretKey.HasHadSuccessfulConnection;

            if (_uiShared.IconTextButton(FontAwesomeIcon.Trash, "Delete Account", isInPopup: true, disabled: !canDelete || !allowDelete, id: "DeleteAccount"+ account.CharacterPlayerContentId))
            {
                _deleteAccountPopupModalShown = true;
                ImGui.OpenPopup("Delete your account?");
            }
            UiSharedService.AttachToolTip(canDelete
                ? GSLoc.Settings.Accounts.DeleteButtonDisabledTT : isPrimary
                    ? GSLoc.Settings.Accounts.DeleteButtonTT + GSLoc.Settings.Accounts.DeleteButtonPrimaryTT
                    : GSLoc.Settings.Accounts.DeleteButtonTT, color: ImGuiColors.DalamudRed);

        }
        // next line:
        using (var group2 = ImRaii.Group())
        {
            ImGui.AlignTextToFramePadding();
            _uiShared.IconText(FontAwesomeIcon.Globe);
            ImUtf8.SameLineInner();
            UiSharedService.ColorText(_uiShared.WorldData[(ushort)account.WorldId], isPrimary ? ImGuiColors.ParsedGold : ImGuiColors.ParsedPink);
            UiSharedService.AttachToolTip(GSLoc.Settings.Accounts.CharaWorldLabel);

            var isPrimaryIcon = _uiShared.GetIconData(FontAwesomeIcon.Fingerprint);
            var successfulConnection = _uiShared.GetIconData(FontAwesomeIcon.PlugCircleCheck);
            float rightEnd = ImGui.GetContentRegionAvail().X - successfulConnection.X - isPrimaryIcon.X - 2 * ImGui.GetStyle().ItemInnerSpacing.X;
            ImGui.SameLine(rightEnd);

            _uiShared.BooleanToColoredIcon(account.IsPrimary, false, FontAwesomeIcon.Fingerprint, FontAwesomeIcon.Fingerprint, isPrimary ? ImGuiColors.ParsedGold : ImGuiColors.ParsedPink, ImGuiColors.DalamudGrey3);
            UiSharedService.AttachToolTip(account.IsPrimary ? GSLoc.Settings.Accounts.FingerprintPrimary : GSLoc.Settings.Accounts.FingerprintSecondary);
            _uiShared.BooleanToColoredIcon(account.SecretKey.HasHadSuccessfulConnection, true, FontAwesomeIcon.PlugCircleCheck, FontAwesomeIcon.PlugCircleXmark, ImGuiColors.ParsedGreen, ImGuiColors.DalamudGrey3);
            UiSharedService.AttachToolTip(account.SecretKey.HasHadSuccessfulConnection ? GSLoc.Settings.Accounts.SuccessfulConnection : GSLoc.Settings.Accounts.NoSuccessfulConnection);
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
            UiSharedService.AttachToolTip(GSLoc.Settings.Accounts.CharaKeyLabel);
            // we shoul draw an inputtext field here if we can edit it, and a text field if we cant.
            if (EditingIdx == idx)
            {
                ImUtf8.SameLineInner();
                ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - _uiShared.GetIconButtonSize(FontAwesomeIcon.PenSquare).X - ImGui.GetStyle().ItemSpacing.X);
                string key = account.SecretKey.Key;
                if (ImGui.InputTextWithHint("##SecondaryAuthKey" + account.CharacterPlayerContentId, "Paste Secret Key Here...", ref key, 64, ImGuiInputTextFlags.EnterReturnsTrue))
                {
                    if (account.SecretKey.Label.IsNullOrEmpty())
                        account.SecretKey.Label = "Alt Character Key for " + account.CharacterName + " on " + _uiShared.WorldData[(ushort)account.WorldId];
                    // set the key and save the changes.
                    account.SecretKey.Key = key;
                    EditingIdx = -1;
                    _serverConfigs.Save();
                }
            }
            else
            {
                ImUtf8.SameLineInner();
                UiSharedService.ColorText(keyDisplayText, isPrimary ? ImGuiColors.ParsedGold : ImGuiColors.ParsedPink);
                if (ImGui.IsItemClicked()) ImGui.SetClipboardText(account.SecretKey.Key);
                UiSharedService.AttachToolTip(GSLoc.Settings.Accounts.CopyKeyToClipboard);
            }

            if (idx != int.MaxValue)
            {
                var insertKey = _uiShared.GetIconData(FontAwesomeIcon.PenSquare);
                float rightEnd = ImGui.GetContentRegionAvail().X - insertKey.X;
                ImGui.SameLine(rightEnd);
                Vector4 col = account.SecretKey.HasHadSuccessfulConnection ? ImGuiColors.DalamudRed : ImGuiColors.DalamudGrey3;
                _uiShared.BooleanToColoredIcon(EditingIdx == idx, false, FontAwesomeIcon.PenSquare, FontAwesomeIcon.PenSquare, ImGuiColors.ParsedPink, col);
                if (ImGui.IsItemClicked() && !account.SecretKey.HasHadSuccessfulConnection)
                    EditingIdx = EditingIdx == idx ? -1 : idx;
                UiSharedService.AttachToolTip(account.SecretKey.HasHadSuccessfulConnection ? GSLoc.Settings.Accounts.EditKeyNotAllowed : GSLoc.Settings.Accounts.EditKeyAllowed);
            }
        }

        if (ImGui.BeginPopupModal("Delete your account?", ref _deleteAccountPopupModalShown, UiSharedService.PopupWindowFlags))
        {
            if(isPrimary)
            {
                UiSharedService.ColorTextWrapped(GSLoc.Settings.Accounts.RemoveAccountPrimaryWarning, ImGuiColors.DalamudRed);
                ImGui.Spacing();
            }
            // display normal warning
            UiSharedService.TextWrapped(GSLoc.Settings.Accounts.RemoveAccountWarning);
            ImGui.TextUnformatted(GSLoc.Settings.Accounts.RemoveAccountConfirm);
            ImGui.Separator();
            ImGui.Spacing();

            var buttonSize = (ImGui.GetWindowContentRegionMax().X - ImGui.GetWindowContentRegionMin().X -
                              ImGui.GetStyle().ItemSpacing.X) / 2;

            if (ImGui.Button(GSLoc.Settings.Accounts.DeleteButtonLabel, new Vector2(buttonSize, 0)))
            {
                _ = Task.Run(_apiHubMain.UserDelete);
                _deleteAccountPopupModalShown = false;
                // if this was our primrary account, we should switch to the intro UI.
                if(isPrimary)
                {
                    // we should remove all other authentications from our server storage authentications and reconnect.
                    _serverConfigs.CurrentServer.Authentications.Clear();
                    Mediator.Publish(new SwitchToIntroUiMessage());
                }
            }

            ImGui.SameLine();

            if (ImGui.Button("Cancel##cancelDelete", new Vector2(buttonSize, 0)))
                _deleteAccountPopupModalShown = false;

            UiSharedService.SetScaledWindowSize(325);
            ImGui.EndPopup();
        }
    }

    /// <summary> Displays the Debug section within the settings, where we can set our debug level </summary>
    private static readonly Dictionary<string, LoggerType[]> loggerSections = new Dictionary<string, LoggerType[]>
    {
        { "Foundation", new[] { LoggerType.Achievements, LoggerType.AchievementEvents, LoggerType.AchievementInfo } },
        { "Interop", new[] { LoggerType.IpcGagSpeak, LoggerType.IpcCustomize, LoggerType.IpcGlamourer, LoggerType.IpcMare, LoggerType.IpcMoodles, LoggerType.IpcPenumbra } },
        { "State Managers", new[] { LoggerType.AppearanceState, LoggerType.ToyboxState, LoggerType.Mediator, LoggerType.GarblerCore } },
        { "Update Monitors", new[] { LoggerType.ToyboxAlarms, LoggerType.ActionsNotifier, LoggerType.KinkPlateMonitor, LoggerType.EmoteMonitor, LoggerType.ChatDetours, LoggerType.ActionEffects, LoggerType.SpatialAudioLogger } },
        { "Hardcore", new[] { LoggerType.HardcoreActions, LoggerType.HardcoreMovement, LoggerType.HardcorePrompt } },
        { "Data & Modules", new[] { LoggerType.ClientPlayerData, LoggerType.GagHandling, LoggerType.PadlockHandling, LoggerType.Restraints, LoggerType.Puppeteer, LoggerType.CursedLoot, LoggerType.ToyboxDevices, LoggerType.ToyboxPatterns, LoggerType.ToyboxTriggers, LoggerType.VibeControl } },
        { "Pair Data", new[] { LoggerType.PairManagement, LoggerType.PairInfo, LoggerType.PairDataTransfer, LoggerType.PairHandlers, LoggerType.OnlinePairs, LoggerType.VisiblePairs, LoggerType.PrivateRooms, LoggerType.GameObjects } },
        { "Services", new[] { LoggerType.Cosmetics, LoggerType.Textures, LoggerType.GlobalChat, LoggerType.ContextDtr, LoggerType.PatternHub, LoggerType.Safeword } },
        { "UI", new[] { LoggerType.UiCore, LoggerType.UserPairDrawer, LoggerType.Permissions, LoggerType.Simulation } },
        { "WebAPI", new[] { LoggerType.PiShock, LoggerType.ApiCore, LoggerType.Callbacks, LoggerType.Health, LoggerType.HubFactory, LoggerType.JwtTokens } }
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
        _uiShared.GagspeakBigText("Player Character Debug Info:");
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
        _uiShared.GagspeakBigText("Pairs Debug Info:");

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
                ImGui.Text($"HasIPCData: {clientPair.LastIpcData == null}");
                ImGui.Text($"PlayerName: {clientPair.PlayerNameWithWorld ?? "N/A"}");

                if (clientPair.PairGlobals != null)
                {
                    if (ImGui.CollapsingHeader($"{clientPair.UserData.UID}'s Global permissions || {_serverConfigs.GetNicknameForUid(clientPair.UserData.UID)}"))
                    {
                        ImGui.Text($"Safeword: {clientPair.PairGlobals.Safeword}");
                        ImGui.Text($"SafewordUsed: {clientPair.PairGlobals.SafewordUsed}");
                        ImGui.Text($"LiveChatGarblerActive: {clientPair.PairGlobals.LiveChatGarblerActive}");
                        ImGui.Text($"LiveChatGarblerLocked: {clientPair.PairGlobals.LiveChatGarblerLocked}");
                        ImGui.Separator();
                        ImGui.Text($"WardrobeEnabled: {clientPair.PairGlobals.WardrobeEnabled}");
                        ImGui.Text($"ItemAutoEquip: {clientPair.PairGlobals.ItemAutoEquip}");
                        ImGui.Text($"RestraintSetAutoEquip: {clientPair.PairGlobals.RestraintSetAutoEquip}");
                        ImGui.Separator();
                        ImGui.Text($"PuppeteerEnabled: {clientPair.PairGlobals.PuppeteerEnabled}");
                        ImGui.Text($"GlobalTriggerPhrase: {clientPair.PairGlobals.GlobalTriggerPhrase}");
                        ImGui.Text($"GlobalAllowSitRequests: {clientPair.PairGlobals.GlobalAllowSitRequests}");
                        ImGui.Text($"GlobalAllowMotionRequests: {clientPair.PairGlobals.GlobalAllowMotionRequests}");
                        ImGui.Text($"GlobalAllowAllRequests: {clientPair.PairGlobals.GlobalAllowAllRequests}");
                        ImGui.Separator();
                        ImGui.Text($"MoodlesEnabled: {clientPair.PairGlobals.MoodlesEnabled}");
                        ImGui.Separator();
                        ImGui.Text($"ToyboxEnabled: {clientPair.PairGlobals.ToyboxEnabled}");
                        ImGui.Text($"LockToyboxUI: {clientPair.PairGlobals.LockToyboxUI}");
                        ImGui.Text($"ToyIsActive: {clientPair.PairGlobals.ToyIsActive}");
                        ImGui.Text($"ToyIntensity: {clientPair.PairGlobals.ToyIntensity}");
                        ImGui.Text($"SpatialVibratorAudio: {clientPair.PairGlobals.SpatialVibratorAudio}");
                        ImGui.Text($"ForcedFollow: {clientPair.PairGlobals.ForcedFollow}");
                        ImGui.Text($"ForcedEmoteState: {clientPair.PairGlobals.ForcedEmoteState}");
                        ImGui.Text($"ForcedToStay: {clientPair.PairGlobals.ForcedStay}");
                        ImGui.Text($"Blindfold: {clientPair.PairGlobals.ForcedBlindfold}");
                        ImGui.Text($"HiddenChat: {clientPair.PairGlobals.ChatBoxesHidden}");
                        ImGui.Text($"HiddenChatInput: {clientPair.PairGlobals.ChatInputHidden}");
                        ImGui.Text($"BlockingChatInput: {clientPair.PairGlobals.ChatInputBlocked}");
                    }
                }
                if (clientPair.PairPerms != null)
                {
                    if (ImGui.CollapsingHeader($"{clientPair.UserData.UID}'s Pair permissions || {_serverConfigs.GetNicknameForUid(clientPair.UserData.UID)}"))
                    {
                        ImGui.Text($"IsPaused: {clientPair.PairPerms.IsPaused}");
                        ImGui.Text($"ExtendedLockTimes: {clientPair.PairPerms.ExtendedLockTimes}");
                        ImGui.Text($"MaxLockTime: {clientPair.PairPerms.MaxLockTime}");
                        ImGui.Text($"InHardcore: {clientPair.PairPerms.InHardcore}");
                        ImGui.Separator();
                        ImGui.Text($"ApplyRestraintSets: {clientPair.PairPerms.ApplyRestraintSets}");
                        ImGui.Text($"LockRestraintSets: {clientPair.PairPerms.LockRestraintSets}");
                        ImGui.Text($"MaxAllowedRestraintTime: {clientPair.PairPerms.MaxAllowedRestraintTime}");
                        ImGui.Text($"RemoveRestraintSets: {clientPair.PairPerms.RemoveRestraintSets}");
                        ImGui.Separator();
                        ImGui.Text($"TriggerPhrase: {clientPair.PairPerms.TriggerPhrase}");
                        ImGui.Text($"StartChar: {clientPair.PairPerms.StartChar}");
                        ImGui.Text($"EndChar: {clientPair.PairPerms.EndChar}");
                        ImGui.Text($"AllowSitRequests: {clientPair.PairPerms.AllowSitRequests}");
                        ImGui.Text($"AllowMotionRequests: {clientPair.PairPerms.AllowMotionRequests}");
                        ImGui.Text($"AllowAllRequests: {clientPair.PairPerms.AllowAllRequests}");
                        ImGui.Separator();
                        ImGui.Text($"AllowPositiveStatusTypes: {clientPair.PairPerms.AllowPositiveStatusTypes}");
                        ImGui.Text($"AllowNegativeStatusTypes: {clientPair.PairPerms.AllowNegativeStatusTypes}");
                        ImGui.Text($"AllowSpecialStatusTypes: {clientPair.PairPerms.AllowSpecialStatusTypes}");
                        ImGui.Text($"PairCanApplyOwnMoodlesToYou: {clientPair.PairPerms.PairCanApplyOwnMoodlesToYou}");
                        ImGui.Text($"PairCanApplyYourMoodlesToYou: {clientPair.PairPerms.PairCanApplyYourMoodlesToYou}");
                        ImGui.Text($"MaxMoodleTime: {clientPair.PairPerms.MaxMoodleTime}");
                        ImGui.Text($"AllowPermanentMoodles: {clientPair.PairPerms.AllowPermanentMoodles}");
                        ImGui.Separator();
                        ImGui.Text($"ChangeToyState: {clientPair.PairPerms.CanToggleToyState}");
                        ImGui.Text($"CanUseVibeRemote: {clientPair.PairPerms.CanUseVibeRemote}");
                        ImGui.Text($"CanToggleAlarms: {clientPair.PairPerms.CanToggleAlarms}");
                        ImGui.Text($"CanSendAlarms: {clientPair.PairPerms.CanSendAlarms}");
                        ImGui.Text($"CanExecutePatterns: {clientPair.PairPerms.CanExecutePatterns}");
                        ImGui.Text($"CanStopPatterns: {clientPair.PairPerms.CanStopPatterns}");
                        ImGui.Text($"CanToggleTriggers: {clientPair.PairPerms.CanToggleTriggers}");
                        ImGui.Separator();
                        ImGui.Text($"AllowForcedFollow: {clientPair.PairPerms.AllowForcedFollow}");
                        ImGui.Text($"AllowForcedSit: {clientPair.PairPerms.AllowForcedSit}");
                        ImGui.Text($"AllowForcedEmoteState: {clientPair.PairPerms.AllowForcedEmote}");
                        ImGui.Text($"AllowForcedToStay: {clientPair.PairPerms.AllowForcedToStay}");
                        ImGui.Text($"AllowBlindfold: {clientPair.PairPerms.AllowBlindfold}");
                        ImGui.Text($"AllowHiddenChat: {clientPair.PairPerms.AllowHidingChatBoxes}");
                        ImGui.Text($"AllowHiddenChatInput: {clientPair.PairPerms.AllowHidingChatInput}");
                        ImGui.Text($"AllowBlockingChatInput: {clientPair.PairPerms.AllowChatInputBlocking}");
                    }
                }
                if (clientPair.PairPermAccess != null)
                {
                    if (ImGui.CollapsingHeader($"{clientPair.UserData.UID}'s Edit Access || {_serverConfigs.GetNicknameForUid(clientPair.UserData.UID)}"))
                    {
                        ImGui.Text("Live Chat Garbler Active Allowed: " + clientPair.PairPermAccess.LiveChatGarblerActiveAllowed);
                        ImGui.Text("Live Chat Garbler Locked Allowed: " + clientPair.PairPermAccess.LiveChatGarblerLockedAllowed);
                        ImGui.Text("Extended Lock Times Allowed: " + clientPair.PairPermAccess.ExtendedLockTimesAllowed);
                        ImGui.Text("Max Lock Time Allowed: " + clientPair.PairPermAccess.MaxLockTimeAllowed);
                        ImGui.Separator();
                        ImGui.Text("Wardrobe Enabled Allowed: " + clientPair.PairPermAccess.WardrobeEnabledAllowed);
                        ImGui.Text("Item Auto Equip Allowed: " + clientPair.PairPermAccess.ItemAutoEquipAllowed);
                        ImGui.Text("Restraint Set Auto Equip Allowed: " + clientPair.PairPermAccess.RestraintSetAutoEquipAllowed); ImGui.Text("Apply Restraint Sets Allowed: " + clientPair.PairPermAccess.ApplyRestraintSetsAllowed);
                        ImGui.Text("Lock Restraint Sets Allowed: " + clientPair.PairPermAccess.LockRestraintSetsAllowed);
                        ImGui.Text("Max Allowed Restraint Time Allowed: " + clientPair.PairPermAccess.MaxAllowedRestraintTimeAllowed);
                        ImGui.Text("Remove Restraint Sets Allowed: " + clientPair.PairPermAccess.RemoveRestraintSetsAllowed);
                        ImGui.Separator();
                        ImGui.Text("Puppeteer Enabled Allowed: " + clientPair.PairPermAccess.PuppeteerEnabledAllowed);
                        ImGui.Text("Allow Sit Requests Allowed: " + clientPair.PairPermAccess.AllowSitRequestsAllowed);
                        ImGui.Text("Allow Motion Requests Allowed: " + clientPair.PairPermAccess.AllowMotionRequestsAllowed);
                        ImGui.Text("Allow All Requests Allowed: " + clientPair.PairPermAccess.AllowAllRequestsAllowed);
                        ImGui.Separator();
                        ImGui.Text("Moodles Enabled Allowed: " + clientPair.PairPermAccess.MoodlesEnabledAllowed);
                        ImGui.Text("Allow Positive Status Types Allowed: " + clientPair.PairPermAccess.AllowPositiveStatusTypesAllowed);
                        ImGui.Text("Allow Negative Status Types Allowed: " + clientPair.PairPermAccess.AllowNegativeStatusTypesAllowed);
                        ImGui.Text("Allow Special Status Types Allowed: " + clientPair.PairPermAccess.AllowSpecialStatusTypesAllowed);
                        ImGui.Text("Pair Can Apply Own Moodles To You Allowed: " + clientPair.PairPermAccess.PairCanApplyOwnMoodlesToYouAllowed);
                        ImGui.Text("Pair Can Apply Your Moodles To You Allowed: " + clientPair.PairPermAccess.PairCanApplyYourMoodlesToYouAllowed);
                        ImGui.Text("Max Moodle Time Allowed: " + clientPair.PairPermAccess.MaxMoodleTimeAllowed);
                        ImGui.Text("Allow Permanent Moodles Allowed: " + clientPair.PairPermAccess.AllowPermanentMoodlesAllowed);
                        ImGui.Text("Allow Removing Moodles Allowed: " + clientPair.PairPermAccess.AllowRemovingMoodlesAllowed);
                        ImGui.Separator();
                        ImGui.Text("Toybox Enabled Allowed: " + clientPair.PairPermAccess.ToyboxEnabledAllowed);
                        ImGui.Text("Lock Toybox UI Allowed: " + clientPair.PairPermAccess.LockToyboxUIAllowed);
                        ImGui.Text("Spatial Vibrator Audio Allowed: " + clientPair.PairPermAccess.SpatialVibratorAudioAllowed);
                        ImGui.Text("Change Toy State Allowed: " + clientPair.PairPermAccess.CanToggleToyStateAllowed);
                        ImGui.Text("Can Use Realtime Vibe Remote Allowed: " + clientPair.PairPermAccess.CanUseVibeRemoteAllowed);
                        ImGui.Text("Can Toggle Alarms: " + clientPair.PairPermAccess.CanToggleAlarmsAllowed);
                        ImGui.Text("Can Send Alarms Allowed: " + clientPair.PairPermAccess.CanSendAlarmsAllowed);
                        ImGui.Text("Can Execute Patterns Allowed: " + clientPair.PairPermAccess.CanExecutePatternsAllowed);
                        ImGui.Text("Can Stop Patterns Allowed: " + clientPair.PairPermAccess.CanStopPatternsAllowed);
                        ImGui.Text("Can Toggle Triggers Allowed: " + clientPair.PairPermAccess.CanToggleTriggersAllowed);
                    }
                }
                if (clientPair.LastAppearanceData != null)
                {
                    if (ImGui.CollapsingHeader($"{clientPair.UserData.UID}'s Appearance Data || {_serverConfigs.GetNicknameForUid(clientPair.UserData.UID)}"))
                    {
                        for (int i = 0; i < 3; i++)
                        {
                            ImGui.Text($"Slot{i}GagType: {clientPair.LastAppearanceData.GagSlots[i].GagType}");
                            ImGui.Text($"Slot{i}GagPadlock: {clientPair.LastAppearanceData.GagSlots[i].Padlock}");
                            ImGui.Text($"Slot{i}GagPassword: {clientPair.LastAppearanceData.GagSlots[i].Password}");
                            ImGui.Text($"Slot{i}GagTimer: {clientPair.LastAppearanceData.GagSlots[i].Timer}");
                            ImGui.Text($"Slot{i}GagAssigner: {clientPair.LastAppearanceData.GagSlots[i].Assigner}");
                            ImGui.Separator();
                        }
                    }
                }
                if (clientPair.LastWardrobeData != null)
                {
                    if (ImGui.CollapsingHeader($"{clientPair.UserData.UID}'s Wardrobe Data || {_serverConfigs.GetNicknameForUid(clientPair.UserData.UID)}"))
                    {
                        ImGui.Text($"ActiveSetId: {clientPair.LastWardrobeData.ActiveSetId}");
                        ImGui.Text($"ActiveSetEnabledBy: {clientPair.LastWardrobeData.ActiveSetEnabledBy}");
                        ImGui.Text($"ActiveSetLockType: {clientPair.LastWardrobeData.Padlock}");
                        ImGui.Text($"ActiveSetLockPassword: {clientPair.LastWardrobeData.Password}");
                        ImGui.Text($"ActiveSetLockTime: {clientPair.LastWardrobeData.Timer}");
                        ImGui.Text($"ActiveSetLockedBy: {clientPair.LastWardrobeData.Assigner}");
                    }
                }
                if (clientPair.LastAliasData != null)
                {
                    if (ImGui.CollapsingHeader($"{clientPair.UserData.UID}'s Alias Data || {_serverConfigs.GetNicknameForUid(clientPair.UserData.UID)}"))
                    {
                        ImGui.Indent();
                        ImGui.Text($"Listening To: {clientPair.LastAliasData.CharacterName} @ {clientPair.LastAliasData.CharacterWorld}");
                        foreach (var alias in clientPair.LastAliasData.AliasList)
                        {
                            var tmptext = alias.Enabled ? "Enabled" : "Disabled";
                            ImGui.Text($"{tmptext} :: INPUT -> {alias.InputCommand}");
                            ImGui.Text($"OUTPUT -> {alias.OutputCommand}");
                        }
                        ImGui.Unindent();
                    }
                }
                if (clientPair.LastLightStorage is not null)
                {
                    if (ImGui.CollapsingHeader($"{clientPair.UserData.UID}'s Light Storage Data || {_serverConfigs.GetNicknameForUid(clientPair.UserData.UID)}"))
                    {
                        ImGui.Text("Gags with Glamour's:");
                        ImGui.Indent();
                        foreach (var gag in clientPair.LastLightStorage.GagItems)
                            ImGui.Text($"Gag with Glamour: {gag.Key}");
                        ImGui.Unindent();
                        ImGui.Spacing();
                        ImGui.Text("Restraints:");
                        ImGui.Indent();
                        foreach (var restraint in clientPair.LastLightStorage.Restraints)
                            ImGui.Text("[Identifier: " + restraint.Identifier + "] [Name: " + restraint.Name + "] [AffectedSlots: " + restraint.AffectedSlots.Count + "]");
                        ImGui.Unindent();
                        ImGui.Spacing();
                        ImGui.Text("Blindfold Item Slot: " + (EquipSlot)clientPair.LastLightStorage.BlindfoldItem.Slot);
                        ImGui.Spacing();
                        ImGui.Text("Cursed Items: ");
                        ImGui.Indent();
                        foreach (var cursedItem in clientPair.LastLightStorage.CursedItems)
                            ImGui.Text("[Identifier: " + cursedItem.Identifier + "] [Name: " + cursedItem.Name + "]");
                        ImGui.Unindent();
                        ImGui.Spacing();
                        ImGui.Text("Patterns: ");
                        ImGui.Indent();
                        foreach (var pattern in clientPair.LastLightStorage.Patterns)
                            ImGui.Text("[Identifier: " + pattern.Identifier + "] [Name: " + pattern.Name + "]");
                        ImGui.Unindent();
                        ImGui.Spacing();
                        ImGui.Text("Alarms: ");
                        ImGui.Indent();
                        foreach (var alarm in clientPair.LastLightStorage.Alarms)
                            ImGui.Text("[Identifier: " + alarm.Identifier + "] [Name: " + alarm.Name + "]");
                        ImGui.Unindent();
                        ImGui.Spacing();
                        ImGui.Text("Triggers: ");
                        ImGui.Indent();
                        foreach (var trigger in clientPair.LastLightStorage.Triggers)
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
        // align the next text the frame padding of the button in the same line.
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(GSLoc.Settings.AccountClaimText);
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
                if (ImGui.BeginTabItem(GSLoc.Settings.TabsGlobal))
                {
                    DrawGlobalSettings();
                    ImGui.EndTabItem();
                }
                if (ImGui.BeginTabItem(GSLoc.Settings.TabsHardcore))
                {
                    _hardcoreSettingsUI.DrawHardcoreSettings();
                    ImGui.EndTabItem();
                }
                if (ImGui.BeginTabItem(GSLoc.Settings.TabsPreferences))
                {
                    DrawPreferences();
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem(GSLoc.Settings.TabsAccounts))
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
            if (ImGui.BeginTabBar("offlineTabBar"))
            {
                if (ImGui.BeginTabItem(GSLoc.Settings.TabsAccounts))
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
    }
}
