using Dalamud.Plugin.Services;

namespace GagSpeak.Achievements;

public class ConditionalDurationAchievement : Achievement
{
    // Required Duration to Achieve
    private readonly TimeSpan MilestoneDuration;

    // Tracked Start Time
    public DateTime StartPoint { get; private set; } = DateTime.MinValue;

    // Requirement that must remain true while tracking.
    public Func<bool> RequiredCondition;

    public DurationTimeUnit TimeUnit { get; init; }

    public bool CompleteWithinTimeSpan { get; init; }

    public ConditionalDurationAchievement(INotificationManager notify, 
        string name, 
        string description, 
        TimeSpan duration, 
        Func<bool> condition,
        DurationTimeUnit timeUnit,
        bool completeWithinTimeSpan = false,  
        string unit = ""
        ) : base(notify, name, description, ConvertToUnit(duration, timeUnit), unit)
    {
        MilestoneDuration = duration;
        RequiredCondition = condition;
        TimeUnit = timeUnit;
        CompleteWithinTimeSpan = completeWithinTimeSpan;
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
        // if completed, return the milestone goal.
        if (IsCompleted) return MilestoneGoal;

        // Calculate elapsed time
        var elapsed = StartPoint != DateTime.MinValue ? DateTime.UtcNow - StartPoint : TimeSpan.Zero;

        // Return progress based on the specified unit
        return TimeUnit switch
        {
            DurationTimeUnit.Seconds => (int)elapsed.TotalSeconds,
            DurationTimeUnit.Minutes => (int)elapsed.TotalMinutes,
            DurationTimeUnit.Hours => (int)elapsed.TotalHours,
            DurationTimeUnit.Days => (int)elapsed.TotalDays,
            _ => 0 // Default case, should not be hit
        };
    }


    /// <summary>
    /// Check if the condition is satisfied
    /// </summary>
    public override void CheckCompletion()
    {
        if (IsCompleted) return;

        // verify our condition is being met.
        if (RequiredCondition())
        {
            // if the condition is set, and the start point is minvalue, set it to current time.
            if (StartPoint == DateTime.MinValue)
            {
                StartPoint = DateTime.UtcNow;
                return;
            }

            // if we have reached the required timespan, mark the achievement as completed.
            if (CompleteWithinTimeSpan)
            {
                // if we should have completed within timespan, and have, mark as done.
                if (DateTime.UtcNow - StartPoint <= MilestoneDuration)
                {
                    MarkCompleted();
                }
                // otherwise, if we exceeded the time, reset.
                else if (DateTime.UtcNow - StartPoint >= MilestoneDuration)
                {
                    ResetOrComplete();
                }
            }
            else
            {
                // if we have not reached the required timespan, mark the achievement as completed.
                if (DateTime.UtcNow - StartPoint >= MilestoneDuration)
                {
                    MarkCompleted();
                }
                // otherwise, if we have not exceeded the time, reset.
                else if (DateTime.UtcNow - StartPoint <= MilestoneDuration)
                {
                    ResetOrComplete();
                }
            }
        }
        else
        {
            // reset the start point if the condition is not met.
            ResetOrComplete();
        }
    }

    public void ResetOrComplete()
    {
        if (StartPoint == DateTime.MinValue) return;

        // if we have reached the required timespan, mark the achievement as completed.
        if (CompleteWithinTimeSpan)
        {
            // if we should have completed within timespan, and have, mark as done.
            if (DateTime.UtcNow - StartPoint <= MilestoneDuration)
            {
                MarkCompleted();
            }
            // otherwise, if we exceeded the time, reset.
            else if (DateTime.UtcNow - StartPoint >= MilestoneDuration)
            {
            StartPoint = DateTime.MinValue;
            }
        }
        else
        {
            // if we have not reached the required timespan, mark the achievement as completed.
            if (DateTime.UtcNow - StartPoint >= MilestoneDuration)
            {
                MarkCompleted();
            }
            // otherwise, if we have not exceeded the time, reset.
            else if (DateTime.UtcNow - StartPoint <= MilestoneDuration)
            {
                StartPoint = DateTime.MinValue;
            }
        }
    }
}




