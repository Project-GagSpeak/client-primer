using GagSpeak.GagspeakConfiguration.Models;
using GagSpeak.Interop.Ipc;
using GagSpeak.PlayerData.Pairs;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using GagspeakAPI.Data.Enum;

namespace GagSpeak.PlayerData.Handlers;
/// <summary>
/// The wardrobe Handler is designed to store a variety of public reference variables for other classes to use.
/// Primarily, they will store values that would typically be required to iterate over a heavy list like all client pairs
/// to find, and only update them when necessary.
/// </summary>
public class WardrobeHandler : DisposableMediatorSubscriberBase
{
    private readonly ClientConfigurationManager _clientConfigs;
    private readonly PairManager _pairManager;

    public WardrobeHandler(ILogger<GagDataHandler> logger, GagspeakMediator mediator,
        ClientConfigurationManager clientConfiguration, PairManager pairManager)
        : base(logger, mediator)
    {
        _clientConfigs = clientConfiguration;
        _pairManager = pairManager;

        // set the selected set to the first set in the list, if we have any sets in our storage
        if (_clientConfigs.GetRestraintSetCount() > 0)
        {
            SelectedSet = _clientConfigs.GetRestraintSet(0);
            SelectedSetIdx = 0;
        }
        else
        {
            SelectedSet = null!;
            SelectedSetIdx = -1;
        }

        // see if any sets are active, and if so, set the active set
        int activeIdx = GetActiveSetIndex();
        if (activeIdx != -1)
        {
            ActiveSet = _clientConfigs.GetRestraintSet(activeIdx);
        }



        Mediator.Subscribe<RestraintSetAddedMessage>(this, (msg) =>
        {
            Logger.LogInformation("Set Added, Wardrobe Config Saved");
            // see if the current selected set is null, if so, set it to the newly added set
            if (SelectedSet == null)
            {
                SelectedSet = _clientConfigs.GetRestraintSet(0);
                SelectedSetIdx = 0;
            }

            // We've just added a new set, so we want to push an update for other players.
            Mediator.Publish(new PlayerCharWardrobeChanged(DataUpdateKind.WardrobeRestraintOutfitsUpdated));
        });

        Mediator.Subscribe<RestraintSetRemovedMessage>(this, (msg) =>
        {
            Logger.LogInformation("Set Removed, Wardrobe Config Saved");
            // if the set removed was index 0, and the storage has no more sets left, set the selected set & idx to null 
            if (msg.RestraintSetIndex == 0 && _clientConfigs.GetRestraintSetCount() == 0)
            {
                SelectedSet = null!;
                SelectedSetIdx = -1;
            }

            // We've just removed a set, so we want to push an update for other players.
            Mediator.Publish(new PlayerCharWardrobeChanged(DataUpdateKind.WardrobeRestraintOutfitsUpdated));
        });

        Mediator.Subscribe<RestraintSetModified>(this, (msg) =>
        {
            Logger.LogInformation("Set Was modified, updating reference set.");
            // updated selected index too jussssssssst to be sure
            SelectedSetIdx = msg.RestraintSetIndex;
            SelectedSet = _clientConfigs.GetRestraintSet(msg.RestraintSetIndex);
        });

        Mediator.Subscribe<RestraintSetToggledMessage>(this, (msg) =>
        {
            switch (msg.State)
            {
                case UpdatedNewState.Enabled:
                    {
                        // Restraint set was activated, so, lets set the active set after applying the changes
                        _clientConfigs.SetRestraintSetState(msg.State, msg.RestraintSetIndex, msg.AssignerUID);
                        Logger.LogInformation("ActiveSet Enabled at index {0}", msg.RestraintSetIndex);

                        // Call the active set
                        ActiveSet = _clientConfigs.GetActiveSet();

                        // Set the pair to who enabled the set
                        PairWhoEnabledSet = _pairManager.DirectPairs.FirstOrDefault(p => p.UserData.UID == msg.AssignerUID);

                        // See if it has any hardcore properties attached for the UID enabling it
                        if (_clientConfigs.PropertiesEnabledForSet(msg.RestraintSetIndex, msg.AssignerUID))
                        {
                            // We will want to publish a call to start monitoring hardcore actions.
                            Mediator.Publish(new HardcoreRestraintSetEnabledMessage());
                        }

                        // Notify pairs that our active set was just enabled.
                        Mediator.Publish(new PlayerCharWardrobeChanged(DataUpdateKind.WardrobeRestraintApplied));
                    }
                    break;
                case UpdatedNewState.Disabled:
                    {
                        var disabledSet = _clientConfigs.GetRestraintSet(msg.RestraintSetIndex);

                        // If set had hardcore properties, disable monitoring.
                        if (_clientConfigs.PropertiesEnabledForSet(msg.RestraintSetIndex, msg.AssignerUID))
                            Mediator.Publish(new HardcoreRestraintSetDisabledMessage());

                        // REVIEW: Why the fuck is this here?
                        /*_clientConfigs.SetRestraintSetState(msg.State, msg.RestraintSetIndex, msg.AssignerUID);*/

                        // Remove Active Set & Pair that Enabled it.
                        ActiveSet = null!;
                        PairWhoEnabledSet = null!;

                        // Notify pairs that our active set was just disabled.
                        Mediator.Publish(new PlayerCharWardrobeChanged(DataUpdateKind.WardrobeRestraintRemoved));
                    }
                    break;
                case UpdatedNewState.Locked:
                    Mediator.Publish(new PlayerCharWardrobeChanged(DataUpdateKind.WardrobeRestraintLocked));
                    break;
                case UpdatedNewState.Unlocked:
                    Mediator.Publish(new PlayerCharWardrobeChanged(DataUpdateKind.WardrobeRestraintUnlocked));
                    break;
            }

        });

        Mediator.Subscribe<RestraintSetPropertyChanged>(this, (msg) => Logger.LogTrace("Set Hardcore Property changed!"));

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


    // Public Accessors.
    public int SelectedSetIdx { get; set; }
    public RestraintSet SelectedSet { get; private set; }
    public int GetActiveSetIndex() => _clientConfigs.GetActiveSetIdx();
    public List<string> GetRestraintSetsByName() => _clientConfigs.GetRestraintSetNames();
    public RestraintSet GetRestraintSet(int index) => _clientConfigs.GetRestraintSet(index);



    public int GetRestraintSetIndexByName(string setName)
        => _clientConfigs.GetRestraintSetIdxByName(setName);

    public void UpdateRestraintSet(int index, RestraintSet set)
        => _clientConfigs.UpdateRestraintSet(index, set);

    // Player related restraint set updates
    public void EnableRestraintSet(int index, string assignerUID)
        => _clientConfigs.SetRestraintSetState(UpdatedNewState.Enabled, index, assignerUID);

    public void LockRestraintSet(int index, string assignerUID, DateTimeOffset endLockTimeUTC)
        => _clientConfigs.LockRestraintSet(index, assignerUID, endLockTimeUTC);

    public void UnlockRestraintSet(int index, string assignerUID)
        => _clientConfigs.UnlockRestraintSet(index, assignerUID);

    public void DisableRestraintSet(int index, string assignerUID)
        => _clientConfigs.SetRestraintSetState(UpdatedNewState.Disabled, index, assignerUID);

    // Callback related forced restraint set updates.
    public void CallbackForceEnableRestraintSet(string setNameToEnable, string setEnablerUID)
    {
        // Update the reference for the pair who enabled it.
        var matchedPair = _pairManager.DirectPairs.FirstOrDefault(p => p.UserData.UID == setEnablerUID);
        if (matchedPair == null)
        {
            Logger.LogWarning("Pair who enabled the set was not found in the pair list.");
            return;
        }

        Logger.LogInformation($"A paired user has forced you to enable your [{setNameToEnable}] restraint set!");
        // fetch the set idx from the name
        int idx = GetRestraintSetIndexByName(setNameToEnable);
        // enable the set
        _clientConfigs.SetRestraintSetState(UpdatedNewState.Enabled, idx, setEnablerUID, false);

        // Update the ActiveSet, reference in the handler.
        ActiveSet = _clientConfigs.GetActiveSet();

        // Update the reference for the pair who enabled it.
        PairWhoEnabledSet = matchedPair;

        // Update the properties for the set relative to the person who enabled it.
        if (_clientConfigs.PropertiesEnabledForSet(idx, setEnablerUID))
        {
            // Publish a call to start monitoring hardcore actions.
            Mediator.Publish(new HardcoreRestraintSetEnabledMessage());
        }
    }

    public void CallbackForceLockRestraintSet(string setNameToLock, string setLockerUID, DateTimeOffset endLockTimeUTC)
    {
        // Update the reference for the pair who enabled it.
        var matchedPair = _pairManager.DirectPairs.FirstOrDefault(p => p.UserData.UID == setLockerUID);
        if (matchedPair == null)
        {
            Logger.LogWarning("Pair who enabled the set was not found in the pair list.");
            return;
        }

        Logger.LogInformation($"A paired user has forced you to lock your [{setNameToLock}] restraint set!");
        // fetch the set idx from the name
        int idx = GetRestraintSetIndexByName(setNameToLock);
        // lock the set
        _clientConfigs.LockRestraintSet(idx, setLockerUID, endLockTimeUTC, false);
    }

    public void CallbackForceUnlockRestraintSet(string setNameToUnlock, string setUnlockerUID)
    {
        // Update the reference for the pair who enabled it.
        var matchedPair = _pairManager.DirectPairs.FirstOrDefault(p => p.UserData.UID == setUnlockerUID);
        if (matchedPair == null)
        {
            Logger.LogWarning("Pair who enabled the set was not found in the pair list.");
            return;
        }

        Logger.LogInformation($"A paired user has forced you to unlock your [{setNameToUnlock}] restraint set!");
        // fetch the set idx from the name
        int idx = GetRestraintSetIndexByName(setNameToUnlock);
        // unlock the set
        _clientConfigs.UnlockRestraintSet(idx, setUnlockerUID, false);
    }

    public void CallbackForceDisableRestraintSet(string setNameToDisable, string setDisablerUID)
    {
        // Update the reference for the pair who enabled it.
        var matchedPair = _pairManager.DirectPairs.FirstOrDefault(p => p.UserData.UID == setDisablerUID);
        if (matchedPair == null)
        {
            Logger.LogWarning("Pair who enabled the set was not found in the pair list.");
            return;
        }

        Logger.LogInformation($"A paired user has forced you to disable your [{setNameToDisable}] restraint set!");
        // fetch the set idx from the name
        int idx = GetRestraintSetIndexByName(setNameToDisable);
        // disable the set
        _clientConfigs.SetRestraintSetState(UpdatedNewState.Disabled, idx, setDisablerUID, false);

        if (_clientConfigs.PropertiesEnabledForSet(idx, setDisablerUID))
        {
            // Publish a call to stop monitoring hardcore actions.
            Mediator.Publish(new HardcoreRestraintSetDisabledMessage());
        }

        // set active set references to null.
        ActiveSet = null!;
        PairWhoEnabledSet = null!;
    }

    public int RestraintSetCount()
        => _clientConfigs.GetRestraintSetCount();

    public void AddRestraintSet(RestraintSet set)
        => _clientConfigs.AddNewRestraintSet(set);

    public void RemoveRestraintSet(int index)
        => _clientConfigs.RemoveRestraintSet(index);

    public bool HardcorePropertiesEnabledForSet(int index, string UidToExamine)
        => _clientConfigs.PropertiesEnabledForSet(index, UidToExamine);

    public List<AssociatedMod> GetAssociatedMods(int setIndex)
        => _clientConfigs.GetAssociatedMods(setIndex);

    public void RemoveAssociatedMod(int setIndex, Mod mod)
        => _clientConfigs.RemoveAssociatedMod(setIndex, mod);

    public void UpdateAssociatedMod(int setIndex, AssociatedMod mod)
        => _clientConfigs.UpdateAssociatedMod(setIndex, mod);

    public void AddAssociatedMod(int setIndex, AssociatedMod mod)
        => _clientConfigs.AddAssociatedMod(setIndex, mod);

    public bool IsBlindfoldActive()
        => _clientConfigs.IsBlindfoldActive();

    public void EnableBlindfold(string ApplierUID)
        => _clientConfigs.SetBlindfoldState(true, ApplierUID);

    public void DisableBlindfold(string ApplierUID)
        => _clientConfigs.SetBlindfoldState(false, "");

    public EquipDrawData GetBlindfoldDrawData()
        => _clientConfigs.GetBlindfoldItem();

    public void SetBlindfoldDrawData(EquipDrawData drawData)
        => _clientConfigs.SetBlindfoldItem(drawData);

}
