using GagSpeak.GagspeakConfiguration.Models;
using GagSpeak.Interop.Ipc;
using GagSpeak.PlayerData.Data;
using GagSpeak.PlayerData.Pairs;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using GagSpeak.Utils;
using GagspeakAPI.Data.Character;
using GagspeakAPI.Data.Enum;
using GagspeakAPI.Dto.Connection;
using static PInvoke.User32;

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

    public WardrobeHandler(ILogger<WardrobeHandler> logger, GagspeakMediator mediator,
        ClientConfigurationManager clientConfiguration, PlayerCharacterManager playerManager, 
        PairManager pairManager) : base(logger, mediator)
    {
        _clientConfigs = clientConfiguration;
        _playerManager = playerManager;
        _pairManager = pairManager;

        Mediator.Subscribe<RestraintSetToggledMessage>(this, (msg) =>
        {
            // handle changes
            if (msg.State == NewState.Enabled)
            {
                Logger.LogInformation("ActiveSet Enabled at index {0}", msg.SetIdx);
                ActiveSet = _clientConfigs.GetActiveSet();
                Mediator.Publish(new UpdateGlamourRestraintsMessage(NewState.Enabled, msg.GlamourChangeTask));
            }

            if (msg.State == NewState.Disabled)
            {
                Logger.LogInformation("ActiveSet Disabled at index {0}", msg.SetIdx);
                Mediator.Publish(new UpdateGlamourRestraintsMessage(NewState.Disabled, msg.GlamourChangeTask));
                ActiveSet = null!;
            }

            // handle the updates if we should
            if (!msg.pushChanges) return;

            // push the wardrobe change
            switch (msg.State)
            {
                case NewState.Enabled: Mediator.Publish(new PlayerCharWardrobeChanged(DataUpdateKind.WardrobeRestraintApplied)); break;
                case NewState.Locked: Mediator.Publish(new PlayerCharWardrobeChanged(DataUpdateKind.WardrobeRestraintLocked)); break;
                case NewState.Unlocked: Mediator.Publish(new PlayerCharWardrobeChanged(DataUpdateKind.WardrobeRestraintUnlocked)); break;
                case NewState.Disabled: Mediator.Publish(new PlayerCharWardrobeChanged(DataUpdateKind.WardrobeRestraintDisabled)); break;
            }
        });

        Mediator.Subscribe<DelayedFrameworkUpdateMessage>(this, (_) => CheckLockedSet());

    }

    /// <summary> The current restraint set that is active on the client. Null if none. </summary>
    public RestraintSet ActiveSet { get; private set; }

    // Store an accessor of the alarm being edited.
    private RestraintSet? _setBeingEdited;
    public int EditingSetIndex { get; private set; } = -1;
    public RestraintSet SetBeingEdited
    {
        get
        {
            if (_setBeingEdited == null && EditingSetIndex >= 0)
            {
                _setBeingEdited = _clientConfigs.GetRestraintSet(EditingSetIndex);
            }
            return _setBeingEdited!;
        }
        private set => _setBeingEdited = value;
    }
    public bool EditingSetNull => SetBeingEdited == null;

    public bool WardrobeEnabled => _playerManager.GlobalPerms != null && _playerManager.GlobalPerms.WardrobeEnabled;
    public bool RestraintSetsEnabled => _playerManager.GlobalPerms != null && _playerManager.GlobalPerms.RestraintSetAutoEquip;

    public void SetEditingRestraintSet(RestraintSet set)
    {
        SetBeingEdited = set;
        EditingSetIndex = GetRestraintSetIndexByName(set.Name);
    }

    public void ClearEditingRestraintSet()
    {
        EditingSetIndex = -1;
        SetBeingEdited = null!;
    }

    public void UpdateActiveSet()
        => ActiveSet = _clientConfigs.GetActiveSet();

    public void UpdateEditedRestraintSet()
    {
        // update the set in the client configs
        _clientConfigs.UpdateRestraintSet(EditingSetIndex, SetBeingEdited);
        // clear the editing set
        ClearEditingRestraintSet();
    }

    public void AddNewRestraintSet(RestraintSet newSet)
        => _clientConfigs.AddNewRestraintSet(newSet);

    public void RemoveRestraintSet(int idxToRemove)
    {
        _clientConfigs.RemoveRestraintSet(idxToRemove);
        ClearEditingRestraintSet();
    }

    public int RestraintSetListSize()
        => _clientConfigs.GetRestraintSetCount();

    public List<RestraintSet> GetAllSetsForSearch()
        => _clientConfigs.StoredRestraintSets;
    public RestraintSet GetRestraintSet(int idx)
        => _clientConfigs.GetRestraintSet(idx);

    public async void EnableRestraintSet(int idx, string AssignerUID = "SelfApplied")
    {
        if(!WardrobeEnabled || !RestraintSetsEnabled)
        {
            Logger.LogInformation("Wardrobe or Restraint Sets are disabled, cannot enable restraint set.");
            return;
        }
        await _clientConfigs.SetRestraintSetState(idx, AssignerUID, NewState.Enabled, true);
    }
    public async void DisableRestraintSet(int idx, string AssignerUID = "SelfApplied")
    {
        if (!WardrobeEnabled || !RestraintSetsEnabled)
        {
            Logger.LogInformation("Wardrobe or Restraint Sets are disabled, cannot disable restraint set.");
            return;
        }
        await _clientConfigs.SetRestraintSetState(idx, AssignerUID, NewState.Disabled, true);
    }
    
    public void LockRestraintSet(int idx, string lockType, string password, DateTimeOffset endLockTimeUTC, string AssignerUID)
        => _clientConfigs.LockRestraintSet(idx, lockType, password, endLockTimeUTC, AssignerUID, true);

    public void UnlockRestraintSet(int idx, string AssignerUID)
        => _clientConfigs.UnlockRestraintSet(idx, AssignerUID, true);

    public int GetActiveSetIndex() 
        => _clientConfigs.GetActiveSetIdx();

    public List<string> GetRestraintSetsByName() 
        => _clientConfigs.GetRestraintSetNames();

    public int GetRestraintSetIndexByName(string setName)
        => _clientConfigs.GetRestraintSetIdxByName(setName);

    public List<AssociatedMod> GetAssociatedMods(int setIndex)
        => _clientConfigs.GetAssociatedMods(setIndex);

    public List<Guid> GetAssociatedMoodles(int setIndex)
        => _clientConfigs.GetAssociatedMoodles(setIndex);
    
    public EquipDrawData GetBlindfoldDrawData()
        => _clientConfigs.GetBlindfoldItem();

    public void SetBlindfoldDrawData(EquipDrawData drawData)
        => _clientConfigs.SetBlindfoldItem(drawData);

    /// <summary>
    /// On each delayed framework check, verifies if the active restraint set has had its lock expire, and if so to unlock it.
    /// </summary>
    private void CheckLockedSet()
    {
        if (ActiveSet == null) return;

        // we have an active set, but dont check if it is not locked. We should keep it on if it is simply active.
        if (!ActiveSet.Locked) return;

        // otherwise, set is both active and locked, so check for unlock
        try
        {
            // check if the locked time minus the current time in UTC is less than timespan.Zero ... if it is, we should push an unlock set update.
            if (GenericHelpers.TimerPadlocks.Contains(ActiveSet.LockType) && ActiveSet.LockedUntil - DateTimeOffset.UtcNow <= TimeSpan.Zero)
            {
                UnlockRestraintSet(GetActiveSetIndex(), ActiveSet.LockedBy);
                Logger.LogInformation("Active Set [{0}] has expired its lock, unlocking and removing restraint set.", ActiveSet.Name);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to check locked set for expiration.");
        }
    }
}
