using GagSpeak.GagspeakConfiguration.Models;

namespace GagSpeak.GagspeakConfiguration.Configurations;

public class WardrobeConfig : IGagspeakConfiguration
{
    /// <summary> The GagSpeak Clients Pattern Stroage </summary>
    public WardrobeStorage WardrobeStorage { get; set; }
    public int Version { get; set; } = 1;
}
