using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using GagSpeak.Services.Textures;
using GagSpeak.UI;

namespace GagSpeak.Services;
public class ProfileFactory
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly CosmeticTexturesService _cosmeticTextures;
    private readonly UiSharedService _uiShared;

    public ProfileFactory(ILoggerFactory loggerFactory, CosmeticTexturesService cosmeticTextures,
        UiSharedService uiShared)
    {
        _loggerFactory = loggerFactory;
        _cosmeticTextures = cosmeticTextures;
        _uiShared = uiShared;
    }

    public GagspeakProfile CreateProfileData(bool Flagged, string Base64ProfilePicture, string Description)
    {
        return new GagspeakProfile(_loggerFactory.CreateLogger<GagspeakProfile>(), _cosmeticTextures,
            Flagged, Base64ProfilePicture, Description);
    }
}
