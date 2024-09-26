namespace GagSpeak.Achievements;

public class DurationAchievement : Achievement
{
    private readonly TimeSpan MilestoneDuration; // Required duration to achieve
    
    // The Current Active Item(s) being tracked. (can be multiple because of gags.
    public readonly Dictionary<string, DateTime> ActiveItems = new();

    public DurationAchievement(string name, string description, TimeSpan duration)
        : base(name, description)
    {
        MilestoneDuration = duration;
    }

    /// <summary>
    /// Begin tracking the time period of a duration achievement
    /// </summary>
    public void StartTracking(string itemName)
    {
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
}




