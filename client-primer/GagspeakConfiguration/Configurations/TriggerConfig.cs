using GagSpeak.GagspeakConfiguration.Models;

namespace GagSpeak.GagspeakConfiguration.Configurations;

public class TriggerConfig : IGagspeakConfiguration
{
    /// <summary> The Trigger Storage for the toybox. </summary>
    public TriggerStorage TriggerStorage { get; set; }
    public static int CurrentVersion => 2;
    public int Version { get; set; } = CurrentVersion;
}
