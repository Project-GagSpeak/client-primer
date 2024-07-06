using FFStreamViewer.WebAPI.GagspeakConfiguration;
using FFStreamViewer.WebAPI.GagspeakConfiguration.Configurations;
using FFStreamViewer.WebAPI.GagspeakConfiguration.Models;
using FFStreamViewer.WebAPI.Services.Mediator;
using Lumina.Text.ReadOnly;

namespace FFStreamViewer.WebAPI.Services.ConfigurationServices;

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

    public ClientConfigurationManager(ILogger<ClientConfigurationManager> logger, OnFrameworkService onFrameworkService,
        GagspeakMediator GagspeakMediator, GagspeakConfigService configService, WardrobeConfigService wardrobeConfig,
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

    // define public access to various storages 
    public GagspeakConfig GagspeakConfig => _configService.Current;

    // the following below need to be replaced with internal functions for any functionality we determine we need to use these with.
    public WardrobeConfig WardrobeConfig => _wardrobeConfig.Current;

    public AliasConfig AliasConfig => _aliasConfig.Current;

    public PatternConfig PatternConfig => _patternConfig.Current;


    /// <summary> Saves the GagspeakConfig. </summary>
    public void Save()
    {
        var caller = new StackTrace().GetFrame(1)?.GetMethod()?.ReflectedType?.Name ?? "Unknown";
        _logger.LogDebug("{caller} Calling config save", caller);
        _configService.Save();
    }
}
