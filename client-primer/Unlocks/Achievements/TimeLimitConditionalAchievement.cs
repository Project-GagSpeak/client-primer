using Dalamud.Plugin.Services;

namespace GagSpeak.Achievements;

public class TimeLimitConditionalAchievement : Achievement
{
    private readonly TimeSpan MilestoneDuration;
    public DateTime StartPoint { get; set; } = DateTime.MinValue;
    public Func<bool> RequiredCondition;
    public DurationTimeUnit TimeUnit { get; init; }
    private CancellationTokenSource _cancellationTokenSource;

    public TimeLimitConditionalAchievement(INotificationManager notify, string name, string desc, TimeSpan duration, 
        Func<bool> condition, DurationTimeUnit unit, string prefix = "", string suffix = "", bool isSecret = false) 
        : base(notify, name, desc, ConvertToUnit(duration, unit), prefix, suffix, isSecret)
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
            return "Completed Within " + MilestoneGoal + " " + SuffixText;

        // If not completed and the StartPoint is DateTime.MinValue, display that that the state is not yet begun.
        if(StartPoint == DateTime.MinValue)
            return PrefixText + " Within " + MilestoneGoal + " " + SuffixText;

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
        if (IsCompleted) 
            return;

        if (RequiredCondition())
        {
            StaticLogger.Logger.LogDebug($"{Title} has met the required condition. Checking time limit.", LoggerType.Achievements);

            if (StartPoint == DateTime.MinValue)
            {
                StaticLogger.Logger.LogDebug($"Starting Timer for {Title}. Timer will automatically reset in {MilestoneDuration}{TimeUnit}", LoggerType.Achievements);
                StartPoint = DateTime.UtcNow;
                StartTimer();
                return;
            }

            if (DateTime.UtcNow - StartPoint <= MilestoneDuration)
            {
                StaticLogger.Logger.LogDebug($"{Title} has met the required condition within the required time limit. Marking as finished.", LoggerType.Achievements);
                MarkCompleted();
                _cancellationTokenSource?.Cancel();
            }
        }
        else
        {
            StaticLogger.Logger.LogDebug($"Conditonal for {Title} has not been met, resetting timer.", LoggerType.Achievements);
            Reset();
        }
    }

    private void StartTimer()
    {
        _cancellationTokenSource = new CancellationTokenSource();
        var token = _cancellationTokenSource.Token;

        Task.Run(async () =>
        {
            try
            {
                await Task.Delay(MilestoneDuration, token);
                if (!token.IsCancellationRequested)
                {
                    StaticLogger.Logger.LogDebug($"Failed to complete conditonal within the time limit provided for {Title}" +
                        $". Resetting Timer!", LoggerType.Achievements);
                    Reset();
                }
            }
            catch (TaskCanceledException) { /* Handle task cancellation if needed */ }
        }, token);
    }

    private void Reset() => StartPoint = DateTime.MinValue;

    public override AchievementType GetAchievementType() => AchievementType.TimeLimitConditional;
}




