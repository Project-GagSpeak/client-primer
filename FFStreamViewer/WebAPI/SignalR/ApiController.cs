using Dalamud.Utility;
using Gagspeak.API.Data;
using Gagspeak.API.Dto;
using Gagspeak.API.Dto.User;
using Gagspeak.API.SignalR;
using FFStreamViewer.Services;
using FFStreamViewer.WebAPI.SignalR;
using FFStreamViewer.WebAPI.SignalR.Utils;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using System.Reflection;
using System.Threading;
using System;
using System.Threading.Tasks;
using System.Net.Http;

namespace FFStreamViewer.WebAPI;

#pragma warning disable MA0040
public sealed partial class ApiController : IGagspeakHubClient
{
    public const string MainServer = "Gagspeak Web Service Server";
    public const string MainServiceUri = "wss://gagspeak.com";

    private readonly HubFactory _hubFactory;                            // the hub factory
    private CancellationTokenSource _connectionCancellationTokenSource; // token for connection creation
    private ConnectionDto? _connectionDto;                              // the connection data transfer object for the current connection
    private bool _doNotNotifyOnNextInfo = false;                        // flag to not notify on next info
    private CancellationTokenSource? _healthCheckTokenSource = new();   // token for health check
    private bool _initialized;                                          // flag for if the hub is initialized
    private string? _lastUsedToken;                                     // the last used token
    private HubConnection? _mareHub;                                    // the current hub connection
    private ServerState _serverState;                                   // the current state of the server

    public ApiController(ILogger<ApiController> logger, HubFactory hubFactory)
    {
        _hubFactory = hubFactory; // set the hub factory to the instance of our plugin
        _connectionCancellationTokenSource = new CancellationTokenSource(); // create a new token for the connection

        // Mediator.Subscribe<DalamudLoginMessage>(this, (_) => DalamudUtilOnLogIn());
        // Mediator.Subscribe<DalamudLogoutMessage>(this, (_) => DalamudUtilOnLogOut());
        // Mediator.Subscribe<HubClosedMessage>(this, (msg) => MareHubOnClosed(msg.Exception));
        // Mediator.Subscribe<HubReconnectedMessage>(this, (msg) => _ = MareHubOnReconnected());
        // Mediator.Subscribe<HubReconnectingMessage>(this, (msg) => MareHubOnReconnecting(msg.Exception));
        // Mediator.Subscribe<CyclePauseMessage>(this, (msg) => _ = CyclePause(msg.UserData));
        // Mediator.Subscribe<CensusUpdateMessage>(this, (msg) => _lastCensus = msg);

        ServerState = ServerState.Offline; // the server state is offline by default

        // if (_dalamudUtil.IsLoggedIn)
        // {
        //     DalamudUtilOnLogIn();
        // }
    }

    public string AuthFailureMessage { get; private set; } = string.Empty; // message to display when authentication fails

    public Version CurrentClientVersion => _connectionDto?.CurrentClientVersion ?? new Version(0, 0, 0);

    public string DisplayName => _connectionDto?.User.AliasOrUID ?? string.Empty; // the display name of the user

    public bool IsConnected => ServerState == ServerState.Connected; // displays if the current server state is connected or not.

    public bool IsCurrentVersion => (Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0, 0)) >= (_connectionDto?.CurrentClientVersion ?? new Version(0, 0, 0, 0));

    public int OnlineUsers => SystemInfoDto.OnlineUsers; // how many online users there are. Pulled from the systeminfoDto

    public bool ServerAlive => ServerState is ServerState.Connected or ServerState.RateLimited or ServerState.Unauthorized or ServerState.Disconnected; // if the server is alive

    public ServerState ServerState
    {
        get => _serverState;
        private set
        {
            FFStreamViewer.Log.Debug($"New ServerState: {value}, prev ServerState: {_serverState}");
            _serverState = value;
        }
    }

    public SystemInfoDto SystemInfoDto { get; private set; } = new(); // the system info data transfer object

    public string UID => _connectionDto?.User.UID ?? string.Empty; // get the UID of the user

    public async Task<bool> CheckClientHealth() // checks the health of the client, by invoking the call from the API hub
    {
        return await _mareHub!.InvokeAsync<bool>(nameof(CheckClientHealth)).ConfigureAwait(false);
    }

    // create a connection
    public async Task CreateConnections()
    {
        // if the server manager is not showing the census popup, then publish the open census popup message
        // if (!_serverManager.ShownCensusPopup)
        // {
        //     Mediator.Publish(new OpenCensusPopupMessage());
        //     while (!_serverManager.ShownCensusPopup)
        //     {
        //         await Task.Delay(500).ConfigureAwait(false);
        //     }
        // }

        FFStreamViewer.Log.Debug("CreateConnections called");

        // if the current server is 
        // if (_serverManager.CurrentServer?.FullPause ?? true)
        // {
        //     FFStreamViewer.Log.Information("Not recreating Connection, paused");
        //     _connectionDto = null;
        //     await StopConnection(ServerState.Disconnected).ConfigureAwait(false);
        //     _connectionCancellationTokenSource?.Cancel();
        //     return;
        // }

        // var secretKey = _serverManager.GetSecretKey();
        // if (secretKey.IsNullOrEmpty())
        // {
        //     FFStreamViewer.Log.Warning("No secret key set for current character");
        //     _connectionDto = null;
        //     await StopConnection(ServerState.NoSecretKey).ConfigureAwait(false);
        //     _connectionCancellationTokenSource?.Cancel();
        //     return;
        // }

        await StopConnection(ServerState.Disconnected).ConfigureAwait(false);

        FFStreamViewer.Log.Information("Recreating Connection");
        //Mediator.Publish(new EventMessage(new Services.Events.Event(nameof(ApiController), Services.Events.EventSeverity.Informational,
        //    $"Starting Connection to {_serverManager.CurrentServer.ServerName}")));

        // we need to create a new connection
        _connectionCancellationTokenSource?.Cancel();
        _connectionCancellationTokenSource?.Dispose();
        _connectionCancellationTokenSource = new CancellationTokenSource();
        var token = _connectionCancellationTokenSource.Token;
        // while the server state is still not yet connected, and our token has not yet been cancelled,
        while (ServerState is not ServerState.Connected && !token.IsCancellationRequested)
        {
            // create a empty authentication failure message
            AuthFailureMessage = string.Empty;

            // await to stop connection to the server
            await StopConnection(ServerState.Disconnected).ConfigureAwait(false);
            // then try and connect to the server
            ServerState = ServerState.Connecting;

            // when we try
            try
            {
                // begin by building a connection
                FFStreamViewer.Log.Debug("Building connection");

                // we will do this with our last used token for validation
                // try
                // {
                //     _lastUsedToken = await _tokenProvider.GetOrUpdateToken(token).ConfigureAwait(false);
                // }
                // catch (MareAuthFailureException ex)
                // {
                //     AuthFailureMessage = ex.Reason;
                //     throw new HttpRequestException("Error during authentication", ex, System.Net.HttpStatusCode.Unauthorized);
                // }

                // This is just used to wait until the player is loaded in but that honestly shouldnt madder too much for us.
                // while (!await _dalamudUtil.GetIsPlayerPresentAsync().ConfigureAwait(false) && !token.IsCancellationRequested)
                // {
                //     FFStreamViewer.Log.Debug("Player not loaded in yet, waiting");
                //     await Task.Delay(TimeSpan.FromSeconds(1), token).ConfigureAwait(false);
                // }

                // if the token is cancelled, then break out of the loop
                if (token.IsCancellationRequested) break;

                // otherwise, create a new hub connection
                _mareHub = _hubFactory.GetOrCreate(token);
                // then initialize our API hooks that call the functions from the API
                InitializeApiHooks();

                // await for the hub to start asynchronously
                await _mareHub.StartAsync(token).ConfigureAwait(false);

                // then fetch the connection data transfer object from it
                _connectionDto = await GetConnectionDto().ConfigureAwait(false);

                // set the server state to connected
                ServerState = ServerState.Connected;

                // and declare the current client version
                var currentClientVer = Assembly.GetExecutingAssembly().GetName().Version!;

                // if the server version is not the same as the API version
                if (_connectionDto.ServerVersion != IGagspeakHub.ApiVersion)
                {
                    // if it is greater than the current client version
                    if (_connectionDto.CurrentClientVersion > currentClientVer)
                    {
                        FFStreamViewer.Log.Error("Client incompatible");
                        // Mediator.Publish(new NotificationMessage("Client incompatible",
                        //     $"Your client is outdated ({currentClientVer.Major}.{currentClientVer.Minor}.{currentClientVer.Build}), current is: " +
                        //     $"{_connectionDto.CurrentClientVersion.Major}.{_connectionDto.CurrentClientVersion.Minor}.{_connectionDto.CurrentClientVersion.Build}. " +
                        //     $"This client version is incompatible and will not be able to connect. Please update your Mare Synchronos client.",
                        //     Dalamud.Interface.Internal.Notifications.NotificationType.Error));
                    }
                    // and stop connection
                    await StopConnection(ServerState.VersionMisMatch).ConfigureAwait(false);
                    return;
                }

                // if it is greater than the current client version in general, let them know the client is oudated
                if (_connectionDto.CurrentClientVersion > currentClientVer)
                {
                    FFStreamViewer.Log.Warning("Client outdated");
                    // Mediator.Publish(new NotificationMessage("Client outdated",
                    //     $"Your client is outdated ({currentClientVer.Major}.{currentClientVer.Minor}.{currentClientVer.Build}), current is: " +
                    //     $"{_connectionDto.CurrentClientVersion.Major}.{_connectionDto.CurrentClientVersion.Minor}.{_connectionDto.CurrentClientVersion.Build}. " +
                    //     $"Please keep your Mare Synchronos client up-to-date.",
                    //     Dalamud.Interface.Internal.Notifications.NotificationType.Warning));
                }

                // await LoadIninitialPairs().ConfigureAwait(false);
                // await LoadOnlinePairs().ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                FFStreamViewer.Log.Warning("Connection attempt cancelled");
                return;
            }
            catch (HttpRequestException ex)
            {
                FFStreamViewer.Log.Warning($"{ex} HttpRequestException on Connection");

                if (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    await StopConnection(ServerState.Unauthorized).ConfigureAwait(false);
                    return;
                }

                ServerState = ServerState.Reconnecting;
                FFStreamViewer.Log.Information("Failed to establish connection, retrying");
                await Task.Delay(TimeSpan.FromSeconds(new Random().Next(5, 20)), token).ConfigureAwait(false);
            }
            catch (InvalidOperationException ex)
            {
                FFStreamViewer.Log.Warning($"{ex} InvalidOperationException on connection");
                await StopConnection(ServerState.Disconnected).ConfigureAwait(false);
                return;
            }
            catch (Exception ex)
            {
                FFStreamViewer.Log.Warning($"{ex} Exception on Connection");

                FFStreamViewer.Log.Information("Failed to establish connection, retrying");
                await Task.Delay(TimeSpan.FromSeconds(new Random().Next(5, 20)), token).ConfigureAwait(false);
            }
        }
    }

    public Task<ConnectionDto> GetConnectionDto() => GetConnectionDto(true);

    public async Task<ConnectionDto> GetConnectionDto(bool publishConnected = true)
    {
        var dto = await _mareHub!.InvokeAsync<ConnectionDto>(nameof(GetConnectionDto)).ConfigureAwait(false);
        if (publishConnected) 
        {
            FFStreamViewer.Log.Debug("Connected Sucessfully");
        } // Mediator.Publish(new ConnectedMessage(dto));
        return dto;
    }

    public void Dispose(bool disposing)
    {
        _healthCheckTokenSource?.Cancel();
        _ = Task.Run(async () => await StopConnection(ServerState.Disconnected).ConfigureAwait(false));
        _connectionCancellationTokenSource?.Cancel();
    }

    private async Task ClientHealthCheck(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _mareHub != null)
        {
            await Task.Delay(TimeSpan.FromSeconds(30), ct).ConfigureAwait(false);
            FFStreamViewer.Log.Debug("Checking Client Health State");

            // bool requireReconnect = await RefreshToken(ct).ConfigureAwait(false);

            // if (requireReconnect) break;

            _ = await CheckClientHealth().ConfigureAwait(false);
        }
    }

    private void DalamudUtilOnLogIn()
    {
        // would run a create connections upon login
        _ = Task.Run(() => CreateConnections());
    }

    private void DalamudUtilOnLogOut()
    {
        // would run to stop the connection on logout
        _ = Task.Run(async () => await StopConnection(ServerState.Disconnected).ConfigureAwait(false));
        ServerState = ServerState.Offline; // switch the state to offline.
    }

    // function to initialize the API hooks.
    private void InitializeApiHooks()
    {
        if (_mareHub == null) return;

        FFStreamViewer.Log.Debug("Initializing data");

        OnReceiveServerMessage((sev, msg) => _ = Client_RecieveServerMessage(sev, msg));
        OnUpdateSystemInfo((dto) => _ = Client_UpdateSystemInfo(dto));


        _healthCheckTokenSource?.Cancel();
        _healthCheckTokenSource?.Dispose();
        _healthCheckTokenSource = new CancellationTokenSource();
        _ = ClientHealthCheck(_healthCheckTokenSource.Token);

        _initialized = true;
    }


    private void MareHubOnClosed(Exception? arg)
    {
        _healthCheckTokenSource?.Cancel();
        // Mediator.Publish(new DisconnectedMessage());
        ServerState = ServerState.Offline;
        if (arg != null)
        {
            FFStreamViewer.Log.Warning($"{arg} Connection closed");
        }
        else
        {
            FFStreamViewer.Log.Information("Connection closed");
        }
    }

    private async Task MareHubOnReconnected()
    {
        ServerState = ServerState.Reconnecting;
        try
        {
            InitializeApiHooks();
            _connectionDto = await GetConnectionDto(publishConnected: false).ConfigureAwait(false);
            if (_connectionDto.ServerVersion != IGagspeakHub.ApiVersion)
            {
                await StopConnection(ServerState.VersionMisMatch).ConfigureAwait(false);
                return;
            }
            ServerState = ServerState.Connected;
            // await LoadIninitialPairs().ConfigureAwait(false);
            // await LoadOnlinePairs().ConfigureAwait(false);
            // Mediator.Publish(new ConnectedMessage(_connectionDto));
        }
        catch (Exception ex)
        {
            FFStreamViewer.Log.Error($"{ex} Failure to obtain data after reconnection");
            await StopConnection(ServerState.Disconnected).ConfigureAwait(false);
        }
    }

    private void MareHubOnReconnecting(Exception? arg)
    {
        _doNotNotifyOnNextInfo = true;
        _healthCheckTokenSource?.Cancel();
        ServerState = ServerState.Reconnecting;
        FFStreamViewer.Log.Warning($"{arg} Connection closed... Reconnecting");
        //Mediator.Publish(new EventMessage(new Services.Events.Event(nameof(ApiController), Services.Events.EventSeverity.Warning,
        //    $"Connection interrupted, reconnecting to {_serverManager.CurrentServer.ServerName}")));

    }

    private async Task StopConnection(ServerState state)
    {
        ServerState = ServerState.Disconnecting;

        FFStreamViewer.Log.Information("Stopping existing connection");
        await _hubFactory.DisposeHubAsync().ConfigureAwait(false);

        if (_mareHub is not null)
        {
            // Mediator.Publish(new EventMessage(new Services.Events.Event(nameof(ApiController), Services.Events.EventSeverity.Informational,
            //     $"Stopping existing connection to {_serverManager.CurrentServer.ServerName}")));

            _initialized = false;
            _healthCheckTokenSource?.Cancel();
            //Mediator.Publish(new DisconnectedMessage());
            _mareHub = null;
            _connectionDto = null;
        }

        ServerState = state;
    }
}
#pragma warning restore MA0040