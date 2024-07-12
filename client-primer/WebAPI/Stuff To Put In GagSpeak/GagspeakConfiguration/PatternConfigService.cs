using FFStreamViewer.WebAPI.GagspeakConfiguration.Configurations;

namespace FFStreamViewer.WebAPI.GagspeakConfiguration;

// will probably change this in the future considering we use a different config storage approach in gagspeak
public class PatternConfigService : ConfigurationServiceBase<PatternConfig>
{
    public const string ConfigName = "patterns.json";

    public PatternConfigService(string configDir) : base(configDir) { }

    protected override string ConfigurationName => ConfigName;
}
