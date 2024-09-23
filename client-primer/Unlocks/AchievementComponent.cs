namespace GagSpeak.Achievements;

public class AchievementComponent
{
    /// <summary> The Type that this Component Represents </summary>
    public AchievementType Type { get; init; }
    public List<object> Achievements { get; private set; }

    public AchievementComponent(AchievementType type)
    {
        Type = type;
        Achievements = new List<object>();
    }

    public void AddAchievement<T>(Achievement<T> achievement) where T : IComparable<T>
    {
        Achievements.Add(achievement);
    }
}
