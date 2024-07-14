namespace GagSpeak.UI.Components;

public enum WardrobeTabSelection
{
    None, // shouldn't see this ever
    ViewActiveSet, // lets you see info about the active set
    RestraintSetInspector, // view the list of your sets and see combined overview.
    AddRestraintSet, // interface for creating a new restraint set.
    EditRestraintSet, // interface for editing an existing restraint set. (can heavily rip from add set)
    ProfileDisplayEdits, // lets you add custom effects to restraint set display in profile.
}

/// <summary> Tab Menu for the GagSetup UI </summary>
public class WardrobeTabMenu : TabMenuBase
{
    /// <summary> Defines the type of tab selection to use. </summary>
    protected override Type TabSelectionType => typeof(WardrobeTabSelection);

    public WardrobeTabMenu() { }
}
