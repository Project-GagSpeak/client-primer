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
        // Create a new JObject for V2, including TriggerStorage initialization
        var v2TriggerStorage = new JObject(); // Create the TriggerStorage here
        var v2Data = new JObject
        {
            ["Version"] = 2,
            ["TriggerStorage"] = v2TriggerStorage // Assign it directly
        };

        // Try to get the old TriggerStorage and Triggers, or default to empty JObject/JArray if missing
        var v2TriggersStorage = oldConfigJson["TriggerStorage"] as JObject ?? new JObject();
        var v2Triggers = v2TriggersStorage["Triggers"] as JArray ?? new JArray();

        // Create a new array to hold the migrated triggers (you can add migration logic here if needed)
        var v2TriggersNew = new JArray(v2Triggers);

        // Assign the new triggers array to the pre-created TriggerStorage
        v2TriggerStorage["Triggers"] = v2TriggersNew;

        return v2Data;
    }



    protected override TriggerConfig DeserializeConfig(JObject configJson)
    {
        var config = new TriggerConfig();
        // Deserialize WardrobeStorage
        JToken triggerStorageToken = configJson["TriggerStorage"]!;
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
