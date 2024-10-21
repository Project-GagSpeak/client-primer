using Dalamud.Plugin.Services;

namespace GagSpeak.Achievements;

public class ProgressAchievement : Achievement
{
    /// <summary>
    /// The Current Progress made towards the achievement.
    /// </summary>
    public int Progress { get; set; }

    public ProgressAchievement(INotificationManager notify, string title, string desc, int goal, string prefix = "", string suffix = "", bool isSecret = false)
        : base(notify, title, desc, goal, prefix, suffix, isSecret)
    {
        Progress = 0;
    }

    public override int CurrentProgress() => IsCompleted ? MilestoneGoal : Progress;

    public override string ProgressString() => PrefixText + " " + (CurrentProgress() + " / " + MilestoneGoal) + " " + SuffixText;

    /// <summary>
    /// Increments the progress towards the achievement.
    /// </summary>
    public void IncrementProgress(int amount = 1)
    {
        if (IsCompleted) 
            return;

        StaticLogger.Logger.LogDebug($"Incrementing Progress by 1 for {Title}. Total Required: {MilestoneGoal}", LoggerType.Achievements);
        Progress += amount;
        // check for completion after incrementing progress
        CheckCompletion();
    }

    /// <summary>
    /// Check if the Milestone has been met.
    /// </summary>
    public override void CheckCompletion()
    {
        if (IsCompleted) return;

        if (Progress >= MilestoneGoal)
        {
            // Mark the achievement as completed
            MarkCompleted();
        }
    }
    public override AchievementType GetAchievementType() => AchievementType.Progress;
}
