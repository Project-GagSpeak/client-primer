using GagspeakAPI.Data.Enum;
using System.Diagnostics;

namespace GagSpeak.GagspeakConfiguration.Models;

/// <summary> Stores EquipDrawData for each GagType </summary>
[Serializable]
public class GagStorage
{
    public Dictionary<GagType, GagDrawData> GagEquipData { get; set; } = new();
}
