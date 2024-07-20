using GagSpeak.Utils;
using Newtonsoft.Json.Linq;
using Penumbra.GameData.Enums;

namespace GagSpeak.GagspeakConfiguration.Models;

[Serializable]
public class BlindfoldModel
{
    /// <summary> If you are currently blindfolded. </summary>
    public bool IsActive { get; set; } = false;
    
    /// <summary> The UID of the player who blindfolded you, if any </summary>
    public string BlindfoldedBy { get; set; } = string.Empty;

    /// <summary> The DrawData for the Hardcore Blindfold Item </summary>
    public EquipDrawData BlindfoldItem { get; set; } = new EquipDrawData(
        ItemIdVars.NothingItem(EquipSlot.Head)) { Slot = EquipSlot.Head, IsEnabled = false };

    // Blank constructor to help with deserialization
    public BlindfoldModel()
    {
        // 
    }

    // serializer
    public JObject Serialize()
    {
        return new JObject
        {
            ["IsActive"] = IsActive,
            ["BlindfoldedBy"] = BlindfoldedBy,
            ["BlindfoldItem"] = BlindfoldItem.Serialize()
        };
    }

    // deserializer
    public void Deserialize(JObject jsonObject)
    {
        IsActive = (bool)jsonObject["IsActive"];
        BlindfoldedBy = (string)jsonObject["BlindfoldedBy"];
        BlindfoldItem.Deserialize((JObject)jsonObject["BlindfoldItem"]);
    }
}
