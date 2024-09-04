using Penumbra.GameData.Data;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;

namespace GagSpeak.Utils;
public class ItemIdVars
{
    private ItemData _itemData;

    public ItemIdVars(ItemData itemData)
    {
        _itemData = itemData;
    }

    public static ItemId NothingId(EquipSlot slot) // used
        => uint.MaxValue - 128 - (uint)slot.ToSlot();

    public static ItemId SmallclothesId(EquipSlot slot) // unused
        => uint.MaxValue - 256 - (uint)slot.ToSlot();

    public static ItemId NothingId(FullEquipType type) // unused
        => uint.MaxValue - 384 - (uint)type;

    public static EquipItem NothingItem(EquipSlot slot) // used
        => new("Nothing", NothingId(slot), 0, 0, 0, 0, slot.ToEquipType(), 0, 0, 0);

    public static EquipItem NothingItem(FullEquipType type) // likely unused
        => new("Nothing", NothingId(type), 0, 0, 0, 0, type, 0, 0, 0);

    public static EquipItem SmallClothesItem(EquipSlot slot) // used
        => new("Smallclothes (NPC)", SmallclothesId(slot), 0, 9903, 0, 1, slot.ToEquipType(), 0, 0, 0);

    public EquipItem Resolve(EquipSlot slot, CustomItemId itemId)
    {
        slot = slot.ToSlot();
        if (itemId == NothingId(slot))
            return NothingItem(slot);
        if (itemId == SmallclothesId(slot))
            return SmallClothesItem(slot);

        if (!itemId.IsItem)
        {
            var item = EquipItem.FromId(itemId);
            return item;
        }
        else if (!_itemData.TryGetValue(itemId.Item, slot, out var item))
        {
            return EquipItem.FromId(itemId);
        }
        else
        {
            if (item.Type.ToSlot() != slot)
                return new EquipItem(string.Intern($"Invalid #{itemId}"), itemId, item.IconId, item.PrimaryId, item.SecondaryId, item.Variant,
                    0, 0, 0, 0);

            return item;
        }
    }
}
