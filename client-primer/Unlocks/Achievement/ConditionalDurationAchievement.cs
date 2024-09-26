namespace GagSpeak.Achievements;

public class ConditionalDurationAchievement : Achievement
{
    // Required Duration to Achieve
    private readonly TimeSpan MilestoneDuration;

    // Tracked Start Time
    public DateTime StartPoint { get; private set; } = DateTime.MinValue;

    // Requirement that must remain true while tracking.
    public Func<bool> RequiredCondition;

    public bool CompleteWithinTimeSpan { get; init; }

    public ConditionalDurationAchievement(string name, string description, TimeSpan duration, 
        Func<bool> condition, bool completeWithinTimeSpan = false)
        : base(name, description)
    {
        MilestoneDuration = duration;
        RequiredCondition = condition;
        CompleteWithinTimeSpan = completeWithinTimeSpan;
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




