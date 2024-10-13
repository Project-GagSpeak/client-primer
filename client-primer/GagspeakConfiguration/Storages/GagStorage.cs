using GagSpeak.GagspeakConfiguration.Configurations;
using GagSpeak.Utils;
using GagspeakAPI.Enums;
using Microsoft.IdentityModel.Tokens;
using Penumbra.GameData.Enums;
using System.Diagnostics;

namespace GagSpeak.GagspeakConfiguration.Models;

/// <summary> Stores EquipDrawData for each GagType </summary>
[Serializable]
public class GagStorage
{
    public Dictionary<GagType, GagDrawData> GagEquipData { get; set; } = new();
}
