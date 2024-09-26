namespace GagSpeak.Achievements;

public enum AchievementType
{
    OrdersModule,
    GagsModule,
    WardrobeModule,
    PuppeteerModule,
    ToyboxModule,
    HardcoreModule,
    RemotesModule,
    GenericModule,
    SecretsModule,
}

public enum UnlocksEvent
{
    OrderAction, // for finished, failed, and created actions.
    
    GagAction, // Contains the layer, type, and if it was self applied.
    GagUnlockGuessFailed,

    RestraintApplied, // Contains the set idx applied, set name, and if it was self applied.
    RestraintLockChange,
    RestraintUnlockGuessFailed,
    PairRestraintLockChange,

    PuppeteerMessageSend,
    PuppeteerAccessGiven, // true == all permissions, false == emote permissions. (yes, im lazy)

    PatternAction,
    DeviceConnected,
    TriggerFired,
    DeathRollCompleted,
    ShockSent,
    ShockReceived,
    AlarmToggled,
    PvpPlayerSlain,
    ClientSlain,

    HardcoreForcedPairAction,

    RemoteOpened,
    VibeRoomCreated,

    //////// Generics & Secrets Below /////////
    ChatMessageSent, // chat type, message, and sender.
    PuppeteerEmoteSent, // emote used in string value.
    PlayerEmoteExecuted, // contains emote used in string value.
    TutorialCompleted,
    PairAdded,
    PresetApplied,
    GlobalSent,
    CursedDungeonLootFound,
    EasterEggFound,
    ChocoboRaceFinished,

}

public enum HardcorePairActionKind
{
    ForcedFollow,
    ForcedSit,
    ForcedStay,
    ForcedBlindfold,
}

public enum PatternInteractionKind
{
    Published,
    Downloaded,
    Liked,
    Started,
    Stopped,
}

public enum OrderInteractionKind
{
    Create,
    Started,
    Completed,
    Fail,
}
