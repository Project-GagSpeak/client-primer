using GagSpeak.GagspeakConfiguration.Configurations;

namespace GagSpeak.GagspeakConfiguration;

// will probably change this in the future considering we use a different config storage approach in gagspeak
public class TriggerConfigService : ConfigurationServiceBase<TriggerConfig>
{
    public const string ConfigName = "triggers.json";
    public const bool PerCharacterConfig = true;
    public TriggerConfigService(string configDir) : base(configDir) { }

    protected override string ConfigurationName => ConfigName;
    protected override bool PerCharacterConfigPath => PerCharacterConfig;

}
