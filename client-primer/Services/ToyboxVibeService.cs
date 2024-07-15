using Buttplug.Client;
using Buttplug.Client.Connectors.WebsocketConnector;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;
using GagSpeak.PlayerData.Data;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;

namespace GagSpeak.Services;
public class ToyboxVibeService : DisposableMediatorSubscriberBase
{
    private readonly ClientConfigurationManager _clientConfigs;
    private readonly PlayerCharacterManager _playerManager;
    private readonly IChatGui _chatGui; // for sending messages to the chat
    public ButtplugClient client;
    public ButtplugWebsocketConnector connector;
    public ButtplugClientDevice? ActiveDevice; // our connected, active device from the list of devices in the ButtplugClient.
    // internal var for the dtr bar
    private readonly IDtrBarEntry DtrEntry;
    private DateTime LastBatteryCheck;

    public ToyboxVibeService(ILogger<ToyboxVibeService> logger, GagspeakMediator mediator,
        ClientConfigurationManager config, PlayerCharacterManager characterHandler,
        IChatGui chatGui, IDtrBar dtrBar) : base(logger, mediator)
    {
        _clientConfigs = config;
        _playerManager = characterHandler;
        _chatGui = chatGui;

        // initially assume we have no connected devices
        AnyDeviceConnected = false;
        IsScanning = false;
        DeviceIndex = -1;
        StepCount = 0;
        BatteryLevel = 0;
        LastBatteryCheck = DateTime.MinValue;
        ////// STEP ONE /////////
        // connect to connector and then client
        connector = CreateNewConnector();
        // create a new client
        client = new ButtplugClient("GagSpeak");
        ////// STEP TWO /////////
        // once the client connects, it will ask server for list of devices.
        // we will want to make sure that we check to see if we have them, meaning we will need to set up events.
        // in order to cover all conditions, we should set up all of our events so we can recieve them from the server
        // (Side note: it is fine that our connector is defined up top, because it isnt used until we do ConnectASync)
        client.DeviceAdded += OnDeviceAdded;
        client.DeviceRemoved += OnDeviceRemoved;
        client.ScanningFinished += OnScanningFinished;
        client.ServerDisconnect += OnServerDisconnect;

        // set the dtr bar entry
        DtrEntry = dtrBar.Get("GagSpeak");

        // subscribe to mediator events.
        Mediator.Subscribe<ToyboxActiveDeviceChangedMessage>(this, (msg) =>
        {
            Logger.LogDebug($"Active Device Index Changed to: {msg.DeviceIndex}");
            // first make sure this index is within valid bounds of our client devices
            if (msg.DeviceIndex >= 0 && msg.DeviceIndex < client.Devices.Count())
            {
                DeviceIndex = msg.DeviceIndex;
                ActiveDevice = client.Devices.ElementAt(DeviceIndex);
                // get the step size for the new device
                GetStepIntervalForActiveDevice();
                GetBatteryLevelForActiveDevice();
            }
            else
            {
                Logger.LogError($"Active Device Index {msg.DeviceIndex} out of bounds, not updating.");
            }

        });

        Mediator.Subscribe<FrameworkUpdateMessage>(this, (msg) => FrameworkUpdate());
    }

    // public accessors
    public int DeviceIndex;         // know our actively selected device index in _client.Devices[]
    public bool AnyDeviceConnected; // to know if any device is connected without needing to interact with server
    public bool IsScanning;         // to know if we are scanning
    public double StepInterval;         // know the step size of our active device
    public int StepCount;           // know the step count of our active device
    public double BatteryLevel;     // know the battery level of our active device
    public int VibratorIntensity;   // the intensity of the vibrator. (reflects what is played to our lovense device)
    public bool ClientConnected => client.Connected;
    public bool ActiveDeviceNotNull => ActiveDevice != null;
    public bool HasConnectedDevice => client.Connected && client.Devices.Any();

    // when this service is disposed, we need to be sure to dispose of the client and the connector
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        // client disposal
        client.DisconnectAsync();
        client.Dispose();
        connector.Dispose();
        // framework disposal
        DtrEntry.Remove();

        // dispose of our events
        client.DeviceAdded -= OnDeviceAdded;
        client.DeviceRemoved -= OnDeviceRemoved;
        client.ScanningFinished -= OnScanningFinished;
        client.ServerDisconnect -= OnServerDisconnect;
    }

    private void FrameworkUpdate()
    {
        // if using a connected device, update battery level and display percent remaining to DTR Bar
        if (ActiveDevice != null)
        {
            string displayName = string.IsNullOrEmpty(ActiveDevice.DisplayName) ? ActiveDevice.Name : ActiveDevice.DisplayName;
            int batteryPercent = (int)(BatteryLevel * 100);

            DtrEntry.Text = new SeString(
                new IconPayload(BitmapFontIcon.ElementLightning),
                new TextPayload($"{displayName} - {batteryPercent}%"));

            DtrEntry.Shown = true;
        }
        // otherwise if using a simulated vibe do the same but with simulated vibe message
        else if (_clientConfigs.GagspeakConfig.UsingSimulatedVibrator && _playerManager.GlobalPerms.ToyIsActive)
        {
            DtrEntry.Text = new SeString(
                new IconPayload(BitmapFontIcon.ElementLightning),
                new TextPayload("Simulated Vibe Active"));

            DtrEntry.Shown = true;
        }
        // otherwise do not show dtr
        else
        {
            DtrEntry.Shown = false;
        }

        // trigger a battery check every 15 seconds while connected
        if (ActiveDevice != null && (DateTime.Now - LastBatteryCheck).TotalSeconds > 30)
        {
            GetBatteryLevelForActiveDevice();
            LastBatteryCheck = DateTime.Now;
        }
    }


    #region Event Handlers
    private void OnDeviceAdded(object? sender, DeviceAddedEventArgs e)
    {
        Logger.LogDebug($"Device Added: {e.Device.Name}");
        if (client.Devices.Count() > 0 && !AnyDeviceConnected)
        {
            ActiveDevice = client.Devices.First();
            GetStepIntervalForActiveDevice();
            GetBatteryLevelForActiveDevice();
            AnyDeviceConnected = true;
        }
        if (AnyDeviceConnected)
        {
            ActiveDevice = client.Devices.First();
            GetStepIntervalForActiveDevice();
            GetBatteryLevelForActiveDevice();
        }
    }

    /// <summary Fired every time a device is removed from the client </summary>
    private void OnDeviceRemoved(object? sender, DeviceRemovedEventArgs e)
    {
        Logger.LogDebug($"Device Removed: {e.Device.Name}");
        if (!HasConnectedDevice)
        {
            AnyDeviceConnected = false;
            DeviceIndex = -1;
        }
    }

    /// <summary Fired when scanning for devices is finished </summary>
    private void OnScanningFinished(object? sender, EventArgs e)
    {
        Logger.LogDebug("Scanning Finished");
        IsScanning = false;
    }

    /// <summary Fired when the server disconnects </summary>
    private void OnServerDisconnect(object? sender, EventArgs e)
    {
        // reset all of our variables so we dont display any data requiring one
        AnyDeviceConnected = false;
        IsScanning = false;
        DeviceIndex = -1;
        ActiveDevice = null;
        StepCount = 0;
        BatteryLevel = 0;
        Logger.LogDebug("Server Disconnected");
    }

    #endregion Event Handlers

    #region Client Functions

    public ButtplugWebsocketConnector CreateNewConnector()
    {
        return _clientConfigs.GagspeakConfig.IntifaceConnectionSocket != null
                    ? new ButtplugWebsocketConnector(new Uri($"{_clientConfigs.GagspeakConfig.IntifaceConnectionSocket}"))
                    : new ButtplugWebsocketConnector(new Uri("ws://localhost:12345"));
    }

    /// <summary> Connect to the server asynchronously </summary>
    public async void ConnectToServerAsync()
    {
        try
        {
            if (client.Connected)
            {
                Logger.LogInformation("No Need to connect to the Intiface server, client was already connected!");
            }

            Logger.LogDebug("Attempting to connect to the Intiface server, client was not initially connected");
            await client.ConnectAsync(connector);
            // after we wait for the connector to process, check if the connection is there
            if (client.Connected)
            {
                // 1. see if there are already devices connected
                AnyDeviceConnected = HasConnectedDevice;
                // 2. If AnyDeviceConnected is true, set our active device to the first device in the list
                if (AnyDeviceConnected)
                {
                    // set our active device to the first device in the list
                    ActiveDevice = client.Devices.First();
                    GetStepIntervalForActiveDevice();
                    GetBatteryLevelForActiveDevice();
                    // we should also set our device index to 0
                    DeviceIndex = 0;
                    // activate the vibe
                    if (_playerManager.GlobalPerms.ToyIsActive)
                    {
                        Logger.LogDebug($"Active Device: {ActiveDevice.Name}, is enabled! Vibrating with intensity: " +
                            $"{(byte)((VibratorIntensity / (double)StepCount) * 100)}");
                        // do the vibe babyyy
                        await ToyboxVibrateAsync((byte)((VibratorIntensity / (double)StepCount) * 100), 100);
                    }
                }
                // if we meet here, it's fine, it just means we are connected and dont yet have anything to display.
                // So we will wait until a device is added to set AnyDeviceConnected to true
                Logger.LogInformation("Successfully connected to the Intiface server!");
            }
            else
            {
                DisconnectAsync();
                Logger.LogInformation("Timed out while attempting to connect to Intiface Server!");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error in ConnectToServerAsync: {ex.Message.ToString()}");
            Logger.LogInformation("Error while connecting to Intiface Server! Make sure you have disabled any other plugins " +
            $"that connect with Intiface before connecting, or make sure you have Intiface running.");
        }
    }

    /// <summary> Disconnect from the server asynchronously </summary>
    public async void DisconnectAsync()
    {
        // when called, attempt to:
        try
        {
            // see if we are connected to the server. If we are, then disconnect from the server
            if (client.Connected)
            {
                await client.DisconnectAsync();
                if (client.Connected == false)
                {
                    Logger.LogInformation("Successfully disconnected from the Intiface server!");
                }
            }
            // once it is disconnected, dispose of the client and the connector
            connector.Dispose();
            connector = CreateNewConnector();
        }
        // if at any point we fail here, throw an exception
        catch (Exception ex)
        {
            // log the exception
            Logger.LogError($"Error while disconnecting from Async: {ex.ToString()}");
        }
    }

    /// <summary> Start scanning for devices asynchronously </summary>
    public async Task StartScanForDevicesAsync()
    {
        Logger.LogInformation("Now scanning for devices, you may attempt to connect a device now");
        try
        {
            if (client.Connected)
            {
                IsScanning = true;
                await client.StartScanningAsync();
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error in ScanForDevicesAsync: {ex.ToString()}");
        }
    }

    /// <summary> Stop scanning for devices asynchronously </summary>
    public async Task StopScanForDevicesAsync()
    {
        Logger.LogInformation("Halting the scan for new devices to add");
        try
        {
            if (client.Connected)
            {
                await client.StopScanningAsync();
                if (IsScanning)
                {
                    IsScanning = false;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error in StopScanForDevicesAsync: {ex.ToString()}");
        }
    }

    /// <summary> Get's the devices step size. </summary>
    /// <returns>The step size of the device</returns>
    public void GetStepIntervalForActiveDevice()
    {
        try
        {
            if (client.Connected && ActiveDevice != null)
            {
                if (ActiveDevice.VibrateAttributes.Count > 0)
                {
                    StepCount = (int)ActiveDevice.VibrateAttributes.First().StepCount;
                    StepInterval = 1.0 / StepCount;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error in setting step size: {ex.ToString()}");
        }
    }

    public async void GetBatteryLevelForActiveDevice()
    {
        try
        {
            if (client.Connected && ActiveDevice != null)
            {
                if (ActiveDevice.HasBattery)
                {
                    // try get to get the battery level
                    try
                    {
                        BatteryLevel = await ActiveDevice.BatteryAsync();
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Error in getting battery level: {ex.ToString()}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error in getting battery level: {ex.ToString()}");
        }
    }


    // Vibrate the device for a set amount of time and strength
    public async Task ToyboxVibrateAsync(byte intensity, int msUntilTaskComplete = 100)
    {
        // when this is received, attempt to:
        try
        {
            // must recalculate the strength to be between 0 and 1. Here before going in
            double strength = intensity / 100.0;
            // round the strength to the nearest step
            strength = Math.Round(strength / StepInterval) * StepInterval;
            // log it
            // Logger.LogDebug($"Rounded Step: {strength}");
            // send it
            if (AnyDeviceConnected && DeviceIndex >= 0 && msUntilTaskComplete > 0)
            {
                await ActiveDevice!.VibrateAsync(strength); // the strength to move to from previous strength level
                // wait for the set amount of seconds
                await Task.Delay(msUntilTaskComplete);
            }
            else
            {
                Logger.LogError("No device connected or device index is out of bounds, cannot vibrate.");

            }
        }
        // if at any point we fail here, throw an exception
        catch (Exception ex)
        {
            // log the exception
            Logger.LogError($"Error while vibrating: {ex.ToString()}");
        }
    }
    #endregion Toy Functions
}

