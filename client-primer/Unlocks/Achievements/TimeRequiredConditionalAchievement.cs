using Dalamud.Plugin.Services;

namespace GagSpeak.Achievements;

public class TimeRequiredConditionalAchievement : Achievement
{
    private readonly TimeSpan MilestoneDuration;
    public DateTime StartPoint { get; set; } = DateTime.MinValue;
    public Func<bool> RequiredCondition;
    public DurationTimeUnit TimeUnit { get; init; }
    private CancellationTokenSource _cancellationTokenSource;

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
            StaticLogger.Logger.LogDebug($"Conditonal for {Title} has been met, checking Timer Status", LoggerType.Achievements);
            if (StartPoint == DateTime.MinValue)
            {
                StaticLogger.Logger.LogDebug($"Starting Timer for {Title}", LoggerType.Achievements);
                StartPoint = DateTime.UtcNow;
                StartTimer();
                return;
            }

            if (DateTime.UtcNow - StartPoint >= MilestoneDuration)
            {
                StaticLogger.Logger.LogDebug($"Time limit for {Title} has been reached, and conditonal was still valid. Marking as complete.", LoggerType.Achievements);
                MarkCompleted();
                _cancellationTokenSource?.Cancel();
            }
            // may need to add a reset if checked before time is up idk.
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
                    if (RequiredCondition())
                    {
                        StaticLogger.Logger.LogDebug($"Time limit for {Title} has been reached, and conditonal was still valid. Marking as complete.", LoggerType.Achievements);
                        MarkCompleted();
                    }
                    else
                    {
                        StaticLogger.Logger.LogDebug($"Conditonal for {Title} has not been met, resetting timer.", LoggerType.Achievements);
                        Reset();
                    }
                }
            }
            catch (TaskCanceledException) { /* Handle task cancellation if needed */ }
        }, token);
    }

    private void Reset() => StartPoint = DateTime.MinValue;

    public override AchievementType GetAchievementType() => AchievementType.RequiredTimeConditional;
}




