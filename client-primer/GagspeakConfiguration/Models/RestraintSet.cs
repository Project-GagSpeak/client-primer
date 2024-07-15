using Penumbra.GameData.Enums;

namespace GagSpeak.GagspeakConfiguration.Models;

/// <summary>
/// A basic authentication class to validate that the information from the client when they attempt to connect is correct.
/// </summary>
[Serializable]
public record RestraintSet
{
    /// <summary> The name of the pattern </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary> The description of the pattern </summary>
    public string Description { get; set; } = string.Empty;

    public bool Enabled { get; set; } = false;

    public bool Locked { get; set; } = false;

    public string EnabledBy { get; set; } = string.Empty;

    public string LockedBy { get; set; } = string.Empty;

    public DateTimeOffset LockedUntil { get; set; } = DateTimeOffset.MinValue;

    public Dictionary<EquipSlot, EquipDrawData> DrawData { get; set; } = new();

    public List<AssociatedMod> AssociatedMods { get; private set; } = new();

    /// <summary> 
    /// The Hardcore Set Properties to apply when restraint set is toggled.
    /// The string indicates the UID associated with the set properties.
    /// </summary>
    public Dictionary<string, HardcoreSetProperties> SetProperties { get; set; } = new();
}
