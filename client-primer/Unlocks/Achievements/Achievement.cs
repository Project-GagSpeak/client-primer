using Dalamud.Interface;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using GagSpeak.Services.Mediator;
using static PInvoke.User32;

namespace GagSpeak.Achievements;

public abstract class Achievement
{
    public INotificationManager Notify { get; }

    /// <summary>
    /// The Title of the Achievement Name.
    /// Should match one of the Const strings in the labels class.
    /// </summary>
    public string Title { get; init; }

    /// <summary>
    /// The Description of the Achievement.
    /// </summary>
    public string Description { get; init; }

    /// <summary>
    /// The Milestone Progress that must be reached to complete the achievement.
    /// This is mostly used for UI validation when displaying progress bars and not directly related
    /// to the CheckCompletion method. That should be dependently used by Achievement Types.
    /// </summary>
    public int MilestoneGoal { get; init; }

    /// <summary>
    /// Displayed before the progressString.
    /// </summary>
    public string PrefixText { get; init; }

    /// <summary>
    /// Displayed After the progressString.
    /// </summary>
    public string SuffixText { get; init; }


    /// <summary>
    /// If the achievement has been completed.
    /// </summary>
    public bool IsCompleted { get; set; } = false;

    public bool IsSecretAchievement { get; init; }

    protected Achievement(INotificationManager notify, string title, string desc, int goal, string prefix, string suffix, bool isSecret = false)
    {
        Notify = notify;
        IsCompleted = false;
        Title = title;
        Description = desc;
        MilestoneGoal = goal;
        PrefixText = prefix;
        SuffixText = suffix;
        IsSecretAchievement = isSecret;
    }

    /// <summary>
    /// Check if the achievement is complete
    /// </summary>
    public abstract void CheckCompletion();

    /// <summary>
    /// Force a requirement that all achievement types must report their progress
    /// </summary>
    public abstract int CurrentProgress();

    /// <summary>
    /// The string representation of our Progress. Used for Achievement Display.
    /// </summary>
    public abstract string ProgressString();

    /// <summary>
    /// Mark the achievement as completed
    /// </summary>
    protected void MarkCompleted()
    {
        StaticLogger.Logger.LogInformation("Achievement Completed: " + Title);
        IsCompleted = true;

        Notify.AddNotification(new Notification()
        {
            Content = "Completed Achievement: "+Title,
            Title = "Achievement Completed!",
            Type = NotificationType.Info,
            Icon = INotificationIcon.From(FontAwesomeIcon.Award),
            Minimized = false,
            InitialDuration = TimeSpan.FromSeconds(5)
        });
    }

    /// <summary>
    /// Useful for quick compression when doing data transfer
    /// </summary>
    public abstract AchievementType GetAchievementType();
}
