using Buttplug.Client;
using Buttplug.Core.Messages;
using GagSpeak.Services.Mediator;
using DebounceThrottle;

namespace GagSpeak.Services.Data;


public class Trigger
{
    private readonly ILogger<Trigger> _logger;
    private readonly ButtplugClientDevice? ClientDevice;

    public Trigger(ILogger<Trigger> logger)
    {
        _logger = logger;
    }
}
