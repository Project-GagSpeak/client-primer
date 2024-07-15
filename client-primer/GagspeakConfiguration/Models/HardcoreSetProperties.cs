using Penumbra.GameData.Enums;

namespace GagSpeak.GagspeakConfiguration.Models;

/// <summary>
/// A basic authentication class to validate that the information from the client when they attempt to connect is correct.
/// </summary>
[Serializable]
public record HardcoreSetProperties
{
    /// <summary> Any action which typically involves fast leg movement is restricted </summary>
    public bool LegsRestrained { get; set; }
    
    /// <summary> Any action which typically involves fast arm movement is restricted </summary>
    public bool ArmsRestrained { get; set; }
    
    /// <summary> Any action requiring speech is restricted </summary>
    public bool Gagged { get; set; }
    
    /// <summary> Any actions requiring awareness or sight is restricted </summary>
    public bool Blindfolded { get; set; }
    
    /// <summary> Player becomes unable to move in this set </summary>
    public bool Immobile { get; set; }
    
    /// <summary> Player is forced to only walk while wearing this restraint </summary>
    public bool Weighty { get; set; }

    /// <summary> Any action requiring focus or concentration has its cast time being slightly slower </summary>
    public bool LightStimulation { get; set; }

    /// <summary> Any action requiring focus or concentration has its cast time being noticeably slower </summary>
    public bool MildStimulation { get; set; }
    
    /// <summary> Any action requiring focus or concentration has its cast time being significantly slower </summary>
    public bool HeavyStimulation { get; set; } 
}
