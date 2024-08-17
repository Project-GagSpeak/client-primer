using Penumbra.GameData.Data;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;

namespace GagSpeak.Utils;
public class ItemIdVars
{
    private readonly ObjectIdentification _objectIdentification;
    private readonly ItemData _itemData;

    public ItemIdVars(ObjectIdentification objectIdentification,
        ItemData itemData)
    {
        _objectIdentification = objectIdentification;
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
            item = slot is EquipSlot.MainHand or EquipSlot.OffHand
                ? Identify(slot, item.PrimaryId, item.SecondaryId, item.Variant)
                : Identify(slot, item.PrimaryId, item.Variant);
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

    public EquipItem Identify(EquipSlot slot, PrimaryId id, Variant variant)
    {
        slot = slot.ToSlot();
        if (slot.ToIndex() == uint.MaxValue)
            return new EquipItem($"Invalid ({id.Id}-{variant})", 0, 0, id, 0, variant, 0, 0, 0, 0);

        switch (id.Id)
        {
            case 0: return NothingItem(slot);
            case 9903: return SmallClothesItem(slot);
            default:
                var item = _objectIdentification.Identify(id, 0, variant, slot).FirstOrDefault();
                return item.Valid
                    ? item 
                    : EquipItem.FromIds(0, 0, id, 0, variant, slot.ToEquipType());
        }
    }

    public EquipItem Identify(EquipSlot slot, PrimaryId id, SecondaryId type, Variant variant,
        FullEquipType mainhandType = FullEquipType.Unknown)
    {
        if (slot is not EquipSlot.MainHand and not EquipSlot.OffHand)
            return new EquipItem($"Invalid ({id.Id}-{type.Id}-{variant})", 0, 0, id, type, variant, 0, 0, 0, 0);

        var item = _objectIdentification.Identify(id, type, variant, slot).FirstOrDefault(i => i.Type.ToSlot() == slot);
        return item.Valid
            ? item
            : EquipItem.FromIds(0, 0, id, type, variant, slot.ToEquipType());
    }
}
