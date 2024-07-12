using FFStreamViewer.WebAPI.GagspeakConfiguration.Models;

namespace FFStreamViewer.WebAPI.GagspeakConfiguration.Configurations;

[Serializable]
public class AliasConfig : IGagspeakConfiguration
{
    /// <summary> The GagSpeak Server's server storage </summary>
    public Dictionary<string, AliasStorage> AliasStorage { get; set; } = new();
    public int Version { get; set; } = 0;
}
