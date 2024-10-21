using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Text;
using GagSpeak.GagspeakConfiguration.Models;
using GagSpeak.UpdateMonitoring.Triggers;
using GagSpeak.Utils;
using GagSpeak.WebAPI;
using GagspeakAPI.Extensions;
using Penumbra.GameData.Enums;

namespace GagSpeak.Achievements;
public partial class AchievementManager
{
    private void OnCommendationsGiven(int amount)
    {
        (SaveData.Achievements[AchievementModuleKind.Secrets].Achievements[SecretLabels.KinkyTeacher] as ConditionalProgressAchievement)?.CheckTaskProgress(amount);
        (SaveData.Achievements[AchievementModuleKind.Secrets].Achievements[SecretLabels.KinkyProfessor] as ConditionalProgressAchievement)?.CheckTaskProgress(amount);
        (SaveData.Achievements[AchievementModuleKind.Secrets].Achievements[SecretLabels.KinkyMentor] as ConditionalProgressAchievement)?.CheckTaskProgress(amount);
    }

    private void OnPairVisible()
    {
        // We need to obtain the total visible user count, then update the respective achievements.
        var visiblePairs = _pairManager.GetVisibleUserCount();
        (SaveData.Achievements[AchievementModuleKind.Secrets].Achievements[SecretLabels.BondageClub] as ThresholdAchievement)?.UpdateThreshold(visiblePairs);
        (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.Humiliation] as ConditionalThresholdAchievement)?.UpdateThreshold(visiblePairs);
    }

    private void OnIconClicked(string windowLabel)
    {
        if (SaveData.EasterEggIcons.ContainsKey(windowLabel) && SaveData.EasterEggIcons[windowLabel] is false)
        {
            if (SaveData.EasterEggIcons[windowLabel])
                return;
            else
                SaveData.EasterEggIcons[windowLabel] = true;
            // update progress.
            (SaveData.Achievements[AchievementModuleKind.Secrets].Achievements[SecretLabels.TooltipLogos] as ProgressAchievement)?.IncrementProgress();
        }
    }

    private void CheckOnZoneSwitchEnd()
    {
        if (_frameworkUtils.IsInMainCity)
        {
            (SaveData.Achievements[AchievementModuleKind.Hardcore].Achievements[HardcoreLabels.WalkOfShame] as ConditionalAchievement)?.CheckCompletion();
        }

        // if present in diadem (for diamdem achievement)
        if (_frameworkUtils.ClientState.TerritoryType is 939)
            (SaveData.Achievements[AchievementModuleKind.Toybox].Achievements[ToyboxLabels.MotivationForRestoration] as TimeRequiredConditionalAchievement)?.CheckCompletion();
        else
            (SaveData.Achievements[AchievementModuleKind.Toybox].Achievements[ToyboxLabels.MotivationForRestoration] as TimeRequiredConditionalAchievement)?.CheckCompletion();

        // If we left before completing the duty, check that here.
        if ((SaveData.Achievements[AchievementModuleKind.Hardcore].Achievements[HardcoreLabels.UCanTieThis] as ConditionalProgressAchievement)?.ConditionalTaskBegun ?? false)
            (SaveData.Achievements[AchievementModuleKind.Hardcore].Achievements[HardcoreLabels.UCanTieThis] as ConditionalProgressAchievement)?.CheckTaskProgress();

        if ((SaveData.Achievements[AchievementModuleKind.Gags].Achievements[GagLabels.SilentButDeadly] as ConditionalProgressAchievement)?.ConditionalTaskBegun ?? false)
            (SaveData.Achievements[AchievementModuleKind.Gags].Achievements[GagLabels.SilentButDeadly] as ConditionalProgressAchievement)?.CheckTaskProgress();

        if ((SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.HealSlut] as ConditionalProgressAchievement)?.ConditionalTaskBegun ?? false)
            (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.HealSlut] as ConditionalProgressAchievement)?.CheckTaskProgress();

    }

    private void CheckDeepDungeonStatus()
    {
        // Detect Specific Dungeon Types
        if (!AchievementHelpers.InDeepDungeon()) return;

        var floor = AchievementHelpers.GetFloor();
        if (floor is null) return;

        var deepDungeonType = _frameworkUtils.GetDeepDungeonType();
        if (deepDungeonType == null) return;

        if (_frameworkUtils.PartyListSize is 1)
            (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.MyKinksRunDeeper] as ConditionalProgressAchievement)?.BeginConditionalTask();

        switch (deepDungeonType)
        {
            case DeepDungeonType.PalaceOfTheDead:
                if ((floor > 40 && floor <= 50) || (floor > 90 && floor <= 100))
                {
                    (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.BondagePalace] as ConditionalProgressAchievement)?.BeginConditionalTask();
                    (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.MyKinkRunsDeep] as ConditionalProgressAchievement)?.BeginConditionalTask();
                    if (floor is 50 || floor is 100)
                    {
                        (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.BondagePalace] as ConditionalProgressAchievement)?.FinishConditionalTask();
                        (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.MyKinkRunsDeep] as ConditionalProgressAchievement)?.FinishConditionalTask();
                        (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.MyKinksRunDeeper] as ConditionalProgressAchievement)?.FinishConditionalTask();
                    }
                }
                break;
            case DeepDungeonType.HeavenOnHigh:
                if (floor > 20 && floor <= 30)
                {
                    (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.HornyOnHigh] as ConditionalProgressAchievement)?.BeginConditionalTask();
                    (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.MyKinkRunsDeep] as ConditionalProgressAchievement)?.BeginConditionalTask();
                    if (floor is 30)
                    {
                        (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.BondagePalace] as ConditionalProgressAchievement)?.FinishConditionalTask();
                        (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.MyKinkRunsDeep] as ConditionalProgressAchievement)?.FinishConditionalTask();
                        (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.MyKinksRunDeeper] as ConditionalProgressAchievement)?.FinishConditionalTask();
                    }
                }
                break;
            case DeepDungeonType.EurekaOrthos:
                if (floor > 20 && floor <= 30)
                {
                    (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.EurekaWhorethos] as ConditionalProgressAchievement)?.BeginConditionalTask();
                    (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.MyKinkRunsDeep] as ConditionalProgressAchievement)?.BeginConditionalTask();
                    if (floor is 30)
                    {
                        (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.EurekaWhorethos] as ConditionalProgressAchievement)?.FinishConditionalTask();
                        (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.MyKinkRunsDeep] as ConditionalProgressAchievement)?.FinishConditionalTask();
                        (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.MyKinksRunDeeper] as ConditionalProgressAchievement)?.FinishConditionalTask();
                    }
                }
                break;
        }
    }

    private void OnOrderAction(OrderInteractionKind orderKind)
    {
        switch (orderKind)
        {
            case OrderInteractionKind.Completed:
                (SaveData.Achievements[AchievementModuleKind.Orders].Achievements[OrderLabels.JustAVolunteer] as ProgressAchievement)?.IncrementProgress();
                (SaveData.Achievements[AchievementModuleKind.Orders].Achievements[OrderLabels.AsYouCommand] as ProgressAchievement)?.IncrementProgress();
                (SaveData.Achievements[AchievementModuleKind.Orders].Achievements[OrderLabels.AnythingForMyOwner] as ProgressAchievement)?.IncrementProgress();
                (SaveData.Achievements[AchievementModuleKind.Orders].Achievements[OrderLabels.GoodDrone] as ProgressAchievement)?.IncrementProgress();
                break;
            case OrderInteractionKind.Fail:
                (SaveData.Achievements[AchievementModuleKind.Orders].Achievements[OrderLabels.BadSlut] as ProgressAchievement)?.IncrementProgress();
                (SaveData.Achievements[AchievementModuleKind.Orders].Achievements[OrderLabels.NeedsTraining] as ProgressAchievement)?.IncrementProgress();
                (SaveData.Achievements[AchievementModuleKind.Orders].Achievements[OrderLabels.UsefulInOtherWays] as ProgressAchievement)?.IncrementProgress();
                break;
            case OrderInteractionKind.Create:
                (SaveData.Achievements[AchievementModuleKind.Orders].Achievements[OrderLabels.NewSlaveOwner] as ProgressAchievement)?.IncrementProgress();
                (SaveData.Achievements[AchievementModuleKind.Orders].Achievements[OrderLabels.TaskManager] as ProgressAchievement)?.IncrementProgress();
                (SaveData.Achievements[AchievementModuleKind.Orders].Achievements[OrderLabels.MaidMaster] as ProgressAchievement)?.IncrementProgress();
                (SaveData.Achievements[AchievementModuleKind.Orders].Achievements[OrderLabels.QueenOfDrones] as ProgressAchievement)?.IncrementProgress();
                break;
        }
    }

    /// <summary>
    /// Determines what to do once a Gag is Applied
    /// </summary>
    /// <param name="gagLayer"> The Layer the gag was applied to. </param>
    /// <param name="gagType"> The type of Gag Applied. </param>
    /// <param name="isSelfApplied"> If it was applied by the client or not. </param>
    /// <param name="fromInitialConnection"> If this event was triggered from the OnConnectedService. </param>
    private void OnGagApplied(GagLayer gagLayer, GagType gagType, bool isSelfApplied, bool fromInitialConnection)
    {
        if (gagType is GagType.None) return;

        // if this is not from an initial connection..
        if(!fromInitialConnection)
        {
            // the gag was applied to us by ourselves.
            if (isSelfApplied)
            {
                (SaveData.Achievements[AchievementModuleKind.Gags].Achievements[GagLabels.SelfApplied] as ProgressAchievement)?.IncrementProgress();
            }
            // the gag was applied to us by someone else.
            else
            {
                (SaveData.Achievements[AchievementModuleKind.Gags].Achievements[GagLabels.LookingForTheRightFit] as ProgressAchievement)?.IncrementProgress();
                (SaveData.Achievements[AchievementModuleKind.Gags].Achievements[GagLabels.OralFixation] as ProgressAchievement)?.IncrementProgress();
                (SaveData.Achievements[AchievementModuleKind.Gags].Achievements[GagLabels.AKinkForDrool] as ProgressAchievement)?.IncrementProgress();

                (SaveData.Achievements[AchievementModuleKind.Gags].Achievements[GagLabels.ATrueGagSlut] as TimedProgressAchievement)?.IncrementProgress();
            }

            (SaveData.Achievements[AchievementModuleKind.Gags].Achievements[GagLabels.SpeechSilverSilenceGolden] as DurationAchievement)?.StartTracking(gagType.GagName()); // no method for remove to stop this added?
            (SaveData.Achievements[AchievementModuleKind.Gags].Achievements[GagLabels.TheKinkyLegend] as DurationAchievement)?.StartTracking(gagType.GagName()); // no method for remove to stop this added?
            (SaveData.Achievements[AchievementModuleKind.Secrets].Achievements[SecretLabels.Experimentalist] as ConditionalAchievement)?.CheckCompletion();
            (SaveData.Achievements[AchievementModuleKind.Secrets].Achievements[SecretLabels.GaggedPleasure] as ConditionalAchievement)?.CheckCompletion();
        }
        // Check regardless of it being an initial server connection or not.
        (SaveData.Achievements[AchievementModuleKind.Gags].Achievements[GagLabels.ShushtainableResource] as ThresholdAchievement)?.UpdateThreshold(_playerData.TotalGagsEquipped);
        (SaveData.Achievements[AchievementModuleKind.Gags].Achievements[GagLabels.ShushtainableResource] as ConditionalAchievement)?.CheckCompletion();
    }

    private void OnGagRemoval(GagLayer layer, GagType gagType, bool isSelfApplied)
    {
        (SaveData.Achievements[AchievementModuleKind.Gags].Achievements[GagLabels.ShushtainableResource] as ThresholdAchievement)?.UpdateThreshold(_playerData.TotalGagsEquipped);

        (SaveData.Achievements[AchievementModuleKind.Gags].Achievements[GagLabels.SpeechSilverSilenceGolden] as DurationAchievement)?.StopTracking(gagType.GagName()); // no method for remove to stop this added?
        (SaveData.Achievements[AchievementModuleKind.Gags].Achievements[GagLabels.TheKinkyLegend] as DurationAchievement)?.StopTracking(gagType.GagName()); // no method for remove to stop this added?

        // Halt our Silent But Deadly Progress if gag is removed mid-dungeon
        if ((SaveData.Achievements[AchievementModuleKind.Gags].Achievements[GagLabels.SilentButDeadly] as ConditionalProgressAchievement)?.ConditionalTaskBegun ?? false)
            (SaveData.Achievements[AchievementModuleKind.Gags].Achievements[GagLabels.SilentButDeadly] as ConditionalProgressAchievement)?.CheckTaskProgress();

    }

    private void OnPairGagApplied(GagType gag)
    {
        if (gag is not GagType.None)
        {
            (SaveData.Achievements[AchievementModuleKind.Gags].Achievements[GagLabels.ApplyToPair] as ProgressAchievement)?.IncrementProgress();
            (SaveData.Achievements[AchievementModuleKind.Gags].Achievements[GagLabels.LookingForTheRightFit] as ProgressAchievement)?.IncrementProgress();
            (SaveData.Achievements[AchievementModuleKind.Gags].Achievements[GagLabels.OralFixation] as ProgressAchievement)?.IncrementProgress();
            (SaveData.Achievements[AchievementModuleKind.Gags].Achievements[GagLabels.AKinkForDrool] as ProgressAchievement)?.IncrementProgress();

            (SaveData.Achievements[AchievementModuleKind.Gags].Achievements[GagLabels.ApplyToPair] as ProgressAchievement)?.IncrementProgress();

            (SaveData.Achievements[AchievementModuleKind.Gags].Achievements[GagLabels.YourFavoriteNurse] as ConditionalProgressAchievement)?.CheckTaskProgress();
        }
    }

    private void OnRestraintSetUpdated(RestraintSet set)
    {
        // check for dyes
        if (set.DrawData.Any(x => x.Value.GameStain.Stain1 != 0 || x.Value.GameStain.Stain2 != 0))
        {
            (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.ToDyeFor] as ProgressAchievement)?.IncrementProgress();
            (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.DyeAnotherDay] as ProgressAchievement)?.IncrementProgress();
            (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.DyeHard] as ProgressAchievement)?.IncrementProgress();
        }
    }

    private void OnRestraintApplied(RestraintSet set, bool isEnabling, string enactorUID)
    {
        if (isEnabling)
        {
            (SaveData.Achievements[AchievementModuleKind.Secrets].Achievements[SecretLabels.Experimentalist] as ConditionalAchievement)?.CheckCompletion();
            (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.FirstTiemers] as ProgressAchievement)?.IncrementProgress();

            // if we are the applier
            if (enactorUID is Globals.SelfApplied)
            {
                (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.SelfBondageEnthusiast] as ProgressAchievement)?.IncrementProgress();
            }
            else // someone else is enabling our set
            {
                (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.AuctionedOff] as ConditionalProgressAchievement)?.BeginConditionalTask();
                // starts the timer.
                (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.Bondodge] as TimeLimitConditionalAchievement)?.CheckCompletion();

                // see if valid for "cuffed-19"
                if (set.DrawData.TryGetValue(EquipSlot.Hands, out var handData) && handData.GameItem.Id != ItemIdVars.NothingItem(EquipSlot.Hands).Id)
                {
                    (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.Cuffed19] as ProgressAchievement)?.IncrementProgress();
                }
            }
        }
        else // set is being disabled
        {
            if (enactorUID is not Globals.SelfApplied)
            {
                // verify that the set is being disabled by someone else.
                if (set.LockedBy != enactorUID)
                {
                    // the assigner and remover were different, so you are being auctioned off.
                    (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.AuctionedOff] as ConditionalProgressAchievement)?.FinishConditionalTask();
                }

                // must be removed within limit or wont award.
                (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.Bondodge] as TimeLimitConditionalAchievement)?.CheckCompletion();

                (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.BondageBunny] as TimedProgressAchievement)?.IncrementProgress();
            }
        }
    }

    private void OnRestraintLock(RestraintSet set, Padlocks padlock, bool isLocking, string enactorUID)
    {
        // we locked our set.
        if (enactorUID is Globals.SelfApplied)
        {
            // nothing here atm.
        }
        // someone else locked our set
        else if (enactorUID is not Globals.SelfApplied)
        {
            if (isLocking && padlock is not Padlocks.None or Padlocks.FiveMinutesPadlock) // locking
            {
                (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.FirstTimeBondage] as DurationAchievement)?.StartTracking(set.Name);
                (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.AmateurBondage] as DurationAchievement)?.StartTracking(set.Name);
                (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.ComfortRestraint] as DurationAchievement)?.StartTracking(set.Name);
                (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.DayInTheLifeOfABondageSlave] as DurationAchievement)?.StartTracking(set.Name);
                (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.AWeekInBondage] as DurationAchievement)?.StartTracking(set.Name);
                (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.AMonthInBondage] as DurationAchievement)?.StartTracking(set.Name);
            }
            if (!isLocking && padlock is not Padlocks.None or Padlocks.FiveMinutesPadlock) // unlocking
            {
                (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.FirstTimeBondage] as DurationAchievement)?.StopTracking(set.Name);
                (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.AmateurBondage] as DurationAchievement)?.StopTracking(set.Name);
                (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.ComfortRestraint] as DurationAchievement)?.StopTracking(set.Name);
                (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.DayInTheLifeOfABondageSlave] as DurationAchievement)?.StopTracking(set.Name);
                (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.AWeekInBondage] as DurationAchievement)?.StopTracking(set.Name);
                (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.AMonthInBondage] as DurationAchievement)?.StopTracking(set.Name);
            }
        }
    }

    /// <summary>
    /// Whenever we are applying a restraint set to a pair. This is fired in our pair manager once we recieve 
    /// </summary>
    private void OnPairRestraintApply(Guid setName, bool isEnabling, string enactorUID)
    {
        // if we enabled a set on someone else
        if (isEnabling && enactorUID == ApiController.UID)
        {
            (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.FirstTiemers] as ProgressAchievement)?.IncrementProgress();
            (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.DiDEnthusiast] as ProgressAchievement)?.IncrementProgress();
            (SaveData.Achievements[AchievementModuleKind.Gags].Achievements[GagLabels.YourFavoriteNurse] as ConditionalProgressAchievement)?.CheckTaskProgress();
        }
    }

    private void OnPairRestraintLockChange(Guid restraintId, Padlocks padlock, bool isLocking, string enactorUID) // uid is self applied if client.
    {
        // May need to figure this for pairs upon connection to validate any actions/unlocks that occured while we were away.
        Logger.LogInformation("Pair Restraint Lock Change: " + padlock.ToName() + " " + isLocking + " " + enactorUID);


        // Change the achievement type of the achievement below, its currently busted.
        // (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.SoldSlave] as ProgressAchievement)?.IncrementProgress();

        // if the pair's set is being locked and it is a timed lock.
        if (isLocking)
        {
            if (padlock is not Padlocks.None or Padlocks.FiveMinutesPadlock) // locking
            {
                // make sure we are the locker before continuing
                if(enactorUID == ApiController.UID)
                {
                    (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.RiggersFirstSession] as DurationAchievement)?.StartTracking(enactorUID);
                    (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.MyLittlePlaything] as DurationAchievement)?.StartTracking(enactorUID);
                    (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.SuitsYouBitch] as DurationAchievement)?.StartTracking(enactorUID);
                    (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.TiesThatBind] as DurationAchievement)?.StartTracking(enactorUID);
                    (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.SlaveTraining] as DurationAchievement)?.StartTracking(enactorUID);
                    (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.CeremonyOfEternalBondage] as DurationAchievement)?.StartTracking(enactorUID);
                }
            }
        }
        if(!isLocking)
        {
            // if the padlock is a timed padlock that we have unlocked, we should stop tracking it from these achievements.
            if (padlock is not Padlocks.None or Padlocks.FiveMinutesPadlock)
            {
                (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.RiggersFirstSession] as DurationAchievement)?.StopTracking(enactorUID);
                (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.MyLittlePlaything] as DurationAchievement)?.StopTracking(enactorUID);
                (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.SuitsYouBitch] as DurationAchievement)?.StopTracking(enactorUID);
                (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.TiesThatBind] as DurationAchievement)?.StopTracking(enactorUID);
                (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.SlaveTraining] as DurationAchievement)?.StopTracking(enactorUID);
                (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.CeremonyOfEternalBondage] as DurationAchievement)?.StopTracking(enactorUID);
            }

            // if we are unlocking in general, increment the rescuer
            (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.TheRescuer] as ProgressAchievement)?.IncrementProgress();
        }
    }

    private void OnPuppetAccessGiven(bool wasAllPerms)
    {
        if (wasAllPerms) // All Perms access given to another pair.
            (SaveData.Achievements[AchievementModuleKind.Puppeteer].Achievements[PuppeteerLabels.CompleteDevotion] as ProgressAchievement)?.IncrementProgress();
        else // Emote perms given to another pair.
            (SaveData.Achievements[AchievementModuleKind.Puppeteer].Achievements[PuppeteerLabels.ControlMyBody] as ProgressAchievement)?.IncrementProgress();
    }

    private void OnPatternAction(PatternInteractionKind actionType, Guid patternGuid, bool wasAlarm)
    {
        switch (actionType)
        {
            case PatternInteractionKind.Published:
                (SaveData.Achievements[AchievementModuleKind.Toybox].Achievements[ToyboxLabels.FunForAll] as ProgressAchievement)?.IncrementProgress();
                (SaveData.Achievements[AchievementModuleKind.Toybox].Achievements[ToyboxLabels.DeviousComposer] as ProgressAchievement)?.IncrementProgress();
                break;
            case PatternInteractionKind.Downloaded:
                (SaveData.Achievements[AchievementModuleKind.Toybox].Achievements[ToyboxLabels.CravingPleasure] as ProgressAchievement)?.IncrementProgress();
                break;
            case PatternInteractionKind.Liked:
                (SaveData.Achievements[AchievementModuleKind.Toybox].Achievements[ToyboxLabels.PatternLover] as ProgressAchievement)?.IncrementProgress();
                break;
            case PatternInteractionKind.Started:
                if (patternGuid != Guid.Empty)
                    (SaveData.Achievements[AchievementModuleKind.Toybox].Achievements[ToyboxLabels.EnduranceQueen] as DurationAchievement)?.StartTracking(patternGuid.ToString());
                if (wasAlarm && patternGuid != Guid.Empty)
                    (SaveData.Achievements[AchievementModuleKind.Toybox].Achievements[ToyboxLabels.HornyMornings] as ProgressAchievement)?.IncrementProgress();
                break;
            case PatternInteractionKind.Stopped:
                if (patternGuid != Guid.Empty)
                    (SaveData.Achievements[AchievementModuleKind.Toybox].Achievements[ToyboxLabels.EnduranceQueen] as DurationAchievement)?.StopTracking(patternGuid.ToString());
                break;
        }
    }

    private void OnDeviceConnected()
    {
        (SaveData.Achievements[AchievementModuleKind.Toybox].Achievements[ToyboxLabels.MyFavoriteToys] as ConditionalAchievement)?.CheckCompletion();
        (SaveData.Achievements[AchievementModuleKind.Secrets].Achievements[SecretLabels.Experimentalist] as ConditionalAchievement)?.CheckCompletion();
    }

    private void OnTriggerFired()
    {
        (SaveData.Achievements[AchievementModuleKind.Toybox].Achievements[ToyboxLabels.SubtleReminders] as ProgressAchievement)?.IncrementProgress();
        (SaveData.Achievements[AchievementModuleKind.Toybox].Achievements[ToyboxLabels.FingerOnTheTrigger] as ProgressAchievement)?.IncrementProgress();
        (SaveData.Achievements[AchievementModuleKind.Toybox].Achievements[ToyboxLabels.TriggerHappy] as ProgressAchievement)?.IncrementProgress();
    }

    /// <summary>
    /// For whenever a hardcore action begins or finishes on either the client or a pair of the client.
    /// </summary>
    /// <param name="actionKind"> The kind of hardcore action that was performed. </param>
    /// <param name="state"> If the hardcore action began or ended. </param>
    /// <param name="affectedPairUID"> who the target of the action is. </param>
    /// <param name="enactorUID"> Who Called the action. </param>
    private void OnHardcoreForcedPairAction(HardcoreAction actionKind, NewState state, string enactorUID, string affectedPairUID)
    {
        switch (actionKind)
        {
            case HardcoreAction.ForcedFollow:
                // if we are the enactor and the pair is the target:
                if (enactorUID == ApiController.UID)
                {
                    // if the state is enabled, begin tracking the pair we forced.
                    if (state is NewState.Enabled)
                    {
                        (SaveData.Achievements[AchievementModuleKind.Hardcore].Achievements[HardcoreLabels.AllTheCollarsOfTheRainbow] as ProgressAchievement)?.IncrementProgress();
                        (SaveData.Achievements[AchievementModuleKind.Hardcore].Achievements[HardcoreLabels.ForcedFollow] as DurationAchievement)?.StartTracking(affectedPairUID);
                        (SaveData.Achievements[AchievementModuleKind.Hardcore].Achievements[HardcoreLabels.ForcedWalkies] as DurationAchievement)?.StartTracking(affectedPairUID);
                    }
                }
                // if the affected pair is not our clients UID and the action is disabling, stop tracking for anything we started. (can ignore the enactor)
                if (affectedPairUID != ApiController.UID && state is NewState.Disabled)
                {
                    (SaveData.Achievements[AchievementModuleKind.Hardcore].Achievements[HardcoreLabels.ForcedFollow] as DurationAchievement)?.StopTracking(affectedPairUID);
                    (SaveData.Achievements[AchievementModuleKind.Hardcore].Achievements[HardcoreLabels.ForcedWalkies] as DurationAchievement)?.StopTracking(affectedPairUID);
                }

                // if UCanTieThis was active, but our follow stopped, we should check / reset our dungeon walkies achievement.
                if (affectedPairUID == ApiController.UID && state is NewState.Disabled)
                    (SaveData.Achievements[AchievementModuleKind.Hardcore].Achievements[HardcoreLabels.UCanTieThis] as ConditionalProgressAchievement)?.CheckTaskProgress();

                // if the affected pair was us:
                if (affectedPairUID == ApiController.UID)
                {
                    // if the new state is enabled, we should begin tracking the time required completion.
                    if (state is NewState.Enabled)
                    {
                        (SaveData.Achievements[AchievementModuleKind.Hardcore].Achievements[HardcoreLabels.TimeForWalkies] as TimeRequiredConditionalAchievement)?.CheckCompletion();
                        (SaveData.Achievements[AchievementModuleKind.Hardcore].Achievements[HardcoreLabels.GettingStepsIn] as TimeRequiredConditionalAchievement)?.CheckCompletion();
                        (SaveData.Achievements[AchievementModuleKind.Hardcore].Achievements[HardcoreLabels.WalkiesLover] as TimeRequiredConditionalAchievement)?.CheckCompletion();
                    }
                    else // and if our state switches to disabled, we should halt the progression.
                    {
                        if ((SaveData.Achievements[AchievementModuleKind.Hardcore].Achievements[HardcoreLabels.TimeForWalkies] as TimeRequiredConditionalAchievement)?.StartPoint != DateTime.MinValue)
                            (SaveData.Achievements[AchievementModuleKind.Hardcore].Achievements[HardcoreLabels.TimeForWalkies] as TimeRequiredConditionalAchievement)?.CheckCompletion();

                        if ((SaveData.Achievements[AchievementModuleKind.Hardcore].Achievements[HardcoreLabels.GettingStepsIn] as TimeRequiredConditionalAchievement)?.StartPoint != DateTime.MinValue)
                            (SaveData.Achievements[AchievementModuleKind.Hardcore].Achievements[HardcoreLabels.GettingStepsIn] as TimeRequiredConditionalAchievement)?.CheckCompletion();

                        if ((SaveData.Achievements[AchievementModuleKind.Hardcore].Achievements[HardcoreLabels.WalkiesLover] as TimeRequiredConditionalAchievement)?.StartPoint != DateTime.MinValue)
                            (SaveData.Achievements[AchievementModuleKind.Hardcore].Achievements[HardcoreLabels.WalkiesLover] as TimeRequiredConditionalAchievement)?.CheckCompletion();
                    }
                }
                break;
            case HardcoreAction.ForcedSit:
                // if we are the affected UID:
                if (affectedPairUID == ApiController.UID)
                {
                    if (state is NewState.Enabled)
                        (SaveData.Achievements[AchievementModuleKind.Hardcore].Achievements[HardcoreLabels.LivingFurniture] as TimeRequiredConditionalAchievement)?.CheckCompletion();

                    if (state is NewState.Disabled)
                        if ((SaveData.Achievements[AchievementModuleKind.Hardcore].Achievements[HardcoreLabels.LivingFurniture] as TimeRequiredConditionalAchievement)?.StartPoint != DateTime.MinValue)
                            (SaveData.Achievements[AchievementModuleKind.Hardcore].Achievements[HardcoreLabels.LivingFurniture] as TimeRequiredConditionalAchievement)?.CheckCompletion();
                }
                break;
            case HardcoreAction.ForcedStay:
                // if we are the affected UID:
                if (affectedPairUID == ApiController.UID)
                {
                    // and we have been ordered to start being forced to stay:
                    if (state is NewState.Enabled)
                    {
                        (SaveData.Achievements[AchievementModuleKind.Hardcore].Achievements[HardcoreLabels.PetTraining] as TimeRequiredConditionalAchievement)?.CheckCompletion();
                        (SaveData.Achievements[AchievementModuleKind.Hardcore].Achievements[HardcoreLabels.NotGoingAnywhere] as TimeRequiredConditionalAchievement)?.CheckCompletion();
                        (SaveData.Achievements[AchievementModuleKind.Hardcore].Achievements[HardcoreLabels.HouseTrained] as TimeRequiredConditionalAchievement)?.CheckCompletion();
                    }
                    else // our forced to stay has ended
                    {
                        if ((SaveData.Achievements[AchievementModuleKind.Hardcore].Achievements[HardcoreLabels.PetTraining] as TimeRequiredConditionalAchievement)?.StartPoint != DateTime.MinValue)
                            (SaveData.Achievements[AchievementModuleKind.Hardcore].Achievements[HardcoreLabels.PetTraining] as TimeRequiredConditionalAchievement)?.CheckCompletion();

                        if ((SaveData.Achievements[AchievementModuleKind.Hardcore].Achievements[HardcoreLabels.NotGoingAnywhere] as TimeRequiredConditionalAchievement)?.StartPoint != DateTime.MinValue)
                            (SaveData.Achievements[AchievementModuleKind.Hardcore].Achievements[HardcoreLabels.NotGoingAnywhere] as TimeRequiredConditionalAchievement)?.CheckCompletion();

                        if ((SaveData.Achievements[AchievementModuleKind.Hardcore].Achievements[HardcoreLabels.HouseTrained] as TimeRequiredConditionalAchievement)?.StartPoint != DateTime.MinValue)
                            (SaveData.Achievements[AchievementModuleKind.Hardcore].Achievements[HardcoreLabels.HouseTrained] as TimeRequiredConditionalAchievement)?.CheckCompletion();
                    }
                }
                break;
            case HardcoreAction.ForcedBlindfold:
                // if we are the affected UID:
                if (affectedPairUID == ApiController.UID)
                {
                    // if we have had our blindfold set to enabled by another pair, perform the following:
                    if (state is NewState.Enabled)
                    {
                        (SaveData.Achievements[AchievementModuleKind.Hardcore].Achievements[HardcoreLabels.BlindLeadingTheBlind] as ConditionalAchievement)?.CheckCompletion();
                        (SaveData.Achievements[AchievementModuleKind.Hardcore].Achievements[HardcoreLabels.WhoNeedsToSee] as TimeRequiredConditionalAchievement)?.CheckCompletion();
                    }
                    // if another pair is removing our blindfold, perform the following:
                    if (state is NewState.Disabled)
                        if ((SaveData.Achievements[AchievementModuleKind.Hardcore].Achievements[HardcoreLabels.WhoNeedsToSee] as TimeRequiredConditionalAchievement)?.StartPoint != DateTime.MinValue)
                            (SaveData.Achievements[AchievementModuleKind.Hardcore].Achievements[HardcoreLabels.WhoNeedsToSee] as TimeRequiredConditionalAchievement)?.CheckCompletion();
                }
                break;
        }
    }

    private void OnShockSent()
    {
        (SaveData.Achievements[AchievementModuleKind.Hardcore].Achievements[HardcoreLabels.IndulgingSparks] as ProgressAchievement)?.IncrementProgress();
        (SaveData.Achievements[AchievementModuleKind.Hardcore].Achievements[HardcoreLabels.CantGetEnough] as ProgressAchievement)?.IncrementProgress();
        (SaveData.Achievements[AchievementModuleKind.Hardcore].Achievements[HardcoreLabels.VerThunder] as ProgressAchievement)?.IncrementProgress();
        (SaveData.Achievements[AchievementModuleKind.Hardcore].Achievements[HardcoreLabels.WickedThunder] as ProgressAchievement)?.IncrementProgress();
        (SaveData.Achievements[AchievementModuleKind.Hardcore].Achievements[HardcoreLabels.ElectropeHasNoLimits] as ProgressAchievement)?.IncrementProgress();
    }

    private void OnShockReceived()
    {
        (SaveData.Achievements[AchievementModuleKind.Hardcore].Achievements[HardcoreLabels.ShockAndAwe] as ProgressAchievement)?.IncrementProgress();
        (SaveData.Achievements[AchievementModuleKind.Hardcore].Achievements[HardcoreLabels.ShockingExperience] as ProgressAchievement)?.IncrementProgress();
        (SaveData.Achievements[AchievementModuleKind.Hardcore].Achievements[HardcoreLabels.ShockolateTasting] as ProgressAchievement)?.IncrementProgress();
        (SaveData.Achievements[AchievementModuleKind.Hardcore].Achievements[HardcoreLabels.ShockAddiction] as ProgressAchievement)?.IncrementProgress();
        (SaveData.Achievements[AchievementModuleKind.Hardcore].Achievements[HardcoreLabels.WarriorOfElectrope] as ProgressAchievement)?.IncrementProgress();
    }


    private void OnChatMessage(XivChatType channel)
    {
        (SaveData.Achievements[AchievementModuleKind.Secrets].Achievements[SecretLabels.HelplessDamsel] as ConditionalAchievement)?.CheckCompletion();

        if (channel is XivChatType.Say)
        {
            (SaveData.Achievements[AchievementModuleKind.Gags].Achievements[GagLabels.SpeakUpSlut] as ProgressAchievement)?.IncrementProgress();
        }
        else if (channel is XivChatType.Yell)
        {
            (SaveData.Achievements[AchievementModuleKind.Gags].Achievements[GagLabels.CantHearYou] as ProgressAchievement)?.IncrementProgress();
        }
        else if (channel is XivChatType.Shout)
        {
            (SaveData.Achievements[AchievementModuleKind.Gags].Achievements[GagLabels.OneMoreForTheCrowd] as ProgressAchievement)?.IncrementProgress();
        }
    }

    private void OnEmoteExecuted(IGameObject emoteCallerObj, ushort emoteId, string emoteName, IGameObject targetObject)
    {
        // doing /lookout while blindfolded.
        if (!(SaveData.Achievements[AchievementModuleKind.Hardcore].Achievements[HardcoreLabels.WhatAView] as ConditionalAchievement)?.IsCompleted ?? false && emoteCallerObj == _frameworkUtils.ClientState.LocalPlayer)
            if (emoteName.Contains("lookout", StringComparison.OrdinalIgnoreCase))
                (SaveData.Achievements[AchievementModuleKind.Hardcore].Achievements[HardcoreLabels.WhatAView] as ConditionalAchievement)?.CheckCompletion();

        // Detect shushing another gagspeak user.
        if (!(SaveData.Achievements[AchievementModuleKind.Gags].Achievements[GagLabels.QuietNowDear] as ConditionalAchievement)?.IsCompleted ?? false && emoteCallerObj == _frameworkUtils.ClientState.LocalPlayer)
            if (emoteName.Contains("shush", StringComparison.OrdinalIgnoreCase))
                (SaveData.Achievements[AchievementModuleKind.Gags].Achievements[GagLabels.QuietNowDear] as ConditionalAchievement)?.CheckCompletion();
        
        // detect getting slapped.
        if (!(SaveData.Achievements[AchievementModuleKind.Generic].Achievements[GenericLabels.ICantBelieveYouveDoneThis] as ConditionalAchievement)?.IsCompleted ?? false && targetObject == _frameworkUtils.ClientState.LocalPlayer)
            if (emoteName.Contains("slap", StringComparison.OrdinalIgnoreCase) && emoteCallerObj.ObjectIndex is not 0) // 0 is the clientPlayer object index.
                (SaveData.Achievements[AchievementModuleKind.Generic].Achievements[GenericLabels.ICantBelieveYouveDoneThis] as ConditionalAchievement)?.CheckCompletion();
    }

    private void OnPuppeteerEmoteSent(string emoteName)
    {
        if (emoteName.Contains("dance", StringComparison.OrdinalIgnoreCase))
        {
            (SaveData.Achievements[AchievementModuleKind.Puppeteer].Achievements[PuppeteerLabels.ShowingOff] as ProgressAchievement)?.IncrementProgress();
        }
        else if (emoteName.Contains("grovel", StringComparison.OrdinalIgnoreCase))
        {
            (SaveData.Achievements[AchievementModuleKind.Puppeteer].Achievements[PuppeteerLabels.KissMyHeels] as ProgressAchievement)?.IncrementProgress();
        }
        else if (emoteName.Contains("sulk", StringComparison.OrdinalIgnoreCase))
        {
            (SaveData.Achievements[AchievementModuleKind.Puppeteer].Achievements[PuppeteerLabels.Ashamed] as ProgressAchievement)?.IncrementProgress();
        }
        else if (emoteName.Contains("sit", StringComparison.OrdinalIgnoreCase) || emoteName.Contains("groundsit", StringComparison.OrdinalIgnoreCase))
        {
            (SaveData.Achievements[AchievementModuleKind.Puppeteer].Achievements[PuppeteerLabels.WhoIsAGoodPet] as ProgressAchievement)?.IncrementProgress();
        }
    }

    private void OnPairAdded()
    {
        (SaveData.Achievements[AchievementModuleKind.Generic].Achievements[GenericLabels.AddedFirstPair] as ConditionalAchievement)?.CheckCompletion();
        (SaveData.Achievements[AchievementModuleKind.Generic].Achievements[GenericLabels.TheCollector] as ProgressAchievement)?.IncrementProgress();
    }

    private void OnCursedLootFound()
    {
        (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.TemptingFatesTreasure] as ProgressAchievement)?.IncrementProgress();
        (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.BadEndSeeker] as ProgressAchievement)?.IncrementProgress();
        (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.EverCursed] as ProgressAchievement)?.IncrementProgress();
    }

    private void OnJobChange(GlamourUpdateType changeType)
    {
        (SaveData.Achievements[AchievementModuleKind.Generic].Achievements[GenericLabels.EscapingIsNotEasy] as ConditionalAchievement)?.CheckCompletion();
    }

    // We need to check for knockback effects in gold sacuer.
    private void OnActionEffectEvent(List<ActionEffectEntry> actionEffects)
    {

        // Check if client player is null
        if (_frameworkUtils.ClientState.LocalPlayer is null)
            return;

        // Return if not in the gold saucer
        if (_frameworkUtils.ClientState.TerritoryType is not 144)
            return;

        // Check if the GagReflex achievement is already completed
        var gagReflexAchievement = SaveData.Achievements[AchievementModuleKind.Gags].Achievements[GagLabels.GagReflex] as ProgressAchievement;
        if (gagReflexAchievement is null || gagReflexAchievement.IsCompleted)
        {
            Logger.LogTrace("GagReflex achievement is already completed or is null");
            return;
        }

        // Check if the player is in a gate with knockback
        if (!AchievementHelpers.IsInGateWithKnockback())
        {
            Logger.LogDebug("Player is not in a gate with knockback");
            return;
        }

        // Check if any effects were a knockback effect targeting the local player
        if (actionEffects.Any(x => x.Type == LimitedActionEffectType.Knockback && x.TargetID == _frameworkUtils.ClientState.LocalPlayer.GameObjectId))
        {
            // Increment progress if the achievement is not yet completed
            gagReflexAchievement.IncrementProgress();
        }
    }
}
