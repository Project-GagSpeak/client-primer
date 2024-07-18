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

    public GagStorageConfigService(string configDir) : base(configDir) { }

    protected override string ConfigurationName => ConfigName;

    protected override GagStorageConfig LoadConfig()
    {
        GagStorageConfig config = new GagStorageConfig();
        if (File.Exists(ConfigurationPath))
        {
            try
            {
                string json = File.ReadAllText(ConfigurationPath);
                JObject configObject = JObject.Parse(json);

                // Assuming GagStorage has a default constructor
                config.GagStorage = new GagStorage();
                config.GagStorage.GagEquipData = new Dictionary<GagList.GagType, GagDrawData>();

                JObject gagEquipDataObject = configObject["GagStorage"]["GagEquipData"].Value<JObject>();
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
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to load {ConfigurationName} configuration.", ex);
            }
        }

        _configLastWriteTime = GetConfigLastWriteTime();
        return config;
    }

    protected override void SaveDirtyConfig()
    {
        _configIsDirty = false;
        var existingConfigs = Directory.EnumerateFiles(ConfigurationDirectory, ConfigurationName + ".bak.*").Select(c => new FileInfo(c))
            .OrderByDescending(c => c.LastWriteTime).ToList();
        if (existingConfigs.Skip(5).Any())
        {
            foreach (var config in existingConfigs.Skip(5))
            {
                config.Delete();
            }
        }

        try
        {
            File.Copy(ConfigurationPath, ConfigurationPath + ".bak." + DateTime.Now.ToString("yyyyMMddHHmmss"), overwrite: true);
        }
        catch
        {
            // Log or handle the error as needed
        }

        JObject configObject = new JObject();
        JObject gagEquipDataObject = new JObject();

        foreach (var kvp in Current.GagStorage.GagEquipData)
        {
            gagEquipDataObject[kvp.Key.ToString()] = kvp.Value.Serialize();
        }

        configObject["GagStorage"] = new JObject
        {
            ["GagEquipData"] = gagEquipDataObject
        };

        string json = configObject.ToString(Formatting.Indented);
        var tempPath = ConfigurationPath + ".tmp";
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, ConfigurationPath, true);
        _configLastWriteTime = new FileInfo(ConfigurationPath).LastWriteTimeUtc;
    }
}
