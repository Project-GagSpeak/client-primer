using GagSpeak.GagspeakConfiguration.Models;

namespace GagSpeak.GagspeakConfiguration.Configurations;

public class AliasConfig : IGagspeakConfiguration
{
    /// <summary> AliasList Storage per-paired user. </summary>
    public Dictionary<string, AliasStorage> AliasStorage { get; set; }
    public static int CurrentVersion => 1;
    public int Version { get; set; } = CurrentVersion;
}
