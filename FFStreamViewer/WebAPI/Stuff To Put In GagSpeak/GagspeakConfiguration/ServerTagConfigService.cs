using FFStreamViewer.WebAPI.GagspeakConfiguration.Configurations;

namespace FFStreamViewer.WebAPI.GagspeakConfiguration;
public class ServerTagConfigService : ConfigurationServiceBase<ServerTagConfig>
{
    public const string ConfigName = "servertags.json";

    public ServerTagConfigService(string configDir) : base(configDir)
    {
    }

    protected override string ConfigurationName => ConfigName;
}
