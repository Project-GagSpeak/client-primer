namespace GagSpeak.Achievements;

public class ProgressAchievement : Achievement
{
    /// <summary>
    /// The Current Progress made towards the achievement.
    /// </summary>
    public int Progress { get; private set; }

    /// <summary>
    /// The Milestone that must be met to complete the achievement.
    /// </summary>
    public int MilestoneGoal { get; private set; }

    public ProgressAchievement(string title, string description, int requiredProgress)
        : base(title, description)
    {
        MilestoneGoal = requiredProgress;
        Progress = 0;
    }

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
}
