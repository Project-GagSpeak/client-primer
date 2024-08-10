using GagSpeak.GagspeakConfiguration.Configurations;
using GagSpeak.GagspeakConfiguration.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GagSpeak.GagspeakConfiguration;

public class WardrobeConfigService : ConfigurationServiceBase<WardrobeConfig>
{
    public const string ConfigName = "wardrobe.json";

    public WardrobeConfigService(string configDir) : base(configDir) { }

    protected override string ConfigurationName => ConfigName;

    protected override WardrobeConfig LoadConfig()
    {
        WardrobeConfig config = new WardrobeConfig();
        if (!File.Exists(ConfigurationPath))
        {
            Save();
            return config;
        }
        if (File.Exists(ConfigurationPath))
        {
            try
            {
                string json = File.ReadAllText(ConfigurationPath);
                JObject configObject = JObject.Parse(json);

                // Deserialize WardrobeStorage
                JToken wardrobeStorageToken = configObject["WardrobeStorage"];
                if (wardrobeStorageToken != null)
                {
                    WardrobeStorage wardrobeStorage = new WardrobeStorage();

                    var restraintSetsArray = wardrobeStorageToken["RestraintSets"]?.Value<JArray>();
                    if (restraintSetsArray == null)
                    {
                        throw new Exception("RestraintSets property is missing in RestraintSets.json");
                    }

                    // we are in version 1, so we should deserialize appropriately
                    foreach (var item in restraintSetsArray)
                    {
                        var restraintSet = new RestraintSet();
                        var ItemValue = item.Value<JObject>();
                        if (ItemValue != null)
                        {
                            restraintSet.Deserialize(ItemValue);
                            wardrobeStorage.RestraintSets.Add(restraintSet);
                        }
                        else
                        {
                            throw new Exception("restraint set contains invalid property");
                        }
                    }

                    // Assuming BlindfoldInfo follows a similar pattern
                    JToken blindfoldInfoToken = wardrobeStorageToken["BlindfoldInfo"];
                    if (blindfoldInfoToken != null)
                    {
                        BlindfoldModel blindfoldModel = new BlindfoldModel();
                        blindfoldModel.Deserialize((JObject)blindfoldInfoToken);
                        wardrobeStorage.BlindfoldInfo = blindfoldModel;
                    }
                    else
                    {
                        wardrobeStorage.BlindfoldInfo = new BlindfoldModel();
                    }
                    config.WardrobeStorage = wardrobeStorage; // loads the wardrobe storage into the stored config.
                }

                // Deserialize Version
                JToken versionToken = configObject["Version"];
                if (versionToken != null)
                {
                    config.Version = versionToken.ToObject<int>();
                }
                Save();
            }
            catch (Exception e)
            {
                throw new Exception($"Failed to load configuration file {ConfigurationPath} {e}");
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
        if (existingConfigs.Skip(1).Any())
        {
            foreach (var config in existingConfigs.Skip(1))
            {
                config.Delete();
            }
        }

        try
        {
            File.Copy(ConfigurationPath, ConfigurationPath + ".bak." + DateTime.Now.ToString("yyyyMMddHHmmss"), overwrite: true);
        }
        catch { /* Consume */ }

        try
        {
            // serialize here.
            JObject configObject = new JObject
            {
                ["Version"] = Current.Version,
                ["WardrobeStorage"] = new JObject()
            };

            JArray restraintSetsArray = new JArray();
            foreach (RestraintSet restraintSet in Current.WardrobeStorage.RestraintSets)
            {
                restraintSetsArray.Add(restraintSet.Serialize());
            }
            configObject["WardrobeStorage"]["RestraintSets"] = restraintSetsArray;

            // Use Serialize method for BlindfoldInfo
            JObject blindfoldInfoObject = Current.WardrobeStorage.BlindfoldInfo.Serialize();

            // Add blindfoldInfoObject to the WardrobeStorage JObject
            configObject["WardrobeStorage"]["BlindfoldInfo"] = blindfoldInfoObject;

            string json = configObject.ToString(Formatting.Indented);
            var tempPath = ConfigurationPath + ".tmp";
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, ConfigurationPath, true);
            _configLastWriteTime = new FileInfo(ConfigurationPath).LastWriteTimeUtc;
        }
        catch (Exception ex) 
        {
            throw new Exception($"Failed to save {ConfigurationName} configuration.", ex);
        }
    }
}
