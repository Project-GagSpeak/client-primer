using System;
using System.Numerics;
using ImGuiNET;
using OtterGui.Raii;
using System.Linq;
using Dalamud.Interface.Utility;
using GagSpeak.GagspeakConfiguration;
using GagspeakAPI.Data.Enum;
using System.Linq;

namespace GagSpeak.UI.Components.SelectionLists;
/// <summary> This class is used to handle the ConfigSettings Tab. </summary>
public class GagStorageSelector // while not in the same section, it is performing the same logic.
{
    public Enum SelectedGag { get; set; } = null!;
    public GagStorageSelector() { }

    public void DrawSelector()
    {
        foreach (var gagItem in Enum.GetValues(typeof(GagList.GagType)).Cast<GagList.GagType>().Where(gag => gag != GagList.GagType.None))
        {
            if (gagItem == GagList.GagType.None) continue;

            if (ImGui.Selectable(gagItem.GetGagAlias(), gagItem.Equals(SelectedGag)))
            {
            SelectedGag = (Enum)gagItem;
            }
        }
    }

}
