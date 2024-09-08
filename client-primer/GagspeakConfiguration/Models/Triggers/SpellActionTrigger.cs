using GagspeakAPI.Data.Enum;
using GagspeakAPI.Data.VibeServer;

namespace GagSpeak.GagspeakConfiguration.Models;

/// <summary>
/// A basic authentication class to validate that the information from the client when they attempt to connect is correct.
/// </summary>
[Serializable]
public record SpellActionTrigger : Trigger
{
    public override TriggerKind Type => TriggerKind.SpellAction;

    // the type of action we are scanning for.
    public LimitedActionEffectType ActionKind { get; set; } = LimitedActionEffectType.Damage;

    // (self = done to you, target = done by you) Conditions vary based on actionKind
    public TriggerDirection Direction { get; set; } = TriggerDirection.Self;

    // the ID of the action to listen to.
    public uint ActionID { get; set; } = uint.MaxValue;
    
    // the threshold value that must be healed/dealt to trigger the action (-1 = full, 0 = onAction)
    public int ThresholdMinValue { get; set; } = -1;
    public int ThresholdMaxValue { get; set; } = 10000000;
}
