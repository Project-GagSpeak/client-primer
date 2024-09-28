using Dalamud.Plugin.Services;

namespace GagSpeak.Achievements;

// construct a small private class within the manager for us to store the data we want to send over our IPC.

public class AchievementSaveData
{
    public AchievementSaveData(INotificationManager _completionNotifier)
    {
        // create a blank achievements dictionary for each type of achievement.
        foreach (AchievementType type in Enum.GetValues(typeof(AchievementType)))
        {
            Achievements[type] = new AchievementComponent(_completionNotifier);
        }
    }
    // Our Stored Achievements.
    public Dictionary<AchievementType, AchievementComponent> Achievements = new();

    // Our Stored Easter Egg Icons Discovery Progress.
    public Dictionary<string, bool> EasterEggIcons { get; set; } = new Dictionary<string, bool>()
    {
        {"Orders", false },
        {"Gags", false },
        {"Wardrobe", false },
        {"Puppeteer", false },
        {"Toybox", false }
    };
}
