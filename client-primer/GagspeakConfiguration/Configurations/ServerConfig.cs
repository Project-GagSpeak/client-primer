using GagSpeak.GagspeakConfiguration.Models;
using GagSpeak.WebAPI;
using Newtonsoft.Json.Linq;

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

    public static int CurrentVersion => 2;
    public int Version { get; set; } = CurrentVersion;
}
