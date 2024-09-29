using Dalamud.Plugin.Services;
using System;

namespace GagSpeak.Achievements;

public class DurationAchievement : Achievement
{
    private readonly TimeSpan MilestoneDuration; // Required duration to achieve
    
    // The Current Active Item(s) being tracked. (can be multiple because of gags.
    public Dictionary<string, DateTime> ActiveItems { get; set; } = new Dictionary<string, DateTime>();

    public DurationTimeUnit TimeUnit { get; init; }

    public DurationAchievement(INotificationManager notify, 
        string name, 
        string desc, 
        TimeSpan duration, 
        DurationTimeUnit timeUnit = DurationTimeUnit.Minutes, 
        string unit = "", 
        bool isSecret = false
        ) : base(notify, name, desc, ConvertToUnit(duration, timeUnit), unit, isSecret)
    {
        MilestoneDuration = duration;
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
        // if completed, return the milestone goal.
        if (IsCompleted) return MilestoneGoal;

        // otherwise, return the ActiveItem with the longest duration from the DateTime.UtcNow and return its value in total minutes.
        var elapsed = ActiveItems.Any() ? (DateTime.UtcNow - ActiveItems.Values.Max()) : TimeSpan.Zero;

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
    /// Begin tracking the time period of a duration achievement
    /// </summary>
    public void StartTracking(string itemName)
    {
        if (IsCompleted) return;

        if (!ActiveItems.ContainsKey(itemName))
        {
            ActiveItems[itemName] = DateTime.UtcNow; // Start tracking time
        }
    }

    /// <summary>
    /// Stop tracking the time period of a duration achievement
    /// </summary>
    public void StopTracking(string itemName)
    {
        if (IsCompleted) return;

        // check completion before we stop tracking.
        CheckCompletion();
        // if not completed, remove the item from tracking.
        if(!IsCompleted)
        {
            if (ActiveItems.ContainsKey(itemName))
            {
                ActiveItems.Remove(itemName); // Stop tracking the item
            }
        }
    }

    /// <summary>
    /// Check if the condition is satisfied
    /// </summary>
    public override void CheckCompletion()
    {
        // if any of the active items exceed the required duration, mark the achievement as completed
        if (ActiveItems.Any(x => DateTime.UtcNow - x.Value >= MilestoneDuration))
        {
            MarkCompleted();
        }
    }

    public override AchievementType GetAchievementType() => AchievementType.Duration;
}




