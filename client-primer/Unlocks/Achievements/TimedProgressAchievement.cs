using Dalamud.Plugin.Services;
using GagSpeak.WebAPI;

namespace GagSpeak.Achievements;

public class TimedProgressAchievement : Achievement
{
    /// <summary>
    /// The Current Progress made towards the achievement.
    /// </summary>
    public int Progress => ProgressTimestamps.Count;

    /// <summary>
    /// The list of DateTimes when progress was made. (TODO: I dont think these are stored yet in light data yet)
    /// </summary>
    public List<DateTime> ProgressTimestamps { get; set; } = new List<DateTime>();

    /// <summary>
    /// How long you have to earn the achievement in.
    /// </summary>
    public TimeSpan TimeToComplete { get; private set; }

    public TimedProgressAchievement(uint id, string title, string desc, int goal, TimeSpan timeLimit, Action<uint, string> onCompleted,
        string prefix = "", string suffix = "", bool isSecret = false) : base(id, title, desc, goal, prefix, suffix, onCompleted, isSecret)
    {
        TimeToComplete = timeLimit;
    }

    public override int CurrentProgress() => IsCompleted ? MilestoneGoal : Progress;

    public override string ProgressString() => PrefixText + " " + (CurrentProgress() + " / " + MilestoneGoal) + " " + SuffixText;

    /// <summary>
    /// Increments the progress towards the achievement.
    /// </summary>
    public void IncrementProgress(int amount = 1)
    {
        if (IsCompleted || !MainHub.IsConnected) 
            return;

        StaticLogger.Logger.LogDebug($"Checking Timer for {Title} to update our time restricted progress.", LoggerType.Achievements);

        // Clear out any timestamps that are older than the time to complete.
        ProgressTimestamps.RemoveAll(x => DateTime.UtcNow - x >= TimeToComplete);
        // Add in the range that we should.
        ProgressTimestamps.AddRange(Enumerable.Repeat(DateTime.UtcNow, amount));
        // check for completion after incrementing progress
        CheckCompletion();
    }

    /// <summary>
    /// Reset the progression of the achievement.
    /// </summary>
    public void ResetProgress() => ProgressTimestamps.Clear();


    /// <summary>
    /// Check if the Milestone has been met.
    /// </summary>
    public override void CheckCompletion()
    {
        if (IsCompleted || !MainHub.IsConnected)
            return;

        if (Progress >= MilestoneGoal)
        {
            // Mark the achievement as completed
            MarkCompleted();
        }
    }

    public override AchievementType GetAchievementType() => AchievementType.TimedProgress;
}
