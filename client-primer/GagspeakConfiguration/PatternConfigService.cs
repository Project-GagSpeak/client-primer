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
    public const bool PerCharacterConfig = false;
    public PatternConfigService(string configDir) : base(configDir) { }

    protected override bool PerCharacterConfigPath => PerCharacterConfig;
    protected override string ConfigurationName => ConfigName;

    protected override PatternConfig DeserializeConfig(JObject configJson)
    {
        PatternConfig config = new PatternConfig();

        // create a new Pattern Storage object
        config.PatternStorage = new PatternStorage();
        // create a new list of patternData inside it
        config.PatternStorage.Patterns = new List<PatternData>();
                
        // read in the pattern data from the config file
        var PatternsList = configJson["PatternStorage"]["Patterns"].Value<JArray>();
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
        return config;
    }

    protected override string SerializeConfig(PatternConfig config)
    {
        JObject configObject = new JObject()
        {
            ["Version"] = config.Version
        };

        // create the array to write to
        JArray patternsArray = new JArray();
        // for each of the patterns in the pattern storage
        foreach (PatternData pattern in config.PatternStorage.Patterns)
        {
            // add the serialized pattern to the array
            patternsArray.Add(pattern.Serialize());
        }

        // add the patterns array to the config object
        configObject["PatternStorage"] = new JObject
        {
            ["Patterns"] = patternsArray
        };

        return configObject.ToString(Formatting.Indented);
    }
}
