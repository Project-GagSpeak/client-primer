using Dalamud.Plugin.Services;
using GagSpeak.WebAPI;

namespace GagSpeak.Achievements;

public class TimeLimitConditionalAchievement : Achievement
{
    private readonly TimeSpan MilestoneDuration;
    public DateTime StartPoint { get; set; } = DateTime.MinValue;
    public Func<bool> RequiredCondition;
    public DurationTimeUnit TimeUnit { get; init; }
    private CancellationTokenSource _cancellationTokenSource;
    private bool _taskStarted = false;


    public TimeLimitConditionalAchievement(uint id, string name, string desc, TimeSpan duration, Func<bool> condition, 
        Action<uint, string> onCompleted, DurationTimeUnit unit, string prefix = "", string suffix = "", bool isSecret = false) 
        : base(id, name, desc, ConvertToUnit(duration, unit), prefix, suffix, onCompleted, isSecret)
    {
        MilestoneDuration = duration;
        RequiredCondition = condition;
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
            return "Completed Within " + MilestoneGoal + " " + TimeUnit + " " + SuffixText;

        // If not completed and the StartPoint is DateTime.MinValue, display that that the state is not yet begun.
        if(StartPoint == DateTime.MinValue)
            return PrefixText + " Within " + MilestoneGoal + " " + TimeUnit + " " + SuffixText;

        // Grab our remaining time.
        var remaining = MilestoneDuration - (StartPoint != DateTime.MinValue ? DateTime.UtcNow - StartPoint : TimeSpan.Zero);
        string outputStr = "";
        if (remaining == TimeSpan.Zero)
        {
            outputStr = "0s";
        }
        else
        {
            if (remaining.Days > 0) outputStr += remaining.Days + "d ";
            if (remaining.Hours > 0) outputStr += remaining.Hours + "h ";
            if (remaining.Minutes > 0) outputStr += remaining.Minutes + "m ";
            if (remaining.Seconds >= 0) outputStr += remaining.Seconds + "s ";
            outputStr += " Remaining To Complete Condition";
        }
        return outputStr;
    }

    public override void CheckCompletion()
    {
        if (IsCompleted || !MainHub.IsConnected)
            return;

        if (RequiredCondition())
        {
            if (StartPoint != DateTime.MinValue && DateTime.UtcNow - StartPoint < MilestoneDuration)
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
        if (IsCompleted || !MainHub.IsConnected || _taskStarted)
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
        if (IsCompleted || !MainHub.IsConnected)
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
                    ResetTask(); // reset if we take longer than the requirement.
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

    public override AchievementType GetAchievementType() => AchievementType.TimeLimitConditional;
}




