using Dalamud.Game.ClientState.Objects.Types;
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
using GagSpeak.UpdateMonitoring.Triggers;
using GagSpeak.Utils;
using GagSpeak.WebAPI;

// if present in diadem (https://github.com/Infiziert90/DiademCalculator/blob/d74a22c58840a864cda12131fe2646dfc45209df/DiademCalculator/Windows/Main/MainWindow.cs#L12)

namespace GagSpeak.Achievements;
public partial class AchievementManager : DisposableMediatorSubscriberBase
{
    private readonly ClientConfigurationManager _clientConfigs;
    private readonly PlayerCharacterData _playerData;
    private readonly PairManager _pairManager;
    private readonly OnFrameworkService _frameworkUtils;
    private readonly ToyboxVibeService _vibeService;
    private readonly UnlocksEventManager _eventManager;
    private readonly INotificationManager _completionNotifier;
    private readonly IDutyState _dutyState;

    // Token used for updating achievement data.
    private CancellationTokenSource? _saveDataUpdateCTS;

    // Used for a special case with the WarriorOfLewd Achievement, to ensure a Cutscene has been longer than 5 seconds.

    // Stores the last time we disconnected via an exception. Controlled via HubFactory/ApiController.
    public static DateTime _lastDisconnectTime = DateTime.MinValue;

    // Dictates if our connection occured after an exception (within 5 minutes).
    private bool _reconnectedAfterException => DateTime.UtcNow - _lastDisconnectTime < TimeSpan.FromMinutes(5);

    // The save data for the achievements.
    public AchievementSaveData SaveData { get; private set; }

    public AchievementManager(ILogger<AchievementManager> logger, GagspeakMediator mediator,
        ClientConfigurationManager clientConfigs, PlayerCharacterData playerData, PairManager pairManager, 
        OnFrameworkService frameworkUtils, ToyboxVibeService vibeService, UnlocksEventManager eventManager, 
        INotificationManager completionNotifier, IDutyState dutyState) : base(logger, mediator)
    {
        _clientConfigs = clientConfigs;
        _playerData = playerData;
        _pairManager = pairManager;
        _frameworkUtils = frameworkUtils;
        _vibeService = vibeService;
        _eventManager = eventManager;
        _completionNotifier = completionNotifier;
        _dutyState = dutyState;

        Logger.LogInformation("Initializing Achievement Save Data", LoggerType.Achievements);
        SaveData = new AchievementSaveData(_completionNotifier);
        Logger.LogInformation("Initializing Achievement Save Data Achievements", LoggerType.Achievements);
        InitializeAchievements();
        Logger.LogInformation("Achievement Save Data Initialized", LoggerType.Achievements);

        // Check for when we are connected to the server, use the connection DTO to load our latest stored save data.
        Mediator.Subscribe<MainHubConnectedMessage>(this, (msg) =>
        {
            // if our connection dto is null, return.
            if (MainHub.ConnectionDto is null)
            {
                Logger.LogError("Connection DTO is null. Cannot proceed with AchievementManager Service.", LoggerType.Achievements);
                return;
            }

            if (_reconnectedAfterException)
            {
                Logger.LogInformation("Our Last Disconnect was due to an exception, loading from stored SaveData instead.", LoggerType.Achievements);
                // May cause some bugs, fiddle around with it if it does.
                _lastDisconnectTime = DateTime.MinValue;
            }
            else
            {
                if (!string.IsNullOrEmpty(MainHub.ConnectionDto.UserAchievements))
                {
                    Logger.LogInformation("Loading in AchievementData from ConnectionDto", LoggerType.Achievements);
                    LoadSaveDataDto(MainHub.ConnectionDto.UserAchievements);
                }
                else
                {
                    Logger.LogInformation("User has empty achievement Save Data. Creating new Save Data.", LoggerType.Achievements);
                }
            }

            // Begin the save cycle.
            _saveDataUpdateCTS?.Cancel();
            _saveDataUpdateCTS?.Dispose();
            _saveDataUpdateCTS = new CancellationTokenSource();
            _ = AchievementDataPeriodicUpdate(_saveDataUpdateCTS.Token);
        });

        Mediator.Subscribe<MainHubDisconnectedMessage>(this, _ => _saveDataUpdateCTS?.Cancel());

        #region Event Subscription
        _eventManager.Subscribe<OrderInteractionKind>(UnlocksEvent.OrderAction, OnOrderAction);
        _eventManager.Subscribe<GagLayer, GagType, bool, bool>(UnlocksEvent.GagAction, OnGagApplied);
        _eventManager.Subscribe<GagLayer, GagType, bool>(UnlocksEvent.GagRemoval, OnGagRemoval);
        _eventManager.Subscribe<GagType>(UnlocksEvent.PairGagAction, OnPairGagApplied);
        _eventManager.Subscribe(UnlocksEvent.GagUnlockGuessFailed, () => (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.RunningGag] as ConditionalAchievement)?.CheckCompletion());

        _eventManager.Subscribe<RestraintSet>(UnlocksEvent.RestraintUpdated, OnRestraintSetUpdated);
        _eventManager.Subscribe<RestraintSet, bool, string>(UnlocksEvent.RestraintApplicationChanged, OnRestraintApplied); // Apply on US
        _eventManager.Subscribe<RestraintSet, Padlocks, bool, string>(UnlocksEvent.RestraintLockChange, OnRestraintLock); // Lock on US
        _eventManager.Subscribe(UnlocksEvent.SoldSlave, () => (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.SoldSlave] as ConditionalAchievement)?.CheckCompletion());
        _eventManager.Subscribe(UnlocksEvent.AuctionedOff, () => (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.AuctionedOff] as ProgressAchievement)?.IncrementProgress());

        _eventManager.Subscribe<Guid, bool, string>(UnlocksEvent.PairRestraintApplied, OnPairRestraintApply);
        _eventManager.Subscribe<Guid, Padlocks, bool, string>(UnlocksEvent.PairRestraintLockChange, OnPairRestraintLockChange);

        _eventManager.Subscribe<PuppeteerMsgType>(UnlocksEvent.PuppeteerOrderSent, OnPuppeteerOrderSent);
        _eventManager.Subscribe<ushort>(UnlocksEvent.PuppeteerEmoteRecieved, OnPuppeteerRecievedEmoteOrder);
        _eventManager.Subscribe<bool>(UnlocksEvent.PuppeteerAccessGiven, OnPuppetAccessGiven);

        _eventManager.Subscribe<PatternInteractionKind, Guid, bool>(UnlocksEvent.PatternAction, OnPatternAction);

        _eventManager.Subscribe(UnlocksEvent.DeviceConnected, OnDeviceConnected);
        _eventManager.Subscribe(UnlocksEvent.TriggerFired, OnTriggerFired);
        _eventManager.Subscribe(UnlocksEvent.DeathRollCompleted, () => (SaveData.Achievements[AchievementModuleKind.Toybox].Achievements[ToyboxLabels.KinkyGambler] as ConditionalAchievement)?.CheckCompletion());
        _eventManager.Subscribe<NewState>(UnlocksEvent.AlarmToggled, _ => (SaveData.Achievements[AchievementModuleKind.Secrets].Achievements[SecretLabels.Experimentalist] as ConditionalAchievement)?.CheckCompletion());
        _eventManager.Subscribe(UnlocksEvent.ShockSent, OnShockSent);
        _eventManager.Subscribe(UnlocksEvent.ShockReceived, OnShockReceived);

        _eventManager.Subscribe<HardcoreAction, NewState, string, string>(UnlocksEvent.HardcoreForcedPairAction, OnHardcoreForcedPairAction);

        _eventManager.Subscribe(UnlocksEvent.RemoteOpened, () => (SaveData.Achievements[AchievementModuleKind.Remotes].Achievements[RemoteLabels.JustVibing] as ProgressAchievement)?.IncrementProgress());
        _eventManager.Subscribe(UnlocksEvent.VibeRoomCreated, () => (SaveData.Achievements[AchievementModuleKind.Remotes].Achievements[RemoteLabels.VibingWithFriends] as ProgressAchievement)?.IncrementProgress());
        _eventManager.Subscribe<NewState>(UnlocksEvent.VibratorsToggled, OnVibratorToggled);

        _eventManager.Subscribe(UnlocksEvent.PvpPlayerSlain, () => (SaveData.Achievements[AchievementModuleKind.Toybox].Achievements[ToyboxLabels.NothingCanStopMe] as ConditionalProgressAchievement)?.CheckTaskProgress(1));
        _eventManager.Subscribe(UnlocksEvent.ClientSlain, () => (SaveData.Achievements[AchievementModuleKind.Secrets].Achievements[SecretLabels.BadEndHostage] as ConditionalAchievement)?.CheckCompletion());
        _eventManager.Subscribe(UnlocksEvent.ClientOneHp, () => (SaveData.Achievements[AchievementModuleKind.Secrets].Achievements[SecretLabels.BoundgeeJumping] as ConditionalAchievement)?.CheckCompletion());
        _eventManager.Subscribe<XivChatType>(UnlocksEvent.ChatMessageSent, OnChatMessage);
        _eventManager.Subscribe<IGameObject, ushort, IGameObject>(UnlocksEvent.EmoteExecuted, OnEmoteExecuted);
        _eventManager.Subscribe(UnlocksEvent.TutorialCompleted, () => (SaveData.Achievements[AchievementModuleKind.Generic].Achievements[GenericLabels.TutorialComplete] as ProgressAchievement)?.CheckCompletion());
        _eventManager.Subscribe(UnlocksEvent.PairAdded, OnPairAdded);
        _eventManager.Subscribe(UnlocksEvent.PresetApplied, () => (SaveData.Achievements[AchievementModuleKind.Generic].Achievements[GenericLabels.AppliedFirstPreset] as ProgressAchievement)?.IncrementProgress());
        _eventManager.Subscribe(UnlocksEvent.GlobalSent, () => (SaveData.Achievements[AchievementModuleKind.Generic].Achievements[GenericLabels.HelloKinkyWorld] as ProgressAchievement)?.IncrementProgress());
        _eventManager.Subscribe(UnlocksEvent.CursedDungeonLootFound, OnCursedLootFound);
        _eventManager.Subscribe<string>(UnlocksEvent.EasterEggFound, OnIconClicked);
        _eventManager.Subscribe(UnlocksEvent.ChocoboRaceFinished, () => (SaveData.Achievements[AchievementModuleKind.Secrets].Achievements[SecretLabels.WildRide] as ConditionalAchievement)?.CheckCompletion());
        _eventManager.Subscribe<int>(UnlocksEvent.PlayersInProximity, (count) => (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.CrowdPleaser] as ConditionalThresholdAchievement)?.UpdateThreshold(count));
        _eventManager.Subscribe(UnlocksEvent.CutsceneInturrupted, () => (SaveData.Achievements[AchievementModuleKind.Generic].Achievements[GenericLabels.WarriorOfLewd] as ConditionalProgressAchievement)?.StartOverDueToInturrupt());

        Mediator.Subscribe<PairHandlerVisibleMessage>(this, _ => OnPairVisible());
        Mediator.Subscribe<CommendationsIncreasedMessage>(this, (msg) => OnCommendationsGiven(msg.amount));
        Mediator.Subscribe<PlaybackStateToggled>(this, (msg) => (SaveData.Achievements[AchievementModuleKind.Secrets].Achievements[SecretLabels.Experimentalist] as ConditionalAchievement)?.CheckCompletion());

        Mediator.Subscribe<SafewordUsedMessage>(this, _ => (SaveData.Achievements[AchievementModuleKind.Generic].Achievements[GenericLabels.KnowsMyLimits] as ProgressAchievement)?.IncrementProgress());

        Mediator.Subscribe<GPoseStartMessage>(this, _ => (SaveData.Achievements[AchievementModuleKind.Gags].Achievements[GagLabels.SayMmmph] as ConditionalProgressAchievement)?.BeginConditionalTask());
        Mediator.Subscribe<GPoseEndMessage>(this, _ => (SaveData.Achievements[AchievementModuleKind.Gags].Achievements[GagLabels.SayMmmph] as ConditionalProgressAchievement)?.FinishConditionalTask());
        Mediator.Subscribe<CutsceneBeginMessage>(this, _ => (SaveData.Achievements[AchievementModuleKind.Generic].Achievements[GenericLabels.WarriorOfLewd] as ConditionalProgressAchievement)?.BeginConditionalTask()); // starts Timer
        Mediator.Subscribe<CutsceneEndMessage>(this, _ => (SaveData.Achievements[AchievementModuleKind.Generic].Achievements[GenericLabels.WarriorOfLewd] as ConditionalProgressAchievement)?.FinishConditionalTask()); // ends/completes progress.

        Mediator.Subscribe<ZoneSwitchStartMessage>(this, (msg) => CheckOnZoneSwitchStart(msg.prevZone));
        Mediator.Subscribe<ZoneSwitchEndMessage>(this, _ =>
        {
            CheckOnZoneSwitchEnd();
            CheckDeepDungeonStatus();
        });

        IpcFastUpdates.GlamourEventFired += OnJobChange;
        ActionEffectMonitor.ActionEffectEntryEvent += OnActionEffectEvent;
        _dutyState.DutyStarted += OnDutyStart;
        _dutyState.DutyCompleted += OnDutyEnd;
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
        _eventManager.Unsubscribe<GagLayer, GagType, bool, bool>(UnlocksEvent.GagAction, OnGagApplied);
        _eventManager.Unsubscribe<GagLayer, GagType, bool>(UnlocksEvent.GagRemoval, OnGagRemoval);
        _eventManager.Unsubscribe<GagType>(UnlocksEvent.PairGagAction, OnPairGagApplied);
        _eventManager.Unsubscribe(UnlocksEvent.GagUnlockGuessFailed, () => (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.RunningGag] as ConditionalAchievement)?.CheckCompletion());

        _eventManager.Unsubscribe<RestraintSet>(UnlocksEvent.RestraintUpdated, OnRestraintSetUpdated);
        _eventManager.Unsubscribe<RestraintSet, bool, string>(UnlocksEvent.RestraintApplicationChanged, OnRestraintApplied); // Apply on US
        _eventManager.Unsubscribe<RestraintSet, Padlocks, bool, string>(UnlocksEvent.RestraintLockChange, OnRestraintLock); // Lock on US
        _eventManager.Unsubscribe(UnlocksEvent.SoldSlave, () => (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.SoldSlave] as ConditionalAchievement)?.CheckCompletion());
        _eventManager.Unsubscribe(UnlocksEvent.AuctionedOff, () => (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.AuctionedOff] as ProgressAchievement)?.IncrementProgress());

        _eventManager.Unsubscribe<Guid, bool, string>(UnlocksEvent.PairRestraintApplied, OnPairRestraintApply);
        _eventManager.Unsubscribe<Guid, Padlocks, bool, string>(UnlocksEvent.PairRestraintLockChange, OnPairRestraintLockChange);

        _eventManager.Unsubscribe<PuppeteerMsgType>(UnlocksEvent.PuppeteerOrderSent, OnPuppeteerOrderSent);
        _eventManager.Unsubscribe<ushort>(UnlocksEvent.PuppeteerEmoteRecieved, OnPuppeteerRecievedEmoteOrder);
        _eventManager.Unsubscribe<bool>(UnlocksEvent.PuppeteerAccessGiven, OnPuppetAccessGiven);

        _eventManager.Unsubscribe<PatternInteractionKind, Guid, bool>(UnlocksEvent.PatternAction, OnPatternAction);

        _eventManager.Unsubscribe(UnlocksEvent.DeviceConnected, OnDeviceConnected);
        _eventManager.Unsubscribe(UnlocksEvent.TriggerFired, OnTriggerFired);
        _eventManager.Unsubscribe(UnlocksEvent.DeathRollCompleted, () => (SaveData.Achievements[AchievementModuleKind.Toybox].Achievements[ToyboxLabels.KinkyGambler] as ConditionalAchievement)?.CheckCompletion());
        _eventManager.Unsubscribe<NewState>(UnlocksEvent.AlarmToggled, _ => (SaveData.Achievements[AchievementModuleKind.Secrets].Achievements[SecretLabels.Experimentalist] as ConditionalAchievement)?.CheckCompletion());
        _eventManager.Unsubscribe(UnlocksEvent.ShockSent, OnShockSent);
        _eventManager.Unsubscribe(UnlocksEvent.ShockReceived, OnShockReceived);

        _eventManager.Unsubscribe<HardcoreAction, NewState, string, string>(UnlocksEvent.HardcoreForcedPairAction, OnHardcoreForcedPairAction);

        _eventManager.Unsubscribe(UnlocksEvent.RemoteOpened, () => (SaveData.Achievements[AchievementModuleKind.Remotes].Achievements[RemoteLabels.JustVibing] as ProgressAchievement)?.CheckCompletion());
        _eventManager.Unsubscribe(UnlocksEvent.VibeRoomCreated, () => (SaveData.Achievements[AchievementModuleKind.Remotes].Achievements[RemoteLabels.VibingWithFriends] as ProgressAchievement)?.CheckCompletion());
        _eventManager.Unsubscribe<NewState>(UnlocksEvent.VibratorsToggled, OnVibratorToggled);

        _eventManager.Unsubscribe(UnlocksEvent.PvpPlayerSlain, () => (SaveData.Achievements[AchievementModuleKind.Toybox].Achievements[ToyboxLabels.NothingCanStopMe] as ConditionalProgressAchievement)?.CheckTaskProgress(1));
        _eventManager.Unsubscribe(UnlocksEvent.ClientSlain, () => (SaveData.Achievements[AchievementModuleKind.Secrets].Achievements[SecretLabels.BadEndHostage] as ConditionalAchievement)?.CheckCompletion());
        _eventManager.Unsubscribe<XivChatType>(UnlocksEvent.ChatMessageSent, OnChatMessage);
        _eventManager.Unsubscribe<IGameObject, ushort, IGameObject>(UnlocksEvent.EmoteExecuted, OnEmoteExecuted);
        _eventManager.Unsubscribe(UnlocksEvent.TutorialCompleted, () => (SaveData.Achievements[AchievementModuleKind.Generic].Achievements[GenericLabels.TutorialComplete] as ProgressAchievement)?.CheckCompletion());
        _eventManager.Unsubscribe(UnlocksEvent.PairAdded, OnPairAdded);
        _eventManager.Unsubscribe(UnlocksEvent.PresetApplied, () => (SaveData.Achievements[AchievementModuleKind.Generic].Achievements[GenericLabels.AppliedFirstPreset] as ProgressAchievement)?.IncrementProgress());
        _eventManager.Unsubscribe(UnlocksEvent.GlobalSent, () => (SaveData.Achievements[AchievementModuleKind.Generic].Achievements[GenericLabels.HelloKinkyWorld] as ProgressAchievement)?.IncrementProgress());
        _eventManager.Unsubscribe(UnlocksEvent.CursedDungeonLootFound, OnCursedLootFound);
        _eventManager.Unsubscribe<string>(UnlocksEvent.EasterEggFound, OnIconClicked);
        _eventManager.Unsubscribe(UnlocksEvent.ChocoboRaceFinished, () => (SaveData.Achievements[AchievementModuleKind.Secrets].Achievements[SecretLabels.WildRide] as ConditionalAchievement)?.CheckCompletion());
        _eventManager.Unsubscribe<int>(UnlocksEvent.PlayersInProximity, (count) => (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.CrowdPleaser] as ConditionalThresholdAchievement)?.UpdateThreshold(count));
        _eventManager.Unsubscribe(UnlocksEvent.CutsceneInturrupted, () => (SaveData.Achievements[AchievementModuleKind.Generic].Achievements[GenericLabels.WarriorOfLewd] as ConditionalProgressAchievement)?.StartOverDueToInturrupt());

        IpcFastUpdates.GlamourEventFired -= OnJobChange;
        ActionEffectMonitor.ActionEffectEntryEvent -= OnActionEffectEvent;
        _dutyState.DutyStarted -= OnDutyStart;
        _dutyState.DutyCompleted -= OnDutyEnd;
    }

    // Updater that sends our latest achievement Data to the server every 30 minutes.
    private async Task AchievementDataPeriodicUpdate(CancellationToken ct)
    {
        Logger.LogInformation("Starting SaveData Update Loop", LoggerType.Achievements);
        var random = new Random();
        while (!ct.IsCancellationRequested)
        {
            Logger.LogInformation("SaveData Update Task is running", LoggerType.Achievements);
            try
            {
                // wait randomly between 4 and 16 seconds before sending the data.
                // The 4 seconds gives us enough time to buffer any disconnects that inturrupt connection.
                await Task.Delay(TimeSpan.FromSeconds(random.Next(4, 16)), ct).ConfigureAwait(false);
                await SendUpdatedDataToServer();
            }
            catch (Exception)
            {
                Logger.LogDebug("Not sending Achievement SaveData due to disconnection canceling the loop.");
            }

            int delayMinutes = random.Next(20, 31); // Random delay between 20 and 30 minutes
            Logger.LogInformation("SaveData Update Task Completed, Firing Again in " + delayMinutes + " Minutes");
            await Task.Delay(TimeSpan.FromMinutes(delayMinutes), ct).ConfigureAwait(false);
        }
    }

    public Task ResetAchievementData()
    {
        // Reset SaveData
        SaveData = new AchievementSaveData(_completionNotifier);
        InitializeAchievements();
        Logger.LogInformation("Reset Achievement Data Completely!", LoggerType.Achievements);
        // Send this off to the server.
        SendUpdatedDataToServer();
        return Task.CompletedTask;
    }

    // Your existing method to send updated data to the server
    private Task SendUpdatedDataToServer()
    {
        var saveDataString = GetSaveDataDtoString();
        // Logic to send base64Data to the server
        Logger.LogInformation("Sending updated achievement data to the server", LoggerType.Achievements);
        Mediator.Publish(new AchievementDataUpdateMessage(saveDataString));
        return Task.CompletedTask;
    }

    public string GetSaveDataDtoString()
    {
        // get the Dto-Ready data object of our saveData
        LightSaveDataDto saveDataDto = SaveData.ToLightSaveDataDto();

        // condense it into the json and compress it.
        string json = JsonConvert.SerializeObject(saveDataDto);
        var compressed = json.Compress(6);
        string base64Data = Convert.ToBase64String(compressed);
        return base64Data;
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
