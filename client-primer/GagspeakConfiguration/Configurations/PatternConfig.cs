using GagSpeak.GagspeakConfiguration.Models;

namespace GagSpeak.GagspeakConfiguration.Configurations;

[Serializable]
public class PatternConfig : IGagspeakConfiguration
{
    /// <summary> The GagSpeak Clients Pattern Stroage </summary>
    public PatternStorage PatternStorage { get; set; } = new();
    public int Version { get; set; } = 1;
}
