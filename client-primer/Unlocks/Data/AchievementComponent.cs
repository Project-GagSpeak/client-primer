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

            if (lightAchievement.Type is AchievementType.Threshold && Achievements[lightAchievement.Title] is ThresholdAchievement thresholdAchievement)
            {
                thresholdAchievement.IsCompleted = lightAchievement.IsCompleted;
                continue; // skip to next achievement
            }

            if (lightAchievement.Type is AchievementType.Duration && Achievements[lightAchievement.Title] is DurationAchievement durationAchievement)
            {
                durationAchievement.IsCompleted = lightAchievement.IsCompleted;
                durationAchievement.ActiveItems = lightAchievement.ActiveItems;
                continue; // skip to next achievement
            }

            if(lightAchievement.Type is AchievementType.TimeLimitConditional && Achievements[lightAchievement.Title] is TimeLimitConditionalAchievement timeLimited)
            {
                timeLimited.IsCompleted = lightAchievement.IsCompleted;
                timeLimited.StartPoint = lightAchievement.StartTime;
                continue; // skip to next achievement
            }

            if (lightAchievement.Type is AchievementType.RequiredTimeConditional && Achievements[lightAchievement.Title] is TimeRequiredConditionalAchievement timeRequired)
            {
                timeRequired.IsCompleted = lightAchievement.IsCompleted;
                timeRequired.StartPoint = lightAchievement.StartTime;
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
                timedProgressAchievement.ProgressTimestamps = lightAchievement.RecordedDateTimes;
                continue; // skip to next achievement
            }
        }
    }

    public void AddProgress(string title, string desc, int goal, string suffix = "", string prefix = "", bool isSecret = false)
    {
        var achievement = new ProgressAchievement(_notificationPinger, title, desc, goal, prefix, suffix, isSecret);
        Achievements.Add(achievement.Title, achievement);
    }

    public void AddConditional(string title, string desc, Func<bool> cond, string suffix = "", string prefix = "", bool isSecret = false)
    {
        var achievement = new ConditionalAchievement(_notificationPinger, title, desc, cond, prefix, suffix, isSecret);
        Achievements.Add(achievement.Title, achievement);
    }

    public void AddThreshold(string title, string desc, int goal, string suffix = "", string prefix = "", bool isSecret = false)
    {
        var achievement = new ThresholdAchievement(_notificationPinger, title, desc, goal, prefix, suffix, isSecret);
        Achievements.Add(achievement.Title, achievement);
    }

    public void AddDuration(string title, string desc, TimeSpan duration, DurationTimeUnit timeUnit, string suffix = "", string prefix = "", bool isSecret = false)
    {
        var achievement = new DurationAchievement(_notificationPinger, title, desc, duration, timeUnit, prefix, suffix, isSecret);
        Achievements.Add(achievement.Title, achievement);
    }

    public void AddRequiredTimeConditional(string title, string desc, TimeSpan duration, Func<bool> cond, DurationTimeUnit timeUnit, string suffix = "", string prefix = "", bool isSecret = false)
    {
        var achievement = new TimeRequiredConditionalAchievement(_notificationPinger, title, desc, duration, cond, timeUnit, prefix, suffix, isSecret);
        Achievements.Add(achievement.Title, achievement);
    }

    public void AddTimeLimitedConditional(string title, string desc, TimeSpan dur, Func<bool> cond, DurationTimeUnit timeUnit, string suffix = "", string prefix = "", bool isSecret = false)
    {
        var achievement = new TimeLimitConditionalAchievement(_notificationPinger, title, desc, dur, cond, timeUnit, prefix, suffix, isSecret);
        Achievements.Add(achievement.Title, achievement);
    }

    public void AddConditionalProgress(string title, string desc, int goal, Func<bool> cond, string suffix = "", string prefix = "", bool reqBeginAndFinish = true, bool isSecret = false)
    {
        var achievement = new ConditionalProgressAchievement(_notificationPinger, title, desc, goal, cond, reqBeginAndFinish, prefix, suffix, isSecret);
        Achievements.Add(achievement.Title, achievement);
    }

    public void AddConditionalThreshold(string title, string desc, int goal, Func<bool> cond, string suffix = "", string prefix = "", bool isSecret = false)
    {
        var achievement = new ConditionalThresholdAchievement(_notificationPinger, title, desc, goal, cond, prefix, suffix, isSecret);
        Achievements.Add(achievement.Title, achievement);
    }

    public void AddTimedProgress(string title, string desc, int goal, TimeSpan timeLimit, string suffix = "", string prefix = "", bool isSecret = false)
    {
        var achievement = new TimedProgressAchievement(_notificationPinger, title, desc, goal, timeLimit, prefix, suffix, isSecret);
        Achievements.Add(achievement.Title, achievement);
    }
}
