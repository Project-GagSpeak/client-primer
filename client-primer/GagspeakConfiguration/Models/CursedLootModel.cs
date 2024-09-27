using GagspeakAPI.Data;

namespace GagSpeak.GagspeakConfiguration.Models;

[Serializable]
public record CursedLootModel
{
    /// <summary> The List of GUID's that are currently cursed </summary>
    public List<CursedItem> CursedItems { get; set; } = new List<CursedItem>();

    /// <summary> The Lower Lock limit for the cursed items </summary>
    public TimeSpan LockRangeLower { get; set; } = TimeSpan.Zero;

    /// <summary> The Upper Lock limit for the cursed items </summary>
    public TimeSpan LockRangeUpper { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary> The chance of a cursed item being locked </summary>
    public int LockChance { get; set; } = 0;
}

[Serializable]
public record CursedItem
{
    public Guid RestraintGuid { get; set; } = Guid.Empty;
    public GagType AttachedGag { get; set; } = GagType.None;
}
