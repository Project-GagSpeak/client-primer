using GagSpeak.Toybox.Models;

namespace GagSpeak.GagspeakConfiguration.Models;

/// <summary>
/// A basic authentication class to validate that the information from the client when they attempt to connect is correct.
/// </summary>
[Serializable]
public record HealthPercentTrigger : Trigger
{
    public override TriggerKind Type => TriggerKind.HealthPercent;

    // if allowing percentageHealth
    public bool AllowPercentageHealth { get; set; } = false;

    // the minvalue to display (can either be in percent or normal numbers, based on above option)
    public int MinHealthValue { get; set; } = 0;

    // the maxvalue to display (can either be in percent or normal numbers, based on above option)
    public int MaxHealthValue { get; set; } = 10000000;
}
