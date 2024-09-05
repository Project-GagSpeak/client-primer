
using GagSpeak.Utils;
using Penumbra.GameData.Enums;

namespace GagSpeak.GagspeakConfiguration.Models;

[Serializable]
public class BlindfoldModel
{
    [JsonIgnore]
    private readonly ItemIdVars _itemHelper;
    public BlindfoldModel(ItemIdVars itemHelper)
    {
        _itemHelper = itemHelper;
        BlindfoldItem = new EquipDrawData(_itemHelper, (ItemIdVars.NothingItem(EquipSlot.Head))) { Slot = EquipSlot.Head, IsEnabled = false };
    }

    public bool ForceHeadgearOnEnable { get; set; } = false;
    public bool ForceVisorOnEnable { get; set; } = false;
    public List<Guid> BlindfoldMoodles { get; set; } = new List<Guid>();
    public EquipDrawData BlindfoldItem { get; set; }

    public JObject Serialize()
    {
        return new JObject
        {
            ["ForceHeadgearOnEnable"] = ForceHeadgearOnEnable,
            ["ForceVisorOnEnable"] = ForceVisorOnEnable,
            ["BlindfoldMoodles"] = new JArray(BlindfoldMoodles),
            ["BlindfoldItem"] = BlindfoldItem.Serialize()
        };
    }

    public void Deserialize(JObject jsonObject)
    {
        ForceHeadgearOnEnable = jsonObject["ForceHeadgearOnEnable"]?.Value<bool>() ?? false;
        ForceVisorOnEnable = jsonObject["ForceVisorOnEnable"]?.Value<bool>() ?? false;
        if (jsonObject["BlindfoldMoodles"] is JArray associatedMoodlesArray)
        {
            BlindfoldMoodles = associatedMoodlesArray.Select(moodle => Guid.Parse(moodle.Value<string>())).ToList();
        }
        BlindfoldItem.Deserialize((JObject)jsonObject["BlindfoldItem"]);
    }
}
