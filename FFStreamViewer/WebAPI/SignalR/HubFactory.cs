using Gagspeak.API.SignalR;
using FFStreamViewer.WebAPI.Services.ServerConfiguration;
using MessagePack;
using MessagePack.Resolvers;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using FFStreamViewer.WebAPI.Services.Mediator;
using FFStreamViewer.WebAPI.SignalR.Utils;

namespace FFStreamViewer.WebAPI.SignalR;

public class HubFactory : MediatorSubscriberBase
{
    private readonly ILoggerProvider _loggingProvider;
    private readonly ServerConfigurationManager _serverConfigManager;
    private readonly TokenProvider _tokenProvider;
    private HubConnection? _instance;   // The instance of the hub connection we have with the server
    private bool _isDisposed = false;   // if the hub factory is disposed of or not.

    // the default constructor for the HubFactory
    public HubFactory(ILogger<HubFactory> logger, GagspeakMediator gagspeakMediator,
        ServerConfigurationManager serverConfigManager,
        TokenProvider tokenProvider, ILoggerProvider pluginLog) : base(logger, gagspeakMediator)
    {
        _serverConfigManager = serverConfigManager;
        _tokenProvider = tokenProvider;
        _loggingProvider = pluginLog;
    }

    /// <summary>
    /// Disposes of the current HubConnection.
    /// <para> This method is async. </para>
    /// </summary>
    public async Task DisposeHubAsync()
    {
        // if our instance is null or we have already disposed, then just return, as we have no need.
        if (_instance == null || _isDisposed) return;

        // Otherwise, log that we are disposing the current HubConnection
        Logger.LogDebug("Disposing current HubConnection");

        // Set the _isDisposed flag to true, as we are disposing of the current HubConnection
        _isDisposed = true;

        // unsubscribe from the Closed, Reconnecting, and Reconnected events
        _instance.Closed -= HubOnClosed;
        _instance.Reconnecting -= HubOnReconnecting;
        _instance.Reconnected -= HubOnReconnected;

        await _instance.StopAsync().ConfigureAwait(false);
        await _instance.DisposeAsync().ConfigureAwait(false);

        _instance = null;

        Logger.LogDebug("Current HubConnection disposed");
    }

    /// <summary> Gets or creates a new HubConnection. </summary>
    public HubConnection GetOrCreate(CancellationToken ct)
    {
        // if we are not disposed and the instance is not null, then return the instance.
        if (!_isDisposed && _instance != null) return _instance;
        // otherwise, build a new HubConnection and return it.
        return BuildHubConnection(ct);
    }

    /// <summary> Builds a new HubConnection. </summary>
    private HubConnection BuildHubConnection(CancellationToken ct)
    {
        // Log that we are building a new HubConnection
        Logger.LogDebug("Building new HubConnection");
        var connectionURI = _serverConfigManager.CurrentApiUrl + IGagspeakHub.Path;
        Logger.LogDebug($"Attempting to connect to URI: {connectionURI}");

        _instance = new HubConnectionBuilder()
            // give it the appropriate URL for the connection.
            .WithUrl(connectionURI, options =>
            {
                // set the accessTokenProvider and transport options for the connection
                options.AccessTokenProvider = () => _tokenProvider.GetOrUpdateToken(ct);
                options.Transports = HttpTransportType.WebSockets | HttpTransportType.ServerSentEvents | HttpTransportType.LongPolling;
            })
            .AddMessagePackProtocol(opt => // add in a message pack protocol
            {
                // create a new resolver for the message pack protocol
                var resolver = CompositeResolver.Create(
                    StandardResolverAllowPrivate.Instance, // use the standard resolver
                    BuiltinResolver.Instance,              // use the built in resolver
                    AttributeFormatterResolver.Instance,   // use the attribute formatter resolver
                                                           // replace enum resolver
                    DynamicEnumAsStringResolver.Instance,  // use the dynamic enum as string resolver
                    DynamicGenericResolver.Instance,       // use the dynamic generic resolver
                    DynamicUnionResolver.Instance,         // use the dynamic union resolver
                    DynamicObjectResolver.Instance,        // use the dynamic object resolver
                    PrimitiveObjectResolver.Instance,      // use the primitive object resolver
                                                           // final fallback(last priority)
                    StandardResolver.Instance);            // use the standard resolver

                // set the serializer options for the message pack protocol
                opt.SerializerOptions =
                    MessagePackSerializerOptions.Standard
                        .WithCompression(MessagePackCompression.Lz4Block)
                        .WithResolver(resolver);
            })
            .WithAutomaticReconnect(new ForeverRetryPolicy(Mediator)) // automatic reconnecting
            .ConfigureLogging(a => // configure the logging for the hub connection
            {
                // clear the providers and add the logging provider
                a.ClearProviders().AddProvider(_loggingProvider);
                // set the minimum log level to information
                a.SetMinimumLevel(LogLevel.Information);
            })
            // finally, build the connection
            .Build();

        // and add our subscribers for the connection
        _instance.Closed += HubOnClosed;
        _instance.Reconnecting += HubOnReconnecting;
        _instance.Reconnected += HubOnReconnected;

        // and claim it to not be disposed since it is established.
        _isDisposed = false;
        // return the instance
        return _instance;
    }

    /// <summary> Task that is fired whenever the HubConnection is closed.</summary>
    private Task HubOnClosed(Exception? arg)
    {
        Mediator.Publish(new HubClosedMessage(arg));
        return Task.CompletedTask;
    }

    /// <summary>Task that is fired whenever the HubConnection is reconnected.</summary>
    private Task HubOnReconnected(string? arg)
    {
        Mediator.Publish(new HubReconnectedMessage(arg));
        return Task.CompletedTask;
    }

    /// <summary>Task that is fired whenever the HubConnection is reconnecting.</summary>
    private Task HubOnReconnecting(Exception? arg)
    {
        Mediator.Publish(new HubReconnectingMessage(arg));
        return Task.CompletedTask;
    }
}
