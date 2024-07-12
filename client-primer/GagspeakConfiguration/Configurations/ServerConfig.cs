using GagSpeak.GagspeakConfiguration.Models;

namespace GagSpeak.GagspeakConfiguration.Configurations;

[Serializable]
public class ServerConfig : IGagspeakConfiguration
{
    /// <summary> The GagSpeak Server's server storage </summary>
    public ServerStorage ServerStorage { get; set; } = new()
    {
        ServerName = ApiController.MainServer,
        ServiceUri = ApiController.MainServiceUri,
    };

    public int Version { get; set; } = 1;
}
