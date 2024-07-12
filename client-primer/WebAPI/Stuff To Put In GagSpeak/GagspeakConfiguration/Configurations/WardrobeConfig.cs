using FFStreamViewer.WebAPI.GagspeakConfiguration.Models;

namespace FFStreamViewer.WebAPI.GagspeakConfiguration.Configurations;

[Serializable]
public class WardrobeConfig : IGagspeakConfiguration
{
    /// <summary> The GagSpeak Clients Pattern Stroage </summary>
    public WardrobeStorage WardrobeStorage { get; set; } = new();
    public int Version { get; set; } = 1;
}
