using Dalamud.Plugin.Services;
using GagSpeak.GagspeakConfiguration;
using GagspeakAPI.Data.Enum;
using Glamourer.Api.Enums;

namespace GagSpeak.PlayerData.Services;

/// <summary>
/// Unfortunately Mediators are slow enough to allow bratty 
/// Submissive girls out of restraints, so we need to use raw event handlers for this.
/// </summary>
public class GlamourFastUpdate
{
    public delegate void GlamourFastUpdateHandler(object sender, GlamourFastUpdateArgs e); // define the event handler
    public event GlamourFastUpdateHandler? GlamourEventFired; // define the event

    public void Invoke(GlamourUpdateType ChangeType)
    {
        GlamourEventFired?.Invoke(this, new GlamourFastUpdateArgs(ChangeType));
    }
}

public class GlamourFastUpdateArgs : EventArgs
{
    public GlamourUpdateType GenericUpdateType { get; }
    public GlamourFastUpdateArgs(GlamourUpdateType updateType) { GenericUpdateType = updateType; }
}
