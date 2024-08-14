using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using GagSpeak.Services.Mediator;
using GagSpeak.Services.Textures;
using GagSpeak.UI;
using GagSpeak.UpdateMonitoring;
using ImGuiNET;
using System.IO;
using System.Numerics;

namespace GagSpeak.Services;
public class GagspeakProfile : DisposableMediatorSubscriberBase
{
    private readonly UiSharedService _uiShared; // TODO: Migrate this to a scoped shared texture service.
    // in order for it to be a host to a shared texture service, methods that access it MUST be made in a draw.

    private string _base64ProfilePicture;
    private Lazy<byte[]> _imageData;
    // TODO: Manage this texture wrap (and all future ones) by access time, so our profile service can cleanup
    // the texture wraps and dispose of them when needed.
    private IDalamudTextureWrap? _lastProfileImage;


    public GagspeakProfile(ILogger<GagspeakProfile> logger, GagspeakMediator mediator,
        UiSharedService uiShared, bool flagged, string base64ProfilePicture, 
        string description) : base(logger, mediator)
    {
        _uiShared = uiShared;

        Flagged = flagged;
        Base64ProfilePicture = base64ProfilePicture;
        Description = description;
        ImageData = new Lazy<byte[]>(() => Convert.FromBase64String(Base64ProfilePicture));

        Mediator.Subscribe<ClearProfileDataMessage>(this, (msg) =>
        {
            if (msg.UserData == null || string.Equals(msg.UserData.UID, _uiShared.ApiController.UID, StringComparison.Ordinal))
            {
                _lastProfileImage?.Dispose();
                _lastProfileImage = null;
            }
        });
    }

    private bool RefreshData = true;
    private bool RefreshWrap = true;

    public bool Flagged { get; set; }
    public string Description { get; set; }
    public string Base64ProfilePicture
    {
        get => _base64ProfilePicture;
        set
        {
            if (_base64ProfilePicture != value)
            {
                _base64ProfilePicture = value;
                Logger.LogDebug("Profile picture updated.");
                RefreshData = true;
                RefreshWrap = true;
            }
        }
    }
    public Lazy<byte[]> ImageData
    {
        get
        {
            if (RefreshData)
            {
                Logger.LogTrace("Refreshing profile image data!");
                _imageData = new Lazy<byte[]>(() => ConvertBase64ToByteArray(Base64ProfilePicture));
                Logger.LogTrace("Refreshed profile image data!");
                RefreshData = false;
            }
            return _imageData;
        }
        set
        {
            _imageData = value;
            RefreshWrap = true;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Logger.LogWarning("Disposing profile image data!");
            _lastProfileImage?.Dispose();
            _lastProfileImage = null;
        }
        base.Dispose(disposing);
    }

    public IDalamudTextureWrap? GetCurrentProfileOrDefault()
    {
        // help prevent the image from being loaded multiple times.
        if (RefreshWrap)
        {
            if (ImageData.Value == null || ImageData.Value.Length == 0)
            {
                Logger.LogTrace("Loading no radial small.");
                _lastProfileImage?.Dispose();
                _lastProfileImage = _uiShared.RentLogoNoRadial();
            }
            else
            {
                Logger.LogTrace("Loading default image while processing the actual image.");
                _lastProfileImage?.Dispose();
                _lastProfileImage = _uiShared.RentLogoNoRadial();
                _ = Task.Run(() =>
                {
                    LoadImageDataToWrap();
                });
            }
            RefreshWrap = false;
        }
        return _lastProfileImage;
    }

    private void LoadImageDataToWrap()
    {
        try
        {
            _lastProfileImage = _uiShared.LoadImage(ImageData.Value);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load profile image data to wrap.");
        }

    }

    private byte[] ConvertBase64ToByteArray(string base64String)
    {
        if (string.IsNullOrEmpty(base64String))
        {
            return Array.Empty<byte>();
        }

        try
        {
            return Convert.FromBase64String(base64String);
        }
        catch (FormatException ex)
        {
            Logger.LogError(ex, "Invalid Base64 string for profile picture.");
            return Array.Empty<byte>();
        }
    }
}
