using GagspeakAPI.Data.Enum;
using GagspeakAPI.Data.VibeServer;

namespace GagSpeak.GagspeakConfiguration.Models;

/// <summary>
/// A basic authentication class to validate that the information from the client when they attempt to connect is correct.
/// </summary>
[Serializable]
public record SocialTrigger : Trigger
{
    public override TriggerKind Type => TriggerKind.SocialAction;
    
    // the social action to monitor.
    public SocialActionType SocialType { get; set; } = SocialActionType.DeathRollLoss;
}
