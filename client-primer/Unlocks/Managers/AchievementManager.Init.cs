using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using GagSpeak.GagspeakConfiguration.Models;
using GagSpeak.Utils;
using GagspeakAPI.Extensions;
using System.Runtime.InteropServices;

namespace GagSpeak.Achievements;

public partial class AchievementManager
{
    public void InitializeAchievements()
    {
        // Module Finished
        #region ORDERS MODULE
        var orderComponent = new AchievementComponent(_completionNotifier);
        orderComponent.AddProgress(OrderLabels.JustAVolunteer, "Finish 1 Order", 1, "Orders Finished");
        orderComponent.AddProgress(OrderLabels.AsYouCommand, "Finish 10 Orders", 10, "Orders Finished");
        orderComponent.AddProgress(OrderLabels.AnythingForMyOwner, "Finish 100 Orders", 100, "Orders Finished");
        orderComponent.AddProgress(OrderLabels.GoodDrone, "Finish 1000 Orders", 1000, "Orders Finished");

        orderComponent.AddProgress(OrderLabels.BadSlut, "Fail 1 Order", 1, "Orders Failed");
        orderComponent.AddProgress(OrderLabels.NeedsTraining, "Fail 10 Orders", 10, "Orders Failed");
        orderComponent.AddProgress(OrderLabels.UsefulInOtherWays, "Fail 100 Orders", 100, "Orders Failed");

        orderComponent.AddProgress(OrderLabels.NewSlaveOwner, "Create 1 Order", 1, "Orders Created");
        orderComponent.AddProgress(OrderLabels.TaskManager, "Create 10 Orders", 10, "Orders Created");
        orderComponent.AddProgress(OrderLabels.MaidMaster, "Create 100 Orders", 100, "Orders Created");
        orderComponent.AddProgress(OrderLabels.QueenOfDrones, "Create 1000 Orders", 1000, "Orders Created");

        SaveData.Achievements[AchievementModuleKind.Orders] = orderComponent;
        #endregion ORDERS MODULE

        // Module Finished
        #region GAG MODULE
        var gagComponent = new AchievementComponent(_completionNotifier);
        gagComponent.AddProgress(GagLabels.SelfApplied, "Apply a Gag to Yourself", 1, "Gags Self-Applied");

        gagComponent.AddProgress(GagLabels.ApplyToPair, "Apply a Gag to another GagSpeak Pair", 1, "Gags Applied");
        gagComponent.AddProgress(GagLabels.LookingForTheRightFit, "Apply Gags to other GagSpeak Pairs, or have a Gag applied to you 10 times.", 10, "Gags Applied");
        gagComponent.AddProgress(GagLabels.OralFixation, "Apply Gags to other GagSpeak Pairs, or have a Gag applied to you 100 times.", 100, "Gags Applied");
        gagComponent.AddProgress(GagLabels.AKinkForDrool, "Apply Gags to other GagSpeak Pairs, or have a Gag applied to you 1000 times.", 1000, "Gags Applied");

        gagComponent.AddThreshold(GagLabels.ShushtainableResource, "Have all three Gag Slots occupied at once.", 3, "Gags Active at Once");

        gagComponent.AddProgress(GagLabels.SpeakUpSlut, "Say anything longer than 5 words with LiveChatGarbler on in /say (Please be smart about this)", 1, "Garbled Messages Sent");
        gagComponent.AddProgress(GagLabels.CantHearYou, "Say anything longer than 5 words with LiveChatGarbler on in /yell (Please be smart about this)", 1, "Garbled Messages Sent");
        gagComponent.AddProgress(GagLabels.OneMoreForTheCrowd, "Say anything longer than 5 words with LiveChatGarbler on in /shout (Please be smart about this)", 1, "Garbled Messages Sent");

        gagComponent.AddDuration(GagLabels.SpeechSilverSilenceGolden, "Wear a gag continuously for 1 week", TimeSpan.FromDays(7), DurationTimeUnit.Hours, "Hours");
        gagComponent.AddDuration(GagLabels.TheKinkyLegend, "Wear a gag continuously for 2 weeks", TimeSpan.FromDays(14), DurationTimeUnit.Hours, "Hours");

        gagComponent.AddConditionalProgress(GagLabels.SilentButDeadly, "Complete 10 roulettes with a gag equipped", 10,
            () => _playerData.AppearanceData?.GagSlots.Any(x => x.GagType.ToGagType() != GagType.None) ?? false, "Roulettes Completed");

        gagComponent.AddTimedProgress(GagLabels.ATrueGagSlut, "Be gagged by 10 different people in less than 1 hour", 10, TimeSpan.FromHours(1), "Gags Received In Hour");

        gagComponent.AddProgress(GagLabels.GagReflex, "Be blown away in a gold saucer GATE with any gag equipped.", 1, "Gag Reflexes Experienced");

        gagComponent.AddConditional(GagLabels.QuietNowDear, "Use /shush while targeting a gagged player", () =>
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
        }, "Pairs Hushed");

        gagComponent.AddConditionalProgress(GagLabels.YourFavoriteNurse, "Apply a restraint set or Gag to a GagSpeak pair while you have a Mask Gag Equipped 20 times", 20,
            () => _playerData.AppearanceData?.GagSlots.Any(x => x.GagType.ToGagType() == GagType.MedicalMask) ?? false, "Patients Serviced", reqBeginAndFinish: false);

        gagComponent.AddConditionalProgress(GagLabels.SayMmmph, "Take a screenshot in /gpose while gagged", 1, () => _playerData.IsPlayerGagged, "Photos Taken");

        SaveData.Achievements[AchievementModuleKind.Gags] = gagComponent;
        #endregion GAG MODULE

        #region WARDROBE MODULE
        var wardrobeComponent = new AchievementComponent(_completionNotifier);
        wardrobeComponent.AddProgress(WardrobeLabels.FirstTiemers, "Have a Restraint Set applied for the first time (or apply one to someone else)", 1, "Restraints Applied");

        // Apply increase to progress if a set is applied with an item in the chest slot and a nothing item in the hand slot.
        wardrobeComponent.AddProgress(WardrobeLabels.Cuffed19, "Get your hands restrained 19 times.", 19, "Cuffs Applied");

        // Increase Progress when the person unlocking is another Pair
        wardrobeComponent.AddProgress(WardrobeLabels.TheRescuer, "Unlock 100 Restraints from someone other than yourself.", 100, "Restraints Unlocked");

        // Increase Progress when the restraint set is applied by yourself.
        wardrobeComponent.AddProgress(WardrobeLabels.SelfBondageEnthusiast, "Apply a restraint to yourself 100 times.", 100, "Restraints Applied");

        // Increase Progress when the restraint set is applied by someone else.
        wardrobeComponent.AddProgress(WardrobeLabels.DiDEnthusiast, "Apply a restraint set to someone else 100 times.", 100, "Restraints Applied");

        // fire event whenever iplayerchara object size changes????
        // Look into how to track this since we use the general object table.
        wardrobeComponent.AddConditionalThreshold(WardrobeLabels.CrowdPleaser, "Be restrained with 15 or more people around you.", 15, 
            () => _clientConfigs.GetActiveSetIdx() != -1, "People Nearby");

        // fire trigger whenever a new visible pair is visible.
        wardrobeComponent.AddConditionalThreshold(WardrobeLabels.Humiliation, "Be restrained with 5 or more GagSpeak Pairs nearby.", 5,
            () => _clientConfigs.GetActiveSetIdx() != -1, "GagSpeak Pairs Nearby");

        wardrobeComponent.AddTimedProgress(WardrobeLabels.BondageBunny, "Be restrained by 5 different people in less than 2 hours.", 5, TimeSpan.FromHours(2), "Restraints Received In 2 Hours");

        wardrobeComponent.AddProgress(WardrobeLabels.ToDyeFor, "Dye a Restraint Set 5 times", 5, "Restraints Dyed");
        wardrobeComponent.AddProgress(WardrobeLabels.DyeAnotherDay, "Dye a Restraint Set 10 times", 10, "Restraints Dyed");
        wardrobeComponent.AddProgress(WardrobeLabels.DyeHard, "Dye a Restraint Set 15 times", 15, "Restraints Dyed");

        wardrobeComponent.AddDuration(WardrobeLabels.RiggersFirstSession, "Lock someone in a Restraint Set for 30 minutes", TimeSpan.FromMinutes(30), DurationTimeUnit.Minutes, "Minutes");
        wardrobeComponent.AddDuration(WardrobeLabels.MyLittlePlaything, "Lock someone in a Restraint Set for 1 hour", TimeSpan.FromHours(1), DurationTimeUnit.Minutes, "Minutes");
        wardrobeComponent.AddDuration(WardrobeLabels.SuitsYouBitch, "Lock someone in a Restraint Set for 6 hours", TimeSpan.FromHours(6), DurationTimeUnit.Hours, "Hours");
        wardrobeComponent.AddDuration(WardrobeLabels.TiesThatBind, "Lock someone in a Restraint Set for 1 day", TimeSpan.FromDays(1), DurationTimeUnit.Hours, "Hours");
        wardrobeComponent.AddDuration(WardrobeLabels.SlaveTraining, "Lock someone in a Restraint Set for 1 week", TimeSpan.FromDays(7), DurationTimeUnit.Days, "Days");
        wardrobeComponent.AddDuration(WardrobeLabels.CeremonyOfEternalBondage, "Lock someone in a Restraint Set for 1 month", TimeSpan.FromDays(30), DurationTimeUnit.Days, "Days");

        wardrobeComponent.AddDuration(WardrobeLabels.FirstTimeBondage, "Endure being locked in a Restraint Set for 30 minutes", TimeSpan.FromMinutes(30), DurationTimeUnit.Minutes, "Minutes");
        wardrobeComponent.AddDuration(WardrobeLabels.AmateurBondage, "Endure being locked in a Restraint Set for 1 hour", TimeSpan.FromHours(1), DurationTimeUnit.Minutes, "Minutes");
        wardrobeComponent.AddDuration(WardrobeLabels.ComfortRestraint, "Endure being locked in a Restraint Set for 6 hours", TimeSpan.FromHours(6), DurationTimeUnit.Hours, "Hours");
        wardrobeComponent.AddDuration(WardrobeLabels.DayInTheLifeOfABondageSlave, "Endure being locked in a Restraint Set for 1 day", TimeSpan.FromDays(1), DurationTimeUnit.Hours, "Hours");
        wardrobeComponent.AddDuration(WardrobeLabels.AWeekInBondage, "Endure being locked in a Restraint Set for 1 week", TimeSpan.FromDays(7), DurationTimeUnit.Days, "Days");
        wardrobeComponent.AddDuration(WardrobeLabels.AMonthInBondage, "Endure being locked in a Restraint Set for 1 month", TimeSpan.FromDays(30), DurationTimeUnit.Days, "Days");


        wardrobeComponent.AddConditional(WardrobeLabels.KinkyExplorer, "Run a Dungeon with Cursed Bondage Loot enabled.", () => _clientConfigs.GagspeakConfig.CursedDungeonLoot, "Cursed Runs Started");
        // Make the below conditional progress.
        wardrobeComponent.AddProgress(WardrobeLabels.TemptingFatesTreasure, "Be Caught in Cursed Bondage Loot for the first time.", 1, "Cursed Loot Discovered");
        wardrobeComponent.AddConditionalProgress(WardrobeLabels.BadEndSeeker, "Get trapped in Cursed Bondage Loot 25 times. (Chance must be 25% or lower)", 25,
            () => _clientConfigs.CursedLootConfig.CursedLootStorage.LockChance <= 25, "Cursed Loot Discovered", reqBeginAndFinish: false);
        wardrobeComponent.AddConditionalProgress(WardrobeLabels.EverCursed, "Get trapped in Cursed Bondage Loot 100 times. (Chance must be 25% or lower)", 100,
            () => _clientConfigs.CursedLootConfig.CursedLootStorage.LockChance <= 25, "Cursed Loot Discovered", reqBeginAndFinish: false);

        // Start condition is entering a duty, end condition is leaving a duty 10 times.
        // TODO: Add Vibed as an option here
        wardrobeComponent.AddConditionalProgress(WardrobeLabels.HealSlut, "Complete a duty as a healer while wearing a gag, restraint, or using a vibe.", 1,
            () => _playerData.IsPlayerGagged || _clientConfigs.GetActiveSetIdx() != -1 || _vibeService.ConnectedToyActive, "Duties Completed");

        // Deep Dungeon Achievements
        wardrobeComponent.AddConditionalProgress(WardrobeLabels.BondagePalace, "Reach Floor 50 or 100 of Palace of the Dead while bound.", 1, () => _clientConfigs.GetActiveSetIdx() != -1, "FloorSets Cleared");
        wardrobeComponent.AddConditionalProgress(WardrobeLabels.HornyOnHigh, "Reach Floor 30 of Heaven-on-High while bound.", 1, () => _clientConfigs.GetActiveSetIdx() != -1, "FloorSets Cleared");
        wardrobeComponent.AddConditionalProgress(WardrobeLabels.EurekaWhorethos, "Reach Floor 30 of Eureka Orthos while bound.", 1, () => _clientConfigs.GetActiveSetIdx() != -1, "FloorSets Cleared");
        wardrobeComponent.AddConditionalProgress(WardrobeLabels.MyKinkRunsDeep, "Complete a deep dungeon with hardcore stimulation or hardcore restraints.", 1, () =>
        {
            var activeSet = _clientConfigs.GetActiveSet();
            if(activeSet is null) return false;
            return activeSet.PropertiesEnabledForUser(activeSet.EnabledBy);
        }, "FloorSets Cleared");
        wardrobeComponent.AddConditionalProgress(WardrobeLabels.MyKinksRunDeeper, "Solo a deep dungeon with hardcore stimulation or hardcore restraints.", 1, () =>
        {
            var activeSet = _clientConfigs.GetActiveSet();
            if (activeSet is null) return false;
            return activeSet.PropertiesEnabledForUser(activeSet.EnabledBy);
        }, "FloorSets Cleared");

        // Complete a Trial within 10 levels of max level with Hardcore Properties
        wardrobeComponent.AddConditionalProgress(WardrobeLabels.TrialOfFocus, "Complete a trial within 10 levels of max level with stimulation (HardcoreLabels Focus).", 1, () =>
        {
            if (_frameworkUtils.PlayerLevel < 90) return false;
            // get the set and make sure stimulation is enabled for the person who enabled it.
            var activeSet = _clientConfigs.GetActiveSet();
            if (activeSet is null) return false;

            if (activeSet.SetProperties.TryGetValue(activeSet.EnabledBy, out var prop))
                return prop.StimulationLevel is not StimulationLevel.None;

            return false;
        }, "Hardcore Trials Cleared");
        wardrobeComponent.AddConditionalProgress(WardrobeLabels.TrialOfDexterity, "Complete a trial within 10 levels of max level with arms/legs restrained.", 1, () =>
        {
            if (_frameworkUtils.PlayerLevel < 90) return false;
            // get the set and make sure stimulation is enabled for the person who enabled it.
            var activeSet = _clientConfigs.GetActiveSet();
            if (activeSet is null) return false;

            if (activeSet.SetProperties.TryGetValue(activeSet.EnabledBy, out var prop))
                return prop.ArmsRestrained || prop.LegsRestrained;

            return false;
        }, "Hardcore Trials Cleared");
        wardrobeComponent.AddConditionalProgress(WardrobeLabels.TrialOfTheBlind, "Complete a trial within 10 levels of max level while blindfolded.", 1, () =>
        {
            if (_frameworkUtils.PlayerLevel < 90) return false;
            // get the set and make sure stimulation is enabled for the person who enabled it.
            var activeSet = _clientConfigs.GetActiveSet();
            if (activeSet is null) return false;

            if (activeSet.SetProperties.TryGetValue(activeSet.EnabledBy, out var prop))
                return prop.Blindfolded;

            return false;
        }, "Hardcore Trials Cleared");

        // While actively moving, incorrectly guess a restraint lock while gagged (Secret)
        wardrobeComponent.AddConditional(WardrobeLabels.RunningGag, "Incorrectly guess a gag's lock password while unrestrained and running.", () =>
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
        }, "Funny Conditions Met");

        // Check this in the action function handler
        wardrobeComponent.AddConditionalProgress(WardrobeLabels.AuctionedOff, "Have a restraint set enabled by one GagSpeak user be removed by a different GagSpeak user.", 1,
            () => _clientConfigs.GetActiveSetIdx() != -1, "Auctions Won");

        // Check this in the action function handler
        wardrobeComponent.AddProgress(WardrobeLabels.SoldSlave, "Have a password-locked restraint set locked by one GagSpeak user be unlocked by another.", 1, "Freedom Relinquished");

        // Bondodge - Within 2 seconds of having a restraint set applied to you, remove it from yourself (might want to add a duration conditional but idk?)
        wardrobeComponent.AddTimeLimitedConditional(WardrobeLabels.Bondodge, "Within 2 seconds of having a restraint set applied to you, remove it from yourself",
            TimeSpan.FromSeconds(2), () => _clientConfigs.GetActiveSetIdx() != -1, DurationTimeUnit.Seconds, "Seconds (Within)", isSecret: true);

        SaveData.Achievements[AchievementModuleKind.Wardrobe] = wardrobeComponent;
        #endregion WARDROBE MODULE

        // Module Finished
        #region PUPPETEER MODULE
        var puppeteerComponent = new AchievementComponent(_completionNotifier);
        // (can work both ways)
        puppeteerComponent.AddProgress(PuppeteerLabels.WhoIsAGoodPet, "Be ordered to sit by another pair through Puppeteer.", 1, "Order Received");

        puppeteerComponent.AddProgress(PuppeteerLabels.ControlMyBody, "Enable Allow Motions for another pair.", 1, "Pairs Granted Access");
        puppeteerComponent.AddProgress(PuppeteerLabels.CompleteDevotion, "Enable All Commands for another pair.", 1, "Pairs Granted Access");

        puppeteerComponent.AddTimedProgress(PuppeteerLabels.MasterOfPuppets, "Puppeteer someone 10 times in an hour.", 10, TimeSpan.FromHours(1), "Commands Given In The Hour");

        puppeteerComponent.AddProgress(PuppeteerLabels.KissMyHeels, "Order someone to /grovel 50 times using Puppeteer.", 20, "Grovels Ordered");

        puppeteerComponent.AddProgress(PuppeteerLabels.Ashamed, "Be forced to /sulk through Puppeteer.", 1, "Sulks Forced");

        puppeteerComponent.AddProgress(PuppeteerLabels.ShowingOff, "Order someone to execute any emote with 'dance' in it 10 times.", 10, "Dances Ordered");

        SaveData.Achievements[AchievementModuleKind.Puppeteer] = puppeteerComponent;
        #endregion PUPPETEER MODULE

        // Module Finished
        #region TOYBOX MODULE
        var toyboxComponent = new AchievementComponent(_completionNotifier);
        toyboxComponent.AddProgress(ToyboxLabels.FunForAll, "Create and publish a pattern for the first time.", 1, "Patterns Published");

        toyboxComponent.AddProgress(ToyboxLabels.DeviousComposer, "Publish 10 patterns you have made.", 10, "Patterns Published");

        toyboxComponent.AddProgress(ToyboxLabels.CravingPleasure, "Download 30 Patterns from the Pattern Hub", 30, "Patterns Downloaded");

        toyboxComponent.AddProgress(ToyboxLabels.PatternLover, "Like 30 Patterns from the Pattern Hub", 30, "Patterns Liked");

        toyboxComponent.AddDuration(ToyboxLabels.EnduranceQueen, "Play a pattern for an hour (59m) without pause.", TimeSpan.FromHours(1), DurationTimeUnit.Minutes, "Minutes");

        toyboxComponent.AddConditional(ToyboxLabels.MyFavoriteToys, "Connect a real device (Intiface / PiShock Device) to GagSpeak.", () =>
            { return _playerData.GlobalPiShockPerms != null || _vibeService.DeviceHandler.AnyDeviceConnected; }, "Devices Connected");

        toyboxComponent.AddRequiredTimeConditional(ToyboxLabels.MotivationForRestoration, "Play a pattern for over 30 minutes in Diadem.", TimeSpan.FromMinutes(30),
            () => _clientConfigs.ActivePatternGuid() != Guid.Empty, DurationTimeUnit.Minutes, suffix: "Minutes vibrated in Diadem");

        toyboxComponent.AddConditional(ToyboxLabels.KinkyGambler, "Complete a DeathRoll (win or loss) while having a DeathRoll trigger on.", 
            () => _clientConfigs.ActiveSocialTriggers.Count() > 0, "DeathRolls Gambled");

        toyboxComponent.AddProgress(ToyboxLabels.SubtleReminders, "Have 10 Triggers go off.", 10, "Triggers Fired");
        toyboxComponent.AddProgress(ToyboxLabels.FingerOnTheTrigger, "Have 100 Triggers go off.", 100, "Triggers Fired");
        toyboxComponent.AddProgress(ToyboxLabels.TriggerHappy, "Have 1000 Triggers go off.", 1000, "Triggers Fired");

        toyboxComponent.AddProgress(ToyboxLabels.HornyMornings, "Have an alarm go off.", 1, "Alarms Went Off");

        toyboxComponent.AddConditionalProgress(ToyboxLabels.NothingCanStopMe, "Kill 500 enemies in PvP Frontlines while restrained or vibed.", 500, 
            () => _frameworkUtils.ClientState.IsPvP, "Players Slain While Bound", reqBeginAndFinish: false);

        SaveData.Achievements[AchievementModuleKind.Toybox] = toyboxComponent;
        #endregion TOYBOX MODULE

        #region HARDCORE MODULE
        var hardcoreComponent = new AchievementComponent(_completionNotifier);
        hardcoreComponent.AddProgress(HardcoreLabels.AllTheCollarsOfTheRainbow, "Force 20 different pairs to follow you.", 20, "Pairs Forced To Follow You");

        hardcoreComponent.AddConditionalProgress(HardcoreLabels.UCanTieThis, "Be forced to follow someone, throughout a duty.", 1,
            () => _playerData.GlobalPerms?.IsFollowing() ?? false, "Duties Completed");

        // Forced follow achievements
        hardcoreComponent.AddDuration(HardcoreLabels.ForcedFollow, "Force someone to follow you for 1 minute.", TimeSpan.FromMinutes(1), DurationTimeUnit.Seconds, "Seconds");
        hardcoreComponent.AddDuration(HardcoreLabels.ForcedWalkies, "Force someone to follow you for 5 minutes.", TimeSpan.FromMinutes(5), DurationTimeUnit.Seconds, "Seconds");

        // Time for Walkies achievements
        hardcoreComponent.AddRequiredTimeConditional(HardcoreLabels.TimeForWalkies, "Be forced to follow someone for 1 minute.", TimeSpan.FromMinutes(1),
            () => _playerData.GlobalPerms?.IsFollowing() ?? false, DurationTimeUnit.Seconds, suffix: "Seconds");
        hardcoreComponent.AddRequiredTimeConditional(HardcoreLabels.GettingStepsIn, "Be forced to follow someone for 5 minutes.", TimeSpan.FromMinutes(5),
            () => _playerData.GlobalPerms?.IsFollowing() ?? false, DurationTimeUnit.Minutes, suffix: "Minutes");
        hardcoreComponent.AddRequiredTimeConditional(HardcoreLabels.WalkiesLover, "Be forced to follow someone for 10 minutes.", TimeSpan.FromMinutes(10),
            () => _playerData.GlobalPerms?.IsFollowing() ?? false, DurationTimeUnit.Minutes, suffix: "Minutes");

        //Part of the Furniture - Be forced to sit for 1 hour or more
        hardcoreComponent.AddRequiredTimeConditional(HardcoreLabels.LivingFurniture, "Be forced to sit for 1 hour or more.", TimeSpan.FromHours(1),
            () => _playerData.GlobalPerms?.IsSitting() ?? false, DurationTimeUnit.Minutes, suffix: "Minutes Forced to Sit");

        hardcoreComponent.AddConditional(HardcoreLabels.WalkOfShame, "Be bound, blindfolded, and leashed in a major city.", () => _clientConfigs.GetActiveSetIdx() != -1 
        && (_playerData.GlobalPerms?.IsBlindfolded() ?? false) && (_playerData.GlobalPerms?.IsFollowing() ?? false), "Walk Of Shames Completed");

        hardcoreComponent.AddConditional(HardcoreLabels.BlindLeadingTheBlind, "Be blindfolded while having someone follow you blindfolded.", 
            () => _playerData.GlobalPerms?.IsBlindfolded() ?? false 
            && _pairManager.DirectPairs.Any(x => x.UserPairGlobalPerms.IsFollowing() && x.UserPairGlobalPerms.IsBlindfolded()), "Blind Pairs Led");

        hardcoreComponent.AddConditional(HardcoreLabels.WhatAView, "Use the /lookout emote while wearing a blindfold.",
            () => (_playerData.GlobalPerms?.IsBlindfolded() ?? false), "Blind Lookouts Performed");

        hardcoreComponent.AddRequiredTimeConditional(HardcoreLabels.WhoNeedsToSee, "Be blindfolded in hardcore mode for 3 hours.", TimeSpan.FromHours(3),
            () => (_playerData.GlobalPerms?.IsBlindfolded() ?? false), DurationTimeUnit.Minutes, suffix: "Minutes");


        hardcoreComponent.AddRequiredTimeConditional(HardcoreLabels.PetTraining, "Be forced to stay in someone's house for 30 minutes.", TimeSpan.FromMinutes(30),
            () => (_playerData.GlobalPerms?.IsStaying() ?? false), DurationTimeUnit.Minutes, suffix: "Minutes");
        hardcoreComponent.AddRequiredTimeConditional(HardcoreLabels.NotGoingAnywhere, "Be forced to stay in someone's house for 1 hour.", TimeSpan.FromHours(1),
            () => (_playerData.GlobalPerms?.IsStaying() ?? false), DurationTimeUnit.Minutes, suffix: "Minutes");
        hardcoreComponent.AddRequiredTimeConditional(HardcoreLabels.HouseTrained, "Be forced to stay in someone's house for 1 day.", TimeSpan.FromDays(1),
            () => (_playerData.GlobalPerms?.IsStaying() ?? false), DurationTimeUnit.Hours, suffix: "Hours");

        // Shock-related achievements - Give out shocks
        hardcoreComponent.AddProgress(HardcoreLabels.IndulgingSparks, "Give out 10 shocks.", 10, "Shocks Sent");
        hardcoreComponent.AddProgress(HardcoreLabels.CantGetEnough, "Give out 100 shocks.", 100, "Shocks Sent");
        hardcoreComponent.AddProgress(HardcoreLabels.VerThunder, "Give out 1000 shocks.", 1000, "Shocks Sent");
        hardcoreComponent.AddProgress(HardcoreLabels.WickedThunder, "Give out 10,000 shocks.", 10000, "Shocks Sent");
        hardcoreComponent.AddProgress(HardcoreLabels.ElectropeHasNoLimits, "Give out 25,000 shocks.", 25000, "Shocks Sent");

        // Shock-related achievements - Get shocked
        hardcoreComponent.AddProgress(HardcoreLabels.ShockAndAwe, "Get shocked 10 times.", 10, "Shocks Received");
        hardcoreComponent.AddProgress(HardcoreLabels.ShockingExperience, "Get shocked 100 times.", 100, "Shocks Received");
        hardcoreComponent.AddProgress(HardcoreLabels.ShockolateTasting, "Get shocked 1000 times.", 1000, "Shocks Received");
        hardcoreComponent.AddProgress(HardcoreLabels.ShockAddiction, "Get shocked 10,000 times.", 10000, "Shocks Received");
        hardcoreComponent.AddProgress(HardcoreLabels.WarriorOfElectrope, "Get shocked 25,000 times.", 25000, "Shocks Received");
        hardcoreComponent.AddProgress(HardcoreLabels.ShockSlut, "Get shocked 50,000 times.", 50000, "Shocks Received");

        // Tamed Brat - Shock collar beep or vibrate 10 times without a follow-up shock (Look into this later)
        // AddDuration(HardcoreLabels.TamedBrat, "Shock collar beep or vibrate 10 times without a follow-up shock for another few minutes.", TimeSpan.FromMinutes(2));
        SaveData.Achievements[AchievementModuleKind.Hardcore] = hardcoreComponent;
        #endregion HARDCORE MODULE

        #region REMOTES MODULE
        var remoteComponent = new AchievementComponent(_completionNotifier);
        remoteComponent.AddProgress(RemoteLabels.JustVibing, "Use the Remote Control feature for the first time.", 1, "Remotes Opened");

        // TODO: Make this turning down someone else's once its implemented.
        // (on second thought this could introduce lots of issues so maybe not? Look into later idk, for now its dormant.)
        remoteComponent.AddProgress(RemoteLabels.DontKillMyVibe, "Dial the remotes intensity from 100% to 0% in under a second", 1, "Vibes Killed");

        remoteComponent.AddProgress(RemoteLabels.VibingWithFriends, "Host a Vibe Server Vibe Room.", 1, "Rooms Joined");
        SaveData.Achievements[AchievementModuleKind.Remotes] = remoteComponent;
        #endregion REMOTES MODULE

        #region GENERIC MODULE
        var genericComponent = new AchievementComponent(_completionNotifier);
        genericComponent.AddProgress(GenericLabels.TutorialComplete, "Welcome To GagSpeak!", 1, "Tutorial Completed");

        genericComponent.AddConditional(GenericLabels.AddedFirstPair, "Add your first pair.", () => _pairManager.DirectPairs.Count > 0, "Pair Added");

        genericComponent.AddProgress(GenericLabels.TheCollector, "Add 20 Pairs.", 20, "Pairs Added");

        genericComponent.AddProgress(GenericLabels.AppliedFirstPreset, "Apply a preset for a pair, defining the boundaries of your contact.", 1, "Presets Applied");

        genericComponent.AddProgress(GenericLabels.HelloKinkyWorld, "Use the gagspeak global chat for the first time.", 1, "Global Messages Sent");

        genericComponent.AddProgress(GenericLabels.KnowsMyLimits, "Use your Safeword for the first time.", 1, "Safewords Used");

        genericComponent.AddConditionalProgress(GenericLabels.WarriorOfLewd, "View a FULL Cutscene while Bound and Gagged.", 1,
            () => _playerData.IsPlayerGagged && _clientConfigs.GetActiveSetIdx() != -1, suffix: "Cutscenes Watched Bound & Gagged");

        genericComponent.AddConditional(GenericLabels.EscapingIsNotEasy, "Change your equipment/change job while locked in a restraint set ", () => _clientConfigs.GetActiveSetIdx() != -1, "Escape Attempts Made");

        genericComponent.AddConditional(GenericLabels.ICantBelieveYouveDoneThis, "Get /slapped while bound", () => _clientConfigs.GetActiveSetIdx() != -1, "Slaps Received");

        SaveData.Achievements[AchievementModuleKind.Generic] = genericComponent;
        #endregion GENERIC MODULE

        #region SECRETS MODULE
        var secretsComponent = new AchievementComponent(_completionNotifier);
        secretsComponent.AddProgress(SecretLabels.TooltipLogos, "Click on all module logo icons to unlock this achievement", 8, "Easter Eggs Found", isSecret: true);

        secretsComponent.AddConditional(SecretLabels.Experimentalist, "Activate a Gag, Restraint Set, Toy, Trigger, Alarm, and Pattern at the same time", () =>
        {
            return _playerData.IsPlayerGagged && _clientConfigs.GetActiveSetIdx() != -1 && _clientConfigs.ActivePatternGuid() != Guid.Empty
            && _clientConfigs.ActiveTriggers.Count() > 0 && _clientConfigs.ActiveAlarmCount > 0 && _vibeService.ConnectedToyActive;
        }, "Conditions Met", isSecret: true);

        // Fire check upon sending a garbled message in chat
        secretsComponent.AddConditional(SecretLabels.HelplessDamsel, "While in hardcore mode, follow or sit while having a toy, restraint, and gag active, then send a garbled message in chat", () =>
        {
            return _playerData.IsPlayerGagged && _clientConfigs.GetActiveSetIdx() != -1 && _vibeService.ConnectedToyActive && _pairManager.DirectPairs.Any(x => x.UserPairOwnUniquePairPerms.InHardcore)
            && (_playerData.GlobalPerms?.IsFollowing() ?? false) || (_playerData.GlobalPerms?.IsFollowing() ?? false);
        }, "Hardcore Conditions Met", isSecret: true);

        secretsComponent.AddConditional(SecretLabels.GaggedPleasure, "Be gagged and Vibrated at the same time", 
            () => _vibeService.ConnectedToyActive && _playerData.IsPlayerGagged, "Pleasure Requirements Met", isSecret: true);

        // Check whenever we receive a new player visible message
        secretsComponent.AddThreshold(SecretLabels.BondageClub, "Have at least 8 pairs near you at the same time", 8, "Club Members Gathered", isSecret: true);

        secretsComponent.AddConditional(SecretLabels.BadEndHostage, "Get KO'd while in a Restraint Set", 
            () => _clientConfigs.GetActiveSetIdx() != -1 && (_frameworkUtils.ClientState.LocalPlayer?.IsDead ?? false), "Hostage BadEnd's Occurred", isSecret: true);

        // track these in a helper function of sorts, compare against the list of preset id's and make sure all are contained in it. Each time the condition is achieved, increase the point.
        // TODO: Come back to this one later, requires setting up a custom list for it. We will have a special helper for this.
        secretsComponent.AddTimedProgress(SecretLabels.WorldTour, "Visit every major city Aetheryte plaza while bound, 2 minutes in each", 7, TimeSpan.FromMinutes(2), "Tours Taken", isSecret: true);

        // Listen for a chat message prompt requiring you to /say something, and once that occurs, check if the player is gagged.
        secretsComponent.AddConditionalProgress(SecretLabels.SilentProtagonist, "Be Gagged with the LiveChatGarbler active, while having an active quest requiring you to /say something", 1,
            () => _playerData.IsPlayerGagged && _playerData.GlobalPerms!.LiveChatGarblerActive, "MissTypes Made", isSecret: true);
        // DO THE ABOVE
        // VIA EXPERIMENTATION
        // AFTER WE GET THE PLUGIN TO RUN AGAIN.

        secretsComponent.AddConditional(SecretLabels.BoundgeeJumping, "Jump off a cliff while in Bondage, Drop to 1 HP", 
            () => _clientConfigs.GetActiveSetIdx() != -1 && _frameworkUtils.ClientState.LocalPlayer?.CurrentHp is 1, "Dangerous Acts Attempted", isSecret: true);

        secretsComponent.AddConditionalProgress(SecretLabels.KinkyTeacher, "Receive 10 commendations while bound", 10, () => _clientConfigs.GetActiveSetIdx() != -1, "Thanks Received", reqBeginAndFinish: false, isSecret: true);
        secretsComponent.AddConditionalProgress(SecretLabels.KinkyProfessor, "Receive 50 commendations while bound", 50, () => _clientConfigs.GetActiveSetIdx() != -1, "Thanks Received", reqBeginAndFinish: false, isSecret: true);
        secretsComponent.AddConditionalProgress(SecretLabels.KinkyMentor, "Receive 100 commendations while bound", 100, () => _clientConfigs.GetActiveSetIdx() != -1, "Thanks Received", reqBeginAndFinish: false, isSecret: true);

        // Make this more logical later, but for now just see if you're restrained while no longer logged in.
        // WILL ADD
        // THIS LATER
        // AND STUFF
        secretsComponent.AddConditional(SecretLabels.AsIfThingsCouldntGetAnyWorse, "Get disconnected (90k) while in bondage", () =>
        { return _clientConfigs.GetActiveSetIdx() != -1 && !_frameworkUtils.ClientState.IsLoggedIn; }, "Fatal Strikes Blown", isSecret: true);

        // Overkill - Bind someone in all available slots
        // <-- Would need to handle the conditional here in the action resolution prior to the call.
        secretsComponent.AddProgress(SecretLabels.Overkill, "Get Bound by someone with a restraint set that takes up all available slots", 1, "Restriction Conditions Satisfied", isSecret: true); 

        // Opportunist - Bind someone who just recently restrained anyone
        // AddProgress(SecretLabels.Opportunist, "Bind someone who recently restrained another", 1); <---- Unsure how to do this, unless they are binding another pair you have.

        // Wild Ride - Win a chocobo race while wearing a restraint set
        secretsComponent.AddConditional(SecretLabels.WildRide, "Win a Chocobo race while restrained", () =>
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
        }, "Races Won In Unusual Conditions", isSecret: true);

        // Bound Triad - Win a Triple Triad match against another GagSpeak user (both bound)
        // AddProgress(SecretLabels.BoundTriad, "Win a Triple Triad match against another GagSpeak user, both bound", 1); <---- Unsure how to atm.

        // My First Collar - Equip a leather choker with your dom's name as creator
        // AddProgress(SecretLabels.MyFirstCollar, "Equip a leather choker with your dom's name as the creator", 1); // <---- Unsure how to do this atm.

        // Obedient Servant - Complete a custom delivery while restrained
        // AddProgress(SecretLabels.ObedientServant, "Complete a custom delivery while in a restraint set", 1); <------ Unsure how to do 

        // Start & End conditions are the Cutscene Start and End.
        secretsComponent.AddConditionalProgress(SecretLabels.SlavePresentation, "Participate in a fashion report while Gagged and Restrained", 1, () =>
        {
            bool fashionCheckVisible = false;
            unsafe
            {
                var fashionCheckOpen = (AtkUnitBase*)AtkFuckery.GetAddonByName("FashionCheck");
                if (fashionCheckOpen != null)
                    fashionCheckVisible = fashionCheckOpen->RootNode->IsVisible();
            };
            return fashionCheckVisible && _clientConfigs.GetActiveSetIdx() != -1 && _playerData.IsPlayerGagged;
        }, "Presentations Given on Stage", isSecret: true);

        SaveData.Achievements[AchievementModuleKind.Secrets] = secretsComponent;
        #endregion SECRETS MODULE
    }
}
