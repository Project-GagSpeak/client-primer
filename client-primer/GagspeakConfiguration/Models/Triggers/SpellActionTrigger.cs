using GagspeakAPI.Data.VibeServer;

namespace GagSpeak.GagspeakConfiguration.Models;

/// <summary>
/// A basic authentication class to validate that the information from the client when they attempt to connect is correct.
/// </summary>
[Serializable]
public record SpellActionTrigger : Trigger
{
    public override TriggerKind Type => TriggerKind.SpellAction;

    // the spells that execute the trigger (Seperated by | )
    public string ActionSpellNames { get; set; } = string.Empty;

    // the direction they are casted in (self = done to you, target = done by you)
    public TriggerDirection Direction { get; set; } = TriggerDirection.Self;

    // the type of action we are scanning for.
    public ActionType ActionKind { get; set; } = ActionType.Any;
    
    // the threshold value that must be healed/dealth to trigger the action (-1 = full, 0 = onAction)
    public int ThresholdMinValue { get; set; } = -1;
    public int ThresholdMaxValue { get; set; } = 10000000;
}
