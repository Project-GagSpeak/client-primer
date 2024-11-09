using System.Windows.Forms;

namespace GagSpeak.Achievements;

public struct AchievementInfo
{
    public uint Id { get; init; }
    public string Title { get; init; }
    public string Description { get; init; }

    public AchievementInfo(uint id, string title, string description)
    {
        Id = id;
        Title = title;
        Description = description;
    }
}

public static class Achievements
{
    // Orders
    public static readonly AchievementInfo JustAVolunteer = new AchievementInfo(1, "Just a Volunteer", "Finish 1 Order");
    public static readonly AchievementInfo AsYouCommand = new AchievementInfo(2, "As You Command", "Finish 10 Orders");
    public static readonly AchievementInfo AnythingForMyOwner = new AchievementInfo(3, "Perfect Programming", "Finish 100 Orders");
    public static readonly AchievementInfo GoodDrone = new AchievementInfo(4, "The Perfect Drone", "Finish 1000 Orders");
    public static readonly AchievementInfo BadSlut = new AchievementInfo(5, "Bad Slut", "Fail 1 Order");
    public static readonly AchievementInfo NeedsTraining = new AchievementInfo(6, "In Need of Training", "Fail 10 Orders");
    public static readonly AchievementInfo UsefulInOtherWays = new AchievementInfo(7, "Useful In Other Ways", "Fail 100 Orders");
    public static readonly AchievementInfo NewSlaveOwner = new AchievementInfo(8, "New Slave Owner", "Create 1 Order");
    public static readonly AchievementInfo TaskManager = new AchievementInfo(9, "Task Manager", "Create 10 Orders");
    public static readonly AchievementInfo MaidMaster = new AchievementInfo(10, "Maid Master", "Create 100 Orders");
    public static readonly AchievementInfo QueenOfDrones = new AchievementInfo(11, "Queen of Drones", "Create 1000 Orders");

    // Gags
    public static readonly AchievementInfo SelfApplied = new AchievementInfo(12, "The Right Fit", "Apply a Gag to Yourself");

    public static readonly AchievementInfo SilenceSlut = new AchievementInfo(13, "A Perfect Fit", "Apply a Gag to another Kinkster");
    public static readonly AchievementInfo WatchYourTongue = new AchievementInfo(14, "Watch Your Tongue", "Apply gags to other kinksters 10 times.");
    public static readonly AchievementInfo TongueTamer = new AchievementInfo(15, "Tongue Tamer", "Apply gags to other kinksters 100 times.");
    public static readonly AchievementInfo KinkyLibrarian = new AchievementInfo(142, "Kinky Librarian", "Apply gags to other kinksters 500 times.");
    public static readonly AchievementInfo OrchestratorOfSilence = new AchievementInfo(16, "Orchestrator Of Silence", "Apply gags to other kinksters 1000 times.");

    public static readonly AchievementInfo SilencedSlut = new AchievementInfo(143, "Silenced Slut", "Get gagged by another Kinkster");
    public static readonly AchievementInfo InDeepSilence = new AchievementInfo(144, "In Deep Silence", "Get gagged by another Kinkster 10 times.");
    public static readonly AchievementInfo SilentObsessions = new AchievementInfo(145, "Silent Obsessions", "Get gagged by another Kinkster 100 times.");
    public static readonly AchievementInfo GoldenSilence = new AchievementInfo(146, "Of Golden Silence", "Get gagged by another Kinkster 500 times.");
    public static readonly AchievementInfo AKinkForDrool = new AchievementInfo(147, "A Kink for Drool", "Get gagged by another Kinkster 1000 times.");
    public static readonly AchievementInfo ThePerfectGagSlut = new AchievementInfo(148, "The Perfect GagSlut", "Get gagged by another Kinkster 5000 times.");

    public static readonly AchievementInfo ShushtainableResource = new AchievementInfo(17, "Sustainable Resource", "Have all three Gag Slots occupied at once.");

    public static readonly AchievementInfo OfVoicelessPleas = new AchievementInfo(18, "Of Voiceless Pleas", "Say anything longer than 5 words with LiveChatGarbler on in /say (Please be smart about this)"); // /say
    public static readonly AchievementInfo DefianceInSilence = new AchievementInfo(149, "Defiance In Silence", "Say anything longer than 5 words with LiveChatGarbler on in /say (Please be smart about this)"); // /say
    public static readonly AchievementInfo MuffledResilience = new AchievementInfo(150, "Muffled Resilience", "Say anything longer than 5 words with LiveChatGarbler on in /say (Please be smart about this)"); // /say
    public static readonly AchievementInfo TrainedInSubSpeech = new AchievementInfo(151, "Trained In Sub Speech", "Say anything longer than 5 words with LiveChatGarbler on in /say (Please be smart about this)"); // /say

    public static readonly AchievementInfo PublicSpeaker = new AchievementInfo(19, "Public Speaker", "Say anything longer than 5 words with LiveChatGarbler on in /yell (Please be smart about this)"); // /yell
    public static readonly AchievementInfo FromCriesOfHumility = new AchievementInfo(20, "From Cries of Humility", "Say anything longer than 5 words with LiveChatGarbler on in /shout (Please be smart about this)"); // /shout
    
    public static readonly AchievementInfo SpeechSilverSilenceGolden = new AchievementInfo(21, "Speech is Silver, Silence is Golden", "Wear a gag continuously for 1 week");
    public static readonly AchievementInfo TheKinkyLegend = new AchievementInfo(22, "The Kinky Legend", "Wear a gag continuously for 2 weeks");
    public static readonly AchievementInfo SilentButDeadly = new AchievementInfo(23, "Silent but Deadly", "Complete 10 roulettes with a gag equipped");
    public static readonly AchievementInfo ATrueGagSlut = new AchievementInfo(24, "A True Gag Slut", "Be gagged by 10 different people in less than 1 hour");
    public static readonly AchievementInfo GagReflex = new AchievementInfo(25, "Gag Reflex", "Be blown away in a gold saucer GATE with any gag equipped.");
    public static readonly AchievementInfo QuietNowDear = new AchievementInfo(26, "Quiet Now, Dear", "Use /shush while targeting a gagged player");
    public static readonly AchievementInfo SilenceOfShame = new AchievementInfo(152, "The Silence of Shame", "Be /shush'ed while gagged");
    public static readonly AchievementInfo YourFavoriteNurse = new AchievementInfo(27, "Your Favorite Nurse", "Apply a restraint set or Gag to a GagSpeak pair while you have a Mask Gag Equipped 20 times");
    public static readonly AchievementInfo SayMmmph = new AchievementInfo(28, "Say Mmmph!", "Take a screenshot in /gpose while gagged");

    // Wardrobe
    public static readonly AchievementInfo FirstTiemers = new AchievementInfo(29, "First Tiemers", "Have a Restraint Set applied for the first time (or apply one to someone else)");
    public static readonly AchievementInfo Cuffed19 = new AchievementInfo(30, "Cuffed-19", "Get your hands restrained 19 times.");
    public static readonly AchievementInfo TheRescuer = new AchievementInfo(31, "The Rescuer", "Unlock 100 Restraints from someone other than yourself.");
    public static readonly AchievementInfo SelfBondageEnthusiast = new AchievementInfo(32, "Self-Bondage Enthusiast", "Apply a restraint to yourself 100 times.");
    public static readonly AchievementInfo DiDEnthusiast = new AchievementInfo(33, "Making a Damsel out of You.", "Apply a restraint set to someone else 100 times.");
    public static readonly AchievementInfo CrowdPleaser = new AchievementInfo(34, "Crowd Pleaser", "Be restrained with 15 or more people around you.");
    public static readonly AchievementInfo Humiliation = new AchievementInfo(35, "Lesson in Humiliation", "Be restrained with 5 or more GagSpeak Pairs nearby.");
    public static readonly AchievementInfo BondageBunny = new AchievementInfo(36, "Bondage Bunny", "Be restrained by 5 different people in less than 2 hours.");

    public static readonly AchievementInfo ToDyeFor = new AchievementInfo(37, "To Dye For", "Dye a Restraint Set 5 times");
    public static readonly AchievementInfo DyeAnotherDay = new AchievementInfo(38, "Dye Another Day", "Dye a Restraint Set 10 times");
    public static readonly AchievementInfo DyeHard = new AchievementInfo(39, "Dye Hard", "Dye a Restraint Set 15 times");

    public static readonly AchievementInfo RiggersFirstSession = new AchievementInfo(40, "Riggers First Session", "Lock someone in a Restraint Set for 30 minutes");
    public static readonly AchievementInfo MyLittlePlaything = new AchievementInfo(41, "My Little Plaything", "Lock someone in a Restraint Set for 1 hour");
    public static readonly AchievementInfo SuitsYouBitch = new AchievementInfo(42, "Suits you, Bitch!", "Lock someone in a Restraint Set for 6 hours");
    public static readonly AchievementInfo TiesThatBind = new AchievementInfo(43, "The Ties That Bind", "Lock someone in a Restraint Set for 1 day");
    public static readonly AchievementInfo SlaveTrainer = new AchievementInfo(44, "Slave Trainer", "Lock someone in a Restraint Set for 1 week");
    public static readonly AchievementInfo CeremonyOfEternalBondage = new AchievementInfo(45, "Ceremony Of Eternal Bondage", "Lock someone in a Restraint Set for 1 month");

    public static readonly AchievementInfo FirstTimeBondage = new AchievementInfo(46, "First Time Bondage", "Endure being locked in a Restraint Set for 30 minutes");
    public static readonly AchievementInfo AmateurBondage = new AchievementInfo(47, "Amateur Bondage", "Endure being locked in a Restraint Set for 1 hour");
    public static readonly AchievementInfo ComfortRestraint = new AchievementInfo(48, "In Comforting Restraints", "Endure being locked in a Restraint Set for 6 hours");
    public static readonly AchievementInfo YourBondageMaid = new AchievementInfo(49, "Bondage Maid", "Endure being locked in a Restraint Set for 1 day");
    public static readonly AchievementInfo YourRubberMaid = new AchievementInfo(153, "Rubber Maid", "Endure being locked in a Restraint Set for 4 days");
    public static readonly AchievementInfo TrainedBondageSlave = new AchievementInfo(50, "Trained Bondage Slave", "Endure being locked in a Restraint Set for 1 week");
    public static readonly AchievementInfo YourRubberSlut = new AchievementInfo(154, "Your Rubber Slut", "Endure being locked in a Restraint Set for 2 weeks");
    public static readonly AchievementInfo ATrueBondageSlave = new AchievementInfo(51, "A True Bondage Slave", "Endure being locked in a Restraint Set for 1 month");

    public static readonly AchievementInfo KinkyExplorer = new AchievementInfo(52, "Kinky Explorer", "Run a Dungeon with Cursed Bondage Loot enabled.");
    public static readonly AchievementInfo TemptingFatesTreasure = new AchievementInfo(53, "Tempting Fate's Treasure", "Be Caught in Cursed Bondage Loot for the first time.");
    public static readonly AchievementInfo BadEndSeeker = new AchievementInfo(54, "Bad End Seeker", "Get trapped in Cursed Bondage Loot 25 times. (Chance must be 25% or lower)");
    public static readonly AchievementInfo EverCursed = new AchievementInfo(55, "Ever Cursed", "Get trapped in Cursed Bondage Loot 100 times. (Chance must be 25% or lower)");
    public static readonly AchievementInfo HealSlut = new AchievementInfo(56, "Heal Slut", "Complete a duty as a healer while wearing a gag, restraint, or using a vibe.");

    public static readonly AchievementInfo BondagePalace = new AchievementInfo(57, "Bondage Palace", "Reach Floor 50 or 100 of Palace of the Dead while bound.");
    public static readonly AchievementInfo HornyOnHigh = new AchievementInfo(58, "Horny on High", "Reach Floor 30 of Heaven-on-High while bound.");
    public static readonly AchievementInfo EurekaWhorethos = new AchievementInfo(59, "Eureka Whore-thos", "Reach Floor 30 of Eureka Orthos while bound.");
    
    public static readonly AchievementInfo MyKinkRunsDeep = new AchievementInfo(60, "My Kinks Run Deep", "Complete a deep dungeon with hardcore stimulation or hardcore restraints.");
    public static readonly AchievementInfo MyKinksRunDeeper = new AchievementInfo(61, "My Kinks Run Deeper", "Solo a deep dungeon with hardcore stimulation or hardcore restraints.");

    public static readonly AchievementInfo TrialOfFocus = new AchievementInfo(62, "Trial Of Focus", "Complete a trial within 10 levels of max level with stimulation (HardcoreLabels Focus).");
    public static readonly AchievementInfo TrialOfDexterity = new AchievementInfo(63, "Trial of Dexterity", "Complete a trial within 10 levels of max level with arms/legs restrained.");
    public static readonly AchievementInfo TrialOfTheBlind = new AchievementInfo(64, "Trial Of The Blind", "Complete a trial within 10 levels of max level while blindfolded.");

    public static readonly AchievementInfo RunningGag = new AchievementInfo(65, "Running Gag", "Incorrectly guess a gag's lock password while unrestrained and running.");

    public static readonly AchievementInfo AuctionedOff = new AchievementInfo(66, "Auctioned Off", "Have a restraint set enabled by one GagSpeak user be removed by a different GagSpeak user.");

    public static readonly AchievementInfo SoldSlave = new AchievementInfo(67, "Sold Slave", "Have a password-locked restraint set locked by one GagSpeak user be unlocked by another.");

    public static readonly AchievementInfo Bondodge = new AchievementInfo(68, "Bondodge", "Within 2 seconds of having a restraint set applied to you, remove it from yourself.");

    // Puppeteer
    public static readonly AchievementInfo WhoIsAGoodPet = new AchievementInfo(69, "Who's a good pet~?", "Be ordered to sit by another pair through Puppeteer.");

    public static readonly AchievementInfo ControlMyBody = new AchievementInfo(70, "Control My Body", "Enable Allow Motions for another pair.");

    public static readonly AchievementInfo CompleteDevotion = new AchievementInfo(71, "Complete Devotion", "Enable All Commands for another pair.");

    public static readonly AchievementInfo MasterOfPuppets = new AchievementInfo(72, "Master of Puppets", "Puppeteer someone 10 times in an hour.");

    public static readonly AchievementInfo KissMyHeels = new AchievementInfo(73, "Kiss my Heels", "Order someone to /grovel 50 times using Puppeteer.");

    public static readonly AchievementInfo Ashamed = new AchievementInfo(74, "Ashamed", "Be forced to /sulk through Puppeteer.");

    public static readonly AchievementInfo ShowingOff = new AchievementInfo(75, "Showing Off", "Order someone to execute any emote with 'dance' in it 10 times.");

    // Toybox
    public static readonly AchievementInfo FunForAll = new AchievementInfo(76, "Fun for all", "Create and publish a pattern for the first time.");

    public static readonly AchievementInfo DeviousComposer = new AchievementInfo(77, "Devious Composer", "Publish 10 patterns you have made.");

    public static readonly AchievementInfo CravingPleasure = new AchievementInfo(78, "Craving Pleasure", "Download 30 Patterns from the Pattern Hub.");

    public static readonly AchievementInfo PatternLover = new AchievementInfo(79, "Pattern Lover", "Like 30 Patterns from the Pattern Hub.");

    public static readonly AchievementInfo EnduranceQueen = new AchievementInfo(80, "Endurance King/Queen", "Play a pattern for an hour (59m) without pause.");

    public static readonly AchievementInfo MyFavoriteToys = new AchievementInfo(81, "My Favorite Toys", "Connect a real device (Intiface / PiShock Device) to GagSpeak.");

    public static readonly AchievementInfo MotivationForRestoration = new AchievementInfo(82, "Motivation for Restoration", "Play a pattern for over 30 minutes in Diadem.");

    public static readonly AchievementInfo KinkyGambler = new AchievementInfo(83, "Kinky Gambler", "Complete a DeathRoll (win or loss) while having a DeathRoll trigger on.");

    public static readonly AchievementInfo SubtleReminders = new AchievementInfo(84, "Subtle Reminders", "Have 10 Triggers go off.");
    public static readonly AchievementInfo FingerOnTheTrigger = new AchievementInfo(85, "Finger on the Trigger", "Have 100 Triggers go off.");
    public static readonly AchievementInfo TriggerHappy = new AchievementInfo(86, "Trigger Happy", "Have 1000 Triggers go off.");

    public static readonly AchievementInfo HornyMornings = new AchievementInfo(87, "Horny Mornings", "Have an alarm go off.");

    public static readonly AchievementInfo NothingCanStopMe = new AchievementInfo(88, "Nothing can stop me", "Kill 500 enemies in PvP Frontlines while restrained or vibed.");

    // Hardcore
    public static readonly AchievementInfo AllTheCollarsOfTheRainbow = new AchievementInfo(89, "All the Collars of the Rainbow", "Force 20 pairs to follow you.");

    public static readonly AchievementInfo UCanTieThis = new AchievementInfo(90, "U Can't Tie This", "Be forced to follow someone, throughout a duty.");

    public static readonly AchievementInfo ForcedFollow = new AchievementInfo(91, "Follow Along Now", "Force someone to follow you for 1 minute.");
    public static readonly AchievementInfo ForcedWalkies = new AchievementInfo(92, "It's the Leash of Your Worries", "Force someone to follow you for 5 minutes.");

    public static readonly AchievementInfo TimeForWalkies = new AchievementInfo(93, "Time for Walkies", "Be forced to follow someone for 1 minute.");
    public static readonly AchievementInfo GettingStepsIn = new AchievementInfo(94, "Getting Your Steps In", "Be forced to follow someone for 5 minutes.");
    public static readonly AchievementInfo WalkiesLover = new AchievementInfo(95, "Walkies Lover", "Be forced to follow someone for 10 minutes.");

    public static readonly AchievementInfo LivingFurniture = new AchievementInfo(96, "Living Art", "Be forced to sit for 1 hour or more.");

    public static readonly AchievementInfo WalkOfShame = new AchievementInfo(97, "Walk of Shame", "Be bound, blindfolded, and leashed in a major city.");

    public static readonly AchievementInfo BlindLeadingTheBlind = new AchievementInfo(98, "Blind leading the Blind", "Be blindfolded while having someone follow you blindfolded.");

    public static readonly AchievementInfo WhatAView = new AchievementInfo(99, "What A View", "Use the /lookout emote while wearing a blindfold.");

    public static readonly AchievementInfo WhoNeedsToSee = new AchievementInfo(100, "Who Needs to See?", "Be blindfolded in hardcore mode for 3 hours.");
    public static readonly AchievementInfo PetTraining = new AchievementInfo(101, "House Pet Training", "Be forced to stay in someone's house for 30 minutes.");
    public static readonly AchievementInfo NotGoingAnywhere = new AchievementInfo(102, "Not Going Anywhere", "Be forced to stay in someone's house for 1 hour.");
    public static readonly AchievementInfo HouseTrained = new AchievementInfo(103, "House Trained", "Be forced to stay in someone's house for 1 day.");

    public static readonly AchievementInfo IndulgingSparks = new AchievementInfo(104, "Indulging Sparks", "Give out 10 shocks.");
    public static readonly AchievementInfo CantGetEnough = new AchievementInfo(105, "Can't Get Enough", "Give out 100 shocks.");
    public static readonly AchievementInfo VerThunder = new AchievementInfo(106, "Ver-Thunder", "Give out 1000 shocks.");
    public static readonly AchievementInfo WickedThunder = new AchievementInfo(107, "Wicked Thunder", "Give out 10,000 shocks.");
    public static readonly AchievementInfo ElectropeHasNoLimits = new AchievementInfo(108, "Electrope Has No Limits", "Give out 25,000 shocks.");

    public static readonly AchievementInfo ShockAndAwe = new AchievementInfo(109, "Shock and Awe", "Get shocked 10 times.");
    public static readonly AchievementInfo ShockingExperience = new AchievementInfo(110, "Shocking Experience", "Get shocked 100 times.");
    public static readonly AchievementInfo ShockolateTasting = new AchievementInfo(111, "Shockolate Tasting", "Get shocked 1000 times.");
    public static readonly AchievementInfo ShockAddiction = new AchievementInfo(112, "Shock Addiction", "Get shocked 10,000 times.");
    public static readonly AchievementInfo WarriorOfElectrope = new AchievementInfo(113, "Warrior of Electrope", "Get shocked 25,000 times.");
    public static readonly AchievementInfo ShockSlut = new AchievementInfo(114, "Shock Slut", "Get shocked 50,000 times.");

    // Remotes
    public static readonly AchievementInfo JustVibing = new AchievementInfo(115, "Just Vibing", "Use the Remote Control feature for the first time.");
    public static readonly AchievementInfo DontKillMyVibe = new AchievementInfo(116, "Don't Kill My Vibe", "Dial the remotes intensity from 100% to 0% in under a second.");
    public static readonly AchievementInfo VibingWithFriends = new AchievementInfo(117, "Vibing With Friends", "Host a Vibe Server Vibe Room.");

    // Generic
    public static readonly AchievementInfo TutorialComplete = new AchievementInfo(118, "Tutorial Complete", "Welcome To GagSpeak!");

    public static readonly AchievementInfo AddedFirstPair = new AchievementInfo(119, "Added First Pair", "Add your first pair.");
    public static readonly AchievementInfo TheCollector = new AchievementInfo(120, "The Collector", "Add 20 Pairs.");

    public static readonly AchievementInfo AppliedFirstPreset = new AchievementInfo(121, "Applied First Preset", "Apply a preset for a pair, defining the boundaries of your contact.");
    
    public static readonly AchievementInfo HelloKinkyWorld = new AchievementInfo(122, "Hello Kinky World", "Use the gagspeak global chat for the first time.");
    
    public static readonly AchievementInfo KnowsMyLimits = new AchievementInfo(123, "Knows My Limits", "Use your Safeword for the first time.");
    
    public static readonly AchievementInfo WarriorOfLewd = new AchievementInfo(124, "Warrior Of Lewd", "View a FULL Cutscene while Bound and Gagged.");
    
    public static readonly AchievementInfo EscapingIsNotEasy = new AchievementInfo(125, "Escaping Is Not Easy", "Change your equipment/change job while locked in a restraint set.");
    
    public static readonly AchievementInfo ICantBelieveYouveDoneThis = new AchievementInfo(126, "I Can't Believe You've Done This", "Get /slapped while bound.");

    // Secrets
    public static readonly AchievementInfo TooltipLogos = new AchievementInfo(127, "Hidden in Plain Sight", "???");

    public static readonly AchievementInfo Experimentalist = new AchievementInfo(128, "Experimentalist", "???");

    public static readonly AchievementInfo HelplessDamsel = new AchievementInfo(129, "Completely Helpless", "???");

    public static readonly AchievementInfo GaggedPleasure = new AchievementInfo(130, "Hrmph~!", "???");

    public static readonly AchievementInfo BondageClub = new AchievementInfo(131, "Bondage Club", "???");

    public static readonly AchievementInfo BadEndHostage = new AchievementInfo(132, "Bad End Hostage", "???");

    public static readonly AchievementInfo WorldTour = new AchievementInfo(133, "World Tour", "???");

    public static readonly AchievementInfo SilentProtagonist = new AchievementInfo(134, "The Silent Protagonist", "???");

    public static readonly AchievementInfo BoundgeeJumping = new AchievementInfo(135, "Boundgee Jumping", "???");

    public static readonly AchievementInfo KinkyTeacher = new AchievementInfo(136, "Perverted Teacher", "???");
    public static readonly AchievementInfo KinkyProfessor = new AchievementInfo(137, "Kinky Professor", "???");
    public static readonly AchievementInfo KinkyMentor = new AchievementInfo(138, "Kinky Mentor", "???");

    public static readonly AchievementInfo Overkill = new AchievementInfo(139, "Overkill", "???");

    public static readonly AchievementInfo WildRide = new AchievementInfo(140, "Wild Ride", "???");

    public static readonly AchievementInfo SlavePresentation = new AchievementInfo(141, "Slave Presentation", "???");


    // Full mapping to quickly go from Uint to AchievementInfo
    public static readonly Dictionary<uint, AchievementInfo> AchievementMap = new Dictionary<uint, AchievementInfo>
    {
        { 1, JustAVolunteer },
        { 2, AsYouCommand },
        { 3, AnythingForMyOwner },
        { 4, GoodDrone },
        { 5, BadSlut },
        { 6, NeedsTraining },
        { 7, UsefulInOtherWays },
        { 8, NewSlaveOwner },
        { 9, TaskManager },
        { 10, MaidMaster },
        { 11, QueenOfDrones },
        { 12, SelfApplied },
        { 13, SilenceSlut },
        { 14, WatchYourTongue },
        { 15, TongueTamer },
        { 16, OrchestratorOfSilence },
        { 17, ShushtainableResource },
        { 18, OfVoicelessPleas },
        { 19, PublicSpeaker },
        { 20, FromCriesOfHumility },
        { 21, SpeechSilverSilenceGolden },
        { 22, TheKinkyLegend },
        { 23, SilentButDeadly },
        { 24, ATrueGagSlut },
        { 25, GagReflex },
        { 26, QuietNowDear },
        { 27, YourFavoriteNurse },
        { 28, SayMmmph },
        { 29, FirstTiemers },
        { 30, Cuffed19 },
        { 31, TheRescuer },
        { 32, SelfBondageEnthusiast },
        { 33, DiDEnthusiast },
        { 34, CrowdPleaser },
        { 35, Humiliation },
        { 36, BondageBunny },
        { 37, ToDyeFor },
        { 38, DyeAnotherDay },
        { 39, DyeHard },
        { 40, RiggersFirstSession },
        { 41, MyLittlePlaything },
        { 42, SuitsYouBitch },
        { 43, TiesThatBind },
        { 44, SlaveTrainer },
        { 45, CeremonyOfEternalBondage },
        { 46, FirstTimeBondage },
        { 47, AmateurBondage },
        { 48, ComfortRestraint },
        { 49, YourBondageMaid },
        { 50, TrainedBondageSlave },
        { 51, ATrueBondageSlave },
        { 52, KinkyExplorer },
        { 53, TemptingFatesTreasure },
        { 54, BadEndSeeker },
        { 55, EverCursed },
        { 56, HealSlut },
        { 57, BondagePalace },
        { 58, HornyOnHigh },
        { 59, EurekaWhorethos },
        { 60, MyKinkRunsDeep },
        { 61, MyKinksRunDeeper },
        { 62, TrialOfFocus },
        { 63, TrialOfDexterity },
        { 64, TrialOfTheBlind },
        { 65, RunningGag },
        { 66, AuctionedOff },
        { 67, SoldSlave },
        { 68, Bondodge },
        { 69, WhoIsAGoodPet },
        { 70, ControlMyBody },
        { 71, CompleteDevotion },
        { 72, MasterOfPuppets },
        { 73, KissMyHeels },
        { 74, Ashamed },
        { 75, ShowingOff },
        { 76, FunForAll },
        { 77, DeviousComposer },
        { 78, CravingPleasure },
        { 79, PatternLover },
        { 80, EnduranceQueen },
        { 81, MyFavoriteToys },
        { 82, MotivationForRestoration },
        { 83, KinkyGambler },
        { 84, SubtleReminders },
        { 85, FingerOnTheTrigger },
        { 86, TriggerHappy },
        { 87, HornyMornings },
        { 88, NothingCanStopMe },
        { 89, AllTheCollarsOfTheRainbow },
        { 90, UCanTieThis },
        { 91, ForcedFollow },
        { 92, ForcedWalkies },
        { 93, TimeForWalkies },
        { 94, GettingStepsIn },
        { 95, WalkiesLover },
        { 96, LivingFurniture },
        { 97, WalkOfShame },
        { 98, BlindLeadingTheBlind },
        { 99, WhatAView },
        { 100, WhoNeedsToSee },
        { 101, PetTraining },
        { 102, NotGoingAnywhere },
        { 103, HouseTrained },
        { 104, IndulgingSparks },
        { 105, CantGetEnough },
        { 106, VerThunder },
        { 107, WickedThunder },
        { 108, ElectropeHasNoLimits },
        { 109, ShockAndAwe },
        { 110, ShockingExperience },
        { 111, ShockolateTasting },
        { 112, ShockAddiction },
        { 113, WarriorOfElectrope },
        { 114, ShockSlut },
        { 115, JustVibing },
        { 116, DontKillMyVibe },
        { 117, VibingWithFriends },
        { 118, TutorialComplete },
        { 119, AddedFirstPair },
        { 120, TheCollector },
        { 121, AppliedFirstPreset },
        { 122, HelloKinkyWorld },
        { 123, KnowsMyLimits },
        { 124, WarriorOfLewd },
        { 125, EscapingIsNotEasy },
        { 126, ICantBelieveYouveDoneThis },
        { 127, TooltipLogos },
        { 128, Experimentalist },
        { 129, HelplessDamsel },
        { 130, GaggedPleasure },
        { 131, BondageClub },
        { 132, BadEndHostage },
        { 133, WorldTour },
        { 134, SilentProtagonist },
        { 135, BoundgeeJumping },
        { 136, KinkyTeacher },
        { 137, KinkyProfessor },
        { 138, KinkyMentor },
        { 139, Overkill },
        { 140, WildRide },
        { 141, SlavePresentation },
        { 142, KinkyLibrarian },
        { 143, SilencedSlut },
        { 144, InDeepSilence },
        { 145, SilentObsessions },
        { 146, GoldenSilence },
        { 147, AKinkForDrool },
        { 148, ThePerfectGagSlut },
        { 149, DefianceInSilence },
        { 150, MuffledResilience },
        { 151, TrainedInSubSpeech },
        { 152, SilenceOfShame },
        { 153, YourRubberMaid },
        { 154, YourRubberSlut }
    };
}
