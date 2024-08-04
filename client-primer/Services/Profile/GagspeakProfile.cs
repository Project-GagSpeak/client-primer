using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using GagSpeak.Services.Textures;
using GagSpeak.UI;
using GagSpeak.UpdateMonitoring;
using ImGuiNET;
using System.IO;
using System.Numerics;

namespace GagSpeak.Services;
public class GagspeakProfile
{
    private readonly ILogger<GagspeakProfile> _logger;
    private readonly CosmeticTexturesService _profileTextures;

    private Lazy<byte[]> _imageData;

    public GagspeakProfile(ILogger<GagspeakProfile> logger, CosmeticTexturesService pfpTextures,
        bool flagged, string base64ProfilePicture, string description)
    {
        _logger = logger;
        _profileTextures = pfpTextures;

        Flagged = flagged;
        Base64ProfilePicture = base64ProfilePicture;
        Description = description;
        _imageData = new Lazy<byte[]>(() => Convert.FromBase64String(Base64ProfilePicture));
    }

    public bool Flagged { get; set; }
    public string Base64ProfilePicture { get; set; }
    public string Description { get; set; }
    public Lazy<byte[]> ImageData => new Lazy<byte[]>(() => Convert.FromBase64String(Base64ProfilePicture));
}
