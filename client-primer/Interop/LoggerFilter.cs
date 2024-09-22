using GagspeakAPI.Enums;
using System.Collections.Generic;

// intended to help with filtering out log message to certain types.
public static class LoggerFilter
{
    public static readonly HashSet<LoggerType> FilteredCategories = new HashSet<LoggerType> { LoggerType.None };
    public static void AddAllowedCategory(LoggerType category)
    {
        FilteredCategories.Add(category);
        EnsureNoneIsIncluded();
    }

    public static void AddAllowedCategories(HashSet<LoggerType> categories)
    {
        foreach (var category in categories)
            FilteredCategories.Add(category);

        EnsureNoneIsIncluded();
    }

    public static void RemoveAllowedCategory(LoggerType category)
    {
        FilteredCategories.Remove(category);
        EnsureNoneIsIncluded();
    }

    private static void EnsureNoneIsIncluded()
    {
        if (!FilteredCategories.Contains(LoggerType.None))
        {
            AddAllowedCategory(LoggerType.None);
        }
    }

    public static void ClearAllowedCategories()
    {
        FilteredCategories.Clear();
        EnsureNoneIsIncluded();
    }

    public static bool ShouldLog(LoggerType category)
    {
        return FilteredCategories.Contains(category);
    }

    // return the sane "all on" list.
    public static HashSet<LoggerType> GetAllRecommendedFilters()
    {
        return new HashSet<LoggerType>
        {
            LoggerType.None, LoggerType.Mediator, LoggerType.IpcGagSpeak, LoggerType.IpcCustomize,
            LoggerType.IpcGlamourer, LoggerType.IpcMare, LoggerType.IpcMoodles, LoggerType.IpcPenumbra,
            LoggerType.GagManagement, LoggerType.PadlockManagement, LoggerType.ClientPlayerData, LoggerType.GameObjects,
            LoggerType.PairManagement, LoggerType.OnlinePairs, LoggerType.VisiblePairs, LoggerType.PrivateRoom,
            LoggerType.Notification, LoggerType.Profiles, LoggerType.Cosmetics, LoggerType.ContextDtr, LoggerType.PatternHub,
            LoggerType.Safeword, LoggerType.Restraints, LoggerType.Puppeteer, LoggerType.ToyboxDevices,
            LoggerType.ToyboxPatterns, LoggerType.ToyboxTriggers, LoggerType.ToyboxAlarms, LoggerType.VibeControl,
            LoggerType.SpatialAudioController, LoggerType.UiCore, LoggerType.UserPairDrawer, LoggerType.Permissions,
            LoggerType.Simulation, LoggerType.PiShock, LoggerType.ApiCore, LoggerType.Callbacks, LoggerType.HubFactory,
        };
    }


    public static void LogTrace(this ILogger logger, string? message, LoggerType type = LoggerType.None)
    {
        if (ShouldLog(type)) 
            logger.Log(LogLevel.Trace, message);
    }

    public static void LogDebug(this ILogger logger, string? message, LoggerType type = LoggerType.None)
    {
        if (ShouldLog(type))
            logger.Log(LogLevel.Debug, message);
    }

    public static void LogInformation(this ILogger logger, string? message, LoggerType type = LoggerType.None)
    {
        if (ShouldLog(type))
            logger.Log(LogLevel.Information, message);
    }

    public static void LogWarning(this ILogger logger, string? message, LoggerType type = LoggerType.None)
    {
        if (ShouldLog(type))
            logger.Log(LogLevel.Warning, message);
    }

    public static void LogError(this ILogger logger, string? message, LoggerType type = LoggerType.None)
    {
        if (ShouldLog(type))
            logger.Log(LogLevel.Error, message);
    }
}
