using Dalamud.Utility;
using GagSpeak.Achievements;
using System.ComponentModel;

namespace GagSpeak.Achievements;

public class AchievementManager
{
    private Dictionary<AchievementType, AchievementComponent> Components;

    public AchievementManager()
    {
        Components = new Dictionary<AchievementType, AchievementComponent>();
    }

    public void AddComponent(AchievementComponent component)
    {
        if (!Components.ContainsKey(component.Type))
        {
            Components[component.Type] = component;
        }
        else
        {
            throw new InvalidOperationException($"Component of type {component.Type} already exists.");
        }
    }

    public AchievementComponent GetComponent(AchievementType type)
    {
        if (Components.TryGetValue(type, out var component))
        {
            return component;
        }
        throw new KeyNotFoundException($"Component of type {type} not found.");
    }

    public void AddAchievementToComponent<T>(AchievementType type, Achievement<T> achievement) where T : IComparable<T>
    {
        if (Components.ContainsKey(type))
        {
            Components[type].AddAchievement(achievement);
        }
        else
        {
            var component = new AchievementComponent(type);
            component.AddAchievement(achievement);
            Components[type] = component;
        }
    }

    public void UpdateAchievementProgress<T>(AchievementType type, Enum achievementName, T newProgress) where T : IComparable<T>
    {
        var component = GetComponent(type);
        var achievement = component.Achievements.OfType<Achievement<T>>().FirstOrDefault(a => a.Name.Equals(achievementName));
        if (achievement != null)
        {
            achievement.AdvanceProgression(newProgress);
        }
        else
        {
            throw new InvalidOperationException($"Achievement {achievementName} not found in component {type}.");
        }
    }
}
