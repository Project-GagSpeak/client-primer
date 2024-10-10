using GagSpeak.Services;
using Microsoft.Extensions.DependencyInjection;
using Penumbra.GameData.Data;
using Penumbra.GameData.DataContainers.Bases;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;

namespace GagSpeak.Utils;
public static class ItemIdVars
{
    // Fantastic hack that pulls from the itemdata stored in the _host services to pull directly.
    // As its singleton and only needed to be made once, there isnt a reason it should need to be
    // included everywhere else for use. Especially on every clone action done in the plugin.
    private static ItemData Data = GagSpeak.ServiceProvider.GetService<ItemData>()!;

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

    public static EquipItem Resolve(EquipSlot slot, CustomItemId itemId)
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
        else if (!Data.TryGetValue(itemId.Item, slot, out var item))
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
