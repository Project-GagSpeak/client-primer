namespace GagSpeak.Achievements;

public class AchievementModule
{
    // create a dictionary where the keys are the achievements names so we can easily find them.
    public Dictionary<string, Achievement> Achievements { get; private set; } = [];
    public AchievementModule()
    {
        Achievements = new Dictionary<string, Achievement>();
    }

    public int TotalAchievements => Achievements.Count;
    public int CompletedAchievements => Achievements.Count(achievement => achievement.Value.IsCompleted);

    public void AddAchievement(Achievement achievement)
    {
        // add the achievement to the dictionary with its name as the key so we can easily find it.
        Achievements.Add(achievement.Title, achievement);
    }
}
