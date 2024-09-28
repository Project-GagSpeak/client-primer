using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Text;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Component.GUI;
using GagSpeak.GagspeakConfiguration.Models;
using GagSpeak.PlayerData.Data;
using GagSpeak.PlayerData.Pairs;
using GagSpeak.PlayerData.Services;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using GagSpeak.Toybox.Services;
using GagSpeak.UpdateMonitoring;
using GagSpeak.Utils;
using GagspeakAPI.Extensions;
using Penumbra.GameData.Enums;

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
    private readonly ItemIdVars _itemHelpers;
    private readonly INotificationManager _completionNotifier;

    private Dictionary<AchievementType, AchievementComponent> Achievements = new();

    private Dictionary<string, bool> EasterEggIcons = new Dictionary<string, bool>()
    {
        {"Orders", false },
        {"Gags", false },
        {"Wardrobe", false },
        {"Puppeteer", false },
        {"Toybox", false }
    };

    [JsonIgnore]
    public int TotalAchievements => Achievements.Values.Sum(component =>
        component.Progress.Count + component.Conditional.Count + component.Duration.Count + 
        component.TimedProgress.Count + component.ConditionalProgress.Count + component.ConditionalDuration.Count);

    [JsonIgnore]
    public int CompletedAchievementsCount => Achievements.Values.Sum(component =>
        component.Progress.Count(x => x.Value.IsCompleted) + component.Conditional.Count(x => x.Value.IsCompleted) +
        component.Duration.Count(x => x.Value.IsCompleted) + component.TimedProgress.Count(x => x.Value.IsCompleted) +
        component.ConditionalProgress.Count(x => x.Value.IsCompleted) + component.ConditionalDuration.Count(x => x.Value.IsCompleted));

    [JsonIgnore]
    public List<Achievement> AllAchievements => Achievements.Values.SelectMany(component =>
    component.Progress.Values.Cast<Achievement>()
        .Concat(component.Conditional.Values.Cast<Achievement>())
        .Concat(component.Duration.Values.Cast<Achievement>())
        .Concat(component.TimedProgress.Values.Cast<Achievement>())
        .Concat(component.ConditionalProgress.Values.Cast<Achievement>())
        .Concat(component.ConditionalDuration.Values.Cast<Achievement>()))
        .ToList();


    public AchievementManager(ILogger<AchievementManager> logger,
        GagspeakMediator mediator, ClientConfigurationManager clientConfigs,
        PlayerCharacterData playerData, PairManager pairManager,
        OnFrameworkService frameworkUtils, ToyboxVibeService vibeService,
        UnlocksEventManager eventManager, ItemIdVars itemHelpers, 
        INotificationManager completionNotifier) : base(logger, mediator)
    {
        _clientConfigs = clientConfigs;
        _playerData = playerData;
        _pairManager = pairManager;
        _frameworkUtils = frameworkUtils;
        _vibeService = vibeService;
        _eventManager = eventManager;
        _itemHelpers = itemHelpers;
        _completionNotifier = completionNotifier;

        foreach (AchievementType type in Enum.GetValues(typeof(AchievementType)))
        {
            Achievements[type] = new AchievementComponent(_completionNotifier);
        }

        // initialize all achievements
        InitializeAchievements();

        // Subscribe to relevant events
        _eventManager.Subscribe<OrderInteractionKind>(UnlocksEvent.OrderAction, OnOrderAction);
        _eventManager.Subscribe<GagLayer, GagType, bool>(UnlocksEvent.GagAction, OnGagApplied);
        _eventManager.Subscribe(UnlocksEvent.GagUnlockGuessFailed, () => Achievements[AchievementType.Wardrobe].Conditional[WardrobeLabels.RunningGag].CheckCompletion());

        _eventManager.Subscribe<RestraintSet, bool>(UnlocksEvent.RestraintApplied, OnRestraintApplied);
        _eventManager.Subscribe<RestraintSet, bool>(UnlocksEvent.RestraintLockChange, OnRestraintLock);
        _eventManager.Subscribe<Padlocks, bool, bool>(UnlocksEvent.PairRestraintLockChange, OnPairRestraintLockChange);

        _eventManager.Subscribe(UnlocksEvent.PuppeteerMessageSend, () => Achievements[AchievementType.Puppeteer].Progress[PuppeteerLabels.MasterOfPuppets].IncrementProgress());
        _eventManager.Subscribe<bool>(UnlocksEvent.PuppeteerAccessGiven, OnPuppetAccessGiven);

        _eventManager.Subscribe<PatternInteractionKind, Guid, bool>(UnlocksEvent.PatternAction, OnPatternAction);
        _eventManager.Subscribe(UnlocksEvent.DeviceConnected, OnDeviceConnected);
        _eventManager.Subscribe(UnlocksEvent.TriggerFired, OnTriggerFired);
        _eventManager.Subscribe(UnlocksEvent.DeathRollCompleted, () => Achievements[AchievementType.Toybox].Achievements[ToyboxLabels.KinkyGambler].CheckCompletion());
        _eventManager.Subscribe<NewState>(UnlocksEvent.AlarmToggled, _ => Achievements[AchievementType.Secrets].Progress[SecretLabels.Experimentalist].IncrementProgress());
        _eventManager.Subscribe(UnlocksEvent.ShockSent, OnShockSent);
        _eventManager.Subscribe(UnlocksEvent.ShockReceived, OnShockReceived);

        _eventManager.Subscribe<HardcorePairActionKind, NewState, string, bool>(UnlocksEvent.HardcoreForcedPairAction, OnHardcoreForcedPairAction);

        _eventManager.Subscribe(UnlocksEvent.RemoteOpened, () => Achievements[AchievementType.Remotes].Progress[RemoteLabels.JustVibing].CheckCompletion());
        _eventManager.Subscribe(UnlocksEvent.VibeRoomCreated, () => Achievements[AchievementType.Remotes].Progress[RemoteLabels.VibingWithFriends].CheckCompletion());

        _eventManager.Subscribe(UnlocksEvent.PvpPlayerSlain, () => Achievements[AchievementType.Toybox].Progress[ToyboxLabels.NothingCanStopMe].IncrementProgress());
        _eventManager.Subscribe(UnlocksEvent.ClientSlain, () => Achievements[AchievementType.Secrets].Conditional[SecretLabels.BadEndHostage].CheckCompletion());
        _eventManager.Subscribe<XivChatType, string, string>(UnlocksEvent.ChatMessageSent, OnChatMessage);
        _eventManager.Subscribe<ulong, ushort, string, ulong>(UnlocksEvent.PlayerEmoteExecuted, OnEmoteExecuted);
        _eventManager.Subscribe<string>(UnlocksEvent.PuppeteerEmoteSent, OnPuppeteerEmoteSent);
        _eventManager.Subscribe(UnlocksEvent.TutorialCompleted, () => Achievements[AchievementType.Generic].Progress[GenericLabels.TutorialComplete].CheckCompletion());
        _eventManager.Subscribe(UnlocksEvent.PairAdded, OnPairAdded);
        _eventManager.Subscribe(UnlocksEvent.PresetApplied, () => Achievements[AchievementType.Generic].Progress[GenericLabels.AppliedFirstPreset].IncrementProgress());
        _eventManager.Subscribe(UnlocksEvent.GlobalSent, () => Achievements[AchievementType.Generic].Progress[GenericLabels.HelloKinkyWorld].IncrementProgress());
        _eventManager.Subscribe(UnlocksEvent.CursedDungeonLootFound, OnCursedLootFound);
        _eventManager.Subscribe<string>(UnlocksEvent.EasterEggFound, OnIconClicked);
        _eventManager.Subscribe(UnlocksEvent.ChocoboRaceFinished, () => Achievements[AchievementType.Secrets].Conditional[SecretLabels.WildRide].CheckCompletion());

        IpcFastUpdates.GlamourEventFired += OnJobChange;

        Mediator.Subscribe<PairHandlerVisibleMessage>(this, _ => Achievements[AchievementType.Secrets].Conditional[SecretLabels.BondageClub].CheckCompletion());
        Mediator.Subscribe<CommendationsIncreasedMessage>(this, (msg) => OnCommendationsGiven(msg.amount));
        Mediator.Subscribe<PlaybackStateToggled>(this, (msg) => Achievements[AchievementType.Secrets].Conditional[SecretLabels.Experimentalist].CheckCompletion());
        
        Mediator.Subscribe<SafewordUsedMessage>(this, _ => Achievements[AchievementType.Generic].Progress[GenericLabels.KnowsMyLimits].CheckCompletion());
        
        Mediator.Subscribe<GPoseStartMessage>(this, _ => Achievements[AchievementType.Gags].ConditionalProgress[GagLabels.SayMmmph].BeginConditionalTask());
        Mediator.Subscribe<GPoseEndMessage>(this, _ => Achievements[AchievementType.Gags].ConditionalProgress[GagLabels.SayMmmph].FinishConditionalTask());
        Mediator.Subscribe<CutsceneBeginMessage>(this, _ => Achievements[AchievementType.Generic].ConditionalDuration[GenericLabels.WarriorOfLewd].CheckCompletion());
        Mediator.Subscribe<CutsceneEndMessage>(this, _ => Achievements[AchievementType.Generic].ConditionalDuration[GenericLabels.WarriorOfLewd].ResetOrComplete());

        Mediator.Subscribe<ZoneSwitchEndMessage>(this, _ =>
        {
            if (_frameworkUtils.IsInMainCity)
            {
                Achievements[AchievementType.Hardcore].Conditional[HardcoreLabels.WalkOfShame].CheckCompletion();
            }

            // if present in diadem (for diamdem achievement)
            if (_frameworkUtils.ClientState.TerritoryType is 939)

                Achievements[AchievementType.Toybox].ConditionalDuration[ToyboxLabels.MotivationForRestoration].CheckCompletion();
            else
                Achievements[AchievementType.Toybox].ConditionalDuration[ToyboxLabels.MotivationForRestoration].ResetOrComplete();

            // if we are in a dungeon:
            if (_frameworkUtils.InDungeonOrDuty)
            {
                Achievements[AchievementType.Gags].ConditionalProgress[GagLabels.SilentButDeadly].BeginConditionalTask();
                Achievements[AchievementType.Hardcore].ConditionalProgress[HardcoreLabels.UCanTieThis].BeginConditionalTask();

                if (_frameworkUtils.PlayerJobRole is ActionRoles.Healer)
                    Achievements[AchievementType.Wardrobe].ConditionalProgress[WardrobeLabels.HealSlut].BeginConditionalTask();
            }
            else
            {
                if (Achievements[AchievementType.Hardcore].ConditionalProgress[HardcoreLabels.UCanTieThis].ConditionalTaskBegun)
                    Achievements[AchievementType.Hardcore].ConditionalProgress[HardcoreLabels.UCanTieThis].FinishConditionalTask();

                if (Achievements[AchievementType.Gags].ConditionalProgress[GagLabels.SilentButDeadly].ConditionalTaskBegun)
                    Achievements[AchievementType.Gags].ConditionalProgress[GagLabels.SilentButDeadly].FinishConditionalTask();

                if (Achievements[AchievementType.Wardrobe].ConditionalProgress[WardrobeLabels.HealSlut].ConditionalTaskBegun)
                    Achievements[AchievementType.Wardrobe].ConditionalProgress[WardrobeLabels.HealSlut].FinishConditionalTask();
            }

            // check stuff for deep dungeon achievements.
            CheckDeepDungeonStatus();
        });
    }

    public AchievementComponent GetComponent(AchievementType type) => Achievements[type];


    private void OnCommendationsGiven(int amount)
    {
        Achievements[AchievementType.Secrets].ConditionalProgress[SecretLabels.KinkyTeacher].CheckTaskProgress(amount);
        Achievements[AchievementType.Secrets].ConditionalProgress[SecretLabels.KinkyProfessor].CheckTaskProgress(amount);
        Achievements[AchievementType.Secrets].ConditionalProgress[SecretLabels.KinkyMentor].CheckTaskProgress(amount);
    }

    private void OnIconClicked(string windowLabel)
    {
        if (EasterEggIcons.ContainsKey(windowLabel))
        {
            if(EasterEggIcons[windowLabel])
                return;
            else
                EasterEggIcons[windowLabel] = true;
            // update progress.
            Achievements[AchievementType.Secrets].Progress[SecretLabels.TooltipLogos].IncrementProgress();
        }
    }

    private void CheckDeepDungeonStatus()
    {
        // Detect Specific Dungeon Types
        if (!UnlocksHelpers.InDeepDungeon()) return;

        var floor = UnlocksHelpers.GetFloor();
        if (floor is null) return;

        var deepDungeonType = _frameworkUtils.GetDeepDungeonType();
        if (deepDungeonType == null) return;

        if (_frameworkUtils.PartyListSize is 1)
            Achievements[AchievementType.Wardrobe].ConditionalProgress[WardrobeLabels.MyKinksRunDeeper].BeginConditionalTask();

        switch (deepDungeonType)
        {
            case DeepDungeonType.PalaceOfTheDead:
                if ((floor > 40 && floor <= 50) || (floor > 90 && floor <= 100))
                {
                    Achievements[AchievementType.Wardrobe].ConditionalProgress[WardrobeLabels.BondagePalace].BeginConditionalTask();
                    Achievements[AchievementType.Wardrobe].ConditionalProgress[WardrobeLabels.MyKinkRunsDeep].BeginConditionalTask();
                    if (floor is 50 || floor is 100)
                    {
                        Achievements[AchievementType.Wardrobe].ConditionalProgress[WardrobeLabels.BondagePalace].FinishConditionalTask();
                        Achievements[AchievementType.Wardrobe].ConditionalProgress[WardrobeLabels.MyKinkRunsDeep].FinishConditionalTask();
                        Achievements[AchievementType.Wardrobe].ConditionalProgress[WardrobeLabels.MyKinksRunDeeper].FinishConditionalTask();
                    }
                }
                break;
            case DeepDungeonType.HeavenOnHigh:
                if (floor > 20 && floor <= 30)
                {
                    Achievements[AchievementType.Wardrobe].ConditionalProgress[WardrobeLabels.HornyOnHigh].BeginConditionalTask();
                    Achievements[AchievementType.Wardrobe].ConditionalProgress[WardrobeLabels.MyKinkRunsDeep].BeginConditionalTask();
                    if (floor is 30)
                    {
                        Achievements[AchievementType.Wardrobe].ConditionalProgress[WardrobeLabels.BondagePalace].FinishConditionalTask();
                        Achievements[AchievementType.Wardrobe].ConditionalProgress[WardrobeLabels.MyKinkRunsDeep].FinishConditionalTask();
                        Achievements[AchievementType.Wardrobe].ConditionalProgress[WardrobeLabels.MyKinksRunDeeper].FinishConditionalTask();
                    }
                }
                break;
            case DeepDungeonType.EurekaOrthos:
                if (floor > 20 && floor <= 30)
                {
                    Achievements[AchievementType.Wardrobe].ConditionalProgress[WardrobeLabels.EurekaWhorethos].BeginConditionalTask();
                    Achievements[AchievementType.Wardrobe].ConditionalProgress[WardrobeLabels.MyKinkRunsDeep].BeginConditionalTask();
                    if (floor is 30)
                    {
                        Achievements[AchievementType.Wardrobe].ConditionalProgress[WardrobeLabels.EurekaWhorethos].FinishConditionalTask();
                        Achievements[AchievementType.Wardrobe].ConditionalProgress[WardrobeLabels.MyKinkRunsDeep].FinishConditionalTask();
                        Achievements[AchievementType.Wardrobe].ConditionalProgress[WardrobeLabels.MyKinksRunDeeper].FinishConditionalTask();
                    }
                }
                break;
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        _eventManager.Unsubscribe<OrderInteractionKind>(UnlocksEvent.OrderAction, OnOrderAction);
        _eventManager.Unsubscribe<GagLayer, GagType, bool>(UnlocksEvent.GagAction, OnGagApplied);
        _eventManager.Unsubscribe(UnlocksEvent.GagUnlockGuessFailed, () => Achievements[AchievementType.Wardrobe].Conditional[WardrobeLabels.RunningGag].CheckCompletion());

        _eventManager.Unsubscribe<RestraintSet, bool>(UnlocksEvent.RestraintApplied, OnRestraintApplied);
        _eventManager.Unsubscribe<RestraintSet, bool>(UnlocksEvent.RestraintLockChange, OnRestraintLock);
        _eventManager.Unsubscribe<Padlocks, bool, bool>(UnlocksEvent.PairRestraintLockChange, OnPairRestraintLockChange);

        _eventManager.Unsubscribe(UnlocksEvent.PuppeteerMessageSend, () => Achievements[AchievementType.Puppeteer].Progress[PuppeteerLabels.MasterOfPuppets].IncrementProgress());
        _eventManager.Unsubscribe<bool>(UnlocksEvent.PuppeteerAccessGiven, OnPuppetAccessGiven);

        _eventManager.Unsubscribe<PatternInteractionKind, Guid, bool>(UnlocksEvent.PatternAction, OnPatternAction);
        _eventManager.Unsubscribe(UnlocksEvent.DeviceConnected, OnDeviceConnected);
        _eventManager.Unsubscribe(UnlocksEvent.TriggerFired, OnTriggerFired);
        _eventManager.Unsubscribe(UnlocksEvent.DeathRollCompleted, () => Achievements[AchievementType.Toybox].Conditional[ToyboxLabels.KinkyGambler].CheckCompletion());
        _eventManager.Unsubscribe<NewState>(UnlocksEvent.AlarmToggled, _ => Achievements[AchievementType.Secrets].Progress[SecretLabels.Experimentalist].IncrementProgress());
        _eventManager.Unsubscribe(UnlocksEvent.ShockSent, OnShockSent);
        _eventManager.Unsubscribe(UnlocksEvent.ShockReceived, OnShockReceived);

        _eventManager.Unsubscribe<HardcorePairActionKind, NewState, string, bool>(UnlocksEvent.HardcoreForcedPairAction, OnHardcoreForcedPairAction);

        _eventManager.Unsubscribe(UnlocksEvent.RemoteOpened, () => Achievements[AchievementType.Remotes].Progress[RemoteLabels.JustVibing].CheckCompletion());
        _eventManager.Unsubscribe(UnlocksEvent.VibeRoomCreated, () => Achievements[AchievementType.Remotes].Progress[RemoteLabels.VibingWithFriends].CheckCompletion());

        _eventManager.Unsubscribe(UnlocksEvent.PvpPlayerSlain, () => Achievements[AchievementType.Toybox].Progress[ToyboxLabels.NothingCanStopMe].IncrementProgress());
        _eventManager.Unsubscribe(UnlocksEvent.ClientSlain, () => Achievements[AchievementType.Secrets].Conditional[SecretLabels.BadEndHostage].CheckCompletion());
        _eventManager.Unsubscribe<XivChatType, string, string>(UnlocksEvent.ChatMessageSent, OnChatMessage);
        _eventManager.Unsubscribe<ulong, ushort, string, ulong>(UnlocksEvent.PlayerEmoteExecuted, OnEmoteExecuted);
        _eventManager.Unsubscribe<string>(UnlocksEvent.PuppeteerEmoteSent, OnPuppeteerEmoteSent);
        _eventManager.Unsubscribe(UnlocksEvent.TutorialCompleted, () => Achievements[AchievementType.Generic].Progress[GenericLabels.TutorialComplete].CheckCompletion());
        _eventManager.Unsubscribe(UnlocksEvent.PairAdded, OnPairAdded);
        _eventManager.Unsubscribe(UnlocksEvent.PresetApplied, () => Achievements[AchievementType.Generic].Progress[GenericLabels.AppliedFirstPreset].IncrementProgress());
        _eventManager.Unsubscribe(UnlocksEvent.GlobalSent, () => Achievements[AchievementType.Generic].Progress[GenericLabels.HelloKinkyWorld].IncrementProgress());
        _eventManager.Unsubscribe(UnlocksEvent.CursedDungeonLootFound, OnCursedLootFound);
        _eventManager.Unsubscribe<string>(UnlocksEvent.EasterEggFound, OnIconClicked);
        _eventManager.Unsubscribe(UnlocksEvent.ChocoboRaceFinished, () => Achievements[AchievementType.Secrets].Conditional[SecretLabels.WildRide].CheckCompletion());

        IpcFastUpdates.GlamourEventFired -= OnJobChange;
    }

    private void OnOrderAction(OrderInteractionKind orderKind)
    {
        switch (orderKind)
        {
            case OrderInteractionKind.Completed:
                Achievements[AchievementType.Orders].Progress[OrderLabels.JustAVolunteer].IncrementProgress();
                Achievements[AchievementType.Orders].Progress[OrderLabels.AsYouCommand].IncrementProgress();
                Achievements[AchievementType.Orders].Progress[OrderLabels.AnythingForMyOwner].IncrementProgress();
                Achievements[AchievementType.Orders].Progress[OrderLabels.GoodDrone].IncrementProgress();
                break;
            case OrderInteractionKind.Fail:
                Achievements[AchievementType.Orders].Progress[OrderLabels.BadSlut].IncrementProgress();
                Achievements[AchievementType.Orders].Progress[OrderLabels.NeedsTraining].IncrementProgress();
                Achievements[AchievementType.Orders].Progress[OrderLabels.UsefulInOtherWays].IncrementProgress();
                break;
            case OrderInteractionKind.Create:
                Achievements[AchievementType.Orders].Progress[OrderLabels.NewSlaveOwner].IncrementProgress();
                Achievements[AchievementType.Orders].Progress[OrderLabels.TaskManager].IncrementProgress();
                Achievements[AchievementType.Orders].Progress[OrderLabels.MaidMaster].IncrementProgress();
                Achievements[AchievementType.Orders].Progress[OrderLabels.QueenOfDrones].IncrementProgress();
                break;
        }
    }

    private void OnGagApplied(GagLayer gagLayer, GagType gagType, bool isSelfApplied)
    {
        // if the gag is self applied
        if (isSelfApplied && gagType is not GagType.None)
        {
            Achievements[AchievementType.Gags].Progress[GagLabels.SelfApplied].IncrementProgress();
        }
        // if the gag is not self applied
        else
        {
            if (gagType is not GagType.None)
            {
                Achievements[AchievementType.Gags].Progress[GagLabels.ApplyToPair].IncrementProgress();
                Achievements[AchievementType.Gags].Progress[GagLabels.LookingForTheRightFit].IncrementProgress();
                Achievements[AchievementType.Gags].Progress[GagLabels.OralFixation].IncrementProgress();
                Achievements[AchievementType.Gags].Progress[GagLabels.AKinkForDrool].IncrementProgress();

                Achievements[AchievementType.Gags].Conditional[GagLabels.ShushtainableResource].CheckCompletion();

                Achievements[AchievementType.Gags].Duration[GagLabels.SpeechSilverSilenceGolden].StartTracking(gagType.GagName());
                Achievements[AchievementType.Gags].Duration[GagLabels.TheKinkyLegend].StartTracking(gagType.GagName());

                Achievements[AchievementType.Gags].TimedProgress[GagLabels.ATrueGagSlut].IncrementProgress();

                Achievements[AchievementType.Gags].ConditionalProgress[GagLabels.YourFavoriteNurse].CheckTaskProgress();

                Achievements[AchievementType.Secrets].Conditional[SecretLabels.Experimentalist].CheckCompletion();

                Achievements[AchievementType.Secrets].Conditional[SecretLabels.GaggedPleasure].CheckCompletion();
            }
        }
        // experimentalist
        Achievements[AchievementType.Secrets].Conditional[SecretLabels.Experimentalist].CheckCompletion();
        Achievements[AchievementType.Secrets].Conditional[SecretLabels.GaggedPleasure].CheckCompletion();
    }

    private void OnRestraintApplied(RestraintSet set, bool isSelfApplied)
    {
        Achievements[AchievementType.Wardrobe].Progress[WardrobeLabels.FirstTiemers].IncrementProgress();
        Achievements[AchievementType.Secrets].Conditional[SecretLabels.Experimentalist].CheckCompletion();

        // we were the applier
        if (isSelfApplied)
        {
            Achievements[AchievementType.Wardrobe].Progress[WardrobeLabels.SelfBondageEnthusiast].IncrementProgress();
        }
        // we were not the applier
        else
        {
            // start the "Auctioned Off" achievement
            Achievements[AchievementType.Wardrobe].ConditionalProgress[WardrobeLabels.AuctionedOff].BeginConditionalTask();

            // Achievements related to applying
            Achievements[AchievementType.Wardrobe].Progress[WardrobeLabels.DiDEnthusiast].IncrementProgress();
            Achievements[AchievementType.Wardrobe].TimedProgress[WardrobeLabels.BondageBunny].IncrementProgress();
            Achievements[AchievementType.Wardrobe].ConditionalDuration[WardrobeLabels.Bondodge].CheckCompletion();

            // see if valid for "cuffed-19"
            if (set.DrawData.TryGetValue(EquipSlot.Hands, out var handData) && handData.GameItem.Id != ItemIdVars.NothingItem(EquipSlot.Hands).Id)
            {
                Achievements[AchievementType.Wardrobe].Progress[WardrobeLabels.Cuffed19].IncrementProgress();
            }

            // check for dyes
            if (set.DrawData.Any(x => x.Value.GameStain.Stain1 != 0 || x.Value.GameStain.Stain2 != 0))
            {
                Achievements[AchievementType.Wardrobe].Progress[WardrobeLabels.ToDyeFor].IncrementProgress();
                Achievements[AchievementType.Wardrobe].Progress[WardrobeLabels.DyeAnotherDay].IncrementProgress();
                Achievements[AchievementType.Wardrobe].Progress[WardrobeLabels.DyeHard].IncrementProgress();
            }
        }
    }

    private void OnPairRestraintLockChange(Padlocks padlock, bool isUnlocking, bool wasAssigner)
    {
        // we have unlocked a pair.
        if (isUnlocking)
        {
            if (padlock is Padlocks.PasswordPadlock && !wasAssigner)
                Achievements[AchievementType.Wardrobe].Progress[WardrobeLabels.SoldSlave].IncrementProgress();

            Achievements[AchievementType.Wardrobe].Progress[WardrobeLabels.TheRescuer].IncrementProgress();
        }
        // we have locked a pair up.
        else
        {
            Achievements[AchievementType.Wardrobe].Progress[WardrobeLabels.RiggersFirstSession].IncrementProgress();
            Achievements[AchievementType.Wardrobe].Progress[WardrobeLabels.MyLittlePlaything].IncrementProgress();
            Achievements[AchievementType.Wardrobe].Progress[WardrobeLabels.SuitsYouBitch].IncrementProgress();
            Achievements[AchievementType.Wardrobe].Progress[WardrobeLabels.TiesThatBind].IncrementProgress();
            Achievements[AchievementType.Wardrobe].Progress[WardrobeLabels.SlaveTraining].IncrementProgress();
            Achievements[AchievementType.Wardrobe].Progress[WardrobeLabels.CeremonyOfEternalBondage].IncrementProgress();
        }
    }

    private void OnRestraintLock(RestraintSet set, bool isSelfApplied)
    {
        // we locked our set.
        if (isSelfApplied)
        {
            Achievements[AchievementType.Wardrobe].Progress[WardrobeLabels.RiggersFirstSession].IncrementProgress();
        }
        // someone else locked our set
        else
        {
            Achievements[AchievementType.Wardrobe].Progress[WardrobeLabels.FirstTimeBondage].IncrementProgress();
            Achievements[AchievementType.Wardrobe].Progress[WardrobeLabels.AmateurBondage].IncrementProgress();
            Achievements[AchievementType.Wardrobe].Progress[WardrobeLabels.ComfortRestraint].IncrementProgress();
            Achievements[AchievementType.Wardrobe].Progress[WardrobeLabels.DayInTheLifeOfABondageSlave].IncrementProgress();
            Achievements[AchievementType.Wardrobe].Progress[WardrobeLabels.AWeekInBondage].IncrementProgress();
            Achievements[AchievementType.Wardrobe].Progress[WardrobeLabels.AMonthInBondage].IncrementProgress();
        }
    }

    private void OnPuppetAccessGiven(bool wasAllPerms)
    {
        if (wasAllPerms) // All Perms access given to another pair.
            Achievements[AchievementType.Puppeteer].Progress[PuppeteerLabels.CompleteDevotion].IncrementProgress();
        else // Emote perms given to another pair.
            Achievements[AchievementType.Puppeteer].Progress[PuppeteerLabels.ControlMyBody].IncrementProgress();
    }

    private void OnPatternAction(PatternInteractionKind actionType, Guid patternGuid, bool wasAlarm)
    {
        switch (actionType)
        {
            case PatternInteractionKind.Published:
                Achievements[AchievementType.Toybox].Progress[ToyboxLabels.FunForAll].IncrementProgress();
                Achievements[AchievementType.Toybox].Progress[ToyboxLabels.DeviousComposer].IncrementProgress();
                break;
            case PatternInteractionKind.Downloaded:
                Achievements[AchievementType.Toybox].Progress[ToyboxLabels.CravingPleasure].IncrementProgress();
                break;
            case PatternInteractionKind.Liked:
                Achievements[AchievementType.Toybox].Progress[ToyboxLabels.PatternLover].IncrementProgress();
                break;
            case PatternInteractionKind.Started:
                if (patternGuid != Guid.Empty)
                    Achievements[AchievementType.Toybox].Duration[ToyboxLabels.EnduranceQueen].StartTracking(patternGuid.ToString());
                if (wasAlarm && patternGuid != Guid.Empty)
                    Achievements[AchievementType.Toybox].Progress[ToyboxLabels.HornyMornings].IncrementProgress();
                break;
            case PatternInteractionKind.Stopped:
                if (patternGuid != Guid.Empty)
                    Achievements[AchievementType.Toybox].Duration[ToyboxLabels.EnduranceQueen].StopTracking(patternGuid.ToString());
                break;
        }
    }

    private void OnDeviceConnected()
    {
        Achievements[AchievementType.Toybox].Conditional[ToyboxLabels.MyFavoriteToys].CheckCompletion();
        Achievements[AchievementType.Secrets].Conditional[SecretLabels.Experimentalist].CheckCompletion();
    }

    private void OnTriggerFired()
    {
        Achievements[AchievementType.Toybox].Progress[ToyboxLabels.SubtleReminders].IncrementProgress();
        Achievements[AchievementType.Toybox].Progress[ToyboxLabels.FingerOnTheTrigger].IncrementProgress();
        Achievements[AchievementType.Toybox].Progress[ToyboxLabels.TriggerHappy].IncrementProgress();
    }

    private void OnHardcoreForcedPairAction(HardcorePairActionKind actionKind, NewState state, string pairUID, bool actionWasFromClient)
    {
        switch (actionKind)
        {
            case HardcorePairActionKind.ForcedFollow:
                // If we are forcing someone else to follow us
                if (actionWasFromClient && state is NewState.Enabled)
                {
                    Achievements[AchievementType.Hardcore].Progress[HardcoreLabels.AllTheCollarsOfTheRainbow].IncrementProgress();
                    Achievements[AchievementType.Hardcore].Duration[HardcoreLabels.ForcedFollow].StartTracking(pairUID);
                    Achievements[AchievementType.Hardcore].Duration[HardcoreLabels.ForcedWalkies].StartTracking(pairUID);
                }
                // check if we have gone through the full dungeon in hardcore follow on.
                if (state is NewState.Disabled && actionWasFromClient)
                    Achievements[AchievementType.Hardcore].ConditionalProgress[HardcoreLabels.UCanTieThis].CheckTaskProgress();

                // if another user has sent us that their forced follow stopped, stop tracking the duration ones.
                if (state is NewState.Disabled && !actionWasFromClient)
                {
                    Achievements[AchievementType.Hardcore].Duration[HardcoreLabels.ForcedFollow].StopTracking(pairUID);
                    Achievements[AchievementType.Hardcore].Duration[HardcoreLabels.ForcedWalkies].StopTracking(pairUID);
                }

                // if someone else has ordered us to start following, begin tracking.
                if (state is NewState.Enabled && !actionWasFromClient)
                {
                    Achievements[AchievementType.Hardcore].ConditionalDuration[HardcoreLabels.TimeForWalkies].CheckCompletion();
                    Achievements[AchievementType.Hardcore].ConditionalDuration[HardcoreLabels.GettingStepsIn].CheckCompletion();
                    Achievements[AchievementType.Hardcore].ConditionalDuration[HardcoreLabels.WalkiesLover].CheckCompletion();
                }
                // if our forced to follow order from another user has stopped, check for completion.
                if (state is NewState.Disabled && !actionWasFromClient)
                {
                    Achievements[AchievementType.Hardcore].ConditionalDuration[HardcoreLabels.TimeForWalkies].ResetOrComplete();
                    Achievements[AchievementType.Hardcore].ConditionalDuration[HardcoreLabels.GettingStepsIn].ResetOrComplete();
                    Achievements[AchievementType.Hardcore].ConditionalDuration[HardcoreLabels.WalkiesLover].ResetOrComplete();
                }

                break;
            case HardcorePairActionKind.ForcedSit:
                if (!actionWasFromClient && state is NewState.Enabled)
                {
                    Achievements[AchievementType.Hardcore].ConditionalDuration[HardcoreLabels.LivingFurniture].CheckCompletion();
                }
                else if (!actionWasFromClient && state is NewState.Disabled)
                {
                    Achievements[AchievementType.Hardcore].ConditionalDuration[HardcoreLabels.LivingFurniture].ResetOrComplete();
                }

                break;
            case HardcorePairActionKind.ForcedStay:
                if (!actionWasFromClient && state is NewState.Enabled)
                {
                    Achievements[AchievementType.Hardcore].ConditionalDuration[HardcoreLabels.PetTraining].CheckCompletion();
                    Achievements[AchievementType.Hardcore].ConditionalDuration[HardcoreLabels.NotGoingAnywhere].CheckCompletion();
                    Achievements[AchievementType.Hardcore].ConditionalDuration[HardcoreLabels.HouseTrained].CheckCompletion();
                }
                else if (!actionWasFromClient && state is NewState.Disabled)
                {
                    Achievements[AchievementType.Hardcore].ConditionalDuration[HardcoreLabels.PetTraining].ResetOrComplete();
                    Achievements[AchievementType.Hardcore].ConditionalDuration[HardcoreLabels.NotGoingAnywhere].ResetOrComplete();
                    Achievements[AchievementType.Hardcore].ConditionalDuration[HardcoreLabels.HouseTrained].ResetOrComplete();
                }
                break;
            case HardcorePairActionKind.ForcedBlindfold:
                // if we have had our blindfold set to enabled by another pair, perform the following:
                if (!actionWasFromClient && state is NewState.Enabled)
                {
                    Achievements[AchievementType.Hardcore].Conditional[HardcoreLabels.BlindLeadingTheBlind].CheckCompletion();
                    Achievements[AchievementType.Hardcore].ConditionalDuration[HardcoreLabels.WhoNeedsToSee].CheckCompletion();
                }
                // if another pair is removing our blindfold, perform the following:
                else if (!actionWasFromClient && state is NewState.Disabled)
                {
                    Achievements[AchievementType.Hardcore].ConditionalDuration[HardcoreLabels.WhoNeedsToSee].ResetOrComplete();
                }
                break;

        }
    }

    private void OnShockSent()
    {
        Achievements[AchievementType.Hardcore].Progress[HardcoreLabels.IndulgingSparks].IncrementProgress();
        Achievements[AchievementType.Hardcore].Progress[HardcoreLabels.CantGetEnough].IncrementProgress();
        Achievements[AchievementType.Hardcore].Progress[HardcoreLabels.VerThunder].IncrementProgress();
        Achievements[AchievementType.Hardcore].Progress[HardcoreLabels.WickedThunder].IncrementProgress();
        Achievements[AchievementType.Hardcore].Progress[HardcoreLabels.ElectropeHasNoLimits].IncrementProgress();
    }

    private void OnShockReceived()
    {
        Achievements[AchievementType.Hardcore].Progress[HardcoreLabels.ShockAndAwe].IncrementProgress();
        Achievements[AchievementType.Hardcore].Progress[HardcoreLabels.ShockingExperience].IncrementProgress();
        Achievements[AchievementType.Hardcore].Progress[HardcoreLabels.ShockolateTasting].IncrementProgress();
        Achievements[AchievementType.Hardcore].Progress[HardcoreLabels.ShockAddiction].IncrementProgress();
        Achievements[AchievementType.Hardcore].Progress[HardcoreLabels.WarriorOfElectrope].IncrementProgress();
    }


    private void OnChatMessage(XivChatType channel, string message, string SenderName)
    {
        if (message.Split(' ').Count() > 5)
        {
            if (channel is XivChatType.Say)
            {
                Achievements[AchievementType.Gags].Progress[GagLabels.SpeakUpSlut].IncrementProgress();
            }
            else if (channel is XivChatType.Yell)
            {
                Achievements[AchievementType.Gags].Progress[GagLabels.CantHearYou].IncrementProgress();
            }
            else if (channel is XivChatType.Shout)
            {
                Achievements[AchievementType.Gags].Progress[GagLabels.OneMoreForTheCrowd].IncrementProgress();
            }
        }
        // check if we meet some of the secret requirements.
        Achievements[AchievementType.Secrets].Conditional[SecretLabels.HelplessDamsel].CheckCompletion();
    }

    private void OnEmoteExecuted(ulong emoteCallerObjectId, ushort emoteId, string emoteName, ulong targetObjectId)
    {
        // doing /lookout while blindfolded.
        if (!Achievements[AchievementType.Hardcore].Conditional[HardcoreLabels.WhatAView].IsCompleted && emoteCallerObjectId == _frameworkUtils.ClientState.LocalPlayer?.GameObjectId)
            if (emoteName.Contains("lookout", StringComparison.OrdinalIgnoreCase))
                Achievements[AchievementType.Hardcore].Conditional[HardcoreLabels.WhatAView].CheckCompletion();

        // detect getting slapped.
        if (!Achievements[AchievementType.Generic].Conditional[GenericLabels.ICantBelieveYouveDoneThis].IsCompleted && targetObjectId == _frameworkUtils.ClientState.LocalPlayer?.GameObjectId)
            if (emoteName.Contains("slap", StringComparison.OrdinalIgnoreCase))
                Achievements[AchievementType.Generic].Conditional[GenericLabels.ICantBelieveYouveDoneThis].CheckCompletion();
    }

    private void OnPuppeteerEmoteSent(string emoteName)
    {
        if (emoteName.Contains("shush", StringComparison.OrdinalIgnoreCase))
        {
            Achievements[AchievementType.Gags].Conditional[GagLabels.QuietNowDear].CheckCompletion();
        }
        else if (emoteName.Contains("dance", StringComparison.OrdinalIgnoreCase))
        {
            Achievements[AchievementType.Puppeteer].Progress[PuppeteerLabels.ShowingOff].IncrementProgress();
        }
        else if (emoteName.Contains("grovel", StringComparison.OrdinalIgnoreCase))
        {
            Achievements[AchievementType.Puppeteer].Progress[PuppeteerLabels.KissMyHeels].IncrementProgress();
        }
        else if (emoteName.Contains("sulk", StringComparison.OrdinalIgnoreCase))
        {
            Achievements[AchievementType.Puppeteer].Progress[PuppeteerLabels.Ashamed].IncrementProgress();
        }
        else if (emoteName.Contains("sit", StringComparison.OrdinalIgnoreCase) || emoteName.Contains("groundsit", StringComparison.OrdinalIgnoreCase))
        {
            Achievements[AchievementType.Puppeteer].Progress[PuppeteerLabels.WhoIsAGoodPet].IncrementProgress();
        }
    }

    private void OnPairAdded()
    {
        Achievements[AchievementType.Generic].Conditional[GenericLabels.AddedFirstPair].CheckCompletion();
        Achievements[AchievementType.Generic].Progress[GenericLabels.TheCollector].IncrementProgress();
    }

    private void OnCursedLootFound()
    {
        Achievements[AchievementType.Wardrobe].Progress[WardrobeLabels.TemptingFatesTreasure].IncrementProgress();
        Achievements[AchievementType.Wardrobe].Progress[WardrobeLabels.BadEndSeeker].IncrementProgress();
        Achievements[AchievementType.Wardrobe].Progress[WardrobeLabels.EverCursed].IncrementProgress();
    }

    private void OnJobChange(GlamourUpdateType changeType)
    {
        Achievements[AchievementType.Generic].Conditional[GenericLabels.EscapingIsNotEasy].CheckCompletion();
    }
}
