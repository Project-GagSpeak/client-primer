using System.Windows.Forms;

namespace GagSpeak.Achievements;

public static class OrderLabels
{
    // Just a Volunteer - Finish 1 Order
    public const string JustAVolunteer = "Just a Volunteer";
    // As You Command - Finish 10 Orders
    public const string AsYouCommand = "As You Command";
    // Anything For My Owner - Finish 100 Orders
    public const string AnythingForMyOwner = "Anything For My Owner";
    // Good Drone - Finish 1000 Orders
    public const string GoodDrone = "Good Drone";

    // Bad Slut - Fail 1 Order
    public const string BadSlut = "Bad Slut";
    // Needs Training - Fail 10 Orders
    public const string NeedsTraining = "Needs Training";
    // Useful In Other Ways - Fail 100 Orders
    public const string UsefulInOtherWays = "Useful In Other Ways";

    // New Slave Owner - Create 1 Order
    public const string NewSlaveOwner = "New Slave Owner";
    // Task Manager - Create 10 Orders
    public const string TaskManager = "Task Manager";
    // Maid Master - Create 100 Orders
    public const string MaidMaster = "Maid Master";
    // Queen of Drones - Create 1000 Orders
    public const string QueenOfDrones = "Queen of Drones";
}

public static class GagLabels
{
    // apply a gag to yourself. (not another user)
    public const string SelfApplied = "Does It Fit?";

    // apply a gag to another user.
    public const string ApplyToPair = "Silence, Slut";

    // have gags applied (from another, or applied to another):
    public const string LookingForTheRightFit = "Looking For The Right Fit"; // 10 times
    public const string OralFixation = "Oral Fixation"; // 100 times
    public const string AKinkForDrool = "A Kink for Drool"; // 1000 times.

    // Have all 3 gag layers with an active gag at the same time.
    public const string ShushtainableResource = "Sustainable Resource";

    // Speak Up, Slut - talk in gagspeak in:
    public const string SpeakUpSlut = "Speak Up, Slut"; // /say
    public const string CantHearYou = "I Can't Hear you";// /yell
    public const string OneMoreForTheCrowd = "Once More For The Crowd"; // /shout

    // Speech is Silver, Silence is Golden - Wear a gag for a full week.
    public const string SpeechSilverSilenceGolden = "Speech is Silver, Silence is Golden";

    // The Kinky Legend - Wear a gag for 2 weeks
    public const string TheKinkyLegend = "The Kinky Legend";

    // Silent but Deadly - Do 10 roulettes with a gag equipped
    public const string SilentButDeadly = "Silent but Deadly";

    // A true gag slut - be gagged by 10 different people in less than 1 hour
    public const string ATrueGagSlut = "A True Gag Slut";

    // Quiet Now, Dear - using /shush while targeting a gagged player
    public const string QuietNowDear = "Quiet Now, Dear";

    // our Favorite Nurse - Play a pattern, apply a restraint set, or apply a gag to another pair while you have the Mask Gag Equipped 20 times
    public const string YourFavoriteNurse = "Your Favorite Nurse";

    // Say Mmmph! - take a screenshot in /gpose while gagged
    public const string SayMmmph = "Say Mmmph!~";
}

public static class WardrobeLabels
{
    /* ----------------- Basic ----------- */
    // First Tiemers - Apply your first restraint (or have one applied)
    public const string FirstTiemers = "First Tiemers";

    // get your hands tied 19 times -> "Cuffed-19"
    public const string Cuffed19 = "Cuffed-19";

    // Unlock 100 Restraints from someone other than yourself
    public const string TheRescuer = "The Rescuer";

    // Apply a restraint to yourself 100 times.
    public const string SelfBondageEnthusiast = "DIY DiD";

    // Apply a restraint set to someone else 100 times.
    public const string DiDEnthusiast = "Making a Damsel out of You."; // 100 times.

    // Crowd pleaser - Be restrained with more than 15 people around you
    public const string CrowdPleaser = "Crowd Pleaser";

    // Be Restrained with at least 5 other visible GagSpeak Pairs around you
    public const string Humiliation = "Lesson in Humiliation";

    // Bondage Bunny - Be Restraintd by 5 different people in less than 2 hours.
    public const string BondageBunny = "Bondage Bunny";

    // Dye a restraint set:
    public const string ToDyeFor = "To Dye For"; // once
    public const string DyeAnotherDay = "Dye Another Day"; // 5 times
    public const string DyeHard = "Dye Hard"; // 15 times

    // Lock someone in a restraint set for:
    public const string RiggersFirstSession = "Riggers First Session"; // 30m
    public const string MyLittlePlaything = "My Little Plaything"; // 1h
    public const string SuitsYouBitch = "Suits you, Bitch!"; // 6h
    public const string TiesThatBind = "Ties That Bind"; // 1day
    public const string SlaveTraining = "Slave Training"; // 1week
    public const string CeremonyOfEternalBondage = "Ceremony Of Eternal Bondage"; // 1month

    // Endure a Locked Restraint set For:
    public const string FirstTimeBondage = "First-Time Bondage"; // 30m
    public const string AmateurBondage = "Amateur Bondage"; // 1h
    public const string ComfortRestraint = "Comforting Restraints"; // 6h
    public const string DayInTheLifeOfABondageSlave = "A Day in the Bondage"; // 1day
    public const string AWeekInBondage = "Week-Long Bondage Training"; // 1week
    public const string AMonthInBondage = "A True Bondage Slave"; // 1month

    // Kinky Explorer - Run a dungeon with Cursed Bondage Loot enabled.
    public const string KinkyExplorer = "Kinky Explorer";

    // Tempting Fate's Treasure - Be Caught in Cursed Bondage Loot for the first time.
    public const string TemptingFatesTreasure = "Tempting Fate's Treasure";

    // Bad End Seeker - Get trapped in Cursed Bondage Loot 25 times..
    public const string BadEndSeeker = "Bad End Seeker";

    // EverCursed - Be caught in Cursed Bondage Loot 100 times.
    public const string EverCursed = "Ever Cursed";



    /* ------------- Unique ----------- */

    // Complete a duty as a healer job while wearing a gag or restraint set (or have a vibe running)
    public const string HealSlut = "HealSlut";

    // Deep Dungeon Achivements:
    public const string BondagePalace = "Bondage Palace"; // Floor 50 / 100 of PotD while bound
    public const string HornyOnHigh = "Horny on High"; // Floor 30 of HoH while bound
    public const string EurekaWhorethos = "Eureka Whore-thos"; // Floor 30 of EO while bound

    // My Kinks Run Deep - complete a deep dungeon with hardcore stimulation or a hardcore restraint
    public const string MyKinkRunsDeep = "My Kinks Run Deep";
    // My Kinks Run Deeper - solo a deep dungeon with hardcore stimulation or a hardcore restraint
    public const string MyKinksRunDeeper = "My Kinks Run Deeper";

    // Complete a Trial within 10 Levels of max level with Hardcore Properties on:
    public const string TrialOfFocus = "Trial Of Focus"; // Stimulation
    public const string TrialOfDexterity = "Trial of Dexterity"; // Restrained Arms/Legs
    public const string TrialOfTheBlind = "Trial Of The Blind"; // Blindfolded

    // While actively moving, incorrectly guess a restraint lock while gagged.
    public const string RunningGag = "Running Gag";

    // Auctioned Off - Have a Restraint Set Enabled by one GagSpeak user be removed by a different GagSpeak user
    public const string AuctionedOff = "Auctioned Off";

    // Sold Slave - Have a Password Locked Restraint set on you that was locked by one GagSpeak user, be unlocked by another GagSpeak use
    public const string SoldSlave = "Sold Slave";

    // Bondodge - Within 2 seconds of having a restraint set applied to you, remove it from yourself
    public const string Bondodge = "Bondodge";
}

public static class PuppeteerLabels
{
    // Be ordered to sit by another pair through puppeteer. (can work both ways)
    public const string WhoIsAGoodPet = "Who's a good pet~?";

    // Control my body - Have another pair enable All Motions in puppeteer for you.
    public const string ControlMyBody = "Control My Body";

    // Complete Devotion - have a pair enable puppeteer all commands for you
    public const string CompleteDevotion = "Complete Devotion";

    // Master of Puppets - puppeteer someone 10 times in an hour
    public const string MasterOfPuppets = "Master of Puppets";

    // Kiss my Heels - Order someone to /grovel 20 times with puppeteer
    public const string KissMyHeels = "Kiss my Heels";

    // Ashamed - Be forced to /sulk
    public const string Ashamed = "Ashamed";

    // Showing Off - Order someone to execute any emote with "dance" in it X times
    public const string ShowingOff = "Showing Off";
}

public static class ToyboxLabels
{
    // Create a Publish a pattern for the first time.
    public const string FunForAll = "Fun for all";

    // DeviousComposer - Create 10 patterns
    public const string DeviousComposer = "Devious Composer";

    // Craving Pleasure - Download 30 patterns.
    public const string CravingPleasure = "Craving Pleasure";

    // Like 30 Patterns
    public const string PatternLover = "Pattern Lover";

    // Endurance King/Queen - Play a pattern for an hour without pause
    public const string EnduranceQueen = "Endurance King/Queen";

    // My Favorite Toys - Connect a real device (vibrator, pishock, etc) to Gagspeak
    public const string MyFavoriteToys = "My Favorite Toys";

    // Motivation for Restoration: Play a pattern for over 30 minutes in Diadem
    public const string MotivationForRestoration = "Motivation for Restoration";

    // Kinky Gambler - complete a deathroll (win or loss) while you have at least one trigger active for losing it
    public const string KinkyGambler = "Kinky Gambler";

    // SubtleReminders - Have a trigger fire off successfully 10 times.
    public const string SubtleReminders = "Subtle Reminders";

    // FingerOnTheTrigger - Have a trigger fire off successfully 100 times.
    public const string FingerOnTheTrigger = "Finger on the Trigger";

    // TriggerHappy - Have a trigger fire off successfully 1000 times.
    public const string TriggerHappy = "Trigger Happy";

    // HornyMornings - Have an alarm go off
    public const string HornyMornings = "Horny Mornings";

    // Nothing can stop me -  Kill 500 enemies in pvp front while restrained7vibed
    public const string NothingCanStopMe = "Nothing can stop me";
}

public static class HardcoreLabels
{
    // Force 20 different pairs to follow you.
    public const string AllTheCollarsOfTheRainbow = "All the Collars of the Rainbow";

    // U Can't Tie This - Force someone to follow you, or be forced to follow someone, throughout a full duty to the end.
    public const string UCanTieThis = "U Can't Tie This";

    // Force someone to follow you For:
    public const string ForcedFollow = "Follow Along Now"; // 1m
    public const string ForcedWalkies = "It's the Leash of Your Worries"; // 5m

    // Time for Walkies - Be forced to follow someone For:
    public const string TimeForWalkies = "Time for Walkies"; // 1m
    public const string GettingStepsIn = "Getting Your Steps In"; // 5m
    public const string WalkiesLover = "Walkies Lover"; // 10m

    // Part of the Furniture - Be forced to sit for 1 hour or more
    public const string LivingFurniture = "Living Art";

    // Shown Off- Be bound blindfolded and leashed in a major city
    public const string WalkOfShame = "Walk of Shame";
    // Blind leading the blind - be blindfolded wail having someone following you blindfolded
    public const string BlindLeadingTheBlind = "Blind Leading the Blind";
    // What A View - use the /lookout emote while wearing a blindfold
    public const string WhatAView = "What A View";

    // Who needs to see? - Be blindfolded in hardcore mode for 3 hours
    public const string WhoNeedsToSee = "Who Needs to See?";

    // Pet training - Forced to stay in someone's house/FC house/Apartment:
    public const string PetTraining = "House Pet Training"; // 30m
    public const string NotGoingAnywhere = "Not Going Anywhere"; // 1 hour
    public const string HouseTrained = "House Trained"; // 1 day

    // Slave harem - Be part of a group of 5+ slaves all forced to stay in the same room

    // Give out Shocks:
    public const string IndulgingSparks = "Indulging Sparks"; // 10 times
    public const string CantGetEnough = "Can't Get Enough"; // 100 times
    public const string VerThunder = "Ver-Thunder"; // 1000 times
    public const string WickedThunder = "Wicked Thunder"; // 10000 times
    public const string ElectropeHasNoLimits = "Electrope Has No Limits"; // 25000 times

    // Get Shocked:
    public const string ShockAndAwe = "Shock and Awe"; // 10 times
    public const string ShockingExperience = "Shocking Experience"; // 100 times
    public const string ShockolateTasting = "Shockolate Tasting"; // 1000 times
    public const string ShockAddiction = "Shock Addiction"; // 10,000 times
    public const string WarriorOfElectrope = "Warrior of Electrope"; // 25000 times
    public const string ShockSlut = "Shock Slut"; // 50,000 Times

    // Tamed Brat" having shock collar beep or viberate 10 times without a follow up shock
    public const string TamedBrat = "Tamed Brat";
}

public static class RemoteLabels
{
    // Just Vibing - use the remote window for the first time
    public const string JustVibing = "Just Vibing";

    // Don't Kill My Vibe - turn the remote down from 100% to 0% in under a second
    public const string DontKillMyVibe = "Don't Kill My Vibe";

    // Vibing with the Group - Host a Vibe Server Vibe Room.
    public const string VibingWithFriends = "Vibing with Friends";
}

public static class GenericLabels
{
    // Complete the Tutorial.
    public const string TutorialComplete = "Welcome To GagSpeak!";

    // Add your first pair.
    public const string AddedFirstPair = "Social Kinkster";

    // Have 20 pairs.
    public const string TheCollector = "The Collector";

    // Apply a preset for a pair, defining the boundaries of your contact.
    public const string AppliedFirstPreset = "Boundary Definer";

    // Knows My Limits - Use the safeword for the first time
    public const string KnowsMyLimits = "Knows My Limits";

    // Escaping Isn't Easy - change your equipment/change job while locked in a restraint set 
    public const string EscapingIsNotEasy = "Escaping Isn't Easy";

    // Hello Kinky World! - use the gagspeak global chat for the first time
    public const string HelloKinkyWorld = "Hello Kinky World!";

    // Warrior of Lewd: View a cutscene while bound (be in one for 30s
    public const string WarriorOfLewd = "Warrior of Lewd";

    // I can't believe you've done this. - Get /slapped while bound
    public const string ICantBelieveYouveDoneThis = "Can't believe you've done this";
}

public static class SecretLabels
{
    // Have to click on all the modules logo icons to unlock this achievement
    public const string TooltipLogos = "Hidden in Plain Sight";

    // have a gag, restraint, active toy, active trigger, active alarm, active pattern, all on at once.
    public const string Experimentalist = "Experimentalist";

    // be in hardcore mode, while actively forced to follow or sit, a toy actively playing, an active restraint,
    // and active gag, send a garbled message in chat.
    public const string HelplessDamsel = "Completely Helpless";

    // Hrmph~! - be gaged and vibrated at the same time.
    public const string GaggedPleasure = "Hrmph~!";

    // Bondage Club - have at least 8 pairs visible/near you at the same time
    public const string BondageClub = "Bondage Club";

    // BadEndHostage - Get KO'd while in a restraint set
    public const string BadEndHostage = "Bad End Hostage";

    // World Tour - Visit every major city Aetheryte plaza while bound, with no breaks in between 2m in each city.
    public const string WorldTour = "World Tour";

    // The Silent Protagonist (hidden) - Be gagged while you have an active quest objective requiring you to /say something.
    public const string SilentProtagonist = "The Silent Protagonist";

    // Jump off a cliff while in bondage: "Boundgee Jumping" (Fall Damage spawns an action effect from it)
    public const string BoundgeeJumping = "Boundgee Jumping";

    // Perverted teacher - Get 10 commendations while bound
    public const string KinkyTeacher = "Perverted Teacher";

    // Kinky Professor - Get 50 commendations while bound
    public const string KinkyProfessor = "Kinky Professor";

    // perverted mentor - Get 100 commends while bound
    public const string KinkyMentor = "Kinky Mentor";

    // How did we get here - Have a Restraint set Active while having the max allowed statuses on you (possible?)
    public const string HowDidWeGetHere = "How Did We Get Here";

    // As if things couldnt get any worse - Get 90k'ed while in bondage.
    public const string AsIfThingsCouldntGetAnyWorse = "As If Things Couldn't Get Any Worse";

    // "Overkill - Bind someone on all available slots"...
    public const string Overkill = "Overkill";

    // "Opportunist - Bind someone who just recently restrained anyone"
    public const string Opportunist = "Opportunist";

    // Wild Ride - Win a chocobo race with a restraint set equiped
    public const string WildRide = "Wild Ride";

    // Bound Triad - Win a triple triad match against another gagspeak user (both must be bound)
    public const string BoundTriad = "Bound Triad";

    // "My First Collar" put on a leather choker or any of the variants that has your doms name as the creator
    public const string MyFirstCollar = "My First Collar";

    // Obedient Servant - Do a custom delivery while in a restaint set
    public const string ObedientServant = "Obedient Servant";

    // Slave presentation . doing the fashion report while being restrained and gagged
    public const string SlavePresentation = "Slave Presentation";
}
