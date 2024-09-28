using Dalamud.Game.Text;
using Dalamud.Plugin.Services;
using GagSpeak.GagspeakConfiguration.Models;
using GagSpeak.PlayerData.Data;
using GagSpeak.PlayerData.Pairs;
using GagSpeak.PlayerData.Services;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using GagSpeak.Toybox.Services;
using GagSpeak.UpdateMonitoring;
using GagSpeak.Utils;
using GagSpeak.WebAPI;
using GagspeakAPI.Dto.Connection;
using GagspeakAPI.Extensions;
using Penumbra.GameData.Enums;

// if present in diadem (https://github.com/Infiziert90/DiademCalculator/blob/d74a22c58840a864cda12131fe2646dfc45209df/DiademCalculator/Windows/Main/MainWindow.cs#L12)

namespace GagSpeak.Achievements;
public partial class AchievementManager : DisposableMediatorSubscriberBase
{
    private readonly ApiController _apiController;
    private readonly ClientConfigurationManager _clientConfigs;
    private readonly PlayerCharacterData _playerData;
    private readonly PairManager _pairManager;
    private readonly OnFrameworkService _frameworkUtils;
    private readonly ToyboxVibeService _vibeService;
    private readonly UnlocksEventManager _eventManager;
    private readonly ItemIdVars _itemHelpers;
    private readonly INotificationManager _completionNotifier;
    
    private CancellationTokenSource? _saveDataUpdateCTS; // The token for updating achievement data.
    private bool _persistDataOnDisconnect = false; // If our data should persist on disconnect.
    private AchievementSaveData SaveData { get; init; } // The save data for the achievements.

    public AchievementManager(ILogger<AchievementManager> logger, GagspeakMediator mediator,
        ApiController apiController, ClientConfigurationManager clientConfigs, 
        PlayerCharacterData playerData, PairManager pairManager, OnFrameworkService frameworkUtils, 
        ToyboxVibeService vibeService, UnlocksEventManager eventManager, ItemIdVars itemHelpers, 
        INotificationManager completionNotifier) : base(logger, mediator)
    {
        _apiController = apiController;
        _clientConfigs = clientConfigs;
        _playerData = playerData;
        _pairManager = pairManager;
        _frameworkUtils = frameworkUtils;
        _vibeService = vibeService;
        _eventManager = eventManager;
        _itemHelpers = itemHelpers;
        _completionNotifier = completionNotifier;

        Logger.LogInformation("Initializing Achievement Save Data", LoggerType.Achievements);
        SaveData = new AchievementSaveData(_completionNotifier);
        Logger.LogInformation("Initializing Achievement Save Data Achievements", LoggerType.Achievements);
        InitializeAchievements();
        Logger.LogInformation("Achievement Save Data Loaded", LoggerType.Achievements);

        // Check for when we are connected to the server, use the connection DTO to load our latest stored save data.
        Mediator.Subscribe<ConnectedMessage>(this, (msg) =>
        {
            var connectionDto = msg.Connection;
            if (!string.IsNullOrEmpty(connectionDto.UserAchievements))
            {
                LoadSaveDataDto(connectionDto.UserAchievements);
            }
            else
            {
                Logger.LogWarning("User has empty achievement Save Data. Might just be a fresh user. But otherwise, report this.", LoggerType.Achievements);
            }
            _saveDataUpdateCTS?.Cancel();
            _saveDataUpdateCTS?.Dispose();
            _saveDataUpdateCTS = new CancellationTokenSource();
            _ = AchievementDataPeriodicUpdate(_saveDataUpdateCTS.Token);
        });

        Mediator.Subscribe<DisconnectedMessage>(this, (msg) =>
        {
            _saveDataUpdateCTS?.Cancel();
            // other stuff here.
        });

        #region Event Subscription
        _eventManager.Subscribe<OrderInteractionKind>(UnlocksEvent.OrderAction, OnOrderAction);
        _eventManager.Subscribe<GagLayer, GagType, bool>(UnlocksEvent.GagAction, OnGagApplied);
        _eventManager.Subscribe(UnlocksEvent.GagUnlockGuessFailed, () => (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.RunningGag] as ConditionalAchievement)?.CheckCompletion());

        _eventManager.Subscribe<RestraintSet, bool>(UnlocksEvent.RestraintApplied, OnRestraintApplied);
        _eventManager.Subscribe<RestraintSet, bool>(UnlocksEvent.RestraintLockChange, OnRestraintLock);
        // stopped here.
        _eventManager.Subscribe<Padlocks, bool, bool>(UnlocksEvent.PairRestraintLockChange, OnPairRestraintLockChange);

        _eventManager.Subscribe(UnlocksEvent.PuppeteerMessageSend, () => (SaveData.Achievements[AchievementModuleKind.Puppeteer].Achievements[PuppeteerLabels.MasterOfPuppets] as ProgressAchievement)?.IncrementProgress());
        _eventManager.Subscribe<bool>(UnlocksEvent.PuppeteerAccessGiven, OnPuppetAccessGiven);

        _eventManager.Subscribe<PatternInteractionKind, Guid, bool>(UnlocksEvent.PatternAction, OnPatternAction);
        _eventManager.Subscribe(UnlocksEvent.DeviceConnected, OnDeviceConnected);
        _eventManager.Subscribe(UnlocksEvent.TriggerFired, OnTriggerFired);
        _eventManager.Subscribe(UnlocksEvent.DeathRollCompleted, () => (SaveData.Achievements[AchievementModuleKind.Toybox].Achievements[ToyboxLabels.KinkyGambler] as ConditionalAchievement)?.CheckCompletion());
        _eventManager.Subscribe<NewState>(UnlocksEvent.AlarmToggled, _ => (SaveData.Achievements[AchievementModuleKind.Secrets].Achievements[SecretLabels.Experimentalist] as ProgressAchievement)?.IncrementProgress());
        _eventManager.Subscribe(UnlocksEvent.ShockSent, OnShockSent);
        _eventManager.Subscribe(UnlocksEvent.ShockReceived, OnShockReceived);

        _eventManager.Subscribe<HardcorePairActionKind, NewState, string, bool>(UnlocksEvent.HardcoreForcedPairAction, OnHardcoreForcedPairAction);

        _eventManager.Subscribe(UnlocksEvent.RemoteOpened, () => (SaveData.Achievements[AchievementModuleKind.Remotes].Achievements[RemoteLabels.JustVibing] as ProgressAchievement)?.CheckCompletion());
        _eventManager.Subscribe(UnlocksEvent.VibeRoomCreated, () => (SaveData.Achievements[AchievementModuleKind.Remotes].Achievements[RemoteLabels.VibingWithFriends] as ProgressAchievement)?.CheckCompletion());

        _eventManager.Subscribe(UnlocksEvent.PvpPlayerSlain, () => (SaveData.Achievements[AchievementModuleKind.Toybox].Achievements[ToyboxLabels.NothingCanStopMe] as ProgressAchievement)?.IncrementProgress());
        _eventManager.Subscribe(UnlocksEvent.ClientSlain, () => (SaveData.Achievements[AchievementModuleKind.Secrets].Achievements[SecretLabels.BadEndHostage] as ConditionalAchievement)?.CheckCompletion());
        _eventManager.Subscribe<XivChatType, string, string>(UnlocksEvent.ChatMessageSent, OnChatMessage);
        _eventManager.Subscribe<ulong, ushort, string, ulong>(UnlocksEvent.PlayerEmoteExecuted, OnEmoteExecuted);
        _eventManager.Subscribe<string>(UnlocksEvent.PuppeteerEmoteSent, OnPuppeteerEmoteSent);
        _eventManager.Subscribe(UnlocksEvent.TutorialCompleted, () => (SaveData.Achievements[AchievementModuleKind.Generic].Achievements[GenericLabels.TutorialComplete] as ProgressAchievement)?.CheckCompletion());
        _eventManager.Subscribe(UnlocksEvent.PairAdded, OnPairAdded);
        _eventManager.Subscribe(UnlocksEvent.PresetApplied, () => (SaveData.Achievements[AchievementModuleKind.Generic].Achievements[GenericLabels.AppliedFirstPreset] as ProgressAchievement)?.IncrementProgress());
        _eventManager.Subscribe(UnlocksEvent.GlobalSent, () => (SaveData.Achievements[AchievementModuleKind.Generic].Achievements[GenericLabels.HelloKinkyWorld] as ProgressAchievement)?.IncrementProgress());
        _eventManager.Subscribe(UnlocksEvent.CursedDungeonLootFound, OnCursedLootFound);
        _eventManager.Subscribe<string>(UnlocksEvent.EasterEggFound, OnIconClicked);
        _eventManager.Subscribe(UnlocksEvent.ChocoboRaceFinished, () => (SaveData.Achievements[AchievementModuleKind.Secrets].Achievements[SecretLabels.WildRide] as ConditionalAchievement)?.CheckCompletion());

        IpcFastUpdates.GlamourEventFired += OnJobChange;

        Mediator.Subscribe<PairHandlerVisibleMessage>(this, _ => (SaveData.Achievements[AchievementModuleKind.Secrets].Achievements[SecretLabels.BondageClub] as ConditionalAchievement)?.CheckCompletion());
        Mediator.Subscribe<CommendationsIncreasedMessage>(this, (msg) => OnCommendationsGiven(msg.amount));
        Mediator.Subscribe<PlaybackStateToggled>(this, (msg) => (SaveData.Achievements[AchievementModuleKind.Secrets].Achievements[SecretLabels.Experimentalist] as ConditionalAchievement)?.CheckCompletion());

        Mediator.Subscribe<SafewordUsedMessage>(this, _ => (SaveData.Achievements[AchievementModuleKind.Generic].Achievements[GenericLabels.KnowsMyLimits] as ProgressAchievement)?.CheckCompletion());

        Mediator.Subscribe<GPoseStartMessage>(this, _ => (SaveData.Achievements[AchievementModuleKind.Gags].Achievements[GagLabels.SayMmmph] as ConditionalProgressAchievement)?.BeginConditionalTask());
        Mediator.Subscribe<GPoseEndMessage>(this, _ => (SaveData.Achievements[AchievementModuleKind.Gags].Achievements[GagLabels.SayMmmph] as ConditionalProgressAchievement)?.FinishConditionalTask());
        Mediator.Subscribe<CutsceneBeginMessage>(this, _ => (SaveData.Achievements[AchievementModuleKind.Generic].Achievements[GenericLabels.WarriorOfLewd] as ConditionalDurationAchievement)?.CheckCompletion());
        Mediator.Subscribe<CutsceneEndMessage>(this, _ => (SaveData.Achievements[AchievementModuleKind.Generic].Achievements[GenericLabels.WarriorOfLewd] as ConditionalDurationAchievement)?.ResetOrComplete());

        Mediator.Subscribe<ZoneSwitchEndMessage>(this, _ =>
        {
            CheckOnZoneSwitchEnd();
            CheckDeepDungeonStatus();
        });
        #endregion Event Subscription
    }

    public int TotalAchievements => SaveData.Achievements.Values.Sum(component => component.Achievements.Count);
    public int CompletedAchievementsCount => SaveData.Achievements.Values.Sum(component => component.Achievements.Count(x => x.Value.IsCompleted));
    public List<Achievement> AllAchievements => SaveData.Achievements.Values.SelectMany(component => component.Achievements.Values.Cast<Achievement>()).ToList();
    public AchievementComponent GetComponent(AchievementModuleKind type) => SaveData.Achievements[type];

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        _saveDataUpdateCTS?.Cancel();
        _saveDataUpdateCTS?.Dispose();

        _eventManager.Unsubscribe<OrderInteractionKind>(UnlocksEvent.OrderAction, OnOrderAction);
        _eventManager.Unsubscribe<GagLayer, GagType, bool>(UnlocksEvent.GagAction, OnGagApplied);
        _eventManager.Unsubscribe(UnlocksEvent.GagUnlockGuessFailed, () => (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.RunningGag] as ConditionalAchievement)?.CheckCompletion());

        _eventManager.Unsubscribe<RestraintSet, bool>(UnlocksEvent.RestraintApplied, OnRestraintApplied);
        _eventManager.Unsubscribe<RestraintSet, bool>(UnlocksEvent.RestraintLockChange, OnRestraintLock);
        _eventManager.Unsubscribe<Padlocks, bool, bool>(UnlocksEvent.PairRestraintLockChange, OnPairRestraintLockChange);

        _eventManager.Unsubscribe(UnlocksEvent.PuppeteerMessageSend, () => (SaveData.Achievements[AchievementModuleKind.Puppeteer].Achievements[PuppeteerLabels.MasterOfPuppets] as ProgressAchievement)?.IncrementProgress());
        _eventManager.Unsubscribe<bool>(UnlocksEvent.PuppeteerAccessGiven, OnPuppetAccessGiven);

        _eventManager.Unsubscribe<PatternInteractionKind, Guid, bool>(UnlocksEvent.PatternAction, OnPatternAction);
        _eventManager.Unsubscribe(UnlocksEvent.DeviceConnected, OnDeviceConnected);
        _eventManager.Unsubscribe(UnlocksEvent.TriggerFired, OnTriggerFired);
        _eventManager.Unsubscribe(UnlocksEvent.DeathRollCompleted, () => (SaveData.Achievements[AchievementModuleKind.Toybox].Achievements[ToyboxLabels.KinkyGambler] as ConditionalAchievement)?.CheckCompletion());
        _eventManager.Unsubscribe<NewState>(UnlocksEvent.AlarmToggled, _ => (SaveData.Achievements[AchievementModuleKind.Secrets].Achievements[SecretLabels.Experimentalist] as ProgressAchievement)?.IncrementProgress());
        _eventManager.Unsubscribe(UnlocksEvent.ShockSent, OnShockSent);
        _eventManager.Unsubscribe(UnlocksEvent.ShockReceived, OnShockReceived);

        _eventManager.Unsubscribe<HardcorePairActionKind, NewState, string, bool>(UnlocksEvent.HardcoreForcedPairAction, OnHardcoreForcedPairAction);

        _eventManager.Unsubscribe(UnlocksEvent.RemoteOpened, () => (SaveData.Achievements[AchievementModuleKind.Remotes].Achievements[RemoteLabels.JustVibing] as ProgressAchievement)?.CheckCompletion());
        _eventManager.Unsubscribe(UnlocksEvent.VibeRoomCreated, () => (SaveData.Achievements[AchievementModuleKind.Remotes].Achievements[RemoteLabels.VibingWithFriends] as ProgressAchievement)?.CheckCompletion());

        _eventManager.Unsubscribe(UnlocksEvent.PvpPlayerSlain, () => (SaveData.Achievements[AchievementModuleKind.Toybox].Achievements[ToyboxLabels.NothingCanStopMe] as ProgressAchievement)?.IncrementProgress());
        _eventManager.Unsubscribe(UnlocksEvent.ClientSlain, () => (SaveData.Achievements[AchievementModuleKind.Secrets].Achievements[SecretLabels.BadEndHostage] as ConditionalAchievement)?.CheckCompletion());
        _eventManager.Unsubscribe<XivChatType, string, string>(UnlocksEvent.ChatMessageSent, OnChatMessage);
        _eventManager.Unsubscribe<ulong, ushort, string, ulong>(UnlocksEvent.PlayerEmoteExecuted, OnEmoteExecuted);
        _eventManager.Unsubscribe<string>(UnlocksEvent.PuppeteerEmoteSent, OnPuppeteerEmoteSent);
        _eventManager.Unsubscribe(UnlocksEvent.TutorialCompleted, () => (SaveData.Achievements[AchievementModuleKind.Generic].Achievements[GenericLabels.TutorialComplete] as ProgressAchievement)?.CheckCompletion());
        _eventManager.Unsubscribe(UnlocksEvent.PairAdded, OnPairAdded);
        _eventManager.Unsubscribe(UnlocksEvent.PresetApplied, () => (SaveData.Achievements[AchievementModuleKind.Generic].Achievements[GenericLabels.AppliedFirstPreset] as ProgressAchievement)?.IncrementProgress());
        _eventManager.Unsubscribe(UnlocksEvent.GlobalSent, () => (SaveData.Achievements[AchievementModuleKind.Generic].Achievements[GenericLabels.HelloKinkyWorld] as ProgressAchievement)?.IncrementProgress());
        _eventManager.Unsubscribe(UnlocksEvent.CursedDungeonLootFound, OnCursedLootFound);
        _eventManager.Unsubscribe<string>(UnlocksEvent.EasterEggFound, OnIconClicked);
        _eventManager.Unsubscribe(UnlocksEvent.ChocoboRaceFinished, () => (SaveData.Achievements[AchievementModuleKind.Secrets].Achievements[SecretLabels.WildRide] as ConditionalAchievement)?.CheckCompletion());

        IpcFastUpdates.GlamourEventFired -= OnJobChange;
    }


    // Updater that sends our latest achievement Data to the server every 30 minutes.
    private async Task AchievementDataPeriodicUpdate(CancellationToken ct)
    {
        Logger.LogInformation("Starting SaveData Update Loop", LoggerType.Achievements);
        while (!ct.IsCancellationRequested)
        {
            Logger.LogInformation("SaveData Update Task is running", LoggerType.Achievements);
            try
            {
                await SendUpdatedDataToServer().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to send updated achievement data to the server.");
            }
            Logger.LogInformation("SaveData Update Task Completed, Firing Again in 30 Minutes");

            await Task.Delay(TimeSpan.FromMinutes(30), ct).ConfigureAwait(false);
        }
    }

    // Your existing method to send updated data to the server
    private async Task SendUpdatedDataToServer()
    {
        // get the Dto-Ready data object of our saveData
        LightSaveDataDto saveDataDto = SaveData.ToLightSaveDataDto();

        // condense it into the json and compress it.
        string json = JsonConvert.SerializeObject(saveDataDto);
        var compressed = json.Compress(6);
        string base64Data = Convert.ToBase64String(compressed);

        // Logic to send base64Data to the server
        Logger.LogInformation("Sending updated achievement data to the server", LoggerType.Achievements);
        await _apiController.UserUpdateAchievementData(new(new(_apiController.UID), base64Data));
    }

    private void LoadSaveDataDto(string Base64saveDataToLoad)
    {
        try
        {
            Logger.LogDebug("Client Connected To Server with valid Achievement Data, loading in now!");
            var bytes = Convert.FromBase64String(Base64saveDataToLoad);
            var version = bytes[0];
            version = bytes.DecompressToString(out var decompressed);

            LightSaveDataDto item = JsonConvert.DeserializeObject<LightSaveDataDto>(decompressed) 
                ?? throw new Exception("Failed to deserialize achievement data from server.");

            // Update the local achievement data
            SaveData.LoadFromLightSaveDataDto(item);
            Logger.LogInformation("Achievement Data Loaded from Server", LoggerType.Achievements);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to apply achievement data from server, or data is empty.");
        }
    }
}
