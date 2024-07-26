using GagspeakAPI.Data.Enum;
using GagspeakAPI.Data.VibeServer;

namespace GagSpeak.GagspeakConfiguration.Models;

/// <summary>
/// A basic authentication class to validate that the information from the client when they attempt to connect is correct.
/// </summary>
[Serializable]
public record GagTrigger : Trigger
{
    public override TriggerKind Type => TriggerKind.GagState;

    // the gag that must be toggled to execute the trigger
    public GagList.GagType Gag { get; set; } = GagList.GagType.None;
    
    // the state of the gag that invokes it.
    public UpdatedNewState GagState { get; set; } = UpdatedNewState.Enabled;
}
