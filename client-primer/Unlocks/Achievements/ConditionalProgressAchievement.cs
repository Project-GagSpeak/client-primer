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

    public ConditionalProgressAchievement(INotificationManager notify, string title, string desc, int goal,
        Func<bool> cond, bool reqBeginAndFinish = true, string prefix = "", string suffix = "", bool isSecret = false)
        : base(notify, title, desc, goal, prefix, suffix, isSecret)
    {
        RequiredCondition = cond;
        Progress = 0;
        RequireTaskBeginAndFinish = reqBeginAndFinish;
        ConditionalTaskBegun = false;
        ConditionalTaskFinished = false;
    }

    public override int CurrentProgress() => IsCompleted ? MilestoneGoal : Progress;
    public override string ProgressString()
    {
        if(IsCompleted)
            return PrefixText + " " + MilestoneGoal + " / " + MilestoneGoal + " " + SuffixText;

        // if we have our conditonal started but not ended, mark we are tracking.
        if(ConditionalTaskBegun && !ConditionalTaskFinished)
            return PrefixText + " " + (CurrentProgress() + " / " + MilestoneGoal) + " " + SuffixText + " (Tracking)";

        // Otherwise, display normal conditon text.
        return PrefixText + " " + (CurrentProgress() + " / " + MilestoneGoal) + " " + SuffixText;
    }

    public async void BeginConditionalTask(int secondsDelayBeforeCheck = 0)
    {
        if (IsCompleted)
            return;

        // wait the delayed time before checking the conditional task.
        if (secondsDelayBeforeCheck > 0)
            await Task.Delay(secondsDelayBeforeCheck * 1000);

        if (!RequiredCondition())
            return;

        StaticLogger.Logger.LogDebug($"Beginning Conditional Task for {Title}");
        ConditionalTaskBegun = true;
    }

    public void FinishConditionalTask()
    {
        if (IsCompleted)
            return;

        StaticLogger.Logger.LogDebug($"Finishing Conditional Task for {Title}");
        ConditionalTaskFinished = true;
        CheckTaskProgress();
    }

    public void StartOverDueToInturrupt()
    {
        if (IsCompleted)
            return;

        StaticLogger.Logger.LogDebug($"Achievement {Title} Requires conditional Begin & End, but we inturrupted before reaching end. Starting Over!", LoggerType.Achievements);
        ConditionalTaskBegun = false;
        ConditionalTaskFinished = false;
    }

    public void CheckTaskProgress(int amountToIncOnSuccess = 1)
    {
        if (IsCompleted) 
            return;

        if (!ConditionalTaskBegun && RequireTaskBeginAndFinish) 
            return;

        // if we have failed the required condition, reset taskBegun to false.
        if (RequireTaskBeginAndFinish && ConditionalTaskBegun && !RequiredCondition())
        {
            StaticLogger.Logger.LogDebug($"Achievement {Title} Requires a conditional task, "
                + "and we failed conditional after it begun. Restarting!", LoggerType.Achievements);
            ConditionalTaskBegun = false;
            return;
        }
        // if we have finished the task, increment the progress
        if ((!RequireTaskBeginAndFinish || (ConditionalTaskBegun && ConditionalTaskFinished)) && RequiredCondition())
        {
            StaticLogger.Logger.LogInformation($"Achievement {Title} Had its Conditional Met from start to finish! Incrementing Progress!", LoggerType.Achievements);
            IncrementProgress(amountToIncOnSuccess);
            // reset the task progress.
            ConditionalTaskBegun = false;
            ConditionalTaskFinished = false;
            return;
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
