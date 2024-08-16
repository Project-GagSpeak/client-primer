using GagSpeak.PlayerData.Pairs;
using GagSpeak.Services.Mediator;
using GagSpeak.Utils.ChatLog;
using GagspeakAPI.Data.Enum;

namespace GagSpeak.Services;

// handles the global chat and pattern discovery social features.
public class MigrateRestraintSets
{
    private readonly ILogger<MigrateRestraintSets> _logger;    
    public MigrateRestraintSets(ILogger<MigrateRestraintSets> logger)
    {
        _logger = logger;
    }
}
