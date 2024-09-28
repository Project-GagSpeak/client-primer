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
using GagspeakAPI.Dto.Connection;
using GagspeakAPI.Extensions;
using Penumbra.GameData.Enums;

// if present in diadem (https://github.com/Infiziert90/DiademCalculator/blob/d74a22c58840a864cda12131fe2646dfc45209df/DiademCalculator/Windows/Main/MainWindow.cs#L12)

namespace GagSpeak.Achievements;
public partial class AchievementManager
{
    private void OnCommendationsGiven(int amount)
    {
        (SaveData.Achievements[AchievementType.Secrets].Achievements[SecretLabels.KinkyTeacher] as ConditionalProgressAchievement)?.CheckTaskProgress(amount);
        (SaveData.Achievements[AchievementType.Secrets].Achievements[SecretLabels.KinkyProfessor] as ConditionalProgressAchievement)?.CheckTaskProgress(amount);
        (SaveData.Achievements[AchievementType.Secrets].Achievements[SecretLabels.KinkyMentor] as ConditionalProgressAchievement)?.CheckTaskProgress(amount);
    }

    private void OnIconClicked(string windowLabel)
    {
        if (SaveData.EasterEggIcons.ContainsKey(windowLabel))
        {
            if (SaveData.EasterEggIcons[windowLabel])
                return;
            else
                SaveData.EasterEggIcons[windowLabel] = true;
            // update progress.
            (SaveData.Achievements[AchievementType.Secrets].Achievements[SecretLabels.TooltipLogos] as ProgressAchievement)?.IncrementProgress();
        }
    }

    private void CheckOnZoneSwitchEnd()
    {
        if (_frameworkUtils.IsInMainCity)
        {
            (SaveData.Achievements[AchievementType.Hardcore].Achievements[HardcoreLabels.WalkOfShame] as ConditionalAchievement)?.CheckCompletion();
        }

        // if present in diadem (for diamdem achievement)
        if (_frameworkUtils.ClientState.TerritoryType is 939)

            (SaveData.Achievements[AchievementType.Toybox].Achievements[ToyboxLabels.MotivationForRestoration] as ConditionalDurationAchievement)?.CheckCompletion();
        else
            (SaveData.Achievements[AchievementType.Toybox].Achievements[ToyboxLabels.MotivationForRestoration] as ConditionalDurationAchievement)?.ResetOrComplete();

        // if we are in a dungeon:
        if (_frameworkUtils.InDungeonOrDuty)
        {
            (SaveData.Achievements[AchievementType.Gags].Achievements[GagLabels.SilentButDeadly] as ConditionalProgressAchievement)?.BeginConditionalTask();
            (SaveData.Achievements[AchievementType.Hardcore].Achievements[HardcoreLabels.UCanTieThis] as ConditionalProgressAchievement)?.BeginConditionalTask();

            if (_frameworkUtils.PlayerJobRole is ActionRoles.Healer)
                (SaveData.Achievements[AchievementType.Wardrobe].Achievements[WardrobeLabels.HealSlut] as ConditionalProgressAchievement)?.BeginConditionalTask();
        }
        else
        {
            if ((SaveData.Achievements[AchievementType.Hardcore].Achievements[HardcoreLabels.UCanTieThis] as ConditionalProgressAchievement)?.ConditionalTaskBegun ?? false)
                (SaveData.Achievements[AchievementType.Hardcore].Achievements[HardcoreLabels.UCanTieThis] as ConditionalProgressAchievement)?.FinishConditionalTask();

            if ((SaveData.Achievements[AchievementType.Gags].Achievements[GagLabels.SilentButDeadly] as ConditionalProgressAchievement)?.ConditionalTaskBegun ?? false)
                (SaveData.Achievements[AchievementType.Gags].Achievements[GagLabels.SilentButDeadly] as ConditionalProgressAchievement)?.FinishConditionalTask();

            if ((SaveData.Achievements[AchievementType.Wardrobe].Achievements[WardrobeLabels.HealSlut] as ConditionalProgressAchievement)?.ConditionalTaskBegun ?? false)
                (SaveData.Achievements[AchievementType.Wardrobe].Achievements[WardrobeLabels.HealSlut] as ConditionalProgressAchievement)?.FinishConditionalTask();
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
            (SaveData.Achievements[AchievementType.Wardrobe].Achievements[WardrobeLabels.MyKinksRunDeeper] as ConditionalProgressAchievement)?.BeginConditionalTask();

        switch (deepDungeonType)
        {
            case DeepDungeonType.PalaceOfTheDead:
                if ((floor > 40 && floor <= 50) || (floor > 90 && floor <= 100))
                {
                    (SaveData.Achievements[AchievementType.Wardrobe].Achievements[WardrobeLabels.BondagePalace] as ConditionalProgressAchievement)?.BeginConditionalTask();
                    (SaveData.Achievements[AchievementType.Wardrobe].Achievements[WardrobeLabels.MyKinkRunsDeep] as ConditionalProgressAchievement)?.BeginConditionalTask();
                    if (floor is 50 || floor is 100)
                    {
                        (SaveData.Achievements[AchievementType.Wardrobe].Achievements[WardrobeLabels.BondagePalace] as ConditionalProgressAchievement)?.FinishConditionalTask();
                        (SaveData.Achievements[AchievementType.Wardrobe].Achievements[WardrobeLabels.MyKinkRunsDeep] as ConditionalProgressAchievement)?.FinishConditionalTask();
                        (SaveData.Achievements[AchievementType.Wardrobe].Achievements[WardrobeLabels.MyKinksRunDeeper] as ConditionalProgressAchievement)?.FinishConditionalTask();
                    }
                }
                break;
            case DeepDungeonType.HeavenOnHigh:
                if (floor > 20 && floor <= 30)
                {
                    (SaveData.Achievements[AchievementType.Wardrobe].Achievements[WardrobeLabels.HornyOnHigh] as ConditionalProgressAchievement)?.BeginConditionalTask();
                    (SaveData.Achievements[AchievementType.Wardrobe].Achievements[WardrobeLabels.MyKinkRunsDeep] as ConditionalProgressAchievement)?.BeginConditionalTask();
                    if (floor is 30)
                    {
                        (SaveData.Achievements[AchievementType.Wardrobe].Achievements[WardrobeLabels.BondagePalace] as ConditionalProgressAchievement)?.FinishConditionalTask();
                        (SaveData.Achievements[AchievementType.Wardrobe].Achievements[WardrobeLabels.MyKinkRunsDeep] as ConditionalProgressAchievement)?.FinishConditionalTask();
                        (SaveData.Achievements[AchievementType.Wardrobe].Achievements[WardrobeLabels.MyKinksRunDeeper] as ConditionalProgressAchievement)?.FinishConditionalTask();
                    }
                }
                break;
            case DeepDungeonType.EurekaOrthos:
                if (floor > 20 && floor <= 30)
                {
                    (SaveData.Achievements[AchievementType.Wardrobe].Achievements[WardrobeLabels.EurekaWhorethos] as ConditionalProgressAchievement)?.BeginConditionalTask();
                    (SaveData.Achievements[AchievementType.Wardrobe].Achievements[WardrobeLabels.MyKinkRunsDeep] as ConditionalProgressAchievement)?.BeginConditionalTask();
                    if (floor is 30)
                    {
                        (SaveData.Achievements[AchievementType.Wardrobe].Achievements[WardrobeLabels.EurekaWhorethos] as ConditionalProgressAchievement)?.FinishConditionalTask();
                        (SaveData.Achievements[AchievementType.Wardrobe].Achievements[WardrobeLabels.MyKinkRunsDeep] as ConditionalProgressAchievement)?.FinishConditionalTask();
                        (SaveData.Achievements[AchievementType.Wardrobe].Achievements[WardrobeLabels.MyKinksRunDeeper] as ConditionalProgressAchievement)?.FinishConditionalTask();
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
                (SaveData.Achievements[AchievementType.Orders].Achievements[OrderLabels.JustAVolunteer] as ProgressAchievement)?.IncrementProgress();
                (SaveData.Achievements[AchievementType.Orders].Achievements[OrderLabels.AsYouCommand] as ProgressAchievement)?.IncrementProgress();
                (SaveData.Achievements[AchievementType.Orders].Achievements[OrderLabels.AnythingForMyOwner] as ProgressAchievement)?.IncrementProgress();
                (SaveData.Achievements[AchievementType.Orders].Achievements[OrderLabels.GoodDrone] as ProgressAchievement)?.IncrementProgress();
                break;
            case OrderInteractionKind.Fail:
                (SaveData.Achievements[AchievementType.Orders].Achievements[OrderLabels.BadSlut] as ProgressAchievement)?.IncrementProgress();
                (SaveData.Achievements[AchievementType.Orders].Achievements[OrderLabels.NeedsTraining] as ProgressAchievement)?.IncrementProgress();
                (SaveData.Achievements[AchievementType.Orders].Achievements[OrderLabels.UsefulInOtherWays] as ProgressAchievement)?.IncrementProgress();
                break;
            case OrderInteractionKind.Create:
                (SaveData.Achievements[AchievementType.Orders].Achievements[OrderLabels.NewSlaveOwner] as ProgressAchievement)?.IncrementProgress();
                (SaveData.Achievements[AchievementType.Orders].Achievements[OrderLabels.TaskManager] as ProgressAchievement)?.IncrementProgress();
                (SaveData.Achievements[AchievementType.Orders].Achievements[OrderLabels.MaidMaster] as ProgressAchievement)?.IncrementProgress();
                (SaveData.Achievements[AchievementType.Orders].Achievements[OrderLabels.QueenOfDrones] as ProgressAchievement)?.IncrementProgress();
                break;
        }
    }

    private void OnGagApplied(GagLayer gagLayer, GagType gagType, bool isSelfApplied)
    {
        // if the gag is self applied
        if (isSelfApplied && gagType is not GagType.None)
        {
            (SaveData.Achievements[AchievementType.Gags].Achievements[GagLabels.SelfApplied] as ProgressAchievement)?.IncrementProgress();
        }
        // if the gag is not self applied
        else
        {
            if (gagType is not GagType.None)
            {
                (SaveData.Achievements[AchievementType.Gags].Achievements[GagLabels.ApplyToPair] as ProgressAchievement)?.IncrementProgress();
                (SaveData.Achievements[AchievementType.Gags].Achievements[GagLabels.LookingForTheRightFit] as ProgressAchievement)?.IncrementProgress();
                (SaveData.Achievements[AchievementType.Gags].Achievements[GagLabels.OralFixation] as ProgressAchievement)?.IncrementProgress();
                (SaveData.Achievements[AchievementType.Gags].Achievements[GagLabels.AKinkForDrool] as ProgressAchievement)?.IncrementProgress();

                (SaveData.Achievements[AchievementType.Gags].Achievements[GagLabels.ShushtainableResource] as ConditionalAchievement)?.CheckCompletion();

                (SaveData.Achievements[AchievementType.Gags].Achievements[GagLabels.SpeechSilverSilenceGolden] as DurationAchievement)?.StartTracking(gagType.GagName());
                (SaveData.Achievements[AchievementType.Gags].Achievements[GagLabels.TheKinkyLegend] as DurationAchievement)?.StartTracking(gagType.GagName());

                (SaveData.Achievements[AchievementType.Gags].Achievements[GagLabels.ATrueGagSlut] as TimedProgressAchievement)?.IncrementProgress();

                (SaveData.Achievements[AchievementType.Gags].Achievements[GagLabels.YourFavoriteNurse] as ConditionalProgressAchievement)?.CheckTaskProgress();

                (SaveData.Achievements[AchievementType.Secrets].Achievements[SecretLabels.Experimentalist] as ConditionalAchievement)?.CheckCompletion();

                (SaveData.Achievements[AchievementType.Secrets].Achievements[SecretLabels.GaggedPleasure] as ConditionalAchievement)?.CheckCompletion();
            }
        }
        // experimentalist
        (SaveData.Achievements[AchievementType.Secrets].Achievements[SecretLabels.Experimentalist] as ConditionalAchievement)?.CheckCompletion();
        (SaveData.Achievements[AchievementType.Secrets].Achievements[SecretLabels.GaggedPleasure] as ConditionalAchievement)?.CheckCompletion();
    }

    private void OnRestraintApplied(RestraintSet set, bool isSelfApplied)
    {
        (SaveData.Achievements[AchievementType.Wardrobe].Achievements[WardrobeLabels.FirstTiemers] as ProgressAchievement)?.IncrementProgress();
        (SaveData.Achievements[AchievementType.Secrets].Achievements[SecretLabels.Experimentalist] as ConditionalAchievement)?.CheckCompletion();

        // we were the applier
        if (isSelfApplied)
        {
            (SaveData.Achievements[AchievementType.Wardrobe].Achievements[WardrobeLabels.SelfBondageEnthusiast] as ProgressAchievement)?.IncrementProgress();
        }
        // we were not the applier
        else
        {
            // start the "Auctioned Off" achievement
            (SaveData.Achievements[AchievementType.Wardrobe].Achievements[WardrobeLabels.AuctionedOff] as ConditionalProgressAchievement)?.BeginConditionalTask();

            // Achievements related to applying
            (SaveData.Achievements[AchievementType.Wardrobe].Achievements[WardrobeLabels.DiDEnthusiast] as ProgressAchievement)?.IncrementProgress();
            (SaveData.Achievements[AchievementType.Wardrobe].Achievements[WardrobeLabels.BondageBunny] as TimedProgressAchievement)?.IncrementProgress();
            (SaveData.Achievements[AchievementType.Wardrobe].Achievements[WardrobeLabels.Bondodge] as ConditionalDurationAchievement)?.CheckCompletion();

            // see if valid for "cuffed-19"
            if (set.DrawData.TryGetValue(EquipSlot.Hands, out var handData) && handData.GameItem.Id != ItemIdVars.NothingItem(EquipSlot.Hands).Id)
            {
                (SaveData.Achievements[AchievementType.Wardrobe].Achievements[WardrobeLabels.Cuffed19] as ProgressAchievement)?.IncrementProgress();
            }

            // check for dyes
            if (set.DrawData.Any(x => x.Value.GameStain.Stain1 != 0 || x.Value.GameStain.Stain2 != 0))
            {
                (SaveData.Achievements[AchievementType.Wardrobe].Achievements[WardrobeLabels.ToDyeFor] as ProgressAchievement)?.IncrementProgress();
                (SaveData.Achievements[AchievementType.Wardrobe].Achievements[WardrobeLabels.DyeAnotherDay] as ProgressAchievement)?.IncrementProgress();
                (SaveData.Achievements[AchievementType.Wardrobe].Achievements[WardrobeLabels.DyeHard] as ProgressAchievement)?.IncrementProgress();
            }
        }
    }

    private void OnPairRestraintLockChange(Padlocks padlock, bool isUnlocking, bool wasAssigner)
    {
        // we have unlocked a pair.
        if (isUnlocking)
        {
            if (padlock is Padlocks.PasswordPadlock && !wasAssigner)
                (SaveData.Achievements[AchievementType.Wardrobe].Achievements[WardrobeLabels.SoldSlave] as ProgressAchievement)?.IncrementProgress();

            (SaveData.Achievements[AchievementType.Wardrobe].Achievements[WardrobeLabels.TheRescuer] as ProgressAchievement)?.IncrementProgress();
        }
        // we have locked a pair up.
        else
        {
            (SaveData.Achievements[AchievementType.Wardrobe].Achievements[WardrobeLabels.RiggersFirstSession] as ProgressAchievement)?.IncrementProgress();
            (SaveData.Achievements[AchievementType.Wardrobe].Achievements[WardrobeLabels.MyLittlePlaything] as ProgressAchievement)?.IncrementProgress();
            (SaveData.Achievements[AchievementType.Wardrobe].Achievements[WardrobeLabels.SuitsYouBitch] as ProgressAchievement)?.IncrementProgress();
            (SaveData.Achievements[AchievementType.Wardrobe].Achievements[WardrobeLabels.TiesThatBind] as ProgressAchievement)?.IncrementProgress();
            (SaveData.Achievements[AchievementType.Wardrobe].Achievements[WardrobeLabels.SlaveTraining] as ProgressAchievement)?.IncrementProgress();
            (SaveData.Achievements[AchievementType.Wardrobe].Achievements[WardrobeLabels.CeremonyOfEternalBondage] as ProgressAchievement)?.IncrementProgress();
        }
    }

    private void OnRestraintLock(RestraintSet set, bool isSelfApplied)
    {
        // we locked our set.
        if (isSelfApplied)
        {
            (SaveData.Achievements[AchievementType.Wardrobe].Achievements[WardrobeLabels.RiggersFirstSession] as ProgressAchievement)?.IncrementProgress();
        }
        // someone else locked our set
        else
        {
            (SaveData.Achievements[AchievementType.Wardrobe].Achievements[WardrobeLabels.FirstTimeBondage] as ProgressAchievement)?.IncrementProgress();
            (SaveData.Achievements[AchievementType.Wardrobe].Achievements[WardrobeLabels.AmateurBondage] as ProgressAchievement)?.IncrementProgress();
            (SaveData.Achievements[AchievementType.Wardrobe].Achievements[WardrobeLabels.ComfortRestraint] as ProgressAchievement)?.IncrementProgress();
            (SaveData.Achievements[AchievementType.Wardrobe].Achievements[WardrobeLabels.DayInTheLifeOfABondageSlave] as ProgressAchievement)?.IncrementProgress();
            (SaveData.Achievements[AchievementType.Wardrobe].Achievements[WardrobeLabels.AWeekInBondage] as ProgressAchievement)?.IncrementProgress();
            (SaveData.Achievements[AchievementType.Wardrobe].Achievements[WardrobeLabels.AMonthInBondage] as ProgressAchievement)?.IncrementProgress();
        }
    }

    private void OnPuppetAccessGiven(bool wasAllPerms)
    {
        if (wasAllPerms) // All Perms access given to another pair.
            (SaveData.Achievements[AchievementType.Puppeteer].Achievements[PuppeteerLabels.CompleteDevotion] as ProgressAchievement)?.IncrementProgress();
        else // Emote perms given to another pair.
            (SaveData.Achievements[AchievementType.Puppeteer].Achievements[PuppeteerLabels.ControlMyBody] as ProgressAchievement)?.IncrementProgress();
    }

    private void OnPatternAction(PatternInteractionKind actionType, Guid patternGuid, bool wasAlarm)
    {
        switch (actionType)
        {
            case PatternInteractionKind.Published:
                (SaveData.Achievements[AchievementType.Toybox].Achievements[ToyboxLabels.FunForAll] as ProgressAchievement)?.IncrementProgress();
                (SaveData.Achievements[AchievementType.Toybox].Achievements[ToyboxLabels.DeviousComposer] as ProgressAchievement)?.IncrementProgress();
                break;
            case PatternInteractionKind.Downloaded:
                (SaveData.Achievements[AchievementType.Toybox].Achievements[ToyboxLabels.CravingPleasure] as ProgressAchievement)?.IncrementProgress();
                break;
            case PatternInteractionKind.Liked:
                (SaveData.Achievements[AchievementType.Toybox].Achievements[ToyboxLabels.PatternLover] as ProgressAchievement)?.IncrementProgress();
                break;
            case PatternInteractionKind.Started:
                if (patternGuid != Guid.Empty)
                    (SaveData.Achievements[AchievementType.Toybox].Achievements[ToyboxLabels.EnduranceQueen] as DurationAchievement)?.StartTracking(patternGuid.ToString());
                if (wasAlarm && patternGuid != Guid.Empty)
                    (SaveData.Achievements[AchievementType.Toybox].Achievements[ToyboxLabels.HornyMornings] as ProgressAchievement)?.IncrementProgress();
                break;
            case PatternInteractionKind.Stopped:
                if (patternGuid != Guid.Empty)
                    (SaveData.Achievements[AchievementType.Toybox].Achievements[ToyboxLabels.EnduranceQueen] as DurationAchievement)?.StopTracking(patternGuid.ToString());
                break;
        }
    }

    private void OnDeviceConnected()
    {
        (SaveData.Achievements[AchievementType.Toybox].Achievements[ToyboxLabels.MyFavoriteToys] as ConditionalAchievement)?.CheckCompletion();
        (SaveData.Achievements[AchievementType.Secrets].Achievements[SecretLabels.Experimentalist] as ConditionalAchievement)?.CheckCompletion();
    }

    private void OnTriggerFired()
    {
        (SaveData.Achievements[AchievementType.Toybox].Achievements[ToyboxLabels.SubtleReminders] as ProgressAchievement)?.IncrementProgress();
        (SaveData.Achievements[AchievementType.Toybox].Achievements[ToyboxLabels.FingerOnTheTrigger] as ProgressAchievement)?.IncrementProgress();
        (SaveData.Achievements[AchievementType.Toybox].Achievements[ToyboxLabels.TriggerHappy] as ProgressAchievement)?.IncrementProgress();
    }

    private void OnHardcoreForcedPairAction(HardcorePairActionKind actionKind, NewState state, string pairUID, bool actionWasFromClient)
    {
        switch (actionKind)
        {
            case HardcorePairActionKind.ForcedFollow:
                // If we are forcing someone else to follow us
                if (actionWasFromClient && state is NewState.Enabled)
                {
                    (SaveData.Achievements[AchievementType.Hardcore].Achievements[HardcoreLabels.AllTheCollarsOfTheRainbow] as ProgressAchievement)?.IncrementProgress();
                    (SaveData.Achievements[AchievementType.Hardcore].Achievements[HardcoreLabels.ForcedFollow] as DurationAchievement)?.StartTracking(pairUID);
                    (SaveData.Achievements[AchievementType.Hardcore].Achievements[HardcoreLabels.ForcedWalkies] as DurationAchievement)?.StartTracking(pairUID);
                }
                // check if we have gone through the full dungeon in hardcore follow on.
                if (state is NewState.Disabled && actionWasFromClient)
                    (SaveData.Achievements[AchievementType.Hardcore].Achievements[HardcoreLabels.UCanTieThis] as ConditionalProgressAchievement)?.CheckTaskProgress();

                // if another user has sent us that their forced follow stopped, stop tracking the duration ones.
                if (state is NewState.Disabled && !actionWasFromClient)
                {
                    (SaveData.Achievements[AchievementType.Hardcore].Achievements[HardcoreLabels.ForcedFollow] as DurationAchievement)?.StopTracking(pairUID);
                    (SaveData.Achievements[AchievementType.Hardcore].Achievements[HardcoreLabels.ForcedWalkies] as DurationAchievement)?.StopTracking(pairUID);
                }

                // if someone else has ordered us to start following, begin tracking.
                if (state is NewState.Enabled && !actionWasFromClient)
                {
                    (SaveData.Achievements[AchievementType.Hardcore].Achievements[HardcoreLabels.TimeForWalkies] as ConditionalDurationAchievement)?.CheckCompletion();
                    (SaveData.Achievements[AchievementType.Hardcore].Achievements[HardcoreLabels.GettingStepsIn] as ConditionalDurationAchievement)?.CheckCompletion();
                    (SaveData.Achievements[AchievementType.Hardcore].Achievements[HardcoreLabels.WalkiesLover] as ConditionalDurationAchievement)?.CheckCompletion();
                }
                // if our forced to follow order from another user has stopped, check for completion.
                if (state is NewState.Disabled && !actionWasFromClient)
                {
                    (SaveData.Achievements[AchievementType.Hardcore].Achievements[HardcoreLabels.TimeForWalkies] as ConditionalDurationAchievement)?.ResetOrComplete();
                    (SaveData.Achievements[AchievementType.Hardcore].Achievements[HardcoreLabels.GettingStepsIn] as ConditionalDurationAchievement)?.ResetOrComplete();
                    (SaveData.Achievements[AchievementType.Hardcore].Achievements[HardcoreLabels.WalkiesLover] as ConditionalDurationAchievement)?.ResetOrComplete();
                }

                break;
            case HardcorePairActionKind.ForcedSit:
                if (!actionWasFromClient && state is NewState.Enabled)
                {
                    (SaveData.Achievements[AchievementType.Hardcore].Achievements[HardcoreLabels.LivingFurniture] as ConditionalDurationAchievement)?.CheckCompletion();
                }
                else if (!actionWasFromClient && state is NewState.Disabled)
                {
                    (SaveData.Achievements[AchievementType.Hardcore].Achievements[HardcoreLabels.LivingFurniture] as ConditionalDurationAchievement)?.ResetOrComplete();
                }

                break;
            case HardcorePairActionKind.ForcedStay:
                if (!actionWasFromClient && state is NewState.Enabled)
                {
                    (SaveData.Achievements[AchievementType.Hardcore].Achievements[HardcoreLabels.PetTraining] as ConditionalDurationAchievement)?.CheckCompletion();
                    (SaveData.Achievements[AchievementType.Hardcore].Achievements[HardcoreLabels.NotGoingAnywhere] as ConditionalDurationAchievement)?.CheckCompletion();
                    (SaveData.Achievements[AchievementType.Hardcore].Achievements[HardcoreLabels.HouseTrained] as ConditionalDurationAchievement)?.CheckCompletion();
                }
                else if (!actionWasFromClient && state is NewState.Disabled)
                {
                    (SaveData.Achievements[AchievementType.Hardcore].Achievements[HardcoreLabels.PetTraining] as ConditionalDurationAchievement)?.ResetOrComplete();
                    (SaveData.Achievements[AchievementType.Hardcore].Achievements[HardcoreLabels.NotGoingAnywhere] as ConditionalDurationAchievement)?.ResetOrComplete();
                    (SaveData.Achievements[AchievementType.Hardcore].Achievements[HardcoreLabels.HouseTrained] as ConditionalDurationAchievement)?.ResetOrComplete();
                }
                break;
            case HardcorePairActionKind.ForcedBlindfold:
                // if we have had our blindfold set to enabled by another pair, perform the following:
                if (!actionWasFromClient && state is NewState.Enabled)
                {
                    (SaveData.Achievements[AchievementType.Hardcore].Achievements[HardcoreLabels.BlindLeadingTheBlind] as ConditionalAchievement)?.CheckCompletion();
                    (SaveData.Achievements[AchievementType.Hardcore].Achievements[HardcoreLabels.WhoNeedsToSee] as ConditionalDurationAchievement)?.CheckCompletion();
                }
                // if another pair is removing our blindfold, perform the following:
                else if (!actionWasFromClient && state is NewState.Disabled)
                {
                    (SaveData.Achievements[AchievementType.Hardcore].Achievements[HardcoreLabels.WhoNeedsToSee] as ConditionalDurationAchievement)?.ResetOrComplete();
                }
                break;

        }
    }

    private void OnShockSent()
    {
        (SaveData.Achievements[AchievementType.Hardcore].Achievements[HardcoreLabels.IndulgingSparks] as ProgressAchievement)?.IncrementProgress();
        (SaveData.Achievements[AchievementType.Hardcore].Achievements[HardcoreLabels.CantGetEnough] as ProgressAchievement)?.IncrementProgress();
        (SaveData.Achievements[AchievementType.Hardcore].Achievements[HardcoreLabels.VerThunder] as ProgressAchievement)?.IncrementProgress();
        (SaveData.Achievements[AchievementType.Hardcore].Achievements[HardcoreLabels.WickedThunder] as ProgressAchievement)?.IncrementProgress();
        (SaveData.Achievements[AchievementType.Hardcore].Achievements[HardcoreLabels.ElectropeHasNoLimits] as ProgressAchievement)?.IncrementProgress();
    }

    private void OnShockReceived()
    {
        (SaveData.Achievements[AchievementType.Hardcore].Achievements[HardcoreLabels.ShockAndAwe] as ProgressAchievement)?.IncrementProgress();
        (SaveData.Achievements[AchievementType.Hardcore].Achievements[HardcoreLabels.ShockingExperience] as ProgressAchievement)?.IncrementProgress();
        (SaveData.Achievements[AchievementType.Hardcore].Achievements[HardcoreLabels.ShockolateTasting] as ProgressAchievement)?.IncrementProgress();
        (SaveData.Achievements[AchievementType.Hardcore].Achievements[HardcoreLabels.ShockAddiction] as ProgressAchievement)?.IncrementProgress();
        (SaveData.Achievements[AchievementType.Hardcore].Achievements[HardcoreLabels.WarriorOfElectrope] as ProgressAchievement)?.IncrementProgress();
    }


    private void OnChatMessage(XivChatType channel, string message, string SenderName)
    {
        if (message.Split(' ').Count() > 5)
        {
            if (channel is XivChatType.Say)
            {
                (SaveData.Achievements[AchievementType.Gags].Achievements[GagLabels.SpeakUpSlut] as ProgressAchievement)?.IncrementProgress();
            }
            else if (channel is XivChatType.Yell)
            {
                (SaveData.Achievements[AchievementType.Gags].Achievements[GagLabels.CantHearYou] as ProgressAchievement)?.IncrementProgress();
            }
            else if (channel is XivChatType.Shout)
            {
                (SaveData.Achievements[AchievementType.Gags].Achievements[GagLabels.OneMoreForTheCrowd] as ProgressAchievement)?.IncrementProgress();
            }
        }
        // check if we meet some of the secret requirements.
        (SaveData.Achievements[AchievementType.Secrets].Achievements[SecretLabels.HelplessDamsel] as ConditionalAchievement)?.CheckCompletion();
    }

    private void OnEmoteExecuted(ulong emoteCallerObjectId, ushort emoteId, string emoteName, ulong targetObjectId)
    {
        // doing /lookout while blindfolded.
        if (!(SaveData.Achievements[AchievementType.Hardcore].Achievements[HardcoreLabels.WhatAView] as ConditionalAchievement)?.IsCompleted ?? false && emoteCallerObjectId == _frameworkUtils.ClientState.LocalPlayer?.GameObjectId)
            if (emoteName.Contains("lookout", StringComparison.OrdinalIgnoreCase))
                (SaveData.Achievements[AchievementType.Hardcore].Achievements[HardcoreLabels.WhatAView] as ConditionalAchievement)?.CheckCompletion();

        // detect getting slapped.
        if (!(SaveData.Achievements[AchievementType.Generic].Achievements[GenericLabels.ICantBelieveYouveDoneThis] as ConditionalAchievement)?.IsCompleted ?? false && targetObjectId == _frameworkUtils.ClientState.LocalPlayer?.GameObjectId)
            if (emoteName.Contains("slap", StringComparison.OrdinalIgnoreCase))
                (SaveData.Achievements[AchievementType.Generic].Achievements[GenericLabels.ICantBelieveYouveDoneThis] as ConditionalAchievement)?.CheckCompletion();
    }

    private void OnPuppeteerEmoteSent(string emoteName)
    {
        if (emoteName.Contains("shush", StringComparison.OrdinalIgnoreCase))
        {
            (SaveData.Achievements[AchievementType.Gags].Achievements[GagLabels.QuietNowDear] as ConditionalAchievement)?.CheckCompletion();
        }
        else if (emoteName.Contains("dance", StringComparison.OrdinalIgnoreCase))
        {
            (SaveData.Achievements[AchievementType.Puppeteer].Achievements[PuppeteerLabels.ShowingOff] as ProgressAchievement)?.IncrementProgress();
        }
        else if (emoteName.Contains("grovel", StringComparison.OrdinalIgnoreCase))
        {
            (SaveData.Achievements[AchievementType.Puppeteer].Achievements[PuppeteerLabels.KissMyHeels] as ProgressAchievement)?.IncrementProgress();
        }
        else if (emoteName.Contains("sulk", StringComparison.OrdinalIgnoreCase))
        {
            (SaveData.Achievements[AchievementType.Puppeteer].Achievements[PuppeteerLabels.Ashamed] as ProgressAchievement)?.IncrementProgress();
        }
        else if (emoteName.Contains("sit", StringComparison.OrdinalIgnoreCase) || emoteName.Contains("groundsit", StringComparison.OrdinalIgnoreCase))
        {
            (SaveData.Achievements[AchievementType.Puppeteer].Achievements[PuppeteerLabels.WhoIsAGoodPet] as ProgressAchievement)?.IncrementProgress();
        }
    }

    private void OnPairAdded()
    {
        (SaveData.Achievements[AchievementType.Generic].Achievements[GenericLabels.AddedFirstPair] as ConditionalAchievement)?.CheckCompletion();
        (SaveData.Achievements[AchievementType.Generic].Achievements[GenericLabels.TheCollector] as ProgressAchievement)?.IncrementProgress();
    }

    private void OnCursedLootFound()
    {
        (SaveData.Achievements[AchievementType.Wardrobe].Achievements[WardrobeLabels.TemptingFatesTreasure] as ProgressAchievement)?.IncrementProgress();
        (SaveData.Achievements[AchievementType.Wardrobe].Achievements[WardrobeLabels.BadEndSeeker] as ProgressAchievement)?.IncrementProgress();
        (SaveData.Achievements[AchievementType.Wardrobe].Achievements[WardrobeLabels.EverCursed] as ProgressAchievement)?.IncrementProgress();
    }

    private void OnJobChange(GlamourUpdateType changeType)
    {
        (SaveData.Achievements[AchievementType.Generic].Achievements[GenericLabels.EscapingIsNotEasy] as ConditionalAchievement)?.CheckCompletion();
    }
}
