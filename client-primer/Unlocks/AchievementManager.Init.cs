using FFXIVClientStructs.FFXIV.Component.GUI;
using GagSpeak.Utils;
using GagspeakAPI.Extensions;

namespace GagSpeak.Achievements;

public partial class AchievementManager
{
    public void InitializeAchievements()
    {
        // Module Finished
        #region ORDERS MODULE
        AddProgress(Achievements.Orders.JustAVolunteer, "Finish 1 Order", 1);
        AddProgress(Achievements.Orders.AsYouCommand, "Finish 10 Orders", 10);
        AddProgress(Achievements.Orders.AnythingForMyOwner, "Finish 100 Orders", 100);
        AddProgress(Achievements.Orders.GoodDrone, "Finish 1000 Orders", 1000);

        AddProgress(Achievements.Orders.BadSlut, "Fail 1 Order", 1);
        AddProgress(Achievements.Orders.NeedsTraining, "Fail 10 Orders", 10);
        AddProgress(Achievements.Orders.UsefulInOtherWays, "Fail 100 Orders", 100);

        AddProgress(Achievements.Orders.NewSlaveOwner, "Create 1 Order", 1);
        AddProgress(Achievements.Orders.TaskManager, "Create 10 Orders", 10);
        AddProgress(Achievements.Orders.MaidMaster, "Create 100 Orders", 100);
        AddProgress(Achievements.Orders.QueenOfDrones, "Create 1000 Orders", 1000);
        #endregion ORDERS MODULE

        // Module Finished
        #region GAG MODULE
        AddProgress(Achievements.Gags.SelfApplied, "Apply a Gag to Yourself", 1);

        AddProgress(Achievements.Gags.ApplyToPair, "Apply a Gag to another GagSpeak Pair", 1);

        AddProgress(Achievements.Gags.LookingForTheRightFit, "Apply Gags to other GagSpeak Pairs, or have a Gag applied to you 10 times.", 10);
        AddProgress(Achievements.Gags.OralFixation, "Apply Gags to other GagSpeak Pairs, or have a Gag applied to you 100 times.", 100);
        AddProgress(Achievements.Gags.AKinkForDrool, "Apply Gags to other GagSpeak Pairs, or have a Gag applied to you 1000 times.", 1000);

        AddConditional(Achievements.Gags.ShushtainableResource, "Have all three Gag Slots occupied at once.",
            () => _playerData.AppearanceData?.GagSlots.All(x => x.GagType.ToGagType() != GagType.None) ?? false);

        AddProgress(Achievements.Gags.SpeakUpSlut, "Say anything longer than 5 words with LiveChatGarbler on in /say", 1);
        AddProgress(Achievements.Gags.CantHearYou, "Say anything longer than 5 words with LiveChatGarbler on in /yell", 1);
        AddProgress(Achievements.Gags.OneMoreForTheCrowd, "Say anything longer than 5 words with LiveChatGarbler on in /shout", 1);

        AddDuration(Achievements.Gags.SpeechSilverSilenceGolden, "Wear a gag continuously for 1 week", TimeSpan.FromDays(7));

        AddDuration(Achievements.Gags.TheKinkyLegend, "Wear a gag continuously for 2 weeks", TimeSpan.FromDays(14));

        AddConditionalProgress(Achievements.Gags.SilentButDeadly, "Complete 10 roulettes with a gag equipped", 10,
            () => _playerData.AppearanceData?.GagSlots.Any(x => x.GagType.ToGagType() != GagType.None) ?? false);

        AddTimedProgress(Achievements.Gags.ATrueGagSlut, "Be gagged by 10 different people in less than 1 hour", 10, TimeSpan.FromHours(1));

        AddConditional(Achievements.Gags.QuietNowDear, "Use /shush while targeting a gagged player", () =>
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
        });

        AddConditionalProgress(Achievements.Gags.YourFavoriteNurse, "Apply a restraint set or Gag to a GagSpeak pair while you have a Mask Gag Equipped 20 times", 20,
            () => _playerData.AppearanceData?.GagSlots.Any(x => x.GagType.ToGagType() == GagType.MedicalMask) ?? false, false);

        AddConditionalProgress(Achievements.Gags.SayMmmph, "Take a screenshot in /gpose while gagged", 1, () => _playerData.IsPlayerGagged());
        #endregion GAG MODULE

        #region WARDROBE MODULE
        //AddProgress(Achievements.Wardrobe.FirstTiemers, "Have a Restraint Set applied for the first time (or apply one to someone else)", 1);

        // Apply increase to progress if a set is applied with an item in the chest slot and a nothing item in the hand slot.
        //AddProgress(Achievements.Wardrobe.Cuffed19, "Get your hands restrained 19 times.", 19);

        // Increase Progress when the person unlocking is another Pair
        //AddProgress(Achievements.Wardrobe.TheRescuer, "Unlock 100 Restraints from someone other than yourself.", 100);

        // Increase Progress when the restraint set is applied by yourself.
        //AddProgress(Achievements.Wardrobe.SelfBondageEnthusiast, "Apply a restraint to yourself 100 times.", 100);

        // Increase Progress when the restraint set is applied by someone else.
        //AddProgress(Achievements.Wardrobe.DiDEnthusiast, "Apply a restraint set to someone else 100 times.", 100);

        // fire event whenever iplayerchara object size changes????
        // (might damage performance, if so just detect on set equip)
        AddProgress(Achievements.Wardrobe.CrowdPleaser, "Be restrained with 15 or more people around you.", 1);

        // fire trigger whenever a new visible pair is visible.
        AddProgress(Achievements.Wardrobe.Humiliation, "Be restrained with 5 or more GagSpeak Pairs nearby.", 1);

        //AddTimedProgress(Achievements.Wardrobe.BondageBunny, "Be restrained by 5 different people in less than 2 hours.", 5, TimeSpan.FromHours(2));

        /*        AddProgress(Achievements.Wardrobe.ToDyeFor, "Dye a Restraint Set 5 times", 5);
                AddProgress(Achievements.Wardrobe.DyeAnotherDay, "Dye a Restraint Set 10 times", 10);
                AddProgress(Achievements.Wardrobe.DyeHard, "Dye a Restraint Set 15 times", 15);*/

        /*        AddDuration(Achievements.Wardrobe.RiggersFirstSession, "Lock someone in a Restraint Set for 30 minutes", TimeSpan.FromMinutes(30));
                AddDuration(Achievements.Wardrobe.MyLittlePlaything, "Lock someone in a Restraint Set for 1 hour", TimeSpan.FromHours(1));
                AddDuration(Achievements.Wardrobe.SuitsYouBitch, "Lock someone in a Restraint Set for 6 hours", TimeSpan.FromHours(6));
                AddDuration(Achievements.Wardrobe.TiesThatBind, "Lock someone in a Restraint Set for 1 day", TimeSpan.FromDays(1));
                AddDuration(Achievements.Wardrobe.SlaveTraining, "Lock someone in a Restraint Set for 1 week", TimeSpan.FromDays(7));
                AddDuration(Achievements.Wardrobe.CeremonyOfEternalBondage, "Lock someone in a Restraint Set for 1 month", TimeSpan.FromDays(30));

                AddDuration(Achievements.Wardrobe.FirstTimeBondage, "Endure being locked in a Restraint Set for 30 minutes", TimeSpan.FromMinutes(30));
                AddDuration(Achievements.Wardrobe.AmateurBondage, "Endure being locked in a Restraint Set for 1 hour", TimeSpan.FromHours(1));
                AddDuration(Achievements.Wardrobe.ComfortRestraint, "Endure being locked in a Restraint Set for 6 hours", TimeSpan.FromHours(6));
                AddDuration(Achievements.Wardrobe.DayInTheLifeOfABondageSlave, "Endure being locked in a Restraint Set for 1 day", TimeSpan.FromDays(1));
                AddDuration(Achievements.Wardrobe.AWeekInBondage, "Endure being locked in a Restraint Set for 1 week", TimeSpan.FromDays(7));
                AddDuration(Achievements.Wardrobe.AMonthInBondage, "Endure being locked in a Restraint Set for 1 month", TimeSpan.FromDays(30));*/

        // Start condition is entering a duty, end condition is leaving a duty 10 times.
        // TODO: Add Vibed as an option here
        /*        AddConditionalProgress(Achievements.Wardrobe.HealSlut, "Complete a duty as a healer while wearing a gag, restraint, or using a vibe.", 1,
                    () => _playerData.IsPlayerGagged() || _clientConfigs.GetActiveSetIdx() != -1);

                // Deep Dungeon Achievements
                AddConditionalProgress(Achievements.Wardrobe.BondagePalace, "Reach Floor 50 or 100 of Palace of the Dead while bound.", 1, () => _clientConfigs.GetActiveSetIdx() != -1);
                AddConditionalProgress(Achievements.Wardrobe.HornyOnHigh, "Reach Floor 30 of Heaven-on-High while bound.", 1, () => _clientConfigs.GetActiveSetIdx() != -1);
                AddConditionalProgress(Achievements.Wardrobe.EurekaWhorethos, "Reach Floor 30 of Eureka Orthos while bound.", 1, () => _clientConfigs.GetActiveSetIdx() != -1);
                AddConditionalProgress(Achievements.Wardrobe.MyKinkRunsDeep, "Complete a deep dungeon with hardcore stimulation or hardcore restraints.", 1, () =>
                {
                    var activeSet = _clientConfigs.GetActiveSet();
                    var activeSetIdx = _clientConfigs.GetActiveSetIdx();
                    if (activeSetIdx == -1 || activeSet is null) return false;
                    return _clientConfigs.PropertiesEnabledForSet(activeSetIdx, activeSet.EnabledBy);
                });
                AddConditionalProgress(Achievements.Wardrobe.MyKinksRunDeeper, "Solo a deep dungeon with hardcore stimulation or hardcore restraints.", 1, () =>
                {
                    var activeSet = _clientConfigs.GetActiveSet();
                    var activeSetIdx = _clientConfigs.GetActiveSetIdx();
                    if (activeSetIdx == -1 || activeSet is null) return false;
                    return _clientConfigs.PropertiesEnabledForSet(activeSetIdx, activeSet.EnabledBy);
                });*/

        // Complete a Trial within 10 levels of max level with Hardcore Properties
        AddConditionalProgress(Achievements.Wardrobe.TrialOfFocus, "Complete a trial within 10 levels of max level with stimulation (Achievements.Hardcore Focus).", 1, () =>
        {
            if (_frameworkUtils.PlayerLevel < 91) return false;
            // get the set and make sure stimulation is enabled for the person who enabled it.
            var activeSet = _clientConfigs.GetActiveSet();
            if (activeSet is null) return false;

            if (activeSet.SetProperties.TryGetValue(activeSet.EnabledBy, out var prop))
                return prop.LightStimulation || prop.MildStimulation || prop.HeavyStimulation;

            return false;
        });
        AddConditionalProgress(Achievements.Wardrobe.TrialOfDexterity, "Complete a trial within 10 levels of max level with arms/legs restrained.", 1, () =>
        {
            if (_frameworkUtils.PlayerLevel < 91) return false;
            // get the set and make sure stimulation is enabled for the person who enabled it.
            var activeSet = _clientConfigs.GetActiveSet();
            if (activeSet is null) return false;

            if (activeSet.SetProperties.TryGetValue(activeSet.EnabledBy, out var prop))
                return prop.ArmsRestrained || prop.LegsRestrained;

            return false;
        });
        AddConditionalProgress(Achievements.Wardrobe.TrialOfTheBlind, "Complete a trial within 10 levels of max level while blindfolded.", 1, () =>
        {
            if (_frameworkUtils.PlayerLevel < 91) return false;
            // get the set and make sure stimulation is enabled for the person who enabled it.
            var activeSet = _clientConfigs.GetActiveSet();
            if (activeSet is null) return false;

            if (activeSet.SetProperties.TryGetValue(activeSet.EnabledBy, out var prop))
                return prop.Blindfolded;

            return false;
        });

        // While actively moving, incorrectly guess a restraint lock while gagged (Secret)
        /*        AddConditional(Achievements.Wardrobe.RunningGag, "Incorrectly guess a gag's lock password while unrestrained and running.", () =>
                {
                    unsafe
                    {
                        var gameControl = XivControl.Control.Instance();
                        var movementByte = Marshal.ReadByte((nint)gameControl, 24131);
                        var movementDetection = AgentMap.Instance();
                        // do a marshal read from this byte offset if it doesnt return proper value.
                        var result = movementDetection->IsPlayerMoving;
                        StaticLogger.Logger.LogInformation("IsPlayerMoving Result: " + result +" || IsWalking Byte: "+movementByte);
                        return _playerData.IsPlayerGagged() && _clientConfigs.GetActiveSetIdx() == -1 && result == 1 && movementByte == 0;
                    }
                });*/

        // Check this in the action function handler
        /*        AddConditionalProgress(Achievements.Wardrobe.AuctionedOff, "Have a restraint set enabled by one GagSpeak user be removed by a different GagSpeak user.", 1,
                    () => _clientConfigs.GetActiveSetIdx() != -1);*/

        // Check this in the action function handler
        //AddProgress(Achievements.Wardrobe.SoldSlave, "Have a password-locked restraint set locked by one GagSpeak user be unlocked by another.", 1);

        // Bondodge - Within 2 seconds of having a restraint set applied to you, remove it from yourself (might want to add a duration conditional but idk?)
        /*        AddConditionalDuration(Achievements.Wardrobe.Bondodge, "Within 2 seconds of having a restraint set applied to you, remove it from yourself",
                    TimeSpan.FromSeconds(2), () => _clientConfigs.GetActiveSetIdx() != -1, true);*/
        #endregion WARDROBE MODULE

        // Module Finished
        #region PUPPETEER MODULE
        // (can work both ways)
        AddProgress(Achievements.Puppeteer.WhoIsAGoodPet, "Be ordered to sit by another pair through Puppeteer.", 1);

        AddProgress(Achievements.Puppeteer.ControlMyBody, "Enable Allow Motions for another pair.", 1);
        AddProgress(Achievements.Puppeteer.CompleteDevotion, "Enable All Commands for another pair.", 1);

        AddTimedProgress(Achievements.Puppeteer.MasterOfPuppets, "Puppeteer someone 10 times in an hour.", 10, TimeSpan.FromHours(1));

        AddProgress(Achievements.Puppeteer.KissMyHeels, "Order someone to /grovel 50 times using Puppeteer.", 20);

        AddProgress(Achievements.Puppeteer.Ashamed, "Be forced to /sulk through Puppeteer.", 1);

        AddProgress(Achievements.Puppeteer.ShowingOff, "Order someone to execute any emote with 'dance' in it 10 times.", 10);
        #endregion PUPPETEER MODULE

        // Module Finished
        #region TOYBOX MODULE
        AddProgress(Achievements.Toybox.FunForAll, "Create and publish a pattern for the first time.", 1);

        AddProgress(Achievements.Toybox.DeviousComposer, "Publish 10 patterns you have made.", 10);

        AddProgress(Achievements.Toybox.CravingPleasure, "Download 30 Patterns from the Pattern Hub", 30);

        AddProgress(Achievements.Toybox.PatternLover, "Like 30 Patterns from the Pattern Hub", 30);

        AddDuration(Achievements.Toybox.EnduranceQueen, "Play a pattern for an hour (59m) without pause.", TimeSpan.FromHours(1));

        AddConditional(Achievements.Toybox.MyFavoriteToys, "Connect a real device (Intiface / PiShock Device) to GagSpeak.", () =>
            { return _playerData.GlobalPiShockPerms != null || _vibeService.DeviceHandler.AnyDeviceConnected; });

        AddConditionalDuration(Achievements.Toybox.MotivationForRestoration, "Play a pattern for over 30 minutes in Diadem.", TimeSpan.FromMinutes(30),
            () => _clientConfigs.ActivePatternGuid() != Guid.Empty);

        AddConditional(Achievements.Toybox.KinkyGambler, "Complete a DeathRoll (win or loss) while having a DeathRoll trigger on.", () => _clientConfigs.ActiveSocialTriggers.Count() > 0);

        AddProgress(Achievements.Toybox.SubtleReminders, "Have 10 Triggers go off.", 10);
        AddProgress(Achievements.Toybox.FingerOnTheTrigger, "Have 100 Triggers go off.", 100);
        AddProgress(Achievements.Toybox.TriggerHappy, "Have 1000 Triggers go off.", 1000);

        AddProgress(Achievements.Toybox.HornyMornings, "Have an alarm go off.", 1);

        AddConditionalProgress(Achievements.Toybox.NothingCanStopMe, "Kill 500 enemies in PvP Frontlines while restrained or vibed.", 500, () => _frameworkUtils.ClientState.IsPvP, false);
        #endregion TOYBOX MODULE

        #region HARDCORE MODULE
        AddProgress(Achievements.Hardcore.AllTheCollarsOfTheRainbow, "Force 20 different pairs to follow you.", 20);

        AddConditionalProgress(Achievements.Hardcore.UCanTieThis, "Be forced to follow someone, throughout a duty.", 1,
            () => _pairManager.DirectPairs.Any(x => x.UserPairOwnUniquePairPerms.IsForcedToFollow));

        // Forced follow achievements
        AddDuration(Achievements.Hardcore.ForcedFollow, "Force someone to follow you for 1 minute.", TimeSpan.FromMinutes(1));
        AddDuration(Achievements.Hardcore.ForcedWalkies, "Force someone to follow you for 5 minutes.", TimeSpan.FromMinutes(5));

        // Time for Walkies achievements
        AddConditionalDuration(Achievements.Hardcore.TimeForWalkies, "Be forced to follow someone for 1 minute.", TimeSpan.FromMinutes(1),
            () => _pairManager.DirectPairs.Any(x => x.UserPairOwnUniquePairPerms.IsForcedToFollow));
        AddConditionalDuration(Achievements.Hardcore.GettingStepsIn, "Be forced to follow someone for 5 minutes.", TimeSpan.FromMinutes(5),
            () => _pairManager.DirectPairs.Any(x => x.UserPairOwnUniquePairPerms.IsForcedToFollow));
        AddConditionalDuration(Achievements.Hardcore.WalkiesLover, "Be forced to follow someone for 10 minutes.", TimeSpan.FromMinutes(10),
            () => _pairManager.DirectPairs.Any(x => x.UserPairOwnUniquePairPerms.IsForcedToFollow));

        //Part of the Furniture - Be forced to sit for 1 hour or more
        AddConditionalDuration(Achievements.Hardcore.LivingFurniture, "Be forced to sit for 1 hour or more.", TimeSpan.FromHours(1),
            () => _pairManager.DirectPairs.Any(x => x.UserPairOwnUniquePairPerms.IsForcedToSit || x.UserPairOwnUniquePairPerms.IsForcedToGroundSit));

        AddConditional(Achievements.Hardcore.WalkOfShame, "Be bound, blindfolded, and leashed in a major city.",
            () => _clientConfigs.GetActiveSetIdx() != -1
            && _pairManager.DirectPairs.Any(x => x.UserPairOwnUniquePairPerms.IsBlindfolded)
            && _pairManager.DirectPairs.Any(x => x.UserPairOwnUniquePairPerms.IsForcedToFollow));

        AddConditional(Achievements.Hardcore.BlindLeadingTheBlind, "Be blindfolded while having someone follow you blindfolded.",
            () => _pairManager.DirectPairs.Any(x => x.UserPairOwnUniquePairPerms.IsBlindfolded)
            && _pairManager.DirectPairs.Any(x => x.UserPairUniquePairPerms.IsForcedToFollow && x.UserPairUniquePairPerms.IsBlindfolded));

        AddConditional(Achievements.Hardcore.WhatAView, "Use the /lookout emote while wearing a blindfold.",
            () => _pairManager.DirectPairs.Any(x => x.UserPairOwnUniquePairPerms.IsBlindfolded));

        AddConditionalDuration(Achievements.Hardcore.WhoNeedsToSee, "Be blindfolded in hardcore mode for 3 hours.", TimeSpan.FromHours(3),
            () => _pairManager.DirectPairs.Any(x => x.UserPairOwnUniquePairPerms.IsBlindfolded));


        AddConditionalDuration(Achievements.Hardcore.PetTraining, "Be forced to stay in someone's house for 30 minutes.", TimeSpan.FromMinutes(30),
            () => _pairManager.DirectPairs.Any(x => x.UserPairOwnUniquePairPerms.IsForcedToStay));
        AddConditionalDuration(Achievements.Hardcore.NotGoingAnywhere, "Be forced to stay in someone's house for 1 hour.", TimeSpan.FromHours(1),
            () => _pairManager.DirectPairs.Any(x => x.UserPairOwnUniquePairPerms.IsForcedToStay));
        AddConditionalDuration(Achievements.Hardcore.HouseTrained, "Be forced to stay in someone's house for 1 day.", TimeSpan.FromDays(1),
            () => _pairManager.DirectPairs.Any(x => x.UserPairOwnUniquePairPerms.IsForcedToStay));

        // Shock-related achievements - Give out shocks
        AddProgress(Achievements.Hardcore.IndulgingSparks, "Give out 10 shocks.", 10);
        AddProgress(Achievements.Hardcore.CantGetEnough, "Give out 100 shocks.", 100);
        AddProgress(Achievements.Hardcore.VerThunder, "Give out 1000 shocks.", 1000);
        AddProgress(Achievements.Hardcore.WickedThunder, "Give out 10,000 shocks.", 10000);
        AddProgress(Achievements.Hardcore.ElectropeHasNoLimits, "Give out 25,000 shocks.", 25000);

        // Shock-related achievements - Get shocked
        AddProgress(Achievements.Hardcore.ShockAndAwe, "Get shocked 10 times.", 10);
        AddProgress(Achievements.Hardcore.ShockingExperience, "Get shocked 100 times.", 100);
        AddProgress(Achievements.Hardcore.ShockolateTasting, "Get shocked 1000 times.", 1000);
        AddProgress(Achievements.Hardcore.ShockAddiction, "Get shocked 10,000 times.", 10000);
        AddProgress(Achievements.Hardcore.WarriorOfElectrope, "Get shocked 25,000 times.", 25000);
        AddProgress(Achievements.Hardcore.ShockSlut, "Get shocked 50,000 times.", 50000);

        // Tamed Brat - Shock collar beep or vibrate 10 times without a follow-up shock (Look into this later)
        // AddDuration(Achievements.Hardcore.TamedBrat, "Shock collar beep or vibrate 10 times without a follow-up shock for another few minutes.", TimeSpan.FromMinutes(2));
        #endregion HARDCORE MODULE

        #region REMOTES MODULE
        AddProgress(Achievements.Remotes.JustVibing, "Use the Remote Control feature for the first time.", 1);

        // TODO: Make this turning down someone else's once its implemented.
        // (on second thought this could introduce lots of issues so maybe not? Look into later idk, for now its dormant.)
        AddProgress(Achievements.Remotes.DontKillMyVibe, "Dial the remotes intensity from 100% to 0% in under a second", 1);

        AddProgress(Achievements.Remotes.VibingWithFriends, "Host a Vibe Server Vibe Room.", 1);
        #endregion REMOTES MODULE

        #region GENERIC MODULE
        AddProgress(Achievements.Generic.TutorialComplete, "Welcome To GagSpeak!", 1);

        AddConditional(Achievements.Generic.AddedFirstPair, "Add your first pair.", () => _pairManager.DirectPairs.Count > 0);

        AddProgress(Achievements.Generic.TheCollector, "Add 20 Pairs.", 20);

        AddProgress(Achievements.Generic.AppliedFirstPreset, "Apply a preset for a pair, defining the boundaries of your contact.", 1);

        AddProgress(Achievements.Generic.HelloKinkyWorld, "Use the gagspeak global chat for the first time.", 1);

        AddProgress(Achievements.Generic.KnowsMyLimits, "Use your Safeword for the first time.", 1);

        AddConditionalDuration(Achievements.Generic.WarriorOfLewd, "View a Cutscene while Bound and Gagged.", TimeSpan.FromSeconds(30),
            () => _playerData.IsPlayerGagged() && _clientConfigs.GetActiveSetIdx() != -1);

        AddConditional(Achievements.Generic.KinkyExplorer, "Run a Dungeon with Cursed Bondage Loot enabled.", () => _clientConfigs.GagspeakConfig.CursedDungeonLoot);

        AddProgress(Achievements.Generic.TemptingFatesTreasure, "Be Caught in Cursed Bondage Loot for the first time.", 1);

        AddProgress(Achievements.Generic.BadEndSeeker, "Get trapped in Cursed Bondage Loot 25 times.", 25);

        AddConditional(Achievements.Generic.EscapingIsNotEasy, "Change your equipment/change job while locked in a restraint set ", () => _clientConfigs.GetActiveSetIdx() != -1);

        AddConditional(Achievements.Generic.ICantBelieveYouveDoneThis, "Get /slapped while bound", () => _clientConfigs.GetActiveSetIdx() != -1);
        #endregion GENERIC MODULE

        #region SECRETS MODULE
        AddProgress(Achievements.Secrets.TooltipLogos, "Click on all module logo icons to unlock this achievement", 8);

        AddConditional(Achievements.Secrets.Experimentalist, "Activate a Gag, Restraint Set, Toy, Trigger, Alarm, and Pattern at the same time", () =>
        {
            return _playerData.IsPlayerGagged() && _clientConfigs.GetActiveSetIdx() != -1 && _clientConfigs.ActivePatternGuid() != Guid.Empty
            && _clientConfigs.ActiveTriggers.Count() > 0 && _clientConfigs.ActiveAlarmCount > 0 && _vibeService.ConnectedToyActive;
        });

        // Fire check upon sending a garbled message in chat
        AddConditional(Achievements.Secrets.HelplessDamsel, "While in hardcore mode, follow or sit while having a toy, restraint, and gag active, then send a garbled message in chat", () =>
        {
            return _playerData.IsPlayerGagged() && _clientConfigs.GetActiveSetIdx() != -1 && _vibeService.ConnectedToyActive
            && _pairManager.DirectPairs.Any(x => x.UserPairOwnUniquePairPerms.InHardcore
            && _pairManager.DirectPairs.Any(x => x.UserPairOwnUniquePairPerms.IsForcedToFollow || x.UserPairOwnUniquePairPerms.IsForcedToSit || x.UserPairOwnUniquePairPerms.IsForcedToGroundSit));
        });

        AddConditional(Achievements.Secrets.GaggedPleasure, "Be gagged and Vibrated at the same time", () => _vibeService.ConnectedToyActive && _playerData.IsPlayerGagged());

        // Check whenever we receive a new player visible message
        AddConditional(Achievements.Secrets.BondageClub, "Have at least 8 pairs near you at the same time", () => _pairManager.GetVisiblePairGameObjects().Count >= 8);

        AddConditional(Achievements.Secrets.BadEndHostage, "Get KO'd while in a Restraint Set", () => _clientConfigs.GetActiveSetIdx() != -1 && (_frameworkUtils.ClientState.LocalPlayer?.IsDead ?? false));

        // track these in a helper function of sorts, compare against the list of preset id's and make sure all are contained in it. Each time the condition is achieved, increase the point.
        // TODO: Come back to this one later, requires setting up a custom list for it. We will have a special helper for this.
        AddTimedProgress(Achievements.Secrets.WorldTour, "Visit every major city Aetheryte plaza while bound, 2 minutes in each", 1, TimeSpan.FromMinutes(2));

        // Listen for a chat message prompt requiring you to /say something, and once that occurs, check if the player is gagged.
        AddConditionalProgress(Achievements.Secrets.SilentProtagonist, "Be Gagged with the LiveChatGarbler active, while having an active quest requiring you to /say something", 1,
            () => _playerData.IsPlayerGagged() && _playerData.GlobalPerms!.LiveChatGarblerActive);
        // DO THE ABOVE
        // VIA EXPERIMENTATION
        // AFTER WE GET THE PLUGIN TO RUN AGAIN.


        // TRACK THE ACTION EFFECT
        // THAT HAPPENS
        // ON FALL DAMAGE CONDITION
        AddConditional(Achievements.Secrets.BoundgeeJumping, "Jump off a cliff while in Bondage", () => _clientConfigs.GetActiveSetIdx() != -1);

        AddConditionalProgress(Achievements.Secrets.KinkyTeacher, "Receive 10 commendations while bound", 10, () => _clientConfigs.GetActiveSetIdx() != -1, false);
        AddConditionalProgress(Achievements.Secrets.KinkyProfessor, "Receive 50 commendations while bound", 50, () => _clientConfigs.GetActiveSetIdx() != -1, false);
        AddConditionalProgress(Achievements.Secrets.KinkyMentor, "Receive 100 commendations while bound", 100, () => _clientConfigs.GetActiveSetIdx() != -1, false);

        // Make this more logical later, but for now just see if you're restrained while no longer logged in.
        // WILL ADD
        // THIS LATER
        // AND STUFF
        AddConditional(Achievements.Secrets.AsIfThingsCouldntGetAnyWorse, "Get disconnected (90k) while in bondage", () =>
        { return _clientConfigs.GetActiveSetIdx() != -1 && !_frameworkUtils.ClientState.IsLoggedIn; });

        // Overkill - Bind someone in all available slots
        AddProgress(Achievements.Secrets.Overkill, "Get Bound by someone with a restraint set that takes up all available slots", 1); // <-- Would need to handle the conditional here in the action resolution prior to the call.

        // Opportunist - Bind someone who just recently restrained anyone
        // AddProgress(Achievements.Secrets.Opportunist, "Bind someone who recently restrained another", 1); <---- Unsure how to do this, unless they are binding another pair you have.

        // Wild Ride - Win a chocobo race while wearing a restraint set
        AddConditional(Achievements.Secrets.WildRide, "Win a Chocobo race while restrained", () =>
        {
            var isRacing = _frameworkUtils.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.ChocoboRacing];
            bool raceEndVisible = false;
            unsafe
            {
                var raceEnded = (AtkUnitBase*)GenericHelpers.GetAddonByName("RaceChocoboResult");
                if (raceEnded != null)
                    raceEndVisible = raceEnded->RootNode->IsVisible();
            };
            return isRacing && raceEndVisible && _clientConfigs.GetActiveSetIdx() != -1;
        });

        // Bound Triad - Win a Triple Triad match against another GagSpeak user (both bound)
        // AddProgress(Achievements.Secrets.BoundTriad, "Win a Triple Triad match against another GagSpeak user, both bound", 1); <---- Unsure how to atm.

        // My First Collar - Equip a leather choker with your dom's name as creator
        // AddProgress(Achievements.Secrets.MyFirstCollar, "Equip a leather choker with your dom's name as the creator", 1); // <---- Unsure how to do this atm.

        // Obedient Servant - Complete a custom delivery while restrained
        // AddProgress(Achievements.Secrets.ObedientServant, "Complete a custom delivery while in a restraint set", 1); <------ Unsure how to do 

        // Start & End conditions are the Cutscene Start and End.
        AddConditionalProgress(Achievements.Secrets.SlavePresentation, "Participate in a fashion report while Gagged and Restrained", 1, () =>
        {
            bool fashionCheckVisible = false;
            unsafe
            {
                var fashionCheckOpen = (AtkUnitBase*)GenericHelpers.GetAddonByName("FashionCheck");
                if (fashionCheckOpen != null)
                    fashionCheckVisible = fashionCheckOpen->RootNode->IsVisible();
            };
            return fashionCheckVisible && _clientConfigs.GetActiveSetIdx() != -1 && _playerData.IsPlayerGagged();
        }); // dont require them for now, but could be a fun one to add later.
        #endregion SECRETS MODULE
    }

    #region Initialization Helpers
    private void AddProgress(string title, string description, int targetProgress)
    {
        var achievement = new ProgressAchievement(title, description, targetProgress);
        ProgressAchievements.Add(achievement.Title, achievement);
    }

    private void AddConditional(string title, string description, Func<bool> condition)
    {
        var achievement = new ConditionalAchievement(title, description, condition);
        ConditionalAchievements.Add(achievement.Title, achievement);
    }

    private void AddDuration(string title, string description, TimeSpan duration)
    {
        var achievement = new DurationAchievement(title, description, duration);
        DurationAchievements.Add(achievement.Title, achievement);
    }

    private void AddConditionalDuration(string title, string description, TimeSpan duration, Func<bool> condition, bool finishWithinTime = false)
    {
        var achievement = new ConditionalDurationAchievement(title, description, duration, condition, finishWithinTime);
        ConditionalDurationAchievements.Add(achievement.Title, achievement);
    }

    private void AddConditionalProgress(string title, string description, int targetProgress, Func<bool> condition, bool requireTaskBeginAndFinish = true)
    {
        var achievement = new ConditionalProgressAchievement(title, description, targetProgress, condition);
        ConditionalProgressAchievements.Add(achievement.Title, achievement);
    }

    private void AddTimedProgress(string title, string description, int targetProgress, TimeSpan timeLimit)
    {
        var achievement = new TimedProgressAchievement(title, description, targetProgress, timeLimit);
        TimedProgressAchievements.Add(achievement.Title, achievement);
    }
    #endregion Initialization Helpers
}
