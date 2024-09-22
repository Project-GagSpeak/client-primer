using Dalamud.Plugin.Services;
using GagSpeak.GagspeakConfiguration;
using GagspeakAPI.Enums;
using Glamourer.Api.Enums;

namespace GagSpeak.PlayerData.Services;

/// <summary>
/// Unfortunately Mediators are slow enough to allow bratty 
/// Submissive girls out of restraints, so we need to use raw event handlers for this.
/// </summary>
public class IpcFastUpdates
{
    public delegate void GlamourFastUpdateHandler(object sender, GlamourUpdateType e); // define the event handler
    public event GlamourFastUpdateHandler? GlamourEventFired; // define the event
    public void InvokeGlamourer(GlamourUpdateType ChangeType) => GlamourEventFired?.Invoke(this, ChangeType);


    public delegate void CustomizeFastUpdateHandler(object sender, Guid e);
    public event CustomizeFastUpdateHandler? CustomizeEventFired;
    public void InvokeCustomize(Guid Guid) => CustomizeEventFired?.Invoke(this, Guid);
}
