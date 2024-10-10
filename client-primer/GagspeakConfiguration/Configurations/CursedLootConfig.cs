using GagSpeak.GagspeakConfiguration.Models;

namespace GagSpeak.GagspeakConfiguration.Configurations;

public class CursedLootConfig : IGagspeakConfiguration
{
    /// <summary> The Clients Cursed Loot Storage </summary>
    public CursedLootStorage CursedLootStorage { get; set; } = new();

    public static int CurrentVersion => 1;

    public int Version { get; set; } = CurrentVersion;
}
