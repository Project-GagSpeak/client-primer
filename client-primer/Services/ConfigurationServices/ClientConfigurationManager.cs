using GagSpeak.GagspeakConfiguration;
using GagSpeak.GagspeakConfiguration.Configurations;
using GagSpeak.GagspeakConfiguration.Models;
using GagSpeak.Services.Mediator;
using GagSpeak.UpdateMonitoring;

namespace GagSpeak.Services.ConfigurationServices;

/// <summary>
/// This configuration manager helps manage the various interactions with all config files related to server-end activity.
/// <para> It provides a comprehensive interface for configuring servers, managing tags and nicknames, and handling authentication keys. </para>
/// </summary>
public class ClientConfigurationManager
{
    private readonly OnFrameworkService _frameworkUtils;            // a utilities class with methods that work with the Dalamud framework
    private readonly ILogger<ClientConfigurationManager> _logger;   // the logger for the server config manager
    private readonly GagspeakMediator _gagspeakMediator;            // the mediator for our Gagspeak Mediator
    private readonly GagspeakConfigService _configService;          // the primary gagspeak config service.
    private readonly WardrobeConfigService _wardrobeConfig;         // the config for the wardrobe service (restraint sets)
    private readonly AliasConfigService _aliasConfig;               // the config for the alias lists (puppeteer stuff)
    private readonly PatternConfigService _patternConfig;           // the config for the pattern service (toybox pattern storage))

    public ClientConfigurationManager(ILogger<ClientConfigurationManager> logger, 
        OnFrameworkService onFrameworkService, GagspeakMediator GagspeakMediator, 
        GagspeakConfigService configService, WardrobeConfigService wardrobeConfig,
        AliasConfigService aliasConfig, PatternConfigService patternConfig)
    {
        _logger = logger;
        _frameworkUtils = onFrameworkService;
        _gagspeakMediator = GagspeakMediator;
        _configService = configService;
        _wardrobeConfig = wardrobeConfig;
        _aliasConfig = aliasConfig;
        _patternConfig = patternConfig;

        // insure the nicknames and tag configs exist in the main server.
        if (_wardrobeConfig.Current.WardrobeStorage == null) { _wardrobeConfig.Current.WardrobeStorage = new(); }
        if (_aliasConfig.Current.AliasStorage == null) { _aliasConfig.Current.AliasStorage = new(); }
        if (_patternConfig.Current.PatternStorage == null) { _patternConfig.Current.PatternStorage = new(); }
    }

    // define public access to various storages (THESE ARE ONLY GETTERS, NO SETTERS)
    public GagspeakConfig GagspeakConfig => _configService.Current;
    private WardrobeConfig WardrobeConfig => _wardrobeConfig.Current;
    private AliasConfig AliasConfig => _aliasConfig.Current;
    private PatternConfig PatternConfig => _patternConfig.Current;

    public bool HasCreatedConfigs()
    {
        return (GagspeakConfig != null && WardrobeConfig != null && AliasConfig != null && PatternConfig != null);
    }

    /// <summary> Saves the GagspeakConfig. </summary>
    public void Save()
    {
        var caller = new StackTrace().GetFrame(1)?.GetMethod()?.ReflectedType?.Name ?? "Unknown";
        _logger.LogDebug("{caller} Calling config save", caller);
        _configService.Save();
    }


    /// <summary> Gets the total count of restraint sets in the wardrobe. </summary>
    internal int GetRestraintSetCount()
    {
        return _wardrobeConfig.Current.WardrobeStorage.RestraintSets.Count;
    }

    /// <summary> Gets index of selected restraint set. </summary>
    internal int GetSelectedSetIdx()
    {
        return _wardrobeConfig.Current.WardrobeStorage.SelectedRestraintSet;
    }

    /// <summary> Gets the DrawData from a wardrobes restraint set. </summary>
    internal List<AssociatedMod> GetAssociatedMods(int setIndex)
    {
        return _wardrobeConfig.Current.WardrobeStorage.RestraintSets[setIndex].AssociatedMods;
    }

    /// <summary> adds a mod to the restraint set's associated mods. </summary>
    internal void AddAssociatedMod(int setIndex, AssociatedMod mod)
    {
        // make sure the associated mods list is not already in the list, and if not, add & save.
        if (!_wardrobeConfig.Current.WardrobeStorage.RestraintSets[setIndex].AssociatedMods.Contains(mod))
        {
            _wardrobeConfig.Current.WardrobeStorage.RestraintSets[setIndex].AssociatedMods.Add(mod);
        }
        _wardrobeConfig.Save();
    }
}
