using GagSpeak.GagspeakConfiguration.Models;

namespace GagSpeak.GagspeakConfiguration.Configurations;

public class AlarmConfig : IGagspeakConfiguration
{
    /// <summary> AliasList Storage per-paired user. </summary>
    public AlarmStorage AlarmStorage { get; set; }
    public int Version { get; set; } = 0;
}
