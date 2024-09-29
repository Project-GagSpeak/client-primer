using Dalamud.Plugin.Services;

namespace GagSpeak.Achievements;

public class TimeRequiredConditionalAchievement : Achievement
{
    private readonly TimeSpan MilestoneDuration;
    public DateTime StartPoint { get; set; } = DateTime.MinValue;
    public Func<bool> RequiredCondition;
    public DurationTimeUnit TimeUnit { get; init; }
    private CancellationTokenSource _cancellationTokenSource;

    public TimeRequiredConditionalAchievement(INotificationManager notify,
        string name,
        string description,
        TimeSpan duration,
        Func<bool> condition,
        DurationTimeUnit timeUnit,
        string unit = "",
        bool isSecret = false
        ) : base(notify, name, description, ConvertToUnit(duration, timeUnit), unit, isSecret)
    {
        MilestoneDuration = duration;
        RequiredCondition = condition;
        TimeUnit = timeUnit;
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

    public override void CheckCompletion()
    {
        if (IsCompleted) return;

        if (RequiredCondition())
        {
            if (StartPoint == DateTime.MinValue)
            {
                StartPoint = DateTime.UtcNow;
                StartTimer();
                return;
            }

            if (DateTime.UtcNow - StartPoint >= MilestoneDuration)
            {
                MarkCompleted();
                _cancellationTokenSource?.Cancel();
            }
            // may need to add a reset if checked before time is up idk.
        }
        else
        {
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
                        MarkCompleted();
                    }
                    else
                    {
                        Reset();
                    }
                }
            }
            catch (TaskCanceledException)
            {
                // Handle task cancellation if needed
            }
        }, token);
    }

    private void Reset()
    {
        StartPoint = DateTime.MinValue;
    }

    public override AchievementType GetAchievementType() => AchievementType.RequiredTimeConditional;
}




