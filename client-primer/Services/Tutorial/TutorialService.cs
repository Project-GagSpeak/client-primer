using Dalamud.Interface.Colors;
using GagSpeak.UI;
using ImGuiNET;
using OtterGui.Services;
using System.Runtime.CompilerServices;

// A Modified take on OtterGui.Widgets.Tutorial.
// This iteration removes redundant buttons, adds detailed text, and sections.
namespace GagSpeak.Services.Tutorial;

/// <summary> Service for the in-game tutorial. </summary>
public class TutorialService
{
    private readonly UiSharedService _uiShared;

    private readonly Dictionary<TutorialType, Tutorial> _tutorials = new();

    public TutorialService(UiSharedService uiShared)
    {
        _uiShared = uiShared;

        // Initialize tutorials for each TutorialType
        foreach (TutorialType type in Enum.GetValues<TutorialType>())
        {
            _tutorials[type] = new Tutorial(_uiShared)
            {
                BorderColor = ImGui.GetColorU32(ImGuiColors.ParsedPink),
                HighlightColor = ImGui.GetColorU32(ImGuiColors.ParsedGold),
                PopupLabel = $"{type} Tutorial",
            };
        }
    }

    public bool IsTutorialActive(TutorialType type) => _tutorials[type].CurrentStep is not -1;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void StartTutorial(TutorialType guide)
    {
        if (!_tutorials.ContainsKey(guide))
            return;

        // set all other tutorials to -1, stopping them.
        foreach (var t in _tutorials)
            t.Value.CurrentStep = (t.Key != guide) ?  -1 : 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void OpenTutorial(TutorialType guide, int step)
    {
        if (_tutorials.TryGetValue(guide, out var tutorial))
            tutorial.Open(step);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SkipTutorial(TutorialType guide)
    {
        // reset the step to -1, stopping the tutorial.
        if (_tutorials.TryGetValue(guide, out var tutorial))
            tutorial.CurrentStep = -1;
    }

    // Create a mappinng between the tutorialTypes and the associated enum size.
    private static readonly Dictionary<TutorialType, int> _tutorialSizes = new()
    {
        { TutorialType.MainUi, Enum.GetValues<StepsMainUi>().Length },
        { TutorialType.Remote, Enum.GetValues<StepsRemote>().Length },
        { TutorialType.Gags, Enum.GetValues<StepsActiveGags>().Length },
        { TutorialType.GagStorage, Enum.GetValues<StepsGagStorage>().Length },
        { TutorialType.Restraints, Enum.GetValues<StepsRestraints>().Length },
        { TutorialType.CursedLoot, Enum.GetValues<StepsCursedLoot>().Length },
        { TutorialType.Toybox, Enum.GetValues<StepsToybox>().Length },
        { TutorialType.Patterns, Enum.GetValues<StepsPatterns>().Length },
        { TutorialType.Triggers, Enum.GetValues<StepsTriggers>().Length },
        { TutorialType.Alarms, Enum.GetValues<StepsAlarms>().Length },
        { TutorialType.Achievements, Enum.GetValues<StepsAchievements>().Length },
    };
}
