using Dalamud.Interface.Textures.TextureWraps;
using GagSpeak.PlayerData.Data;
using GagSpeak.Services.Mediator;
using GagSpeak.Services.Textures;
using GagSpeak.UI;
using GagSpeak.WebAPI;
using GagspeakAPI.Data;
using GagspeakAPI.Data.IPC;
using Microsoft.IdentityModel.Tokens;

namespace GagSpeak.Services;

/// <summary>
/// Determines the configuration data stored within a GagSpeak KinkPlate.
/// </summary>
public class KinkPlate : DisposableMediatorSubscriberBase
{
    private readonly PlayerCharacterData _playerData;
    private readonly CosmeticService _cosmetics;

    // KinkPlate Data for User.
    private string _base64ProfilePicture;
    private Lazy<byte[]> _imageData;
    private IDalamudTextureWrap? _storedProfileImage;

    public KinkPlate(ILogger<KinkPlate> logger, GagspeakMediator mediator,
        PlayerCharacterData playerData, CosmeticService cosmeticService, 
        KinkPlateContent plateContent, string base64ProfilePicture) : base(logger, mediator)
    {
        _playerData = playerData;
        _cosmetics = cosmeticService;

        // Set the KinkPlate Data
        KinkPlateInfo = plateContent;
        Base64ProfilePicture = base64ProfilePicture;
        // set the image data if the profilePicture is not empty.
        if (!string.IsNullOrEmpty(Base64ProfilePicture))
        {
            _imageData = new Lazy<byte[]>(() => Convert.FromBase64String(Base64ProfilePicture));
        }
        else
        {
            _imageData = new Lazy<byte[]>(() => Array.Empty<byte>());
        }

        Mediator.Subscribe<ClearProfileDataMessage>(this, (msg) =>
        {
            if (msg.UserData == null || string.Equals(msg.UserData.UID, MainHub.UID, StringComparison.Ordinal))
            {
                _storedProfileImage?.Dispose();
                _storedProfileImage = null;
            }
        });
    }

    public KinkPlateContent KinkPlateInfo;
    public string Base64ProfilePicture
    {
        get => _base64ProfilePicture;
        set
        {
            if (_base64ProfilePicture != value)
            {
                _base64ProfilePicture = value;
                Logger.LogDebug("Profile picture updated.", LoggerType.Profiles);
                if(!string.IsNullOrEmpty(_base64ProfilePicture))
                {
                    Logger.LogTrace("Refreshing profile image data!", LoggerType.Profiles);
                    _imageData = new Lazy<byte[]>(() => ConvertBase64ToByteArray(Base64ProfilePicture));
                    Logger.LogTrace("Refreshed profile image data!", LoggerType.Profiles);
                }
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Logger.LogInformation("Disposing profile image data!");
            _storedProfileImage?.Dispose();
            _storedProfileImage = null;
        }
        base.Dispose(disposing);
    }

    public IDalamudTextureWrap? GetCurrentProfileOrDefault()
    {
        // If the user does not have a profile set, return the default logo.
        if(string.IsNullOrEmpty(Base64ProfilePicture) || _imageData.Value.IsNullOrEmpty())
            return _cosmetics.CorePluginTextures[CorePluginTexture.Logo256bg];

        // Otherwise, fetch the profile image for it.
        if(_storedProfileImage is not null)
            return _storedProfileImage;

        // load it
        try
        {
            Logger.LogTrace("Loading profile image data to wrap.");
            _storedProfileImage = _cosmetics.GetProfilePicture(_imageData.Value);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load profile image data to wrap.");
        }
        return _storedProfileImage;
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
