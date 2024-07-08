using Dalamud.Game;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFStreamViewer.Events;
using FFStreamViewer.Livestream;
using FFStreamViewer.Services;
using FFStreamViewer.UI;
using FFStreamViewer.WebAPI.UI.Permissions;
using FFStreamViewer.UI.Tabs.MediaTab;
using FFStreamViewer.WebAPI;
using FFStreamViewer.WebAPI.GagspeakConfiguration;
using FFStreamViewer.WebAPI.Interop;
using FFStreamViewer.WebAPI.Interop.Ipc;
using FFStreamViewer.WebAPI.PlayerData.Factories;
using FFStreamViewer.WebAPI.PlayerData.Pairs;
using FFStreamViewer.WebAPI.Services;
using FFStreamViewer.WebAPI.Services.Events;
using FFStreamViewer.WebAPI.Services.Mediator;
using FFStreamViewer.WebAPI.Services.ConfigurationServices;
using FFStreamViewer.WebAPI.SignalR;
using FFStreamViewer.WebAPI.UI;
using FFStreamViewer.WebAPI.UI.Components;
using FFStreamViewer.WebAPI.UI.Handlers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using FFStreamViewer.WebAPI.UI.Components.Popup;
using OtterGui.Classes;
using FFStreamViewer.WebAPI.PlayerData.Data;
using FFStreamViewer.WebAPI.PlayerData.VisibleData;

namespace FFStreamViewer;

public sealed class FFStreamViewer : IDalamudPlugin
{
    private readonly IHost _host;  // the host builder for the plugin instance. (What makes everything work)
    public FFStreamViewer(IDalamudPluginInterface pi, IChatGui chatGui, IClientState clientState,
        ICommandManager commandManager, ICondition condition, IDataManager dataManager, 
        IDtrBar dtrBar, IFramework framework, IGameGui gameGui, IGameInteropProvider gameInteropProvider, 
        INotificationManager notificationManager, IObjectTable objectTable, IPluginLog pluginLog, 
        ISigScanner sigScanner, ITargetManager targetManager, ITextureProvider textureProvider)
    {
        // create the host builder for the plugin
        _host = ConstructHostBuilder(pi, pluginLog, commandManager, dataManager, framework, objectTable,
            clientState, condition, chatGui, gameGui, dtrBar, targetManager, notificationManager, textureProvider);
        // start up the host
        _ = _host.StartAsync();
    }

    // Method that creates the host builder for the FFStreamViewer plugin
    public IHost ConstructHostBuilder(IDalamudPluginInterface pi, IPluginLog pl, ICommandManager cm,
        IDataManager dm, IFramework fw, IObjectTable ot, IClientState cs, ICondition con, IChatGui cg,
        IGameGui gg, IDtrBar bar, ITargetManager tm, INotificationManager nm, ITextureProvider tp)
    {
        // create a new host builder for the plugin
        return new HostBuilder()
            // get the content root for our plugin
            .UseContentRoot(GetPluginContentRoot(pi))
            // configure the logging for the plugin
            .ConfigureLogging((hostContext, loggingBuilder) => GetPluginLogConfiguration(loggingBuilder, pl))
            // get the plugin service collection for our plugin
            .ConfigureServices((hostContext, serviceCollection)
                => GetPluginServices(serviceCollection, pi, pl, cm, dm, fw, ot, cs, con, cg, gg, bar, tm, nm, tp))
            // Build the host builder so it becomes an IHost object.
            .Build();
    }
    /// <summary> Gets the folder content location to know where the config files are saved. </summary>
    private string GetPluginContentRoot(IDalamudPluginInterface pi) => pi.ConfigDirectory.FullName;


    /// <summary> Gets the log configuration for the plugin. </summary>
    private void GetPluginLogConfiguration(ILoggingBuilder lb, IPluginLog pluginLog)
    {
        // clear our providers, add dalamud logging (the override that integrates ILogger into IPluginLog), and set the minimum level to trace
        lb.ClearProviders();
        lb.AddDalamudLogging(pluginLog);
        lb.SetMinimumLevel(LogLevel.Trace);
    }

    /// <summary> Gets the plugin services for the FFStreamViewer plugin. </summary>
    private IServiceCollection GetPluginServices(IServiceCollection collection, IDalamudPluginInterface pi,
        IPluginLog pl, ICommandManager cm, IDataManager dm, IFramework fw, IObjectTable ot, IClientState cs, ICondition con, IChatGui cg,
        IGameGui gg, IDtrBar bar, ITargetManager tm, INotificationManager nm, ITextureProvider tp)
    {
        return collection
            // add the general services to the collection
            .AddSingleton(new WindowSystem("FFStreamViewer"))
            .AddSingleton<FileDialogManager>()
            .AddSingleton(new Dalamud.Localization("FFStreamViewer.Localization.", "", useEmbedded: true))
            // add the generic services for FFStreamViewer
            .AddFFStreamViewerGeneric(pi, cs, con, dm, fw, gg, ot, tm, tp)
            // add the services related to the IPC calls for FFStreamViewer
            .AddFFStreamViewerIPC(pi)
            // add the services related to the configs for FFStreamViewer
            .AddFFStreamViewerConfigs(pi)
            // add the scoped services for FFStreamViewer
            .AddFFStreamViewerScoped(pi, tp, nm, cg)
            // add the hosted services for FFStreamViewer (these should all contain startAsync and stopAsync methods)
            .AddFFStreamViewerHosted();
    }

    public void Dispose()
    {
        _host.StopAsync().GetAwaiter().GetResult();
        _host.Dispose();
    }
}

public static class FFStreamViewerServiceExtensions
{
    #region GenericServices
    public static IServiceCollection AddFFStreamViewerGeneric(this IServiceCollection services,
        IDalamudPluginInterface pi, IClientState cs, ICondition con, IDataManager dm, IFramework fw,
        IGameGui gg, IObjectTable ot, ITargetManager tm, ITextureProvider tp)
    => services
        // Data Services
        .AddSingleton<FFSV_Config>()
        .AddSingleton<CommandManager>()
        // Events Services
        .AddSingleton<MediaError>()
        .AddSingleton((s) => new EventAggregator(pi.ConfigDirectory.FullName,
            s.GetRequiredService<ILogger<EventAggregator>>(), s.GetRequiredService<GagspeakMediator>()))
        // Livestream Services
        .AddSingleton<MediaGameObject>()
        .AddSingleton<MediaCameraObject>()
        .AddSingleton<MediaManager>()
        .AddSingleton<MediaObject>()
        // Service Services
        .AddSingleton<FrameworkManager>()
        .AddSingleton<MessageService>()
        .AddSingleton<BackupService>()
        .AddSingleton<ConfigMigrationService>()
        .AddSingleton<FilenameService>()
        .AddSingleton<SaveService>()
        // UI Services
        .AddSingleton<FFSV_WindowManager>()
        .AddSingleton<MediaTab>()
        .AddSingleton<WebAPITestingTab>()
        .AddSingleton<MainWindow>()
        .AddSingleton<DebugWindow>()
        .AddSingleton<FFStreamViewerChangelog>()
        // Utilities Services
        .AddSingleton<ILoggerProvider, Microsoft.Extensions.Logging.Console.ConsoleLoggerProvider>()
        // ((Additional Below is specifically for the implemented client-server related test classes))
        .AddSingleton<FFStreamViewerHost>()
        // UI general services
        .AddSingleton<IdDisplayHandler>()
        .AddSingleton<SelectPairForTagUi>()
        .AddSingleton<TagHandler>()
        .AddSingleton<UserPairPermsSticky>()
        // WebAPI Services
        .AddSingleton<ApiController>()
        .AddSingleton<HubFactory>()
        .AddSingleton<TokenProvider>()
        // PlayerData Services
        .AddSingleton<PlayerCharacterManager>()
        .AddSingleton<GameObjectHandlerFactory>()
        .AddSingleton<PairFactory>()
        .AddSingleton<PairHandlerFactory>()
        .AddSingleton<PairManager>()
        // Service Services
        .AddSingleton<ClientConfigurationManager>()
        .AddSingleton<ServerConfigurationManager>()
        .AddSingleton<GagspeakMediator>()
        .AddSingleton((s) => new GagspeakProfileManager(s.GetRequiredService<ILogger<GagspeakProfileManager>>(),
            s.GetRequiredService<GagspeakConfigService>(), s.GetRequiredService<GagspeakMediator>(), 
            s.GetRequiredService<ApiController>(), pi, tp))
        .AddSingleton((s) => new OnFrameworkService(s.GetRequiredService<ILogger<OnFrameworkService>>(),
            cs, con, dm, fw, gg, tm, ot,
            s.GetRequiredService<GagspeakMediator>()));
    #endregion GenericServices

    #region IpcServices
    public static IServiceCollection AddFFStreamViewerIPC(this IServiceCollection services, IDalamudPluginInterface pi)
    => services
        .AddSingleton((s) => new IpcCallerMoodles(s.GetRequiredService<ILogger<IpcCallerMoodles>>(), pi,
            s.GetRequiredService<OnFrameworkService>(), s.GetRequiredService<GagspeakMediator>()))
        .AddSingleton((s) => new IpcManager(s.GetRequiredService<ILogger<IpcManager>>(),
            s.GetRequiredService<GagspeakMediator>(), s.GetRequiredService<IpcCallerMoodles>()));

    #endregion IpcServices
    #region ConfigServices
    public static IServiceCollection AddFFStreamViewerConfigs(this IServiceCollection services, IDalamudPluginInterface pi)
    => services
        // client-end configs
        .AddSingleton((s) => new GagspeakConfigService(pi.ConfigDirectory.FullName))
        .AddSingleton((s) => new WardrobeConfigService(pi.ConfigDirectory.FullName))
        .AddSingleton((s) => new AliasConfigService(pi.ConfigDirectory.FullName))
        .AddSingleton((s) => new PatternConfigService(pi.ConfigDirectory.FullName))
        // server-end configs
        .AddSingleton((s) => new ServerConfigService(pi.ConfigDirectory.FullName))
        .AddSingleton((s) => new NicknamesConfigService(pi.ConfigDirectory.FullName))
        .AddSingleton((s) => new ServerTagConfigService(pi.ConfigDirectory.FullName));
    /*.AddSingleton((s) => new ConfigurationMigrator(s.GetRequiredService<ILogger<ConfigurationMigrator>>(), pi)); May need this to migrate our configs */

    #endregion ConfigServices
    #region ScopedServices
    public static IServiceCollection AddFFStreamViewerScoped(this IServiceCollection services, IDalamudPluginInterface pi, 
        ITextureProvider tp, INotificationManager nm, IChatGui cg)
    => services
        // WebAPI Services

        // GagSpeak Configuration Services

        // Interop Services

        // PlayerData Services
        .AddScoped<OnlinePlayerManager>()
        // Service Services
        .AddScoped<DrawEntityFactory>()
        .AddScoped<UiFactory>()
        .AddScoped<SelectTagForPairUi>()
        .AddScoped<WindowMediatorSubscriberBase, SettingsUi>()
        .AddScoped<WindowMediatorSubscriberBase, IntroUi>()
        .AddScoped<WindowMediatorSubscriberBase, CompactUi>()
        .AddScoped<WindowMediatorSubscriberBase, PopoutProfileUi>()
        //.AddScoped<WindowMediatorSubscriberBase, DataAnalysisUi>()
        .AddScoped<WindowMediatorSubscriberBase, EventViewerUI>()
        .AddScoped<WindowMediatorSubscriberBase, EditProfileUi>((s) => new EditProfileUi(s.GetRequiredService<ILogger<EditProfileUi>>(),
            s.GetRequiredService<GagspeakMediator>(), s.GetRequiredService<ApiController>(), tp, s.GetRequiredService<UiSharedService>(),
            s.GetRequiredService<FileDialogManager>(), s.GetRequiredService<GagspeakProfileManager>()))
        .AddScoped<WindowMediatorSubscriberBase, PopupHandler>()
        .AddScoped<IStickyUiHandler, VerificationPopupHandler>()
        .AddScoped<CacheCreationService>()
        .AddScoped<OnlinePlayerManager>()
        .AddScoped((s) => new UiService(s.GetRequiredService<ILogger<UiService>>(), pi.UiBuilder, s.GetRequiredService<GagspeakConfigService>(),
            s.GetRequiredService<WindowSystem>(), s.GetServices<WindowMediatorSubscriberBase>(),
            s.GetRequiredService<UiFactory>(), s.GetRequiredService<GagspeakMediator>()))
/*        .AddScoped((s) => new CommandManagerService(commandManager, s.GetRequiredService<PerformanceCollectorService>(),
            s.GetRequiredService<ServerConfigurationManager>(), s.GetRequiredService<CacheMonitor>(), s.GetRequiredService<ApiController>(),
            s.GetRequiredService<GagspeakMediator>(), s.GetRequiredService<GagspeakConfigService>())) */
        .AddScoped((s) => new NotificationService(s.GetRequiredService<ILogger<NotificationService>>(),
            s.GetRequiredService<GagspeakMediator>(), nm, cg, s.GetRequiredService<GagspeakConfigService>()))
        .AddScoped((s) => new UiSharedService(s.GetRequiredService<ILogger<UiSharedService>>(), s.GetRequiredService<IpcManager>(), s.GetRequiredService<ApiController>(),
            s.GetRequiredService<OnFrameworkService>(), pi, s.GetRequiredService<Dalamud.Localization>(),
            s.GetRequiredService<ClientConfigurationManager>(), s.GetRequiredService<ServerConfigurationManager>(), s.GetRequiredService<GagspeakMediator>()));


    #endregion ScopedServices
    #region HostedServices
    public static IServiceCollection AddFFStreamViewerHosted(this IServiceCollection services)
    => services
        // WebAPI Services

        // GagSpeak Configuration Services

        // Interop Services

        // PlayerData Services

        // Service Services
        .AddHostedService(p => p.GetRequiredService<GagspeakMediator>())
        .AddHostedService(p => p.GetRequiredService<OnFrameworkService>())
        .AddHostedService(p => p.GetRequiredService<EventAggregator>())
        // add our main Plugin.cs file as a hosted ;
        .AddHostedService<FFStreamViewerHost>();
    #endregion HostedServices
}
