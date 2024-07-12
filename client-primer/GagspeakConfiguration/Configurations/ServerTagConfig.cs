using GagSpeak.GagspeakConfiguration.Models;

namespace GagSpeak.GagspeakConfiguration.Configurations;

public class ServerTagConfig : IGagspeakConfiguration
{
    public ServerTagStorage ServerTagStorage { get; set; }
    public int Version { get; set; } = 0;
}
