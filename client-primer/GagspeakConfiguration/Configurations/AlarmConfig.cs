using GagSpeak.GagspeakConfiguration.Models;

namespace GagSpeak.GagspeakConfiguration.Configurations;

public class AlarmConfig : IGagspeakConfiguration
{
    /// <summary> AliasList Storage per-paired user. </summary>
    public AlarmStorage AlarmStorage { get; set; }
    public static int CurrentVersion => 2;
    public int Version { get; set; } = CurrentVersion;
}
