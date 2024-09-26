namespace GagSpeak.Achievements;

public class ConditionalAchievement : Achievement
{
    /// <summary>
    /// The condition that must be met to complete the achievement
    /// </summary>
    private Func<bool> Condition;

    public ConditionalAchievement(string title, string description, Func<bool> condition) 
        : base(title, description)
    {
        Condition = condition;
    }

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




