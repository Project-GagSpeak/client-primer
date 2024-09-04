using GagSpeak.Utils;
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
    [JsonIgnore]
    private readonly ItemIdVars _itemHelpers;

    public bool IsEnabled { get; set; } = true;
    public EquipSlot Slot { get; set; } = EquipSlot.Head;
    public EquipItem GameItem { get; set; }
    public StainIds GameStain { get; set; } = StainIds.None;
    public bool ForceHeadgearOnEnable { get; set; } = false;
    public bool ForceVisorOnEnable { get; set; } = false;
    
    // List of Moodles to apply while Gagged.
    public List<Guid> GagMoodles { get; set; } = new List<Guid>();
    
    // C+ Preset when its not a bitch anymore

    // Spatial Audio type to use while gagged. (May not use since will just have one type?)
    
    [JsonIgnore]
    public int ActiveSlotId => Array.IndexOf(EquipSlotExtensions.EqdpSlots.ToArray(), Slot);
    public GagDrawData(ItemIdVars itemHelper, EquipItem gameItem)
    {
        _itemHelpers = itemHelper;
        GameItem = gameItem;
    }

    // In EquipDrawData
    public JObject Serialize()
    {
        return new JObject()
        {
            ["IsEnabled"] = IsEnabled,
            ["ForceHeadgearOnEnable"] = ForceHeadgearOnEnable,
            ["ForceVisorOnEnable"] = ForceVisorOnEnable,
            ["GagMoodles"] = new JArray(GagMoodles),
            ["Slot"] = Slot.ToString(),
            ["CustomItemId"] = GameItem.Id.ToString(),
            ["GameStain"] = GameStain.ToString(),
        };
    }

    public void Deserialize(JObject jsonObject)
    {
        IsEnabled = jsonObject["IsEnabled"]?.Value<bool>() ?? false;
        ForceHeadgearOnEnable = jsonObject["ForceHeadgearOnEnable"]?.Value<bool>() ?? false;
        ForceVisorOnEnable = jsonObject["ForceVisorOnEnable"]?.Value<bool>() ?? false;
        // Deserialize the AssociatedMoodles
        if (jsonObject["GagMoodles"] is JArray associatedMoodlesArray)
        {
            GagMoodles = associatedMoodlesArray.Select(moodle => Guid.Parse(moodle.Value<string>())).ToList();
        }
        Slot = (EquipSlot)Enum.Parse(typeof(EquipSlot), jsonObject["Slot"]?.Value<string>() ?? string.Empty);
        ulong customItemId = jsonObject["CustomItemId"]?.Value<ulong>() ?? 4294967164;
        GameItem = _itemHelpers.Resolve(Slot, new CustomItemId(customItemId));

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

