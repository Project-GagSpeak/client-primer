using Dalamud.Plugin.Services;

namespace GagSpeak.Achievements;

public class AchievementComponent
{
    [JsonIgnore]
    private readonly INotificationManager _notificationPinger;


    // Sorted Achievements.
    public Dictionary<string, Achievement> Achievements { get; }

/*    public Dictionary<string, ProgressAchievement> Progress { get; }
    public Dictionary<string, ConditionalAchievement> Conditional { get; }
    public Dictionary<string, DurationAchievement> Duration { get; }
    public Dictionary<string, TimedProgressAchievement> TimedProgress { get; }
    public Dictionary<string, ConditionalProgressAchievement> ConditionalProgress { get; }
    public Dictionary<string, ConditionalDurationAchievement> ConditionalDuration { get; }*/

    public AchievementComponent(INotificationManager completionNotification)
    {
        _notificationPinger = completionNotification;

        Progress = new Dictionary<string, ProgressAchievement>();
        Conditional = new Dictionary<string, ConditionalAchievement>();
        Duration = new Dictionary<string, DurationAchievement>();
        TimedProgress = new Dictionary<string, TimedProgressAchievement>();
        ConditionalProgress = new Dictionary<string, ConditionalProgressAchievement>();
        ConditionalDuration = new Dictionary<string, ConditionalDurationAchievement>();
    }

    [JsonIgnore]
    public int Total => Achievements.Count;

    [JsonIgnore]
    public List<Achievement> All => Achievements.Values.Cast<Achievement>().ToList();

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
