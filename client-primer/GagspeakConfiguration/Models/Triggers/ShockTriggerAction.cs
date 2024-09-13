using GagspeakAPI.Data.VibeServer;

namespace GagSpeak.GagspeakConfiguration.Models;

[Serializable]
public record ShockTriggerAction
{
    public ShockMode OpCode { get; set; } = ShockMode.Beep; // OpCode (beep by default)
    public TimeSpan Duration { get; set; } = TimeSpan.Zero; // duration of action
    public int Intensity { get; set; } = 0; // intensity of action (0-100)
}

