using GagSpeak.GagspeakConfiguration.Configurations;

namespace GagSpeak.GagspeakConfiguration;

// will probably change this in the future considering we use a different config storage approach in gagspeak
public class GagStorageConfigService : ConfigurationServiceBase<GagStorageConfig>
{
    public const string ConfigName = "gag-storage.json";

    public GagStorageConfigService(string configDir) : base(configDir) { }

    protected override string ConfigurationName => ConfigName;
}
