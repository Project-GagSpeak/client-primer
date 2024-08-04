using GagSpeak.Services.Mediator;
using GagSpeak.UI;
using System.Numerics;

namespace UI.UiRemote;

/// <summary>
/// Responsibilities:
/// 
/// - Handle the containment of recorded data and stored data
/// - Dictate what is played to who, and what kind of remote is being used(?)
/// - Handle the saving and loading of patterns.
/// </summary>
public class RemoteHandler
{
    private readonly ILogger<RemoteHandler> _logger;


    public RemoteHandler(ILogger<RemoteHandler> logger)
    {
    }

}
