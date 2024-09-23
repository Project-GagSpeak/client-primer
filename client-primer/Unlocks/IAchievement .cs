namespace GagSpeak.Achievements;

/// <summary>
/// An achievement represents a goal or milestone task that can provide a reward/unlockable when completed.
/// </summary>
/// <typeparam name="T"> The typedef to be used to track achievement progress</typeparam>
public interface IAchievement<T>
{
    /// <summary>
    /// Defines the Name Given for the created Achievement.
    /// </summary>
    string Name { get; init; }

    /// <summary>
    /// Ensures that the client has a way to check if the progress threshold has been met for an achievement.
    /// </summary>
    bool IsUnlocked { get; }

    /// <summary>
    /// Requires all Achievement objects to have a threshold (or goal) to be met for the unlock to occur.
    /// </summary>
    T Threshold { get; init; }

    /// <summary>
    /// Requires that the Achievement object has a way to update the progress towards the achievements Threshold.
    /// </summary>
    /// <param name="newProgress"> the amount of progress made.</param>
    void AdvanceProgression(T newProgress);
}

