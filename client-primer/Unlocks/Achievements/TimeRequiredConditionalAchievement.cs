using Dalamud.Plugin.Services;

namespace GagSpeak.Achievements;

public class TimeRequiredConditionalAchievement : Achievement
{
    private readonly TimeSpan MilestoneDuration;
    public DateTime StartPoint { get; set; } = DateTime.MinValue;
    public Func<bool> RequiredCondition;
    public DurationTimeUnit TimeUnit { get; init; }
    private CancellationTokenSource _cancellationTokenSource;
    private bool _taskStarted = false;

    public TimeRequiredConditionalAchievement(INotificationManager notify, string name, string desc, TimeSpan dur, 
        Func<bool> cond, DurationTimeUnit unit, string prefix = "", string suffix = "", bool isSecret = false) 
        : base(notify, name, desc, ConvertToUnit(dur, unit), prefix, suffix, isSecret)
    {
        MilestoneDuration = dur;
        RequiredCondition = cond;
        TimeUnit = unit;
    }

    private static int ConvertToUnit(TimeSpan duration, DurationTimeUnit unit)
    {
        return unit switch
        {
            DurationTimeUnit.Seconds => (int)duration.TotalSeconds,
            DurationTimeUnit.Minutes => (int)duration.TotalMinutes,
            DurationTimeUnit.Hours => (int)duration.TotalHours,
            DurationTimeUnit.Days => (int)duration.TotalDays,
            _ => throw new ArgumentOutOfRangeException(nameof(unit), "Invalid time unit")
        };
    }

    public override int CurrentProgress()
    {
        if (IsCompleted) return MilestoneGoal;

        var elapsed = StartPoint != DateTime.MinValue ? DateTime.UtcNow - StartPoint : TimeSpan.Zero;

        return TimeUnit switch
        {
            DurationTimeUnit.Seconds => (int)elapsed.TotalSeconds,
            DurationTimeUnit.Minutes => (int)elapsed.TotalMinutes,
            DurationTimeUnit.Hours => (int)elapsed.TotalHours,
            DurationTimeUnit.Days => (int)elapsed.TotalDays,
            _ => 0
        };
    }

    public override string ProgressString()
    {
        if (IsCompleted) 
            return PrefixText + " " + MilestoneGoal + " " + TimeUnit + " " + SuffixText;

        if(StartPoint == DateTime.MinValue)
            return PrefixText + " 0s / " + MilestoneGoal + " " + TimeUnit + " " + SuffixText;

        var elapsed = MilestoneDuration - (StartPoint != DateTime.MinValue ? DateTime.UtcNow - StartPoint : TimeSpan.Zero);
        string outputStr = "";
        if (elapsed == TimeSpan.Zero)
        {
            outputStr = "0s";
        }
        else
        {
            if (elapsed.Days > 0) outputStr += elapsed.Days + "d ";
            if (elapsed.Hours > 0) outputStr += elapsed.Hours + "h ";
            if (elapsed.Minutes > 0) outputStr += elapsed.Minutes + "m ";
            if (elapsed.Seconds >= 0) outputStr += elapsed.Seconds + "s ";
            outputStr += " Elapsed";
        }
        // Add the Ratio
        return PrefixText + " " + outputStr + " / " + MilestoneGoal + " " + TimeUnit + " " + SuffixText;
    }

    public override void CheckCompletion()
    {
        if (IsCompleted)
            return;

        if (RequiredCondition())
        {
            if (StartPoint != DateTime.MinValue && DateTime.UtcNow - StartPoint >= MilestoneDuration)
                CompleteTask();
        }
        else
        {
            StaticLogger.Logger.LogDebug($"Condition for {Title} not met. Resetting the timer.", LoggerType.Achievements);
            ResetTask();
        }
    }

    // Method to Start the Task/Timer
    public void StartTask()
    {
        if (IsCompleted || _taskStarted)
            return;

        if (RequiredCondition())
        {
            StaticLogger.Logger.LogDebug($"Condition for {Title} met. Starting the timer.", LoggerType.Achievements);
            StartPoint = DateTime.UtcNow;
            _taskStarted = true;
            StartTimer();
        }
    }

    // Method to interrupt and reset the task
    public void InterruptTask()
    {
        if (IsCompleted)
            return;

        StaticLogger.Logger.LogDebug($"Interrupting task for {Title}.", LoggerType.Achievements);
        _taskStarted = false;
        ResetTask();
    }

    // Method to Complete the Task when time and condition are met
    private void CompleteTask()
    {
        StaticLogger.Logger.LogDebug($"Time and condition met for {Title}. Marking as completed.", LoggerType.Achievements);
        MarkCompleted();
        _cancellationTokenSource?.Cancel();
        _taskStarted = false;
    }

    // Starts the timer task
    private void StartTimer()
    {
        _cancellationTokenSource = new CancellationTokenSource();
        var token = _cancellationTokenSource.Token;

        Task.Run(async () =>
        {
            try
            {
                await Task.Delay(MilestoneDuration, token);
                if (!token.IsCancellationRequested && RequiredCondition())
                    CompleteTask(); // reset if we take longer than the requirement.
            }
            catch (TaskCanceledException)
            {
                StaticLogger.Logger.LogDebug($"Timer for {Title} was canceled.", LoggerType.Achievements);
            }
        }, token);
    }

    // Resets the task if condition fails or is interrupted
    private void ResetTask()
    {
        StartPoint = DateTime.MinValue;
        _cancellationTokenSource?.Cancel();
    }

    public override AchievementType GetAchievementType() => AchievementType.RequiredTimeConditional;
}




