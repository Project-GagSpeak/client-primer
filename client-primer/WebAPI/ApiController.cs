using Dalamud.Interface.ImGuiNotification;
using Dalamud.Utility;
using GagSpeak.Achievements;
using GagSpeak.GagspeakConfiguration;
using GagSpeak.PlayerData.Pairs;
using GagSpeak.PlayerData.PrivateRooms;
using GagSpeak.PlayerData.Services;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using GagSpeak.UpdateMonitoring;
using GagspeakAPI.Data;
using GagspeakAPI.Dto.Connection;
using GagspeakAPI.Dto.Permissions;
using GagspeakAPI.SignalR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using System.Reflection;

namespace GagSpeak.WebAPI;

#pragma warning disable MA0040
public sealed partial class ApiController : DisposableMediatorSubscriberBase, IGagspeakHubClient, IToyboxHubClient
{
    public const string MainServer = "GagSpeak Main";
    public const string MainServiceUri = "wss://gagspeak.kinkporium.studio";

    private readonly AchievementManager _achievementManager;        // the achievement manager
    private readonly OnFrameworkService _frameworkUtils;            // the on framework service
    private readonly HubFactory _hubFactory;                        // the hub factory
    private readonly ClientCallbackService _clientCallbacks;        // the player character manager
    private readonly PrivateRoomManager _privateRoomManager;        // the private room manager
    private readonly PairManager _pairManager;                      // for managing the clients paired users
    private readonly ServerConfigurationManager _serverConfigs;     // the server configuration manager
    private readonly PiShockProvider _piShockProvider;              // the PiShock provider
    private readonly TokenProvider _tokenProvider;                  // the token provider for authentications
    private readonly GagspeakConfigService _gagspeakConfigService;  // the Gagspeak configuration service

    private bool _doNotNotifyOnNextInfo = false;                    // flag to not notify on next info
    // gagspeak hub variables
    private static ConnectionDto? _connectionDto = null; // The static instance of our connectionDto.
    private CancellationTokenSource _connectionCTS;                 // token for connection creation
    private CancellationTokenSource? _healthCTS = new();            // token for health check
    private bool _initialized;                                      // flag for if the hub is initialized
    private string? _lastUsedToken;                                 // the last used token (will use this for toybox connection too)
    private HubConnection? _gagspeakHub;                            // the current hub connection
    private static ServerState _serverState = ServerState.Offline;  // Make the backing field static

    // toybox hub variables
    private ToyboxConnectionDto? _toyboxConnectionDto;              // dto of our connection to toybox server
    private CancellationTokenSource _connectionToyboxCTS;           // token for connection creation
    private CancellationTokenSource? _toyboxHealthCTS = new();             // token for health check
    private bool _toyboxInitialized;                                // flag for if the toybox hub is initialized
    private HubConnection? _toyboxHub;                              // the toybox hub connection
    private ServerState _toyboxServerState;                         // the current state of the toybox server

    public ApiController(ILogger<ApiController> logger, GagspeakMediator mediator,
        AchievementManager achievementManager, OnFrameworkService frameworkUtils, HubFactory hubFactory, 
        ClientCallbackService clientCallbacks, PrivateRoomManager roomManager, PairManager pairManager,
        ServerConfigurationManager serverManager, GagspeakMediator gagspeakMediator, 
        PiShockProvider piShockProvider, TokenProvider tokenProvider, 
        GagspeakConfigService gagspeakConfigService) : base(logger, gagspeakMediator)
    {
        _achievementManager = achievementManager;
        _frameworkUtils = frameworkUtils;
        _hubFactory = hubFactory;
        _clientCallbacks = clientCallbacks;
        _privateRoomManager = roomManager;
        _pairManager = pairManager;
        _serverConfigs = serverManager;
        _piShockProvider = piShockProvider;
        _tokenProvider = tokenProvider;
        _gagspeakConfigService = gagspeakConfigService;
        _connectionCTS = new CancellationTokenSource();

        // subscribe to our mediator publishers for login/logout, and connection statuses
        Mediator.Subscribe<DalamudLoginMessage>(this, (_) => FrameworkUtilOnLogIn());
        Mediator.Subscribe<DalamudLogoutMessage>(this, (_) => FrameworkUtilOnLogOut());
        Mediator.Subscribe<CyclePauseMessage>(this, (msg) => _ = CyclePause(msg.UserData));
        // main hub connection subscribers
        Mediator.Subscribe<HubClosedMessage>(this, (msg) => GagspeakHubOnClosed(msg.Exception));
        Mediator.Subscribe<HubReconnectedMessage>(this, (msg) => _ = GagspeakHubOnReconnected());
        Mediator.Subscribe<HubReconnectingMessage>(this, (msg) => GagspeakHubOnReconnecting(msg.Exception));
        // toybox hub connection subscribers
        Mediator.Subscribe<ToyboxHubClosedMessage>(this, (msg) => ToyboxHubOnClosed(msg.Exception));
        Mediator.Subscribe<ToyboxHubReconnectedMessage>(this, (msg) => _ = ToyboxHubOnReconnected());
        Mediator.Subscribe<ToyboxHubReconnectingMessage>(this, (msg) => ToyboxHubOnReconnecting(msg.Exception));

        Mediator.Subscribe<AchievementDataUpdateMessage>(this, (data) => _ = UserUpdateAchievementData(new(new(UID), data.base64Data)));

        // initially set the server state to offline.
        ServerState = ServerState.Offline;
        ToyboxServerState = ServerState.Offline;
        _serverConfigs.CurrentServer.ToyboxFullPause = true;

        // if we are already logged in, then run the login function
        if (_frameworkUtils.IsLoggedIn) { FrameworkUtilOnLogIn(); }
    }

    public string AuthFailureMessage { get; private set; } = string.Empty;                                  // the authentication failure msg
    public static Version CurrentClientVersion => _connectionDto?.CurrentClientVersion ?? new Version(0, 0, 0);    // current client version
    public string DisplayName => _connectionDto?.User.AliasOrUID ?? string.Empty;                           // display name of user (the UID you see in the UI for yourself)
    public bool IsConnected => ServerState == ServerState.Connected;                                        // if we are connected to the server
    public bool IsToyboxConnected => ToyboxServerState == ServerState.Connected;                            // if we are connected to the toybox server
    public bool IsCurrentVersion => (Assembly.GetExecutingAssembly().GetName()                              // if the current version is the same as the version from the executing assembly
                                        .Version ?? new Version(0, 0, 0, 0)) >= (_connectionDto?.CurrentClientVersion ?? new Version(0, 0, 0, 0));
    public int OnlineUsers => SystemInfoDto.OnlineUsers;                                                    // the number of online users logged into the server
    public int ToyboxOnlineUsers => SystemInfoDto.OnlineToyboxUsers;
    public SystemInfoDto SystemInfoDto { get; private set; } = new();                                       // the system info data transfer object
    public static string UID => _connectionDto?.User.UID ?? string.Empty;                                          // the UID of the connected client user
    public static UserData PlayerUserData => _connectionDto!.User;                                                        // the user data of the connected client user
    public static ServerState ServerState
    {
        get => _serverState;
        private set
        {
            StaticLogger.Logger.LogDebug($"New MainServerState: {value}, prev MainServerState: {_serverState}", LoggerType.ApiCore);
            _serverState = value;
        }
    }
    public bool ServerAlive => ServerState is ServerState.Connected or ServerState.Unauthorized or ServerState.Disconnected;

    public ServerState ToyboxServerState
    {
        get => _toyboxServerState;
        private set
        {
            Logger.LogDebug($"New ToyboxServerState: {value}, prev ToyboxServerState: {_toyboxServerState}", LoggerType.ApiCore);
            _toyboxServerState = value;
        }
    }
    public bool ToyboxServerAlive => ToyboxServerState is ServerState.Connected or ServerState.Unauthorized or ServerState.Disconnected;


    /* ---------------------------------------------- METHODS ----------------------------------------------------- */

    /// <summary>Invoke a call to the server's hub function CHECKCLIENTHEALTH function.</summary>
    /// <returns>a boolean value indicating if the client is healthy</returns>
    public async Task<bool> CheckMainClientHealth()
    {
        return await _gagspeakHub!.InvokeAsync<bool>(nameof(CheckMainClientHealth)).ConfigureAwait(false);
    }

    /// <summary>Invoke a call to the toybox hub CHECKCLIENTHEALTH function.</summary>
    /// <returns>a boolean value indicating if the client is healthy</returns>
    public async Task<bool> CheckToyboxClientHealth()
    {
        return await _toyboxHub!.InvokeAsync<bool>(nameof(CheckToyboxClientHealth)).ConfigureAwait(false);
    }

    public async Task<(string, string)> FetchNewAccountDetailsAndDisconnect()
    {
        var token = new CancellationTokenSource().Token;
        try
        {
            // stop the connection so we can try again
            // (I know we did it outside the while loop but this is a while loop so we do this to make sure it reconnects)
            await StopConnection(ServerState.Disconnected).ConfigureAwait(false);
            ServerState = ServerState.Connecting;

            Logger.LogDebug("Building connection", LoggerType.ApiCore);
            // try and fetch our last used token from the token provider
            try
            {
                _lastUsedToken = await _tokenProvider.GetOrUpdateToken(token).ConfigureAwait(false);
            }
            // if we failed to grab the last used token, then throw the auth failure exception
            catch (GagspeakAuthFailureException ex)
            {
                // set the authentication failure message to the reason of the exception
                AuthFailureMessage = ex.Reason;
                // throw the exception
                throw new HttpRequestException("Error during authentication", ex, System.Net.HttpStatusCode.Unauthorized);
            }

            // wait to connect to the server until they have logged in with their player character and make sure the cancelation token has not yet been called
            while (!await _frameworkUtils.GetIsPlayerPresentAsync().ConfigureAwait(false) && !token.IsCancellationRequested)
            {
                // log that the player has not yet loaded in and wait for 1 second
                Logger.LogDebug("Player not loaded in yet, waiting", LoggerType.ApiCore);
                await Task.Delay(TimeSpan.FromSeconds(1), token).ConfigureAwait(false);
            }

            // otherwise, create a new hub connection
            _gagspeakHub = _hubFactory.GetOrCreate(token);

            Logger.LogDebug("Starting created hub instance", LoggerType.ApiCore);

            // start the hub we just created in async
            await _gagspeakHub.StartAsync(token).ConfigureAwait(false);

            Logger.LogDebug("Calling OneTimeUseAccountGeneration.", LoggerType.ApiCore);

            // Invoke the method to fetch new account details
            // Replace "RequestNewAccountDetails" with the actual method name and adjust return type as necessary
            var accountDetails = await _gagspeakHub.InvokeAsync<(string, string)>("OneTimeUseAccountGeneration");
            Logger.LogInformation("New Account Details Fetched.", LoggerType.ApiCore);
            // Return the fetched account details
            return accountDetails;
        }
        catch (HubException ex) // Assuming MissingClaimException is a custom exception you've defined
        {
            Logger.LogError($"Error fetching new account details: Missing claim in token. {ex.Message}");
            throw;
        }
        catch (Exception ex)
        {
            // Handle exceptions (logging, rethrowing, etc.)
            Logger.LogInformation($"Error fetching new account details: {ex.StackTrace}", LoggerType.ApiCore);
            throw;
        }
        finally
        {
            Logger.LogInformation("Stopping connection", LoggerType.ApiCore);
            // Ensure the connection is properly closed and disposed of
            if (_gagspeakHub != null && _gagspeakHub.State == HubConnectionState.Connected)
            {
                await StopConnection(ServerState.Disconnected).ConfigureAwait(false);
            }
        }
    }

    // create toybox server connection
    public async Task CreateToyboxConnection()
    {
        // perform the same regular checks as the main server connection, minus the redundancy.
        Logger.LogInformation("CreateToyboxConnection called", LoggerType.ApiCore);

        // make sure we are connected to the main server. If not, shut it down.
        if (!ServerAlive)
        {
            Logger.LogInformation("Stopping connection because connection to main server was lost.", LoggerType.ApiCore);
            await StopConnection(ServerState.Disconnected, HubType.ToyboxHub).ConfigureAwait(false);
            _connectionToyboxCTS?.Cancel();
            return;
        }
        // if we have opted to disconnect from the main server (manual button toggle)
        if (_serverConfigs.CurrentServer?.ToyboxFullPause ?? true)
        {
            Logger.LogInformation("Stopping connection to toybox server, as user wished to disconnect", LoggerType.ApiCore);
            await StopConnection(ServerState.Disconnected, HubType.ToyboxHub).ConfigureAwait(false);
            _connectionToyboxCTS?.Cancel();
            return;
        }
        // reset connection to toybox server (resuming a fullpause fully reconnects)
        await StopConnection(ServerState.Disconnected, HubType.ToyboxHub).ConfigureAwait(false);

        // now we can recreate the connection
        Logger.LogInformation("Starting Connection to Toybox Server", LoggerType.ApiCore);

        // recreate CTS for the toybox connection
        _connectionToyboxCTS?.Cancel();
        _connectionToyboxCTS?.Dispose();
        _connectionToyboxCTS = new CancellationTokenSource();
        CancellationToken token = _connectionToyboxCTS.Token;

        /* -------- WHILE THE SERVER STATE IS STILL NOT YET CONNECTED, (And the cancelation token hasn't been requested) -------- */
        while (ToyboxServerState is not ServerState.Connected && !token.IsCancellationRequested)
        {
            // stop the connection so we can try again (since its in a while loop)
            await StopConnection(ServerState.Disconnected, HubType.ToyboxHub).ConfigureAwait(false);
            ToyboxServerState = ServerState.Connecting;

            try
            {
                Logger.LogDebug("Building connection to toybox WebSocket server", LoggerType.ApiCore);
                // wait to connect to the server until they have logged in with their player character and make sure the cancelation token has not yet been called
                while (!await _frameworkUtils.GetIsPlayerPresentAsync().ConfigureAwait(false) && !token.IsCancellationRequested)
                {
                    // log that the player has not yet loaded in and wait for 1 second
                    Logger.LogDebug("Player not loaded in yet, waiting", LoggerType.ApiCore);
                    await Task.Delay(TimeSpan.FromSeconds(1), token).ConfigureAwait(false);
                }

                // if the token is cancelled, then break out of the loop
                if (token.IsCancellationRequested || _lastUsedToken == null) break;

                // otherwise, create a new hub connection
                _toyboxHub = _hubFactory.GetOrCreate(token, HubType.ToyboxHub, _lastUsedToken);
                // initialize the API hooks that will listen to the signalR function calls from our server to the connected clients
                InitializeApiHooks(HubType.ToyboxHub);

                // start the hub we just created in async
                await _toyboxHub.StartAsync(token).ConfigureAwait(false);

                // then fetch the connection data transfer object from the server
                _toyboxConnectionDto = await GetToyboxConnectionDto().ConfigureAwait(false);
                // if the connectionDTO is null, then stop the connection and return
                if (_toyboxConnectionDto == null)
                {
                    Logger.LogError("Toybox Connection DTO is null, this means you failed to connect (duh).");
                    await StopConnection(ServerState.Disconnected, HubType.ToyboxHub).ConfigureAwait(false);
                    return;
                }

                // if we reach here it means we are officially connected to the server
                ToyboxServerState = ServerState.Connected;

                // declare the current client version from the executing assembly
                Logger.LogInformation("Client Version for server: " + IGagspeakHub.ApiVersion, LoggerType.HubFactory);
                Logger.LogInformation("Server Version: " + _toyboxConnectionDto.ServerVersion, LoggerType.HubFactory);

                // if the server version is not the same as the API version
                if (_toyboxConnectionDto.ServerVersion != IGagspeakHub.ApiVersion)
                {
                    // publish a notification message to the client that their client is incompatible
                    Mediator.Publish(new NotificationMessage("Toybox Server version incompatible with client.",
                        $"Your client is outdated and will not be able to connect. Please update Gagspeak to fix.",
                        NotificationType.Error));
                    // stop connection
                    Logger.LogInformation("_toyboxConnectionDto.ServerVersion != IGagspeakHub.ApiVersion", LoggerType.ApiCore);
                    await StopConnection(ServerState.VersionMisMatch, HubType.ToyboxHub).ConfigureAwait(false);
                    return;
                }

                // load in the online pairs for our client
                await LoadToyboxOnlinePairs().ConfigureAwait(false);

                // initialize the connectionDto information to the privateRoomManager.
                Logger.LogInformation("Toybox Connection DTO ServerVersion: " + _toyboxConnectionDto.ServerVersion, LoggerType.HubFactory);
                Logger.LogInformation("Toybox Connection DTO HostedRoom: " + _toyboxConnectionDto.HostedRoom.NewRoomName, LoggerType.HubFactory);
                Logger.LogInformation("Toybox Connection DTO ConnectedRooms: " + _toyboxConnectionDto.ConnectedRooms.Count, LoggerType.HubFactory);
            }
            catch (OperationCanceledException) { Logger.LogWarning("Toybox Connection attempt cancelled", LoggerType.ApiCore); return; }
            catch (HttpRequestException ex)
            {
                Logger.LogWarning($"{ex} HttpRequestException on Toybox Connection");

                if (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    // stop the connection and change the state to unauthorized.
                    await StopConnection(ServerState.Unauthorized, HubType.ToyboxHub).ConfigureAwait(false);
                    return;
                }

                // otherwise attempt to reconnect
                ToyboxServerState = ServerState.Reconnecting;
                Logger.LogInformation("Failed to establish connection to toybox server, retrying", LoggerType.ApiCore);
                await Task.Delay(TimeSpan.FromSeconds(new Random().Next(5, 20)), token).ConfigureAwait(false);
            }
            catch (InvalidOperationException ex)
            {
                // if we had an invalid operation exception then we should stop the connection (disconnect).
                Logger.LogWarning($"{ex} InvalidOperationException on connection");
                await StopConnection(ServerState.Disconnected, HubType.ToyboxHub).ConfigureAwait(false);
                return;
            }
            catch (Exception ex)
            {
                // if we had any other exception, log it and attempt to reconnect
                Logger.LogWarning($"{ex} Exception on Toybox Connection, retrying");
                await Task.Delay(TimeSpan.FromSeconds(new Random().Next(5, 20)), token).ConfigureAwait(false);
            }
        }

    }

    /// <summary>
    /// Creates a connection
    /// <para>
    /// NOTICE: THIS FUNCTION NEEDS A MASSIVE REWORK.
    /// 
    /// It currently causes a connect and disconnect several times throughout the process 
    /// which is not healthy for proper throttling.
    /// 
    /// Additionally it falsely invoked on connected methods causing things to trigger when they shouldnt be.
    /// </para>
    /// </summary>
    /// <returns></returns>
    public async Task CreateConnections()
    {
        Logger.LogInformation("CreateConnections called", LoggerType.ApiCore);

        // if we have opted to disconnect from the server for now, then stop the connection and return.
        if (_serverConfigs.CurrentServer?.FullPause ?? true)
        {
            Logger.LogInformation("Stopping connection because user has wished to disconnect", LoggerType.ApiCore);
            if (_serverConfigs.CurrentServer is not null && !_serverConfigs.CurrentServer.FullPause && !_serverConfigs.CurrentServer.ToyboxFullPause)
            {
                Logger.LogTrace("Disconnecting from Toybox Server because both connections were active.", LoggerType.ApiCore);
                _serverConfigs.CurrentServer.ToyboxFullPause = !_serverConfigs.CurrentServer.ToyboxFullPause;
                _serverConfigs.Save();
                _ = CreateToyboxConnection();
            }

            if (ServerState is ServerState.Connected)
            {
                Logger.LogInformation("Sending off a final update before closing the hub connections on logout.", LoggerType.Achievements);
                await UserUpdateAchievementData(new(new(UID), _achievementManager.GetSaveDataDtoString()));
            }

            Logger.LogInformation("Disconnecting from GagSpeak Hub", LoggerType.ApiCore);
            _connectionDto = null;
            await StopConnection(ServerState.Disconnected).ConfigureAwait(false);
            _connectionCTS?.Cancel();
            return;
        }

        // get the currently stored secretkey from the server manager
        var secretKey = _serverConfigs.GetSecretKeyForCharacter();
        // log the secret key
        Logger.LogDebug("Secret Key fetched: " + secretKey, LoggerType.ApiCore);
        // if the secret key is null or empty
        if (secretKey.IsNullOrEmpty())
        {
            // log a warning that no secret key is set for the current character
            Logger.LogWarning("No secret key set for current character");
            // set the connection data transfer object to null
            _connectionDto = null;
            // stop the connection with the server state set to no secret key
            await StopConnection(ServerState.NoSecretKey).ConfigureAwait(false);
            // cancel the connection token source
            _connectionCTS?.Cancel();
            // early return
            return;
        }

        // if we reach here, we have a valid secret key, so lets stop the current connection (incase one was running)
        await StopConnection(ServerState.Disconnected).ConfigureAwait(false);

        // now we can recreate the connection
        Logger.LogInformation("Starting Connection to "+_serverConfigs.CurrentServer.ServerName, LoggerType.ApiCore);

        // dispose of the old connection CTS and create a new one
        _connectionCTS?.Cancel();
        _connectionCTS?.Dispose();
        _connectionCTS = new CancellationTokenSource();
        // make a variable token and set it to the token of the connection CTS
        CancellationToken token = _connectionCTS.Token;

        /* -------- WHILE THE SERVER STATE IS STILL NOT YET CONNECTED, (And the cancelation token hasn't been requested) -------- */
        while (ServerState is not ServerState.Connected && !token.IsCancellationRequested)
        {
            // clear the authentication failure message
            AuthFailureMessage = string.Empty;

            // stop the connection so we can try again
            // (I know we did it outside the while loop but this is a while loop so we do this to make sure it reconnects)
            await StopConnection(ServerState.Disconnected).ConfigureAwait(false);
            ServerState = ServerState.Connecting;

            // try to connect to the server....
            try
            {
                Logger.LogDebug("Building connection", LoggerType.ApiCore);
                // try and fetch our last used token from the token provider
                try
                {
                    _lastUsedToken = await _tokenProvider.GetOrUpdateToken(token).ConfigureAwait(false);
                }
                // if we failed to grab the last used token, then throw the auth failure exception
                catch (GagspeakAuthFailureException ex)
                {
                    // set the authentication failure message to the reason of the exception
                    AuthFailureMessage = ex.Reason;
                    // throw the exception
                    throw new HttpRequestException("Error during authentication", ex, System.Net.HttpStatusCode.Unauthorized);
                }

                // wait to connect to the server until they have logged in with their player character and make sure the cancelation token has not yet been called
                while (!await _frameworkUtils.GetIsPlayerPresentAsync().ConfigureAwait(false) && !token.IsCancellationRequested)
                {
                    // log that the player has not yet loaded in and wait for 1 second
                    Logger.LogDebug("Player not loaded in yet, waiting", LoggerType.ApiCore);
                    await Task.Delay(TimeSpan.FromSeconds(1), token).ConfigureAwait(false);
                }

                // if the token is cancelled, then break out of the loop
                if (token.IsCancellationRequested) break;

                // otherwise, create a new hub connection
                _gagspeakHub = _hubFactory.GetOrCreate(token);
                // initialize the API hooks that will listen to the signalR function calls from our server to the connected clients
                InitializeApiHooks();

                // start the hub we just created in async
                await _gagspeakHub.StartAsync(token).ConfigureAwait(false);

                // then fetch the connection data transfer object from the server
                _connectionDto = await GetConnectionDto().ConfigureAwait(false);
                // if the connectionDTO is null, then stop the connection and return
                if (_connectionDto == null)
                {
                    Logger.LogError("Connection DTO is null, this indicates that the secretkey for your character no longer exists in the database.\n" +
                        "You will need to generate a new one. If the key was for your primary user, contact cordy.");
                    await StopConnection(ServerState.Disconnected).ConfigureAwait(false);
                    return;
                }

                // if we reach here it means we are officially connected to the server
                ServerState = ServerState.Connected;

                // declare the current client version from the executing assembly
                var currentClientVer = Assembly.GetExecutingAssembly().GetName().Version!;
                Logger.LogInformation("Current Client Version: " + currentClientVer, LoggerType.ApiCore);
                Logger.LogInformation("Server Version: " + _connectionDto.CurrentClientVersion, LoggerType.ApiCore);

                // if the server version is not the same as the API version
                if (_connectionDto.ServerVersion != IGagspeakHub.ApiVersion)
                {
                    // if it is greater than the current client version
                    if (_connectionDto.CurrentClientVersion > currentClientVer)
                    {
                        // publish a notification message to the client that their client is incompatible
                        Mediator.Publish(new NotificationMessage("Client incompatible",
                            $"Your client is outdated ({currentClientVer.Major}.{currentClientVer.Minor}.{currentClientVer.Build}.{currentClientVer.Revision}), current is: " +
                            $"{_connectionDto.CurrentClientVersion.Major}.{_connectionDto.CurrentClientVersion.Minor}.{_connectionDto.CurrentClientVersion.Build}." +
                            $"{_connectionDto.CurrentClientVersion.Revision}" +
                            $"This client version is incompatible and will not be able to connect. Please update your Gagspeak client.",
                            NotificationType.Error));
                    }
                    // stop connection
                    Logger.LogInformation("_connectionDto.ServerVersion != IGagspeakHub.ApiVersion", LoggerType.ApiCore);
                    await StopConnection(ServerState.VersionMisMatch).ConfigureAwait(false);
                    return;
                }

                // if it is greater than the current client version in general, let them know the client is outdated
                if (_connectionDto.CurrentClientVersion > currentClientVer)
                {
                    // publish a notification message that the client is outdated
                    Mediator.Publish(new NotificationMessage("Client outdated",
                            $"Your client is outdated ({currentClientVer.Major}.{currentClientVer.Minor}.{currentClientVer.Build}.{currentClientVer.Revision}), current is: " +
                            $"{_connectionDto.CurrentClientVersion.Major}.{_connectionDto.CurrentClientVersion.Minor}.{_connectionDto.CurrentClientVersion.Build}." +
                            $"{_connectionDto.CurrentClientVersion.Revision} Please keep your Gagspeak client up-to-date.", NotificationType.Warning));
                    // stop connection
                    Logger.LogInformation("_connectionDto.CurrentClientVersion > currentClientVer", LoggerType.ApiCore);
                    await StopConnection(ServerState.VersionMisMatch).ConfigureAwait(false);
                    return;

                }
                // load the initial pairs for our client
                await LoadIninitialPairs().ConfigureAwait(false);
                // load in the online pairs for our client
                await LoadOnlinePairs().ConfigureAwait(false);
                // set the secret keys successful connection
                _serverConfigs.SetSecretKeyAsValid(secretKey);
                // auto connect to toybox vibe servers if enabled.

            }
            catch (OperationCanceledException)
            {
                // if at any point the connection was cancelled, this will be called.
                Logger.LogWarning("Connection attempt cancelled");
                return;
            }
            catch (HttpRequestException ex)
            {
                // if we get a http request exception, log it and stop the connection
                Logger.LogWarning($"{ex} HttpRequestException on Connection");

                if (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    // stop the connection and change the state to unauthorized.
                    await StopConnection(ServerState.Unauthorized).ConfigureAwait(false);
                    return;
                }

                // otherwise attempt to reconnect
                ServerState = ServerState.Reconnecting;
                Logger.LogWarning("Failed to establish connection, retrying", LoggerType.ApiCore);
                await Task.Delay(TimeSpan.FromSeconds(new Random().Next(5, 20)), token).ConfigureAwait(false);
            }
            catch (InvalidOperationException ex)
            {
                // if we had an invalid operation exception then we should stop the connection (disconnect).
                Logger.LogWarning($"{ex} InvalidOperationException on connection", LoggerType.ApiCore);
                await StopConnection(ServerState.Disconnected).ConfigureAwait(false);
                return;
            }
            catch (Exception ex)
            {
                // if we had any other exception, log it and attempt to reconnect
                Logger.LogWarning($"{ex} Exception on Connection", LoggerType.ApiCore);
                Logger.LogInformation("Failed to establish connection, retrying", LoggerType.ApiCore);
                await Task.Delay(TimeSpan.FromSeconds(new Random().Next(5, 20)), token).ConfigureAwait(false);
            }
        }
    }

    public Task CyclePause(UserData userData)
    {
        CancellationTokenSource cts = new();
        cts.CancelAfter(TimeSpan.FromSeconds(5));
        // run this task in async, so that we can pause multiple people at once if we really want to.
        _ = Task.Run(async () =>
        {
            var pair = _pairManager.GetOnlineUserPairs().Single(p => p.UserPair != null && p.UserData == userData);
            var perm = pair.UserPair!.OwnPairPerms;
            // set the permission to true
            perm.IsPaused = true;
            // update the pair permissions
            await UserUpdateOwnPairPerm(new UserPairPermChangeDto(userData, new KeyValuePair<string, object>(nameof(perm.IsPaused), true))).ConfigureAwait(false);
            // wait until it's changed
            while (pair.UserPair!.OwnPairPerms != perm)
            {
                await Task.Delay(250, cts.Token).ConfigureAwait(false);
                Logger.LogTrace("Waiting for permissions change for " + userData.UID, LoggerType.ApiCore);
            }
            // set it back to false;
            perm.IsPaused = false;
            await UserUpdateOwnPairPerm(new UserPairPermChangeDto(userData, new KeyValuePair<string, object>(nameof(perm.IsPaused), false))).ConfigureAwait(false);
        }, cts.Token).ContinueWith((t) => cts.Dispose());

        return Task.CompletedTask;
    }

    /// <summary> Call made to server when we request to get confirmation that we are connected </summary>
    public Task<ConnectionDto> GetConnectionDto() => GetConnectionDto(true);

    /// <summary>Invoke a call to the server's hub function GETCONNECTIONDTO function.</summary>
    /// <returns> The server will return back a connectionDto </returns>
    public async Task<ConnectionDto> GetConnectionDto(bool publishConnected = true)
    {
        // invoke the call to the server's hub function GETCONNECTIONDTO, and await for the Dto in the responce.
        var dto = await _gagspeakHub!.InvokeAsync<ConnectionDto>(nameof(GetConnectionDto)).ConfigureAwait(false);
        // if we are publishing the connected message, publish it
        if (publishConnected) Mediator.Publish(new ConnectedMessage(dto));
        // return the ConnectionDto we got from the server
        return dto;
    }

    /// <summary> Call made to toybox server when we request to get confirmation that we are connected </summary>
    public Task<ToyboxConnectionDto> GetToyboxConnectionDto() => GetToyboxConnectionDto(true);

    /// <summary>Invoke a call to the toybox server's to validate our connection.</summary>
    /// <returns> The server will return back a connectionDto </returns>
    public async Task<ToyboxConnectionDto> GetToyboxConnectionDto(bool publishConnected = true)
    {
        // invoke the call to the server's hub function GETCONNECTIONDTO, and await for the Dto in the responce.
        var dto = await _toyboxHub!.InvokeAsync<ToyboxConnectionDto>(nameof(GetToyboxConnectionDto)).ConfigureAwait(false);
        // if we are publishing the connected message, publish it
        if (publishConnected) Mediator.Publish(new ToyboxConnectedMessage(dto));
        // return the ConnectionDto we got from the server
        return dto;
    }

    /// <summary> The Disposal method for the API Controller.
    /// <para> This will cancel the healthCTS, stop the connection to the server, and cancel the connectionCTS.</para>
    /// </summary>
    /// <param name="disposing"></param>
    protected override void Dispose(bool disposing)
    {
        // dispose of the base
        base.Dispose(disposing);

        // Continue the disposal in Asyncronous function.
        _ = DisposeAsync();
    }

    /// <summary>
    /// Make the Disposal Asyncronous so that we perform nessisary operations requiring awaiters to finish.
    /// </summary>
    public async Task DisposeAsync()
    {
        // Push the achievement data update before shutting down
        try
        {
            if(ServerState == ServerState.Connected)
            {
                Logger.LogInformation("Sending off a final update before closing the hub connections.", LoggerType.Achievements);
                await UserUpdateAchievementData(new(new(UID), _achievementManager.GetSaveDataDtoString()));
            }

            Logger.LogInformation("Closing Hub Connections", LoggerType.ApiCore);
            _healthCTS?.Cancel();
            _toyboxHealthCTS?.Cancel();

            await StopConnection(ServerState.Disconnected, HubType.ToyboxHub).ConfigureAwait(false);
            await StopConnection(ServerState.Disconnected, HubType.MainHub).ConfigureAwait(false);
            
            _connectionCTS?.Cancel();
            _connectionToyboxCTS?.Cancel();

        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to send updated achievement data before shutdown.");
        }
    }

    /// <summary>
    /// Performs a health check on the client by periodically checking the client's health state.
    /// <para> We must check this periodically because if the client changes their key we will need to reconnect.</para>
    /// </summary>
    /// <param name="ct">The cancellation token to stop the health check.</param>
    /// <returns>A task representing the asynchronous health check operation.</returns>
    private async Task ClientMainHealthCheck(CancellationToken ct)
    {
        // Ensure the hub connection is initialized before starting the loop
        if (_gagspeakHub is null)
        {
            Logger.LogError("HubConnection is null. Cannot perform main client health check.", LoggerType.Health);
            return;
        }

        // while the cancellation token is not requested and the hub is not null
        while (!ct.IsCancellationRequested && _gagspeakHub != null)
        {
            // wait for 30 seconds
            await Task.Delay(TimeSpan.FromSeconds(30), ct).ConfigureAwait(false);
            // log that we are checking the client health state
            Logger.LogTrace("Checking Main Server Client Health State", LoggerType.Health);

            // refresh the token and check if we need to reconnect
            bool requireReconnect = await RefreshToken(ct).ConfigureAwait(false);
            // if we need to reconnect, break out of the loop
            if (requireReconnect) break;

            // Check if the hub connection is still valid before invoking the health check
            if (_gagspeakHub is not null)
            {
                // otherwise, invoke the check client health function on the server to get an update on its state.
                _ = await CheckMainClientHealth().ConfigureAwait(false);
            }
            else
            {
                Logger.LogError("HubConnection became null during health check loop.", LoggerType.Health);
                break;
            }
        }
    }

    private async Task ClientToyboxHealthCheck(CancellationToken ct)
    {
        // while the cancellation token is not requested and the hub is not null
        while (!ct.IsCancellationRequested && _toyboxHealthCTS != null)
        {
            // wait for 30 seconds
            await Task.Delay(TimeSpan.FromSeconds(30), ct).ConfigureAwait(false);
            // log that we are checking the client health state
            Logger.LogTrace("Checking Toybox Server Client Health State", LoggerType.Health);

            // refresh the token and check if we need to reconnect
            bool requireReconnect = await RefreshToken(ct).ConfigureAwait(false);
            // if we need to reconnect, break out of the loop
            if (requireReconnect) break;

            // otherwise, invoke the check client health function on the server to get an update on its state.
            _ = await CheckToyboxClientHealth().ConfigureAwait(false);
        }
    }



    /// <summary> GagSpeakMediator will call this function when the client logs into the game instance.
    /// <para> This will run the createConnections function, connecting us to the servers </para>
    /// </summary>
    private void FrameworkUtilOnLogIn()
    {
        // do an update check on the authentications to check and see if the current players local content ID has no current authentications.
        if (!_serverConfigs.CharacterHasSecretKey())
        {
            // check to see if we have an authentication for this local content ID. If we dont, create a new one.
            if (!_serverConfigs.AuthExistsForCurrentLocalContentId())
            {
                // then we can safely assume this is an alt account character of the Primary account.
                // so, we can create a empty authentication template by storing ContentID, name, world.
                Logger.LogDebug("Character has no secret key, generating new auth for current character", LoggerType.ApiCore);
                _serverConfigs.GenerateAuthForCurrentCharacter();
            }
            // otherwise, we can use the existing authentication
        }
        // would run a create connections upon login
        _ = Task.Run(() => CreateConnections());
    }

    /// <summary> GagSpeakMediator will call this function when the client logs out of the game instance.
    /// <para> This will stop our connection to the servers and set the state to offline upon logout </para>
    /// </summary>
    private async void FrameworkUtilOnLogOut()
    {
        if(ServerState == ServerState.Connected)
        {
            Logger.LogInformation("Sending off a final update before closing the hub connections on logout.", LoggerType.Achievements);
            await UserUpdateAchievementData(new(new(UID), _achievementManager.GetSaveDataDtoString()));
        }
        
        // would run to stop the connection on logout
        Logger.LogInformation("Stopping connection on logout", LoggerType.ApiCore);
        await StopConnection(ServerState.Disconnected).ConfigureAwait(false);
        await StopConnection(ServerState.Disconnected, HubType.ToyboxHub).ConfigureAwait(false);
        
        ServerState = ServerState.Offline; // switch the state to offline.
        ToyboxServerState = ServerState.Offline; // switch the toybox state to offline.
    }

    /// <summary>
    /// Method initializes the API Hooks, to establish listeners from SignalR
    /// <para> AKA the client starts recieving the function calls from the server to its connected clients. (because we declare ourselves are the client)</para>
    /// </summary>
    private void InitializeApiHooks(HubType hubType = HubType.MainHub)
    {
        if (hubType == HubType.MainHub)
        {
            // if the hub is null, return
            if (_gagspeakHub == null) return;
            // otherwise, log that we are initializing the data, and initialize it.
            Logger.LogDebug("Initializing data", LoggerType.ApiCore);

            // On the left is the function from the gagspeakhubclient.cs in the API, on the right is the function to be called in the API controller.
            OnReceiveServerMessage((sev, msg) => _ = Client_ReceiveServerMessage(sev, msg));
            OnReceiveHardReconnectMessage((sev, msg, state) => _ = Client_ReceiveHardReconnectMessage(sev, msg, state));
            OnUpdateSystemInfo(dto => _ = Client_UpdateSystemInfo(dto));

            OnUserAddClientPair(dto => _ = Client_UserAddClientPair(dto));
            OnUserRemoveClientPair(dto => _ = Client_UserRemoveClientPair(dto));
            OnUpdateUserIndividualPairStatusDto(dto => _ = Client_UpdateUserIndividualPairStatusDto(dto));

            OnUserApplyMoodlesByGuid(dto => _ = Client_UserApplyMoodlesByGuid(dto));
            OnUserApplyMoodlesByStatus(dto => _ = Client_UserApplyMoodlesByStatus(dto));
            OnUserRemoveMoodles(dto => _ = Client_UserRemoveMoodles(dto));
            OnUserClearMoodles(dto => _ = Client_UserClearMoodles(dto));

            OnUserUpdateSelfAllGlobalPerms(dto => _ = Client_UserUpdateSelfAllGlobalPerms(dto));
            OnUserUpdateSelfAllUniquePerms(dto => _ = Client_UserUpdateSelfAllUniquePerms(dto));
            OnUserUpdateSelfPairPermsGlobal(dto => _ = Client_UserUpdateSelfPairPermsGlobal(dto));
            OnUserUpdateSelfPairPerms(dto => _ = Client_UserUpdateSelfPairPerms(dto));
            OnUserUpdateSelfPairPermAccess(dto => _ = Client_UserUpdateSelfPairPermAccess(dto));
            OnUserUpdateOtherAllPairPerms(dto => _ = Client_UserUpdateOtherAllPairPerms(dto));
            OnUserUpdateOtherAllGlobalPerms(dto => _ = Client_UserUpdateOtherAllGlobalPerms(dto));
            OnUserUpdateOtherAllUniquePerms(dto => _ = Client_UserUpdateOtherAllUniquePerms(dto));
            OnUserUpdateOtherPairPermsGlobal(dto => _ = Client_UserUpdateOtherPairPermsGlobal(dto));
            OnUserUpdateOtherPairPerms(dto => _ = Client_UserUpdateOtherPairPerms(dto));
            OnUserUpdateOtherPairPermAccess(dto => _ = Client_UserUpdateOtherPairPermAccess(dto));

            OnUserReceiveCharacterDataComposite(dto => _ = Client_UserReceiveCharacterDataComposite(dto));
            OnUserReceiveOwnDataIpc(dto => _ = Client_UserReceiveOwnDataIpc(dto));
            OnUserReceiveOtherDataIpc(dto => _ = Client_UserReceiveOtherDataIpc(dto));
            OnUserReceiveOwnDataAppearance(dto => _ = Client_UserReceiveOwnDataAppearance(dto));
            OnUserReceiveOtherDataAppearance(dto => _ = Client_UserReceiveOtherDataAppearance(dto));
            OnUserReceiveOwnDataWardrobe(dto => _ = Client_UserReceiveOwnDataWardrobe(dto));
            OnUserReceiveOtherDataWardrobe(dto => _ = Client_UserReceiveOtherDataWardrobe(dto));
            OnUserReceiveOwnDataAlias(dto => _ = Client_UserReceiveOwnDataAlias(dto));
            OnUserReceiveOtherDataAlias(dto => _ = Client_UserReceiveOtherDataAlias(dto));
            OnUserReceiveOwnDataToybox(dto => _ = Client_UserReceiveOwnDataToybox(dto));
            OnUserReceiveOtherDataToybox(dto => _ = Client_UserReceiveOtherDataToybox(dto));
            OnUserReceiveDataPiShock(dto => _ = Client_UserReceiveDataPiShock(dto));

            OnUserReceiveShockInstruction(dto => _ = Client_UserReceiveShockInstruction(dto));
            OnGlobalChatMessage(dto => _ = Client_GlobalChatMessage(dto));
            OnUserSendOffline(dto => _ = Client_UserSendOffline(dto));
            OnUserSendOnline(dto => _ = Client_UserSendOnline(dto));
            OnUserUpdateProfile(dto => _ = Client_UserUpdateProfile(dto));
            OnDisplayVerificationPopup(dto => _ = Client_DisplayVerificationPopup(dto));

            // create a new health check token
            _healthCTS?.Cancel();
            _healthCTS?.Dispose();
            _healthCTS = new CancellationTokenSource();
            _ = ClientMainHealthCheck(_healthCTS.Token);
            // set us to initialized (yippee!!!)
            _initialized = true;
        }
        // initialize api hooks from the toybox hub.
        else
        {
            // if the hub is null, return
            if (_toyboxHub == null) return;
            // otherwise, log that we are initializing the data, and initialize it.
            Logger.LogDebug("Initializing ToyboxHub API Hooks", LoggerType.ApiCore);

            // On the left is the function from the GagspeakHubClient.cs in the API, on the right is the function to be called in the API controller.
            OnReceiveServerMessage((sev, msg) => _ = Client_ReceiveServerMessage(sev, msg));
            OnUserReceiveRoomInvite(dto => _ = Client_UserReceiveRoomInvite(dto));
            OnPrivateRoomJoined(dto => _ = Client_PrivateRoomJoined(dto));
            OnPrivateRoomOtherUserJoined(dto => _ = Client_PrivateRoomOtherUserJoined(dto));
            OnPrivateRoomOtherUserLeft(dto => _ = Client_PrivateRoomOtherUserLeft(dto));
            OnPrivateRoomRemovedUser(dto => _ = Client_PrivateRoomRemovedUser(dto));
            OnPrivateRoomUpdateUser(dto => _ = Client_PrivateRoomUpdateUser(dto));
            OnPrivateRoomMessage(dto => _ = Client_PrivateRoomMessage(dto));
            OnPrivateRoomReceiveUserDevice(dto => _ = Client_PrivateRoomReceiveUserDevice(dto));
            OnPrivateRoomDeviceUpdate(dto => _ = Client_PrivateRoomDeviceUpdate(dto));
            OnPrivateRoomClosed(dto => _ = Client_PrivateRoomClosed(dto));

            OnToyboxUserSendOnline(dto => _ = Client_ToyboxUserSendOnline(dto));
            OnToyboxUserSendOffline(dto => _ = Client_ToyboxUserSendOffline(dto));

            // create a new health check token
            _toyboxHealthCTS?.Cancel();
            _toyboxHealthCTS?.Dispose();
            _toyboxHealthCTS = new CancellationTokenSource();
            _ = ClientToyboxHealthCheck(_toyboxHealthCTS.Token);
            // set us to initialized (yippee!!!)
            _toyboxInitialized = true;
        }
    }

    /// <summary> Load the initial pairs linked with the client</summary>
    private async Task LoadIninitialPairs()
    {
        // for each user pair in the paired clients list
        foreach (var userPair in await UserGetPairedClients().ConfigureAwait(false))
        {
            // debug the pair, then add it to the pair manager.
            Logger.LogTrace("Individual Pair Found: " + userPair.User.AliasOrUID, LoggerType.ApiCore);
            _pairManager.AddUserPair(userPair);
        }
    }

    /// <summary> Load the online pairs linked with the client </summary>
    private async Task LoadOnlinePairs()
    {
        // for each online user pair in the online user pairs list
        foreach (var entry in await UserGetOnlinePairs().ConfigureAwait(false))
        {
            // debug the pair, then mark it as online in the pair manager.
            Logger.LogDebug("Pair online: " + entry, LoggerType.ApiCore);
            _pairManager.MarkPairOnline(entry, sendNotif: false);
        }
        Mediator.Publish(new OnlinePairsLoadedMessage());
    }

    /// <summary> Load the online pairs linked with the client </summary>
    private async Task LoadToyboxOnlinePairs()
    {
        var UidList = _pairManager.GetOnlineUserUids();
        // for each online user pair in the online user pairs list
        foreach (var entry in await ToyboxUserGetOnlinePairs(UidList).ConfigureAwait(false))
        {
            // debug the pair, then mark it as online in the pair manager.
            Logger.LogDebug("Pair online: " + entry, LoggerType.ApiCore);
            _pairManager.MarkPairToyboxOnline(entry);
        }
    }

    /* ================ Main Hub SignalR Functions ================ */
    /// <summary> When the hub is closed, this function will be called. </summary>
    private void GagspeakHubOnClosed(Exception? arg)
    {
        Logger.LogWarning("Hub Closed");
        // cancel the health token
        _healthCTS?.Cancel();
        // publish a disconnected message to the mediator, and set the server state to offline. Then log the result
        Mediator.Publish(new DisconnectedMessage());
        ServerState = ServerState.Offline;
        if (arg != null)
        {
            Logger.LogWarning($"{arg} Connection closed");
        }
        else
        {
            Logger.LogInformation("Connection closed", LoggerType.ApiCore);
        }
    }

    /// <summary> When the hub is reconnected, this function will be called. </summary>
    private async Task GagspeakHubOnReconnected()
    {
        // set the state to reconnected
        ServerState = ServerState.Reconnecting;
        try
        {
            // initialize the api hooks again
            InitializeApiHooks();
            // get the new connectionDto // TODO: Make this false, not true.
            _connectionDto = await GetConnectionDto(publishConnected: true).ConfigureAwait(false);
            // if its not equal to the APIVersion then stop the connection
            if (_connectionDto.ServerVersion != IGagspeakHub.ApiVersion)
            {
                Logger.LogInformation("_connectionDto.ServerVersion != IGagspeakHub.ApiVersion", LoggerType.ApiCore);
                await StopConnection(ServerState.VersionMisMatch).ConfigureAwait(false);
                return;
            }
            // if it is greater than the current client version in general, let them know the client is outdated
            if (_connectionDto.CurrentClientVersion > Assembly.GetExecutingAssembly().GetName().Version!)
            {
                // publish a notification message that the client is outdated
                Mediator.Publish(new NotificationMessage("Client outdated",
                    $"Your client is outdated ({Assembly.GetExecutingAssembly().GetName().Version!.Major}." +
                    $"{Assembly.GetExecutingAssembly().GetName().Version!.Minor}.{Assembly.GetExecutingAssembly().GetName().Version!.Build}.{Assembly.GetExecutingAssembly().GetName().Version!.Revision}), current is: " +
                    $"{_connectionDto.CurrentClientVersion.Major}.{_connectionDto.CurrentClientVersion.Minor}.{_connectionDto.CurrentClientVersion.Build}.{_connectionDto.CurrentClientVersion.Revision} " +
                    $"Please keep your Gagspeak client up-to-date.",
                    NotificationType.Warning));

                // stop connection
                Logger.LogInformation("_connectionDto.CurrentClientVersion > Assembly.GetExecutingAssembly().GetName().Version!", LoggerType.ApiCore);
                await StopConnection(ServerState.VersionMisMatch).ConfigureAwait(false);
                return;

            }
            // otherwise set it to connected and publish the connected message after loading the pairs.
            ServerState = ServerState.Connected;
            await LoadIninitialPairs().ConfigureAwait(false);
            await LoadOnlinePairs().ConfigureAwait(false);
            Mediator.Publish(new ConnectedMessage(_connectionDto));
        }
        catch (Exception ex)
        {
            // stop connection if this throws an exception at any point.
            Logger.LogError($"{ex} Failure to obtain data after reconnection");
            await StopConnection(ServerState.Disconnected).ConfigureAwait(false);
        }
    }

    /// <summary> When the hub is reconnecting, this function will be called. </summary>
    private void GagspeakHubOnReconnecting(Exception? arg)
    {
        // set do not notify on next info to true
        _doNotNotifyOnNextInfo = true;
        // call the healthcheck token and cancel it
        _healthCTS?.Cancel();
        // set the state to reconnecting
        ServerState = ServerState.Reconnecting;

        if (arg is System.Net.WebSockets.WebSocketException)
        {
            Logger.LogInformation("System closed unexpectedly, flagging Achievement Manager to not set data on reconnection.");
            AchievementManager._lastDisconnectTime = DateTime.UtcNow;
        }

        Logger.LogWarning($"{arg} Connection closed... Reconnecting");
        Logger.LogInformation("Connection interrupted, reconnecting to " + _serverConfigs.CurrentServer.ServerName, LoggerType.ApiCore);
    }

    /* ================ Toybox Hub SignalR Functions ================ */
    /// <summary> When the hub is closed, this function will be called. </summary>
    private void ToyboxHubOnClosed(Exception? arg)
    {
        // cancel the health token
        _toyboxHealthCTS?.Cancel();
        // publish a disconnected message to the mediator, and set the server state to offline. Then log the result
        Mediator.Publish(new ToyboxDisconnectedMessage());
        // dont publish disconnected message, because we dont want to stop anything (yet), but set state to offline
        ToyboxServerState = ServerState.Offline;
        if (arg != null)
        {
            Logger.LogWarning($"{arg} Toybox Connection closed");
        }
        else
        {
            Logger.LogInformation("Toybox Connection closed", LoggerType.ApiCore);
        }
    }

    /// <summary> When the hub is reconnected, this function will be called. </summary>
    private async Task ToyboxHubOnReconnected()
    {
        // set the state to reconnected
        ToyboxServerState = ServerState.Reconnecting;
        try
        {
            // initialize the api hooks again
            InitializeApiHooks(HubType.ToyboxHub);
            // get the new connectionDto
            _toyboxConnectionDto = await GetToyboxConnectionDto(publishConnected: false).ConfigureAwait(false);
            // if its not equal to the APIVersion then stop the connection
            if (_toyboxConnectionDto.ServerVersion != IGagspeakHub.ApiVersion)
            {
                await StopConnection(ServerState.VersionMisMatch, HubType.ToyboxHub).ConfigureAwait(false);
                return;
            }

            // otherwise set it to connected and publish the connected message after loading the pairs.
            ToyboxServerState = ServerState.Connected;
            Mediator.Publish(new ToyboxConnectedMessage(_toyboxConnectionDto));
        }
        catch (Exception ex)
        {
            // stop connection if this throws an exception at any point.
            Logger.LogError($"{ex} Failure to obtain data after reconnection to toybox hub");
            await StopConnection(ServerState.Disconnected, HubType.ToyboxHub).ConfigureAwait(false);
        }
    }

    /// <summary> When the hub is reconnecting, this function will be called. </summary>
    private void ToyboxHubOnReconnecting(Exception? arg)
    {
        // call the healthcheck token and cancel it
        _toyboxHealthCTS?.Cancel();
        // set the state to reconnecting
        ToyboxServerState = ServerState.Reconnecting;
        Logger.LogWarning($"{arg} Connection closed with toybox hub... Reconnecting");
        // publish a event message to the mediator alerting us of the reconnection
        Logger.LogInformation("Connection interrupted, reconnecting to Toybox Hub", LoggerType.ApiCore);
    }

    /// <summary>
    /// Refresh the token and check if we need to reconnect.
    /// </summary>
    /// <param name="ct">The Cancelation token</param>
    /// <returns>a boolean that is true if we need to refresh the token, and false if not.</returns>
    private async Task<bool> RefreshToken(CancellationToken ct)
    {
        // Logger.LogTrace("Checking token", LoggerType.ApiCore);
        // assume we dont require a reconnect
        bool requireReconnect = false;
        try
        {
            // get the token from the token provider
            var token = await _tokenProvider.GetOrUpdateToken(ct).ConfigureAwait(false);
            // if the token is not equal to the last used token
            if (!string.Equals(token, _lastUsedToken, StringComparison.Ordinal))
            {
                Logger.LogDebug("Reconnecting due to updated token", LoggerType.ApiCore);
                // reconnect because it was updated
                _doNotNotifyOnNextInfo = true;
                await CreateConnections().ConfigureAwait(false);
                requireReconnect = true;
            }
        }
        catch (GagspeakAuthFailureException ex)
        {
            AuthFailureMessage = ex.Reason;
            await StopConnection(ServerState.Unauthorized).ConfigureAwait(false);
            requireReconnect = true;
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Could not refresh token, forcing reconnect");
            _doNotNotifyOnNextInfo = true;
            await CreateConnections().ConfigureAwait(false);
            requireReconnect = true;
        }
        // return if it was required or not at the end of this logic.
        return requireReconnect;
    }

    /// <summary> Stop the connection to the server. </summary>
    private async Task StopConnection(ServerState state, HubType hubType = HubType.MainHub)
    {
        if (hubType == HubType.MainHub)
        {
            // set state to disconnecting
            ServerState = ServerState.Disconnecting;
            // dispose of the hub factory
            Logger.LogInformation("Stopping existing connection", LoggerType.ApiCore);
            await _hubFactory.DisposeHubAsync(HubType.MainHub).ConfigureAwait(false);
            // if the hub is not null
            if (_gagspeakHub is not null)
            {
                // publish the event message to the mediator that we are stopping the connection
                Logger.LogInformation("Stopping existing connection to Main Hub :: " + _serverConfigs.CurrentServer.ServerName, LoggerType.ApiCore);
                // set initialized to false, cancel the health CTS, and publish a disconnected message to the mediator
                _initialized = false;
                _healthCTS?.Cancel();
                Mediator.Publish(new DisconnectedMessage());
                // set the connectionDto and hub to null.
                _gagspeakHub = null;
                _connectionDto = null;
            }
            // update the server state.
            ServerState = state;
        }
        else // hub type is toybox hub
        {
            // set state to disconnecting
            ToyboxServerState = ServerState.Disconnecting;
            // dispose of the hub factory
            Logger.LogInformation("Stopping existing Toybox connection", LoggerType.ApiCore);
            await _hubFactory.DisposeHubAsync(HubType.ToyboxHub).ConfigureAwait(false);
            // if the hub is not null
            if (_toyboxHub is not null)
            {
                // publish the event message to the mediator that we are stopping the connection
                Logger.LogInformation("Stopping existing connection to Toybox Hub", LoggerType.ApiCore);
                // set initialized to false, cancel the health CTS, and publish a disconnected message to the mediator
                _toyboxInitialized = false;
                _toyboxHealthCTS?.Cancel();
                Mediator.Publish(new ToyboxDisconnectedMessage());
                // set the connectionDto and hub to null.
                _toyboxHub = null;
                _toyboxConnectionDto = null;
            }
            // update the server state.
            ToyboxServerState = state;
        }
    }

    /// <summary> A helper function to check if the connection is valid. 
    /// <para>Throws exception if not connected, preventing further code from executing.</para>
    /// </summary>
    /// <exception cref="InvalidDataException"></exception>
    private void CheckConnection(HubType hubType = HubType.MainHub)
    {
        if (hubType == HubType.MainHub)
        {
            if (ServerState is not (ServerState.Connected or ServerState.Connecting or ServerState.Reconnecting))
            {
                throw new InvalidDataException("Not connected");
            }
        }
        else // hub type is toybox hub
        {
            if (ToyboxServerState is not (ServerState.Connected or ServerState.Connecting or ServerState.Reconnecting))
            {
                throw new InvalidDataException("ToyboxHub Not connected");
            }
        }
    }
}
#pragma warning restore MA0040
