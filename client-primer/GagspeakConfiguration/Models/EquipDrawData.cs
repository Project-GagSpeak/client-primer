using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;

namespace GagSpeak.GagspeakConfiguration.Models;

// PERSONAL NOTE:
// tried to stick with [JsonIgnore] and [JsonProperty("some_name")] 


/// <summary> Model for the draw data of a players equipment slot </summary>
/// <param name="gameItem"> the game item we are storing the drawdata of.</param>
[Serializable]
public record EquipDrawData
{
    public bool IsEnabled { get; set; } = false; // determines if it will be applied during event handling.
    public string EquippedBy { get; set; } = string.Empty; // remove if no use
    public bool Locked { get; set; } = false; // remove if no use
    public EquipSlot Slot { get; set; } = EquipSlot.Head;
    public EquipItem GameItem { get; set; } = new EquipItem();
    public StainIds GameStain { get; set; } = StainIds.None;

    public EquipDrawData(EquipItem gameItem) => GameItem = gameItem;

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
            ["EquippedBy"] = EquippedBy,
            ["Locked"] = Locked,
            ["Slot"] = Slot.ToString(),
            ["GameItem"] = gameItemObj,
            ["GameStain"] = GameStain.ToString(),
        };
    }

    public void Deserialize(JObject jsonObject)
    {
        IsEnabled = jsonObject["IsEnabled"]?.Value<bool>() ?? false;
        EquippedBy = jsonObject["EquippedBy"]?.Value<string>() ?? string.Empty;
        Locked = jsonObject["Locked"]?.Value<bool>() ?? false;
        Slot = (EquipSlot)Enum.Parse(typeof(EquipSlot), jsonObject["Slot"]?.Value<string>() ?? string.Empty);
        var serializer = new JsonSerializer();
        serializer.Converters.Add(new EquipItemConverter());
        GameItem = jsonObject["GameItem"] != null ? jsonObject["GameItem"].ToObject<EquipItem>(serializer) : new EquipItem();
        // Parse the StainId
        var gameStainString = jsonObject["GameStain"]?.Value<string>() ?? "0,0";
        var stainParts = gameStainString.Split(',');
        if (stainParts.Length == 2 && int.TryParse(stainParts[0], out int stain1) && int.TryParse(stainParts[1], out int stain2))
        {
            GameStain = new StainIds((StainId)stain1, (StainId)stain2);
        }
        else
        {
            GameStain = StainIds.None;
        }
    }
}
