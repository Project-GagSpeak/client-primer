using GagSpeak.PlayerData.Pairs;
using GagSpeak.Services.Mediator;
using GagSpeak.Utils.ChatLog;
using GagspeakAPI.Data.Enum;

namespace GagSpeak.Services;

// handles the global chat and pattern discovery social features.
public class MigrateGagStorage
{
    private readonly ILogger<MigrateGagStorage> _logger;
    public MigrateGagStorage(ILogger<MigrateGagStorage> logger)
    {
        _logger = logger;
    }
}
