using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using GagSpeak.Services.Mediator;
using GagSpeak.Services.Textures;
using GagSpeak.UI;

namespace GagSpeak.Services;
public class ProfileFactory
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly GagspeakMediator _mediator;
    private readonly UiSharedService _uiShared;

    public ProfileFactory(ILoggerFactory loggerFactory, GagspeakMediator mediator,
        UiSharedService uiShared)
    {
        _loggerFactory = loggerFactory;
        _mediator = mediator;
        _uiShared = uiShared;
    }

    public GagspeakProfile CreateProfileData(bool Flagged, string Base64ProfilePicture, string Description)
    {
        return new GagspeakProfile(_loggerFactory.CreateLogger<GagspeakProfile>(), _mediator,
            _uiShared, Flagged, Base64ProfilePicture, Description);
    }
}
