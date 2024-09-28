using Dalamud.Plugin.Services;

namespace GagSpeak.Achievements;

public class ConditionalAchievement : Achievement
{
    /// <summary>
    /// The condition that must be met to complete the achievement
    /// </summary>
    private Func<bool> Condition;

    public ConditionalAchievement(INotificationManager notify, string title, string desc, Func<bool> condition, string unit = "") 
        : base(notify, title, desc, 1, unit)
    {
        Condition = condition;
    }

    public override int CurrentProgress() => IsCompleted ? 1 : 0;

    /// <summary>
    /// Check if the condition is satisfied
    /// </summary>
    public override void CheckCompletion()
    {
        if (IsCompleted) return;

        if (Condition())
        {
            // Mark the achievement as completed
            MarkCompleted();
        }
    }
}




