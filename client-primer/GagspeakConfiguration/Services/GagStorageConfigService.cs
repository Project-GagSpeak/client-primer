using GagSpeak.GagspeakConfiguration.Configurations;
using GagSpeak.GagspeakConfiguration.Models;
using GagSpeak.PlayerData.Data;
using GagSpeak.Utils;
using GagspeakAPI.Data.Enum;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Penumbra.GameData.Enums;

namespace GagSpeak.GagspeakConfiguration;

// will probably change this in the future considering we use a different config storage approach in gagspeak
public class GagStorageConfigService : ConfigurationServiceBase<GagStorageConfig>
{
    public const string ConfigName = "gag-storage.json";
    public const bool PerCharacterConfig = true;
    public GagStorageConfigService(string configDir) : base(configDir) { }

    protected override string ConfigurationName => ConfigName;
    protected override bool PerCharacterConfigPath => PerCharacterConfig;

    protected override GagStorageConfig DeserializeConfig(JObject configJson)
    {
        GagStorageConfig config = new GagStorageConfig();

        // Assuming GagStorage has a default constructor
        config.GagStorage = new GagStorage();
        config.GagStorage.GagEquipData = new Dictionary<GagList.GagType, GagDrawData>();

        JObject gagEquipDataObject = configJson["GagStorage"]["GagEquipData"].Value<JObject>();
        if (gagEquipDataObject == null) return config;
        
        foreach (var gagData in gagEquipDataObject)
        {
            var gagType = (GagList.GagType)Enum.Parse(typeof(GagList.GagType), gagData.Key);
            if (gagData.Value is JObject itemObject)
            {
                string? slotString = itemObject["Slot"].Value<string>();
                EquipSlot slot = (EquipSlot)Enum.Parse(typeof(EquipSlot), slotString);
                var gagDrawData = new GagDrawData(ItemIdVars.NothingItem(slot)); // Assuming GagDrawData has a parameterless constructor
                gagDrawData.Deserialize(itemObject);
                config.GagStorage.GagEquipData.Add(gagType,gagDrawData);
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
