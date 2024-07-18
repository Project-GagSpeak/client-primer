using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;

namespace GagSpeak.GagspeakConfiguration.Models;

/// <summary> Model for the draw data of a players equipment slot </summary>
/// <param name="gameItem"> the game item we are storing the drawdata of.</param>
[Serializable]
public record GagDrawData
{
    public bool IsEnabled { get; set; } = false;
    public EquipSlot Slot { get; set; } = EquipSlot.Head;
    public EquipItem GameItem { get; set; }
    public StainIds GameStain { get; set; } = StainIds.None;
    public int ActiveSlotId { get; set; } = 0;

    public GagDrawData(EquipItem gameItem) => GameItem = gameItem;


    // In EquipDrawData
    public JObject Serialize()
    {
        // Create a Json with the EquipItemConverter
        var serializer = new JsonSerializer();
        serializer.Converters.Add(new EquipItemConverter());
        // Serialize _gameItem and _gameStain as JObjects
        JObject gameItemObj = JObject.FromObject(GameItem, serializer);

        // Include gameItemObj and gameStainObj in the serialized object
        return new JObject()
        {
            ["IsEnabled"] = IsEnabled,
            ["Slot"] = Slot.ToString(),
            ["GameItem"] = gameItemObj,
            ["GameStain"] = GameStain.ToString(),
            ["ActiveSlotListIdx"] = ActiveSlotId,
        };
    }

    public void Deserialize(JObject jsonObject)
    {
        IsEnabled = jsonObject["IsEnabled"]?.Value<bool>() ?? false;
        Slot = (EquipSlot)Enum.Parse(typeof(EquipSlot), jsonObject["Slot"]?.Value<string>() ?? string.Empty);
        var serializer = new JsonSerializer();
        serializer.Converters.Add(new EquipItemConverter());
        GameItem = jsonObject["GameItem"] != null ? jsonObject["GameItem"].ToObject<EquipItem>(serializer) : new EquipItem();
        // Parse the StainId
        if (jsonObject["GameStain"] is JArray gameStainArray && gameStainArray.Count >= 2)
        {
            var stain1 = gameStainArray[0].ToObject<StainId>();
            var stain2 = gameStainArray[1].ToObject<StainId>();
            GameStain = new StainIds(stain1, stain2);
        }
        else
        {
            GameStain = StainIds.None;
        }
    }
}

