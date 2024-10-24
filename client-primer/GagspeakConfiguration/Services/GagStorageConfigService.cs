using GagSpeak.GagspeakConfiguration.Configurations;
using GagSpeak.GagspeakConfiguration.Models;
using GagSpeak.Utils;
using GagspeakAPI.Extensions;
using Penumbra.GameData.Enums;

namespace GagSpeak.GagspeakConfiguration;

public class GagStorageConfigService : ConfigurationServiceBase<GagStorageConfig>
{
    public const string ConfigName = "gag-storage.json";
    public const bool PerCharacterConfig = true;
    public GagStorageConfigService(string configDir) : base(configDir) { }

    protected override string ConfigurationName => ConfigName;
    protected override bool PerCharacterConfigPath => PerCharacterConfig;

    protected override JObject MigrateConfig(JObject oldConfigJson, int readVersion)
    {
        JObject newConfigJson = oldConfigJson;
        if (readVersion == 1)
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
        var v1GagEquipData = v1Data["GagStorage"]?["GagEquipData"] as JObject ?? new JObject();

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
                    ["CustomItemId"] = gameItem["Id"]?.ToString(),
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
        v2Data["GagStorage"]!["GagEquipData"] = v2GagEquipData;

        return v2Data;
    }

    protected override GagStorageConfig DeserializeConfig(JObject configJson)
    {
        GagStorageConfig config = new GagStorageConfig();

        // Assuming GagStorage has a default constructor
        config.GagStorage = new GagStorage();
        config.GagStorage.GagEquipData = new Dictionary<GagType, GagDrawData>();

        JObject gagEquipDataObject = configJson["GagStorage"]!["GagEquipData"] as JObject ?? new JObject();
        if (gagEquipDataObject == null) return config;

        int i = 0;
        foreach (var gagData in gagEquipDataObject)
        {
            GagType gagType;
            if (gagData.Key.IsValidGagName())
            {
                gagType = Enum.GetValues(typeof(GagType))
                    .Cast<GagType>()
                    .FirstOrDefault(gt => gt.GagName() == gagData.Key);
            }
            else
            {
                gagType = Enum.GetValues(typeof(GagType))
                    .Cast<GagType>()
                    .FirstOrDefault(gt => gt.ToString() == gagData.Key);
                if (gagType == default)
                {
                    if (gagData.Key == "WiffleGag") gagType = GagType.WhiffleGag;
                    if (gagData.Key == "TenticleGag") gagType = GagType.TentacleGag;
                }
            }

            if (gagData.Value is JObject itemObject)
            {
                string? slotString = itemObject["Slot"]?.Value<string>() ?? "Head";
                EquipSlot slot = (EquipSlot)Enum.Parse(typeof(EquipSlot), slotString);
                var gagDrawData = new GagDrawData(ItemIdVars.NothingItem(slot));
                gagDrawData.Deserialize(itemObject);
                config.GagStorage.GagEquipData.Add(gagType, gagDrawData);
                i++;
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
            gagEquipDataObject[kvp.Key.GagName()] = kvp.Value.Serialize();
        }

        configObject["GagStorage"] = new JObject
        {
            ["GagEquipData"] = gagEquipDataObject
        };

        return configObject.ToString(Formatting.Indented);
    }
}
