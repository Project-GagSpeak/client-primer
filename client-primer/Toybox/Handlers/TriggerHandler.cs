using GagSpeak.PlayerData.Data;
using GagSpeak.Services;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using GagSpeak.WebAPI;

namespace GagSpeak.PlayerData.Handlers;

public class TriggerHandler
{
    private readonly ILogger<TriggerHandler> _logger;
    public TriggerHandler(ILogger<TriggerHandler> logger)
    {
        _logger = logger;
        // may or may not ever be a thing, dont keep ur hopes up
    }
}
