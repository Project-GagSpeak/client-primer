namespace GagSpeak.PlayerData.Services;

/// <summary>
/// Unfortunately Mediators are slow enough to allow bratty 
/// Submissive girls out of restraints, so we need to use raw event handlers for this.
/// </summary>
public class IpcFastUpdates
{
    public delegate void GlamourFastUpdateHandler(GlamourUpdateType e); // define the event handler
    public static event GlamourFastUpdateHandler? GlamourEventFired; // define the static event
    public static void InvokeGlamourer(GlamourUpdateType changeType) => GlamourEventFired?.Invoke(changeType);



    public delegate void CustomizeFastUpdateHandler(Guid e);
    public static event CustomizeFastUpdateHandler? CustomizeEventFired;
    public static void InvokeCustomize(Guid Guid) => CustomizeEventFired?.Invoke(Guid);
}
