using Dalamud.Plugin.Services;

namespace GagSpeak.Achievements;

public class AchievementComponent
{    
    [JsonIgnore]
    public int Total => Achievements.Count;

    [JsonIgnore]
    public List<Achievement> All => Achievements.Values.Cast<Achievement>().ToList();


    // Abstract Achievements Dictionary, stores all other types of achievements within it.
    public Dictionary<string, Achievement> Achievements { get; } = new Dictionary<string, Achievement>();

    // Additional dictionary to support ID-based lookups, if needed
    public Dictionary<uint, Achievement> IdToAchievementMap => Achievements.Values.ToDictionary(a => a.AchievementId, a => a);

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

            if (lightAchievement.Type is AchievementType.ConditionalThreshold && Achievements[lightAchievement.Title] is ConditionalThresholdAchievement conditionalThresholdAchievement)
            {
                conditionalThresholdAchievement.IsCompleted = lightAchievement.IsCompleted;
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

            if (lightAchievement.Type is AchievementType.TimeLimitConditional && Achievements[lightAchievement.Title] is TimeLimitConditionalAchievement timeLimited)
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
        }
    }

    public void AddProgress(uint id, string title, string desc, int goal, Action<uint, string> onCompleted, string suffix = "", string prefix = "", bool isSecret = false)
    {
        var achievement = new ProgressAchievement(id, title, desc, goal, onCompleted, prefix, suffix, isSecret);
        Achievements.Add(achievement.Title, achievement);
    }

    public void AddConditional(uint id, string title, string desc, Func<bool> cond, Action<uint, string> onCompleted, string suffix = "", string prefix = "", bool isSecret = false)
    {
        var achievement = new ConditionalAchievement(id, title, desc, cond, onCompleted, prefix, suffix, isSecret);
        Achievements.Add(achievement.Title, achievement);
    }

    public void AddThreshold(uint id, string title, string desc, int goal, Action<uint, string> onCompleted, string suffix = "", string prefix = "", bool isSecret = false)
    {
        var achievement = new ThresholdAchievement(id, title, desc, goal, onCompleted, prefix, suffix, isSecret);
        Achievements.Add(achievement.Title, achievement);
    }

    public void AddDuration(uint id, string title, string desc, TimeSpan duration, DurationTimeUnit timeUnit, Action<uint, string> onCompleted, string suffix = "", string prefix = "", bool isSecret = false)
    {
        var achievement = new DurationAchievement(id, title, desc, duration, onCompleted, timeUnit, prefix, suffix, isSecret);
        Achievements.Add(achievement.Title, achievement);
    }

    public void AddRequiredTimeConditional(uint id, string title, string desc, TimeSpan duration, Func<bool> cond, DurationTimeUnit timeUnit, Action<uint, string> onCompleted, string suffix = "", string prefix = "", bool isSecret = false)
    {
        var achievement = new TimeRequiredConditionalAchievement(id, title, desc, duration, cond, onCompleted, timeUnit, prefix, suffix, isSecret);
        Achievements.Add(achievement.Title, achievement);
    }

    public void AddTimeLimitedConditional(uint id, string title, string desc, TimeSpan dur, Func<bool> cond, DurationTimeUnit timeUnit, Action<uint, string> onCompleted, string suffix = "", string prefix = "", bool isSecret = false)
    {
        var achievement = new TimeLimitConditionalAchievement(id, title, desc, dur, cond, onCompleted, timeUnit, prefix, suffix, isSecret);
        Achievements.Add(achievement.Title, achievement);
    }

    public void AddConditionalProgress(uint id, string title, string desc, int goal, Func<bool> cond, Action<uint, string> onCompleted, string suffix = "", string prefix = "", bool reqBeginAndFinish = true, bool isSecret = false)
    {
        var achievement = new ConditionalProgressAchievement(id, title, desc, goal, cond, onCompleted, reqBeginAndFinish, prefix, suffix, isSecret);
        Achievements.Add(achievement.Title, achievement);
    }

    public void AddConditionalThreshold(uint id, string title, string desc, int goal, Func<bool> cond, Action<uint, string> onCompleted, string suffix = "", string prefix = "", bool isSecret = false)
    {
        var achievement = new ConditionalThresholdAchievement(id, title, desc, goal, cond, onCompleted, prefix, suffix, isSecret);
        Achievements.Add(achievement.Title, achievement);
    }

    public void AddTimedProgress(uint id, string title, string desc, int goal, TimeSpan timeLimit, Action<uint, string> onCompleted, string suffix = "", string prefix = "", bool isSecret = false)
    {
        var achievement = new TimedProgressAchievement(id, title, desc, goal, timeLimit, onCompleted, prefix, suffix, isSecret);
        Achievements.Add(achievement.Title, achievement);
    }
}
