using Gagspeak.API.SignalR;
using FFStreamViewer.WebAPI.SignalR.Utils;
using MessagePack;
using MessagePack.Resolvers;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System.Threading;
using System;

namespace FFStreamViewer.WebAPI.SignalR;

public class HubFactory // : MediatorSubscriberBase
{
    private readonly ILoggerProvider _loggingProvider;
    // private readonly ServerConfigurationManager _serverConfigurationManager;
    // private readonly TokenProvider _tokenProvider;
    private HubConnection? _instance;   // The instance of the hub connection we have with the server
    private bool _isDisposed = false;   // if the hub factory is disposed of or not.

    // the default constructor for the HubFactory
    public HubFactory(ILogger<HubFactory> logger, ILoggerProvider pluginLog)
    {
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
        FFStreamViewer.Log.Debug("Disposing current HubConnection");

        // Set the _isDisposed flag to true, as we are disposing of the current HubConnection
        _isDisposed = true;

        // unsubscribe from the Closed, Reconnecting, and Reconnected events
        _instance.Closed -= HubOnClosed;
        _instance.Reconnecting -= HubOnReconnecting;
        _instance.Reconnected -= HubOnReconnected;

        await _instance.StopAsync().ConfigureAwait(false);
        await _instance.DisposeAsync().ConfigureAwait(false);

        _instance = null;

        FFStreamViewer.Log.Debug("Current HubConnection disposed");
    }

    /// <summary>
    /// Gets or creates a new HubConnection.
    /// </summary>
    public HubConnection GetOrCreate(CancellationToken ct)
    {
        // if we are not disposed and the instance is not null, then return the instance.
        if (!_isDisposed && _instance != null) return _instance;
        
        // otherwise, build a new HubConnection and return it.
        return BuildHubConnection(ct);
    }

    // temp var placement
    public string CurrentApiUrl { get; set; } = "https://localhost:5001";

    /// <summary>
    /// Builds a new HubConnection.
    /// </summary>
    private HubConnection BuildHubConnection(CancellationToken ct)
    {
        // Log that we are building a new HubConnection
        FFStreamViewer.Log.Debug("Building new HubConnection");

        // Create a new HubConnectionBuilder
        _instance = new HubConnectionBuilder()
            // give it the appropriate URL for the connection.
            .WithUrl(CurrentApiUrl + IGagspeakHub.Path, options =>
            {
                //options.AccessTokenProvider = () => _tokenProvider.GetOrUpdateToken(ct);

                // set the transport options for the connection
                options.Transports = HttpTransportType.WebSockets | HttpTransportType.ServerSentEvents | HttpTransportType.LongPolling;
            })
            .AddMessagePackProtocol(opt => // add in a message pack protocol
            {
                // create a new resolver for the message pack protocol
                var resolver = CompositeResolver.Create(
                    StandardResolverAllowPrivate.Instance, // use the standard resolver
                    BuiltinResolver.Instance,              // use the builtin resolver
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
            //.WithAutomaticReconnect(new ForeverRetryPolicy(Mediator))    Not sure how this works yet so dont use it
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

    /// <summary>
    /// Task that is fired whenever the HubConnection is closed.
    /// </summary>
    private Task HubOnClosed(Exception? arg)
    {
        //Mediator.Publish(new HubClosedMessage(arg));
        FFStreamViewer.Log.Debug("Hub is not Closed");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Task that is fired whenever the HubConnection is reconnected.
    /// </summary>
    private Task HubOnReconnected(string? arg)
    {
        //Mediator.Publish(new HubReconnectedMessage(arg));
        FFStreamViewer.Log.Debug("Hub is not Reconnected");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Task that is fired whenever the HubConnection is reconnecting.
    /// </summary>
    private Task HubOnReconnecting(Exception? arg)
    {
        //Mediator.Publish(new HubReconnectingMessage(arg));
        FFStreamViewer.Log.Debug("Hub is not Reconnecting");
        return Task.CompletedTask;
    }
}