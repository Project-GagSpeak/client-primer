using Dalamud.Plugin.Services;
using static FFXIVClientStructs.FFXIV.Client.Game.UI.Achievement.Delegates;
using System.ComponentModel;
using System;

namespace GagSpeak.Achievements;

// construct a small private class within the manager for us to store the data we want to send over our IPC.

public class AchievementSaveData
{
    public AchievementSaveData()
    {
        foreach (AchievementModuleKind type in Enum.GetValues(typeof(AchievementModuleKind)))
            Components[type] = new AchievementComponent();
    }

    // Our Stored Achievements.
    public Dictionary<AchievementModuleKind, AchievementComponent> Components = new();

    // Our Stored Easter Egg Icons Discovery Progress.
    public Dictionary<string, bool> EasterEggIcons { get; set; } = new Dictionary<string, bool>()
    {
        {"Orders", false },
        {"Gags", false },
        {"Wardrobe", false },
        {"Puppeteer", false },
        {"Toybox", false }
    };

    public Dictionary<ushort, bool> VisitedWorldTour { get; set; } = new Dictionary<ushort, bool>()
    {
        {129, false }, // Limsa Lominsa
        {132, false }, // Gridania
        {130, false }, // Ul'dah
        {418, false }, // Ishgard
        {628, false }, // Kugane
        {819, false }, // The Crystarium
        {820, false }, // Eulmore
        {962, false }, // Old Sharlayan
        {963, false}, // Raz-At-Han
        {1185, false }, // Tuliyollal
        {1186, false }, // Solution 9
    };



    public LightSaveDataDto ToLightSaveDataDto()
    {
        var dto = new LightSaveDataDto
        {
            LightAchievementData = new List<LightAchievement>(),
            EasterEggIcons = this.EasterEggIcons,
            VisitedWorldTour = this.VisitedWorldTour
        };

        foreach (var achievementComponent in Components)
        {
            var componentKind = achievementComponent.Key;
            var component = achievementComponent.Value;

            foreach (var achievement in component.Achievements.Values)
            {
                var lightAchievement = new LightAchievement
                {
                    Component = componentKind,
                    Type = achievement.GetAchievementType(),
                    AchievementId = achievement.AchievementId,
                    Title = achievement.Title,
                    IsCompleted = achievement.IsCompleted,
                    Progress = GetProgress(achievement) ?? 0,
                    ConditionalTaskBegun = achievement is ConditionalProgressAchievement conditionalProgressAchievement ? conditionalProgressAchievement.ConditionalTaskBegun : false,
                    StartTime = GetStartTime(achievement) ?? DateTime.MinValue,
                    RecordedDateTimes = achievement is TimedProgressAchievement timedProgressAchievement ? (timedProgressAchievement.ProgressTimestamps ?? new List<DateTime>()) : new List<DateTime>(),
                    ActiveItems = achievement is DurationAchievement durationAchievement ? durationAchievement.ActiveItems : new Dictionary<string, DateTime>()
                };

                dto.LightAchievementData.Add(lightAchievement);
            }
        }
        return dto;
    }

    public void LoadFromLightSaveDataDto(LightSaveDataDto dto)
    {
        try
        {
            // Update Easter Egg Icons
            EasterEggIcons = new Dictionary<string, bool>(dto.EasterEggIcons);
            VisitedWorldTour = new Dictionary<ushort, bool>(dto.VisitedWorldTour);

            // Group LightAchievements by AchievementModuleKind
            var groupedAchievements = dto.LightAchievementData
                .GroupBy(a => a.Component)
                .ToDictionary(g => g.Key, g => g.ToList());

            // Iterate through each component of the Achievements dictionary.
            // For each component, get the list of light data, and call the Components function to update the achievements.
            foreach (var component in Components)
                if (groupedAchievements.TryGetValue(component.Key, out var lightAchievements))
                    component.Value.LoadFromLightAchievements(lightAchievements);
        }
        catch (Exception e)
        {
            StaticLogger.Logger.LogError(e, "Failed to load achievement data from save data.");
        }
    }

    private int? GetProgress(Achievement achievement)
    {
        if (achievement is ConditionalProgressAchievement conditionalProgressAchievement)
            return conditionalProgressAchievement.Progress;
        if (achievement is ProgressAchievement progressAchievement)
            return progressAchievement.Progress;
        return null;
    }

    private DateTime? GetStartTime(Achievement achievement)
    {
        if (achievement is TimeLimitConditionalAchievement timeLimited)
            return timeLimited.StartPoint;
        if (achievement is TimeRequiredConditionalAchievement timeRequired)
            return timeRequired.StartPoint;
        return null;
    }

    public Achievement? GetAchievementById(uint id)
    {
        // get the achievementInfo by the id.
        if(Achievements.AchievementMap.TryGetValue(id, out var info))
        {
            foreach (var component in Components.Values)
                if (component.Achievements.TryGetValue(info.Title, out var achievement))
                    return achievement;
        }
        return null;
    }
}

public class LightSaveDataDto
{
    /// <summary>
    /// a lightweight version that is easily compressible for IPC Transfer.
    /// </summary>
    public List<LightAchievement> LightAchievementData { get; set; }

    /// <summary>
    /// easter egg icons
    /// </summary>
    public Dictionary<string, bool> EasterEggIcons { get; set; }

    /// <summary>
    /// World Tour Visited Locations
    /// </summary>
    public Dictionary<ushort, bool> VisitedWorldTour { get; set; }
}

public struct LightAchievement
{
    /// <summary>
    /// The component the achievement belongs to.
    /// </summary>
    public AchievementModuleKind Component { get; set; }

    /// <summary>
    /// The kind of achievement it is. (Useful for type casting)
    /// </summary>
    public AchievementType Type { get; set; }

    /// <summary>
    /// the Unique Identifier for the achievement.
    /// </summary>
    public uint AchievementId { get; set; }

    /// <summary>
    /// The name of the Achievement (Relevant to know which to replace) ((Also reflects the KEY in the component dictionary))
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets if the achievement was completed or not.
    /// </summary>
    public bool IsCompleted { get; set; }

    /// <summary>
    /// latest progress (for ProgressAchievements & ConditionalProgressAchievements & TimedProgress)
    /// </summary>
    public int Progress { get; set; }

    /// <summary>
    /// Gets if the conditionalTaskBegin is true (for ConditionalProgressAchievements)
    /// </summary>
    public bool ConditionalTaskBegun { get; set; }

    /// <summary>
    /// Gets StartTime (for TimedProgressAchievements & TimeRequired/TimeLimited)
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// Stores recorded times things in TimedProgressAchievements handle.
    /// </summary>
    public List<DateTime> RecordedDateTimes { get; set; }

    /// <summary>
    /// the list of items that are being monitored (for duration achievements)
    /// </summary>
    public Dictionary<string, DateTime> ActiveItems { get; set; }
}
