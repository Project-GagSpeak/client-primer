using Dalamud.Plugin.Services;

namespace GagSpeak.Achievements;

public class ProgressAchievement : Achievement
{
    /// <summary>
    /// The Current Progress made towards the achievement.
    /// </summary>
    public int Progress { get; set; }

    public ProgressAchievement(INotificationManager notify, string title, string desc, int goal, string unit = "")
        : base(notify, title, desc, goal, unit)
    {
        Progress = 0;
    }

    public override int CurrentProgress() => IsCompleted ? MilestoneGoal : Progress;

    /// <summary>
    /// Increments the progress towards the achievement.
    /// </summary>
    public void IncrementProgress(int amount = 1)
    {
        if (IsCompleted) return;

        Progress += amount;
        // check for completion after incrementing progress
        CheckCompletion();
    }

    /// <summary>
    /// Reset the progression of the achievement.
    /// </summary>
    public void ResetProgress()
    {
        Progress = 0;
    }


    /// <summary>
    /// Check if the Milestone has been met.
    /// </summary>
    public override void CheckCompletion()
    {
        if (Progress >= MilestoneGoal)
        {
            // Mark the achievement as completed
            MarkCompleted();
        }
    }
    public override AchievementType GetAchievementType() => AchievementType.Progress;
}
