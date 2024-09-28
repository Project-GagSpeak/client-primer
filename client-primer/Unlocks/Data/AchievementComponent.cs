using Dalamud.Plugin.Services;

namespace GagSpeak.Achievements;

public class AchievementComponent
{
    [JsonIgnore]
    private readonly INotificationManager _notificationPinger;
    
    [JsonIgnore]
    public int Total => Achievements.Count;

    [JsonIgnore]
    public List<Achievement> All => Achievements.Values.Cast<Achievement>().ToList();


    // Abstract Achievements Dictionary, stores all other types of achievements within it.
    public Dictionary<string, Achievement> Achievements { get; }

    public AchievementComponent(INotificationManager completionNotification)
    {
        _notificationPinger = completionNotification;
        Achievements = new Dictionary<string, Achievement>();
    }

    public void LoadFromLightAchievements(List<LightAchievement> lightAchievements)
    {
        foreach (var lightAchievement in lightAchievements)
        {
            if (lightAchievement.Type is AchievementType.Progress && Achievements[lightAchievement.Title] is ProgressAchievement progressAchievement)
            {
                progressAchievement.IsCompleted = lightAchievement.IsCompleted;
                progressAchievement.Progress = lightAchievement.Progress;
                continue; // skip to next achievement
            }

            if (lightAchievement.Type is AchievementType.Conditional && Achievements[lightAchievement.Title] is ConditionalAchievement conditionalAchievement)
            {
                conditionalAchievement.IsCompleted = lightAchievement.IsCompleted;
                continue; // skip to next achievement
            }

            if (lightAchievement.Type is AchievementType.Duration && Achievements[lightAchievement.Title] is DurationAchievement durationAchievement)
            {
                durationAchievement.IsCompleted = lightAchievement.IsCompleted;
                durationAchievement.ActiveItems = lightAchievement.ActiveItems;
                continue; // skip to next achievement
            }

            if (lightAchievement.Type is AchievementType.ConditionalDuration && Achievements[lightAchievement.Title] is ConditionalDurationAchievement conditionalDurationAchievement)
            {
                conditionalDurationAchievement.IsCompleted = lightAchievement.IsCompleted;
                conditionalDurationAchievement.StartPoint = lightAchievement.StartTime;
                continue; // skip to next achievement
            }

            if (lightAchievement.Type is AchievementType.ConditionalProgress && Achievements[lightAchievement.Title] is ConditionalProgressAchievement conditionalProgressAchievement)
            {
                conditionalProgressAchievement.IsCompleted = lightAchievement.IsCompleted;
                conditionalProgressAchievement.Progress = lightAchievement.Progress;
                conditionalProgressAchievement.ConditionalTaskBegun = lightAchievement.ConditionalTaskBegun;
                continue; // skip to next achievement
            }

            if (lightAchievement.Type is AchievementType.TimedProgress && Achievements[lightAchievement.Title] is TimedProgressAchievement timedProgressAchievement)
            {
                timedProgressAchievement.IsCompleted = lightAchievement.IsCompleted;
                timedProgressAchievement.Progress = lightAchievement.Progress;
                timedProgressAchievement.StartTime = lightAchievement.StartTime;
                continue; // skip to next achievement
            }

        }
    }

    public void AddProgress(string title, string description, int targetProgress, string suffix)
    {
        var achievement = new ProgressAchievement(_notificationPinger, title, description, targetProgress, suffix);
        Achievements.Add(achievement.Title, achievement);
    }

    public void AddConditional(string title, string description, Func<bool> condition, string suffix)
    {
        var achievement = new ConditionalAchievement(_notificationPinger, title, description, condition, suffix);
        Achievements.Add(achievement.Title, achievement);
    }

    public void AddDuration(string title, string description, TimeSpan duration, DurationTimeUnit timeUnit, string suffix)
    {
        var achievement = new DurationAchievement(_notificationPinger, title, description, duration, timeUnit, suffix);
        Achievements.Add(achievement.Title, achievement);
    }

    public void AddConditionalDuration(string title, string description, TimeSpan duration, Func<bool> condition,
        DurationTimeUnit timeUnit, string suffix, bool finishWithinTime = false)
    {
        var achievement = new ConditionalDurationAchievement(_notificationPinger, title, description, 
            duration, condition, timeUnit, finishWithinTime, suffix);
        Achievements.Add(achievement.Title, achievement);
    }

    public void AddConditionalProgress(string title, string description, int targetProgress, Func<bool> condition,
        string suffix, bool requireTaskBeginAndFinish = true)
    {
        var achievement = new ConditionalProgressAchievement(_notificationPinger, title, description, targetProgress, 
            condition, requireTaskBeginAndFinish, suffix);
        Achievements.Add(achievement.Title, achievement);
    }

    public void AddTimedProgress(string title, string description, int targetProgress, TimeSpan timeLimit, string suffix)
    {
        var achievement = new TimedProgressAchievement(_notificationPinger, title, description, targetProgress, timeLimit, suffix);
        Achievements.Add(achievement.Title, achievement);
    }
}
