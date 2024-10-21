using Dalamud.Plugin.Services;
using System;

namespace GagSpeak.Achievements;

public class ConditionalThresholdAchievement : Achievement
{

    /// <summary>
    /// The condition that must be met to complete the achievement
    /// </summary>
    private Func<bool> Condition;

    /// <summary>
    /// The condition that determines what our threshold is.
    /// </summary>
    private int LastRecordedThreshold { get; set; }

    public ConditionalThresholdAchievement(INotificationManager notify, string title, string desc, int goal, Func<bool> condition,
        string prefix = "", string suffix = "", bool isSecret = false) : base(notify, title, desc, goal, prefix, suffix, isSecret)
    {
        Condition = condition;
        LastRecordedThreshold = 0;
    }

    public override int CurrentProgress() => IsCompleted ? MilestoneGoal : LastRecordedThreshold;

    public override string ProgressString() => PrefixText + " " + (CurrentProgress() + " / " + MilestoneGoal) + " " + SuffixText;

    public void UpdateThreshold(int newestThreshold)
    {
        if (IsCompleted) 
            return;

        // if the condition is met, we should update it, otherwise, we should reset the threshold down to 0.
        if(Condition())
        {
            LastRecordedThreshold = newestThreshold;
            StaticLogger.Logger.LogDebug($"Updating Threshold for {Title}. Current Threshold: {LastRecordedThreshold}" +
                $" -- Total Required: {MilestoneGoal}", LoggerType.Achievements);
            CheckCompletion();
        }
        else
        {
            LastRecordedThreshold = 0;
        }
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

    public override AchievementType GetAchievementType() => AchievementType.ConditionalThreshold;
}




