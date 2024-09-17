using GagSpeak.GagspeakConfiguration.Configurations;
using GagSpeak.GagspeakConfiguration.Models;
using GagSpeak.Utils;
using GagspeakAPI.Data.Enum;
using Penumbra.GameData.Enums;

namespace GagSpeak.GagspeakConfiguration;

// will probably change this in the future considering we use a different config storage approach in gagspeak
public class GagStorageConfigService : ConfigurationServiceBase<GagStorageConfig>
{
    private readonly ItemIdVars _itemHelper;

    public const string ConfigName = "gag-storage.json";
    public const bool PerCharacterConfig = true;
    public GagStorageConfigService(ItemIdVars itemHelper, string configDir) : base(configDir) { _itemHelper = itemHelper; }

    protected override string ConfigurationName => ConfigName;
    protected override bool PerCharacterConfigPath => PerCharacterConfig;

    protected override JObject MigrateConfig(JObject oldConfigJson, int readVersion)
    {
        JObject newConfigJson = oldConfigJson;
        if (readVersion < 2)
        {
            newConfigJson = MigrateFromV1toV2(oldConfigJson);
        }
        else
        {
            newConfigJson = oldConfigJson;
        }
        return newConfigJson;
    }

    public JObject MigrateFromV1toV2(JObject v1Data)
    {
        // Create a new JObject for V2
        var v2Data = new JObject
        {
            ["Version"] = 2,
            ["GagStorage"] = new JObject
            {
                ["GagEquipData"] = new JObject()
            }
        };

        // Get the V1 GagEquipData
        JObject v1GagEquipData;
        v1GagEquipData = (JObject)v1Data["GagStorage"]["GagEquipData"];

        var v2GagEquipData = new JObject();

        foreach (var item in v1GagEquipData.Properties())
        {
            try
            {
                var gagName = item.Name;
                var v1Gag = (JObject)item.Value;

                // Corrected path to access GameItem
                var gameItem = v1Gag["GameItem"] as JObject;
                if (gameItem == null)
                {
                    StaticLogger.Logger.LogError($"gameItem is null for gagName: {gagName}. v1Gag contents: {v1Gag.ToString(Formatting.Indented)}");
                    throw new Exception($"gameItem is null for gagName: {gagName}");
                }

                var v2Gag = new JObject
                {
                    ["IsEnabled"] = v1Gag["IsEnabled"],
                    ["ForceHeadgearOnEnable"] = false,
                    ["ForceVisorOnEnable"] = false,
                    ["GagMoodles"] = new JArray(),
                    ["Slot"] = v1Gag["Slot"],
                    ["CustomItemId"] = gameItem["Id"].ToString(),
                    ["GameStain"] = v1Gag["GameStain"]
                };

                v2GagEquipData[gagName] = v2Gag;
            }
            catch (AggregateException ex)
            {
                throw new AggregateException($"Failed to migrate gag item '{item.Name}'.", ex);
            }
        }

        // Assign the transformed GagEquipData to V2 data
        v2Data["GagStorage"]["GagEquipData"] = v2GagEquipData;

        return v2Data;
    }

    protected override GagStorageConfig DeserializeConfig(JObject configJson)
    {
        GagStorageConfig config = new GagStorageConfig();

        // Assuming GagStorage has a default constructor
        config.GagStorage = new GagStorage();
        config.GagStorage.GagEquipData = new Dictionary<GagType, GagDrawData>();

        JObject gagEquipDataObject = configJson["GagStorage"]["GagEquipData"].Value<JObject>();
        if (gagEquipDataObject == null) return config;

        foreach (var gagData in gagEquipDataObject)
        {
            var gagType = (GagType)Enum.Parse(typeof(GagType), gagData.Key);
            if (gagData.Value is JObject itemObject)
            {
                string? slotString = itemObject["Slot"].Value<string>();
                EquipSlot slot = (EquipSlot)Enum.Parse(typeof(EquipSlot), slotString);
                var gagDrawData = new GagDrawData(_itemHelper, ItemIdVars.NothingItem(slot));
                gagDrawData.Deserialize(itemObject);
                config.GagStorage.GagEquipData.Add(gagType, gagDrawData);
            }
        }
        return config;
    }

    protected override string SerializeConfig(GagStorageConfig config)
    {
        JObject configObject = new JObject()
        {
            ["Version"] = config.Version // Include the version of GagStorageConfig
        };
        JObject gagEquipDataObject = new JObject();

        foreach (var kvp in config.GagStorage.GagEquipData)
        {
            gagEquipDataObject[kvp.Key.ToString()] = kvp.Value.Serialize();
        }

        configObject["GagStorage"] = new JObject
        {
            ["GagEquipData"] = gagEquipDataObject
        };

        return configObject.ToString(Formatting.Indented);
    }
}
