using FFStreamViewer.WebAPI.GagspeakConfiguration.Configurations;

namespace FFStreamViewer.WebAPI.GagspeakConfiguration;

public class WardrobeConfigService : ConfigurationServiceBase<WardrobeConfig>
{
    public const string ConfigName = "wardrobe.json";

    public WardrobeConfigService(string configDir) : base(configDir) { }

    protected override string ConfigurationName => ConfigName;
}
