using Dalamud.Game;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using GagSpeak.GagspeakConfiguration;
using GagSpeak.Interop;
using GagSpeak.Interop.Ipc;
using GagSpeak.MufflerCore.Handler;
using GagSpeak.PlayerData.Data;
using GagSpeak.PlayerData.Factories;
using GagSpeak.PlayerData.Handlers;
using GagSpeak.PlayerData.Pairs;
using GagSpeak.PlayerData.Services;
using GagSpeak.Services;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Events;
using GagSpeak.Services.Mediator;
using GagSpeak.UI;
using GagSpeak.UI.Components.Popup;
using GagSpeak.UI.Components.UserPairList;
using GagSpeak.UI.Handlers;
using GagSpeak.UI.MainWindow;
using GagSpeak.UI.Permissions;
using GagSpeak.UI.Profile;
using GagSpeak.UI.UiGagSetup;
using GagSpeak.UpdateMonitoring;
using GagSpeak.WebAPI;
using Interop.Ipc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace GagSpeak;

public sealed class GagSpeak : IDalamudPlugin
{
    private readonly IHost _host;  // the host builder for the plugin instance. (What makes everything work)
    public GagSpeak(IDalamudPluginInterface pi, IChatGui chatGui, IClientState clientState,
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

    // Method that creates the host builder for the GagSpeak plugin
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

    /// <summary> Gets the plugin services for the GagSpeak plugin. </summary>
    private IServiceCollection GetPluginServices(IServiceCollection collection, IDalamudPluginInterface pi,
        IPluginLog pl, ICommandManager cm, IDataManager dm, IFramework fw, IObjectTable ot, IClientState cs, ICondition con, IChatGui cg,
        IGameGui gg, IDtrBar bar, ITargetManager tm, INotificationManager nm, ITextureProvider tp)
    {
        return collection
            // add the general services to the collection
            .AddSingleton(new WindowSystem("GagSpeak"))
            .AddSingleton<FileDialogManager>()
            .AddSingleton(new Dalamud.Localization("GagSpeak.Localization.", "", useEmbedded: true))
            // add the generic services for GagSpeak
            .AddGagSpeakGeneric(pi, cs, con, dm, fw, gg, ot, tm, tp)
            // add the services related to the IPC calls for GagSpeak
            .AddGagSpeakIPC(pi, cs)
            // add the services related to the configs for GagSpeak
            .AddGagSpeakConfigs(pi)
            // add the scoped services for GagSpeak
            .AddGagSpeakScoped(pi, tp, nm, cg)
            // add the hosted services for GagSpeak (these should all contain startAsync and stopAsync methods)
            .AddGagSpeakHosted();
    }

    public void Dispose()
    {
        _host.StopAsync().GetAwaiter().GetResult();
        _host.Dispose();
    }
}

public static class GagSpeakServiceExtensions
{
    #region GenericServices
    public static IServiceCollection AddGagSpeakGeneric(this IServiceCollection services,
        IDalamudPluginInterface pi, IClientState cs, ICondition con, IDataManager dm, IFramework fw,
        IGameGui gg, IObjectTable ot, ITargetManager tm, ITextureProvider tp)
    => services
        // Events Services
        .AddSingleton((s) => new EventAggregator(pi.ConfigDirectory.FullName,
            s.GetRequiredService<ILogger<EventAggregator>>(), s.GetRequiredService<GagspeakMediator>()))
        // MufflerCore
        .AddSingleton((s) => new GagDataHandler(s.GetRequiredService<ILogger<GagDataHandler>>(),
            s.GetRequiredService<GagspeakMediator>(), s.GetRequiredService<ClientConfigurationManager>(), pi))
        .AddSingleton((s) => new Ipa_EN_FR_JP_SP_Handler(s.GetRequiredService<ILogger<Ipa_EN_FR_JP_SP_Handler>>(),
            s.GetRequiredService<ClientConfigurationManager>(), pi))
        // PlayerData Services
        .AddSingleton<GagManager>()
        .AddSingleton<PadlockHandler>()
        .AddSingleton<PlayerCharacterManager>()
        .AddSingleton<GameObjectHandlerFactory>()
        .AddSingleton<PairFactory>()
        .AddSingleton<PairHandlerFactory>()
        .AddSingleton<PairManager>()
        // Utilities Services
        .AddSingleton<ILoggerProvider, Microsoft.Extensions.Logging.Console.ConsoleLoggerProvider>()
        .AddSingleton<GagSpeakHost>()
        // UI general services
        .AddSingleton<IdDisplayHandler>()
        .AddSingleton<SelectPairForTagUi>()
        .AddSingleton<TagHandler>()
        .AddSingleton<UserPairListHandler>()
        .AddSingleton<UserPairPermsSticky>()
        // WebAPI Services
        .AddSingleton<ApiController>()
        .AddSingleton<HubFactory>()
        .AddSingleton<TokenProvider>()
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
    public static IServiceCollection AddGagSpeakIPC(this IServiceCollection services, IDalamudPluginInterface pi, IClientState cs)
    => services
        .AddSingleton((s) => new IpcCallerMoodles(s.GetRequiredService<ILogger<IpcCallerMoodles>>(), pi,
            s.GetRequiredService<OnFrameworkService>(), s.GetRequiredService<GagspeakMediator>()))
        .AddSingleton((s) => new IpcCallerPenumbra(s.GetRequiredService<ILogger<IpcCallerPenumbra>>(), pi,
            s.GetRequiredService<OnFrameworkService>(), s.GetRequiredService<GagspeakMediator>()))
        .AddSingleton((s) => new IpcCallerGlamourer(s.GetRequiredService<ILogger<IpcCallerGlamourer>>(), pi, cs,
            s.GetRequiredService<OnFrameworkService>(), s.GetRequiredService<GagspeakMediator>()))
        .AddSingleton((s) => new IpcCallerCustomize(s.GetRequiredService<ILogger<IpcCallerCustomize>>(), pi, cs,
            s.GetRequiredService<OnFrameworkService>(), s.GetRequiredService<GagspeakMediator>()))

        .AddSingleton((s) => new IpcManager(s.GetRequiredService<ILogger<IpcManager>>(),
            s.GetRequiredService<GagspeakMediator>(), s.GetRequiredService<IpcCallerCustomize>(),
            s.GetRequiredService<IpcCallerGlamourer>(), s.GetRequiredService<IpcCallerPenumbra>(),
            s.GetRequiredService<IpcCallerMoodles>()));

    #endregion IpcServices
    #region ConfigServices
    public static IServiceCollection AddGagSpeakConfigs(this IServiceCollection services, IDalamudPluginInterface pi)
    => services
        // client-end configs
        .AddSingleton((s) => new GagspeakConfigService(pi.ConfigDirectory.FullName))
        .AddSingleton((s) => new GagStorageConfigService(pi.ConfigDirectory.FullName))
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
    public static IServiceCollection AddGagSpeakScoped(this IServiceCollection services, IDalamudPluginInterface pi,
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
        .AddScoped<WindowMediatorSubscriberBase, MainWindowUI>()
        .AddScoped<WindowMediatorSubscriberBase, PopoutProfileUi>()
        .AddScoped<WindowMediatorSubscriberBase, EventViewerUI>()
        .AddScoped<WindowMediatorSubscriberBase, LovenseRemoteUI>() // might be factory driven later.
        .AddScoped<WindowMediatorSubscriberBase, GagSetupUI>((s) => new GagSetupUI(s.GetRequiredService<ILogger<GagSetupUI>>(),
            s.GetRequiredService<GagspeakMediator>(), s.GetRequiredService<UiSharedService>(), s.GetRequiredService<PadlockHandler>(), 
            s.GetRequiredService<PlayerCharacterManager>(), tp, pi))
        .AddScoped<WindowMediatorSubscriberBase, WardrobeUI>((s) => new WardrobeUI(s.GetRequiredService<ILogger<WardrobeUI>>(),
            s.GetRequiredService<GagspeakMediator>(), s.GetRequiredService<UiSharedService>(), tp, pi))
        .AddScoped<WindowMediatorSubscriberBase, PuppeteerUI>((s) => new PuppeteerUI(s.GetRequiredService<ILogger<PuppeteerUI>>(),
            s.GetRequiredService<GagspeakMediator>(), s.GetRequiredService<UiSharedService>(), 
            s.GetRequiredService<UserPairListHandler>(), tp, pi))
        .AddScoped<WindowMediatorSubscriberBase, ToyboxUI>((s) => new ToyboxUI(s.GetRequiredService<ILogger<ToyboxUI>>(),
            s.GetRequiredService<GagspeakMediator>(), s.GetRequiredService<UiSharedService>(), tp, pi))
        .AddScoped<WindowMediatorSubscriberBase, OrdersUI>((s) => new OrdersUI(s.GetRequiredService<ILogger<OrdersUI>>(), tp,
            s.GetRequiredService<GagspeakMediator>(), s.GetRequiredService<UiSharedService>(), pi))
        .AddScoped<WindowMediatorSubscriberBase, BlindfoldUI>((s) => new BlindfoldUI(s.GetRequiredService<ILogger<BlindfoldUI>>(),
            s.GetRequiredService<GagspeakMediator>(), pi, s.GetRequiredService<ClientConfigurationManager>(),
            s.GetRequiredService<UiSharedService>(), tp))
        .AddScoped<WindowMediatorSubscriberBase, EditProfileUi>((s) => new EditProfileUi(s.GetRequiredService<ILogger<EditProfileUi>>(),
            s.GetRequiredService<GagspeakMediator>(), s.GetRequiredService<ApiController>(), tp, s.GetRequiredService<UiSharedService>(),
            s.GetRequiredService<FileDialogManager>(), s.GetRequiredService<GagspeakProfileManager>()))
        .AddScoped<WindowMediatorSubscriberBase, PopupHandler>()
        .AddScoped<IStickyUiHandler, VerificationPopupHandler>()
        .AddScoped<CacheCreationService>()
        .AddScoped<OnlinePlayerManager>()
        .AddScoped((s) => new UiService(s.GetRequiredService<ILogger<UiService>>(), pi.UiBuilder, s.GetRequiredService<GagspeakConfigService>(),
            s.GetRequiredService<WindowSystem>(), s.GetServices<WindowMediatorSubscriberBase>(),
            s.GetRequiredService<UiFactory>(), s.GetRequiredService<GagspeakMediator>(), s.GetRequiredService<FileDialogManager>()))
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
    public static IServiceCollection AddGagSpeakHosted(this IServiceCollection services)
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
        .AddHostedService<GagSpeakHost>();
    #endregion HostedServices
}
