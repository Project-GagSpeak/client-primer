namespace GagSpeak.Services;

/// <summary>
/// Record for the GagspeakProfileData, separate from the API, this allows for a supporter picture as well.
/// </summary>
/// <param name="IsFlagged"></param>
/// <param name="IsNSFW"></param>
/// <param name="Base64ProfilePicture"></param>
/// <param name="Base64SupporterPicture"></param>
/// <param name="Description"></param>
public record GagspeakProfileData(bool IsFlagged, string Base64ProfilePicture, string Base64SupporterPicture, string Description)
{
    public Lazy<byte[]> ImageData { get; } = new Lazy<byte[]>(Convert.FromBase64String(Base64ProfilePicture));
    public Lazy<byte[]> SupporterImageData { get; } = new Lazy<byte[]>(
                            string.IsNullOrEmpty(Base64SupporterPicture) ? [] : Convert.FromBase64String(Base64SupporterPicture));
}
