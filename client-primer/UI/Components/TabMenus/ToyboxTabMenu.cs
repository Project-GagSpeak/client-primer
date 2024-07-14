namespace GagSpeak.UI.Components;

public enum ToyboxTabSelection
{
    None, // should never be here except on startup.
    ToyManager, // manage connected and active toys. configure simulated vibe ext.
    PatternManager, // manage, create, or send patterns.
    TriggersManager, // manage, create, or send triggers.
    AlarmManager, // manage, create, or send alarms.
    PlayerSounds, // configure the sounds client makes when various toybox actions occur. (maybe merge into toy manager)
    ProfileCosmetics, // lets you add custom display effects related to toybox actions.
}

/// <summary> Tab Menu for the GagSetup UI </summary>
public class ToyboxTabMenu : TabMenuBase
{
    /// <summary> Defines the type of tab selection to use. </summary>
    protected override Type TabSelectionType => typeof(ToyboxTabSelection);

    public ToyboxTabMenu() { }
}
