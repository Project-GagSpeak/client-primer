using GagSpeak.GagspeakConfiguration.Models;
using GagSpeak.Interop.Ipc;
using GagSpeak.PlayerData.Pairs;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
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
    private readonly PairManager _pairManager;

    public WardrobeHandler(ILogger<WardrobeHandler> logger, GagspeakMediator mediator,
        ClientConfigurationManager clientConfiguration, PairManager pairManager)
        : base(logger, mediator)
    {
        _clientConfigs = clientConfiguration;
        _pairManager = pairManager;

        Mediator.Subscribe<RestraintSetToggledMessage>(this, (msg) =>
        {
            // handle changes
            if (msg.State == NewState.Enabled)
            {
                Logger.LogInformation("ActiveSet Enabled at index {0}", msg.SetIdx);
                ActiveSet = _clientConfigs.GetActiveSet();
                Mediator.Publish(new UpdateGlamourRestraintsMessage(NewState.Enabled));
            }

            if (msg.State == NewState.Disabled)
            {
                Logger.LogInformation("ActiveSet Disabled at index {0}", msg.SetIdx);
                Mediator.Publish(new UpdateGlamourRestraintsMessage(NewState.Disabled));
                ActiveSet = null!;
            }

            if (msg.GlamourChangeTask != null)
            {
                Logger.LogInformation("GlamourChangeTask SetResult(true)");
                msg.GlamourChangeTask.SetResult(true);
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

    public async void EnableRestraintSet(int idx, string AssignerUID = "SelfApplied") // a self-enable
        => await _clientConfigs.SetRestraintSetState(idx, AssignerUID, NewState.Enabled, true);

    public async void DisableRestraintSet(int idx, string AssignerUID = "SelfApplied") // a self-disable
        => await _clientConfigs.SetRestraintSetState(idx, AssignerUID, NewState.Disabled, true);
    public void LockRestraintSet(int idx, string lockType, string password, DateTimeOffset endLockTimeUTC, string AssignerUID)
        => _clientConfigs.LockRestraintSet(idx, lockType, password, endLockTimeUTC, AssignerUID, true);

    public void UnlockRestraintSet(int idx, string AssignerUID)
        => _clientConfigs.UnlockRestraintSet(idx, AssignerUID, true);

    public int GetActiveSetIndex() 
        => _clientConfigs.GetActiveSetIdx();

    public bool IsBlindfoldActive()
        => _clientConfigs.IsBlindfoldActive();

    public List<string> GetRestraintSetsByName() 
        => _clientConfigs.GetRestraintSetNames();

    public int GetRestraintSetIndexByName(string setName)
        => _clientConfigs.GetRestraintSetIdxByName(setName);

    public void UpdateRestraintSet(int index, RestraintSet set)
        => _clientConfigs.UpdateRestraintSet(index, set);

    // Callback related forced restraint set updates.
    public async void CallbackForceEnableRestraintSet(OnlineUserCharaWardrobeDataDto callbackDto)
    {
        // Update the reference for the pair who enabled it.
        var matchedPair = _pairManager.DirectPairs.FirstOrDefault(p => p.UserData.UID == callbackDto.User.UID);
        if (matchedPair == null) throw new Exception("Pair who enabled the set was not found in the pair list.");

        Logger.LogInformation($"{callbackDto.User.UID} has forced you to enable your [{callbackDto.WardrobeData.ActiveSetName}] restraint set!");
        int idx = GetRestraintSetIndexByName(callbackDto.WardrobeData.ActiveSetName);
        // This might need to be merged and always callback because of how we switch sets?
        await _clientConfigs.SetRestraintSetState(idx, callbackDto.User.UID, NewState.Enabled, false);
    }

    public void CallbackForceLockRestraintSet(OnlineUserCharaWardrobeDataDto callbackDto)
    {
        // Update the reference for the pair who enabled it.
        var matchedPair = _pairManager.DirectPairs.FirstOrDefault(p => p.UserData.UID == callbackDto.User.UID);
        if (matchedPair == null) throw new Exception("Pair who enabled the set was not found in the pair list.");

        Logger.LogInformation($"{callbackDto.User.UID} has locked your [{callbackDto.WardrobeData.ActiveSetName}] restraint set!");
        int idx = GetRestraintSetIndexByName(callbackDto.WardrobeData.ActiveSetName);
        _clientConfigs.LockRestraintSet(idx, callbackDto.WardrobeData.WardrobeActiveSetPadLock,
            callbackDto.WardrobeData.WardrobeActiveSetPassword,
            callbackDto.WardrobeData.WardrobeActiveSetLockTime, callbackDto.User.UID, false);
    }

    public void CallbackForceUnlockRestraintSet(OnlineUserCharaWardrobeDataDto callbackDto)
    {
        // Update the reference for the pair who enabled it.
        var matchedPair = _pairManager.DirectPairs.FirstOrDefault(p => p.UserData.UID == callbackDto.User.UID);
        if (matchedPair == null) throw new Exception("Pair who enabled the set was not found in the pair list.");

        Logger.LogInformation($"{callbackDto.User.UID} has forced you to unlock your [{callbackDto.WardrobeData.ActiveSetName}] restraint set!");
        int idx = GetRestraintSetIndexByName(callbackDto.WardrobeData.ActiveSetName);
        _clientConfigs.UnlockRestraintSet(idx, callbackDto.User.UID, false);
    }

    public async void CallbackForceDisableRestraintSet(OnlineUserCharaWardrobeDataDto callbackDto)
    {
        // Update the reference for the pair who enabled it.
        var matchedPair = _pairManager.DirectPairs.FirstOrDefault(p => p.UserData.UID == callbackDto.User.UID);
        if (matchedPair == null) throw new Exception("Pair who enabled the set was not found in the pair list.");

        Logger.LogInformation($"{callbackDto.User.UID} has disabled your Active restraint set!");
        int idx = GetActiveSetIndex();
        await _clientConfigs.SetRestraintSetState(idx, callbackDto.User.UID, NewState.Disabled, false);
    }

    public List<AssociatedMod> GetAssociatedMods(int setIndex)
        => _clientConfigs.GetAssociatedMods(setIndex);


    // Replace these with the same edit & save approach as the others.
    public void EnableBlindfold(string ApplierUID)
        => _clientConfigs.SetBlindfoldState(true, ApplierUID);

    public void DisableBlindfold(string ApplierUID)
        => _clientConfigs.SetBlindfoldState(false, "");

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
            if (ActiveSet.LockedUntil - DateTimeOffset.UtcNow < TimeSpan.Zero)
            {
                // wont madder if we use LockedBy for name when pushing own update because we don't
                // ensure they match server-side for self owned validation.
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
