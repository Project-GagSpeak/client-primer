using Buttplug.Client;
using GagSpeak.Services.Data;

namespace GagSpeak.UI;

// we need a factory to create new instances of Device objects whenever a device is added.
public class DeviceFactory
{
    private readonly ILoggerFactory _loggerfactory;

    public DeviceFactory(ILoggerFactory loggerfactory)
    {
        _loggerfactory = loggerfactory;
    }

    public ConnectedDevice CreateConnectedDevice(ButtplugClientDevice newDevice)
    {
        return new ConnectedDevice(_loggerfactory.CreateLogger<ConnectedDevice>(), newDevice);
    }
}
