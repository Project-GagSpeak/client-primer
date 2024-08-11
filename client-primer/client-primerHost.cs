using Dalamud.Game;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using GagSpeak.GagspeakConfiguration;
using GagSpeak.Interop;
using GagSpeak.Interop.Ipc;
using GagSpeak.Interop.IpcHelpers.Penumbra;
using GagSpeak.MufflerCore.Handler;
using GagSpeak.PlayerData.Data;
using GagSpeak.PlayerData.Factories;
using GagSpeak.PlayerData.Handlers;
using GagSpeak.PlayerData.Pairs;
using GagSpeak.PlayerData.PrivateRooms;
using GagSpeak.PlayerData.Services;
using GagSpeak.ResourceManager;
using GagSpeak.ResourceManager.Loaders;
using GagSpeak.ResourceManager.ResourceSpawn;
using GagSpeak.Services;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Data;
using GagSpeak.Services.Events;
using GagSpeak.Services.Mediator;
using GagSpeak.Services.Textures;
using GagSpeak.Toybox.Services;
using GagSpeak.Toybox.SimulatedVibe;
using GagSpeak.UI;
using GagSpeak.UI.Components;
using GagSpeak.UI.Components.Popup;
using GagSpeak.UI.Components.UserPairList;
using GagSpeak.UI.Handlers;
using GagSpeak.UI.MainWindow;
using GagSpeak.UI.Profile;
using GagSpeak.UI.Tabs.WardrobeTab;
using GagSpeak.UI.UiGagSetup;
using GagSpeak.UI.UiOrders;
using GagSpeak.UI.UiPuppeteer;
using GagSpeak.UI.UiRemote;
using GagSpeak.UI.UiToybox;
using GagSpeak.UI.UiWardrobe;
using GagSpeak.UpdateMonitoring;
using GagSpeak.UpdateMonitoring.Chat;
using GagSpeak.UpdateMonitoring.Chat.ChatMonitors;
using GagSpeak.WebAPI;
using Interop.Ipc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OtterGui.Log;
using Penumbra.GameData.Data;
using Penumbra.GameData.DataContainers;
using UI.UiRemote;

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
            clientState, condition, chatGui, gameGui, gameInteropProvider, dtrBar, sigScanner, targetManager,
            notificationManager, textureProvider);
        // start up the host
        _ = _host.StartAsync();
    }

    // Method that creates the host builder for the GagSpeak plugin
    public IHost ConstructHostBuilder(IDalamudPluginInterface pi, IPluginLog pl, ICommandManager cm,
        IDataManager dm, IFramework fw, IObjectTable ot, IClientState cs, ICondition con, IChatGui cg,
        IGameGui gg, IGameInteropProvider gip, IDtrBar bar, ISigScanner ss, ITargetManager tm,
        INotificationManager nm, ITextureProvider tp)
    {
        // create a new host builder for the plugin
        return new HostBuilder()
            // get the content root for our plugin
            .UseContentRoot(GetPluginContentRoot(pi))
            // configure the logging for the plugin
            .ConfigureLogging((hostContext, loggingBuilder) => GetPluginLogConfiguration(loggingBuilder, pl))
            // get the plugin service collection for our plugin
            .ConfigureServices((hostContext, serviceCollection)
                => GetPluginServices(serviceCollection, pi, pl, cm, dm, fw, ot, cs, con, cg, gg, gip, bar, ss, tm, nm, tp))
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
        IPluginLog pl, ICommandManager cm, IDataManager dm, IFramework fw, IObjectTable ot, IClientState cs, ICondition con,
        IChatGui cg, IGameGui gg, IGameInteropProvider gip, IDtrBar bar, ISigScanner ss, ITargetManager tm, INotificationManager nm, ITextureProvider tp)
    {
        return collection
            // add the general services to the collection
            .AddSingleton(new WindowSystem("GagSpeak"))
            .AddSingleton<FileDialogManager>()
            .AddSingleton(new Dalamud.Localization("GagSpeak.Localization.", "", useEmbedded: true))
            // add the generic services for GagSpeak
            .AddGagSpeakGeneric(pi, cs, con, dm, fw, gg, gip, ot, ss, tm, tp)
            // add the services related to the IPC calls for GagSpeak
            .AddGagSpeakIPC(pi, cs)
            // add the services related to the configs for GagSpeak
            .AddGagSpeakConfigs(pi)
            // add the scoped services for GagSpeak
            .AddGagSpeakScoped(pi, tp, nm, cg, dm)
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
        IGameGui gg, IGameInteropProvider gip, IObjectTable ot, ISigScanner ss, ITargetManager tm, ITextureProvider tp)
    => services
        // Events Services
        .AddSingleton((s) => new EventAggregator(pi.ConfigDirectory.FullName,
            s.GetRequiredService<ILogger<EventAggregator>>(), s.GetRequiredService<GagspeakMediator>()))
        .AddSingleton<GlamourFastUpdate>()

        // MufflerCore
        .AddSingleton((s) => new GagDataHandler(s.GetRequiredService<ILogger<GagDataHandler>>(),
            s.GetRequiredService<GagspeakMediator>(), s.GetRequiredService<ClientConfigurationManager>(), pi))
        .AddSingleton((s) => new Ipa_EN_FR_JP_SP_Handler(s.GetRequiredService<ILogger<Ipa_EN_FR_JP_SP_Handler>>(),
            s.GetRequiredService<ClientConfigurationManager>(), pi))

        .AddSingleton((s) => new ChatSender(ss))
        .AddSingleton((s) => new ChatInputDetour(ss, gip, s.GetRequiredService<ILogger<ChatInputDetour>>(),
            s.GetRequiredService<GagspeakConfigService>(), s.GetRequiredService<PlayerCharacterManager>(),
            s.GetRequiredService<GagManager>()))

        // PlayerData Services
        .AddSingleton<GagManager>()
        .AddSingleton<PadlockHandler>()
        .AddSingleton<PatternHandler>()
        .AddSingleton<ToyboxHandler>()
        .AddSingleton<RemoteHandler>()
        .AddSingleton<PlayerCharacterManager>()
        .AddSingleton<GameObjectHandlerFactory>()
        .AddSingleton<PairFactory>()
        .AddSingleton<PairHandlerFactory>()
        .AddSingleton<ParticipantFactory>()
        .AddSingleton<PrivateRoomFactory>()
        .AddSingleton<PairManager>()
        .AddSingleton<PrivateRoomManager>()
        // Toybox Services
        .AddSingleton<ConnectedDevice>()
        .AddSingleton<DeviceFactory>()
        .AddSingleton<DeviceHandler>()
        .AddSingleton<PatternPlayback>()
        .AddSingleton<AlarmHandler>()
        .AddSingleton<TriggerHandler>()

        // ResourceManager Services
        .AddSingleton((s) => new ResourceLoader(s.GetRequiredService<ILogger<ResourceLoader>>(),
            s.GetRequiredService<GagspeakMediator>(), s.GetRequiredService<GagspeakConfigService>(), 
            s.GetRequiredService<AvfxManager>(), s.GetRequiredService<ScdManager>(), dm, ss, gip))
        .AddSingleton((s) => new AvfxManager(s.GetRequiredService<ILogger<AvfxManager>>(), dm, pi.ConfigDirectory.FullName))
        .AddSingleton((s) => new ScdManager(s.GetRequiredService<ILogger<ScdManager>>(), dm, pi.ConfigDirectory.FullName))
        .AddSingleton((s) => new VfxSpawns(s.GetRequiredService<ILogger<VfxSpawns>>(),
            s.GetRequiredService<GagspeakMediator>(), s.GetRequiredService<ResourceLoader>(), cs, tm))
        // Utilities Services
        .AddSingleton<ILoggerProvider, Microsoft.Extensions.Logging.Console.ConsoleLoggerProvider>()
        .AddSingleton<GagSpeakHost>()
        .AddSingleton<ModAssociations>()
        // apparently adding services from submodules is cursed
        .AddSingleton((s) => new DictBonusItems(pi, new Logger(), dm))
        .AddSingleton((s) => new DictStain(pi, new Logger(), dm))
        .AddSingleton<ItemData>()
        .AddSingleton((s) => new ItemsByType(pi, new Logger(), dm))
        .AddSingleton((s) => new ItemsPrimaryModel(pi, new Logger(), dm, s.GetRequiredService<ItemsByType>()))
        .AddSingleton((s) => new ItemsSecondaryModel(pi, new Logger(), dm, s.GetRequiredService<ItemsByType>()))
        .AddSingleton((s) => new ItemsTertiaryModel(pi, new Logger(), dm, s.GetRequiredService<ItemsByType>(),
            s.GetRequiredService<ItemsSecondaryModel>()))

        // UI general services
        .AddSingleton<ActiveGagsPanel>()
        .AddSingleton((s) => new GagStoragePanel(s.GetRequiredService<ILogger<GagStoragePanel>>(),
            s.GetRequiredService<GagspeakMediator>(), s.GetRequiredService<ClientConfigurationManager>(),
            s.GetRequiredService<UiSharedService>(), s.GetRequiredService<DictStain>(),
            s.GetRequiredService<ItemData>(), s.GetRequiredService<TextureService>(), dm))

        .AddSingleton<ActiveRestraintSet>()
        .AddSingleton<RestraintSetManager>()
        .AddSingleton<RestraintStruggleSim>()
        .AddSingleton<MoodlesManager>()
        .AddSingleton((s) => new RestraintSetEditor(s.GetRequiredService<ILogger<RestraintSetEditor>>(),
            s.GetRequiredService<GagspeakMediator>(), s.GetRequiredService<UiSharedService>(),
            s.GetRequiredService<WardrobeHandler>(), s.GetRequiredService<DictStain>(),
            s.GetRequiredService<ItemData>(), s.GetRequiredService<DictBonusItems>(),
            s.GetRequiredService<TextureService>(), s.GetRequiredService<ModAssociations>(),
            s.GetRequiredService<PairManager>(), dm))
        .AddSingleton<RestraintCosmetics>()
        .AddSingleton<WardrobeHandler>()


        .AddSingleton<AliasTable>()

        .AddSingleton<ToyboxOverview>()
        .AddSingleton<ToyboxPatterns>()
        .AddSingleton<ToyboxPrivateRooms>()
        .AddSingleton<ToyboxTriggerManager>()
        .AddSingleton<ToyboxAlarmManager>()
        .AddSingleton<ToyboxCosmetics>()
        .AddSingleton((s) => new VibeSimAudio(s.GetRequiredService<ILogger<VibeSimAudio>>(), pi))

        .AddSingleton<OrdersViewActive>()
        .AddSingleton<OrdersCreator>()
        .AddSingleton<OrdersAssigner>()

        .AddSingleton<IdDisplayHandler>()
        .AddSingleton<SelectPairForTagUi>()
        .AddSingleton<TagHandler>()
        .AddSingleton<UserPairListHandler>()
        // UI Extras services
        .AddSingleton<MainUiHomepage>()
        .AddSingleton<MainUiWhitelist>()
        .AddSingleton<MainUiAccount>()
        // WebAPI Services
        .AddSingleton<ApiController>()
        .AddSingleton<HubFactory>()
        .AddSingleton<TokenProvider>()
        // Service Services
        .AddSingleton<ToyboxVibeService>()
        .AddSingleton<ToyboxRemoteService>()
        .AddSingleton<GlamourChangedService>()
        .AddSingleton<ClientConfigurationManager>()
        .AddSingleton<ServerConfigurationManager>()
        .AddSingleton<GagspeakMediator>()
        .AddSingleton((s) => new ProfileService(s.GetRequiredService<ILogger<ProfileService>>(),
            s.GetRequiredService<GagspeakMediator>(), s.GetRequiredService<ApiController>(),
            s.GetRequiredService<ProfileFactory>()))
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
            s.GetRequiredService<OnFrameworkService>(), s.GetRequiredService<GagspeakMediator>(),
            s.GetRequiredService<GlamourFastUpdate>()))
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
        .AddSingleton((s) => new AlarmConfigService(pi.ConfigDirectory.FullName))
        .AddSingleton((s) => new TriggerConfigService(pi.ConfigDirectory.FullName))
        // server-end configs
        .AddSingleton((s) => new ServerConfigService(pi.ConfigDirectory.FullName))
        .AddSingleton((s) => new NicknamesConfigService(pi.ConfigDirectory.FullName))
        .AddSingleton((s) => new ServerTagConfigService(pi.ConfigDirectory.FullName));
    /*.AddSingleton((s) => new ConfigurationMigrator(s.GetRequiredService<ILogger<ConfigurationMigrator>>(), pi)); May need this to migrate our configs */

    #endregion ConfigServices
    #region ScopedServices
    public static IServiceCollection AddGagSpeakScoped(this IServiceCollection services, IDalamudPluginInterface pi,
        ITextureProvider tp, INotificationManager nm, IChatGui cg, IDataManager dm)
    => services
        // Service Services
        .AddScoped<ProfileFactory>()
        .AddScoped<DrawEntityFactory>()
        .AddScoped<UiFactory>()
        .AddScoped<SelectTagForPairUi>()
        .AddScoped<WindowMediatorSubscriberBase, SettingsUi>()
        .AddScoped<WindowMediatorSubscriberBase, IntroUi>()
        .AddScoped<WindowMediatorSubscriberBase, MainWindowUI>((s) => new MainWindowUI(s.GetRequiredService<ILogger<MainWindowUI>>(),
            s.GetRequiredService<GagspeakMediator>(), s.GetRequiredService<UiSharedService>(),
            s.GetRequiredService<ApiController>(), s.GetRequiredService<GagspeakConfigService>(),
            s.GetRequiredService<PairManager>(), s.GetRequiredService<ServerConfigurationManager>(),
            s.GetRequiredService<MainUiHomepage>(), s.GetRequiredService<MainUiWhitelist>(),
            s.GetRequiredService<MainUiAccount>(), s.GetRequiredService<DrawEntityFactory>(), pi))
        .AddScoped<WindowMediatorSubscriberBase, PopoutProfileUi>()
        .AddScoped<WindowMediatorSubscriberBase, EventViewerUI>()
        .AddScoped<WindowMediatorSubscriberBase, RemotePersonal>()
        .AddScoped<WindowMediatorSubscriberBase, RemotePatternMaker>()
        // RemoteController made via the factory is defined via the factory and not here.
        .AddScoped<WindowMediatorSubscriberBase, GagSetupUI>()
        .AddScoped<WindowMediatorSubscriberBase, WardrobeUI>()
        .AddScoped<WindowMediatorSubscriberBase, PuppeteerUI>()
        .AddScoped<WindowMediatorSubscriberBase, ToyboxUI>()
        .AddScoped<WindowMediatorSubscriberBase, OrdersUI>()
        .AddScoped<WindowMediatorSubscriberBase, BlindfoldUI>((s) => new BlindfoldUI(s.GetRequiredService<ILogger<BlindfoldUI>>(),
            s.GetRequiredService<GagspeakMediator>(), pi, s.GetRequiredService<ClientConfigurationManager>(),
            s.GetRequiredService<UiSharedService>(), tp))
        .AddScoped<WindowMediatorSubscriberBase, EditProfileUi>((s) => new EditProfileUi(s.GetRequiredService<ILogger<EditProfileUi>>(),
            s.GetRequiredService<GagspeakMediator>(), s.GetRequiredService<ApiController>(), tp, s.GetRequiredService<UiSharedService>(),
            s.GetRequiredService<FileDialogManager>(), s.GetRequiredService<ProfileService>()))
        .AddScoped<WindowMediatorSubscriberBase, PopupHandler>()
        .AddScoped<IPopupHandler, VerificationPopupHandler>()
        .AddScoped<IPopupHandler, SavePatternPopupHandler>()
        .AddScoped<CacheCreationService>()
        .AddScoped((s) => new CosmeticTexturesService(tp, pi))
        .AddScoped<TextureService>()
        .AddScoped<OnlinePairManager>()
        .AddScoped<VisiblePairManager>()
        .AddScoped((s) => new TextureService(pi.UiBuilder, dm, tp))
        .AddScoped((s) => new UiService(s.GetRequiredService<ILogger<UiService>>(), pi.UiBuilder, s.GetRequiredService<GagspeakConfigService>(),
            s.GetRequiredService<WindowSystem>(), s.GetServices<WindowMediatorSubscriberBase>(),
            s.GetRequiredService<UiFactory>(), s.GetRequiredService<GagspeakMediator>(), s.GetRequiredService<FileDialogManager>()))
        /*        .AddScoped((s) => new CommandManagerService(commandManager, s.GetRequiredService<PerformanceCollectorService>(),
                    s.GetRequiredService<ServerConfigurationManager>(), s.GetRequiredService<CacheMonitor>(), s.GetRequiredService<ApiController>(),
                    s.GetRequiredService<GagspeakMediator>(), s.GetRequiredService<GagspeakConfigService>())) */
        .AddScoped((s) => new NotificationService(s.GetRequiredService<ILogger<NotificationService>>(),
            s.GetRequiredService<GagspeakMediator>(), nm, cg, s.GetRequiredService<GagspeakConfigService>()))
        .AddScoped((s) => new UiSharedService(s.GetRequiredService<ILogger<UiSharedService>>(), s.GetRequiredService<GagspeakMediator>(),
            s.GetRequiredService<Dalamud.Localization>(), s.GetRequiredService<ApiController>(),
            s.GetRequiredService<ClientConfigurationManager>(), s.GetRequiredService<ServerConfigurationManager>(),
            s.GetRequiredService<OnFrameworkService>(), s.GetRequiredService<IpcManager>(), pi, tp));


    #endregion ScopedServices
    #region HostedServices
    public static IServiceCollection AddGagSpeakHosted(this IServiceCollection services)
    => services
        .AddHostedService(p => p.GetRequiredService<GagspeakMediator>())
        .AddHostedService(p => p.GetRequiredService<OnFrameworkService>())
        .AddHostedService(p => p.GetRequiredService<EventAggregator>())
        // add our main Plugin.cs file as a hosted ;
        .AddHostedService<GagSpeakHost>();
    #endregion HostedServices
}
