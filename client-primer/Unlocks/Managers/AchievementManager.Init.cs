using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using GagSpeak.GagspeakConfiguration.Models;
using GagSpeak.Utils;
using GagspeakAPI.Data;
using GagspeakAPI.Extensions;
using System.Runtime.InteropServices;

namespace GagSpeak.Achievements;

public partial class AchievementManager
{
    public void InitializeAchievements()
    {
        // Module Finished
        #region ORDERS MODULE
        var orderComponent = new AchievementComponent();
        orderComponent.AddProgress(Achievements.JustAVolunteer, 1, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Orders Finished");
        orderComponent.AddProgress(Achievements.AsYouCommand, 10, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Orders Finished");
        orderComponent.AddProgress(Achievements.AnythingForMyOwner, 100, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Orders Finished");
        orderComponent.AddProgress(Achievements.GoodDrone, 1000, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Orders Finished");

        orderComponent.AddProgress(Achievements.BadSlut, 1, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Orders Failed");
        orderComponent.AddProgress(Achievements.NeedsTraining, 10, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Orders Failed");
        orderComponent.AddProgress(Achievements.UsefulInOtherWays, 100, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Orders Failed");

        orderComponent.AddProgress(Achievements.NewSlaveOwner, 1, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Orders Created");
        orderComponent.AddProgress(Achievements.TaskManager, 10, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Orders Created");
        orderComponent.AddProgress(Achievements.MaidMaster, 100, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Orders Created");
        orderComponent.AddProgress(Achievements.QueenOfDrones, 1000, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Orders Created");

        SaveData.Components[AchievementModuleKind.Orders] = orderComponent;
        #endregion ORDERS MODULE

        // Module Finished
        #region GAG MODULE
        var gagComponent = new AchievementComponent();
        gagComponent.AddProgress(Achievements.SelfApplied, 1, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Gags Self-Applied");

        gagComponent.AddProgress(Achievements.SilenceSlut, 1, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Gags to Kinksters", "Applied");
        gagComponent.AddProgress(Achievements.WatchYourTongue, 10, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Gags to Kinksters", "Applied");
        gagComponent.AddProgress(Achievements.TongueTamer, 100, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Gags to Kinksters", "Applied");
        gagComponent.AddProgress(Achievements.KinkyLibrarian, 500, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Gags to Kinksters", "Applied");
        gagComponent.AddProgress(Achievements.OrchestratorOfSilence, 1000, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Gags to Kinksters", "Applied");

        gagComponent.AddProgress(Achievements.SilencedSlut, 1, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "times by Kinksters", "Gagged");
        gagComponent.AddProgress(Achievements.InDeepSilence, 10, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "times by Kinksters", "Gagged");
        gagComponent.AddProgress(Achievements.SilentObsessions, 100, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "times by Kinksters", "Gagged");
        gagComponent.AddProgress(Achievements.GoldenSilence, 500, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "times by Kinksters", "Gagged");
        gagComponent.AddProgress(Achievements.AKinkForDrool, 1000, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "times by Kinksters", "Gagged");
        gagComponent.AddProgress(Achievements.ThePerfectGagSlut, 5000, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "times by Kinksters", "Gagged");

        gagComponent.AddThreshold(Achievements.ShushtainableResource, 3, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Gags Active at Once");

        gagComponent.AddProgress(Achievements.OfVoicelessPleas, 1, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Garbled Messages Sent");
        gagComponent.AddProgress(Achievements.DefianceInSilence, 500, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Garbled Messages Sent");
        gagComponent.AddProgress(Achievements.MuffledResilience, 1000, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Garbled Messages Sent");
        gagComponent.AddProgress(Achievements.TrainedInSubSpeech, 2500, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Garbled Messages Sent");
        gagComponent.AddProgress(Achievements.PublicSpeaker, 1, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Garbled Messages Sent");
        gagComponent.AddProgress(Achievements.FromCriesOfHumility, 1, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Garbled Messages Sent");

        gagComponent.AddDuration(Achievements.WhispersToWhimpers, TimeSpan.FromMinutes(5), DurationTimeUnit.Seconds, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Seconds Gagged", "Spent");
        gagComponent.AddDuration(Achievements.OfMuffledMoans, TimeSpan.FromMinutes(10), DurationTimeUnit.Seconds, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Seconds Gagged", "Spent");
        gagComponent.AddDuration(Achievements.SilentStruggler, TimeSpan.FromMinutes(30), DurationTimeUnit.Minutes, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Minutes Gagged", "Spent");
        gagComponent.AddDuration(Achievements.QuietedCaptive, TimeSpan.FromHours(1), DurationTimeUnit.Minutes, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Minutes Gagged", "Spent");
        gagComponent.AddDuration(Achievements.MessyDrooler, TimeSpan.FromHours(6), DurationTimeUnit.Minutes, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Minutes Gagged", "Spent");
        gagComponent.AddDuration(Achievements.DroolingDiva, TimeSpan.FromHours(12), DurationTimeUnit.Hours, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Hours Gagged", "Spent");
        gagComponent.AddDuration(Achievements.EmbraceOfSilence, TimeSpan.FromDays(1), DurationTimeUnit.Hours, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Hours Gagged", "Spent");
        gagComponent.AddDuration(Achievements.SubjugationToSilence, TimeSpan.FromDays(4), DurationTimeUnit.Hours, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Hours Gagged", "Spent");
        gagComponent.AddDuration(Achievements.SpeechSilverSilenceGolden, TimeSpan.FromDays(7), DurationTimeUnit.Hours, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Hours Gagged", "Spent");
        gagComponent.AddDuration(Achievements.TheKinkyLegend, TimeSpan.FromDays(14), DurationTimeUnit.Hours, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Hours Gagged", "Spent");

        gagComponent.AddConditionalProgress(Achievements.SilentButDeadly, 10,
            () => _playerData.AppearanceData?.GagSlots.Any(x => x.GagType.ToGagType() != GagType.None) ?? false, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Roulettes Completed");

        gagComponent.AddTimedProgress(Achievements.ATrueGagSlut, 10, TimeSpan.FromHours(1), (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Gags Received In Hour");

        gagComponent.AddProgress(Achievements.GagReflex, 1, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Gag Reflexes Experienced");

        gagComponent.AddConditional(Achievements.QuietNowDear, () =>
        {
            bool targetIsGagged = false;
            if (_pairManager.GetVisiblePairGameObjects().Any(x => x.GameObjectId == _frameworkUtils.TargetObjectId))
            {
                var targetPair = _pairManager.DirectPairs.FirstOrDefault(x => x.VisiblePairGameObject?.GameObjectId == _frameworkUtils.TargetObjectId);
                if (targetPair != null)
                {
                    targetIsGagged = targetPair.LastReceivedAppearanceData?.GagSlots.Any(x => x.GagType.ToGagType() != GagType.None) ?? false;
                }
            }
            return targetIsGagged;
        }, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Pairs Hushed");

        gagComponent.AddConditional(Achievements.SilenceOfShame, () => _playerData.IsPlayerGagged, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Kinksters", "Hushed by");

        gagComponent.AddConditionalProgress(Achievements.YourFavoriteNurse, 20,
            () => _playerData.AppearanceData?.GagSlots.Any(x => x.GagType.ToGagType() == GagType.MedicalMask) ?? false, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Patients Serviced", reqBeginAndFinish: false);

        gagComponent.AddConditionalProgress(Achievements.SayMmmph, 1, () => _playerData.IsPlayerGagged, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Photos Taken");

        SaveData.Components[AchievementModuleKind.Gags] = gagComponent;
        #endregion GAG MODULE

        #region WARDROBE MODULE
        var wardrobeComponent = new AchievementComponent();

        wardrobeComponent.AddProgress(Achievements.FirstTiemers, 1, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Restraints Applied");
        wardrobeComponent.AddProgress(Achievements.Cuffed19, 19, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Cuffs Applied");
        wardrobeComponent.AddProgress(Achievements.TheRescuer, 100, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Restraints Unlocked");
        wardrobeComponent.AddProgress(Achievements.SelfBondageEnthusiast, 100, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Restraints Applied");
        wardrobeComponent.AddProgress(Achievements.DiDEnthusiast, 100, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Restraints Applied");

        wardrobeComponent.AddConditionalThreshold(Achievements.CrowdPleaser, 15,
            () => _clientConfigs.GetActiveSetIdx() != -1, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "People Nearby");
        wardrobeComponent.AddConditionalThreshold(Achievements.Humiliation, 5,
            () => _clientConfigs.GetActiveSetIdx() != -1, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "GagSpeak Pairs Nearby");

        wardrobeComponent.AddTimedProgress(Achievements.BondageBunny, 5, TimeSpan.FromHours(2), (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Restraints Received In 2 Hours");

        wardrobeComponent.AddProgress(Achievements.ToDyeFor, 5, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Restraints Dyed");
        wardrobeComponent.AddProgress(Achievements.DyeAnotherDay, 10, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Restraints Dyed");
        wardrobeComponent.AddProgress(Achievements.DyeHard, 15, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Restraints Dyed");

        wardrobeComponent.AddDuration(Achievements.RiggersFirstSession, TimeSpan.FromMinutes(30), DurationTimeUnit.Minutes, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Minutes");
        wardrobeComponent.AddDuration(Achievements.MyLittlePlaything, TimeSpan.FromHours(1), DurationTimeUnit.Minutes, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Minutes");
        wardrobeComponent.AddDuration(Achievements.SuitsYouBitch, TimeSpan.FromHours(6), DurationTimeUnit.Hours, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Hours");
        wardrobeComponent.AddDuration(Achievements.TiesThatBind, TimeSpan.FromDays(1), DurationTimeUnit.Hours, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Hours");
        wardrobeComponent.AddDuration(Achievements.SlaveTrainer, TimeSpan.FromDays(7), DurationTimeUnit.Days, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Days");
        wardrobeComponent.AddDuration(Achievements.CeremonyOfEternalBondage, TimeSpan.FromDays(30), DurationTimeUnit.Days, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Days");

        wardrobeComponent.AddDuration(Achievements.FirstTimeBondage, TimeSpan.FromMinutes(30), DurationTimeUnit.Minutes, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Minutes locked up", "Spent");
        wardrobeComponent.AddDuration(Achievements.AmateurBondage, TimeSpan.FromHours(1), DurationTimeUnit.Minutes, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Minutes locked up", "Spent");
        wardrobeComponent.AddDuration(Achievements.ComfortRestraint, TimeSpan.FromHours(6), DurationTimeUnit.Hours, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Hours locked up", "Spent");
        wardrobeComponent.AddDuration(Achievements.YourBondageMaid, TimeSpan.FromDays(1), DurationTimeUnit.Hours, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Hours locked up", "Spent");
        wardrobeComponent.AddDuration(Achievements.YourRubberMaid, TimeSpan.FromDays(4), DurationTimeUnit.Hours, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Hours locked up", "Spent");
        wardrobeComponent.AddDuration(Achievements.TrainedBondageSlave, TimeSpan.FromDays(7), DurationTimeUnit.Days, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Days locked up", "Spent");
        wardrobeComponent.AddDuration(Achievements.YourRubberSlut, TimeSpan.FromDays(4), DurationTimeUnit.Days, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Days locked up", "Spent");
        wardrobeComponent.AddDuration(Achievements.ATrueBondageSlave, TimeSpan.FromDays(30), DurationTimeUnit.Days, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Days locked up", "Spent");

        wardrobeComponent.AddConditional(Achievements.KinkyExplorer, () => _clientConfigs.GagspeakConfig.CursedDungeonLoot, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Cursed Runs Started");
        wardrobeComponent.AddProgress(Achievements.TemptingFatesTreasure, 1, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Cursed Loot Discovered");
        wardrobeComponent.AddConditionalProgress(Achievements.BadEndSeeker, 25,
            () => _clientConfigs.CursedLootConfig.CursedLootStorage.LockChance <= 25, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Cursed Loot Discovered", reqBeginAndFinish: false);
        wardrobeComponent.AddConditionalProgress(Achievements.EverCursed, 100,
            () => _clientConfigs.CursedLootConfig.CursedLootStorage.LockChance <= 25, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Cursed Loot Discovered", reqBeginAndFinish: false);

        wardrobeComponent.AddConditionalProgress(Achievements.HealSlut, 1,
            () => _playerData.IsPlayerGagged || _clientConfigs.GetActiveSetIdx() != -1 || _vibeService.ConnectedToyActive, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Duties Completed");

        wardrobeComponent.AddConditionalProgress(Achievements.BondagePalace, 1, () => _clientConfigs.GetActiveSetIdx() != -1, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "FloorSets Cleared");
        wardrobeComponent.AddConditionalProgress(Achievements.HornyOnHigh, 1, () => _clientConfigs.GetActiveSetIdx() != -1, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "FloorSets Cleared");
        wardrobeComponent.AddConditionalProgress(Achievements.EurekaWhorethos, 1, () => _clientConfigs.GetActiveSetIdx() != -1, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "FloorSets Cleared");
        wardrobeComponent.AddConditionalProgress(Achievements.MyKinkRunsDeep, 1, () =>
        {
            var activeSet = _clientConfigs.GetActiveSet();
            if (activeSet is null) return false;
            return activeSet.PropertiesEnabledForUser(activeSet.EnabledBy);
        }, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "FloorSets Cleared");
        wardrobeComponent.AddConditionalProgress(Achievements.MyKinksRunDeeper, 1, () =>
        {
            var activeSet = _clientConfigs.GetActiveSet();
            if (activeSet is null) return false;
            return activeSet.PropertiesEnabledForUser(activeSet.EnabledBy);
        }, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "FloorSets Cleared");

        wardrobeComponent.AddConditionalProgress(Achievements.TrialOfFocus, 1, () =>
        {
            if (_frameworkUtils.PlayerLevel < 90) return false;
            var activeSet = _clientConfigs.GetActiveSet();
            if (activeSet is null) return false;

            if (activeSet.SetTraits.TryGetValue(activeSet.EnabledBy, out var prop))
                return prop.StimulationLevel is not StimulationLevel.None;

            return false;
        }, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Hardcore Trials Cleared");
        wardrobeComponent.AddConditionalProgress(Achievements.TrialOfDexterity, 1, () =>
        {
            if (_frameworkUtils.PlayerLevel < 90) return false;
            var activeSet = _clientConfigs.GetActiveSet();
            if (activeSet is null) return false;

            if (activeSet.SetTraits.TryGetValue(activeSet.EnabledBy, out var prop))
                return prop.ArmsRestrained || prop.LegsRestrained;

            return false;
        }, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Hardcore Trials Cleared");
        wardrobeComponent.AddConditionalProgress(Achievements.TrialOfTheBlind, 1, () =>
        {
            if (_frameworkUtils.PlayerLevel < 90) return false;
            // get the set and make sure stimulation is enabled for the person who enabled it.
            var activeSet = _clientConfigs.GetActiveSet();
            if (activeSet is null) return false;

            if (activeSet.SetTraits.TryGetValue(activeSet.EnabledBy, out var prop))
                return prop.Blindfolded;

            return false;
        }, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Hardcore Trials Cleared");

        // While actively moving, incorrectly guess a restraint lock while gagged (Secret)
        wardrobeComponent.AddConditional(Achievements.RunningGag, () =>
        {
            unsafe
            {
                var gameControl = FFXIVClientStructs.FFXIV.Client.Game.Control.Control.Instance();
                var movementByte = Marshal.ReadByte((nint)gameControl, 24131);
                var movementDetection = AgentMap.Instance();
                // do a marshal read from this byte offset if it doesnt return proper value.
                var result = movementDetection->IsPlayerMoving;
                StaticLogger.Logger.LogInformation("IsPlayerMoving Result: " + result +" || IsWalking Byte: "+movementByte);
                return _playerData.IsPlayerGagged && _clientConfigs.GetActiveSetIdx() == -1 && result == 1 && movementByte == 0;
            }
        }, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Funny Conditions Met");

        // Check this in the action function handler
        wardrobeComponent.AddProgress(Achievements.AuctionedOff, 1, (id, name) => WasCompleted(id, name).ConfigureAwait(false), prefix: "Auctioned Off", suffix: "Times");

        // Check this in the action function handler
        wardrobeComponent.AddConditional(Achievements.SoldSlave,
            () => _clientConfigs.GetActiveSetIdx() != -1, (id, name) => WasCompleted(id, name).ConfigureAwait(false), prefix: "Sold off in Bondage ", suffix: "Times");

        // Bondodge - Within 2 seconds of having a restraint set applied to you, remove it from yourself (might want to add a duration conditional but idk?)
        wardrobeComponent.AddTimeLimitedConditional(Achievements.Bondodge,
            TimeSpan.FromSeconds(2), () => _clientConfigs.GetActiveSetIdx() != -1, DurationTimeUnit.Seconds, (id, name) => WasCompleted(id, name).ConfigureAwait(false));

        SaveData.Components[AchievementModuleKind.Wardrobe] = wardrobeComponent;
        #endregion WARDROBE MODULE

        // Module Finished
        #region PUPPETEER MODULE
        var puppeteerComponent = new AchievementComponent();
        // (can work both ways)
        puppeteerComponent.AddProgress(Achievements.AnObedientPet, 1, (id, name) => WasCompleted(id, name).ConfigureAwait(false), prefix: "Recieved", suffix: "Sit Orders");

        puppeteerComponent.AddProgress(Achievements.ControlMyBody, 1, (id, name) => WasCompleted(id, name).ConfigureAwait(false), prefix: "Granted", suffix: "Pairs Access");
        puppeteerComponent.AddProgress(Achievements.CompleteDevotion, 1, (id, name) => WasCompleted(id, name).ConfigureAwait(false), prefix: "Granted", suffix: "Pairs Access");

        puppeteerComponent.AddTimedProgress(Achievements.MasterOfPuppets, 10, TimeSpan.FromHours(1), (id, name) => WasCompleted(id, name).ConfigureAwait(false), prefix: "Gave", suffix: "Within the last Hour");

        puppeteerComponent.AddProgress(Achievements.KissMyHeels, 50, (id, name) => WasCompleted(id, name).ConfigureAwait(false), prefix: "Ordered", suffix: "Grovels");

        puppeteerComponent.AddProgress(Achievements.Ashamed, 5, (id, name) => WasCompleted(id, name).ConfigureAwait(false), prefix: "Ordered to sulk", suffix: "Times");

        puppeteerComponent.AddProgress(Achievements.HouseServant, 10, (id, name) => WasCompleted(id, name).ConfigureAwait(false), prefix: "Ordered to sweep", suffix: "Times");

        puppeteerComponent.AddProgress(Achievements.AMaestroOfMyProperty, 10, (id, name) => WasCompleted(id, name).ConfigureAwait(false), prefix: "Ordered", suffix: "Dances");

        puppeteerComponent.AddProgress(Achievements.OrchestratorsApprentice, 10, (id, name) => WasCompleted(id, name).ConfigureAwait(false), prefix: "Gave", suffix: "Orders to Kinksters");
        puppeteerComponent.AddProgress(Achievements.NoStringsAttached, 25, (id, name) => WasCompleted(id, name).ConfigureAwait(false), prefix: "Gave", suffix: "Orders to Kinksters");
        puppeteerComponent.AddProgress(Achievements.PuppetMaster, 50, (id, name) => WasCompleted(id, name).ConfigureAwait(false), prefix: "Gave", suffix: "Orders to Kinksters");
        puppeteerComponent.AddProgress(Achievements.MasterOfManipulation, 100, (id, name) => WasCompleted(id, name).ConfigureAwait(false), prefix: "Gave", suffix: "Orders to Kinksters");
        puppeteerComponent.AddProgress(Achievements.TheGrandConductor, 250, (id, name) => WasCompleted(id, name).ConfigureAwait(false), prefix: "Gave", suffix: "Orders to Kinksters");
        puppeteerComponent.AddProgress(Achievements.MaestroOfStrings, 500, (id, name) => WasCompleted(id, name).ConfigureAwait(false), prefix: "Gave", suffix: "Orders to Kinksters");
        puppeteerComponent.AddProgress(Achievements.OfGrandiousSymphony, 1000, (id, name) => WasCompleted(id, name).ConfigureAwait(false), prefix: "Gave", suffix: "Orders to Kinksters");
        puppeteerComponent.AddProgress(Achievements.SovereignMaestro, 2500, (id, name) => WasCompleted(id, name).ConfigureAwait(false), prefix: "Gave", suffix: "Orders to Kinksters");
        puppeteerComponent.AddProgress(Achievements.OrchestratorOfMinds, 5000, (id, name) => WasCompleted(id, name).ConfigureAwait(false), prefix: "Gave", suffix: "Orders to Kinksters");

        puppeteerComponent.AddProgress(Achievements.WillingPuppet, 10, (id, name) => WasCompleted(id, name).ConfigureAwait(false), prefix: "Puppeteered", suffix: "Times");
        puppeteerComponent.AddProgress(Achievements.AtYourCommand, 25, (id, name) => WasCompleted(id, name).ConfigureAwait(false), prefix: "Puppeteered", suffix: "Times");
        puppeteerComponent.AddProgress(Achievements.YourMarionette, 50, (id, name) => WasCompleted(id, name).ConfigureAwait(false), prefix: "Puppeteered", suffix: "Times");
        puppeteerComponent.AddProgress(Achievements.TheInstrument, 100, (id, name) => WasCompleted(id, name).ConfigureAwait(false), prefix: "Puppeteered", suffix: "Times");
        puppeteerComponent.AddProgress(Achievements.AMannequinsMadness, 250, (id, name) => WasCompleted(id, name).ConfigureAwait(false), prefix: "Puppeteered", suffix: "Times");
        puppeteerComponent.AddProgress(Achievements.DevotedDoll, 500, (id, name) => WasCompleted(id, name).ConfigureAwait(false), prefix: "Puppeteered", suffix: "Times");
        puppeteerComponent.AddProgress(Achievements.EnthralledDoll, 1000, (id, name) => WasCompleted(id, name).ConfigureAwait(false), prefix: "Puppeteered", suffix: "Times");
        puppeteerComponent.AddProgress(Achievements.ObedientDoll, 1750, (id, name) => WasCompleted(id, name).ConfigureAwait(false), prefix: "Puppeteered", suffix: "Times");
        puppeteerComponent.AddProgress(Achievements.ServiceDoll, 2500, (id, name) => WasCompleted(id, name).ConfigureAwait(false), prefix: "Puppeteered", suffix: "Times");
        puppeteerComponent.AddProgress(Achievements.MastersPlaything, 5000, (id, name) => WasCompleted(id, name).ConfigureAwait(false), prefix: "Puppeteered", suffix: "Times");
        puppeteerComponent.AddProgress(Achievements.MistressesPlaything, 5000, (id, name) => WasCompleted(id, name).ConfigureAwait(false), prefix: "Puppeteered", suffix: "Times");
        puppeteerComponent.AddProgress(Achievements.ThePerfectDoll, 10000, (id, name) => WasCompleted(id, name).ConfigureAwait(false), prefix: "Puppeteered", suffix: "Times");

        SaveData.Components[AchievementModuleKind.Puppeteer] = puppeteerComponent;
        #endregion PUPPETEER MODULE

        // Module Finished
        #region TOYBOX MODULE
        var toyboxComponent = new AchievementComponent();
        toyboxComponent.AddProgress(Achievements.MyPleasantriesForAll, 1, (id, name) => WasCompleted(id, name).ConfigureAwait(false), prefix: "Published", suffix: "Patterns");
        toyboxComponent.AddProgress(Achievements.DeviousComposer, 10, (id, name) => WasCompleted(id, name).ConfigureAwait(false), prefix: "Published", suffix: "Patterns");

        toyboxComponent.AddProgress(Achievements.TasteOfTemptation, 1, (id, name) => WasCompleted(id, name).ConfigureAwait(false), prefix: "Downloaded", suffix: "Patterns");
        toyboxComponent.AddProgress(Achievements.SeekerOfSensations, 10, (id, name) => WasCompleted(id, name).ConfigureAwait(false), prefix: "Downloaded", suffix: "Patterns");
        toyboxComponent.AddProgress(Achievements.CravingPleasure, 30, (id, name) => WasCompleted(id, name).ConfigureAwait(false), prefix: "Downloaded", suffix: "Patterns");

        toyboxComponent.AddProgress(Achievements.GoodVibes, 1, (id, name) => WasCompleted(id, name).ConfigureAwait(false), prefix: "Liked", suffix: "Patterns");
        toyboxComponent.AddProgress(Achievements.DelightfulPleasures, 10, (id, name) => WasCompleted(id, name).ConfigureAwait(false), prefix: "Liked", suffix: "Patterns");
        toyboxComponent.AddProgress(Achievements.PatternLover, 25, (id, name) => WasCompleted(id, name).ConfigureAwait(false), prefix: "Liked", suffix: "Patterns");
        toyboxComponent.AddProgress(Achievements.SensualConnoisseur, 50, (id, name) => WasCompleted(id, name).ConfigureAwait(false), prefix: "Liked", suffix: "Patterns");
        toyboxComponent.AddProgress(Achievements.PassionateAdmirer, 100, (id, name) => WasCompleted(id, name).ConfigureAwait(false), prefix: "Liked", suffix: "Patterns");

        toyboxComponent.AddDuration(Achievements.ALittleTease, TimeSpan.FromSeconds(20), DurationTimeUnit.Seconds, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Seconds", "Vibrated for");
        toyboxComponent.AddDuration(Achievements.ShortButSweet, TimeSpan.FromMinutes(1), DurationTimeUnit.Seconds, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Seconds", "Vibrated for");
        toyboxComponent.AddDuration(Achievements.TemptingRythms, TimeSpan.FromMinutes(2), DurationTimeUnit.Seconds, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Seconds", "Vibrated for");
        toyboxComponent.AddDuration(Achievements.MyBuildingDesire, TimeSpan.FromMinutes(5), DurationTimeUnit.Minutes, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Minutes", "Vibrated for");
        toyboxComponent.AddDuration(Achievements.WithWavesOfSensation, TimeSpan.FromMinutes(10), DurationTimeUnit.Minutes, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Minutes", "Vibrated for");
        toyboxComponent.AddDuration(Achievements.WithHeightenedSensations, TimeSpan.FromMinutes(15), DurationTimeUnit.Minutes, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Minutes", "Vibrated for");
        toyboxComponent.AddDuration(Achievements.MusicalMoaner, TimeSpan.FromMinutes(20), DurationTimeUnit.Minutes, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Minutes", "Vibrated for");
        toyboxComponent.AddDuration(Achievements.StimulatingExperiences, TimeSpan.FromMinutes(30), DurationTimeUnit.Minutes, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Minutes", "Vibrated for");
        toyboxComponent.AddDuration(Achievements.EnduranceKing, TimeSpan.FromHours(1), DurationTimeUnit.Minutes, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Minutes", "Vibrated for");
        toyboxComponent.AddDuration(Achievements.EnduranceQueen, TimeSpan.FromHours(1), DurationTimeUnit.Minutes, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Minutes", "Vibrated for");

        toyboxComponent.AddConditional(Achievements.CollectorOfSinfulTreasures, () =>
        { return (_playerData.GlobalPerms?.HasValidShareCode() ?? false) || _vibeService.DeviceHandler.AnyDeviceConnected; }, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Devices Connected");

        toyboxComponent.AddRequiredTimeConditional(Achievements.MotivationForRestoration, TimeSpan.FromMinutes(30),
            () => _clientConfigs.ActivePatternGuid() != Guid.Empty, DurationTimeUnit.Minutes, (id, name) => WasCompleted(id, name).ConfigureAwait(false), suffix: " Vibrated in Diadem");

        toyboxComponent.AddConditional(Achievements.KinkyGambler,
            () => _clientConfigs.ActiveSocialTriggers.Count() > 0, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "DeathRolls Gambled");

        toyboxComponent.AddProgress(Achievements.SubtleReminders, 10, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Triggers Fired");
        toyboxComponent.AddProgress(Achievements.LostInTheMoment, 100, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Triggers Fired");
        toyboxComponent.AddProgress(Achievements.TriggerHappy, 1000, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Triggers Fired");

        toyboxComponent.AddProgress(Achievements.HornyMornings, 1, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Alarms Went Off");

        SaveData.Components[AchievementModuleKind.Toybox] = toyboxComponent;
        #endregion TOYBOX MODULE

        #region HARDCORE MODULE
        var hardcoreComponent = new AchievementComponent();
        hardcoreComponent.AddProgress(Achievements.AllTheCollarsOfTheRainbow, 20, (id, name) => WasCompleted(id, name).ConfigureAwait(false), prefix: "Forced", suffix: "Pairs To Follow You");

        hardcoreComponent.AddConditionalProgress(Achievements.UCanTieThis, 1,
            () => _playerData.GlobalPerms?.IsFollowing() ?? false, (id, name) => WasCompleted(id, name).ConfigureAwait(false), prefix: "Completed", suffix: "Duties in ForcedFollow.");

        // Forced follow achievements
        hardcoreComponent.AddDuration(Achievements.ForcedFollow, TimeSpan.FromMinutes(1), DurationTimeUnit.Seconds, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Seconds", "Leashed a Kinkster for");
        hardcoreComponent.AddDuration(Achievements.ForcedWalkies, TimeSpan.FromMinutes(5), DurationTimeUnit.Seconds, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Seconds", "Leashed a Kinkster for");

        // Time for Walkies achievements
        hardcoreComponent.AddRequiredTimeConditional(Achievements.TimeForWalkies, TimeSpan.FromMinutes(1), () => _playerData.GlobalPerms?.IsFollowing() ?? false, DurationTimeUnit.Seconds, (id, name) => WasCompleted(id, name).ConfigureAwait(false));
        hardcoreComponent.AddRequiredTimeConditional(Achievements.GettingStepsIn, TimeSpan.FromMinutes(5), () => _playerData.GlobalPerms?.IsFollowing() ?? false, DurationTimeUnit.Minutes, (id, name) => WasCompleted(id, name).ConfigureAwait(false));
        hardcoreComponent.AddRequiredTimeConditional(Achievements.WalkiesLover, TimeSpan.FromMinutes(10), () => _playerData.GlobalPerms?.IsFollowing() ?? false, DurationTimeUnit.Minutes, (id, name) => WasCompleted(id, name).ConfigureAwait(false));

        //Part of the Furniture - Be forced to sit for 1 hour or more
        hardcoreComponent.AddRequiredTimeConditional(Achievements.LivingFurniture, TimeSpan.FromHours(1), () => _playerData.GlobalPerms?.IsSitting() ?? false, DurationTimeUnit.Minutes, (id, name) => WasCompleted(id, name).ConfigureAwait(false), suffix: "Forced to Sit");

        hardcoreComponent.AddRequiredTimeConditional(Achievements.WalkOfShame, TimeSpan.FromMinutes(5),
            () =>
            {
                if (_clientConfigs.GetActiveSetIdx() != -1 && (_playerData.GlobalPerms?.IsBlindfolded() ?? false) && (_playerData.GlobalPerms?.IsFollowing() ?? false))
                    if (_frameworkUtils.IsInMainCity)
                        return true;
                return false;
            }, DurationTimeUnit.Minutes, (id, name) => WasCompleted(id, name).ConfigureAwait(false), prefix: "Walked for", suffix: "In a Major City");

        hardcoreComponent.AddConditional(Achievements.BlindLeadingTheBlind,
            () =>
            {
                if (_playerData.GlobalPerms?.IsBlindfolded() ?? false)
                    if (_pairManager.DirectPairs.Any(x => x.UserPairGlobalPerms.IsFollowing() && x.UserPairGlobalPerms.IsBlindfolded()))
                        return true;
                return false;
            }, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Blind Pairs Led");

        hardcoreComponent.AddConditional(Achievements.WhatAView, () => (_playerData.GlobalPerms?.IsBlindfolded() ?? false), (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Blind Lookouts Performed");

        hardcoreComponent.AddRequiredTimeConditional(Achievements.WhoNeedsToSee, TimeSpan.FromHours(3), () => (_playerData.GlobalPerms?.IsBlindfolded() ?? false), DurationTimeUnit.Minutes, (id, name) => WasCompleted(id, name).ConfigureAwait(false));

        hardcoreComponent.AddRequiredTimeConditional(Achievements.OfDomesticDiscipline, TimeSpan.FromMinutes(30), () => (_playerData.GlobalPerms?.IsStaying() ?? false), DurationTimeUnit.Minutes, (id, name) => WasCompleted(id, name).ConfigureAwait(false));
        hardcoreComponent.AddRequiredTimeConditional(Achievements.HomeboundSubmission, TimeSpan.FromHours(1), () => (_playerData.GlobalPerms?.IsStaying() ?? false), DurationTimeUnit.Minutes, (id, name) => WasCompleted(id, name).ConfigureAwait(false));
        hardcoreComponent.AddRequiredTimeConditional(Achievements.PerfectHousePet, TimeSpan.FromDays(1), () => (_playerData.GlobalPerms?.IsStaying() ?? false), DurationTimeUnit.Hours, (id, name) => WasCompleted(id, name).ConfigureAwait(false));

        // Shock-related achievements - Give out shocks
        hardcoreComponent.AddProgress(Achievements.IndulgingSparks, 10, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Shocks Sent");
        hardcoreComponent.AddProgress(Achievements.ShockingTemptations, 100, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Shocks Sent");
        hardcoreComponent.AddProgress(Achievements.TheCrazeOfShockies, 1000, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Shocks Sent");
        hardcoreComponent.AddProgress(Achievements.WickedThunder, 10000, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Shocks Sent");
        hardcoreComponent.AddProgress(Achievements.ElectropeHasNoLimits, 25000, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Shocks Sent");

        // Shock-related achievements - Get shocked
        hardcoreComponent.AddProgress(Achievements.ElectrifyingPleasure, 10, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Shocks Received");
        hardcoreComponent.AddProgress(Achievements.ShockingExperience, 100, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Shocks Received");
        hardcoreComponent.AddProgress(Achievements.WiredForObedience, 1000, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Shocks Received");
        hardcoreComponent.AddProgress(Achievements.ShockAddiction, 10000, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Shocks Received");
        hardcoreComponent.AddProgress(Achievements.SlaveToTheShock, 25000, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Shocks Received");
        hardcoreComponent.AddProgress(Achievements.ShockSlut, 50000, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Shocks Received");

        // Tamed Brat - Shock collar beep or vibrate 10 times without a follow-up shock (Look into this later)
        // AddDuration(HardcoreLabels.TamedBrat, "Shock collar beep or vibrate 10 times without a follow-up shock for another few minutes.", TimeSpan.FromMinutes(2));
        SaveData.Components[AchievementModuleKind.Hardcore] = hardcoreComponent;
        #endregion HARDCORE MODULE

        #region REMOTES MODULE
        var remoteComponent = new AchievementComponent();
        remoteComponent.AddProgress(Achievements.JustVibing, 1, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Remotes Opened");

        // TODO: Make this turning down someone else's once its implemented.
        // (on second thought this could introduce lots of issues so maybe not? Look into later idk, for now its dormant.)
        remoteComponent.AddProgress(Achievements.DontKillMyVibe, 1, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Vibes Killed");

        remoteComponent.AddProgress(Achievements.VibingWithFriends, 1, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Rooms Joined");
        SaveData.Components[AchievementModuleKind.Remotes] = remoteComponent;
        #endregion REMOTES MODULE

        #region GENERIC MODULE
        var genericComponent = new AchievementComponent();
        genericComponent.AddProgress(Achievements.TutorialComplete, 1, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Tutorial Completed");

        genericComponent.AddConditional(Achievements.KinkyNovice, () => _pairManager.DirectPairs.Count > 0, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Pair Added");

        genericComponent.AddProgress(Achievements.TheCollector, 20, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Pairs Added");

        genericComponent.AddProgress(Achievements.BoundaryRespecter, 1, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Presets Applied");

        genericComponent.AddProgress(Achievements.HelloKinkyWorld, 1, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Global Messages Sent");

        genericComponent.AddProgress(Achievements.KnowsMyLimits, 1, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Safewords Used");

        genericComponent.AddConditionalProgress(Achievements.WarriorOfLewd, 1,
            () => _playerData.IsPlayerGagged && _clientConfigs.GetActiveSetIdx() != -1, (id, name) => WasCompleted(id, name).ConfigureAwait(false), suffix: "Cutscenes Watched Bound & Gagged");

        genericComponent.AddConditional(Achievements.EscapingIsNotEasy, () => _clientConfigs.GetActiveSetIdx() != -1, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Escape Attempts Made");

        genericComponent.AddConditional(Achievements.ICantBelieveYouveDoneThis, () => _clientConfigs.GetActiveSetIdx() != -1, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Slaps Received");

        genericComponent.AddConditionalProgress(Achievements.EscapedPatient, 10, () => _frameworkUtils.ClientState.IsPvP, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Players Slain", "", false);
        genericComponent.AddConditionalProgress(Achievements.BoundToKill, 25, () => _frameworkUtils.ClientState.IsPvP, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Players Slain", "", false);
        genericComponent.AddConditionalProgress(Achievements.TheShackledSlayer, 50, () => _frameworkUtils.ClientState.IsPvP, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Players Slain", "", false);
        genericComponent.AddConditionalProgress(Achievements.DangerousConvict, 100, () => _frameworkUtils.ClientState.IsPvP, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Players Slain", "", false);
        genericComponent.AddConditionalProgress(Achievements.OfUnyieldingForce, 200, () => _frameworkUtils.ClientState.IsPvP, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Players Slain", "", false);
        genericComponent.AddConditionalProgress(Achievements.StimulationOverdrive, 300, () => _frameworkUtils.ClientState.IsPvP, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Players Slain", "", false);
        genericComponent.AddConditionalProgress(Achievements.BoundYetUnbroken, 400, () => _frameworkUtils.ClientState.IsPvP, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Players Slain", "", false);
        genericComponent.AddConditionalProgress(Achievements.ChainsCantHoldMe, 500, () => _frameworkUtils.ClientState.IsPvP, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Players Slain", "", false);

        SaveData.Components[AchievementModuleKind.Generic] = genericComponent;
        #endregion GENERIC MODULE

        #region SECRETS MODULE
        var secretsComponent = new AchievementComponent();
        secretsComponent.AddProgress(Achievements.HiddenInPlainSight, 5, (id, name) => WasCompleted(id, name).ConfigureAwait(false), prefix: "Found", suffix: "Easter Eggs", isSecret: true);

        secretsComponent.AddConditional(Achievements.Experimentalist, () =>
        {
            return _playerData.IsPlayerGagged && _clientConfigs.GetActiveSetIdx() != -1 && _clientConfigs.ActivePatternGuid() != Guid.Empty
            && _clientConfigs.ActiveTriggers.Count() > 0 && _clientConfigs.ActiveAlarmCount > 0 && _vibeService.ConnectedToyActive;
        }, (id, name) => WasCompleted(id, name).ConfigureAwait(false), prefix: "Met", suffix: "Conditions", isSecret: true);

        secretsComponent.AddConditional(Achievements.HelplessDamsel, () =>
        {
            return _playerData.IsPlayerGagged && _clientConfigs.GetActiveSetIdx() != -1 && _vibeService.ConnectedToyActive && _pairManager.DirectPairs.Any(x => x.UserPairOwnUniquePairPerms.InHardcore)
            && (_playerData.GlobalPerms?.IsFollowing() ?? false) || (_playerData.GlobalPerms?.IsSitting() ?? false);
        }, (id, name) => WasCompleted(id, name).ConfigureAwait(false), prefix: "Met", suffix: "Hardcore Conditions", isSecret: true);

        secretsComponent.AddConditional(Achievements.GaggedPleasure, () => _vibeService.ConnectedToyActive && _playerData.IsPlayerGagged, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Pleasure Requirements Met", isSecret: true);
        secretsComponent.AddThreshold(Achievements.BondageClub, 8, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Club Members Gathered", isSecret: true);
        secretsComponent.AddConditional(Achievements.BadEndHostage, () => _clientConfigs.GetActiveSetIdx() != -1 && (_frameworkUtils.ClientState.LocalPlayer?.IsDead ?? false), (id, name) => WasCompleted(id, name).ConfigureAwait(false), prefix: "Encountered", suffix: "Bad Ends", isSecret: true);
        secretsComponent.AddConditionalProgress(Achievements.TourDeBound, 11, () => _clientConfigs.GetActiveSetIdx() != -1, (id, name) => WasCompleted(id, name).ConfigureAwait(false), prefix: "Taken", suffix: "Tours in Bondage", isSecret: true);
        secretsComponent.AddConditionalProgress(Achievements.MuffledProtagonist, 1, () => _playerData.IsPlayerGagged && _playerData.GlobalPerms!.LiveChatGarblerActive, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "MissTypes Made", isSecret: true);
        // The above is currently non functional as i dont have the data to know which chat message type contains these request tasks.

        secretsComponent.AddConditional(Achievements.BoundgeeJumping, () => _clientConfigs.GetActiveSetIdx() != -1, (id, name) => WasCompleted(id, name).ConfigureAwait(false), prefix: "Attempted", suffix: "Dangerous Acts", isSecret: true);
        secretsComponent.AddConditionalProgress(Achievements.KinkyTeacher, 10, () => _clientConfigs.GetActiveSetIdx() != -1, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Thanks Received", reqBeginAndFinish: false, isSecret: true);
        secretsComponent.AddConditionalProgress(Achievements.KinkyProfessor, 50, () => _clientConfigs.GetActiveSetIdx() != -1, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Thanks Received", reqBeginAndFinish: false, isSecret: true);
        secretsComponent.AddConditionalProgress(Achievements.KinkyMentor, 100, () => _clientConfigs.GetActiveSetIdx() != -1, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Thanks Received", reqBeginAndFinish: false, isSecret: true);
        secretsComponent.AddThreshold(Achievements.ExtremeBondageEnjoyer, 10, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Restriction Conditions Satisfied", isSecret: true); 
        secretsComponent.AddConditional(Achievements.WildRide, () =>
        {
            var isRacing = _frameworkUtils.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.ChocoboRacing];
            bool raceEndVisible = false;
            unsafe
            {
                var raceEnded = (AtkUnitBase*)AtkFuckery.GetAddonByName("RaceChocoboResult");
                if (raceEnded != null)
                    raceEndVisible = raceEnded->RootNode->IsVisible();
            };
            return isRacing && raceEndVisible && _clientConfigs.GetActiveSetIdx() != -1;
        }, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Races Won In Unusual Conditions", isSecret: true);

        secretsComponent.AddConditional(Achievements.SlavePresentation, () =>
        {
            return _clientConfigs.GetActiveSetIdx() != -1 && _playerData.IsPlayerGagged;
        }, (id, name) => WasCompleted(id, name).ConfigureAwait(false), "Presentations Given on Stage", isSecret: true);

        // Bound Triad - Win a Triple Triad match against another GagSpeak user (both bound)
        // AddProgress(,SecretLabels.BoundTriad, "Win a Triple Triad match against another GagSpeak user, both bound", 1); <---- Unsure how to atm.

        // My First Collar - Equip a leather choker with your dom's name as creator
        // AddProgress(,SecretLabels.MyFirstCollar, "Equip a leather choker with your dom's name as the creator", 1); // <---- Not ideal to track this as it requires packet sending.

        // Obedient Servant - Complete a custom delivery while restrained
        // AddProgress(,SecretLabels.ObedientServant, "Complete a custom delivery while in a restraint set", 1); <------ Unsure how to do 
        SaveData.Components[AchievementModuleKind.Secrets] = secretsComponent;
        #endregion SECRETS MODULE

    }
}
