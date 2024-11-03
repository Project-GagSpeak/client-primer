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
        orderComponent.AddProgress(1, OrderLabels.JustAVolunteer, "Finish 1 Order", 1, (id, name) => WasCompleted(id, name), "Orders Finished");
        orderComponent.AddProgress(2, OrderLabels.AsYouCommand, "Finish 10 Orders", 10, (id, name) => WasCompleted(id, name), "Orders Finished");
        orderComponent.AddProgress(3, OrderLabels.AnythingForMyOwner, "Finish 100 Orders", 100, (id, name) => WasCompleted(id, name), "Orders Finished");
        orderComponent.AddProgress(4, OrderLabels.GoodDrone, "Finish 1000 Orders", 1000, (id, name) => WasCompleted(id, name), "Orders Finished");

        orderComponent.AddProgress(5, OrderLabels.BadSlut, "Fail 1 Order", 1, (id, name) => WasCompleted(id, name), "Orders Failed");
        orderComponent.AddProgress(6, OrderLabels.NeedsTraining, "Fail 10 Orders", 10, (id, name) => WasCompleted(id, name), "Orders Failed");
        orderComponent.AddProgress(7, OrderLabels.UsefulInOtherWays, "Fail 100 Orders", 100, (id, name) => WasCompleted(id, name), "Orders Failed");

        orderComponent.AddProgress(8, OrderLabels.NewSlaveOwner, "Create 1 Order", 1, (id, name) => WasCompleted(id, name), "Orders Created");
        orderComponent.AddProgress(9, OrderLabels.TaskManager, "Create 10 Orders", 10, (id, name) => WasCompleted(id, name), "Orders Created");
        orderComponent.AddProgress(10, OrderLabels.MaidMaster, "Create 100 Orders", 100, (id, name) => WasCompleted(id, name), "Orders Created");
        orderComponent.AddProgress(11, OrderLabels.QueenOfDrones, "Create 1000 Orders", 1000, (id, name) => WasCompleted(id, name), "Orders Created");

        SaveData.Achievements[AchievementModuleKind.Orders] = orderComponent;
        #endregion ORDERS MODULE

        // Module Finished
        #region GAG MODULE
        var gagComponent = new AchievementComponent();
        gagComponent.AddProgress(12, GagLabels.SelfApplied, "Apply a Gag to Yourself", 1, (id, name) => WasCompleted(id, name), "Gags Self-Applied");

        gagComponent.AddProgress(13, GagLabels.ApplyToPair, "Apply a Gag to another GagSpeak Pair", 1, (id, name) => WasCompleted(id, name), "Gags Applied");
        gagComponent.AddProgress(14, GagLabels.LookingForTheRightFit, "Apply Gags to other GagSpeak Pairs, or have a Gag applied to you 10 times.", 10, (id, name) => WasCompleted(id, name), "Gags Applied");
        gagComponent.AddProgress(15, GagLabels.OralFixation, "Apply Gags to other GagSpeak Pairs, or have a Gag applied to you 100 times.", 100, (id, name) => WasCompleted(id, name), "Gags Applied");
        gagComponent.AddProgress(16, GagLabels.AKinkForDrool, "Apply Gags to other GagSpeak Pairs, or have a Gag applied to you 1000 times.", 1000, (id, name) => WasCompleted(id, name), "Gags Applied");

        gagComponent.AddThreshold(17, GagLabels.ShushtainableResource, "Have all three Gag Slots occupied at once.", 3, (id, name) => WasCompleted(id, name), "Gags Active at Once");

        gagComponent.AddProgress(18, GagLabels.SpeakUpSlut, "Say anything longer than 5 words with LiveChatGarbler on in /say (Please be smart about this)", 1, (id, name) => WasCompleted(id, name), "Garbled Messages Sent");
        gagComponent.AddProgress(19, GagLabels.CantHearYou, "Say anything longer than 5 words with LiveChatGarbler on in /yell (Please be smart about this)", 1,    (id, name) => WasCompleted(id, name), "Garbled Messages Sent");
        gagComponent.AddProgress(20, GagLabels.OneMoreForTheCrowd, "Say anything longer than 5 words with LiveChatGarbler on in /shout (Please be smart about this)", 1, (id, name) => WasCompleted(id, name), "Garbled Messages Sent");

        gagComponent.AddDuration(21, GagLabels.SpeechSilverSilenceGolden, "Wear a gag continuously for 1 week", TimeSpan.FromDays(7), DurationTimeUnit.Hours, (id, name) => WasCompleted(id, name));
        gagComponent.AddDuration(22, GagLabels.TheKinkyLegend, "Wear a gag continuously for 2 weeks", TimeSpan.FromDays(14), DurationTimeUnit.Hours, (id, name) => WasCompleted(id, name));

        gagComponent.AddConditionalProgress(23, GagLabels.SilentButDeadly, "Complete 10 roulettes with a gag equipped", 10,
            () => _playerData.AppearanceData?.GagSlots.Any(x => x.GagType.ToGagType() != GagType.None) ?? false, (id, name) => WasCompleted(id, name), "Roulettes Completed");

        gagComponent.AddTimedProgress(24, GagLabels.ATrueGagSlut, "Be gagged by 10 different people in less than 1 hour", 10, TimeSpan.FromHours(1), (id, name) => WasCompleted(id, name), "Gags Received In Hour");

        gagComponent.AddProgress(25, GagLabels.GagReflex, "Be blown away in a gold saucer GATE with any gag equipped.", 1, (id, name) => WasCompleted(id, name), "Gag Reflexes Experienced");

        gagComponent.AddConditional(26, GagLabels.QuietNowDear, "Use /shush while targeting a gagged player", () =>
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
        }, (id, name) => WasCompleted(id, name), "Pairs Hushed");

        gagComponent.AddConditionalProgress(27, GagLabels.YourFavoriteNurse, "Apply a restraint set or Gag to a GagSpeak pair while you have a Mask Gag Equipped 20 times", 20,
            () => _playerData.AppearanceData?.GagSlots.Any(x => x.GagType.ToGagType() == GagType.MedicalMask) ?? false, (id, name) => WasCompleted(id, name), "Patients Serviced", reqBeginAndFinish: false);

        gagComponent.AddConditionalProgress(28, GagLabels.SayMmmph, "Take a screenshot in /gpose while gagged", 1, () => _playerData.IsPlayerGagged, (id, name) => WasCompleted(id, name), "Photos Taken");

        SaveData.Achievements[AchievementModuleKind.Gags] = gagComponent;
        #endregion GAG MODULE

        #region WARDROBE MODULE
        var wardrobeComponent = new AchievementComponent();

        wardrobeComponent.AddProgress(29, WardrobeLabels.FirstTiemers, "Have a Restraint Set applied for the first time (or apply one to someone else)", 1, (id, name) => WasCompleted(id, name), "Restraints Applied");
        wardrobeComponent.AddProgress(30, WardrobeLabels.Cuffed19, "Get your hands restrained 19 times.", 19, (id, name) => WasCompleted(id, name), "Cuffs Applied");
        wardrobeComponent.AddProgress(31, WardrobeLabels.TheRescuer, "Unlock 100 Restraints from someone other than yourself.", 100, (id, name) => WasCompleted(id, name), "Restraints Unlocked");
        wardrobeComponent.AddProgress(32, WardrobeLabels.SelfBondageEnthusiast, "Apply a restraint to yourself 100 times.", 100, (id, name) => WasCompleted(id, name), "Restraints Applied");
        wardrobeComponent.AddProgress(33, WardrobeLabels.DiDEnthusiast, "Apply a restraint set to someone else 100 times.", 100, (id, name) => WasCompleted(id, name), "Restraints Applied");

        wardrobeComponent.AddConditionalThreshold(34, WardrobeLabels.CrowdPleaser, "Be restrained with 15 or more people around you.", 15,
            () => _clientConfigs.GetActiveSetIdx() != -1, (id, name) => WasCompleted(id, name), "People Nearby");
        wardrobeComponent.AddConditionalThreshold(35, WardrobeLabels.Humiliation, "Be restrained with 5 or more GagSpeak Pairs nearby.", 5,
            () => _clientConfigs.GetActiveSetIdx() != -1, (id, name) => WasCompleted(id, name), "GagSpeak Pairs Nearby");

        wardrobeComponent.AddTimedProgress(36, WardrobeLabels.BondageBunny, "Be restrained by 5 different people in less than 2 hours.", 5, TimeSpan.FromHours(2), (id, name) => WasCompleted(id, name), "Restraints Received In 2 Hours");

        wardrobeComponent.AddProgress(37, WardrobeLabels.ToDyeFor, "Dye a Restraint Set 5 times", 5, (id, name) => WasCompleted(id, name), "Restraints Dyed");
        wardrobeComponent.AddProgress(38, WardrobeLabels.DyeAnotherDay, "Dye a Restraint Set 10 times", 10, (id, name) => WasCompleted(id, name), "Restraints Dyed");
        wardrobeComponent.AddProgress(39, WardrobeLabels.DyeHard, "Dye a Restraint Set 15 times", 15, (id, name) => WasCompleted(id, name), "Restraints Dyed");

        wardrobeComponent.AddDuration(40, WardrobeLabels.RiggersFirstSession, "Lock someone in a Restraint Set for 30 minutes", TimeSpan.FromMinutes(30), DurationTimeUnit.Minutes, (id, name) => WasCompleted(id, name));
        wardrobeComponent.AddDuration(41, WardrobeLabels.MyLittlePlaything, "Lock someone in a Restraint Set for 1 hour", TimeSpan.FromHours(1), DurationTimeUnit.Minutes, (id, name) => WasCompleted(id, name));
        wardrobeComponent.AddDuration(42, WardrobeLabels.SuitsYouBitch, "Lock someone in a Restraint Set for 6 hours", TimeSpan.FromHours(6), DurationTimeUnit.Hours, (id, name) => WasCompleted(id, name));
        wardrobeComponent.AddDuration(43, WardrobeLabels.TiesThatBind, "Lock someone in a Restraint Set for 1 day", TimeSpan.FromDays(1), DurationTimeUnit.Hours, (id, name) => WasCompleted(id, name));
        wardrobeComponent.AddDuration(44, WardrobeLabels.SlaveTraining, "Lock someone in a Restraint Set for 1 week", TimeSpan.FromDays(7), DurationTimeUnit.Days, (id, name) => WasCompleted(id, name));
        wardrobeComponent.AddDuration(45, WardrobeLabels.CeremonyOfEternalBondage, "Lock someone in a Restraint Set for 1 month", TimeSpan.FromDays(30), DurationTimeUnit.Days, (id, name) => WasCompleted(id, name));

        wardrobeComponent.AddDuration(46, WardrobeLabels.FirstTimeBondage, "Endure being locked in a Restraint Set for 30 minutes", TimeSpan.FromMinutes(30), DurationTimeUnit.Minutes, (id, name) => WasCompleted(id, name));
        wardrobeComponent.AddDuration(47, WardrobeLabels.AmateurBondage, "Endure being locked in a Restraint Set for 1 hour", TimeSpan.FromHours(1), DurationTimeUnit.Minutes, (id, name) => WasCompleted(id, name));
        wardrobeComponent.AddDuration(48, WardrobeLabels.ComfortRestraint, "Endure being locked in a Restraint Set for 6 hours", TimeSpan.FromHours(6), DurationTimeUnit.Hours, (id, name) => WasCompleted(id, name));
        wardrobeComponent.AddDuration(49, WardrobeLabels.DayInTheLifeOfABondageSlave, "Endure being locked in a Restraint Set for 1 day", TimeSpan.FromDays(1), DurationTimeUnit.Hours, (id, name) => WasCompleted(id, name));
        wardrobeComponent.AddDuration(50, WardrobeLabels.AWeekInBondage, "Endure being locked in a Restraint Set for 1 week", TimeSpan.FromDays(7), DurationTimeUnit.Days, (id, name) => WasCompleted(id, name));
        wardrobeComponent.AddDuration(51, WardrobeLabels.AMonthInBondage, "Endure being locked in a Restraint Set for 1 month", TimeSpan.FromDays(30), DurationTimeUnit.Days, (id, name) => WasCompleted(id, name));

        wardrobeComponent.AddConditional(52, WardrobeLabels.KinkyExplorer, "Run a Dungeon with Cursed Bondage Loot enabled.", () => _clientConfigs.GagspeakConfig.CursedDungeonLoot, (id, name) => WasCompleted(id, name), "Cursed Runs Started");
        wardrobeComponent.AddProgress(53, WardrobeLabels.TemptingFatesTreasure, "Be Caught in Cursed Bondage Loot for the first time.", 1, (id, name) => WasCompleted(id, name), "Cursed Loot Discovered");
        wardrobeComponent.AddConditionalProgress(54, WardrobeLabels.BadEndSeeker, "Get trapped in Cursed Bondage Loot 25 times. (Chance must be 25% or lower)", 25,
            () => _clientConfigs.CursedLootConfig.CursedLootStorage.LockChance <= 25, (id, name) => WasCompleted(id, name), "Cursed Loot Discovered", reqBeginAndFinish: false);
        wardrobeComponent.AddConditionalProgress(55, WardrobeLabels.EverCursed, "Get trapped in Cursed Bondage Loot 100 times. (Chance must be 25% or lower)", 100,
            () => _clientConfigs.CursedLootConfig.CursedLootStorage.LockChance <= 25, (id, name) => WasCompleted(id, name), "Cursed Loot Discovered", reqBeginAndFinish: false);

        wardrobeComponent.AddConditionalProgress(56, WardrobeLabels.HealSlut, "Complete a duty as a healer while wearing a gag, restraint, or using a vibe.", 1,
            () => _playerData.IsPlayerGagged || _clientConfigs.GetActiveSetIdx() != -1 || _vibeService.ConnectedToyActive, (id, name) => WasCompleted(id, name), "Duties Completed");

        wardrobeComponent.AddConditionalProgress(57, WardrobeLabels.BondagePalace, "Reach Floor 50 or 100 of Palace of the Dead while bound.", 1, () => _clientConfigs.GetActiveSetIdx() != -1, (id, name) => WasCompleted(id, name), "FloorSets Cleared");
        wardrobeComponent.AddConditionalProgress(58, WardrobeLabels.HornyOnHigh, "Reach Floor 30 of Heaven-on-High while bound.", 1, () => _clientConfigs.GetActiveSetIdx() != -1, (id, name) => WasCompleted(id, name), "FloorSets Cleared");
        wardrobeComponent.AddConditionalProgress(59, WardrobeLabels.EurekaWhorethos, "Reach Floor 30 of Eureka Orthos while bound.", 1, () => _clientConfigs.GetActiveSetIdx() != -1, (id, name) => WasCompleted(id, name), "FloorSets Cleared");
        wardrobeComponent.AddConditionalProgress(60, WardrobeLabels.MyKinkRunsDeep, "Complete a deep dungeon with hardcore stimulation or hardcore restraints.", 1, () =>
        {
            var activeSet = _clientConfigs.GetActiveSet();
            if (activeSet is null) return false;
            return activeSet.PropertiesEnabledForUser(activeSet.EnabledBy);
        }, (id, name) => WasCompleted(id, name), "FloorSets Cleared");
        wardrobeComponent.AddConditionalProgress(61, WardrobeLabels.MyKinksRunDeeper, "Solo a deep dungeon with hardcore stimulation or hardcore restraints.", 1, () =>
        {
            var activeSet = _clientConfigs.GetActiveSet();
            if (activeSet is null) return false;
            return activeSet.PropertiesEnabledForUser(activeSet.EnabledBy);
        }, (id, name) => WasCompleted(id, name), "FloorSets Cleared");

        wardrobeComponent.AddConditionalProgress(62, WardrobeLabels.TrialOfFocus, "Complete a trial within 10 levels of max level with stimulation (HardcoreLabels Focus).", 1, () =>
        {
            if (_frameworkUtils.PlayerLevel < 90) return false;
            var activeSet = _clientConfigs.GetActiveSet();
            if (activeSet is null) return false;

            if (activeSet.SetTraits.TryGetValue(activeSet.EnabledBy, out var prop))
                return prop.StimulationLevel is not StimulationLevel.None;

            return false;
        }, (id, name) => WasCompleted(id, name), "Hardcore Trials Cleared");
        wardrobeComponent.AddConditionalProgress(63, WardrobeLabels.TrialOfDexterity, "Complete a trial within 10 levels of max level with arms/legs restrained.", 1, () =>
        {
            if (_frameworkUtils.PlayerLevel < 90) return false;
            var activeSet = _clientConfigs.GetActiveSet();
            if (activeSet is null) return false;

            if (activeSet.SetTraits.TryGetValue(activeSet.EnabledBy, out var prop))
                return prop.ArmsRestrained || prop.LegsRestrained;

            return false;
        }, (id, name) => WasCompleted(id, name), "Hardcore Trials Cleared");
        wardrobeComponent.AddConditionalProgress(64, WardrobeLabels.TrialOfTheBlind, "Complete a trial within 10 levels of max level while blindfolded.", 1, () =>
        {
            if (_frameworkUtils.PlayerLevel < 90) return false;
            // get the set and make sure stimulation is enabled for the person who enabled it.
            var activeSet = _clientConfigs.GetActiveSet();
            if (activeSet is null) return false;

            if (activeSet.SetTraits.TryGetValue(activeSet.EnabledBy, out var prop))
                return prop.Blindfolded;

            return false;
        }, (id, name) => WasCompleted(id, name), "Hardcore Trials Cleared");

        // While actively moving, incorrectly guess a restraint lock while gagged (Secret)
        wardrobeComponent.AddConditional(65, WardrobeLabels.RunningGag, "Incorrectly guess a gag's lock password while unrestrained and running.", () =>
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
        }, (id, name) => WasCompleted(id, name), "Funny Conditions Met");

        // Check this in the action function handler
        wardrobeComponent.AddProgress(66, WardrobeLabels.AuctionedOff, "Have a restraint set enabled by one GagSpeak user be removed by a different GagSpeak user.", 1, (id, name) => WasCompleted(id, name), prefix: "Auctioned Off", suffix: "Times");

        // Check this in the action function handler
        wardrobeComponent.AddConditional(67, WardrobeLabels.SoldSlave, "Have a password-locked restraint set locked by one GagSpeak user be unlocked by another.",
            () => _clientConfigs.GetActiveSetIdx() != -1, (id, name) => WasCompleted(id, name), prefix: "Sold off in Bondage ", suffix: "Times");

        // Bondodge - Within 2 seconds of having a restraint set applied to you, remove it from yourself (might want to add a duration conditional but idk?)
        wardrobeComponent.AddTimeLimitedConditional(68, WardrobeLabels.Bondodge, "Within 2 seconds of having a restraint set applied to you, remove it from yourself",
            TimeSpan.FromSeconds(2), () => _clientConfigs.GetActiveSetIdx() != -1, DurationTimeUnit.Seconds, (id, name) => WasCompleted(id, name));

        SaveData.Achievements[AchievementModuleKind.Wardrobe] = wardrobeComponent;
        #endregion WARDROBE MODULE

        // Module Finished
        #region PUPPETEER MODULE
        var puppeteerComponent = new AchievementComponent();
        // (can work both ways)
        puppeteerComponent.AddProgress(69, PuppeteerLabels.WhoIsAGoodPet, "Be ordered to sit by another pair through Puppeteer.", 1, (id, name) => WasCompleted(id, name), prefix: "Recieved", suffix: "Sit Orders");

        puppeteerComponent.AddProgress(70, PuppeteerLabels.ControlMyBody, "Enable Allow Motions for another pair.", 1, (id, name) => WasCompleted(id, name), prefix: "Granted", suffix: "Pairs Access");
        puppeteerComponent.AddProgress(71, PuppeteerLabels.CompleteDevotion, "Enable All Commands for another pair.", 1, (id, name) => WasCompleted(id, name), prefix: "Granted", suffix: "Pairs Access");

        puppeteerComponent.AddTimedProgress(72, PuppeteerLabels.MasterOfPuppets, "Puppeteer someone 10 times in an hour.", 10, TimeSpan.FromHours(1), (id, name) => WasCompleted(id, name), prefix: "Gave", suffix: "Within the last Hour");

        puppeteerComponent.AddProgress(73, PuppeteerLabels.KissMyHeels, "Order someone to /grovel 50 times using Puppeteer.", 50, (id, name) => WasCompleted(id, name), prefix: "Ordered", suffix: "Grovels");

        puppeteerComponent.AddProgress(74, PuppeteerLabels.Ashamed, "Be forced to /sulk through Puppeteer.", 1, (id, name) => WasCompleted(id, name), prefix: "Forced a Pair to Sulk", suffix: "Times");

        puppeteerComponent.AddProgress(75, PuppeteerLabels.ShowingOff, "Order someone to execute any emote with 'dance' in it 10 times.", 10, (id, name) => WasCompleted(id, name), prefix: "Ordered", suffix: "Dances");

        SaveData.Achievements[AchievementModuleKind.Puppeteer] = puppeteerComponent;
        #endregion PUPPETEER MODULE

        // Module Finished
        #region TOYBOX MODULE
        var toyboxComponent = new AchievementComponent();
        toyboxComponent.AddProgress(76, ToyboxLabels.FunForAll, "Create and publish a pattern for the first time.", 1, (id, name) => WasCompleted(id, name), prefix: "Published", suffix: "Patterns");

        toyboxComponent.AddProgress(77, ToyboxLabels.DeviousComposer, "Publish 10 patterns you have made.", 10, (id, name) => WasCompleted(id, name), prefix: "Published", suffix: "Patterns");

        toyboxComponent.AddProgress(78, ToyboxLabels.CravingPleasure, "Download 30 Patterns from the Pattern Hub", 30, (id, name) => WasCompleted(id, name), prefix: "Downloaded", suffix: "Patterns");

        toyboxComponent.AddProgress(79, ToyboxLabels.PatternLover, "Like 30 Patterns from the Pattern Hub", 30, (id, name) => WasCompleted(id, name), prefix: "Liked", suffix: "Patterns");

        toyboxComponent.AddDuration(80, ToyboxLabels.EnduranceQueen, "Play a pattern for an hour (59m) without pause.", TimeSpan.FromHours(1), DurationTimeUnit.Minutes, (id, name) => WasCompleted(id, name));

        toyboxComponent.AddConditional(81, ToyboxLabels.MyFavoriteToys, "Connect a real device (Intiface / PiShock Device) to GagSpeak.", () =>
        { return (_playerData.GlobalPerms?.HasValidShareCode() ?? false) || _vibeService.DeviceHandler.AnyDeviceConnected; }, (id, name) => WasCompleted(id, name), "Devices Connected");

        toyboxComponent.AddRequiredTimeConditional(82, ToyboxLabels.MotivationForRestoration, "Play a pattern for over 30 minutes in Diadem.", TimeSpan.FromMinutes(30),
            () => _clientConfigs.ActivePatternGuid() != Guid.Empty, DurationTimeUnit.Minutes, (id, name) => WasCompleted(id, name), suffix: " Vibrated in Diadem");

        toyboxComponent.AddConditional(83, ToyboxLabels.KinkyGambler, "Complete a DeathRoll (win or loss) while having a DeathRoll trigger on.",
            () => _clientConfigs.ActiveSocialTriggers.Count() > 0, (id, name) => WasCompleted(id, name), "DeathRolls Gambled");

        toyboxComponent.AddProgress(84, ToyboxLabels.SubtleReminders, "Have 10 Triggers go off.", 10, (id, name) => WasCompleted(id, name), "Triggers Fired");
        toyboxComponent.AddProgress(85, ToyboxLabels.FingerOnTheTrigger, "Have 100 Triggers go off.", 100, (id, name) => WasCompleted(id, name), "Triggers Fired");
        toyboxComponent.AddProgress(86, ToyboxLabels.TriggerHappy, "Have 1000 Triggers go off.", 1000, (id, name) => WasCompleted(id, name), "Triggers Fired");

        toyboxComponent.AddProgress(87, ToyboxLabels.HornyMornings, "Have an alarm go off.", 1, (id, name) => WasCompleted(id, name), "Alarms Went Off");

        toyboxComponent.AddConditionalProgress(88, ToyboxLabels.NothingCanStopMe, "Kill 500 enemies in PvP Frontlines while restrained or vibed.", 500,
            () => _frameworkUtils.ClientState.IsPvP, (id, name) => WasCompleted(id, name), "Players Slain While Bound", reqBeginAndFinish: false);

        SaveData.Achievements[AchievementModuleKind.Toybox] = toyboxComponent;
        #endregion TOYBOX MODULE

        #region HARDCORE MODULE
        var hardcoreComponent = new AchievementComponent();
        hardcoreComponent.AddProgress(89, HardcoreLabels.AllTheCollarsOfTheRainbow, "Force 20 pairs to follow you.", 20, (id, name) => WasCompleted(id, name), prefix: "Forced", suffix: "Pairs To Follow You");

        hardcoreComponent.AddConditionalProgress(90, HardcoreLabels.UCanTieThis, "Be forced to follow someone, throughout a duty.", 1,
            () => _playerData.GlobalPerms?.IsFollowing() ?? false, (id, name) => WasCompleted(id, name), prefix: "Completed", suffix: "Duties in ForcedFollow.");

        // Forced follow achievements
        hardcoreComponent.AddDuration(91, HardcoreLabels.ForcedFollow, "Force someone to follow you for 1 minute.", TimeSpan.FromMinutes(1), DurationTimeUnit.Seconds, (id, name) => WasCompleted(id, name));
        hardcoreComponent.AddDuration(92, HardcoreLabels.ForcedWalkies, "Force someone to follow you for 5 minutes.", TimeSpan.FromMinutes(5), DurationTimeUnit.Seconds, (id, name) => WasCompleted(id, name));

        // Time for Walkies achievements
        hardcoreComponent.AddRequiredTimeConditional(93, HardcoreLabels.TimeForWalkies, "Be forced to follow someone for 1 minute.", TimeSpan.FromMinutes(1),
            () => _playerData.GlobalPerms?.IsFollowing() ?? false, DurationTimeUnit.Seconds, (id, name) => WasCompleted(id, name));
        hardcoreComponent.AddRequiredTimeConditional(94, HardcoreLabels.GettingStepsIn, "Be forced to follow someone for 5 minutes.", TimeSpan.FromMinutes(5),
            () => _playerData.GlobalPerms?.IsFollowing() ?? false, DurationTimeUnit.Minutes, (id, name) => WasCompleted(id, name));
        hardcoreComponent.AddRequiredTimeConditional(95, HardcoreLabels.WalkiesLover, "Be forced to follow someone for 10 minutes.", TimeSpan.FromMinutes(10),
            () => _playerData.GlobalPerms?.IsFollowing() ?? false, DurationTimeUnit.Minutes, (id, name) => WasCompleted(id, name));

        //Part of the Furniture - Be forced to sit for 1 hour or more
        hardcoreComponent.AddRequiredTimeConditional(96, HardcoreLabels.LivingFurniture, "Be forced to sit for 1 hour or more.", TimeSpan.FromHours(1),
            () => _playerData.GlobalPerms?.IsSitting() ?? false, DurationTimeUnit.Minutes, (id, name) => WasCompleted(id, name), suffix: "Forced to Sit");

        hardcoreComponent.AddRequiredTimeConditional(97, HardcoreLabels.WalkOfShame, "Be bound, blindfolded, and leashed in a major city.", TimeSpan.FromMinutes(5),
            () =>
            {
                if (_clientConfigs.GetActiveSetIdx() != -1 && (_playerData.GlobalPerms?.IsBlindfolded() ?? false) && (_playerData.GlobalPerms?.IsFollowing() ?? false))
                    if (_frameworkUtils.IsInMainCity)
                        return true;
                return false;
            }, DurationTimeUnit.Minutes, (id, name) => WasCompleted(id, name), prefix: "Walked for", suffix: "In a Major City");

        hardcoreComponent.AddConditional(98, HardcoreLabels.BlindLeadingTheBlind, "Be blindfolded while having someone follow you blindfolded.",
            () =>
            {
                if (_playerData.GlobalPerms?.IsBlindfolded() ?? false)
                    if (_pairManager.DirectPairs.Any(x => x.UserPairGlobalPerms.IsFollowing() && x.UserPairGlobalPerms.IsBlindfolded()))
                        return true;
                return false;
            }, (id, name) => WasCompleted(id, name), "Blind Pairs Led");

        hardcoreComponent.AddConditional(99, HardcoreLabels.WhatAView, "Use the /lookout emote while wearing a blindfold.",
            () => (_playerData.GlobalPerms?.IsBlindfolded() ?? false), (id, name) => WasCompleted(id, name), "Blind Lookouts Performed");

        hardcoreComponent.AddRequiredTimeConditional(100, HardcoreLabels.WhoNeedsToSee, "Be blindfolded in hardcore mode for 3 hours.", TimeSpan.FromHours(3),
            () => (_playerData.GlobalPerms?.IsBlindfolded() ?? false), DurationTimeUnit.Minutes, (id, name) => WasCompleted(id, name));

        hardcoreComponent.AddRequiredTimeConditional(101, HardcoreLabels.PetTraining, "Be forced to stay in someone's house for 30 minutes.", TimeSpan.FromMinutes(30),
            () => (_playerData.GlobalPerms?.IsStaying() ?? false), DurationTimeUnit.Minutes, (id, name) => WasCompleted(id, name));
        hardcoreComponent.AddRequiredTimeConditional(102, HardcoreLabels.NotGoingAnywhere, "Be forced to stay in someone's house for 1 hour.", TimeSpan.FromHours(1),
            () => (_playerData.GlobalPerms?.IsStaying() ?? false), DurationTimeUnit.Minutes, (id, name) => WasCompleted(id, name));
        hardcoreComponent.AddRequiredTimeConditional(103, HardcoreLabels.HouseTrained, "Be forced to stay in someone's house for 1 day.", TimeSpan.FromDays(1),
            () => (_playerData.GlobalPerms?.IsStaying() ?? false), DurationTimeUnit.Hours, (id, name) => WasCompleted(id, name));

        // Shock-related achievements - Give out shocks
        hardcoreComponent.AddProgress(104, HardcoreLabels.IndulgingSparks, "Give out 10 shocks.", 10, (id, name) => WasCompleted(id, name), "Shocks Sent");
        hardcoreComponent.AddProgress(105, HardcoreLabels.CantGetEnough, "Give out 100 shocks.", 100, (id, name) => WasCompleted(id, name), "Shocks Sent");
        hardcoreComponent.AddProgress(106, HardcoreLabels.VerThunder, "Give out 1000 shocks.", 1000, (id, name) => WasCompleted(id, name), "Shocks Sent");
        hardcoreComponent.AddProgress(107, HardcoreLabels.WickedThunder, "Give out 10,000 shocks.", 10000, (id, name) => WasCompleted(id, name), "Shocks Sent");
        hardcoreComponent.AddProgress(108, HardcoreLabels.ElectropeHasNoLimits, "Give out 25,000 shocks.", 25000, (id, name) => WasCompleted(id, name), "Shocks Sent");

        // Shock-related achievements - Get shocked
        hardcoreComponent.AddProgress(109, HardcoreLabels.ShockAndAwe, "Get shocked 10 times.", 10, (id, name) => WasCompleted(id, name), "Shocks Received");
        hardcoreComponent.AddProgress(110, HardcoreLabels.ShockingExperience, "Get shocked 100 times.", 100, (id, name) => WasCompleted(id, name), "Shocks Received");
        hardcoreComponent.AddProgress(111, HardcoreLabels.ShockolateTasting, "Get shocked 1000 times.", 1000, (id, name) => WasCompleted(id, name), "Shocks Received");
        hardcoreComponent.AddProgress(112, HardcoreLabels.ShockAddiction, "Get shocked 10,000 times.", 10000, (id, name) => WasCompleted(id, name), "Shocks Received");
        hardcoreComponent.AddProgress(113, HardcoreLabels.WarriorOfElectrope, "Get shocked 25,000 times.", 25000, (id, name) => WasCompleted(id, name), "Shocks Received");
        hardcoreComponent.AddProgress(114, HardcoreLabels.ShockSlut, "Get shocked 50,000 times.", 50000, (id, name) => WasCompleted(id, name), "Shocks Received");

        // Tamed Brat - Shock collar beep or vibrate 10 times without a follow-up shock (Look into this later)
        // AddDuration(HardcoreLabels.TamedBrat, "Shock collar beep or vibrate 10 times without a follow-up shock for another few minutes.", TimeSpan.FromMinutes(2));
        SaveData.Achievements[AchievementModuleKind.Hardcore] = hardcoreComponent;
        #endregion HARDCORE MODULE

        #region REMOTES MODULE
        var remoteComponent = new AchievementComponent();
        remoteComponent.AddProgress(115, RemoteLabels.JustVibing, "Use the Remote Control feature for the first time.", 1, (id, name) => WasCompleted(id, name), "Remotes Opened");

        // TODO: Make this turning down someone else's once its implemented.
        // (on second thought this could introduce lots of issues so maybe not? Look into later idk, for now its dormant.)
        remoteComponent.AddProgress(116, RemoteLabels.DontKillMyVibe, "Dial the remotes intensity from 100% to 0% in under a second", 1, (id, name) => WasCompleted(id, name), "Vibes Killed");

        remoteComponent.AddProgress(117, RemoteLabels.VibingWithFriends, "Host a Vibe Server Vibe Room.", 1, (id, name) => WasCompleted(id, name), "Rooms Joined");
        SaveData.Achievements[AchievementModuleKind.Remotes] = remoteComponent;
        #endregion REMOTES MODULE

        #region GENERIC MODULE
        var genericComponent = new AchievementComponent();
        genericComponent.AddProgress(118, GenericLabels.TutorialComplete, "Welcome To GagSpeak!", 1, (id, name) => WasCompleted(id, name), "Tutorial Completed");

        genericComponent.AddConditional(119, GenericLabels.AddedFirstPair, "Add your first pair.", () => _pairManager.DirectPairs.Count > 0, (id, name) => WasCompleted(id, name), "Pair Added");

        genericComponent.AddProgress(120, GenericLabels.TheCollector, "Add 20 Pairs.", 20, (id, name) => WasCompleted(id, name), "Pairs Added");

        genericComponent.AddProgress(121, GenericLabels.AppliedFirstPreset, "Apply a preset for a pair, defining the boundaries of your contact.", 1, (id, name) => WasCompleted(id, name), "Presets Applied");

        genericComponent.AddProgress(122, GenericLabels.HelloKinkyWorld, "Use the gagspeak global chat for the first time.", 1, (id, name) => WasCompleted(id, name), "Global Messages Sent");

        genericComponent.AddProgress(123, GenericLabels.KnowsMyLimits, "Use your Safeword for the first time.", 1, (id, name) => WasCompleted(id, name), "Safewords Used");

        genericComponent.AddConditionalProgress(124, GenericLabels.WarriorOfLewd, "View a FULL Cutscene while Bound and Gagged.", 1,
            () => _playerData.IsPlayerGagged && _clientConfigs.GetActiveSetIdx() != -1, (id, name) => WasCompleted(id, name), suffix: "Cutscenes Watched Bound & Gagged");

        genericComponent.AddConditional(125, GenericLabels.EscapingIsNotEasy, "Change your equipment/change job while locked in a restraint set ", () => _clientConfigs.GetActiveSetIdx() != -1, (id, name) => WasCompleted(id, name), "Escape Attempts Made");

        genericComponent.AddConditional(126, GenericLabels.ICantBelieveYouveDoneThis, "Get /slapped while bound", () => _clientConfigs.GetActiveSetIdx() != -1, (id, name) => WasCompleted(id, name), "Slaps Received");

        SaveData.Achievements[AchievementModuleKind.Generic] = genericComponent;
        #endregion GENERIC MODULE

        #region SECRETS MODULE
        var secretsComponent = new AchievementComponent();
        secretsComponent.AddProgress(127, SecretLabels.TooltipLogos, "???", 5, (id, name) => WasCompleted(id, name), prefix: "Found", suffix: "Easter Eggs", isSecret: true);

        secretsComponent.AddConditional(128, SecretLabels.Experimentalist, "???", () =>
        {
            return _playerData.IsPlayerGagged && _clientConfigs.GetActiveSetIdx() != -1 && _clientConfigs.ActivePatternGuid() != Guid.Empty
            && _clientConfigs.ActiveTriggers.Count() > 0 && _clientConfigs.ActiveAlarmCount > 0 && _vibeService.ConnectedToyActive;
        }, (id, name) => WasCompleted(id, name), prefix: "Met", suffix: "Conditions", isSecret: true);

        secretsComponent.AddConditional(129, SecretLabels.HelplessDamsel, "???", () =>
        {
            return _playerData.IsPlayerGagged && _clientConfigs.GetActiveSetIdx() != -1 && _vibeService.ConnectedToyActive && _pairManager.DirectPairs.Any(x => x.UserPairOwnUniquePairPerms.InHardcore)
            && (_playerData.GlobalPerms?.IsFollowing() ?? false) || (_playerData.GlobalPerms?.IsSitting() ?? false);
        }, (id, name) => WasCompleted(id, name), prefix: "Met", suffix: "Hardcore Conditions", isSecret: true);

        secretsComponent.AddConditional(130, SecretLabels.GaggedPleasure, "???", () => _vibeService.ConnectedToyActive && _playerData.IsPlayerGagged, (id, name) => WasCompleted(id, name), "Pleasure Requirements Met", isSecret: true);
        secretsComponent.AddThreshold(131, SecretLabels.BondageClub, "???", 8, (id, name) => WasCompleted(id, name), "Club Members Gathered", isSecret: true);
        secretsComponent.AddConditional(132, SecretLabels.BadEndHostage, "???", () => _clientConfigs.GetActiveSetIdx() != -1 && (_frameworkUtils.ClientState.LocalPlayer?.IsDead ?? false), (id, name) => WasCompleted(id, name), prefix: "Encountered", suffix: "Bad Ends", isSecret: true);
        secretsComponent.AddConditionalProgress(133, SecretLabels.WorldTour, "???", 11, () => _clientConfigs.GetActiveSetIdx() != -1, (id, name) => WasCompleted(id, name), prefix: "Taken", suffix: "Tours in Bondage", isSecret: true);
        secretsComponent.AddConditionalProgress(134, SecretLabels.SilentProtagonist, "???", 1, () => _playerData.IsPlayerGagged && _playerData.GlobalPerms!.LiveChatGarblerActive, (id, name) => WasCompleted(id, name), "MissTypes Made", isSecret: true);
        // The above is currently non functional as i dont have the data to know which chat message type contains these request tasks.

        secretsComponent.AddConditional(135, SecretLabels.BoundgeeJumping, "???", () => _clientConfigs.GetActiveSetIdx() != -1, (id, name) => WasCompleted(id, name), prefix: "Attempted", suffix: "Dangerous Acts", isSecret: true);
        secretsComponent.AddConditionalProgress(136, SecretLabels.KinkyTeacher, "???", 10, () => _clientConfigs.GetActiveSetIdx() != -1, (id, name) => WasCompleted(id, name), "Thanks Received", reqBeginAndFinish: false, isSecret: true);
        secretsComponent.AddConditionalProgress(137, SecretLabels.KinkyProfessor, "???", 50, () => _clientConfigs.GetActiveSetIdx() != -1, (id, name) => WasCompleted(id, name), "Thanks Received", reqBeginAndFinish: false, isSecret: true);
        secretsComponent.AddConditionalProgress(138, SecretLabels.KinkyMentor, "???", 100, () => _clientConfigs.GetActiveSetIdx() != -1, (id, name) => WasCompleted(id, name), "Thanks Received", reqBeginAndFinish: false, isSecret: true);
        secretsComponent.AddThreshold(139, SecretLabels.Overkill, "???", 10, (id, name) => WasCompleted(id, name), "Restriction Conditions Satisfied", isSecret: true); 
        secretsComponent.AddConditional(140, SecretLabels.WildRide, "???", () =>
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
        }, (id, name) => WasCompleted(id, name), "Races Won In Unusual Conditions", isSecret: true);

        // Bound Triad - Win a Triple Triad match against another GagSpeak user (both bound)
        // AddProgress(,SecretLabels.BoundTriad, "Win a Triple Triad match against another GagSpeak user, both bound", 1); <---- Unsure how to atm.

        // My First Collar - Equip a leather choker with your dom's name as creator
        // AddProgress(,SecretLabels.MyFirstCollar, "Equip a leather choker with your dom's name as the creator", 1); // <---- Not ideal to track this as it requires packet sending.

        // Obedient Servant - Complete a custom delivery while restrained
        // AddProgress(,SecretLabels.ObedientServant, "Complete a custom delivery while in a restraint set", 1); <------ Unsure how to do 

        // Start & End conditions are the Cutscene Start and End. (When Fashion Checks are available, see what message is triggered after these.
        secretsComponent.AddConditional(141, SecretLabels.SlavePresentation, "???", () =>
        {
            return _clientConfigs.GetActiveSetIdx() != -1 && _playerData.IsPlayerGagged;
        }, (id, name) => WasCompleted(id, name), "Presentations Given on Stage", isSecret: true);

        SaveData.Achievements[AchievementModuleKind.Secrets] = secretsComponent;
        #endregion SECRETS MODULE
    }
}
