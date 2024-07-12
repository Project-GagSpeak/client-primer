namespace GagSpeak.GagspeakConfiguration.Models;

[Serializable]
public class WardrobeStorage
{
    /// <summary> The list of restraint sets in the wardrobe </summary>
    public List<RestraintSet> RestraintSets { get; set; } = new();

    /// <summary> the currently selected restraint set (may not need this as part of the config?) </summary>
    public int SelectedRestraintSet { get; set; } = 0;
}
