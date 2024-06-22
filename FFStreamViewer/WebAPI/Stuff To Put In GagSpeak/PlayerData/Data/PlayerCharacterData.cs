using Gagspeak.API.Data;
using Gagspeak.API.Data.CharacterData;
using Gagspeak.API.Data.Enum;

namespace FFStreamViewer.WebAPI.PlayerData.Data;

// unsure atm why we would need this, but we will find out soon.
public class PlayerCharacterData
{
    public CharaGeneralConfig _generalConfig { get; set; } = new CharaGeneralConfig();
    public CharaAppearanceConfig _appearanceConfig { get; set; } = new CharaAppearanceConfig();
    /// <summary>
    /// The paired user specific settings.
    /// <para>Key: The paired user's UID</para>
    /// <para>Value: The paired user's specific settings for that UID</para>
    /// </summary>
    public Dictionary<string, CharaPairSpecificConfig> _pairSpecificConfig { get; set; } = new Dictionary<string, CharaPairSpecificConfig>();

    // Moodles data is stored as a string, but is a JSON object
    public string _moodlesData { get; set; } = string.Empty;

    public CharacterData ToAPI()
    {
        return new CharacterData()
        {
            GeneralConfig = _generalConfig,
            AppearanceConfig = _appearanceConfig,
            PairSpecificConfig = _pairSpecificConfig,
            MoodlesData = _moodlesData
        };
    }
}
