using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Utility;
using GagSpeak.GagspeakConfiguration;
using GagSpeak.Interop.Ipc;
using GagSpeak.PlayerData.Data;
using GagSpeak.PlayerData.Pairs;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using GagSpeak.UpdateMonitoring;
using GagSpeak.WebAPI;
using GagspeakAPI.Data.Enum;
using GagspeakAPI.Data.Permissions;
using ImGuiNET;
using System.Globalization;
using System.Numerics;

namespace GagSpeak.UI;

public class SettingsUi : WindowMediatorSubscriberBase
{
    private readonly PlayerCharacterManager _playerCharacterManager;
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
        PlayerCharacterManager playerCharacterManager,
        ServerConfigurationManager serverConfigurationManager, GagspeakMediator mediator,
        IpcManager ipcManager, OnFrameworkService frameworkUtil) : base(logger, mediator, "GagSpeak Settings")
    {
        _playerCharacterManager = playerCharacterManager;
        _configService = configService;
        _pairManager = pairManager;
        _serverConfigurationManager = serverConfigurationManager;
        _ipcManager = ipcManager;
        _frameworkUtil = frameworkUtil;
        _uiShared = uiShared;
        AllowClickthrough = false;
        AllowPinning = false;

        SizeConstraints = new WindowSizeConstraints()
        {
            MinimumSize = new Vector2(625, 400),
            MaximumSize = new Vector2(800, 2000),
        };

        Mediator.Subscribe<OpenSettingsUiMessage>(this, (_) => Toggle());
        Mediator.Subscribe<SwitchToIntroUiMessage>(this, (_) => IsOpen = false);
    }

    private ApiController ApiController => _uiShared.ApiController;
    private UserGlobalPermissions PlayerGlobalPerms => _playerCharacterManager.GlobalPerms;

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


        // draw out our pair manager
        // Start of the Pair Manager section
        ImGui.Text("Pair Manager:");

        // Display additional info about the Pair Manager
        int totalPairs = _pairManager.DirectPairs.Count;
        int visibleUsersCount = _pairManager.GetVisibleUserCount();
        ImGui.Text($"Total Pairs: {totalPairs}");
        ImGui.Text($"Visible Users: {visibleUsersCount}");

        // Iterate through all client pairs in the PairManager
        foreach (var clientPair in _pairManager.DirectPairs)
        {
            if (ImGui.CollapsingHeader($"Pair: {clientPair.UserData.UID} || {_serverConfigurationManager.GetNicknameForUid(clientPair.UserData.UID)}"))
            {
                ImGui.Text($"UserData UID: {clientPair.UserData.UID}");
                ImGui.Indent();

                // Accessing and displaying information from the Pair object
                ImGui.Text($"IsDirectlyPaired: {clientPair.IsDirectlyPaired}");
                ImGui.Text($"IsOneSidedPair: {clientPair.IsOneSidedPair}");
                ImGui.Text($"IsOnline: {clientPair.IsOnline}");
                ImGui.Text($"IsPaired: {clientPair.IsPaired}");
                ImGui.Text($"IsVisible: {clientPair.IsVisible}");
                ImGui.Text($"PlayerName: {clientPair.PlayerName ?? "N/A"}");

                if (clientPair.UserPairGlobalPerms != null)
                {
                    if (ImGui.CollapsingHeader($"{clientPair.UserData.UID}'s Global permissions || {_serverConfigurationManager.GetNicknameForUid(clientPair.UserData.UID)}"))
                    {
                        ImGui.Text($"Safeword: {clientPair.UserPairGlobalPerms.Safeword}");
                        ImGui.Text($"SafewordUsed: {clientPair.UserPairGlobalPerms.SafewordUsed}");
                        ImGui.Text($"CommandsFromFriends: {clientPair.UserPairGlobalPerms.CommandsFromFriends}");
                        ImGui.Text($"CommandsFromParty: {clientPair.UserPairGlobalPerms.CommandsFromParty}");
                        ImGui.Text($"LiveChatGarblerActive: {clientPair.UserPairGlobalPerms.LiveChatGarblerActive}");
                        ImGui.Text($"LiveChatGarblerLocked: {clientPair.UserPairGlobalPerms.LiveChatGarblerLocked}");
                        ImGui.Separator();
                        ImGui.Text($"WardrobeEnabled: {clientPair.UserPairGlobalPerms.WardrobeEnabled}");
                        ImGui.Text($"ItemAutoEquip: {clientPair.UserPairGlobalPerms.ItemAutoEquip}");
                        ImGui.Text($"RestraintSetAutoEquip: {clientPair.UserPairGlobalPerms.RestraintSetAutoEquip}");
                        ImGui.Text($"LockGagStorageOnGagLock: {clientPair.UserPairGlobalPerms.LockGagStorageOnGagLock}");
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
                    if (ImGui.CollapsingHeader($"{clientPair.UserData.UID}'s Pair permissions || {_serverConfigurationManager.GetNicknameForUid(clientPair.UserData.UID)}"))
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
                        ImGui.Text($"ChangeToyState: {clientPair.UserPairUniquePairPerms.ChangeToyState}");
                        ImGui.Text($"CanControlIntensity: {clientPair.UserPairUniquePairPerms.CanControlIntensity}");
                        ImGui.Text($"VibratorAlarms: {clientPair.UserPairUniquePairPerms.VibratorAlarms}");
                        ImGui.Text($"CanUseRealtimeVibeRemote: {clientPair.UserPairUniquePairPerms.CanUseRealtimeVibeRemote}");
                        ImGui.Text($"CanExecutePatterns: {clientPair.UserPairUniquePairPerms.CanExecutePatterns}");
                        ImGui.Text($"CanExecuteTriggers: {clientPair.UserPairUniquePairPerms.CanExecuteTriggers}");
                        ImGui.Text($"CanToyboxTriggerCreators: {clientPair.UserPairUniquePairPerms.CanCreateTriggers}");
                        ImGui.Text($"CanSendTriggers: {clientPair.UserPairUniquePairPerms.CanSendTriggers}");
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
                    if (ImGui.CollapsingHeader($"{clientPair.UserData.UID}'s Edit Access || {_serverConfigurationManager.GetNicknameForUid(clientPair.UserData.UID)}"))
                    {
                        ImGui.Text("Coming soon!");
                    }
                }
                if (clientPair.UserPairAppearanceData != null)
                {
                    if (ImGui.CollapsingHeader($"{clientPair.UserData.UID}'s Appearance Data || {_serverConfigurationManager.GetNicknameForUid(clientPair.UserData.UID)}"))
                    {
                        ImGui.Text($"SlotOneGagType: {clientPair.UserPairAppearanceData.SlotOneGagType}");
                        ImGui.Text($"SlotOneGagPadlock: {clientPair.UserPairAppearanceData.SlotOneGagPadlock}");
                        ImGui.Text($"SlotOneGagPassword: {clientPair.UserPairAppearanceData.SlotOneGagPassword}");
                        ImGui.Text($"SlotOneGagTimer: {clientPair.UserPairAppearanceData.SlotOneGagTimer}");
                        ImGui.Text($"SlotOneGagAssigner: {clientPair.UserPairAppearanceData.SlotOneGagAssigner}");
                        ImGui.Separator();
                        ImGui.Text($"SlotTwoGagType: {clientPair.UserPairAppearanceData.SlotTwoGagType}");
                        ImGui.Text($"SlotTwoGagPadlock: {clientPair.UserPairAppearanceData.SlotTwoGagPadlock}");
                        ImGui.Text($"SlotTwoGagPassword: {clientPair.UserPairAppearanceData.SlotTwoGagPassword}");
                        ImGui.Text($"SlotTwoGagTimer: {clientPair.UserPairAppearanceData.SlotTwoGagTimer}");
                        ImGui.Text($"SlotTwoGagAssigner: {clientPair.UserPairAppearanceData.SlotTwoGagAssigner}");
                        ImGui.Separator();
                        ImGui.Text($"SlotThreeGagType: {clientPair.UserPairAppearanceData.SlotThreeGagType}");
                        ImGui.Text($"SlotThreeGagPadlock: {clientPair.UserPairAppearanceData.SlotThreeGagPadlock}");
                        ImGui.Text($"SlotThreeGagPassword: {clientPair.UserPairAppearanceData.SlotThreeGagPassword}");
                        ImGui.Text($"SlotThreeGagTimer: {clientPair.UserPairAppearanceData.SlotThreeGagTimer}");
                        ImGui.Text($"SlotThreeGagAssigner: {clientPair.UserPairAppearanceData.SlotThreeGagAssigner}");
                    }
                }
                if (clientPair.UserPairWardrobeData != null)
                {
                    if (ImGui.CollapsingHeader($"{clientPair.UserData.UID}'s Wardrobe Data || {_serverConfigurationManager.GetNicknameForUid(clientPair.UserData.UID)}"))
                    {
                        ImGui.Text($"OutfitList:");
                        ImGui.Indent();
                        foreach (var outfit in clientPair.UserPairWardrobeData.OutfitNames)
                        {
                            ImGui.Text($"{outfit}");
                        }
                        ImGui.Unindent();
                        ImGui.Text($"ActiveSetName: {clientPair.UserPairWardrobeData.ActiveSetName}");
                        ImGui.Text($"ActiveSetDescription: {clientPair.UserPairWardrobeData.ActiveSetDescription}");
                        ImGui.Text($"ActiveSetEnabledBy: {clientPair.UserPairWardrobeData.ActiveSetEnabledBy}");
                        ImGui.Text($"ActiveSetIsLocked: {clientPair.UserPairWardrobeData.ActiveSetIsLocked}");
                        ImGui.Text($"ActiveSetLockedBy: {clientPair.UserPairWardrobeData.ActiveSetLockedBy}");
                        ImGui.Text($"ActiveSetLockTime: {clientPair.UserPairWardrobeData.ActiveSetLockTime}");
                    }
                }
                if (clientPair.UserPairAliasData != null)
                {
                    if (ImGui.CollapsingHeader($"{clientPair.UserData.UID}'s Alias Data || {_serverConfigurationManager.GetNicknameForUid(clientPair.UserData.UID)}"))
                    {
                        foreach (var alias in clientPair.UserPairAliasData.AliasList)
                        {
                            var tmptext = alias.Enabled ? "Enabled" : "Disabled";
                            ImGui.Text($"{tmptext} :: INPUT -> {alias.InputCommand}");
                            ImGui.Text($"OUTPUT -> {alias.OutputCommand}");
                        }
                    }
                }
                if (clientPair.UserPairPatternData != null)
                {
                    if (ImGui.CollapsingHeader($"{clientPair.UserData.UID}'s Pattern Data || {_serverConfigurationManager.GetNicknameForUid(clientPair.UserData.UID)}"))
                    {
                        foreach (var pattern in clientPair.UserPairPatternData.PatternList)
                        {
                            ImGui.Text($"Pattern Name: {pattern.Name}");
                            ImGui.Text($"Pattern Description: {pattern.Description}");
                            ImGui.Text($"Pattern Duration: {pattern.Duration}");
                            ImGui.Text($"Pattern IsActive: {pattern.IsActive}");
                            ImGui.Text($"Pattern ShouldLoop: {pattern.ShouldLoop}");
                        }
                    }
                }




                if (clientPair.HasCachedPlayer)
                {
                    ImGui.Text($"OnlineUser UID: {clientPair.CachedPlayerOnlineDto.User.UID}");
                    ImGui.Text($"OnlineUser Alias: {clientPair.CachedPlayerOnlineDto.User.Alias}");
                    ImGui.Text($"OnlineUser Identifier: {clientPair.CachedPlayerOnlineDto.Ident}");
                    ImGui.Text($"HasCachedPlayer? : {clientPair.HasCachedPlayer}");
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


    private void DrawGlobalSettings()
    {
        _lastTab = "Global Settings";

        bool cmdsFromFriends = PlayerGlobalPerms.CommandsFromFriends;
        bool cmdsFromParty = PlayerGlobalPerms.CommandsFromParty;
        bool liveChatGarblerActive = PlayerGlobalPerms.LiveChatGarblerActive;
        bool liveChatGarblerLocked = PlayerGlobalPerms.LiveChatGarblerLocked;

        bool wardrobeEnabled = PlayerGlobalPerms.WardrobeEnabled;
        bool itemAutoEquip = PlayerGlobalPerms.ItemAutoEquip;
        bool restraintSetAutoEquip = PlayerGlobalPerms.RestraintSetAutoEquip;

        bool puppeteerEnabled = PlayerGlobalPerms.PuppeteerEnabled;
        string globalTriggerPhrase = PlayerGlobalPerms.GlobalTriggerPhrase;
        bool globalAllowSitRequests = PlayerGlobalPerms.GlobalAllowSitRequests;
        bool globalAllowMotionRequests = PlayerGlobalPerms.GlobalAllowMotionRequests;
        bool globalAllowAllRequests = PlayerGlobalPerms.GlobalAllowAllRequests;

        bool moodlesEnabled = PlayerGlobalPerms.MoodlesEnabled;

        bool toyboxEnabled = PlayerGlobalPerms.ToyboxEnabled;
        bool lockToyboxUI = PlayerGlobalPerms.LockToyboxUI;
        bool toyIsActive = PlayerGlobalPerms.ToyIsActive;
        bool spatialVibratorAudio = PlayerGlobalPerms.SpatialVibratorAudio;


        _uiShared.BigText("Global Settings");

        if (ImGui.Checkbox("Allow GagSpeak Commands from In-Game Friend list", ref cmdsFromFriends))
        {
            PlayerGlobalPerms.CommandsFromFriends = cmdsFromFriends;
            // TODO: Perform a mediator call that we have updated a permission.

        }
        _uiShared.DrawHelpText("If enabled, GagSpeak commands can be sent from friends in your friend list In-Game. Even if they are not paired.");

        if (ImGui.Checkbox("Allow GagSpeak Commands from In-Game Party list", ref cmdsFromParty))
        {
            PlayerGlobalPerms.CommandsFromParty = cmdsFromParty;
            // TODO: Perform a mediator call that we have updated a permission.

        }
        _uiShared.DrawHelpText("If enabled, GagSpeak commands can be sent from party members in your party list In-Game. Even if they are not paired.");

        using (ImRaii.Disabled(liveChatGarblerLocked))
        {
            if (ImGui.Checkbox("Enable Live Chat Garbler", ref liveChatGarblerActive))
            {
                PlayerGlobalPerms.LiveChatGarblerActive = liveChatGarblerActive;
                // TODO: Perform a mediator call that we have updated a permission.

            }
            _uiShared.DrawHelpText("If enabled, the Live Chat Garbler will garble your chat messages in-game. (This is done server-side, others will see it too)");
        }



        ImGui.Separator();
        _uiShared.BigText("Wardrobe");

        if (ImGui.Checkbox("Enable Wardrobe", ref wardrobeEnabled))
        {
            PlayerGlobalPerms.WardrobeEnabled = wardrobeEnabled;
            // TODO: Perform a mediator call that we have updated a permission.

        }
        _uiShared.DrawHelpText("If enabled, the all glamourer / penumbra / visual display information will become functional.");

        using (ImRaii.Disabled(!wardrobeEnabled))
        {
            using var indent = ImRaii.PushIndent();

            if (ImGui.Checkbox("Allow Items to be Auto-Equipped", ref itemAutoEquip))
            {
                PlayerGlobalPerms.ItemAutoEquip = itemAutoEquip;
                // TODO: Perform a mediator call that we have updated a permission.

            }
            _uiShared.DrawHelpText("Allows Glamourer to bind glamours to your character while gagged. These glamour's are defined by the Gag Storage.");

            if (ImGui.Checkbox("Allow Restraint Sets to be Auto-Equipped", ref restraintSetAutoEquip))
            {
                PlayerGlobalPerms.RestraintSetAutoEquip = restraintSetAutoEquip;
                // TODO: Perform a mediator call that we have updated a permission.

            }
            _uiShared.DrawHelpText("Allows Glamourer to bind restraint sets to your character. Restraint sets can be created in the Wardrobe Interface.");
        }



        ImGui.Separator();
        _uiShared.BigText("Puppeteer");

        if (ImGui.Checkbox("Enable Puppeteer", ref puppeteerEnabled))
        {
            PlayerGlobalPerms.PuppeteerEnabled = puppeteerEnabled;
            // TODO: Perform a mediator call that we have updated a permission.

        }
        _uiShared.DrawHelpText("If enabled, the Puppeteer component will become functional.");

        using (ImRaii.Disabled(!puppeteerEnabled))
        {
            using var indent = ImRaii.PushIndent();

            ImGui.SetNextItemWidth(200 * ImGuiHelpers.GlobalScale);
            if (ImGui.InputText("Global Trigger Phrase", ref globalTriggerPhrase, 100, ImGuiInputTextFlags.EnterReturnsTrue))
            {
                PlayerGlobalPerms.GlobalTriggerPhrase = globalTriggerPhrase;
                // TODO: Perform a mediator call that we have updated a permission.

            }
            _uiShared.DrawHelpText("The global trigger phrase that will be used to trigger puppeteer commands.\n" +
                "LEAVE THIS FIELD BLANK TO HAVE NO GLOBAL TRIGGER PHRASE");

            if (ImGui.Checkbox("Globally Allow Sit Requests", ref globalAllowSitRequests))
            {
                PlayerGlobalPerms.GlobalAllowSitRequests = globalAllowSitRequests;
                // TODO: Perform a mediator call that we have updated a permission.

            }
            _uiShared.DrawHelpText("If enabled, the user will allow sit requests to be sent to them.");

            if (ImGui.Checkbox("Globally Allow Motion Requests", ref globalAllowMotionRequests))
            {
                PlayerGlobalPerms.GlobalAllowMotionRequests = globalAllowMotionRequests;
                // TODO: Perform a mediator call that we have updated a permission.

            }
            _uiShared.DrawHelpText("If enabled, the user will allow motion requests to be sent to them.");

            if (ImGui.Checkbox("Globally Allow All Requests", ref globalAllowAllRequests))
            {
                PlayerGlobalPerms.GlobalAllowAllRequests = globalAllowAllRequests;
                // TODO: Perform a mediator call that we have updated a permission.

            }
            _uiShared.DrawHelpText("If enabled, the user will allow all requests to be sent to them.");
        }


        ImGui.Separator();
        _uiShared.BigText("Moodles");

        if (ImGui.Checkbox("Enable Moodles", ref moodlesEnabled))
        {
            PlayerGlobalPerms.MoodlesEnabled = moodlesEnabled;
            // TODO: Perform a mediator call that we have updated a permission.

        }
        _uiShared.DrawHelpText("If enabled, the moodles component will become functional.");



        ImGui.Separator();
        _uiShared.BigText("Toybox");

        if (ImGui.Checkbox("Enable Toybox", ref toyboxEnabled))
        {
            PlayerGlobalPerms.ToyboxEnabled = toyboxEnabled;
            // TODO: Perform a mediator call that we have updated a permission.

        }
        _uiShared.DrawHelpText("If enabled, the toybox component will become functional.");
    }

    private void DrawPreferences()
    {
        _lastTab = "Preferences";

        // the nicknames section
        _uiShared.BigText("Nicknames");

        // see if the user wants to allow a popup to create nicknames upon adding a paired user
        var openPopupOnAddition = _configService.Current.OpenPopupOnAdd;
        if (ImGui.Checkbox("Open Nickname Popup when adding a GagSpeak user", ref openPopupOnAddition))
        {
            _configService.Current.OpenPopupOnAdd = openPopupOnAddition;
            _configService.Save();
        }
        _uiShared.DrawHelpText("When enabled, a popup will automatically display after adding another user," +
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
        if (_serverConfigurationManager.CurrentServer.SecretKeys.Any())
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
                    ImGui.SetNextItemWidth(175 * ImGuiHelpers.GlobalScale);
                    ImGui.InputText("##CharaName" + charaName + i, ref charaName, 32);

                    ImGui.SameLine();
                    // grab the world the player is in.
                    var worldIdx = (ushort)item.WorldId;
                    var worldName = _uiShared.WorldData[worldIdx];
                    ImGui.SetNextItemWidth(125 * ImGuiHelpers.GlobalScale);
                    ImGui.InputText("##CharaWorld" + charaName + i, ref worldName, 32);
                }
                finally
                {
                    ImGui.EndDisabled();
                }
                ImGui.SameLine();
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
                ImGui.SetNextItemWidth(510 * ImGuiHelpers.GlobalScale);
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

            ImGui.Separator();
            // draw debug information for character information.
            ImGui.Text("PlayerCharacter Debug Information:");
            if (ImGui.CollapsingHeader("Global Data")) { DrawGlobalInfo(); }
            if (ImGui.CollapsingHeader("Appearance Data")) { DrawAppearanceInfo(); }
            if (ImGui.CollapsingHeader("Wardrobe Data")) { DrawWardrobeInfo(); }

            if (ImGui.CollapsingHeader("AliasData"))
            {
                foreach (var alias in _playerCharacterManager.GetAllAliasListKeys())
                {
                    var aliasData = _playerCharacterManager.GetAliasData(alias);
                    if (ImGui.CollapsingHeader($"Alias Data for {alias}"))
                    {
                        ImGui.Text("List of Alias's For this User:");
                        // begin a table.
                        using var table = ImRaii.Table($"##table-for-{alias}", 2);
                        if (!table) { return; }

                        using var spacing = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, ImGui.GetStyle().ItemInnerSpacing);
                        ImGui.TableSetupColumn("If You Say:", ImGuiTableColumnFlags.WidthFixed, ImGuiHelpers.GlobalScale * 100);
                        ImGui.TableSetupColumn("They will Execute:", ImGuiTableColumnFlags.WidthStretch);

                        foreach (var aliasTrigger in aliasData.AliasList)
                        {
                            ImGui.Separator();
                            ImGui.Text("[INPUT TRIGGER]: ");
                            ImGui.SameLine();
                            ImGui.Text(aliasTrigger.InputCommand);
                            ImGui.NewLine();
                            ImGui.Text("[OUTPUT RESPONSE]: ");
                            ImGui.SameLine();
                            ImGui.Text(aliasTrigger.OutputCommand);
                        }
                    }
                }
            }

            if (ImGui.CollapsingHeader("Patterns Data")) { DrawPatternsInfo(); }
        }
    }

    private void DrawGlobalInfo()
    {
        var globalPerms = _playerCharacterManager.GlobalPerms;
        ImGui.Text($"Safeword: {globalPerms.Safeword}");
        ImGui.Text($"SafewordUsed: {globalPerms.SafewordUsed}");
        ImGui.Text($"CommandsFromFriends: {globalPerms.CommandsFromFriends}");
        ImGui.Text($"CommandsFromParty: {globalPerms.CommandsFromParty}");
        ImGui.Text($"LiveChatGarblerActive: {globalPerms.LiveChatGarblerActive}");
        ImGui.Text($"LiveChatGarblerLocked: {globalPerms.LiveChatGarblerLocked}");
        ImGui.Separator();
        ImGui.Text($"WardrobeEnabled: {globalPerms.WardrobeEnabled}");
        ImGui.Text($"ItemAutoEquip: {globalPerms.ItemAutoEquip}");
        ImGui.Text($"RestraintSetAutoEquip: {globalPerms.RestraintSetAutoEquip}");
        ImGui.Text($"LockGagStorageOnGagLock: {globalPerms.LockGagStorageOnGagLock}");
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
        ImGui.Text("Underlayer Gag:");
        ImGui.Indent();
        ImGui.Text($"Gag Type: {appearanceData.SlotOneGagType}");
        ImGui.Text($"Gag Padlock: {appearanceData.SlotOneGagPadlock}");
        ImGui.Text($"Gag Password: {appearanceData.SlotOneGagPassword}");
        ImGui.Text($"Gag Expiration Timer: {appearanceData.SlotOneGagTimer}");
        ImGui.Text($"Gag Assigner: {appearanceData.SlotOneGagAssigner}");
        ImGui.Unindent();
        ImGui.Separator();
        ImGui.Text("Middle Gag:");
        ImGui.Indent();
        ImGui.Text($"Gag Type: {appearanceData.SlotTwoGagType}");
        ImGui.Text($"Gag Padlock: {appearanceData.SlotTwoGagPadlock}");
        ImGui.Text($"Gag Password: {appearanceData.SlotTwoGagPassword}");
        ImGui.Text($"Gag Expiration Timer: {appearanceData.SlotTwoGagTimer}");
        ImGui.Text($"Gag Assigner: {appearanceData.SlotTwoGagAssigner}");
        ImGui.Unindent();
        ImGui.Separator();
        ImGui.Text("Overlayer Gag:");
        ImGui.Indent();
        ImGui.Text($"Gag Type: {appearanceData.SlotThreeGagType}");
        ImGui.Text($"Gag Padlock: {appearanceData.SlotThreeGagPadlock}");
        ImGui.Text($"Gag Password: {appearanceData.SlotThreeGagPassword}");
        ImGui.Text($"Gag Expiration Timer: {appearanceData.SlotThreeGagTimer}");
        ImGui.Text($"Gag Assigner: {appearanceData.SlotThreeGagAssigner}");
        ImGui.Unindent();
    }

    private void DrawWardrobeInfo()
    {
        var wardrobeData = _playerCharacterManager.WardrobeData;
        ImGui.Text("Wardrobe Outfits:");
        ImGui.Indent();
        foreach (var item in wardrobeData.OutfitNames)
        {
            ImGui.Text(item);
        }
        ImGui.Unindent();
        if (wardrobeData.ActiveSetName != string.Empty)
        {
            ImGui.Text("Active Set Info: ");
            ImGui.Indent();
            ImGui.Text($"Name: {wardrobeData.ActiveSetName}");
            ImGui.Text($"Description: {wardrobeData.ActiveSetDescription}");
            ImGui.Text($"Enabled By: {wardrobeData.ActiveSetEnabledBy}");
            ImGui.Text($"Is Locked: {wardrobeData.ActiveSetIsLocked}");
            ImGui.Text($"Locked By: {wardrobeData.ActiveSetLockedBy}");
            ImGui.Text($"Locked Until: {wardrobeData.ActiveSetLockTime}");
            ImGui.Unindent();
        }
    }

    private void DrawPatternsInfo()
    {
        var patternData = _playerCharacterManager.PatternData;
        foreach (var item in patternData.PatternList)
        {
            ImGui.Text($"Info for Pattern: {item.Name}");
            ImGui.Indent();
            ImGui.Text($"Description: {item.Description}");
            ImGui.Text($"Duration: {item.Duration}");
            ImGui.Text($"Is Active: {item.IsActive}");
            ImGui.Text($"Should Loop: {item.ShouldLoop}");
            ImGui.Unindent();
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
            ImGui.TextUnformatted("Server is " + _serverConfigurationManager.CurrentServer!.ServerName + ":");
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
        // draw our seperator
        ImGui.Separator();
        // draw out the tab bar for us.
        if (ImGui.BeginTabBar("mainTabBar"))
        {
            if (ImGui.BeginTabItem("Global Settings"))
            {
                DrawGlobalSettings();
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
            ImGui.EndTabBar();
        }
    }
}
