using System;
using System.Collections.Generic;
using Penumbra.GameData.Enums;
using Newtonsoft.Json.Linq;
using GagSpeak.UI.Equipment;
using GagSpeak.Utility;
using GagSpeak.Interop.Penumbra;
using OtterGui.Classes;
using Dalamud.Interface.ImGuiNotification;

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

    // update to reflect glamourer slot data later.
    public Dictionary<EquipSlot, EquipDrawData> _drawData; // stores the equipment draw data for the set
    public List<(Mod mod, ModSettings modSettings, bool disableWhenInactive, bool redrawAfterToggle)> _associatedMods { get; private set; }
}
