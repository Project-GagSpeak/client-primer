using GagSpeak.GagspeakConfiguration.Configurations;

namespace GagSpeak.GagspeakConfiguration;

public class AlarmConfigService : ConfigurationServiceBase<AlarmConfig>
{
    public const string ConfigName = "alarms.json";
    public const bool PerCharacterConfig = true;

    public AlarmConfigService(string configDir) : base(configDir) { }

    protected override string ConfigurationName => ConfigName;
    protected override bool PerCharacterConfigPath => PerCharacterConfig;

    // apply an override for migrations off the baseconfigservice
    protected override JObject MigrateConfig(JObject oldConfigJson, int readVersion)
    {
        JObject newConfigJson;

        // if migrating from any version less than 2, to 2
        if (readVersion <= 2)
        {
            newConfigJson = MigrateFromV1toV2(oldConfigJson);
        }
        else
        {
            // no migration needed
            newConfigJson = oldConfigJson;
        }
        return newConfigJson;
    }

    // Safely update data for new format.
    private JObject MigrateFromV1toV2(JObject oldConfigJson)
    {
        // create a new JObject to store the new config
        JObject newConfigJson = new();
        // set the version to 2
        newConfigJson["Version"] = 2;

        // completely erase the old alarm storage, and replace with a new one.
        newConfigJson["AlarmStorage"] = new JObject
        {
            ["Alarms"] = new JArray()
        };

        return newConfigJson;
    }
}
