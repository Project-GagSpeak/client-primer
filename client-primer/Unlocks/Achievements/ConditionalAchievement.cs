using Dalamud.Plugin.Services;

namespace GagSpeak.Achievements;

public class ConditionalAchievement : Achievement
{
    /// <summary>
    /// The condition that must be met to complete the achievement
    /// </summary>
    private Func<bool> Condition;

    public ConditionalAchievement(INotificationManager notify, string title, string desc, Func<bool> condition, 
        string prefix = "", string suffix = "", bool isSecret = false) : base(notify, title, desc, 1, prefix, suffix, isSecret)
    {
        Condition = condition;
    }

    public override int CurrentProgress() => IsCompleted ? 1 : 0;

    public override string ProgressString() => PrefixText + " " + (IsCompleted ? "1" : "0" + " / 1") + " " + SuffixText;

    public override void CheckCompletion()
    {
        if (IsCompleted) 
            return;

        StaticLogger.Logger.LogDebug($"Checking if {Title} satisfies conditional", LoggerType.Achievements);

        if (Condition())
        {
            // Mark the achievement as completed
            MarkCompleted();
        }
    }

    public override AchievementType GetAchievementType() => AchievementType.Conditional;
}




