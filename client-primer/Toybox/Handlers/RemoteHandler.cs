using GagSpeak.Services.Mediator;
using GagSpeak.UI;
using System.Numerics;

namespace UI.UiRemote;

public class RemoteHandler
{
    private readonly ILogger<RemoteHandler> _logger;
    private readonly UiSharedService _uiSharedService;

    public RemoteHandler(ILogger<RemoteHandler> logger,
        UiSharedService uiSharedService)
    {
        _uiSharedService = uiSharedService;
    }

}
