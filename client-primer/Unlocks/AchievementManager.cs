using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Text;
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


    public Dictionary<string, ProgressAchievement> ProgressAchievements = [];
    public Dictionary<string, ConditionalAchievement> ConditionalAchievements = [];
    public Dictionary<string, DurationAchievement> DurationAchievements = [];
    public Dictionary<string, TimedProgressAchievement> TimedProgressAchievements = [];
    public Dictionary<string, ConditionalProgressAchievement> ConditionalProgressAchievements = [];
    public Dictionary<string, ConditionalDurationAchievement> ConditionalDurationAchievements = [];

    private Dictionary<string, bool> EasterEggIcons = new Dictionary<string, bool>()
    {
        {"Orders", false },
        {"Gags", false },
        {"Wardrobe", false },
        {"Puppeteer", false },
        {"Toybox", false }
    };

    public int TotalAchievements => ProgressAchievements.Count + ConditionalAchievements.Count + DurationAchievements.Count
        + TimedProgressAchievements.Count + ConditionalProgressAchievements.Count + ConditionalDurationAchievements.Count;

    public List<Achievement> AllAchievements => ProgressAchievements.Values.Cast<Achievement>()
        .Concat(ConditionalAchievements.Values.Cast<Achievement>())
        .Concat(DurationAchievements.Values.Cast<Achievement>())
        .Concat(TimedProgressAchievements.Values.Cast<Achievement>())
        .Concat(ConditionalProgressAchievements.Values.Cast<Achievement>())
        .Concat(ConditionalDurationAchievements.Values.Cast<Achievement>())
        .ToList();

    public AchievementManager(ILogger<AchievementManager> logger,
        GagspeakMediator mediator, ClientConfigurationManager clientConfigs,
        PlayerCharacterData playerData, PairManager pairManager,
        OnFrameworkService frameworkUtils, ToyboxVibeService vibeService,
        UnlocksEventManager eventManager, ItemIdVars itemHelpers) : base(logger, mediator)
    {
        _clientConfigs = clientConfigs;
        _playerData = playerData;
        _pairManager = pairManager;
        _frameworkUtils = frameworkUtils;
        _vibeService = vibeService;
        _eventManager = eventManager;
        _itemHelpers = itemHelpers;

        ProgressAchievements = new Dictionary<string, ProgressAchievement>();
        ConditionalAchievements = new Dictionary<string, ConditionalAchievement>();
        DurationAchievements = new Dictionary<string, DurationAchievement>();
        TimedProgressAchievements = new Dictionary<string, TimedProgressAchievement>();
        ConditionalProgressAchievements = new Dictionary<string, ConditionalProgressAchievement>();
        ConditionalDurationAchievements = new Dictionary<string, ConditionalDurationAchievement>();


        // initialize all achievements
        InitializeAchievements();

        // Subscribe to relevant events
        _eventManager.Subscribe<OrderInteractionKind>(UnlocksEvent.OrderAction, OnOrderAction);
        _eventManager.Subscribe<GagLayer, GagType, bool>(UnlocksEvent.GagAction, OnGagApplied);
        _eventManager.Subscribe(UnlocksEvent.GagUnlockGuessFailed, () => ConditionalAchievements[Achievements.Wardrobe.RunningGag].CheckCompletion());

        _eventManager.Subscribe<RestraintSet, bool>(UnlocksEvent.RestraintApplied, OnRestraintApplied);
        _eventManager.Subscribe<RestraintSet, bool, bool>(UnlocksEvent.RestraintLockChange, OnRestraintLockChange);
        _eventManager.Subscribe<Padlocks, bool, bool>(UnlocksEvent.PairRestraintLockChange, OnPairRestraintLockChange);
        _eventManager.Subscribe<RestraintSet, bool>(UnlocksEvent.RestraintUnlockGuessFailed, OnRestraintUnlockGuessFailed);

        _eventManager.Subscribe(UnlocksEvent.PuppeteerMessageSend, () => ProgressAchievements[Achievements.Puppeteer.MasterOfPuppets].IncrementProgress());
        _eventManager.Subscribe<bool>(UnlocksEvent.PuppeteerAccessGiven, OnPuppetAccessGiven);

        _eventManager.Subscribe<PatternInteractionKind, Guid, bool>(UnlocksEvent.PatternAction, OnPatternAction);
        _eventManager.Subscribe(UnlocksEvent.DeviceConnected, OnDeviceConnected);
        _eventManager.Subscribe(UnlocksEvent.TriggerFired, OnTriggerFired);
        _eventManager.Subscribe(UnlocksEvent.DeathRollCompleted, OnDeathRollCompleted);
        _eventManager.Subscribe<NewState>(UnlocksEvent.AlarmToggled, _ => ProgressAchievements[Achievements.Secrets.Experimentalist].IncrementProgress());
        _eventManager.Subscribe(UnlocksEvent.ShockSent, OnShockSent);
        _eventManager.Subscribe(UnlocksEvent.ShockReceived, OnShockReceived);

        _eventManager.Subscribe<HardcorePairActionKind, NewState, string, bool>(UnlocksEvent.HardcoreForcedPairAction, OnHardcoreForcedPairAction);

        _eventManager.Subscribe(UnlocksEvent.RemoteOpened, () => ProgressAchievements[Achievements.Remotes.JustVibing].CheckCompletion());
        _eventManager.Subscribe(UnlocksEvent.VibeRoomCreated, () => ProgressAchievements[Achievements.Remotes.VibingWithFriends].CheckCompletion());

        _eventManager.Subscribe(UnlocksEvent.PvpPlayerSlain, OnPlayerSlain);
        _eventManager.Subscribe(UnlocksEvent.ClientSlain, OnClientSlain);
        _eventManager.Subscribe<XivChatType, string, string>(UnlocksEvent.ChatMessageSent, OnChatMessage);
        _eventManager.Subscribe<ulong, ushort, string, ulong>(UnlocksEvent.PlayerEmoteExecuted, OnEmoteExecuted);
        _eventManager.Subscribe<string>(UnlocksEvent.PuppeteerEmoteSent, OnPuppeteerEmoteSent);
        _eventManager.Subscribe(UnlocksEvent.TutorialCompleted, () => ProgressAchievements[Achievements.Generic.TutorialComplete].CheckCompletion());
        _eventManager.Subscribe(UnlocksEvent.PairAdded, OnPairAdded);
        _eventManager.Subscribe(UnlocksEvent.PresetApplied, () => ProgressAchievements[Achievements.Generic.AppliedFirstPreset].IncrementProgress());
        _eventManager.Subscribe(UnlocksEvent.GlobalSent, () => ProgressAchievements[Achievements.Generic.HelloKinkyWorld].IncrementProgress());
        _eventManager.Subscribe(UnlocksEvent.CursedDungeonLootFound, OnCursedLootFound);
        _eventManager.Subscribe<string>(UnlocksEvent.EasterEggFound, OnIconClicked);
        _eventManager.Subscribe(UnlocksEvent.ChocoboRaceFinished, () => ConditionalAchievements[Achievements.Secrets.WildRide].CheckCompletion());

        IpcFastUpdates.GlamourEventFired += OnJobChange;

        Mediator.Subscribe<PairHandlerVisibleMessage>(this, _ => OnNewPairVisible());
        Mediator.Subscribe<CommendationsIncreasedMessage>(this, (msg) => OnCommendationsGiven(msg.amount));
        Mediator.Subscribe<PlaybackStateToggled>(this, (msg) => ConditionalAchievements[Achievements.Secrets.Experimentalist].CheckCompletion());
        
        Mediator.Subscribe<SafewordUsedMessage>(this, _ => ProgressAchievements[Achievements.Generic.KnowsMyLimits].CheckCompletion());
        
        Mediator.Subscribe<GPoseStartMessage>(this, _ => ConditionalProgressAchievements[Achievements.Gags.SayMmmph].BeginConditionalTask());
        Mediator.Subscribe<GPoseEndMessage>(this, _ => ConditionalProgressAchievements[Achievements.Gags.SayMmmph].FinishConditionalTask());
        Mediator.Subscribe<CutsceneBeginMessage>(this, _ => ConditionalDurationAchievements[Achievements.Generic.WarriorOfLewd].CheckCompletion());
        Mediator.Subscribe<CutsceneEndMessage>(this, _ => ConditionalDurationAchievements[Achievements.Generic.WarriorOfLewd].ResetOrComplete());

        Mediator.Subscribe<ZoneSwitchEndMessage>(this, _ =>
        {
            if (_frameworkUtils.IsInMainCity)
            {
                ConditionalAchievements[Achievements.Hardcore.WalkOfShame].CheckCompletion();
            }

            // if present in diadem (for diamdem achievement)
            if (_frameworkUtils.ClientState.TerritoryType is 939)

                ConditionalDurationAchievements[Achievements.Toybox.MotivationForRestoration].CheckCompletion();
            else
                ConditionalDurationAchievements[Achievements.Toybox.MotivationForRestoration].ResetOrComplete();

            // if we are in a dungeon:
            if (_frameworkUtils.InDungeonOrDuty)
            {
                ConditionalProgressAchievements[Achievements.Gags.SilentButDeadly].BeginConditionalTask();
                ConditionalProgressAchievements[Achievements.Hardcore.UCanTieThis].BeginConditionalTask();

                if (_frameworkUtils.PlayerJobRole is ActionRoles.Healer)
                    ConditionalProgressAchievements[Achievements.Wardrobe.HealSlut].BeginConditionalTask();
            }
            else
            {
                if (ConditionalProgressAchievements[Achievements.Hardcore.UCanTieThis].ConditionalTaskBegun)
                    ConditionalProgressAchievements[Achievements.Hardcore.UCanTieThis].FinishConditionalTask();

                if (ConditionalProgressAchievements[Achievements.Gags.SilentButDeadly].ConditionalTaskBegun)
                    ConditionalProgressAchievements[Achievements.Gags.SilentButDeadly].FinishConditionalTask();

                if (ConditionalProgressAchievements[Achievements.Wardrobe.HealSlut].ConditionalTaskBegun)
                    ConditionalProgressAchievements[Achievements.Wardrobe.HealSlut].FinishConditionalTask();
            }

            // check stuff for deep dungeon achievements.
            CheckDeepDungeonStatus();
        });

        Mediator.Subscribe<DelayedFrameworkUpdateMessage>(this, _ => CheckGoldSaucerEvents());
    }

    private unsafe void CheckGoldSaucerEvents()
    {
        // on each delayed framework, check to see if we are chocobo racing.
        if (_frameworkUtils.Condition[ConditionFlag.ChocoboRacing])
        {
            var resultMenu = (AtkUnitBase*)GenericHelpers.GetAddonByName("RaceChocoboResult");
            if (resultMenu != null)
            {
                if (resultMenu->RootNode->IsVisible())
                {
                    // invoke the thing.
                    Logger.LogInformation("Would be invoking the achievement Check now");
                }
            }
        }

        // check on the current gold saucer gates.
        if(false)
        {
            var gateMenu = (AtkUnitBase*)GenericHelpers.GetAddonByName("GoldSaucerGate");
            if (gateMenu != null)
            {
                if (gateMenu->RootNode->IsVisible())
                {
                    // invoke the thing.
                    Logger.LogInformation("Would be invoking the achievement Check now");
                }
            }
        }
        Logger.LogInformation("Territory Location: {0}", _frameworkUtils.ClientState.TerritoryType);
    }

    private void OnNewPairVisible()
    {
        ConditionalAchievements[Achievements.Secrets.BondageClub].CheckCompletion();
    }

    private void OnCommendationsGiven(int amount)
    {
        ConditionalProgressAchievements[Achievements.Secrets.KinkyTeacher].CheckTaskProgress(amount);
        ConditionalProgressAchievements[Achievements.Secrets.KinkyProfessor].CheckTaskProgress(amount);
        ConditionalProgressAchievements[Achievements.Secrets.KinkyMentor].CheckTaskProgress(amount);
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
            ProgressAchievements[Achievements.Secrets.TooltipLogos].IncrementProgress();
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
            ConditionalProgressAchievements[Achievements.Wardrobe.MyKinksRunDeeper].BeginConditionalTask();

        switch (deepDungeonType)
        {
            case DeepDungeonType.PalaceOfTheDead:
                if ((floor > 40 && floor <= 50) || (floor > 90 && floor <= 100))
                {
                    ConditionalProgressAchievements[Achievements.Wardrobe.BondagePalace].BeginConditionalTask();
                    ConditionalProgressAchievements[Achievements.Wardrobe.MyKinkRunsDeep].BeginConditionalTask();
                    if (floor is 50 || floor is 100)
                    {
                        ConditionalProgressAchievements[Achievements.Wardrobe.BondagePalace].FinishConditionalTask();
                        ConditionalProgressAchievements[Achievements.Wardrobe.MyKinkRunsDeep].FinishConditionalTask();
                        ConditionalProgressAchievements[Achievements.Wardrobe.MyKinksRunDeeper].FinishConditionalTask();
                    }
                }
                break;
            case DeepDungeonType.HeavenOnHigh:
                if (floor > 20 && floor <= 30)
                {
                    ConditionalProgressAchievements[Achievements.Wardrobe.HornyOnHigh].BeginConditionalTask();
                    ConditionalProgressAchievements[Achievements.Wardrobe.MyKinkRunsDeep].BeginConditionalTask();
                    if (floor is 30)
                    {
                        ConditionalProgressAchievements[Achievements.Wardrobe.BondagePalace].FinishConditionalTask();
                        ConditionalProgressAchievements[Achievements.Wardrobe.MyKinkRunsDeep].FinishConditionalTask();
                        ConditionalProgressAchievements[Achievements.Wardrobe.MyKinksRunDeeper].FinishConditionalTask();
                    }
                }
                break;
            case DeepDungeonType.EurekaOrthos:
                if (floor > 20 && floor <= 30)
                {
                    ConditionalProgressAchievements[Achievements.Wardrobe.EurekaWhorethos].BeginConditionalTask();
                    ConditionalProgressAchievements[Achievements.Wardrobe.MyKinkRunsDeep].BeginConditionalTask();
                    if (floor is 30)
                    {
                        ConditionalProgressAchievements[Achievements.Wardrobe.EurekaWhorethos].FinishConditionalTask();
                        ConditionalProgressAchievements[Achievements.Wardrobe.MyKinkRunsDeep].FinishConditionalTask();
                        ConditionalProgressAchievements[Achievements.Wardrobe.MyKinksRunDeeper].FinishConditionalTask();
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
        _eventManager.Unsubscribe(UnlocksEvent.GagUnlockGuessFailed, () => ConditionalAchievements[Achievements.Wardrobe.RunningGag].CheckCompletion());
        _eventManager.Unsubscribe<RestraintSet, bool>(UnlocksEvent.RestraintApplied, OnRestraintApplied);
        _eventManager.Unsubscribe<RestraintSet, bool, bool>(UnlocksEvent.RestraintLockChange, OnRestraintLockChange);
        _eventManager.Unsubscribe<Padlocks, bool, bool>(UnlocksEvent.PairRestraintLockChange, OnPairRestraintLockChange);
        _eventManager.Unsubscribe<RestraintSet, bool>(UnlocksEvent.RestraintUnlockGuessFailed, OnRestraintUnlockGuessFailed);
        _eventManager.Unsubscribe(UnlocksEvent.PuppeteerMessageSend, () => ProgressAchievements[Achievements.Puppeteer.MasterOfPuppets].IncrementProgress());
        _eventManager.Unsubscribe<bool>(UnlocksEvent.PuppeteerAccessGiven, OnPuppetAccessGiven);
        _eventManager.Unsubscribe<PatternInteractionKind, Guid, bool>(UnlocksEvent.PatternAction, OnPatternAction);
        _eventManager.Unsubscribe(UnlocksEvent.DeviceConnected, OnDeviceConnected);
        _eventManager.Unsubscribe(UnlocksEvent.TriggerFired, OnTriggerFired);
        _eventManager.Unsubscribe(UnlocksEvent.DeathRollCompleted, OnDeathRollCompleted);
        _eventManager.Unsubscribe<NewState>(UnlocksEvent.AlarmToggled, _ => ProgressAchievements[Achievements.Secrets.Experimentalist].IncrementProgress());
        _eventManager.Unsubscribe(UnlocksEvent.ShockSent, OnShockSent);
        _eventManager.Unsubscribe(UnlocksEvent.ShockReceived, OnShockReceived);
        _eventManager.Unsubscribe<HardcorePairActionKind, NewState, string, bool>(UnlocksEvent.HardcoreForcedPairAction, OnHardcoreForcedPairAction);
        _eventManager.Unsubscribe(UnlocksEvent.RemoteOpened, () => ProgressAchievements[Achievements.Remotes.JustVibing].CheckCompletion());
        _eventManager.Unsubscribe(UnlocksEvent.VibeRoomCreated, () => ProgressAchievements[Achievements.Remotes.VibingWithFriends].CheckCompletion());
        _eventManager.Unsubscribe(UnlocksEvent.PvpPlayerSlain, OnPlayerSlain);
        _eventManager.Unsubscribe(UnlocksEvent.ClientSlain, OnClientSlain);
        _eventManager.Unsubscribe<XivChatType, string, string>(UnlocksEvent.ChatMessageSent, OnChatMessage);
        _eventManager.Unsubscribe<ulong, ushort, string, ulong>(UnlocksEvent.PlayerEmoteExecuted, OnEmoteExecuted);
        _eventManager.Unsubscribe<string>(UnlocksEvent.PuppeteerEmoteSent, OnPuppeteerEmoteSent);
        _eventManager.Unsubscribe(UnlocksEvent.TutorialCompleted, () => ProgressAchievements[Achievements.Generic.TutorialComplete].CheckCompletion());
        _eventManager.Unsubscribe(UnlocksEvent.PairAdded, OnPairAdded);
        _eventManager.Unsubscribe(UnlocksEvent.PresetApplied, () => ProgressAchievements[Achievements.Generic.AppliedFirstPreset].IncrementProgress());
        _eventManager.Unsubscribe(UnlocksEvent.GlobalSent, () => ProgressAchievements[Achievements.Generic.HelloKinkyWorld].IncrementProgress());
        _eventManager.Unsubscribe(UnlocksEvent.CursedDungeonLootFound, OnCursedLootFound);
        _eventManager.Unsubscribe<string>(UnlocksEvent.EasterEggFound, OnIconClicked);
        _eventManager.Unsubscribe(UnlocksEvent.ChocoboRaceFinished, () => ConditionalAchievements[Achievements.Secrets.WildRide].CheckCompletion());

        IpcFastUpdates.GlamourEventFired -= OnJobChange;
    }

    private void OnOrderAction(OrderInteractionKind orderKind)
    {
        switch (orderKind)
        {
            case OrderInteractionKind.Completed:
                ProgressAchievements[Achievements.Orders.JustAVolunteer].IncrementProgress();
                ProgressAchievements[Achievements.Orders.AsYouCommand].IncrementProgress();
                ProgressAchievements[Achievements.Orders.AnythingForMyOwner].IncrementProgress();
                ProgressAchievements[Achievements.Orders.GoodDrone].IncrementProgress();
                break;
            case OrderInteractionKind.Fail:
                ProgressAchievements[Achievements.Orders.BadSlut].IncrementProgress();
                ProgressAchievements[Achievements.Orders.NeedsTraining].IncrementProgress();
                ProgressAchievements[Achievements.Orders.UsefulInOtherWays].IncrementProgress();
                break;
            case OrderInteractionKind.Create:
                ProgressAchievements[Achievements.Orders.NewSlaveOwner].IncrementProgress();
                ProgressAchievements[Achievements.Orders.TaskManager].IncrementProgress();
                ProgressAchievements[Achievements.Orders.MaidMaster].IncrementProgress();
                ProgressAchievements[Achievements.Orders.QueenOfDrones].IncrementProgress();
                break;
        }
    }

    private void OnGagApplied(GagLayer gagLayer, GagType gagType, bool isSelfApplied)
    {
        // if the gag is self applied
        if (isSelfApplied)
        {
            ProgressAchievements[Achievements.Gags.SelfApplied].IncrementProgress();
        }
        // if the gag is not self applied
        else
        {
            ProgressAchievements[Achievements.Gags.ApplyToPair].IncrementProgress();
            ProgressAchievements[Achievements.Gags.LookingForTheRightFit].IncrementProgress();
            ProgressAchievements[Achievements.Gags.OralFixation].IncrementProgress();
            ProgressAchievements[Achievements.Gags.AKinkForDrool].IncrementProgress();

            ConditionalAchievements[Achievements.Gags.ShushtainableResource].CheckCompletion();

            DurationAchievements[Achievements.Gags.SpeechSilverSilenceGolden].StartTracking(gagType.GagName());
            DurationAchievements[Achievements.Gags.TheKinkyLegend].StartTracking(gagType.GagName());

            TimedProgressAchievements[Achievements.Gags.ATrueGagSlut].IncrementProgress();

            ConditionalProgressAchievements[Achievements.Gags.YourFavoriteNurse].CheckTaskProgress();

            ConditionalAchievements[Achievements.Secrets.Experimentalist].CheckCompletion();

            ConditionalAchievements[Achievements.Secrets.GaggedPleasure].CheckCompletion();
        }
        // experimentalist
        ConditionalAchievements[Achievements.Secrets.Experimentalist].CheckCompletion();
        ConditionalAchievements[Achievements.Secrets.GaggedPleasure].CheckCompletion();
    }

    private void OnRestraintApplied(RestraintSet set, bool isSelfApplied)
    {
        ProgressAchievements[Achievements.Wardrobe.FirstTiemers].IncrementProgress();
        ConditionalAchievements[Achievements.Secrets.Experimentalist].CheckCompletion();

        // we were the applier
        if (isSelfApplied)
        {
            ProgressAchievements[Achievements.Wardrobe.SelfBondageEnthusiast].IncrementProgress();
        }
        // we were not the applier
        else
        {
            // start the "Auctioned Off" achievement
            ConditionalProgressAchievements[Achievements.Wardrobe.AuctionedOff].BeginConditionalTask();

            // Achievements related to applying
            ProgressAchievements[Achievements.Wardrobe.DiDEnthusiast].IncrementProgress();
            TimedProgressAchievements[Achievements.Wardrobe.BondageBunny].IncrementProgress();
            ConditionalDurationAchievements[Achievements.Wardrobe.Bondodge].CheckCompletion();

            // see if valid for "cuffed-19"
            if (set.DrawData.TryGetValue(EquipSlot.Hands, out var handData) && handData.GameItem.Id != ItemIdVars.NothingItem(EquipSlot.Hands).Id)
            {
                ProgressAchievements[Achievements.Wardrobe.Cuffed19].IncrementProgress();
            }

            // check for dyes
            if (set.DrawData.Any(x => x.Value.GameStain.Stain1 != 0 || x.Value.GameStain.Stain2 != 0))
            {
                ProgressAchievements[Achievements.Wardrobe.ToDyeFor].IncrementProgress();
                ProgressAchievements[Achievements.Wardrobe.DyeAnotherDay].IncrementProgress();
                ProgressAchievements[Achievements.Wardrobe.DyeHard].IncrementProgress();
            }
        }
    }

    private void OnPairRestraintLockChange(Padlocks padlock, bool isUnlocking, bool wasAssigner)
    {
        // we have unlocked a pair.
        if (isUnlocking)
        {
            if (padlock is Padlocks.PasswordPadlock && !wasAssigner)
                ProgressAchievements[Achievements.Wardrobe.SoldSlave].IncrementProgress();

            ProgressAchievements[Achievements.Wardrobe.TheRescuer].IncrementProgress();
        }
        // we have locked a pair up.
        else
        {
            ProgressAchievements[Achievements.Wardrobe.RiggersFirstSession].IncrementProgress();
            ProgressAchievements[Achievements.Wardrobe.MyLittlePlaything].IncrementProgress();
            ProgressAchievements[Achievements.Wardrobe.SuitsYouBitch].IncrementProgress();
            ProgressAchievements[Achievements.Wardrobe.TiesThatBind].IncrementProgress();
            ProgressAchievements[Achievements.Wardrobe.SlaveTraining].IncrementProgress();
            ProgressAchievements[Achievements.Wardrobe.CeremonyOfEternalBondage].IncrementProgress();
        }
    }

    private void OnRestraintLockChange(RestraintSet set, bool isUnlocking, bool isSelfApplied)
    {
        // we are unlocking
        if (isUnlocking)
        {
            // we locked our set.
            if (isSelfApplied)
            {

            }
            // someone else locked our set
            else
            {

            }
        }
        // we are locking
        else
        {
            // we locked our set.
            if (isSelfApplied)
            {
                ProgressAchievements[Achievements.Wardrobe.RiggersFirstSession].IncrementProgress();
            }
            // someone else locked our set
            else
            {
                ProgressAchievements[Achievements.Wardrobe.FirstTimeBondage].IncrementProgress();
                ProgressAchievements[Achievements.Wardrobe.AmateurBondage].IncrementProgress();
                ProgressAchievements[Achievements.Wardrobe.ComfortRestraint].IncrementProgress();
                ProgressAchievements[Achievements.Wardrobe.DayInTheLifeOfABondageSlave].IncrementProgress();
                ProgressAchievements[Achievements.Wardrobe.AWeekInBondage].IncrementProgress();
                ProgressAchievements[Achievements.Wardrobe.AMonthInBondage].IncrementProgress();
            }
        }
    }

    private void OnRestraintUnlockGuessFailed(RestraintSet set, bool isSelfApplied)
    {

    }

    private void OnRestraintRemoved(RestraintSet set, bool isSelfApplied)
    {

    }
    private void OnPuppetAccessGiven(bool wasAllPerms)
    {
        if (wasAllPerms) // All Perms access given to another pair.
            ProgressAchievements[Achievements.Puppeteer.CompleteDevotion].IncrementProgress();
        else // Emote perms given to another pair.
            ProgressAchievements[Achievements.Puppeteer.ControlMyBody].IncrementProgress();
    }

    private void OnPatternAction(PatternInteractionKind actionType, Guid patternGuid, bool wasAlarm)
    {
        switch (actionType)
        {
            case PatternInteractionKind.Published:
                ProgressAchievements[Achievements.Toybox.FunForAll].IncrementProgress();
                ProgressAchievements[Achievements.Toybox.DeviousComposer].IncrementProgress();
                break;
            case PatternInteractionKind.Downloaded:
                ProgressAchievements[Achievements.Toybox.CravingPleasure].IncrementProgress();
                break;
            case PatternInteractionKind.Liked:
                ProgressAchievements[Achievements.Toybox.PatternLover].IncrementProgress();
                break;
            case PatternInteractionKind.Started:
                if (patternGuid != Guid.Empty)
                    DurationAchievements[Achievements.Toybox.EnduranceQueen].StartTracking(patternGuid.ToString());
                if (wasAlarm && patternGuid != Guid.Empty)
                    ProgressAchievements[Achievements.Toybox.HornyMornings].IncrementProgress();
                break;
            case PatternInteractionKind.Stopped:
                if (patternGuid != Guid.Empty)
                    DurationAchievements[Achievements.Toybox.EnduranceQueen].StopTracking(patternGuid.ToString());
                break;
        }
    }

    private void OnDeviceConnected()
    {
        ConditionalAchievements[Achievements.Toybox.MyFavoriteToys].CheckCompletion();
        ConditionalAchievements[Achievements.Secrets.Experimentalist].CheckCompletion();
    }

    private void OnTriggerFired()
    {
        ProgressAchievements[Achievements.Toybox.SubtleReminders].IncrementProgress();
        ProgressAchievements[Achievements.Toybox.FingerOnTheTrigger].IncrementProgress();
        ProgressAchievements[Achievements.Toybox.TriggerHappy].IncrementProgress();
    }

    private void OnDeathRollCompleted() => ConditionalAchievements[Achievements.Toybox.KinkyGambler].CheckCompletion();
    private void OnPlayerSlain() => ProgressAchievements[Achievements.Toybox.NothingCanStopMe].IncrementProgress();

    private void OnClientSlain()
    {
        ConditionalAchievements[Achievements.Secrets.BadEndHostage].CheckCompletion();
    }

    private void OnHardcoreForcedPairAction(HardcorePairActionKind actionKind, NewState state, string pairUID, bool actionWasFromClient)
    {
        switch (actionKind)
        {
            case HardcorePairActionKind.ForcedFollow:
                // If we are forcing someone else to follow us
                if (actionWasFromClient && state is NewState.Enabled)
                {
                    ProgressAchievements[Achievements.Hardcore.AllTheCollarsOfTheRainbow].IncrementProgress();
                    DurationAchievements[Achievements.Hardcore.ForcedFollow].StartTracking(pairUID);
                    DurationAchievements[Achievements.Hardcore.ForcedWalkies].StartTracking(pairUID);
                }
                // check if we have gone through the full dungeon in hardcore follow on.
                if (state is NewState.Disabled && actionWasFromClient)
                    ConditionalProgressAchievements[Achievements.Hardcore.UCanTieThis].CheckTaskProgress();

                // if another user has sent us that their forced follow stopped, stop tracking the duration ones.
                if (state is NewState.Disabled && !actionWasFromClient)
                {
                    DurationAchievements[Achievements.Hardcore.ForcedFollow].StopTracking(pairUID);
                    DurationAchievements[Achievements.Hardcore.ForcedWalkies].StopTracking(pairUID);
                }

                // if someone else has ordered us to start following, begin tracking.
                if (state is NewState.Enabled && !actionWasFromClient)
                {
                    ConditionalDurationAchievements[Achievements.Hardcore.TimeForWalkies].CheckCompletion();
                    ConditionalDurationAchievements[Achievements.Hardcore.GettingStepsIn].CheckCompletion();
                    ConditionalDurationAchievements[Achievements.Hardcore.WalkiesLover].CheckCompletion();
                }
                // if our forced to follow order from another user has stopped, check for completion.
                if (state is NewState.Disabled && !actionWasFromClient)
                {
                    ConditionalDurationAchievements[Achievements.Hardcore.TimeForWalkies].ResetOrComplete();
                    ConditionalDurationAchievements[Achievements.Hardcore.GettingStepsIn].ResetOrComplete();
                    ConditionalDurationAchievements[Achievements.Hardcore.WalkiesLover].ResetOrComplete();
                }

                break;
            case HardcorePairActionKind.ForcedSit:
                if (!actionWasFromClient && state is NewState.Enabled)
                {
                    ConditionalDurationAchievements[Achievements.Hardcore.LivingFurniture].CheckCompletion();
                }
                else if (!actionWasFromClient && state is NewState.Disabled)
                {
                    ConditionalDurationAchievements[Achievements.Hardcore.LivingFurniture].ResetOrComplete();
                }

                break;
            case HardcorePairActionKind.ForcedStay:
                if (!actionWasFromClient && state is NewState.Enabled)
                {
                    ConditionalDurationAchievements[Achievements.Hardcore.PetTraining].CheckCompletion();
                    ConditionalDurationAchievements[Achievements.Hardcore.NotGoingAnywhere].CheckCompletion();
                    ConditionalDurationAchievements[Achievements.Hardcore.HouseTrained].CheckCompletion();
                }
                else if (!actionWasFromClient && state is NewState.Disabled)
                {
                    ConditionalDurationAchievements[Achievements.Hardcore.PetTraining].ResetOrComplete();
                    ConditionalDurationAchievements[Achievements.Hardcore.NotGoingAnywhere].ResetOrComplete();
                    ConditionalDurationAchievements[Achievements.Hardcore.HouseTrained].ResetOrComplete();
                }
                break;
            case HardcorePairActionKind.ForcedBlindfold:
                // if we have had our blindfold set to enabled by another pair, perform the following:
                if (!actionWasFromClient && state is NewState.Enabled)
                {
                    ConditionalAchievements[Achievements.Hardcore.BlindLeadingTheBlind].CheckCompletion();
                    ConditionalDurationAchievements[Achievements.Hardcore.WhoNeedsToSee].CheckCompletion();
                }
                // if another pair is removing our blindfold, perform the following:
                else if (!actionWasFromClient && state is NewState.Disabled)
                {
                    ConditionalDurationAchievements[Achievements.Hardcore.WhoNeedsToSee].ResetOrComplete();
                }
                break;

        }
    }

    private void OnShockSent()
    {
        ProgressAchievements[Achievements.Hardcore.IndulgingSparks].IncrementProgress();
        ProgressAchievements[Achievements.Hardcore.CantGetEnough].IncrementProgress();
        ProgressAchievements[Achievements.Hardcore.VerThunder].IncrementProgress();
        ProgressAchievements[Achievements.Hardcore.WickedThunder].IncrementProgress();
        ProgressAchievements[Achievements.Hardcore.ElectropeHasNoLimits].IncrementProgress();
    }

    private void OnShockReceived()
    {
        ProgressAchievements[Achievements.Hardcore.ShockAndAwe].IncrementProgress();
        ProgressAchievements[Achievements.Hardcore.ShockingExperience].IncrementProgress();
        ProgressAchievements[Achievements.Hardcore.ShockolateTasting].IncrementProgress();
        ProgressAchievements[Achievements.Hardcore.ShockAddiction].IncrementProgress();
        ProgressAchievements[Achievements.Hardcore.WarriorOfElectrope].IncrementProgress();
    }


    private void OnChatMessage(XivChatType channel, string message, string SenderName)
    {
        if (message.Split(' ').Count() > 5)
        {
            if (channel is XivChatType.Say)
            {
                ProgressAchievements[Achievements.Gags.SpeakUpSlut].IncrementProgress();
            }
            else if (channel is XivChatType.Yell)
            {
                ProgressAchievements[Achievements.Gags.CantHearYou].IncrementProgress();
            }
            else if (channel is XivChatType.Shout)
            {
                ProgressAchievements[Achievements.Gags.OneMoreForTheCrowd].IncrementProgress();
            }
        }
        // check if we meet some of the secret requirements.
        ConditionalAchievements[Achievements.Secrets.HelplessDamsel].CheckCompletion();
    }

    private void OnEmoteExecuted(ulong emoteCallerObjectId, ushort emoteId, string emoteName, ulong targetObjectId)
    {
        // doing /lookout while blindfolded.
        if (!ConditionalAchievements[Achievements.Hardcore.WhatAView].IsCompleted && emoteCallerObjectId == _frameworkUtils.ClientState.LocalPlayer?.GameObjectId)
            if (emoteName.Contains("lookout", StringComparison.OrdinalIgnoreCase))
                ConditionalAchievements[Achievements.Hardcore.WhatAView].CheckCompletion();

        // detect getting slapped.
        if (!ConditionalAchievements[Achievements.Generic.ICantBelieveYouveDoneThis].IsCompleted && targetObjectId == _frameworkUtils.ClientState.LocalPlayer?.GameObjectId)
            if (emoteName.Contains("slap", StringComparison.OrdinalIgnoreCase))
                ConditionalAchievements[Achievements.Generic.ICantBelieveYouveDoneThis].CheckCompletion();
    }

    private void OnPuppeteerEmoteSent(string emoteName)
    {
        if (emoteName.Contains("shush", StringComparison.OrdinalIgnoreCase))
        {
            ConditionalAchievements[Achievements.Gags.QuietNowDear].CheckCompletion();
        }
        else if (emoteName.Contains("dance", StringComparison.OrdinalIgnoreCase))
        {
            ProgressAchievements[Achievements.Puppeteer.ShowingOff].IncrementProgress();
        }
        else if (emoteName.Contains("grovel", StringComparison.OrdinalIgnoreCase))
        {
            ProgressAchievements[Achievements.Puppeteer.KissMyHeels].IncrementProgress();
        }
        else if (emoteName.Contains("sulk", StringComparison.OrdinalIgnoreCase))
        {
            ProgressAchievements[Achievements.Puppeteer.Ashamed].IncrementProgress();
        }
        else if (emoteName.Contains("sit", StringComparison.OrdinalIgnoreCase) || emoteName.Contains("groundsit", StringComparison.OrdinalIgnoreCase))
        {
            ProgressAchievements[Achievements.Puppeteer.WhoIsAGoodPet].IncrementProgress();
        }
    }

    private void OnPairAdded()
    {
        ConditionalAchievements[Achievements.Generic.AddedFirstPair].CheckCompletion();
        ProgressAchievements[Achievements.Generic.TheCollector].IncrementProgress();
    }

    private void OnCursedLootFound()
    {
        ProgressAchievements[Achievements.Generic.BadEndSeeker].IncrementProgress();
        ProgressAchievements[Achievements.Generic.TemptingFatesTreasure].IncrementProgress();
    }

    private void OnJobChange(GlamourUpdateType changeType)
    {
        ConditionalAchievements[Achievements.Generic.EscapingIsNotEasy].CheckCompletion();
    }
}
