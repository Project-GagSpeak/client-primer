namespace GagSpeak.Services;

/// <summary>
/// Record for the GagspeakProfileData, separate from the API, this allows for a supporter picture as well.
/// </summary>
/// <param name="IsFlagged"></param>
/// <param name="Base64ProfilePicture"></param>
/// <param name="Base64SupporterPicture"></param>
/// <param name="Description"></param>
public record GagspeakProfileData(
    bool IsFlagged,
    string Base64ProfilePicture,
    string Base64SupporterPicture, 
    string Description)
{
    public Lazy<byte[]> ProfilePicData { get; } = new Lazy<byte[]>(Convert.FromBase64String(Base64ProfilePicture));

    // see later if more benificial to store the cosmetic images here or on file since they are predetermined and not custom
}
