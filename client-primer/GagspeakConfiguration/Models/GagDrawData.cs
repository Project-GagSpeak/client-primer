using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;

namespace GagSpeak.GagspeakConfiguration.Models;

/// <summary> Model for the draw data of a players equipment slot </summary>
/// <param name="gameItem"> the game item we are storing the drawdata of.</param>
[Serializable]
public record GagDrawData(EquipItem gameItem)
{
    public bool IsEnabled = false;
    public EquipSlot Slot = EquipSlot.Head;
    public EquipItem GameItem = gameItem;
    public StainId GameStain = 0;
    public int ActiveSlotId = 0; // what slot of the equipment it is.
}
