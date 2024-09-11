using Buttplug.Client;
using Buttplug.Core.Messages;
using GagSpeak.Services.Mediator;
using DebounceThrottle;
using GagspeakAPI.Data.VibeServer;

namespace GagSpeak.Services.Data;

// at the same time, the payoff of storing disconnected devices could make reconnection faster.
public class ConnectedDevice
{
    private readonly ILogger<ConnectedDevice> _logger;
    public readonly ButtplugClientDevice? ClientDevice;

    public ConnectedDevice(ILogger<ConnectedDevice> logger, ButtplugClientDevice? buttplugClientDevice)
    {
        _logger = logger;
        // only add device if itis not null.
        if (buttplugClientDevice != null)
        {
            ClientDevice = buttplugClientDevice; // set device
            DeviceId = buttplugClientDevice.Index; // set device id
            DeviceName = buttplugClientDevice.Name; // set device name
            SetupCommandsAndAttributes(); // configure what commands the device can execute.
            ResetMotors(); // reset all motors to 0.
            UpdateBatteryPercentage(); // update our battery display.
        }
    }

    // Generic Accessors
    public uint DeviceId = uint.MaxValue;               
    public string DeviceName = string.Empty;
    public string DisplayName = string.Empty; // fancy displayname users can edit.

    // Vibration Accessors
    public bool CanVibrate = false;
    public int VibratorMortors = -1; // # of motors connected device has.
    public int VibratorStepCount = 0; // (how many 'settings' the device has)
    public double VibratorStepInterval = 0.0; // (how much has to change for the vibe to change settings)
    public List<GenericDeviceMessageAttributes> ViberateAttributes = new List<GenericDeviceMessageAttributes>(); // references stored device attributes (thanks c#)
    
    // Rotation Accessors
    public bool CanRotate = false;
    public int RotateMotors = -1;
    public int RotateStepCount = 0;
    public double RotateStepInterval = 0.0;
    public List<GenericDeviceMessageAttributes> RotateAttributes = new List<GenericDeviceMessageAttributes>(); // references stored device attributes (thanks c#)

    // Linear Accessors
    public bool CanLinear = false;
    public int LinearMotors = -1;
    public int LinearStepCount = 0;
    public double LinearStepInterval = 0.0;
    public List<GenericDeviceMessageAttributes> LinearAttributes = new List<GenericDeviceMessageAttributes>(); // references stored device attributes (thanks c#)

    // Oscillation Accessors
    public bool CanOscillate = false;
    public int OscillateMotors = -1;
    public int OscillateStepCount = 0;
    public double OscillateStepInterval = 0.0;
    public List<GenericDeviceMessageAttributes> OscillateAttributes = new List<GenericDeviceMessageAttributes>(); // references stored device attributes (thanks c#)

    // Battery Accessors
    public bool CanCheckBattery = false;
    public double BatteryLevel = -1.0; // the range for battery is in the 0.0 - 1.0 range.
    public bool CanStopExecuting = true; // if the device can stop executing commands.
    public bool IsConnected = false;

    public List<VibrateType> DeviceVibrateTypes = new List<VibrateType>();

    public byte[] CurrentVibratorIntensity      = Array.Empty<byte>();
    public byte[] CurrentRotationIntensity      = Array.Empty<byte>();
    public byte[] CurrentLinearIntensity        = Array.Empty<byte>();
    public byte[] CurrentOscillationIntensity   = Array.Empty<byte>();

    // create our debouncers to prevent overload from multiple inputs occuring too 
    // create a new debouncer with a 20ms delay. (extend if too fast or run into issues, but this allows for max accuracy)
    private DebounceDispatcher VibrateDebouncer     = new DebounceDispatcher(TimeSpan.FromMilliseconds(20));
    private DebounceDispatcher RotateDebouncer      = new DebounceDispatcher(TimeSpan.FromMilliseconds(20));
    private DebounceDispatcher LinearDebouncer      = new DebounceDispatcher(TimeSpan.FromMilliseconds(20));
    private DebounceDispatcher OscillateDebouncer   = new DebounceDispatcher(TimeSpan.FromMilliseconds(20));

    public void SetupCommandsAndAttributes()
    {
        // perform early return if the device is not connected.
        if (ClientDevice == null)
        {
            string text = DisplayName == string.Empty ? DeviceName : DisplayName;
            _logger.LogError($"Device {DeviceId}:{text} is not connected, cannot setup commands.");
            return;
        }

        // otherwise, set up our attributes to reference the ones inside of the device.
        // this way we can easily access them later.
        ViberateAttributes = ClientDevice.VibrateAttributes;
        if(ViberateAttributes.Count > 0)
        {
            CanVibrate = true;
            VibratorMortors = ViberateAttributes.Count;
            VibratorStepCount = (int)ViberateAttributes[0].StepCount;
            VibratorStepInterval = 1.0 / VibratorStepCount;
            DeviceVibrateTypes.Add(VibrateType.Vibrate);
        }
        RotateAttributes = ClientDevice.RotateAttributes;
        if(RotateAttributes.Count > 0)
        {
            CanRotate = true;
            RotateMotors = RotateAttributes.Count;
            RotateStepCount = (int)RotateAttributes[0].StepCount;
            RotateStepInterval = 1.0 / RotateStepCount;
            DeviceVibrateTypes.Add(VibrateType.Rotate);
        }
        LinearAttributes = ClientDevice.LinearAttributes;
        if(LinearAttributes.Count > 0)
        {
            CanLinear = true;
            LinearMotors = LinearAttributes.Count;
            LinearStepCount = (int)LinearAttributes[0].StepCount;
            LinearStepInterval = 1.0 / LinearStepCount;
            DeviceVibrateTypes.Add(VibrateType.Linear);
        }
        OscillateAttributes = ClientDevice.OscillateAttributes;
        if(OscillateAttributes.Count > 0)
        {
            CanOscillate = true;
            OscillateMotors = OscillateAttributes.Count;
            OscillateStepCount = (int)OscillateAttributes[0].StepCount;
            OscillateStepInterval = 1.0 / OscillateStepCount;            
            DeviceVibrateTypes.Add(VibrateType.Oscillate);
        }
        // check if the device can check battery, set that we can check it to true and grab the current %.
        if(ClientDevice.HasBattery)
        {
            CanCheckBattery = true;
            UpdateBatteryPercentage();
        }
    }

    /// <summary> Resets all motors of the device to 0, stopping all vibrations. </summary>
    public void ResetMotors()
    {
        if (CanVibrate) { ResetIntensityHelper(ref CurrentVibratorIntensity, VibratorMortors); }
        if (CanRotate) { ResetIntensityHelper(ref CurrentRotationIntensity, RotateMotors); }
        if (CanLinear) { ResetIntensityHelper(ref CurrentLinearIntensity, LinearMotors); }
        if (CanOscillate) { ResetIntensityHelper(ref CurrentOscillationIntensity, OscillateMotors); }
    }

    /// <summary> Helper function to reset all motors intensity arrays back to 0. </summary>
    public void ResetIntensityHelper(ref byte[] intensityArray, int motorCount)
    {
        intensityArray = new byte[motorCount];
        for (int i = 0; i < motorCount; i++)
        {
            intensityArray[i] = 0;
        }
    }

    /// <summary> Helper function to fetch the current VibrationInfo of a device </summary>
    public List<string> GetVibrationTypesInfo()
    {
        return new (bool condition, string command)[]
        {
            (CanVibrate, $"Vibrate motors={VibratorMortors}"),
            (CanRotate, $"Rotate motors={RotateMotors}"),
            (CanLinear, $"Linear motors={LinearMotors}"),
            (CanOscillate, $"Oscillate motors={OscillateMotors}"),
            (CanCheckBattery, $"Battery Level={BatteryLevel}"),
            (CanStopExecuting, $"Can Stop Executing={CanStopExecuting}")
        }
        .Where(tuple => tuple.condition)
        .Select(tuple => tuple.command)
        .ToList();
    }

    public async void UpdateBatteryPercentage()
    {
        // early return if the device is null or we can't check the battery.
        if (ClientDevice == null || !CanCheckBattery) { return; }
        // otherwise, fetch the batter.
        try
        {
            BatteryLevel = await ClientDevice.BatteryAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error Updating Battery Lvl: {ex.Message}");
        }
    }

    /// <summary> Helper function to return a devices battery level in a XX% format </summary>
    public string BatteryPercentString() => BatteryLevel == -1.0 ? "Unknown" : $"{BatteryLevel * 100}%";

    /// <summary> Halt all vibrations </summary>
    public async void StopInTheNameOfTheVibe()
    {
        if (ClientDevice == null || !CanStopExecuting) { return; }
        try
        {
            // halt each type of vibration.
            if(CanVibrate) { await ClientDevice.VibrateAsync(0.0); }
            if(CanRotate) { await ClientDevice.RotateAsync(0.0, clockwise: true); }
            if(CanOscillate) { await ClientDevice.OscillateAsync(0.0); };
            if(CanStopExecuting) { await ClientDevice.Stop(); };
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error Stopping Device: {ex.Message}");
        }
        ResetMotors();
    }

    public double GetVibrationDoubleFromByte(byte intensity) => Math.Round((intensity / 100.0) / VibratorStepInterval) * VibratorStepInterval;
    public double GetRotateDoubleFromByte(byte intensity) => Math.Round((intensity / 100.0) / RotateStepInterval) * RotateStepInterval;
    public double GetLinearDoubleFromByte(byte intensity) => Math.Round((intensity / 100.0) / LinearStepInterval) * LinearStepInterval;
    public double GetOscillateDoubleFromByte(byte intensity) => Math.Round((intensity / 100.0) / OscillateStepInterval) * OscillateStepInterval;


    /// <summary> Send a vibration command.(create worker threads for this later or something idfk) </summary>
    public void SendVibration(byte intensity, int motorIndex = -1)
    {
        if (ClientDevice == null || !CanVibrate || !IsConnected)
        {
            _logger.LogError("Cannot send vibration command, device is not connected or does not support vibration.");
            return;
        }
        // attempt to calulate and send off vibration
        try
        {
            if (motorIndex != -1)
            {
                // append the byte to the corrent motor index spesified.
                CurrentVibratorIntensity[motorIndex] = intensity;
            }
            else
            {
                // otherwise, append the new intensity to all motors
                for (int i = 0; i < VibratorMortors; i++)
                {
                    CurrentVibratorIntensity[i] = intensity;
                }
            }
            // create the storage we are sending off.
            double[] motorIntensitysToSend = new double[VibratorMortors];
            // convert the bytes to the doubles that will be sent off
            for (int i = 0; i < CurrentVibratorIntensity.Length; i++)
            {
                motorIntensitysToSend[i] = GetVibrationDoubleFromByte(CurrentVibratorIntensity[i]);
            }

            // send the vibration off to each motor asyncronously.
            VibrateDebouncer.Debounce(delegate
            {
                ClientDevice.VibrateAsync(motorIntensitysToSend);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error Sending Vibration: {ex}");
        }
    }

    // UNSURE IF THIS NEEDS TO BE ASYNC OR NOT I'VE NEVER WORKED WITH ANYTHING BESIDES VIBRATE
    /// <summary> Send a rotation command. </summary>
    public void SendRotate(byte intensity, bool clockwise = true, int motorIndex = -1)
    {
        _logger.LogDebug("Rotation Disabled Currently");
/*        if (ClientDevice == null || !CanRotate || !IsConnected)
        {
            _logger.LogError("Cannot send rotation command, device is not connected or does not support rotation.");
            return;
        }

        try
        {
            // if motor spesified, update that motor only.
            if (motorIndex != -1)
            {
                CurrentRotationIntensity[motorIndex] = intensity;
            }
            // otherwise update all motors
            else
            {
                for (int i = 0; i < RotateMotors; i++)
                {
                    CurrentRotationIntensity[i] = intensity;
                }
            }
            // prepare sendoff
            List<(double, bool)> motorIntensitysToSend = new List<(double, bool)>();
            // store the bytes as doubles.
            for (int i = 0; i < CurrentRotationIntensity.Length; i++)
            {
                motorIntensitysToSend.Add((GetRotateDoubleFromByte(CurrentRotationIntensity[i]), clockwise));
            }

            // send them off.
            RotateDebouncer.Debounce(delegate
            {
                ClientDevice.RotateAsync(motorIntensitysToSend);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error Sending Rotation: {ex.Message}");
        }*/
    }

    /// <summary>
    /// Sends a linear vibration instruction to the device.
    /// </summary>
    /// <param name="intensity"> The intensity of the vibration, from 0 to 100. </param>
    /// <param name="period"> The duration of the vibration, in milliseconds. </param>
    /// <param name="motorIndex"> The motor on the device to execute the command on. </param>
    public void SendLinear(byte intensity, int period = 500, int motorIndex = -1)
    {
        _logger.LogDebug("Linear Disabled Currently");
/*        if (ClientDevice == null || !CanLinear || !IsConnected)
        {
            _logger.LogError("Cannot send linear command, device is not connected or does not support linear.");
            return;
        }

        try
        {
            // only update the motor spesified if one is.
            if (motorIndex != -1)
            {
                CurrentLinearIntensity[motorIndex] = intensity;
            }
            // otherwise update all motors with the same command.
            else
            {
                for (int i = 0; i < LinearMotors; i++)
                {
                    CurrentLinearIntensity[i] = intensity;
                }
            }
            // prepare the sendoff
            List<(uint, double)> motorIntensitysToSend = new List<(uint, double)>();
            // update each motor accordingly.
            for (int i = 0; i < CurrentLinearIntensity.Length; i++)
            {
                motorIntensitysToSend.Add(((uint)period, GetLinearDoubleFromByte(CurrentLinearIntensity[i])));
            }

            // send it off to the debouncer
            LinearDebouncer.Debounce(delegate
            {
                ClientDevice.LinearAsync(motorIntensitysToSend);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error Sending Linear: {ex.Message}");
        }*/
    }

    /// <summary>
    /// Sends an oscillation instruction to the device.
    /// </summary>
    /// <param name="intensity"> The intensity of the vibration, from 0 to 100. </param>
    /// <param name="period"> The period of the oscillation, in milliseconds. </param>
    /// <param name="motorIndex"> The motor on the device to execute the command on. </param>
    public void SendOscillate(byte intensity, int period = 500, int motorIndex = -1)
    {
        _logger.LogDebug("Oscillation Disabled Currently");
/*        if (ClientDevice == null || !CanOscillate || !IsConnected)
        {
            _logger.LogError("Cannot send oscillation command, device is not connected or does not support oscillation.");
            return;
        }

        try
        {
            // only update the motor spesified if one is.
            if (motorIndex != -1)
            {
                CurrentOscillationIntensity[motorIndex] = intensity;
            }
            // otherwise update all motors with the same command.
            else
            {
                for (int i = 0; i < OscillateMotors; i++)
                {
                    CurrentOscillationIntensity[i] = intensity;
                }
            }
            // prepare the sendoff
            List<(uint, double)> motorIntensitysToSend = new List<(uint, double)>();
            // update each motor accordingly.
            for (int i = 0; i < CurrentOscillationIntensity.Length; i++)
            {
                motorIntensitysToSend.Add(((uint)period, GetOscillateDoubleFromByte(CurrentOscillationIntensity[i])));
            }

            // send it off to the debouncer
            OscillateDebouncer.Debounce(delegate
            {
                ClientDevice.OscillateAsync(motorIntensitysToSend);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error Sending Oscillation: {ex.Message}");
        }*/
    }

}
