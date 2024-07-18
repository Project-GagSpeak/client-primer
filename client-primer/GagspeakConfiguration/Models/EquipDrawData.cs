using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;

namespace GagSpeak.GagspeakConfiguration.Models;

/// <summary> Model for the draw data of a players equipment slot </summary>
/// <param name="gameItem"> the game item we are storing the drawdata of.</param>
[Serializable]
public record EquipDrawData(EquipItem gameItem)
{
    public bool IsEnabled = false;
    public string EquippedBy = string.Empty; // remove if no use
    public bool Locked = false; // remove if no use
    public EquipSlot Slot = EquipSlot.Head;
    public EquipItem GameItem = gameItem;
    public StainIds GameStain = StainIds.None;
    public int ActiveSlotId = 0; // what slot of the equipment it is.
}
