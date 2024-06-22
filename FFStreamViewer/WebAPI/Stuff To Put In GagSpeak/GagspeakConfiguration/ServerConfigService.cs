using FFStreamViewer.WebAPI.GagspeakConfiguration.Configurations;

namespace FFStreamViewer.WebAPI.GagspeakConfiguration;

public class ServerConfigService : ConfigurationServiceBase<ServerConfig>
{
    public const string ConfigName = "server.json";

    public ServerConfigService(string configDir) : base(configDir)
    {
    }

    protected override string ConfigurationName => ConfigName;
}
