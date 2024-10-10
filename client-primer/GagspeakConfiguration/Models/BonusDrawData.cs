
using Newtonsoft.Json.Linq;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;

namespace GagSpeak.GagspeakConfiguration.Models;

/// <summary> Model for the draw data of a players equipment slot </summary>
[Serializable]
public record BonusDrawData
{
    public bool IsEnabled = false; // determines if it will be applied during event handling.
    public string EquippedBy = string.Empty; // remove if no use
    public bool Locked = false; // remove if no use
    public BonusItemFlag Slot = BonusItemFlag.Glasses;
    public BonusItem GameItem;
    // no stains for now.
    public BonusDrawData(BonusItem gameItem) => GameItem = gameItem;

    // In EquipDrawData
    public JObject Serialize()
    {
        // Include gameItemObj and gameStainObj in the serialized object
        return new JObject()
        {
            ["IsEnabled"] = IsEnabled,
            ["EquippedBy"] = EquippedBy,
            ["Locked"] = Locked,
            ["Slot"] = Slot.ToString(),
            ["GameItem"] = JObject.FromObject(GameItem),
        };
    }

    public void Deserialize(JObject jsonObject)
    {
        IsEnabled = jsonObject["IsEnabled"]?.Value<bool>() ?? false;
        EquippedBy = jsonObject["EquippedBy"]?.Value<string>() ?? string.Empty;
        Locked = jsonObject["Locked"]?.Value<bool>() ?? false;
        Slot = (BonusItemFlag)Enum.Parse(typeof(BonusItemFlag), jsonObject["Slot"]?.Value<string>() ?? string.Empty);
        GameItem = jsonObject["GameItem"] != null ? jsonObject["GameItem"].ToObject<BonusItem>() : new BonusItem();
    }
}
