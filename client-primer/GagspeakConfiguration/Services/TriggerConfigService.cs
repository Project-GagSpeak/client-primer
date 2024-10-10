using GagSpeak.GagspeakConfiguration.Configurations;
using GagSpeak.GagspeakConfiguration.Models;
using GagspeakAPI.Enums;

namespace GagSpeak.GagspeakConfiguration;

public class TriggerConfigService : ConfigurationServiceBase<TriggerConfig>
{
    public const string ConfigName = "triggers.json";
    public const bool PerCharacterConfig = true;
    public TriggerConfigService(string configDir) : base(configDir) { }

    protected override string ConfigurationName => ConfigName;
    protected override bool PerCharacterConfigPath => PerCharacterConfig;

    protected override JObject MigrateConfig(JObject oldConfigJson, int readVersion)
    {
        JObject newConfigJson = oldConfigJson;
        // if migrating from any version less than 2, to 2
        if (readVersion <= 2)
        {
            newConfigJson = MigrateFromV1toV2(oldConfigJson);
        }
        else
        {
            newConfigJson = oldConfigJson;
        }
        return newConfigJson;
    }

    private JObject MigrateFromV1toV2(JObject oldConfigJson)
    {
        // Create a new JObject for V3
        var v2Data = new JObject
        {
            ["Version"] = 2,
            ["TriggerStorage"] = new JObject()
        };

        // Get the V2 WardrobeStorage and RestraintSets
        var v2TriggersStorage = (JObject)oldConfigJson["TriggerStorage"];
        var v2Triggers = (JArray)v2TriggersStorage["Triggers"];
        var v2TriggersNew = new JArray();

        foreach (var v2Trigger in v2Triggers)
        {
            // dont need anything in here yet, but still good to implement the logic for detmining what to do with the abstract type triggers.
            v2TriggersNew.Add(v2Trigger);
        }
        v2Data["TriggerStorage"]["Triggers"] = v2Triggers;

        return v2Data;
    }


    protected override TriggerConfig DeserializeConfig(JObject configJson)
    {
        var config = new TriggerConfig();
        // Deserialize WardrobeStorage
        JToken triggerStorageToken = configJson["TriggerStorage"];
        if (triggerStorageToken != null)
        {
            TriggerStorage triggerStorage = new TriggerStorage();

            var triggerArray = triggerStorageToken["Triggers"]?.Value<JArray>();
            if (triggerArray == null)
            {
                StaticLogger.Logger.LogWarning("RestraintSets property is missing in RestraintSets.json");
                throw new Exception("RestraintSets property is missing in RestraintSets.json");
            }

            // we are in version 1, so we should deserialize appropriately
            foreach (var triggerToken in triggerArray)
            {
                // we need to obtain the information from the triggers "Type" property to know which kind of trigger to create, as we cant create an abstract "Trigger".
                if (Enum.TryParse(triggerToken["Type"]?.ToString(), out TriggerKind triggerType))
                {
                    Trigger triggerAbstract;
                    switch (triggerType)
                    {
                        case TriggerKind.Chat:
                            triggerAbstract = triggerToken.ToObject<ChatTrigger>()!;
                            break;
                        case TriggerKind.SpellAction:
                            triggerAbstract = triggerToken.ToObject<SpellActionTrigger>()!;
                            break;
                        case TriggerKind.HealthPercent:
                            triggerAbstract = triggerToken.ToObject<HealthPercentTrigger>()!;
                            break;
                        case TriggerKind.RestraintSet:
                            triggerAbstract = triggerToken.ToObject<RestraintTrigger>()!;
                            break;
                        case TriggerKind.GagState:
                            triggerAbstract = triggerToken.ToObject<GagTrigger>()!;
                            break;
                        case TriggerKind.SocialAction:
                            triggerAbstract = triggerToken.ToObject<SocialTrigger>()!;
                            break;
                        // could add a social feed trigger type here.
                        default:
                            throw new Exception("Invalid Trigger Type");
                    }
                    triggerStorage.Triggers.Add(triggerAbstract);
                }
                else
                {
                    throw new Exception("Invalid Trigger Type");
                }
            }

            config.TriggerStorage = triggerStorage;
        }
        return config;
    }
}
