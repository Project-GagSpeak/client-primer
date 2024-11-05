using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Text;
using GagSpeak.GagspeakConfiguration.Models;
using GagSpeak.UpdateMonitoring.Triggers;
using GagSpeak.Utils;
using GagSpeak.WebAPI;
using GagspeakAPI.Enums;
using GagspeakAPI.Extensions;
using Penumbra.GameData.Enums;

namespace GagSpeak.Achievements;
public partial class AchievementManager
{
    private void OnCommendationsGiven(int amount)
    {
        (SaveData.Components[AchievementModuleKind.Secrets].Achievements[Achievements.KinkyTeacher.Title] as ConditionalProgressAchievement)?.CheckTaskProgress(amount);
        (SaveData.Components[AchievementModuleKind.Secrets].Achievements[Achievements.KinkyProfessor.Title] as ConditionalProgressAchievement)?.CheckTaskProgress(amount);
        (SaveData.Components[AchievementModuleKind.Secrets].Achievements[Achievements.KinkyMentor.Title] as ConditionalProgressAchievement)?.CheckTaskProgress(amount);
    }

    private void OnPairVisible()
    {
        // We need to obtain the total visible user count, then update the respective achievements.
        var visiblePairs = _pairManager.GetVisibleUserCount();
        (SaveData.Components[AchievementModuleKind.Secrets].Achievements[Achievements.BondageClub.Title] as ThresholdAchievement)?.UpdateThreshold(visiblePairs);
        (SaveData.Components[AchievementModuleKind.Wardrobe].Achievements[Achievements.Humiliation.Title] as ConditionalThresholdAchievement)?.UpdateThreshold(visiblePairs);
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
            (SaveData.Components[AchievementModuleKind.Secrets].Achievements[Achievements.TooltipLogos.Title] as ProgressAchievement)?.IncrementProgress();
        }
    }

    private DateTime worldTourStartedTime = DateTime.MinValue;
    private void CheckOnZoneSwitchStart(ushort prevZone)
    {
        // we left the zone we were at, so see if our woldTourStartedTime is not minvalue, if it isnt we need to check our conditonalProgressAchievement.
        if(worldTourStartedTime != DateTime.MinValue)
        {
            // Ensure it has been longer than 2 minutes since the recorded time. (in UTC)
            if ((DateTime.UtcNow - worldTourStartedTime).TotalMinutes > 2)
            {
                // Check to see if we qualify for starting any world tour conditions.
                if (SaveData.VisitedWorldTour.ContainsKey(prevZone) && SaveData.VisitedWorldTour[prevZone] is false)
                {
                    // Mark the conditonal as finished in the achievement, and mark as completed.
                    if (_clientConfigs.GetActiveSetIdx() != -1)
                    {
                        (SaveData.Components[AchievementModuleKind.Secrets].Achievements[Achievements.WorldTour.Title] as ConditionalProgressAchievement)?.FinishConditionalTask();
                        SaveData.VisitedWorldTour[prevZone] = true;
                    }
                    else
                        (SaveData.Components[AchievementModuleKind.Secrets].Achievements[Achievements.WorldTour.Title] as ConditionalProgressAchievement)?.StartOverDueToInturrupt();
                    // reset the datetime to .MinValue
                    worldTourStartedTime = DateTime.MinValue;
                }
            }
        }
    }

    private void CheckOnZoneSwitchEnd()
    {
        if(_frameworkUtils.IsInMainCity)
            (SaveData.Components[AchievementModuleKind.Hardcore].Achievements[Achievements.WalkOfShame.Title] as TimeRequiredConditionalAchievement)?.CheckCompletion();

        ushort territory = _frameworkUtils.ClientState.TerritoryType;

        // if present in diadem (for diamdem achievement)
        if (territory is 939)
            (SaveData.Components[AchievementModuleKind.Toybox].Achievements[Achievements.MotivationForRestoration.Title] as TimeRequiredConditionalAchievement)?.CheckCompletion();
        else
            (SaveData.Components[AchievementModuleKind.Toybox].Achievements[Achievements.MotivationForRestoration.Title] as TimeRequiredConditionalAchievement)?.CheckCompletion();

        // If we left before completing the duty, check that here.
        if ((SaveData.Components[AchievementModuleKind.Hardcore].Achievements[Achievements.UCanTieThis.Title] as ConditionalProgressAchievement)?.ConditionalTaskBegun ?? false)
            (SaveData.Components[AchievementModuleKind.Hardcore].Achievements[Achievements.UCanTieThis.Title] as ConditionalProgressAchievement)?.StartOverDueToInturrupt();

        if ((SaveData.Components[AchievementModuleKind.Gags].Achievements[Achievements.SilentButDeadly.Title] as ConditionalProgressAchievement)?.ConditionalTaskBegun ?? false)
            (SaveData.Components[AchievementModuleKind.Gags].Achievements[Achievements.SilentButDeadly.Title] as ConditionalProgressAchievement)?.CheckTaskProgress();

        if ((SaveData.Components[AchievementModuleKind.Wardrobe].Achievements[Achievements.HealSlut.Title] as ConditionalProgressAchievement)?.ConditionalTaskBegun ?? false)
            (SaveData.Components[AchievementModuleKind.Wardrobe].Achievements[Achievements.HealSlut.Title] as ConditionalProgressAchievement)?.StartOverDueToInturrupt();

        // Check to see if we qualify for starting any world tour conditions.
        if (SaveData.VisitedWorldTour.ContainsKey(territory) && SaveData.VisitedWorldTour[territory] is false)
        {
            // if its already true, dont worry about it.
            if (SaveData.VisitedWorldTour[territory] is true)
            {
                Logger.LogTrace("World Tour Progress already completed for: " + territory, LoggerType.Achievements);
                return;
            }
            else // Begin the progress for this city's world tour. 
            {
                Logger.LogTrace("Starting World Tour Progress for: " + territory, LoggerType.Achievements);
                (SaveData.Components[AchievementModuleKind.Secrets].Achievements[Achievements.WorldTour.Title] as ConditionalProgressAchievement)?.BeginConditionalTask();
                worldTourStartedTime = DateTime.UtcNow;
            }
        }
        else
        {
            Logger.LogTrace("World Tour Progress already completed for: " + territory, LoggerType.Achievements);
        }
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
            (SaveData.Components[AchievementModuleKind.Wardrobe].Achievements[Achievements.MyKinksRunDeeper.Title] as ConditionalProgressAchievement)?.BeginConditionalTask();

        switch (deepDungeonType)
        {
            case DeepDungeonType.PalaceOfTheDead:
                if ((floor > 40 && floor <= 50) || (floor > 90 && floor <= 100))
                {
                    (SaveData.Components[AchievementModuleKind.Wardrobe].Achievements[Achievements.BondagePalace.Title] as ConditionalProgressAchievement)?.BeginConditionalTask();
                    (SaveData.Components[AchievementModuleKind.Wardrobe].Achievements[Achievements.MyKinkRunsDeep.Title] as ConditionalProgressAchievement)?.BeginConditionalTask();
                    if (floor is 50 || floor is 100)
                    {
                        (SaveData.Components[AchievementModuleKind.Wardrobe].Achievements[Achievements.BondagePalace.Title] as ConditionalProgressAchievement)?.FinishConditionalTask();
                        (SaveData.Components[AchievementModuleKind.Wardrobe].Achievements[Achievements.MyKinkRunsDeep.Title] as ConditionalProgressAchievement)?.FinishConditionalTask();
                        (SaveData.Components[AchievementModuleKind.Wardrobe].Achievements[Achievements.MyKinksRunDeeper.Title] as ConditionalProgressAchievement)?.FinishConditionalTask();
                    }
                }
                break;
            case DeepDungeonType.HeavenOnHigh:
                if (floor > 20 && floor <= 30)
                {
                    (SaveData.Components[AchievementModuleKind.Wardrobe].Achievements[Achievements.HornyOnHigh.Title] as ConditionalProgressAchievement)?.BeginConditionalTask();
                    (SaveData.Components[AchievementModuleKind.Wardrobe].Achievements[Achievements.MyKinkRunsDeep.Title] as ConditionalProgressAchievement)?.BeginConditionalTask();
                    if (floor is 30)
                    {
                        (SaveData.Components[AchievementModuleKind.Wardrobe].Achievements[Achievements.MyKinkRunsDeep.Title] as ConditionalProgressAchievement)?.FinishConditionalTask();
                        (SaveData.Components[AchievementModuleKind.Wardrobe].Achievements[Achievements.MyKinksRunDeeper.Title] as ConditionalProgressAchievement)?.FinishConditionalTask();
                    }
                }
                break;
            case DeepDungeonType.EurekaOrthos:
                if (floor > 20 && floor <= 30)
                {
                    (SaveData.Components[AchievementModuleKind.Wardrobe].Achievements[Achievements.EurekaWhorethos.Title] as ConditionalProgressAchievement)?.BeginConditionalTask();
                    (SaveData.Components[AchievementModuleKind.Wardrobe].Achievements[Achievements.MyKinkRunsDeep.Title] as ConditionalProgressAchievement)?.BeginConditionalTask();
                    if (floor is 30)
                    {
                        (SaveData.Components[AchievementModuleKind.Wardrobe].Achievements[Achievements.EurekaWhorethos.Title] as ConditionalProgressAchievement)?.FinishConditionalTask();
                        (SaveData.Components[AchievementModuleKind.Wardrobe].Achievements[Achievements.MyKinkRunsDeep.Title] as ConditionalProgressAchievement)?.FinishConditionalTask();
                        (SaveData.Components[AchievementModuleKind.Wardrobe].Achievements[Achievements.MyKinksRunDeeper.Title] as ConditionalProgressAchievement)?.FinishConditionalTask();
                    }
                }
                break;
        }
    }

    private void OnDutyStart(object? sender, ushort e)
    {
        Logger.LogInformation("Duty Started", LoggerType.Achievements);
        if (_frameworkUtils.InPvP)// || _frameworkUtils.PartyListSize < 4)
            return;

        (SaveData.Components[AchievementModuleKind.Wardrobe].Achievements[Achievements.KinkyExplorer.Title] as ConditionalAchievement)?.CheckCompletion();

        (SaveData.Components[AchievementModuleKind.Gags].Achievements[Achievements.SilentButDeadly.Title] as ConditionalProgressAchievement)?.BeginConditionalTask();
        (SaveData.Components[AchievementModuleKind.Hardcore].Achievements[Achievements.UCanTieThis.Title] as ConditionalProgressAchievement)?.BeginConditionalTask(10); // 10s delay.

        if (_frameworkUtils.PlayerJobRole is ActionRoles.Healer)
            (SaveData.Components[AchievementModuleKind.Wardrobe].Achievements[Achievements.HealSlut.Title] as ConditionalProgressAchievement)?.BeginConditionalTask();

        // If the party size is 8, let's check for the trials.
        if(_frameworkUtils.PartyListSize is 8 && _frameworkUtils.PlayerLevel >= 90)
        {
            (SaveData.Components[AchievementModuleKind.Wardrobe].Achievements[Achievements.TrialOfFocus.Title] as ConditionalProgressAchievement)?.BeginConditionalTask();
            (SaveData.Components[AchievementModuleKind.Wardrobe].Achievements[Achievements.TrialOfDexterity.Title] as ConditionalProgressAchievement)?.BeginConditionalTask();
            (SaveData.Components[AchievementModuleKind.Wardrobe].Achievements[Achievements.TrialOfTheBlind.Title] as ConditionalProgressAchievement)?.BeginConditionalTask();
        }
    }

    private void OnDutyEnd(object? sender, ushort e)
    {
        if (_frameworkUtils.InPvP)// || _frameworkUtils.PartyListSize < 4)
            return;
        Logger.LogInformation("Duty Ended", LoggerType.Achievements);
        if ((SaveData.Components[AchievementModuleKind.Hardcore].Achievements[Achievements.UCanTieThis.Title] as ConditionalProgressAchievement)?.ConditionalTaskBegun ?? false)
            (SaveData.Components[AchievementModuleKind.Hardcore].Achievements[Achievements.UCanTieThis.Title] as ConditionalProgressAchievement)?.FinishConditionalTask();

        if ((SaveData.Components[AchievementModuleKind.Gags].Achievements[Achievements.SilentButDeadly.Title] as ConditionalProgressAchievement)?.ConditionalTaskBegun ?? false)
            (SaveData.Components[AchievementModuleKind.Gags].Achievements[Achievements.SilentButDeadly.Title] as ConditionalProgressAchievement)?.FinishConditionalTask();

        if ((SaveData.Components[AchievementModuleKind.Wardrobe].Achievements[Achievements.HealSlut.Title] as ConditionalProgressAchievement)?.ConditionalTaskBegun ?? false)
            (SaveData.Components[AchievementModuleKind.Wardrobe].Achievements[Achievements.HealSlut.Title] as ConditionalProgressAchievement)?.FinishConditionalTask();

        // Trial has ended, check for completion.
        if ((SaveData.Components[AchievementModuleKind.Wardrobe].Achievements[Achievements.TrialOfFocus.Title] as ConditionalProgressAchievement)?.ConditionalTaskBegun ?? false)
        {
            if (_frameworkUtils.PartyListSize is 8 && _frameworkUtils.PlayerLevel >= 90)
                (SaveData.Components[AchievementModuleKind.Wardrobe].Achievements[Achievements.TrialOfFocus.Title] as ConditionalProgressAchievement)?.FinishConditionalTask();
            else // if we are not in a full party, we should reset the task.
                (SaveData.Components[AchievementModuleKind.Wardrobe].Achievements[Achievements.TrialOfFocus.Title] as ConditionalProgressAchievement)?.StartOverDueToInturrupt();
        }
        
        if ((SaveData.Components[AchievementModuleKind.Wardrobe].Achievements[Achievements.TrialOfDexterity.Title] as ConditionalProgressAchievement)?.ConditionalTaskBegun ?? false)
        {
            if(_frameworkUtils.PartyListSize is 8 && _frameworkUtils.PlayerLevel >= 90)
                (SaveData.Components[AchievementModuleKind.Wardrobe].Achievements[Achievements.TrialOfDexterity.Title] as ConditionalProgressAchievement)?.FinishConditionalTask();
            else // if we are not in a full party, we should reset the task.
                (SaveData.Components[AchievementModuleKind.Wardrobe].Achievements[Achievements.TrialOfDexterity.Title] as ConditionalProgressAchievement)?.StartOverDueToInturrupt();
        }
        
        if ((SaveData.Components[AchievementModuleKind.Wardrobe].Achievements[Achievements.TrialOfTheBlind.Title] as ConditionalProgressAchievement)?.ConditionalTaskBegun ?? false)
        {
            if (_frameworkUtils.PartyListSize is 8 && _frameworkUtils.PlayerLevel >= 90)
                (SaveData.Components[AchievementModuleKind.Wardrobe].Achievements[Achievements.TrialOfTheBlind.Title] as ConditionalProgressAchievement)?.FinishConditionalTask();
            else // if we are not in a full party, we should reset the task.
                (SaveData.Components[AchievementModuleKind.Wardrobe].Achievements[Achievements.TrialOfTheBlind.Title] as ConditionalProgressAchievement)?.StartOverDueToInturrupt();
        }
    }

    private void OnOrderAction(OrderInteractionKind orderKind)
    {
        switch (orderKind)
        {
            case OrderInteractionKind.Completed:
                (SaveData.Components[AchievementModuleKind.Orders].Achievements[Achievements.JustAVolunteer.Title] as ProgressAchievement)?.IncrementProgress();
                (SaveData.Components[AchievementModuleKind.Orders].Achievements[Achievements.AsYouCommand.Title] as ProgressAchievement)?.IncrementProgress();
                (SaveData.Components[AchievementModuleKind.Orders].Achievements[Achievements.AnythingForMyOwner.Title] as ProgressAchievement)?.IncrementProgress();
                (SaveData.Components[AchievementModuleKind.Orders].Achievements[Achievements.GoodDrone.Title] as ProgressAchievement)?.IncrementProgress();
                break;
            case OrderInteractionKind.Fail:
                (SaveData.Components[AchievementModuleKind.Orders].Achievements[Achievements.BadSlut.Title] as ProgressAchievement)?.IncrementProgress();
                (SaveData.Components[AchievementModuleKind.Orders].Achievements[Achievements.NeedsTraining.Title] as ProgressAchievement)?.IncrementProgress();
                (SaveData.Components[AchievementModuleKind.Orders].Achievements[Achievements.UsefulInOtherWays.Title] as ProgressAchievement)?.IncrementProgress();
                break;
            case OrderInteractionKind.Create:
                (SaveData.Components[AchievementModuleKind.Orders].Achievements[Achievements.NewSlaveOwner.Title] as ProgressAchievement)?.IncrementProgress();
                (SaveData.Components[AchievementModuleKind.Orders].Achievements[Achievements.TaskManager.Title] as ProgressAchievement)?.IncrementProgress();
                (SaveData.Components[AchievementModuleKind.Orders].Achievements[Achievements.MaidMaster.Title] as ProgressAchievement)?.IncrementProgress();
                (SaveData.Components[AchievementModuleKind.Orders].Achievements[Achievements.QueenOfDrones.Title] as ProgressAchievement)?.IncrementProgress();
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
                (SaveData.Components[AchievementModuleKind.Gags].Achievements[Achievements.SelfApplied.Title] as ProgressAchievement)?.IncrementProgress();
            }
            // the gag was applied to us by someone else.
            else
            {
                (SaveData.Components[AchievementModuleKind.Gags].Achievements[Achievements.LookingForTheRightFit.Title] as ProgressAchievement)?.IncrementProgress();
                (SaveData.Components[AchievementModuleKind.Gags].Achievements[Achievements.OralFixation.Title] as ProgressAchievement)?.IncrementProgress();
                (SaveData.Components[AchievementModuleKind.Gags].Achievements[Achievements.AKinkForDrool.Title] as ProgressAchievement)?.IncrementProgress();

                (SaveData.Components[AchievementModuleKind.Gags].Achievements[Achievements.ATrueGagSlut.Title] as TimedProgressAchievement)?.IncrementProgress();
            }

            (SaveData.Components[AchievementModuleKind.Gags].Achievements[Achievements.SpeechSilverSilenceGolden.Title] as DurationAchievement)?.StartTracking(gagType.GagName(), MainHub.UID);
            (SaveData.Components[AchievementModuleKind.Gags].Achievements[Achievements.TheKinkyLegend.Title] as DurationAchievement)?.StartTracking(gagType.GagName(), MainHub.UID);
            (SaveData.Components[AchievementModuleKind.Secrets].Achievements[Achievements.Experimentalist.Title] as ConditionalAchievement)?.CheckCompletion();
            (SaveData.Components[AchievementModuleKind.Secrets].Achievements[Achievements.GaggedPleasure.Title] as ConditionalAchievement)?.CheckCompletion();
        }
        // Check regardless of it being an initial server connection or not.
        (SaveData.Components[AchievementModuleKind.Gags].Achievements[Achievements.ShushtainableResource.Title] as ThresholdAchievement)?.UpdateThreshold(_playerData.TotalGagsEquipped);
        (SaveData.Components[AchievementModuleKind.Gags].Achievements[Achievements.ShushtainableResource.Title] as ConditionalAchievement)?.CheckCompletion();
    }

    private void OnGagRemoval(GagLayer layer, GagType gagType, bool isSelfApplied)
    {
        (SaveData.Components[AchievementModuleKind.Gags].Achievements[Achievements.ShushtainableResource.Title] as ThresholdAchievement)?.UpdateThreshold(_playerData.TotalGagsEquipped);

        (SaveData.Components[AchievementModuleKind.Gags].Achievements[Achievements.SpeechSilverSilenceGolden.Title] as DurationAchievement)?.StopTracking(gagType.GagName(), MainHub.UID);
        (SaveData.Components[AchievementModuleKind.Gags].Achievements[Achievements.TheKinkyLegend.Title] as DurationAchievement)?.StopTracking(gagType.GagName(), MainHub.UID);

        // Halt our Silent But Deadly Progress if gag is removed mid-dungeon
        if ((SaveData.Components[AchievementModuleKind.Gags].Achievements[Achievements.SilentButDeadly.Title] as ConditionalProgressAchievement)?.ConditionalTaskBegun ?? false)
            (SaveData.Components[AchievementModuleKind.Gags].Achievements[Achievements.SilentButDeadly.Title] as ConditionalProgressAchievement)?.CheckTaskProgress();

    }

    private void OnPairGagApplied(GagType gag)
    {
        if (gag is not GagType.None)
        {
            (SaveData.Components[AchievementModuleKind.Gags].Achievements[Achievements.ApplyToPair.Title] as ProgressAchievement)?.IncrementProgress();
            (SaveData.Components[AchievementModuleKind.Gags].Achievements[Achievements.LookingForTheRightFit.Title] as ProgressAchievement)?.IncrementProgress();
            (SaveData.Components[AchievementModuleKind.Gags].Achievements[Achievements.OralFixation.Title] as ProgressAchievement)?.IncrementProgress();
            (SaveData.Components[AchievementModuleKind.Gags].Achievements[Achievements.AKinkForDrool.Title] as ProgressAchievement)?.IncrementProgress();

            (SaveData.Components[AchievementModuleKind.Gags].Achievements[Achievements.ApplyToPair.Title] as ProgressAchievement)?.IncrementProgress();

            (SaveData.Components[AchievementModuleKind.Gags].Achievements[Achievements.YourFavoriteNurse.Title] as ConditionalProgressAchievement)?.CheckTaskProgress();
        }
    }

    private void OnRestraintSetUpdated(RestraintSet set)
    {
        // check for dyes
        if (set.DrawData.Any(x => x.Value.GameStain.Stain1 != 0 || x.Value.GameStain.Stain2 != 0))
        {
            (SaveData.Components[AchievementModuleKind.Wardrobe].Achievements[Achievements.ToDyeFor.Title] as ProgressAchievement)?.IncrementProgress();
            (SaveData.Components[AchievementModuleKind.Wardrobe].Achievements[Achievements.DyeAnotherDay.Title] as ProgressAchievement)?.IncrementProgress();
            (SaveData.Components[AchievementModuleKind.Wardrobe].Achievements[Achievements.DyeHard.Title] as ProgressAchievement)?.IncrementProgress();
        }
    }

    private void OnRestraintApplied(RestraintSet set, bool isEnabling, string enactorUID)
    {
        // Check this regardless.
        (SaveData.Components[AchievementModuleKind.Hardcore].Achievements[Achievements.WalkOfShame.Title] as TimeRequiredConditionalAchievement)?.CheckCompletion();

        // Set is being enabled.
        if (isEnabling)
        {
            var territory = _frameworkUtils.ClientState.TerritoryType;
            // Check to see if we qualify for starting any world tour conditions.
            if (SaveData.VisitedWorldTour.ContainsKey(territory) && SaveData.VisitedWorldTour[territory] is false)
            {
                // if its already true, dont worry about it.
                if (SaveData.VisitedWorldTour[territory] is true)
                {
                    Logger.LogTrace("World Tour Progress already completed for: " + territory, LoggerType.Achievements);
                    return;
                }
                else // Begin the progress for this city's world tour. 
                {
                    Logger.LogTrace("Starting World Tour Progress for: " + territory, LoggerType.Achievements);
                    (SaveData.Components[AchievementModuleKind.Secrets].Achievements[Achievements.WorldTour.Title] as ConditionalProgressAchievement)?.BeginConditionalTask();
                    worldTourStartedTime = DateTime.UtcNow;
                }
            }

            (SaveData.Components[AchievementModuleKind.Secrets].Achievements[Achievements.Experimentalist.Title] as ConditionalAchievement)?.CheckCompletion();
            (SaveData.Components[AchievementModuleKind.Wardrobe].Achievements[Achievements.FirstTiemers.Title] as ProgressAchievement)?.IncrementProgress();

            // if we are the applier
            if (enactorUID == MainHub.UID)
            {
                (SaveData.Components[AchievementModuleKind.Wardrobe].Achievements[Achievements.SelfBondageEnthusiast.Title] as ProgressAchievement)?.IncrementProgress();
            }
            else // someone else is enabling our set
            {
                (SaveData.Components[AchievementModuleKind.Wardrobe].Achievements[Achievements.AuctionedOff.Title] as ConditionalProgressAchievement)?.BeginConditionalTask();
                // starts the timer.
                (SaveData.Components[AchievementModuleKind.Wardrobe].Achievements[Achievements.Bondodge.Title] as TimeLimitConditionalAchievement)?.CheckCompletion();

                // track overkill
                (SaveData.Components[AchievementModuleKind.Secrets].Achievements[Achievements.Overkill.Title] as ThresholdAchievement)?.UpdateThreshold(set.EquippedSlotsTotal);

                // Track Bondage Bunny
                (SaveData.Components[AchievementModuleKind.Wardrobe].Achievements[Achievements.BondageBunny.Title] as TimedProgressAchievement)?.IncrementProgress();

                // see if valid for "cuffed-19"
                if (set.DrawData.TryGetValue(EquipSlot.Hands, out var handData) && handData.GameItem.Id != ItemIdVars.NothingItem(EquipSlot.Hands).Id)
                {
                    (SaveData.Components[AchievementModuleKind.Wardrobe].Achievements[Achievements.Cuffed19.Title] as ProgressAchievement)?.IncrementProgress();
                }
            }
        }
        else // set is being disabled
        {
            if (enactorUID != MainHub.UID)
            {
                // verify that the set is being disabled by someone else.
                if (set.LockedBy != enactorUID)
                {
                    // the assigner and remover were different, so you are being auctioned off.
                    (SaveData.Components[AchievementModuleKind.Wardrobe].Achievements[Achievements.AuctionedOff.Title] as ConditionalProgressAchievement)?.FinishConditionalTask();
                }

                // must be removed within limit or wont award.
                (SaveData.Components[AchievementModuleKind.Wardrobe].Achievements[Achievements.Bondodge.Title] as TimeLimitConditionalAchievement)?.CheckCompletion();
            }
            // If a set is being disabled at all, we should reset our conditionals.
            (SaveData.Components[AchievementModuleKind.Wardrobe].Achievements[Achievements.TrialOfFocus.Title] as ConditionalProgressAchievement)?.StartOverDueToInturrupt();
            (SaveData.Components[AchievementModuleKind.Wardrobe].Achievements[Achievements.TrialOfDexterity.Title] as ConditionalProgressAchievement)?.StartOverDueToInturrupt();
            (SaveData.Components[AchievementModuleKind.Wardrobe].Achievements[Achievements.TrialOfTheBlind.Title] as ConditionalProgressAchievement)?.StartOverDueToInturrupt();

            (SaveData.Components[AchievementModuleKind.Secrets].Achievements[Achievements.Overkill.Title] as ThresholdAchievement)?.UpdateThreshold(0);

            // Validate the world tour achievement.
            var territory = _frameworkUtils.ClientState.TerritoryType;
            // Ensure it has been longer than 2 minutes since the recorded time. (in UTC)
            if (SaveData.VisitedWorldTour.ContainsKey(territory) && SaveData.VisitedWorldTour[territory] is false)
            {
                // Fail the conditional task.
                (SaveData.Components[AchievementModuleKind.Secrets].Achievements[Achievements.WorldTour.Title] as ConditionalProgressAchievement)?.StartOverDueToInturrupt();
                worldTourStartedTime = DateTime.MinValue;
            }
        }
    }

    private void OnRestraintLock(RestraintSet set, Padlocks padlock, bool isLocking, string enactorUID)
    {
        Logger.LogTrace(enactorUID + " is " + (isLocking ? "locking" : "unlocking") + " a set: " + set.Name + " that had the padlock: " + padlock.ToName());
        // we locked our set.
        if (isLocking)
        {
            if (padlock is not Padlocks.None or Padlocks.FiveMinutesPadlock)
            {
                // make sure that someone is locking us up in a set.
                if (enactorUID != MainHub.UID)
                {
                    (SaveData.Components[AchievementModuleKind.Wardrobe].Achievements[Achievements.FirstTimeBondage.Title] as DurationAchievement)?.StartTracking(set.RestraintId.ToString(), MainHub.UID);
                    (SaveData.Components[AchievementModuleKind.Wardrobe].Achievements[Achievements.AmateurBondage.Title] as DurationAchievement)?.StartTracking(set.RestraintId.ToString(), MainHub.UID);
                    (SaveData.Components[AchievementModuleKind.Wardrobe].Achievements[Achievements.ComfortRestraint.Title] as DurationAchievement)?.StartTracking(set.RestraintId.ToString(), MainHub.UID);
                    (SaveData.Components[AchievementModuleKind.Wardrobe].Achievements[Achievements.DayInTheLifeOfABondageSlave.Title] as DurationAchievement)?.StartTracking(set.RestraintId.ToString(), MainHub.UID);
                    (SaveData.Components[AchievementModuleKind.Wardrobe].Achievements[Achievements.AWeekInBondage.Title] as DurationAchievement)?.StartTracking(set.RestraintId.ToString(), MainHub.UID);
                    (SaveData.Components[AchievementModuleKind.Wardrobe].Achievements[Achievements.AMonthInBondage.Title] as DurationAchievement)?.StartTracking(set.RestraintId.ToString(), MainHub.UID);
                }
            }
        }
        else
        { 
            // if the set is being unlocked, stop progress regardless.
            if (padlock is not Padlocks.None or Padlocks.FiveMinutesPadlock)
            {
                (SaveData.Components[AchievementModuleKind.Wardrobe].Achievements[Achievements.FirstTimeBondage.Title] as DurationAchievement)?.StopTracking(set.RestraintId.ToString(), MainHub.UID);
                (SaveData.Components[AchievementModuleKind.Wardrobe].Achievements[Achievements.AmateurBondage.Title] as DurationAchievement)?.StopTracking(set.RestraintId.ToString(), MainHub.UID);
                (SaveData.Components[AchievementModuleKind.Wardrobe].Achievements[Achievements.ComfortRestraint.Title] as DurationAchievement)?.StopTracking(set.RestraintId.ToString(), MainHub.UID);
                (SaveData.Components[AchievementModuleKind.Wardrobe].Achievements[Achievements.DayInTheLifeOfABondageSlave.Title] as DurationAchievement)?.StopTracking(set.RestraintId.ToString(), MainHub.UID);
                (SaveData.Components[AchievementModuleKind.Wardrobe].Achievements[Achievements.AWeekInBondage.Title] as DurationAchievement)?.StopTracking(set.RestraintId.ToString(), MainHub.UID);
                (SaveData.Components[AchievementModuleKind.Wardrobe].Achievements[Achievements.AMonthInBondage.Title] as DurationAchievement)?.StopTracking(set.RestraintId.ToString(), MainHub.UID);
            }
        }
    }

    /// <summary>
    /// Whenever we are applying a restraint set to a pair. This is fired in our pair manager once we recieve 
    /// </summary>
    private void OnPairRestraintApply(Guid setName, bool isEnabling, string enactorUID)
    {
        Logger.LogTrace(enactorUID + " is "+ (isEnabling ? "applying" : "Removing") + " a set to a pair: " + setName);
        // if we enabled a set on someone else
        if (isEnabling && enactorUID == MainHub.UID)
        {
            (SaveData.Components[AchievementModuleKind.Wardrobe].Achievements[Achievements.FirstTiemers.Title] as ProgressAchievement)?.IncrementProgress();
            (SaveData.Components[AchievementModuleKind.Wardrobe].Achievements[Achievements.DiDEnthusiast.Title] as ProgressAchievement)?.IncrementProgress();
            (SaveData.Components[AchievementModuleKind.Gags].Achievements[Achievements.YourFavoriteNurse.Title] as ConditionalProgressAchievement)?.CheckTaskProgress();
        }
    }

    private void OnPairRestraintLockChange(Guid restraintId, Padlocks padlock, bool isLocking, string enactorUID, string affectedPairUID) // uid is self applied if client.
    {
        // May need to figure this for pairs upon connection to validate any actions/unlocks that occured while we were away.
        Logger.LogInformation("Pair Restraint Lock Change: " + padlock.ToName() + " " + isLocking + " " + enactorUID);


        // Change the achievement type of the achievement below, its currently busted.
        (SaveData.Components[AchievementModuleKind.Wardrobe].Achievements[Achievements.SoldSlave.Title] as ProgressAchievement)?.IncrementProgress();

        // if the pair's set is being locked and it is a timed lock.
        if (isLocking)
        {
            if (padlock is not Padlocks.None or Padlocks.FiveMinutesPadlock) // locking
            {
                // make sure we are the locker before continuing
                if(enactorUID == MainHub.UID)
                {
                    (SaveData.Components[AchievementModuleKind.Wardrobe].Achievements[Achievements.RiggersFirstSession.Title] as DurationAchievement)?.StartTracking(restraintId.ToString(), affectedPairUID);
                    (SaveData.Components[AchievementModuleKind.Wardrobe].Achievements[Achievements.MyLittlePlaything.Title] as DurationAchievement)?.StartTracking(restraintId.ToString(), affectedPairUID);
                    (SaveData.Components[AchievementModuleKind.Wardrobe].Achievements[Achievements.SuitsYouBitch.Title] as DurationAchievement)?.StartTracking(restraintId.ToString(), affectedPairUID);
                    (SaveData.Components[AchievementModuleKind.Wardrobe].Achievements[Achievements.TiesThatBind.Title] as DurationAchievement)?.StartTracking(restraintId.ToString(), affectedPairUID);
                    (SaveData.Components[AchievementModuleKind.Wardrobe].Achievements[Achievements.SlaveTraining.Title] as DurationAchievement)?.StartTracking(restraintId.ToString(), affectedPairUID);
                    (SaveData.Components[AchievementModuleKind.Wardrobe].Achievements[Achievements.CeremonyOfEternalBondage.Title] as DurationAchievement)?.StartTracking(restraintId.ToString(), affectedPairUID);
                }
            }
        }
        if(!isLocking)
        {
            // if the padlock is a timed padlock that we have unlocked, we should stop tracking it from these achievements.
            if (padlock is not Padlocks.None or Padlocks.FiveMinutesPadlock)
            {
                (SaveData.Components[AchievementModuleKind.Wardrobe].Achievements[Achievements.RiggersFirstSession.Title] as DurationAchievement)?.StopTracking(restraintId.ToString(), affectedPairUID);
                (SaveData.Components[AchievementModuleKind.Wardrobe].Achievements[Achievements.MyLittlePlaything.Title] as DurationAchievement)?.StopTracking(restraintId.ToString(), affectedPairUID);
                (SaveData.Components[AchievementModuleKind.Wardrobe].Achievements[Achievements.SuitsYouBitch.Title] as DurationAchievement)?.StopTracking(restraintId.ToString(), affectedPairUID);
                (SaveData.Components[AchievementModuleKind.Wardrobe].Achievements[Achievements.TiesThatBind.Title] as DurationAchievement)?.StopTracking(restraintId.ToString(), affectedPairUID);
                (SaveData.Components[AchievementModuleKind.Wardrobe].Achievements[Achievements.SlaveTraining.Title] as DurationAchievement)?.StopTracking(restraintId.ToString(), affectedPairUID);
                (SaveData.Components[AchievementModuleKind.Wardrobe].Achievements[Achievements.CeremonyOfEternalBondage.Title] as DurationAchievement)?.StopTracking(restraintId.ToString(), affectedPairUID);
            }

            // if we are unlocking in general, increment the rescuer
            (SaveData.Components[AchievementModuleKind.Wardrobe].Achievements[Achievements.TheRescuer.Title] as ProgressAchievement)?.IncrementProgress();
        }
    }

    private void OnPuppetAccessGiven(bool wasAllPerms)
    {
        if (wasAllPerms) // All Perms access given to another pair.
            (SaveData.Components[AchievementModuleKind.Puppeteer].Achievements[Achievements.CompleteDevotion.Title] as ProgressAchievement)?.IncrementProgress();
        else // Emote perms given to another pair.
            (SaveData.Components[AchievementModuleKind.Puppeteer].Achievements[Achievements.ControlMyBody.Title] as ProgressAchievement)?.IncrementProgress();
    }

    private void OnPatternAction(PatternInteractionKind actionType, Guid patternGuid, bool wasAlarm)
    {
        switch (actionType)
        {
            case PatternInteractionKind.Published:
                (SaveData.Components[AchievementModuleKind.Toybox].Achievements[Achievements.FunForAll.Title] as ProgressAchievement)?.IncrementProgress();
                (SaveData.Components[AchievementModuleKind.Toybox].Achievements[Achievements.DeviousComposer.Title] as ProgressAchievement)?.IncrementProgress();
                break;
            case PatternInteractionKind.Downloaded:
                (SaveData.Components[AchievementModuleKind.Toybox].Achievements[Achievements.CravingPleasure.Title] as ProgressAchievement)?.IncrementProgress();
                break;
            case PatternInteractionKind.Liked:
                (SaveData.Components[AchievementModuleKind.Toybox].Achievements[Achievements.PatternLover.Title] as ProgressAchievement)?.IncrementProgress();
                break;
            case PatternInteractionKind.Started:
                if (patternGuid != Guid.Empty)
                {
                    (SaveData.Components[AchievementModuleKind.Toybox].Achievements[Achievements.EnduranceQueen.Title] as DurationAchievement)?.StartTracking(patternGuid.ToString(), MainHub.UID);
                }
                if (wasAlarm && patternGuid != Guid.Empty)
                    (SaveData.Components[AchievementModuleKind.Toybox].Achievements[Achievements.HornyMornings.Title] as ProgressAchievement)?.IncrementProgress();
                break;
            case PatternInteractionKind.Stopped:
                if (patternGuid != Guid.Empty)
                    (SaveData.Components[AchievementModuleKind.Toybox].Achievements[Achievements.EnduranceQueen.Title] as DurationAchievement)?.StopTracking(patternGuid.ToString(), MainHub.UID);
                break;
        }
    }

    private void OnDeviceConnected()
    {
        (SaveData.Components[AchievementModuleKind.Toybox].Achievements[Achievements.MyFavoriteToys.Title] as ConditionalAchievement)?.CheckCompletion();
    }

    private void OnTriggerFired()
    {
        (SaveData.Components[AchievementModuleKind.Toybox].Achievements[Achievements.SubtleReminders.Title] as ProgressAchievement)?.IncrementProgress();
        (SaveData.Components[AchievementModuleKind.Toybox].Achievements[Achievements.FingerOnTheTrigger.Title] as ProgressAchievement)?.IncrementProgress();
        (SaveData.Components[AchievementModuleKind.Toybox].Achievements[Achievements.TriggerHappy.Title] as ProgressAchievement)?.IncrementProgress();
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
        Logger.LogInformation("Hardcore Action: " + actionKind + " State: " + state + " Enactor: " + enactorUID + " Affected: " + affectedPairUID);
        switch (actionKind)
        {
            case HardcoreAction.ForcedFollow:
                // if we are the enactor and the pair is the target:
                if (enactorUID == MainHub.UID)
                {
                    Logger.LogInformation("We were the enactor for forced follow");
                    // if the state is enabled, begin tracking the pair we forced.
                    if (state is NewState.Enabled)
                    {
                        Logger.LogInformation("Forced Follow New State is Enabled");
                        (SaveData.Components[AchievementModuleKind.Hardcore].Achievements[Achievements.AllTheCollarsOfTheRainbow.Title] as ProgressAchievement)?.IncrementProgress();
                        (SaveData.Components[AchievementModuleKind.Hardcore].Achievements[Achievements.ForcedFollow.Title] as DurationAchievement)?.StartTracking(affectedPairUID, affectedPairUID);
                        (SaveData.Components[AchievementModuleKind.Hardcore].Achievements[Achievements.ForcedWalkies.Title] as DurationAchievement)?.StartTracking(affectedPairUID, affectedPairUID);
                    }
                }
                // if the affected pair is not our clients UID and the action is disabling, stop tracking for anything we started. (can ignore the enactor)
                if (affectedPairUID != MainHub.UID && state is NewState.Disabled)
                {
                    Logger.LogInformation("We were not the affected pair and the new state is disabled");
                    (SaveData.Components[AchievementModuleKind.Hardcore].Achievements[Achievements.ForcedFollow.Title] as DurationAchievement)?.StopTracking(affectedPairUID, affectedPairUID);
                    (SaveData.Components[AchievementModuleKind.Hardcore].Achievements[Achievements.ForcedWalkies.Title] as DurationAchievement)?.StopTracking(affectedPairUID, affectedPairUID);
                }

                // if the affected pair was us:
                if (affectedPairUID == MainHub.UID)
                {
                    Logger.LogInformation("We were the affected pair");
                    // Check in each state
                    (SaveData.Components[AchievementModuleKind.Hardcore].Achievements[Achievements.WalkOfShame.Title] as TimeRequiredConditionalAchievement)?.CheckCompletion();

                    // if the new state is enabled, we should begin tracking the time required completion.
                    if (state is NewState.Enabled)
                    {
                        Logger.LogInformation("Forced Follow New State is Enabled");
                        (SaveData.Components[AchievementModuleKind.Hardcore].Achievements[Achievements.TimeForWalkies.Title] as TimeRequiredConditionalAchievement)?.CheckCompletion();
                        (SaveData.Components[AchievementModuleKind.Hardcore].Achievements[Achievements.GettingStepsIn.Title] as TimeRequiredConditionalAchievement)?.CheckCompletion();
                        (SaveData.Components[AchievementModuleKind.Hardcore].Achievements[Achievements.WalkiesLover.Title] as TimeRequiredConditionalAchievement)?.CheckCompletion();
                    }
                    else // and if our state switches to disabled, we should halt the progression.
                    {
                        Logger.LogInformation("Forced Follow New State is Disabled");

                        if ((SaveData.Components[AchievementModuleKind.Hardcore].Achievements[Achievements.TimeForWalkies.Title] as TimeRequiredConditionalAchievement)?.StartPoint != DateTime.MinValue)
                            (SaveData.Components[AchievementModuleKind.Hardcore].Achievements[Achievements.TimeForWalkies.Title] as TimeRequiredConditionalAchievement)?.CheckCompletion();

                        if ((SaveData.Components[AchievementModuleKind.Hardcore].Achievements[Achievements.GettingStepsIn.Title] as TimeRequiredConditionalAchievement)?.StartPoint != DateTime.MinValue)
                            (SaveData.Components[AchievementModuleKind.Hardcore].Achievements[Achievements.GettingStepsIn.Title] as TimeRequiredConditionalAchievement)?.CheckCompletion();

                        if ((SaveData.Components[AchievementModuleKind.Hardcore].Achievements[Achievements.WalkiesLover.Title] as TimeRequiredConditionalAchievement)?.StartPoint != DateTime.MinValue)
                            (SaveData.Components[AchievementModuleKind.Hardcore].Achievements[Achievements.WalkiesLover.Title] as TimeRequiredConditionalAchievement)?.CheckCompletion();

                        (SaveData.Components[AchievementModuleKind.Hardcore].Achievements[Achievements.UCanTieThis.Title] as ConditionalProgressAchievement)?.StartOverDueToInturrupt();

                    }
                }
                break;
            case HardcoreAction.ForcedEmoteState:
                // if we are the affected UID:
                // TODO: This will probably break due to us not passing in the changed string on emoteID shift.
                if (affectedPairUID == MainHub.UID)
                {
                    if (state is NewState.Enabled)
                        (SaveData.Components[AchievementModuleKind.Hardcore].Achievements[Achievements.LivingFurniture.Title] as TimeRequiredConditionalAchievement)?.CheckCompletion();

                    if (state is NewState.Disabled)
                        if ((SaveData.Components[AchievementModuleKind.Hardcore].Achievements[Achievements.LivingFurniture.Title] as TimeRequiredConditionalAchievement)?.StartPoint != DateTime.MinValue)
                            (SaveData.Components[AchievementModuleKind.Hardcore].Achievements[Achievements.LivingFurniture.Title] as TimeRequiredConditionalAchievement)?.CheckCompletion();
                }
                break;
            case HardcoreAction.ForcedStay:
                // if we are the affected UID:
                if (affectedPairUID == MainHub.UID)
                {
                    // and we have been ordered to start being forced to stay:
                    if (state is NewState.Enabled)
                    {
                        (SaveData.Components[AchievementModuleKind.Hardcore].Achievements[Achievements.PetTraining.Title] as TimeRequiredConditionalAchievement)?.CheckCompletion();
                        (SaveData.Components[AchievementModuleKind.Hardcore].Achievements[Achievements.NotGoingAnywhere.Title] as TimeRequiredConditionalAchievement)?.CheckCompletion();
                        (SaveData.Components[AchievementModuleKind.Hardcore].Achievements[Achievements.HouseTrained.Title] as TimeRequiredConditionalAchievement)?.CheckCompletion();
                    }
                    else // our forced to stay has ended
                    {
                        if ((SaveData.Components[AchievementModuleKind.Hardcore].Achievements[Achievements.PetTraining.Title] as TimeRequiredConditionalAchievement)?.StartPoint != DateTime.MinValue)
                            (SaveData.Components[AchievementModuleKind.Hardcore].Achievements[Achievements.PetTraining.Title] as TimeRequiredConditionalAchievement)?.CheckCompletion();

                        if ((SaveData.Components[AchievementModuleKind.Hardcore].Achievements[Achievements.NotGoingAnywhere.Title] as TimeRequiredConditionalAchievement)?.StartPoint != DateTime.MinValue)
                            (SaveData.Components[AchievementModuleKind.Hardcore].Achievements[Achievements.NotGoingAnywhere.Title] as TimeRequiredConditionalAchievement)?.CheckCompletion();

                        if ((SaveData.Components[AchievementModuleKind.Hardcore].Achievements[Achievements.HouseTrained.Title] as TimeRequiredConditionalAchievement)?.StartPoint != DateTime.MinValue)
                            (SaveData.Components[AchievementModuleKind.Hardcore].Achievements[Achievements.HouseTrained.Title] as TimeRequiredConditionalAchievement)?.CheckCompletion();
                    }
                }
                break;
            case HardcoreAction.ForcedBlindfold:
                // if we are the affected UID:
                if (affectedPairUID == MainHub.UID)
                {
                    // Check in each state
                    (SaveData.Components[AchievementModuleKind.Hardcore].Achievements[Achievements.WalkOfShame.Title] as TimeRequiredConditionalAchievement)?.CheckCompletion();

                    // if we have had our blindfold set to enabled by another pair, perform the following:
                    if (state is NewState.Enabled)
                    {
                        (SaveData.Components[AchievementModuleKind.Hardcore].Achievements[Achievements.BlindLeadingTheBlind.Title] as ConditionalAchievement)?.CheckCompletion();
                        (SaveData.Components[AchievementModuleKind.Hardcore].Achievements[Achievements.WhoNeedsToSee.Title] as TimeRequiredConditionalAchievement)?.CheckCompletion();
                    }
                    // if another pair is removing our blindfold, perform the following:
                    if (state is NewState.Disabled)
                        if ((SaveData.Components[AchievementModuleKind.Hardcore].Achievements[Achievements.WhoNeedsToSee.Title] as TimeRequiredConditionalAchievement)?.StartPoint != DateTime.MinValue)
                            (SaveData.Components[AchievementModuleKind.Hardcore].Achievements[Achievements.WhoNeedsToSee.Title] as TimeRequiredConditionalAchievement)?.CheckCompletion();
                }
                break;
        }
    }

    private void OnShockSent()
    {
        (SaveData.Components[AchievementModuleKind.Hardcore].Achievements[Achievements.IndulgingSparks.Title] as ProgressAchievement)?.IncrementProgress();
        (SaveData.Components[AchievementModuleKind.Hardcore].Achievements[Achievements.CantGetEnough.Title] as ProgressAchievement)?.IncrementProgress();
        (SaveData.Components[AchievementModuleKind.Hardcore].Achievements[Achievements.VerThunder.Title] as ProgressAchievement)?.IncrementProgress();
        (SaveData.Components[AchievementModuleKind.Hardcore].Achievements[Achievements.WickedThunder.Title] as ProgressAchievement)?.IncrementProgress();
        (SaveData.Components[AchievementModuleKind.Hardcore].Achievements[Achievements.ElectropeHasNoLimits.Title] as ProgressAchievement)?.IncrementProgress();
    }

    private void OnShockReceived()
    {
        (SaveData.Components[AchievementModuleKind.Hardcore].Achievements[Achievements.ShockAndAwe.Title] as ProgressAchievement)?.IncrementProgress();
        (SaveData.Components[AchievementModuleKind.Hardcore].Achievements[Achievements.ShockingExperience.Title] as ProgressAchievement)?.IncrementProgress();
        (SaveData.Components[AchievementModuleKind.Hardcore].Achievements[Achievements.ShockolateTasting.Title] as ProgressAchievement)?.IncrementProgress();
        (SaveData.Components[AchievementModuleKind.Hardcore].Achievements[Achievements.ShockAddiction.Title] as ProgressAchievement)?.IncrementProgress();
        (SaveData.Components[AchievementModuleKind.Hardcore].Achievements[Achievements.WarriorOfElectrope.Title] as ProgressAchievement)?.IncrementProgress();
    }


    private void OnChatMessage(XivChatType channel)
    {
        (SaveData.Components[AchievementModuleKind.Secrets].Achievements[Achievements.HelplessDamsel.Title] as ConditionalAchievement)?.CheckCompletion();

        if (channel is XivChatType.Say)
        {
            (SaveData.Components[AchievementModuleKind.Gags].Achievements[Achievements.SpeakUpSlut.Title] as ProgressAchievement)?.IncrementProgress();
        }
        else if (channel is XivChatType.Yell)
        {
            (SaveData.Components[AchievementModuleKind.Gags].Achievements[Achievements.CantHearYou.Title] as ProgressAchievement)?.IncrementProgress();
        }
        else if (channel is XivChatType.Shout)
        {
            (SaveData.Components[AchievementModuleKind.Gags].Achievements[Achievements.OneMoreForTheCrowd.Title] as ProgressAchievement)?.IncrementProgress();
        }
    }

    private void OnEmoteExecuted(IGameObject emoteCallerObj, ushort emoteId, IGameObject targetObject)
    {
        switch (emoteId)
        {
            case 22:
                if(emoteCallerObj.ObjectIndex is 0)
                    (SaveData.Components[AchievementModuleKind.Hardcore].Achievements[Achievements.WhatAView.Title] as ConditionalAchievement)?.CheckCompletion();
                break;

            case 231:
                if (emoteCallerObj.ObjectIndex is 0)
                    (SaveData.Components[AchievementModuleKind.Gags].Achievements[Achievements.QuietNowDear.Title] as ConditionalAchievement)?.CheckCompletion();
                break;

            case 111:
                if (emoteCallerObj.ObjectIndex is not 0 && targetObject.ObjectIndex is 0) // Was originally If(emoteCallerObj.ObjectIndex is not 0)
                    (SaveData.Components[AchievementModuleKind.Generic].Achievements[Achievements.ICantBelieveYouveDoneThis.Title] as ConditionalAchievement)?.CheckCompletion();
                break;
        }
    }

    private void OnPuppeteerOrderSent(PuppeteerMsgType orderType)
    {
        switch(orderType)
        {
            case PuppeteerMsgType.GrovelOrder:
                (SaveData.Components[AchievementModuleKind.Puppeteer].Achievements[Achievements.KissMyHeels.Title] as ProgressAchievement)?.IncrementProgress();
                break;

            case PuppeteerMsgType.DanceOrder:
                (SaveData.Components[AchievementModuleKind.Puppeteer].Achievements[Achievements.ShowingOff.Title] as ProgressAchievement)?.IncrementProgress();
                break;
        }
        // Increase regardless.
        (SaveData.Components[AchievementModuleKind.Puppeteer].Achievements[Achievements.MasterOfPuppets.Title] as TimedProgressAchievement)?.IncrementProgress();
    }

    private void OnPuppeteerReceivedEmoteOrder(ushort emoteId)
    {
        switch(emoteId)
        {
            case 38:
                (SaveData.Components[AchievementModuleKind.Puppeteer].Achievements[Achievements.Ashamed.Title] as ProgressAchievement)?.IncrementProgress();
                break;

            case 50:
            case 52:
                (SaveData.Components[AchievementModuleKind.Puppeteer].Achievements[Achievements.WhoIsAGoodPet.Title] as ProgressAchievement)?.IncrementProgress();
                break;
        }
    }

    private void OnPairAdded()
    {
        (SaveData.Components[AchievementModuleKind.Generic].Achievements[Achievements.AddedFirstPair.Title] as ConditionalAchievement)?.CheckCompletion();
        (SaveData.Components[AchievementModuleKind.Generic].Achievements[Achievements.TheCollector.Title] as ProgressAchievement)?.IncrementProgress();
    }

    private void OnCursedLootFound()
    {
        (SaveData.Components[AchievementModuleKind.Wardrobe].Achievements[Achievements.TemptingFatesTreasure.Title] as ProgressAchievement)?.IncrementProgress();
        (SaveData.Components[AchievementModuleKind.Wardrobe].Achievements[Achievements.BadEndSeeker.Title] as ConditionalProgressAchievement)?.CheckTaskProgress();
        (SaveData.Components[AchievementModuleKind.Wardrobe].Achievements[Achievements.EverCursed.Title] as ConditionalProgressAchievement)?.CheckTaskProgress();
    }

    private void OnJobChange(GlamourUpdateType changeType)
    {
        if(changeType is GlamourUpdateType.JobChange)
            (SaveData.Components[AchievementModuleKind.Generic].Achievements[Achievements.EscapingIsNotEasy.Title] as ConditionalAchievement)?.CheckCompletion();
    }

    private void OnVibratorToggled(NewState newState)
    {
        if (newState is NewState.Enabled)
        {
            (SaveData.Components[AchievementModuleKind.Secrets].Achievements[Achievements.GaggedPleasure.Title] as ConditionalAchievement)?.CheckCompletion();
            (SaveData.Components[AchievementModuleKind.Secrets].Achievements[Achievements.Experimentalist.Title] as ConditionalAchievement)?.CheckCompletion();
        }
        else
        {

        }
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
        var gagReflexAchievement = SaveData.Components[AchievementModuleKind.Gags].Achievements[Achievements.GagReflex.Title] as ProgressAchievement;
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
