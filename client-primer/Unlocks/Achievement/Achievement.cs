namespace GagSpeak.Achievements;

public abstract class Achievement
{
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
    /// If the achievement has been completed.
    /// </summary>
    public bool IsCompleted { get; protected set; } = false;

    public bool IsSecretAchievement { get; init; }

    protected Achievement(string title, string description, bool isSecret = false)
    {
        Title = title;
        Description = description;
        IsCompleted = false;
        IsSecretAchievement = isSecret;
    }

    /// <summary>
    /// Check if the achievement is complete
    /// </summary>
    public abstract void CheckCompletion();

    /// <summary>
    /// Mark the achievement as completed
    /// </summary>
    protected void MarkCompleted()
    {
        IsCompleted = true;
        StaticLogger.Logger.LogInformation("Achievement [" + Title + "] Completed!");
    }
}
