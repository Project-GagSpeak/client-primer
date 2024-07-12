using GagSpeak.GagspeakConfiguration.Configurations;

namespace GagSpeak.GagspeakConfiguration;

public class WardrobeConfigService : ConfigurationServiceBase<WardrobeConfig>
{
    public const string ConfigName = "wardrobe.json";

    public WardrobeConfigService(string configDir) : base(configDir) { }

    protected override string ConfigurationName => ConfigName;
}
