using GagSpeak.ChatMessages;
using GagspeakAPI.Data.Enum;
using GagspeakAPI.Data.VibeServer;

namespace GagSpeak.GagspeakConfiguration.Models;

[Serializable]
public record DeviceAction
{
    // the device being linked to this action
    // This can be switched to another device if the user downloading it has
    // a device that supports all the same required vibration types.
    public string DeviceName { get; set; } = string.Empty;

    // the required vibration type the device must have
    public List<VibrateType> RequiredVibratorTypes { get; set; } = [];

    // what kind of action to execute on each motor.
    // the requirements the device must have
    public Dictionary<int, MotorAction> MotorActions { get; set; } = new Dictionary<int, MotorAction>();
}
