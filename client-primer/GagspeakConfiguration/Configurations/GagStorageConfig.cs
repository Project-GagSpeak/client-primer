using GagSpeak.GagspeakConfiguration.Models;

namespace GagSpeak.GagspeakConfiguration.Configurations;

[Serializable]
public class GagStorageConfig : IGagspeakConfiguration
{
    /// <summary> The GagSpeak Clients Pattern Stroage </summary>
    public GagStorage GagStorage { get; set; } = new();
    public int Version { get; set; } = 0;
}
