using GagSpeak.GagspeakConfiguration.Configurations;

namespace GagSpeak.GagspeakConfiguration;
// will probably change this in the future considering we use a different config storage approach in gagspeak
public class GagspeakConfigService : ConfigurationServiceBase<GagspeakConfig>
{
    public const string ConfigName = "config-testing.json";
    public const bool PerCharacterConfig = false;
    public GagspeakConfigService(string configDir) : base(configDir) { }

    protected override string ConfigurationName => ConfigName;
    protected override bool PerCharacterConfigPath => PerCharacterConfig;

}
