namespace FFStreamViewer.WebAPI.GagspeakConfiguration.Models;

/// <summary>
/// A basic authentication class to validate that the information from the client when they attempt to connect is correct.
/// </summary>
[Serializable]
public record PatternData
{
    /// <summary> The name of the pattern </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary> The description of the pattern </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary> The duration of the pattern </summary>
    public string Duration { get; set; } = string.Empty;

    /// <summary> If the pattern is selected </summary>
    public bool Selected { get; set; } = false;

    /// <summary> If the pattern is active </summary>
    public bool IsActive { get; set; } = false;

    /// <summary> If the pattern should loop </summary>
    public bool ShouldLoop { get; set; } = false;

    /// <summary> The list of allowed users who can view this pattern </summary>
    public List<string> AllowedUsers { get; set; } = new();

    /// <summary> The pattern byte data </summary>
    public List<byte> PatternByteData { get; set; } = new();
}
