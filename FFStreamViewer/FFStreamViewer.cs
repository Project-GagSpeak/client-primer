using Dalamud.Game;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFStreamViewer.WebAPI.GagspeakConfiguration;
using FFStreamViewer.WebAPI.PlayerData.Pairs;
using FFStreamViewer.WebAPI.Services;
using FFStreamViewer.WebAPI.Services.Events;
using FFStreamViewer.WebAPI.Services.Mediator;
using FFStreamViewer.WebAPI.Services.ServerConfiguration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace FFStreamViewer;
/// <summary> The main class for the FFStreamViewer plugin.
/// <para>
/// I've been looking into better structures for a service collection than otters approach,
/// and ive taken a liking to this one as it gives me an easier understanding for including
/// interfaces and scoped services and hosted services.
/// </para>
/// </summary>

public class FFStreamViewerHost : MediatorSubscriberBase, IHostedService
{
    private readonly OnFrameworkService _frameworkUtil;                     // For running on the games framework thread
    private readonly GagspeakConfigService _gagspeakConfigService;          // the plugins config service
    private readonly ServerConfigurationManager _serverConfigurationManager;// the servers config manager
    private readonly IServiceScopeFactory _serviceScopeFactory;             // the service scope factory.
    private IServiceScope? _runtimeServiceScope;                            // the runtime service scope
    private Task? _launchTask;                                              // the task ran when plugin is launched.

    public FFStreamViewerHost(ILogger<FFStreamViewer> logger, GagspeakConfigService gagspeakConfigService,
        ServerConfigurationManager serverConfigurationManager, OnFrameworkService frameworkUtil,
        IServiceScopeFactory serviceScopeFactory, GagspeakMediator mediator) : base(logger, mediator)
    {
        // set the services
        _frameworkUtil = frameworkUtil;
        _gagspeakConfigService = gagspeakConfigService;
        _serverConfigurationManager = serverConfigurationManager;
        _serviceScopeFactory = serviceScopeFactory;
    }
    /// <summary> The task to run after all services have been properly constructed.
    /// <para> this will kickstart the server and begin all operations and verifications.</para>
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        // set the version to the current assembly version
        var version = Assembly.GetExecutingAssembly().GetName().Version!;
        // log our version
        Logger.LogInformation("Launching {name} {major}.{minor}.{build}", "GagSpeak", version.Major, version.Minor, version.Build);

        // publish an event message to the mediator that we have started the plugin
        Mediator.Publish(new EventMessage(new Event(nameof(FFStreamViewer), EventSeverity.Informational,
            $"Starting Gagspeak{version.Major}.{version.Minor}.{version.Build}")));

        // subscribe to the main UI message window for making the primary UI be the main UI interface.
        Mediator.Subscribe<SwitchToMainUiMessage>(this, (msg) =>
        {
            if (_launchTask == null || _launchTask.IsCompleted) _launchTask = Task.Run(WaitForPlayerAndLaunchCharacterManager);
        });

        // subscribe to the login and logout messages
        Mediator.Subscribe<DalamudLoginMessage>(this, (_) => DalamudUtilOnLogIn());
        Mediator.Subscribe<DalamudLogoutMessage>(this, (_) => DalamudUtilOnLogOut());

        // start processing the mediator queue.
        Mediator.StartQueueProcessing();

        // return that the startAsync has been completed.
        return Task.CompletedTask;
    }

    /// <summary> The task to run when the plugin is stopped (called from the disposal)
    public Task StopAsync(CancellationToken cancellationToken)
    {
        UnsubscribeAll();

        DalamudUtilOnLogOut();

        Logger.LogDebug("Halting GagspeakPlugin");

        return Task.CompletedTask;
    }

    /// <summary> What to execute whenever the user logs in.
    /// <para>
    /// For our plugin here, it will be to log that we logged in,
    /// And if the launch task is null or was completed, to launch the run task 
    /// </para>
    /// </summary>
    private void DalamudUtilOnLogIn()
    {
        Logger?.LogDebug("Client login");
        if (_launchTask == null || _launchTask.IsCompleted) _launchTask = Task.Run(WaitForPlayerAndLaunchCharacterManager);
    }

    /// <summary> What to execute whenever the user logs out.
    /// <para>
    /// For our plugin here, it will be to log that we logged out,
    /// And to dispose of the runtime service scope.
    /// </para>
    /// </summary>
    private void DalamudUtilOnLogOut()
    {
        Logger?.LogDebug("Client logout");
        _runtimeServiceScope?.Dispose();
    }

    /// <summary> The Task executed by the launchTask var from the main plugin.cs 
    /// <para>
    /// This task will await for the player to be present (they are logged in and visible),
    /// then will dispose of the runtime service scope and create a new one to fetch
    /// the required services for our plugin to function as a base level.
    /// </para>
    /// </summary>
    private async Task WaitForPlayerAndLaunchCharacterManager()
    {
        // wait for the player to be present
        while (!await _frameworkUtil.GetIsPlayerPresentAsync().ConfigureAwait(false))
        {
            await Task.Delay(100).ConfigureAwait(false);
        }

        // then launch the managers for the plugin to function at a base level
        try
        {
            Logger?.LogDebug("Launching Managers");
            // before we do lets recreate the runtime service scope
            _runtimeServiceScope?.Dispose();
            _runtimeServiceScope = _serviceScopeFactory.CreateScope();
            _runtimeServiceScope.ServiceProvider.GetRequiredService<UiService>();
            // _runtimeServiceScope.ServiceProvider.GetRequiredService<CommandManagerService>();
            _gagspeakConfigService.Current.ButtonUsed = false;

            // if the client does not have a valid setup or config, switch to the intro ui
            if (!_gagspeakConfigService.Current.HasValidSetup() || !_serverConfigurationManager.HasValidConfig())
            {
                Logger?.LogDebug("Has Valid Setup: {setup} Has Valid Config: {config}", _gagspeakConfigService.Current.HasValidSetup(), _serverConfigurationManager.HasValidConfig());
                // publish the switch to intro ui message to the mediator
                Mediator.Publish(new SwitchToIntroUiMessage());
                return;
            }

            // get the required service for the online player manager (and notification service if we add it)
            _runtimeServiceScope.ServiceProvider.GetRequiredService<OnlinePlayerManager>();

            // _runtimeServiceScope.ServiceProvider.GetRequiredService<NotificationService>();
        }
        catch (Exception ex)
        {
            Logger?.LogCritical(ex, "Error during launch of managers");
        }
    }
}
