using GagSpeak.GagspeakConfiguration.Configurations;

namespace GagSpeak.GagspeakConfiguration;

public class NicknamesConfigService : ConfigurationServiceBase<UidNicknamesConfig>
{
    public const string ConfigName = "nicknames.json";

    public NicknamesConfigService(string configDir) : base(configDir)
    {
    }

    protected override string ConfigurationName => ConfigName;
}