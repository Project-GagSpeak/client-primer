namespace GagSpeak.GagspeakConfiguration.Models;

[Serializable]
public class BlindfoldModel
{
    /// <summary> If you are currently blindfolded. </summary>
    public bool IsActive { get; set; } = false;
    
    /// <summary> The UID of the player who blindfolded you, if any </summary>
    public string BlindfoldedBy { get; set; } = string.Empty;

    /// <summary> The DrawData for the Hardcore Blindfold Item </summary>
    public EquipDrawData BlindfoldItem;
}
