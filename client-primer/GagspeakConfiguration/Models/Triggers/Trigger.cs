using GagspeakAPI.Enums;
using GagspeakAPI.Data;

namespace GagSpeak.GagspeakConfiguration.Models;

/// <summary>
/// A basic authentication class to validate that the information from the client when they attempt to connect is correct.
/// </summary>
[Serializable]
public abstract record Trigger
{
    // Define which kind of trigger it is
    public abstract TriggerKind Type { get; }

    // required attributes
    public Guid TriggerIdentifier { get; set; } = Guid.NewGuid();
    public bool Enabled { get; set; } = false;
    public int Priority { get; set; } = 0;

    // generic attributes
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public TimeSpan StartAfter { get; set; } = TimeSpan.Zero;
    public TimeSpan EndAfter { get; set; } = TimeSpan.Zero;

    // List of UID's that are able to Enable/Interact with this trigger.
    public List<string> CanToggleTrigger { get; set; } = new List<string>();

    // The actions to execute to each motor on the list of appended devices.
    // Below are dummy values that are set to default when not used.
    public TriggerActionKind TriggerActionKind { get; set; } = TriggerActionKind.SexToy;
    public List<DeviceTriggerAction> TriggerAction { get; set; } = new List<DeviceTriggerAction>();
    public ShockTriggerAction ShockTriggerAction { get; set; } = new ShockTriggerAction();
    public string RestraintNameAction { get; set; } = string.Empty;
    public GagLayer GagLayerAction { get; set; } = GagLayer.UnderLayer;
    public GagType GagTypeAction { get; set; } = GagType.None;
    public Guid MoodlesIdentifier { get; set; } = Guid.Empty; // can be a status or preset, depending on TriggerActionKind
}


