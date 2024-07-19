using GagSpeak.GagspeakConfiguration.Models;

namespace GagSpeak.GagspeakConfiguration.Configurations;

public class AliasConfig : IGagspeakConfiguration
{
    /// <summary> AliasList Storage per-paired user. </summary>
    public Dictionary<string, AliasStorage> AliasStorage { get; set; }
    public int Version { get; set; } = 0;
}
