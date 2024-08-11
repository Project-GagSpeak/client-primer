namespace GagSpeak.GagspeakConfiguration.Models;

[Serializable]
public class SecretKey
{
    public string Label { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
}
