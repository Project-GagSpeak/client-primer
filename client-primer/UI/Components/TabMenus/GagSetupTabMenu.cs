namespace GagSpeak.UI.Components;

public enum GagsetupTabSelection
{
    None, // should never be here except on startup.
    ActiveGags, // for displaying the status of our 3 gag layers.
    Lockpicker, // feature-creep addon for minigames to lockpick gags.
    GagStorage, // for configuring the Glamour for our gags.
    Cosmetics, // lets you add custom overlay effects to gag images (supporter only)
}

/// <summary> Tab Menu for the GagSetup UI </summary>
public class GagSetupTabMenu : TabMenuBase
{
    /// <summary> Defines the type of tab selection to use. </summary>
    protected override Type TabSelectionType => typeof(GagsetupTabSelection);

    public GagSetupTabMenu() { }
}
