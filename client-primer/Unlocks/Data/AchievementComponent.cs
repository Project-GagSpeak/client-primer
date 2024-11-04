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

    public void AddProgress(AchievementInfo info, int goal, Action<uint, string> onCompleted, string suffix = "", string prefix = "", bool isSecret = false)
    {
        var achievement = new ProgressAchievement(info.Id, info.Title, info.Description, goal, onCompleted, prefix, suffix, isSecret);
        Achievements.Add(achievement.Title, achievement);
    }

    public void AddConditional(AchievementInfo info, Func<bool> cond, Action<uint, string> onCompleted, string suffix = "", string prefix = "", bool isSecret = false)
    {
        var achievement = new ConditionalAchievement(info.Id, info.Title, info.Description, cond, onCompleted, prefix, suffix, isSecret);
        Achievements.Add(achievement.Title, achievement);
    }

    public void AddThreshold(AchievementInfo info, int goal, Action<uint, string> onCompleted, string suffix = "", string prefix = "", bool isSecret = false)
    {
        var achievement = new ThresholdAchievement(info.Id, info.Title, info.Description, goal, onCompleted, prefix, suffix, isSecret);
        Achievements.Add(achievement.Title, achievement);
    }

    public void AddDuration(AchievementInfo info, TimeSpan duration, DurationTimeUnit timeUnit, Action<uint, string> onCompleted, string suffix = "", string prefix = "", bool isSecret = false)
    {
        var achievement = new DurationAchievement(info.Id, info.Title, info.Description, duration, onCompleted, timeUnit, prefix, suffix, isSecret);
        Achievements.Add(achievement.Title, achievement);
    }

    public void AddRequiredTimeConditional(AchievementInfo info, TimeSpan duration, Func<bool> cond, DurationTimeUnit timeUnit, Action<uint, string> onCompleted, string suffix = "", string prefix = "", bool isSecret = false)
    {
        var achievement = new TimeRequiredConditionalAchievement(info.Id, info.Title, info.Description, duration, cond, onCompleted, timeUnit, prefix, suffix, isSecret);
        Achievements.Add(achievement.Title, achievement);
    }

    public void AddTimeLimitedConditional(AchievementInfo info, TimeSpan dur, Func<bool> cond, DurationTimeUnit timeUnit, Action<uint, string> onCompleted, string suffix = "", string prefix = "", bool isSecret = false)
    {
        var achievement = new TimeLimitConditionalAchievement(info.Id, info.Title, info.Description, dur, cond, onCompleted, timeUnit, prefix, suffix, isSecret);
        Achievements.Add(achievement.Title, achievement);
    }

    public void AddConditionalProgress(AchievementInfo info, int goal, Func<bool> cond, Action<uint, string> onCompleted, string suffix = "", string prefix = "", bool reqBeginAndFinish = true, bool isSecret = false)
    {
        var achievement = new ConditionalProgressAchievement(info.Id, info.Title, info.Description, goal, cond, onCompleted, reqBeginAndFinish, prefix, suffix, isSecret);
        Achievements.Add(achievement.Title, achievement);
    }

    public void AddConditionalThreshold(AchievementInfo info, int goal, Func<bool> cond, Action<uint, string> onCompleted, string suffix = "", string prefix = "", bool isSecret = false)
    {
        var achievement = new ConditionalThresholdAchievement(info.Id, info.Title, info.Description, goal, cond, onCompleted, prefix, suffix, isSecret);
        Achievements.Add(achievement.Title, achievement);
    }

    public void AddTimedProgress(AchievementInfo info, int goal, TimeSpan timeLimit, Action<uint, string> onCompleted, string suffix = "", string prefix = "", bool isSecret = false)
    {
        var achievement = new TimedProgressAchievement(info.Id, info.Title, info.Description, goal, timeLimit, onCompleted, prefix, suffix, isSecret);
        Achievements.Add(achievement.Title, achievement);
    }
}
