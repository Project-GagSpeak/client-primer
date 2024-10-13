using GagSpeak.GagspeakConfiguration.Configurations;

namespace GagSpeak.GagspeakConfiguration;
public class GagspeakConfigService : ConfigurationServiceBase<GagspeakConfig>
{
    public const string ConfigName = "config-testing.json";
    public const bool PerCharacterConfig = false;
    public GagspeakConfigService(string configDir) : base(configDir) { }

    protected override string ConfigurationName => ConfigName;
    protected override bool PerCharacterConfigPath => PerCharacterConfig;

    // apply an override for migrations off the baseconfigservice
    protected override JObject MigrateConfig(JObject oldConfigJson, int readVersion)
    {
        JObject newConfigJson;
        // if migrating from any version less than 2, to 2
        if (readVersion <= 3)
        {
            newConfigJson = oldConfigJson;
            newConfigJson["LoggerFilters"] = new JArray();
        }
        else
        {
            newConfigJson = oldConfigJson;
        }

        return newConfigJson;
    }
}
