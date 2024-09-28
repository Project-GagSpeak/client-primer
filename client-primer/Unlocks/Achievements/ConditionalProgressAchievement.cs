using Dalamud.Plugin.Services;

namespace GagSpeak.Achievements;

public class ConditionalProgressAchievement : Achievement
{
    /// <summary>
    /// The Current Progress made towards the achievement.
    /// </summary>
    public int Progress { get; set; }

    /// <summary>
    /// What is required to be true throughout the progress from start to end
    /// </summary>
    public Func<bool> RequiredCondition { get; set; }

    /// <summary>
    /// Determine if we need the task begin and end to be set for the progress to increment.
    /// </summary>
    public bool RequireTaskBeginAndFinish { get; set; }

    /// <summary>
    /// If the task for earning one point towards the milestone has been met.
    /// </summary>
    public bool ConditionalTaskBegun { get; set; }

    /// <summary>
    /// If the task checkpoint rewarding a progress point has been met.
    /// </summary>
    public bool ConditionalTaskFinished { get; set; }

    public ConditionalProgressAchievement(INotificationManager notify, string title, string description, int goal,
        Func<bool> requiredState, bool requireTaskBeginAndFinish = true, string unit = "") 
        : base(notify, title, description, goal, unit)
    {
        RequiredCondition = requiredState;
        Progress = 0;
    }

    public override int CurrentProgress() => IsCompleted ? MilestoneGoal : Progress;

    public void BeginConditionalTask()
    {
        ConditionalTaskBegun = true;
        CheckTaskProgress();
    }

    public void FinishConditionalTask()
    {
        ConditionalTaskFinished = true;
        CheckTaskProgress();
    }

    public void CheckTaskProgress(int amountToIncOnSuccess = 1)
    {
        if (IsCompleted) return;

        if (!ConditionalTaskBegun && RequireTaskBeginAndFinish) return;

        // if we have failed the required condition, reset taskBegun to false.
        if (RequireTaskBeginAndFinish && ConditionalTaskBegun && !RequiredCondition())
        {
            ConditionalTaskBegun = false;
        }

        // if we have finished the task, increment the progress
        if ( (!RequireTaskBeginAndFinish || (ConditionalTaskBegun && ConditionalTaskFinished)) && RequiredCondition())
        {
            IncrementProgress(amountToIncOnSuccess);
            // reset the task progress.
            ConditionalTaskBegun = false;
            ConditionalTaskFinished = false;
        }
    }

    /// <summary>
    /// Increments the progress towards the achievement.
    /// </summary>
    private void IncrementProgress(int amountToIncOnSuccess)
    {
        Progress = Progress + amountToIncOnSuccess;
        // check for completion after incrementing progress
        CheckCompletion();
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

    public override AchievementType GetAchievementType() => AchievementType.ConditionalProgress;
}
