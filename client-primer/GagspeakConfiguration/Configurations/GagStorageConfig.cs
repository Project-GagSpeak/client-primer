using GagSpeak.GagspeakConfiguration.Models;

namespace GagSpeak.GagspeakConfiguration.Configurations;

public class GagStorageConfig : IGagspeakConfiguration
{
    public GagStorage GagStorage { get; set; }
    public int Version { get; set; } = 0;
}
