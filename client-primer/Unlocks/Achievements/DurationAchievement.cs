using GagSpeak.WebAPI;

namespace GagSpeak.Achievements;

public struct TrackedItem
{
    public string ItemIdentifier { get; init; }
    public string UIDAffected { get; init; }
    public TrackedItem(string item, string uidAffected)
    {
        ItemIdentifier = item;
        UIDAffected = uidAffected;
    }
}

public class DurationAchievement : Achievement
{
    private readonly TimeSpan MilestoneDuration; // Required duration to achieve

    // The Current Active Item(s) being tracked. (can be multiple because of gags.
    public Dictionary<TrackedItem, DateTime> ActiveItems { get; set; } = new Dictionary<TrackedItem, DateTime>();

    public DurationTimeUnit TimeUnit { get; init; }

    public DurationAchievement(uint id, string name, string desc, TimeSpan duration, Action<uint, string> onCompleted,
        DurationTimeUnit timeUnit = DurationTimeUnit.Minutes, string prefix = "", string suffix = "",
        bool isSecret = false) : base(id, name, desc, ConvertToUnit(duration, timeUnit), prefix, suffix, onCompleted, isSecret)
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
        if (IsCompleted || !MainHub.IsConnected)
            return MilestoneGoal;

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

    public override string ProgressString()
    {
        if (IsCompleted)
        {
            return PrefixText + " " + (MilestoneGoal + "/" + MilestoneGoal) + " " + SuffixText;
        }
        // Get the current longest equipped thing.
        var elapsed = ActiveItems.Any() ? (DateTime.UtcNow - ActiveItems.Values.Min()) : TimeSpan.Zero;

        // Construct the string to output for the progress.
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
            outputStr += "elapsed";
        }
        // Add the Ratio
        return PrefixText + " " + outputStr + " / " + MilestoneGoal + " " + SuffixText;
    }

    /// <summary>
    /// Begin tracking the time period of a duration achievement
    /// </summary>
    public void StartTracking(TrackedItem item)
    {
        if (IsCompleted || !MainHub.IsConnected)
            return;

        if (!ActiveItems.ContainsKey(item))
        {
            StaticLogger.Logger.LogDebug($"Started Tracking item {item.ItemIdentifier} on {item.UIDAffected} for {Title}", LoggerType.Achievements);
            ActiveItems[item] = DateTime.UtcNow; // Start tracking time
        }
        else
        {
            StaticLogger.Logger.LogDebug($"Item {item.ItemIdentifier} on {item.UIDAffected} is already being tracked for {Title}, ignoring. (Likely loading in from reconnect)", LoggerType.Achievements);
        }
    }

    /// <summary>
    /// Cleans up any items no longer present on the UID that are still cached.
    /// </summary>
    public void CleanupTracking(List<TrackedItem> items)
    {
        var itemsToRemove = ActiveItems.Keys.Except(items).ToList();
        StaticLogger.Logger.LogDebug($"Cleaning up tracking items for {Title}", LoggerType.Achievements);
        foreach (var key in itemsToRemove)
        {
            StaticLogger.Logger.LogDebug($"Item {key.ItemIdentifier} on {key.UIDAffected} is no longer present, removing from tracking.", LoggerType.Achievements);
            ActiveItems.Remove(key);
        }
    }

    /// <summary>
    /// Stop tracking the time period of a duration achievement
    /// </summary>
    public void StopTracking(TrackedItem item)
    {
        if (IsCompleted || !MainHub.IsConnected)
            return;

        StaticLogger.Logger.LogDebug($"Stopped Tracking item {item.ItemIdentifier} on {item.UIDAffected} for {Title}", LoggerType.Achievements);

        // check completion before we stop tracking.
        CheckCompletion();
        // if not completed, remove the item from tracking.
        if (!IsCompleted)
        {
            if (ActiveItems.ContainsKey(item))
            {
                StaticLogger.Logger.LogDebug($"Item {item.ItemIdentifier} on {item.UIDAffected} was not completed, removing from tracking.", LoggerType.Achievements);
                ActiveItems.Remove(item); // Stop tracking the item
            }
            else
            {
                // Log all currently active tracked items for debugging.
                StaticLogger.Logger.LogDebug($"Items Currently still being tracked: {string.Join(", ", ActiveItems.Keys)}", LoggerType.Achievements);
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
            // Mark the achievement as completed
            StaticLogger.Logger.LogInformation($"Achievement {Title} has been been active for the required Duration. "
                + "Marking as finished!", LoggerType.Achievements);
            MarkCompleted();
        }
    }

    public override AchievementType GetAchievementType() => AchievementType.Duration;
}
