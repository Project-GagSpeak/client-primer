using GagSpeak.ChatMessages;
using GagspeakAPI.Data.Enum;
using GagSpeak.Toybox.Models;

namespace GagSpeak.GagspeakConfiguration.Models;

[Serializable]
public record MotorAction
{
    // the type of action being executed
    public VibrationExecutionType ExecutionType { get; set; } = VibrationExecutionType.Vibration;

    // the vibrationThreshold to execute, or the pattern name to play
    public string Action { get; set; } = string.Empty;
}
