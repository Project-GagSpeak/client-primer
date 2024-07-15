using Dalamud.Plugin;
using GagSpeak.GagspeakConfiguration.Models;
using GagSpeak.Hardcore;
using GagSpeak.Hardcore.Movement;
using GagSpeak.PlayerData.Data;
using GagSpeak.PlayerData.Pairs;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using GagSpeak.UI;
using GagspeakAPI.Data.Enum;
using System.Numerics;

namespace GagSpeak.PlayerData.Handlers;
/// <summary>
/// The wardrobe Handler is designed to store a variety of public reference variables for other classes to use.
/// Primarily, they will store values that would typically be required to iterate over a heavy list like all client pairs
/// to find, and only update them when necessary.
/// </summary>
public class WardrobeHandler : DisposableMediatorSubscriberBase
{
    private readonly ClientConfigurationManager _clientConfigs;
    private readonly PlayerCharacterManager _playerManager;
    private readonly PairManager _pairManager;
    
    public WardrobeHandler(ILogger<GagDataHandler> logger, GagspeakMediator mediator,
        ClientConfigurationManager clientConfiguration, PlayerCharacterManager playerManager,
        PairManager pairManager) : base(logger, mediator)
    {
        _clientConfigs = clientConfiguration;
        _playerManager = playerManager;
        _pairManager = pairManager;

        Mediator.Subscribe<RestraintSetToggledMessage>(this, (msg) =>
        {
            if(msg.State == UpdatedNewState.Disabled)
            {
                // grab the restraint set that was just disabled
                var disabledSet = _clientConfigs.GetRestraintSet(msg.RestraintSetIndex);
                // see if we had any hardcore properties set for the user pair who had enabled the set,
                if(_clientConfigs.PropertiesEnabledForSet(msg.RestraintSetIndex, msg.AssignerUID))
                {
                    // we will want to publish a call to stop monitoring hardcore actions.
                    Mediator.Publish(new HardcoreRestraintSetDisabledMessage());
                }
                // call to the config to toggle the set off properly.
                _clientConfigs.SetRestraintSetState(msg.State, msg.RestraintSetIndex, msg.AssignerUID);
                // remove the active restraint set
                ActiveSet = null;
                // remove the pair who enabled the set
                PairWhoEnabledSet = null;
            }
            else
            {
                // restraint set was activated, so, lets set the active set after applying the changes
                _clientConfigs.SetRestraintSetState(msg.State, msg.RestraintSetIndex, msg.AssignerUID);
                // call the active set
                ActiveSet = _clientConfigs.GetActiveSet();
                // set the pair to who enabled the set
                PairWhoEnabledSet = _pairManager.DirectPairs.FirstOrDefault(p => p.UserData.UID == msg.AssignerUID);
                // see if it has any hardcore properties attached for the UID enabling it
                if(_clientConfigs.PropertiesEnabledForSet(msg.RestraintSetIndex, msg.AssignerUID))
                {
                    // we will want to publish a call to start monitoring hardcore actions.
                    Mediator.Publish(new HardcoreRestraintSetEnabledMessage());
                }
            }
        });

        Mediator.Subscribe<RestraintSetPropertyChanged>(this, (msg) =>
        {
            Logger.LogTrace("We detected a change in your hardcore restraint set");
        });

        Mediator.Subscribe<BeginForcedToFollowMessage>(this, (msg) => ForcedToFollowPair = msg.Pair);

        Mediator.Subscribe<BeginForcedToSitMessage>(this, (msg) => ForcedToSitPair = msg.Pair);
        
        Mediator.Subscribe<BeginForcedToStayMessage>(this, (msg) => ForcedToStayPair = msg.Pair);
        
        Mediator.Subscribe<BeginForcedBlindfoldMessage>(this, (msg) => BlindfoldedByPair = msg.Pair);
    }

    // keep in mind, for everything below, only one can be active at a time. That is the beauty of this.
    // Additionally, we will only ever care about the restraint set properties of the active set.
    /// <summary> The current restraint set that is active on the client. Null if none. </summary>
    public RestraintSet ActiveSet { get; private set; }

    /// <summary> Stores the pair who activated the set. </summary>
    public Pair PairWhoEnabledSet { get; private set; }
    
    /// <summary> The current Pair in our pair list who has forced us to follow them. Null if none. </summary>
    public Pair? ForcedToFollowPair { get; private set; }

    /// <summary> The current Pair in our pair list who has forced us to sit. Null if none. </summary>
    public Pair? ForcedToSitPair { get; private set; }

    /// <summary> The current Pair in our pair list who has forced us to stay. Null if none. </summary>
    public Pair? ForcedToStayPair { get; private set; }

    /// <summary> The current Pair in our pair list who has blindfolded us. Null if none. </summary>
    public Pair? BlindfoldedByPair { get; private set; }

    /// <summary> Methods to clear the set pairs are here below </summary>

}
