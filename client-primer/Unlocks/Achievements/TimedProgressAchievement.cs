using Dalamud.Plugin.Services;

namespace GagSpeak.Achievements;

public class TimedProgressAchievement : Achievement
{
    /// <summary>
    /// The Current Progress made towards the achievement.
    /// </summary>
    public int Progress { get; set; }

    /// <summary>
    /// The DateTime when the progress went from 0 to 1
    /// </summary>
    public DateTime StartTime { get; set; } = DateTime.MinValue;
    /// <summary>
    /// How long you have to earn the achievement in.
    /// </summary>
    public TimeSpan TimeToComplete { get; private set; }

    public TimedProgressAchievement(INotificationManager notify, string title, string desc, int goal, TimeSpan timeLimit, string unit = "", bool isSecret = false)
        : base(notify, title, desc, goal, unit, isSecret)
    {
        TimeToComplete = timeLimit;
        Progress = 0;
    }

    public override int CurrentProgress() => IsCompleted ? MilestoneGoal : Progress;

    /// <summary>
    /// Increments the progress towards the achievement.
    /// </summary>
    public void IncrementProgress(int amount = 1)
    {
        if (IsCompleted) return;

        CheckTimeLimit();
        Progress += amount;
        // check for completion after incrementing progress
        CheckCompletion();
    }

    private void CheckTimeLimit()
    {
        if (IsCompleted) return;

        // start the timer if the progress is 0
        if (Progress == 0)
        {
            StartTime = DateTime.UtcNow;
        }
        // reset the progress if we've exceeded the required time limit.
        else if (DateTime.UtcNow - StartTime >= TimeToComplete)
        {
            ResetProgress();
        }
    }

    /// <summary>
    /// Reset the progression of the achievement.
    /// </summary>
    public void ResetProgress() => Progress = 0;


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

    public override AchievementType GetAchievementType() => AchievementType.TimedProgress;
}
