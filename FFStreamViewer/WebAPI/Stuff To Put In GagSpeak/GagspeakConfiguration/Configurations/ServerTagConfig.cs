using FFStreamViewer.WebAPI.GagspeakConfiguration.Models;

namespace FFStreamViewer.WebAPI.GagspeakConfiguration.Configurations;

public class ServerTagConfig : IGagspeakConfiguration
{
    public ServerTagStorage ServerTagStorage { get; set; }
    public int Version { get; set; } = 0;
}
