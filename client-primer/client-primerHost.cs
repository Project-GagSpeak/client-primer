using Dalamud.Game;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using GagSpeak.GagspeakConfiguration;
using GagSpeak.Hardcore;
using GagSpeak.Hardcore.Hotbar;
using GagSpeak.Hardcore.Movement;
using GagSpeak.Interop;
using GagSpeak.Interop.Ipc;
using GagSpeak.Interop.IpcHelpers.Moodles;
using GagSpeak.Interop.IpcHelpers.Penumbra;
using GagSpeak.MufflerCore.Handler;
using GagSpeak.PlayerData.Data;
using GagSpeak.PlayerData.Factories;
using GagSpeak.PlayerData.Handlers;
using GagSpeak.PlayerData.Pairs;
using GagSpeak.PlayerData.PrivateRooms;
using GagSpeak.PlayerData.Services;
using GagSpeak.Services;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Events;
using GagSpeak.Services.Mediator;
using GagSpeak.Services.Migrations;
using GagSpeak.Services.Textures;
using GagSpeak.Toybox.Controllers;
using GagSpeak.Toybox.Data;
using GagSpeak.Toybox.Services;
using GagSpeak.Toybox.SimulatedVibe;
using GagSpeak.UI;
using GagSpeak.UI.Components;
using GagSpeak.UI.Components.Popup;
using GagSpeak.UI.Components.UserPairList;
using GagSpeak.UI.Handlers;
using GagSpeak.UI.MainWindow;
using GagSpeak.UI.Permissions;
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
using GagSpeak.UpdateMonitoring.SpatialAudio.Loaders;
using GagSpeak.UpdateMonitoring.SpatialAudio.Managers;
using GagSpeak.UpdateMonitoring.SpatialAudio.Spawner;
using GagSpeak.UpdateMonitoring.Triggers;
using GagSpeak.Utils;
using GagSpeak.WebAPI;
using Interop.Ipc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OtterGui.Log;
using Penumbra.GameData.Data;
using Penumbra.GameData.DataContainers;
using UI.UiRemote;
using UpdateMonitoring;

namespace GagSpeak;

public sealed class GagSpeak : IDalamudPlugin
{
    private readonly IHost _host;  // the host builder for the plugin instance. (What makes everything work)
    public GagSpeak(IDalamudPluginInterface pi, IAddonLifecycle addonLifecycle, IChatGui chatGui,
        IClientState clientState, ICommandManager commandManager, ICondition condition,
        IDataManager dataManager, IDtrBar dtrBar, IFramework framework, IKeyState keyState,
        IGameGui gameGui, IGameInteropProvider gameInteropProvider, INotificationManager notificationManager,
        IObjectTable objectTable, IPluginLog pluginLog, ISigScanner sigScanner,
        ITargetManager targetManager, ITextureProvider textureProvider)
    {

        // create the host builder for the plugin
        _host = ConstructHostBuilder(pi, pluginLog, addonLifecycle, commandManager, dataManager, framework, keyState,
            objectTable, clientState, condition, chatGui, gameGui, gameInteropProvider, dtrBar,
            sigScanner, targetManager, notificationManager, textureProvider);
        // start up the host
        _ = _host.StartAsync();
    }

    // Method that creates the host builder for the GagSpeak plugin
    public IHost ConstructHostBuilder(IDalamudPluginInterface pi, IPluginLog pl, IAddonLifecycle alc,
        ICommandManager cm, IDataManager dm, IFramework fw, IKeyState ks, IObjectTable ot,
        IClientState cs, ICondition con, IChatGui cg, IGameGui gg, IGameInteropProvider gip,
        IDtrBar bar, ISigScanner ss, ITargetManager tm, INotificationManager nm, ITextureProvider tp)
    {
        // create a new host builder for the plugin
        return new HostBuilder()
            // get the content root for our plugin
            .UseContentRoot(GetPluginContentRoot(pi))
            // configure the logging for the plugin
            .ConfigureLogging((hostContext, loggingBuilder) => GetPluginLogConfiguration(loggingBuilder, pl))
            // get the plugin service collection for our plugin
            .ConfigureServices((hostContext, serviceCollection)
                => GetPluginServices(serviceCollection, pi, pl, alc, cm, dm, fw, ks, ot, cs, con, cg, gg, gip, bar, ss, tm, nm, tp))
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
        IPluginLog pl, IAddonLifecycle alc, ICommandManager cm, IDataManager dm, IFramework fw,
        IKeyState ks, IObjectTable ot, IClientState cs, ICondition con, IChatGui cg, IGameGui gg,
        IGameInteropProvider gip, IDtrBar bar, ISigScanner ss, ITargetManager tm, INotificationManager nm,
        ITextureProvider tp)
    {
        return collection
            // add the general services to the collection
            .AddSingleton(new WindowSystem("GagSpeak"))
            .AddSingleton<FileDialogManager>()
            .AddSingleton(new Dalamud.Localization("GagSpeak.Localization.", "", useEmbedded: true))
            // add the generic services for GagSpeak
            .AddGagSpeakGeneric(pi, alc, cs, cg, con, dm, bar, fw, ks, gg, gip, ot, ss, tm, tp)
            // add the services related to the IPC calls for GagSpeak
            .AddGagSpeakIPC(pi, cs)
            // add the services related to the configs for GagSpeak
            .AddGagSpeakConfigs(pi)
            // add the scoped services for GagSpeak
            .AddGagSpeakScoped(cs, cm, pi, tp, nm, cg, dm)
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
        IDalamudPluginInterface pi, IAddonLifecycle alc, IClientState cs, IChatGui cg, ICondition con,
        IDataManager dm, IDtrBar dtr, IFramework fw, IKeyState ks, IGameGui gg, IGameInteropProvider gip,
        IObjectTable ot, ISigScanner ss, ITargetManager tm, ITextureProvider tp)
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

        // Chat Services
        .AddSingleton((s) => new ChatBoxMessage(s.GetRequiredService<ILogger<ChatBoxMessage>>(),
            s.GetRequiredService<GagspeakMediator>(), s.GetRequiredService<PuppeteerHandler>(),
            s.GetRequiredService<ChatSender>(), s.GetRequiredService<TriggerController>(), cg, cs))
        .AddSingleton((s) => new ChatSender(ss))
        .AddSingleton((s) => new ChatInputDetour(ss, gip, s.GetRequiredService<ILogger<ChatInputDetour>>(),
            s.GetRequiredService<GagspeakConfigService>(), s.GetRequiredService<PlayerCharacterManager>(),
            s.GetRequiredService<GagManager>()))

        // Hardcore services.
        .AddSingleton<HotbarLocker>()
        .AddSingleton((s) => new AtkHelpers(gg))
        .AddSingleton((s) => new SettingsHardcore(s.GetRequiredService<ILogger<SettingsHardcore>>(),
            s.GetRequiredService<ApiController>(), s.GetRequiredService<UiSharedService>(),
            s.GetRequiredService<ClientConfigurationManager>(), s.GetRequiredService<HardcoreHandler>(),
            s.GetRequiredService<WardrobeHandler>(), s.GetRequiredService<PairManager>(),
            s.GetRequiredService<TextureService>(), s.GetRequiredService<DictStain>(),
            s.GetRequiredService<ItemData>(), dm))


        // PlayerData Services
        .AddSingleton<GagManager>()
        .AddSingleton<PadlockHandler>()
        .AddSingleton<PatternHandler>()
        .AddSingleton((s) => new HardcoreHandler(s.GetRequiredService<ILogger<HardcoreHandler>>(),
            s.GetRequiredService<GagspeakMediator>(), s.GetRequiredService<GagspeakConfigService>(),
            s.GetRequiredService<PairManager>(), s.GetRequiredService<WardrobeHandler>(),
            s.GetRequiredService<ApiController>(), tm))
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
        .AddSingleton<ToyboxFactory>()
        .AddSingleton<DeviceController>()
        .AddSingleton<PatternPlayback>()
        .AddSingleton<AlarmHandler>()
        .AddSingleton<TriggerHandler>()

        // UpdateMonitoring Services
        .AddSingleton((s) => new ActionMonitor(s.GetRequiredService<ILogger<ActionMonitor>>(),
            s.GetRequiredService<GagspeakMediator>(), s.GetRequiredService<ClientConfigurationManager>(),
            s.GetRequiredService<HotbarLocker>(), s.GetRequiredService<HardcoreHandler>(), s.GetRequiredService<WardrobeHandler>(),
            s.GetRequiredService<OnFrameworkService>(), s.GetRequiredService<GlamourFastUpdate>(), cs, dm, gip))

        .AddSingleton((s) => new MovementMonitor(s.GetRequiredService<ILogger<MovementMonitor>>(),
            s.GetRequiredService<GagspeakMediator>(), s.GetRequiredService<HardcoreHandler>(),
            s.GetRequiredService<WardrobeHandler>(), s.GetRequiredService<ClientConfigurationManager>(),
            s.GetRequiredService<OptionPromptListeners>(), s.GetRequiredService<OnFrameworkService>(),
            s.GetRequiredService<MoveController>(), con, cs, ks, ot))
        .AddSingleton((s) => new MoveController(s.GetRequiredService<ILogger<MoveController>>(), gip, ot))

        .AddSingleton((s) => new OptionPromptListeners(s.GetRequiredService<ILogger<OptionPromptListeners>>(),
            s.GetRequiredService<HardcoreHandler>(), tm, gip, alc))


        .AddSingleton((s) => new ResourceLoader(s.GetRequiredService<ILogger<ResourceLoader>>(),
            s.GetRequiredService<GagspeakMediator>(), s.GetRequiredService<GagspeakConfigService>(),
            s.GetRequiredService<AvfxManager>(), s.GetRequiredService<ScdManager>(), dm, ss, gip))
        .AddSingleton((s) => new AvfxManager(s.GetRequiredService<ILogger<AvfxManager>>(), dm, pi.ConfigDirectory.FullName))
        .AddSingleton((s) => new ScdManager(s.GetRequiredService<ILogger<ScdManager>>(), dm, pi.ConfigDirectory.FullName))
        .AddSingleton((s) => new VfxSpawns(s.GetRequiredService<ILogger<VfxSpawns>>(),
            s.GetRequiredService<GagspeakMediator>(), s.GetRequiredService<ResourceLoader>(), cs, tm))

        .AddSingleton((s) => new ActionEffectMonitor(s.GetRequiredService<ILogger<ActionEffectMonitor>>(),
            s.GetRequiredService<GagspeakConfigService>(), ss, gip))
        .AddSingleton((s) => new TriggerController(s.GetRequiredService<ILogger<TriggerController>>(),
            s.GetRequiredService<GagspeakMediator>(), s.GetRequiredService<ClientConfigurationManager>(),
            s.GetRequiredService<PlayerCharacterManager>(), s.GetRequiredService<PairManager>(), s.GetRequiredService<ActionEffectMonitor>(),
            s.GetRequiredService<ToyboxFactory>(), s.GetRequiredService<OnFrameworkService>(), s.GetRequiredService<ToyboxVibeService>(),
            s.GetRequiredService<IpcCallerMoodles>(), cg, cs, dm))

        .AddSingleton((s) => new DtrBarService(s.GetRequiredService<ILogger<DtrBarService>>(),
            s.GetRequiredService<GagspeakMediator>(), s.GetRequiredService<ApiController>(),
            s.GetRequiredService<PairManager>(), s.GetRequiredService<OnFrameworkService>(), cs, dtr))

        // Utilities Services
        .AddSingleton<ILoggerProvider, Microsoft.Extensions.Logging.Console.ConsoleLoggerProvider>()
        .AddSingleton<GagSpeakHost>()
        .AddSingleton<ModAssociations>()
        .AddSingleton<MoodlesAssociations>()
        .AddSingleton<ProfileFactory>()
        // Register ObjectIdentification and ItemData
        .AddSingleton<ObjectIdentification>()
        .AddSingleton<ItemData>()
        .AddSingleton<ItemIdVars>()

        // UI Helpers
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
            s.GetRequiredService<UiSharedService>(), s.GetRequiredService<DictStain>(), s.GetRequiredService<ItemData>(),
            s.GetRequiredService<TextureService>(), s.GetRequiredService<MoodlesAssociations>(), dm))

        // Wardrobe UI
        .AddSingleton<ActiveRestraintSet>()
        .AddSingleton<RestraintSetManager>()
        .AddSingleton<RestraintStruggleSim>()
        .AddSingleton((s) => new MoodlesService(s.GetRequiredService<ILogger<MoodlesService>>(), s.GetRequiredService<UiSharedService>(), dm))
        .AddSingleton<MoodlesManager>()
        .AddSingleton((s) => new RestraintSetEditor(s.GetRequiredService<ILogger<RestraintSetEditor>>(),
            s.GetRequiredService<GagspeakMediator>(), s.GetRequiredService<UiSharedService>(), s.GetRequiredService<WardrobeHandler>(),
            s.GetRequiredService<DictStain>(), s.GetRequiredService<ItemData>(), s.GetRequiredService<DictBonusItems>(),
            s.GetRequiredService<TextureService>(), s.GetRequiredService<ModAssociations>(), s.GetRequiredService<MoodlesAssociations>(),
            s.GetRequiredService<PairManager>(), dm))
        .AddSingleton<RestraintCosmetics>()
        .AddSingleton<WardrobeHandler>()

        // Puppeteer UI
        .AddSingleton((s) => new PuppeteerHandler(s.GetRequiredService<ILogger<PuppeteerHandler>>(),
            s.GetRequiredService<GagspeakMediator>(), s.GetRequiredService<ClientConfigurationManager>(),
            s.GetRequiredService<PlayerCharacterManager>(), s.GetRequiredService<PairManager>(), dm))
        .AddSingleton<AliasTable>()

        // Toybox UI
        .AddSingleton<ToyboxOverview>()
        .AddSingleton<ToyboxPatterns>()
        .AddSingleton<ToyboxPrivateRooms>()
        .AddSingleton<ToyboxTriggerManager>()
        .AddSingleton<ToyboxAlarmManager>()
        .AddSingleton<ToyboxCosmetics>()
        .AddSingleton((s) => new VibeSimAudio(s.GetRequiredService<ILogger<VibeSimAudio>>(), pi))

        // Orders UI
        .AddSingleton<OrdersViewActive>()
        .AddSingleton<OrdersCreator>()
        .AddSingleton<OrdersAssigner>()

        // UI Components
        .AddSingleton<PermActionsComponents>()
        .AddSingleton<IdDisplayHandler>()
        .AddSingleton<SelectPairForTagUi>()
        .AddSingleton<TagHandler>()
        .AddSingleton<UserPairListHandler>()
        .AddSingleton<MainUiHomepage>()
        .AddSingleton<MainUiWhitelist>()
        .AddSingleton<MainUiPatternHub>()
        .AddSingleton<MainUiChat>()
        .AddSingleton((s) => new MainUiAccount(s.GetRequiredService<ILogger<MainUiAccount>>(),
            s.GetRequiredService<GagspeakMediator>(), s.GetRequiredService<ApiController>(),
            s.GetRequiredService<UiSharedService>(), s.GetRequiredService<OnFrameworkService>(),
            s.GetRequiredService<GagspeakConfigService>(), s.GetRequiredService<ProfileService>(), pi))

        // WebAPI Services
        .AddSingleton<ApiController>()
        .AddSingleton<HubFactory>()
        .AddSingleton<TokenProvider>()
        .AddSingleton<PiShockProvider>()

        // Service Services
        .AddSingleton<SafewordService>()
        .AddSingleton<ToyboxVibeService>()
        .AddSingleton<ToyboxRemoteService>()
        .AddSingleton<PatternPlaybackService>()
        .AddSingleton((s) => new TriggerService(s.GetRequiredService<ILogger<TriggerService>>(), s.GetRequiredService<GagspeakMediator>(),
            s.GetRequiredService<ClientConfigurationManager>(), s.GetRequiredService<ToyboxVibeService>(), cs, dm))
        .AddSingleton<AppearanceChangeService>()
        .AddSingleton<ClientConfigurationManager>()
        .AddSingleton<ServerConfigurationManager>()
        .AddSingleton<GagspeakMediator>()
        .AddSingleton<DiscoverService>()
        .AddSingleton<PatternHubService>()
        .AddSingleton<PermissionPresetService>()
        .AddSingleton((s) => new ProfileService(s.GetRequiredService<ILogger<ProfileService>>(),
            s.GetRequiredService<GagspeakMediator>(), s.GetRequiredService<ApiController>(),
            s.GetRequiredService<ProfileFactory>()))
        .AddSingleton((s) => new OnFrameworkService(s.GetRequiredService<ILogger<OnFrameworkService>>(),
            cs, con, dm, fw, gg, tm, ot, s.GetRequiredService<GagspeakMediator>()));
    #endregion GenericServices

    #region IpcServices
    public static IServiceCollection AddGagSpeakIPC(this IServiceCollection services, IDalamudPluginInterface pi, IClientState cs)
    => services
        .AddSingleton((s) => new IpcCallerMare(s.GetRequiredService<ILogger<IpcCallerMare>>(), pi,
            s.GetRequiredService<OnFrameworkService>(), s.GetRequiredService<GagspeakMediator>()))
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
            s.GetRequiredService<IpcCallerMoodles>(), s.GetRequiredService<IpcCallerMare>()))

        .AddSingleton((s) => new IpcProvider(s.GetRequiredService<ILogger<IpcProvider>>(),
            s.GetRequiredService<GagspeakMediator>(), s.GetRequiredService<PairManager>(),
            s.GetRequiredService<OnFrameworkService>(), pi))
        .AddSingleton((s) => new PenumbraChangedItemTooltip(s.GetRequiredService<ILogger<PenumbraChangedItemTooltip>>(),
            s.GetRequiredService<GagspeakMediator>(), s.GetRequiredService<IpcCallerPenumbra>(), cs,
            s.GetRequiredService<ItemData>()));

    #endregion IpcServices
    #region ConfigServices
    public static IServiceCollection AddGagSpeakConfigs(this IServiceCollection services, IDalamudPluginInterface pi)
    => services
        // client-end configs
        .AddSingleton((s) => new GagspeakConfigService(pi.ConfigDirectory.FullName))
        .AddSingleton((s) => new GagStorageConfigService(s.GetRequiredService<ItemIdVars>(), pi.ConfigDirectory.FullName))
        .AddSingleton((s) => new WardrobeConfigService(s.GetRequiredService<ItemIdVars>(), pi.ConfigDirectory.FullName))
        .AddSingleton((s) => new AliasConfigService(pi.ConfigDirectory.FullName))
        .AddSingleton((s) => new PatternConfigService(pi.ConfigDirectory.FullName))
        .AddSingleton((s) => new AlarmConfigService(pi.ConfigDirectory.FullName))
        .AddSingleton((s) => new TriggerConfigService(pi.ConfigDirectory.FullName))
        // server-end configs
        .AddSingleton((s) => new ServerConfigService(pi.ConfigDirectory.FullName))
        .AddSingleton((s) => new NicknamesConfigService(pi.ConfigDirectory.FullName))
        .AddSingleton((s) => new ServerTagConfigService(pi.ConfigDirectory.FullName))

        // Configuration Migrators
        .AddSingleton((s) => new MigrateGagStorage(s.GetRequiredService<ILogger<MigrateGagStorage>>(),
            s.GetRequiredService<ClientConfigurationManager>(), s.GetRequiredService<ItemIdVars>(), pi.ConfigDirectory.FullName))
        .AddSingleton((s) => new MigrateRestraintSets(s.GetRequiredService<ILogger<MigrateRestraintSets>>(),
            s.GetRequiredService<ClientConfigurationManager>(), s.GetRequiredService<ItemIdVars>(), pi.ConfigDirectory.FullName))
        .AddSingleton((s) => new MigratePatterns(s.GetRequiredService<ILogger<MigratePatterns>>(),
            s.GetRequiredService<ClientConfigurationManager>(), pi.ConfigDirectory.FullName));

    #endregion ConfigServices
    #region ScopedServices
    public static IServiceCollection AddGagSpeakScoped(this IServiceCollection services, IClientState cs,
        ICommandManager cm, IDalamudPluginInterface pi, ITextureProvider tp, INotificationManager nm,
        IChatGui cg, IDataManager dm)
    => services
        // Service Services
        .AddScoped<DrawEntityFactory>()
        .AddScoped<UiFactory>((s) => new UiFactory(s.GetRequiredService<ILoggerFactory>(), s.GetRequiredService<GagspeakMediator>(),
            s.GetRequiredService<ApiController>(), s.GetRequiredService<UiSharedService>(), s.GetRequiredService<ToyboxVibeService>(),
            s.GetRequiredService<IdDisplayHandler>(), s.GetRequiredService<PairManager>(), s.GetRequiredService<PlayerCharacterManager>(),
            s.GetRequiredService<ToyboxRemoteService>(), s.GetRequiredService<ServerConfigurationManager>(),
            s.GetRequiredService<ProfileService>(), s.GetRequiredService<OnFrameworkService>(), s.GetRequiredService<GagspeakConfigService>(),
            s.GetRequiredService<MoodlesService>(), s.GetRequiredService<PermissionPresetService>(), s.GetRequiredService<PermActionsComponents>(), cs))
        .AddScoped<SelectTagForPairUi>()
        .AddScoped<WindowMediatorSubscriberBase, SettingsUi>()
        .AddScoped<WindowMediatorSubscriberBase, IntroUi>()
        .AddScoped<WindowMediatorSubscriberBase, MainWindowUI>((s) => new MainWindowUI(s.GetRequiredService<ILogger<MainWindowUI>>(),
            s.GetRequiredService<GagspeakMediator>(), s.GetRequiredService<UiSharedService>(),
            s.GetRequiredService<ApiController>(), s.GetRequiredService<GagspeakConfigService>(),
            s.GetRequiredService<PairManager>(), s.GetRequiredService<ServerConfigurationManager>(),
            s.GetRequiredService<MainUiHomepage>(), s.GetRequiredService<MainUiWhitelist>(),
            s.GetRequiredService<MainUiPatternHub>(), s.GetRequiredService<MainUiChat>(), s.GetRequiredService<MainUiAccount>(),
            s.GetRequiredService<DrawEntityFactory>(), pi))
        .AddScoped<WindowMediatorSubscriberBase, PopoutProfileUi>()
        .AddScoped<WindowMediatorSubscriberBase, EventViewerUI>()
        .AddScoped<WindowMediatorSubscriberBase, ChangelogUI>()
        .AddScoped<WindowMediatorSubscriberBase, MigrationsUI>()
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
        .AddScoped<WindowMediatorSubscriberBase, EditProfileUi>()
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
            s.GetRequiredService<WindowSystem>(), s.GetServices<WindowMediatorSubscriberBase>(), s.GetRequiredService<UiFactory>(),
            s.GetRequiredService<GagspeakMediator>(), s.GetRequiredService<FileDialogManager>(), s.GetRequiredService<PenumbraChangedItemTooltip>()))
        .AddScoped((s) => new CommandManagerService(s.GetRequiredService<GagspeakMediator>(), s.GetRequiredService<GagspeakConfigService>(), 
            s.GetRequiredService<ServerConfigurationManager>(), s.GetRequiredService<ChatBoxMessage>(), s.GetRequiredService<TriggerController>(), cg, cm))
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
        .AddHostedService(p => p.GetRequiredService<IpcProvider>())
        .AddHostedService(p => p.GetRequiredService<SafewordService>())
        // add our main Plugin.cs file as a hosted ;
        .AddHostedService<GagSpeakHost>();
    #endregion HostedServices
}
