namespace GagSpeak.Achievements;

/// <summary>
/// An achievement represents a goal or milestone task that can provide a reward/unlockable when completed.
/// </summary>
/// <typeparam name="T"> The typedef to be used to track achievement progress</typeparam>
public class Achievement<T> : IAchievement<T> where T : IComparable<T>
{
    public string Name { get; init; }
    public T Progress { get; private set; }
    public T Threshold { get; init; }
    public bool IsUnlocked => Progress.CompareTo(Threshold) >= 0;

    public Achievement(string name, T threshold, T startingProgress = default(T)!)
    {
        if(startingProgress == null && default(T) != null) 
            throw new ArgumentNullException(nameof(startingProgress));

        Name = name;
        Progress = startingProgress!;
        Threshold = threshold;
    }

    public void AdvanceProgression(T newProgress)
    {
        Progress = newProgress;
    }
}
