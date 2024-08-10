using GagSpeak.GagspeakConfiguration.Configurations;
using GagSpeak.GagspeakConfiguration.Models;
using GagSpeak.Utils;
using GagspeakAPI.Data.Enum;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Penumbra.GameData.Enums;

namespace GagSpeak.GagspeakConfiguration;

// will probably change this in the future considering we use a different config storage approach in gagspeak
public class PatternConfigService : ConfigurationServiceBase<PatternConfig>
{
    public const string ConfigName = "patterns.json";

    public PatternConfigService(string configDir) : base(configDir) { }

    protected override string ConfigurationName => ConfigName;

    protected override PatternConfig LoadConfig()
    {
        PatternConfig config = new PatternConfig();
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

                // Check for Version property
                JToken versionToken = configObject["Version"];
                if (versionToken == null)
                {
                    Save();
                    return config!;
                }

                if (versionToken.ToObject<int>() != config.Version)
                {
                    // Version mismatch or missing, remove associated files
                    var gagStorageFiles = Directory.EnumerateFiles(ConfigurationDirectory, "patterns.json");
                    // if migrating from any version less than 2, to 2
                    if (versionToken.ToObject<int>() < 2)
                    {
                        // perform a full reset
                        foreach (var file in gagStorageFiles)
                        {
                            File.Delete(file);
                        }
                        Save();
                        return config;
                    }
                }

                // create a new Pattern Storage object
                config.PatternStorage = new PatternStorage();
                // create a new list of patternData inside it
                config.PatternStorage.Patterns = new List<PatternData>();
                
                // read in the pattern data from the config file
                var PatternsList = configObject["PatternStorage"]["Patterns"].Value<JArray>();
                // if the patterns list had data
                if(PatternsList != null)
                {
                    // then for each pattern in the list
                    foreach (var pattern in PatternsList)
                    {
                        // create a new pattern object
                        var patternData = new PatternData();
                        // deserialize the object
                        patternData.Deserialize(pattern.Value<JObject>());
                        // add the pattern to the list
                        config.PatternStorage.Patterns.Add(patternData);
                    }
                }
                // save the config with the update pattern storage data.
                Save();
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
        catch {  /* Consume */}

        JObject configObject = new JObject()
        {
            ["Version"] = Current.Version // Include the version of PatternStorage
        };

        // create the array to write to
        JArray patternsArray = new JArray();
        // for each of the patterns in the pattern storage
        foreach (PatternData pattern in Current.PatternStorage.Patterns)
        {
            // add the serialized pattern to the array
            patternsArray.Add(pattern.Serialize());
        }

        // add the patterns array to the config object
        configObject["PatternStorage"] = new JObject
        {
            ["Patterns"] = patternsArray
        };

        string json = configObject.ToString(Formatting.Indented);
        var tempPath = ConfigurationPath + ".tmp";
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, ConfigurationPath, true);
        _configLastWriteTime = new FileInfo(ConfigurationPath).LastWriteTimeUtc;
    }
}
