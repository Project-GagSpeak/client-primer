using GagSpeak.Utils;
using System.Text.Json.Serialization;

namespace GagSpeak.GagspeakConfiguration.Models;

[Serializable]
public class WardrobeStorage
{
    [JsonIgnore]
    private readonly ItemIdVars _itemHelper;

    /// <summary> The list of restraint sets in the wardrobe </summary>
    public List<RestraintSet> RestraintSets { get; set; }

    /// <summary> The DrawData for the Hardcore Blindfold Item </summary>
    public BlindfoldModel BlindfoldInfo { get; set; }

    // Blank constructor to help with deserialization
    public WardrobeStorage(ItemIdVars itemHelper)
    {
        _itemHelper = itemHelper;
        RestraintSets = new List<RestraintSet>();
        BlindfoldInfo = new BlindfoldModel(_itemHelper);
    }
}
