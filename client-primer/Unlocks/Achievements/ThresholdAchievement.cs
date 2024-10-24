using Dalamud.Plugin.Services;
using System;

namespace GagSpeak.Achievements;

public class ThresholdAchievement : Achievement
{
    /// <summary>
    /// The condition that determines what our threshold is.
    /// </summary>
    private int LastRecordedThreshold { get; set; }

    public ThresholdAchievement(INotificationManager notify, string title, string desc, int goal,
        string prefix = "", string suffix = "", bool isSecret = false) : base(notify, title, desc, goal, prefix, suffix, isSecret)
    {
        LastRecordedThreshold = 0;
    }

    public override int CurrentProgress() => IsCompleted ? MilestoneGoal : LastRecordedThreshold;

    public override string ProgressString() => PrefixText + " " + (CurrentProgress() + " / " + MilestoneGoal) + " " + SuffixText;

    public void UpdateThreshold(int threshold)
    {
        if (IsCompleted) 
            return;

        LastRecordedThreshold = threshold;
        StaticLogger.Logger.LogDebug($"Updating Threshold for {Title}. Current Threshold: {LastRecordedThreshold}" +
            $" -- Total Required: {MilestoneGoal}", LoggerType.Achievements);
        CheckCompletion();
    }


    /// <summary>
    /// Check if the condition is satisfied
    /// </summary>
    public override void CheckCompletion()
    {
        if (IsCompleted) return;

        if (LastRecordedThreshold >= MilestoneGoal)
        {
            // Mark the achievement as completed
            MarkCompleted();
        }
    }

    public override AchievementType GetAchievementType() => AchievementType.Threshold;
}




