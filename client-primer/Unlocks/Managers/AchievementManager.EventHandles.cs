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
        (SaveData.Achievements[AchievementModuleKind.Secrets].Achievements[SecretLabels.KinkyTeacher] as ConditionalProgressAchievement)?.CheckTaskProgress(amount);
        (SaveData.Achievements[AchievementModuleKind.Secrets].Achievements[SecretLabels.KinkyProfessor] as ConditionalProgressAchievement)?.CheckTaskProgress(amount);
        (SaveData.Achievements[AchievementModuleKind.Secrets].Achievements[SecretLabels.KinkyMentor] as ConditionalProgressAchievement)?.CheckTaskProgress(amount);
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

            (SaveData.Achievements[AchievementModuleKind.Toybox].Achievements[ToyboxLabels.MotivationForRestoration] as ConditionalDurationAchievement)?.CheckCompletion();
        else
            (SaveData.Achievements[AchievementModuleKind.Toybox].Achievements[ToyboxLabels.MotivationForRestoration] as ConditionalDurationAchievement)?.ResetOrComplete();

        // if we are in a dungeon:
        if (_frameworkUtils.InDungeonOrDuty)
        {
            (SaveData.Achievements[AchievementModuleKind.Gags].Achievements[GagLabels.SilentButDeadly] as ConditionalProgressAchievement)?.BeginConditionalTask();
            (SaveData.Achievements[AchievementModuleKind.Hardcore].Achievements[HardcoreLabels.UCanTieThis] as ConditionalProgressAchievement)?.BeginConditionalTask();

            if (_frameworkUtils.PlayerJobRole is ActionRoles.Healer)
                (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.HealSlut] as ConditionalProgressAchievement)?.BeginConditionalTask();
        }
        else
        {
            if ((SaveData.Achievements[AchievementModuleKind.Hardcore].Achievements[HardcoreLabels.UCanTieThis] as ConditionalProgressAchievement)?.ConditionalTaskBegun ?? false)
                (SaveData.Achievements[AchievementModuleKind.Hardcore].Achievements[HardcoreLabels.UCanTieThis] as ConditionalProgressAchievement)?.FinishConditionalTask();

            if ((SaveData.Achievements[AchievementModuleKind.Gags].Achievements[GagLabels.SilentButDeadly] as ConditionalProgressAchievement)?.ConditionalTaskBegun ?? false)
                (SaveData.Achievements[AchievementModuleKind.Gags].Achievements[GagLabels.SilentButDeadly] as ConditionalProgressAchievement)?.FinishConditionalTask();

            if ((SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.HealSlut] as ConditionalProgressAchievement)?.ConditionalTaskBegun ?? false)
                (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.HealSlut] as ConditionalProgressAchievement)?.FinishConditionalTask();
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

    private void OnGagApplied(GagLayer gagLayer, GagType gagType, bool isSelfApplied)
    {
        // if the gag is self applied
        if (isSelfApplied && gagType is not GagType.None)
        {
            (SaveData.Achievements[AchievementModuleKind.Gags].Achievements[GagLabels.SelfApplied] as ProgressAchievement)?.IncrementProgress();
        }
        // if the gag is not self applied
        else
        {
            if (gagType is not GagType.None)
            {
                (SaveData.Achievements[AchievementModuleKind.Gags].Achievements[GagLabels.ApplyToPair] as ProgressAchievement)?.IncrementProgress();
                (SaveData.Achievements[AchievementModuleKind.Gags].Achievements[GagLabels.LookingForTheRightFit] as ProgressAchievement)?.IncrementProgress();
                (SaveData.Achievements[AchievementModuleKind.Gags].Achievements[GagLabels.OralFixation] as ProgressAchievement)?.IncrementProgress();
                (SaveData.Achievements[AchievementModuleKind.Gags].Achievements[GagLabels.AKinkForDrool] as ProgressAchievement)?.IncrementProgress();

                (SaveData.Achievements[AchievementModuleKind.Gags].Achievements[GagLabels.ShushtainableResource] as ConditionalAchievement)?.CheckCompletion();

                (SaveData.Achievements[AchievementModuleKind.Gags].Achievements[GagLabels.SpeechSilverSilenceGolden] as DurationAchievement)?.StartTracking(gagType.GagName());
                (SaveData.Achievements[AchievementModuleKind.Gags].Achievements[GagLabels.TheKinkyLegend] as DurationAchievement)?.StartTracking(gagType.GagName());

                (SaveData.Achievements[AchievementModuleKind.Gags].Achievements[GagLabels.ATrueGagSlut] as TimedProgressAchievement)?.IncrementProgress();

                (SaveData.Achievements[AchievementModuleKind.Gags].Achievements[GagLabels.YourFavoriteNurse] as ConditionalProgressAchievement)?.CheckTaskProgress();

                (SaveData.Achievements[AchievementModuleKind.Secrets].Achievements[SecretLabels.Experimentalist] as ConditionalAchievement)?.CheckCompletion();

                (SaveData.Achievements[AchievementModuleKind.Secrets].Achievements[SecretLabels.GaggedPleasure] as ConditionalAchievement)?.CheckCompletion();
            }
        }
        // experimentalist
        (SaveData.Achievements[AchievementModuleKind.Secrets].Achievements[SecretLabels.Experimentalist] as ConditionalAchievement)?.CheckCompletion();
        (SaveData.Achievements[AchievementModuleKind.Secrets].Achievements[SecretLabels.GaggedPleasure] as ConditionalAchievement)?.CheckCompletion();
    }

    private void OnRestraintApplied(RestraintSet set, bool isSelfApplied)
    {
        (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.FirstTiemers] as ProgressAchievement)?.IncrementProgress();
        (SaveData.Achievements[AchievementModuleKind.Secrets].Achievements[SecretLabels.Experimentalist] as ConditionalAchievement)?.CheckCompletion();

        // we were the applier
        if (isSelfApplied)
        {
            (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.SelfBondageEnthusiast] as ProgressAchievement)?.IncrementProgress();
        }
        // we were not the applier
        else
        {
            // start the "Auctioned Off" achievement
            (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.AuctionedOff] as ConditionalProgressAchievement)?.BeginConditionalTask();

            // Achievements related to applying
            (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.DiDEnthusiast] as ProgressAchievement)?.IncrementProgress();
            (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.BondageBunny] as TimedProgressAchievement)?.IncrementProgress();
            (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.Bondodge] as ConditionalDurationAchievement)?.CheckCompletion();

            // see if valid for "cuffed-19"
            if (set.DrawData.TryGetValue(EquipSlot.Hands, out var handData) && handData.GameItem.Id != ItemIdVars.NothingItem(EquipSlot.Hands).Id)
            {
                (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.Cuffed19] as ProgressAchievement)?.IncrementProgress();
            }

            // check for dyes
            if (set.DrawData.Any(x => x.Value.GameStain.Stain1 != 0 || x.Value.GameStain.Stain2 != 0))
            {
                (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.ToDyeFor] as ProgressAchievement)?.IncrementProgress();
                (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.DyeAnotherDay] as ProgressAchievement)?.IncrementProgress();
                (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.DyeHard] as ProgressAchievement)?.IncrementProgress();
            }
        }
    }

    private void OnPairRestraintLockChange(Padlocks padlock, bool isUnlocking, bool wasAssigner)
    {
        // we have unlocked a pair.
        if (isUnlocking)
        {
            if (padlock is Padlocks.PasswordPadlock && !wasAssigner)
                (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.SoldSlave] as ProgressAchievement)?.IncrementProgress();

            (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.TheRescuer] as ProgressAchievement)?.IncrementProgress();
        }
        // we have locked a pair up.
        else
        {
            (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.RiggersFirstSession] as ProgressAchievement)?.IncrementProgress();
            (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.MyLittlePlaything] as ProgressAchievement)?.IncrementProgress();
            (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.SuitsYouBitch] as ProgressAchievement)?.IncrementProgress();
            (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.TiesThatBind] as ProgressAchievement)?.IncrementProgress();
            (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.SlaveTraining] as ProgressAchievement)?.IncrementProgress();
            (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.CeremonyOfEternalBondage] as ProgressAchievement)?.IncrementProgress();
        }
    }

    private void OnRestraintLock(RestraintSet set, bool isSelfApplied)
    {
        // we locked our set.
        if (isSelfApplied)
        {
            (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.RiggersFirstSession] as ProgressAchievement)?.IncrementProgress();
        }
        // someone else locked our set
        else
        {
            (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.FirstTimeBondage] as ProgressAchievement)?.IncrementProgress();
            (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.AmateurBondage] as ProgressAchievement)?.IncrementProgress();
            (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.ComfortRestraint] as ProgressAchievement)?.IncrementProgress();
            (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.DayInTheLifeOfABondageSlave] as ProgressAchievement)?.IncrementProgress();
            (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.AWeekInBondage] as ProgressAchievement)?.IncrementProgress();
            (SaveData.Achievements[AchievementModuleKind.Wardrobe].Achievements[WardrobeLabels.AMonthInBondage] as ProgressAchievement)?.IncrementProgress();
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

    private void OnHardcoreForcedPairAction(HardcorePairActionKind actionKind, NewState state, string pairUID, bool actionWasFromClient)
    {
        switch (actionKind)
        {
            case HardcorePairActionKind.ForcedFollow:
                // If we are forcing someone else to follow us
                if (actionWasFromClient && state is NewState.Enabled)
                {
                    (SaveData.Achievements[AchievementModuleKind.Hardcore].Achievements[HardcoreLabels.AllTheCollarsOfTheRainbow] as ProgressAchievement)?.IncrementProgress();
                    (SaveData.Achievements[AchievementModuleKind.Hardcore].Achievements[HardcoreLabels.ForcedFollow] as DurationAchievement)?.StartTracking(pairUID);
                    (SaveData.Achievements[AchievementModuleKind.Hardcore].Achievements[HardcoreLabels.ForcedWalkies] as DurationAchievement)?.StartTracking(pairUID);
                }
                // check if we have gone through the full dungeon in hardcore follow on.
                if (state is NewState.Disabled && actionWasFromClient)
                    (SaveData.Achievements[AchievementModuleKind.Hardcore].Achievements[HardcoreLabels.UCanTieThis] as ConditionalProgressAchievement)?.CheckTaskProgress();

                // if another user has sent us that their forced follow stopped, stop tracking the duration ones.
                if (state is NewState.Disabled && !actionWasFromClient)
                {
                    (SaveData.Achievements[AchievementModuleKind.Hardcore].Achievements[HardcoreLabels.ForcedFollow] as DurationAchievement)?.StopTracking(pairUID);
                    (SaveData.Achievements[AchievementModuleKind.Hardcore].Achievements[HardcoreLabels.ForcedWalkies] as DurationAchievement)?.StopTracking(pairUID);
                }

                // if someone else has ordered us to start following, begin tracking.
                if (state is NewState.Enabled && !actionWasFromClient)
                {
                    (SaveData.Achievements[AchievementModuleKind.Hardcore].Achievements[HardcoreLabels.TimeForWalkies] as ConditionalDurationAchievement)?.CheckCompletion();
                    (SaveData.Achievements[AchievementModuleKind.Hardcore].Achievements[HardcoreLabels.GettingStepsIn] as ConditionalDurationAchievement)?.CheckCompletion();
                    (SaveData.Achievements[AchievementModuleKind.Hardcore].Achievements[HardcoreLabels.WalkiesLover] as ConditionalDurationAchievement)?.CheckCompletion();
                }
                // if our forced to follow order from another user has stopped, check for completion.
                if (state is NewState.Disabled && !actionWasFromClient)
                {
                    (SaveData.Achievements[AchievementModuleKind.Hardcore].Achievements[HardcoreLabels.TimeForWalkies] as ConditionalDurationAchievement)?.ResetOrComplete();
                    (SaveData.Achievements[AchievementModuleKind.Hardcore].Achievements[HardcoreLabels.GettingStepsIn] as ConditionalDurationAchievement)?.ResetOrComplete();
                    (SaveData.Achievements[AchievementModuleKind.Hardcore].Achievements[HardcoreLabels.WalkiesLover] as ConditionalDurationAchievement)?.ResetOrComplete();
                }

                break;
            case HardcorePairActionKind.ForcedSit:
                if (!actionWasFromClient && state is NewState.Enabled)
                {
                    (SaveData.Achievements[AchievementModuleKind.Hardcore].Achievements[HardcoreLabels.LivingFurniture] as ConditionalDurationAchievement)?.CheckCompletion();
                }
                else if (!actionWasFromClient && state is NewState.Disabled)
                {
                    (SaveData.Achievements[AchievementModuleKind.Hardcore].Achievements[HardcoreLabels.LivingFurniture] as ConditionalDurationAchievement)?.ResetOrComplete();
                }

                break;
            case HardcorePairActionKind.ForcedStay:
                if (!actionWasFromClient && state is NewState.Enabled)
                {
                    (SaveData.Achievements[AchievementModuleKind.Hardcore].Achievements[HardcoreLabels.PetTraining] as ConditionalDurationAchievement)?.CheckCompletion();
                    (SaveData.Achievements[AchievementModuleKind.Hardcore].Achievements[HardcoreLabels.NotGoingAnywhere] as ConditionalDurationAchievement)?.CheckCompletion();
                    (SaveData.Achievements[AchievementModuleKind.Hardcore].Achievements[HardcoreLabels.HouseTrained] as ConditionalDurationAchievement)?.CheckCompletion();
                }
                else if (!actionWasFromClient && state is NewState.Disabled)
                {
                    (SaveData.Achievements[AchievementModuleKind.Hardcore].Achievements[HardcoreLabels.PetTraining] as ConditionalDurationAchievement)?.ResetOrComplete();
                    (SaveData.Achievements[AchievementModuleKind.Hardcore].Achievements[HardcoreLabels.NotGoingAnywhere] as ConditionalDurationAchievement)?.ResetOrComplete();
                    (SaveData.Achievements[AchievementModuleKind.Hardcore].Achievements[HardcoreLabels.HouseTrained] as ConditionalDurationAchievement)?.ResetOrComplete();
                }
                break;
            case HardcorePairActionKind.ForcedBlindfold:
                // if we have had our blindfold set to enabled by another pair, perform the following:
                if (!actionWasFromClient && state is NewState.Enabled)
                {
                    (SaveData.Achievements[AchievementModuleKind.Hardcore].Achievements[HardcoreLabels.BlindLeadingTheBlind] as ConditionalAchievement)?.CheckCompletion();
                    (SaveData.Achievements[AchievementModuleKind.Hardcore].Achievements[HardcoreLabels.WhoNeedsToSee] as ConditionalDurationAchievement)?.CheckCompletion();
                }
                // if another pair is removing our blindfold, perform the following:
                else if (!actionWasFromClient && state is NewState.Disabled)
                {
                    (SaveData.Achievements[AchievementModuleKind.Hardcore].Achievements[HardcoreLabels.WhoNeedsToSee] as ConditionalDurationAchievement)?.ResetOrComplete();
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


    private void OnChatMessage(XivChatType channel, string message, string SenderName)
    {
        if (message.Split(' ').Count() > 5)
        {
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
        // check if we meet some of the secret requirements.
        (SaveData.Achievements[AchievementModuleKind.Secrets].Achievements[SecretLabels.HelplessDamsel] as ConditionalAchievement)?.CheckCompletion();
    }

    private void OnEmoteExecuted(ulong emoteCallerObjectId, ushort emoteId, string emoteName, ulong targetObjectId)
    {
        // doing /lookout while blindfolded.
        if (!(SaveData.Achievements[AchievementModuleKind.Hardcore].Achievements[HardcoreLabels.WhatAView] as ConditionalAchievement)?.IsCompleted ?? false && emoteCallerObjectId == _frameworkUtils.ClientState.LocalPlayer?.GameObjectId)
            if (emoteName.Contains("lookout", StringComparison.OrdinalIgnoreCase))
                (SaveData.Achievements[AchievementModuleKind.Hardcore].Achievements[HardcoreLabels.WhatAView] as ConditionalAchievement)?.CheckCompletion();

        // detect getting slapped.
        if (!(SaveData.Achievements[AchievementModuleKind.Generic].Achievements[GenericLabels.ICantBelieveYouveDoneThis] as ConditionalAchievement)?.IsCompleted ?? false && targetObjectId == _frameworkUtils.ClientState.LocalPlayer?.GameObjectId)
            if (emoteName.Contains("slap", StringComparison.OrdinalIgnoreCase))
                (SaveData.Achievements[AchievementModuleKind.Generic].Achievements[GenericLabels.ICantBelieveYouveDoneThis] as ConditionalAchievement)?.CheckCompletion();
    }

    private void OnPuppeteerEmoteSent(string emoteName)
    {
        if (emoteName.Contains("shush", StringComparison.OrdinalIgnoreCase))
        {
            (SaveData.Achievements[AchievementModuleKind.Gags].Achievements[GagLabels.QuietNowDear] as ConditionalAchievement)?.CheckCompletion();
        }
        else if (emoteName.Contains("dance", StringComparison.OrdinalIgnoreCase))
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
}
