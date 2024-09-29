using Dalamud.Plugin.Services;

namespace GagSpeak.Achievements;

public class ThresholdAchievement : Achievement
{
    /// <summary>
    /// The condition that determines what our threshold is.
    /// </summary>
    private int LastRecordedThreshold { get; set; }

    public ThresholdAchievement(INotificationManager notify, 
        string title, 
        string desc,
        int goal,
        string unit = "", 
        bool isSecret = false
        ) : base(notify, title, desc, goal, unit, isSecret)
    {
        LastRecordedThreshold = 0;
    }

    public override int CurrentProgress() => LastRecordedThreshold;

    public void UpdateThreshold(int threshold)
    {
        LastRecordedThreshold = threshold;
        CheckCompletion();
    }


    /// <summary>
    /// Check if the condition is satisfied
    /// </summary>
    public override void CheckCompletion()
    {
        if (IsCompleted) return;

        if (LastRecordedThreshold >= MilestoneGoal)
        {
            // Mark the achievement as completed
            MarkCompleted();
        }
    }

    public override AchievementType GetAchievementType() => AchievementType.Threshold;
}




