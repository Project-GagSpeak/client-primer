using GagspeakAPI.Data.VibeServer;

namespace GagSpeak.GagspeakConfiguration.Models;

/// <summary>
/// A basic authentication class to validate that the information from the client when they attempt to connect is correct.
/// </summary>
[Serializable]
public abstract record Trigger
{
    // required attributes
    public Guid TriggerIdentifier { get; set; } = Guid.NewGuid();
    public bool Enabled { get; set; } = false;
    public int Priority { get; set; } = 0;

    // generic attributes
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public TimeSpan StartAfter { get; set; } = TimeSpan.Zero;
    public TimeSpan EndAfter { get; set; } = TimeSpan.Zero;

    // Define which kind of trigger it is
    public abstract TriggerKind Type { get; }

    // List of UID's that are able to Enable/Interact with this trigger.
    public List<string> CanToggleTrigger { get; set; } = [];

    /*
     * Here is where we define the kind of execution the trigger will have to the connected devices.
     * 
     * Instead of requiring certain types of connected devices, we will instead allow the user to define
     * Which kind of Generic Vibrator Attribute it is using, and which kind of motor
     */
    public List<DeviceAction> Actions { get; set; } = [];
}


