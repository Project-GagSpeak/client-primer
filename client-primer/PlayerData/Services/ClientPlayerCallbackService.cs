using Dalamud.Utility;
using GagSpeak.GagspeakConfiguration.Models;
using GagSpeak.Interop.Ipc;
using GagSpeak.PlayerData.Data;
using GagSpeak.PlayerData.Handlers;
using GagSpeak.PlayerData.Pairs;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using GagSpeak.Toybox.Services;
using GagspeakAPI.Data.Character;
using GagspeakAPI.Data.Permissions;
using GagspeakAPI.Dto.Connection;
using GagspeakAPI.Dto.IPC;
using GagspeakAPI.Dto.Permissions;
using GagspeakAPI.Dto.User;
using GagspeakAPI.Extensions;
using Glamourer.Api.IpcSubscribers.Legacy;
using Microsoft.Extensions.FileSystemGlobbing.Internal;
using static Lumina.Data.Parsing.Layer.LayerCommon;

namespace GagSpeak.PlayerData.Services;

// A class to help with callbacks received from the server.
public class ClientCallbackService
{
    private readonly ILogger<ClientCallbackService> _logger;
    private readonly GagspeakMediator _mediator;
    private readonly PlayerCharacterData _playerData;
    private readonly WardrobeHandler _wardrobeHandler;
    private readonly ClientConfigurationManager _clientConfigs;
    private readonly PairManager _pairManager;
    private readonly GagManager _gagManager;
    private readonly IpcManager _ipcManager;
    private readonly IpcFastUpdates _ipcFastUpdates;
    private readonly AppearanceHandler _appearanceHandler;
    private readonly PlaybackService _playbackService;

    public ClientCallbackService(ILogger<ClientCallbackService> logger,
        GagspeakMediator mediator, ClientConfigurationManager clientConfigs,
        PlayerCharacterData playerData, WardrobeHandler wardrobeHandler,
        PairManager pairManager, GagManager gagManager, IpcManager ipcManager, 
        IpcFastUpdates ipcFastUpdates, AppearanceHandler appearanceHandler, 
        PlaybackService playbackService)
    {
        _logger = logger;
        _mediator = mediator;
        _clientConfigs = clientConfigs;
        _playerData = playerData;
        _wardrobeHandler = wardrobeHandler;
        _pairManager = pairManager;
        _gagManager = gagManager;
        _ipcManager = ipcManager;
        _ipcFastUpdates = ipcFastUpdates;
        _appearanceHandler = appearanceHandler;
        _playbackService = playbackService;
    }

    public bool ShockCodePresent => _playerData.CoreDataNull && _playerData.GlobalPerms!.GlobalShockShareCode.IsNullOrEmpty();
    public string GlobalPiShockShareCode => _playerData.GlobalPerms!.GlobalShockShareCode;
    public void SetGlobalPerms(UserGlobalPermissions perms) => _playerData.GlobalPerms = perms;
    public void SetAppearanceData(CharacterAppearanceData appearanceData) => _playerData.AppearanceData = appearanceData;
    public void ApplyGlobalPerm(UserGlobalPermChangeDto dto) => _playerData.ApplyGlobalPermChange(dto);
    private bool CanDoWardrobeInteract() => !_playerData.CoreDataNull && _playerData.GlobalPerms!.WardrobeEnabled && _playerData.GlobalPerms.RestraintSetAutoEquip;

    #region IPC Callbacks
    public async void ApplyStatusesByGuid(ApplyMoodlesByGuidDto dto)
    {
        if (!_pairManager.GetVisibleUsers().Select(u => u.UID).Contains(dto.User.UID))
        {
            _logger.LogError("Received Update by player is no longer present.");
            return;
        }
        await _ipcManager.Moodles.ApplyOwnStatusByGUID(dto.Statuses).ConfigureAwait(false);
    }

    public async void ApplyStatusesToSelf(ApplyMoodlesByStatusDto dto, string clientPlayerNameWithWorld)
    {
        string nameWithWorldOfApplier = _pairManager.DirectPairs.FirstOrDefault(p => p.UserData.UID == dto.User.UID)?.PlayerNameWithWorld ?? string.Empty;
        if (nameWithWorldOfApplier.IsNullOrEmpty())
        {
            _logger.LogError("Received Update by player is no longer present.");
            return;
        }
        await _ipcManager.Moodles.ApplyStatusesFromPairToSelf(nameWithWorldOfApplier, clientPlayerNameWithWorld, dto.Statuses).ConfigureAwait(false);
    }

    public async void RemoveStatusesFromSelf(RemoveMoodlesDto dto)
    {
        if (!_pairManager.GetVisibleUsers().Select(u => u.UID).Contains(dto.User.UID))
        {
            _logger.LogError("Received Update by player is no longer present.");
            return;
        }
        await _ipcManager.Moodles.RemoveOwnStatusByGuid(dto.Statuses).ConfigureAwait(false);
    }

    public async void ClearStatusesFromSelf(UserDto dto)
    {
        if (!_pairManager.GetVisibleUsers().Select(u => u.UID).Contains(dto.User.UID))
        {
            _logger.LogError("Received Update by player is no longer present.");
            return;
        }

        if (!_pairManager.DirectPairs.First(p => p.UserData.UID == dto.User.UID).UserPairUniquePairPerms.AllowRemovingMoodles)
        {
            _logger.LogError("Player does not have permission to clear their own moodles.");
            return;
        }
        await _ipcManager.Moodles.ClearStatusAsync().ConfigureAwait(false);
    }
    #endregion IPC Callbacks


    public async void CallbackAppearanceUpdate(OnlineUserCharaAppearanceDataDto callbackDto, bool callbackWasFromSelf)
    {
        if (_playerData.CoreDataNull) return;

        NewState callbackGagState = callbackDto.UpdateKind.ToNewState();
        GagLayer callbackGagLayer = callbackDto.UpdateKind.ToSlot();
        var currentGagType = _playerData.AppearanceData!.GagSlots[(int)callbackGagLayer].GagType.ToGagType();

        if (callbackWasFromSelf)
        {
            if (callbackGagState is NewState.Enabled)
            {
                _logger.LogDebug("SelfApplied GAG APPLY Verified by Server Callback.", LoggerType.Callbacks);
                UnlocksEventManager.AchievementEvent(UnlocksEvent.GagAction, 
                    callbackGagLayer, 
                    callbackDto.AppearanceData.GagSlots[(int)callbackGagLayer].GagType.ToGagType(), 
                    true);
                return;
            }

            if(callbackGagState is NewState.Locked)
                _logger.LogDebug("SelfApplied GAG LOCK Verified by Server Callback.", LoggerType.Callbacks);

            if (callbackGagState is NewState.Unlocked)
            {
                _logger.LogDebug("SelfApplied GAG UNLOCK Verified by Server Callback.", LoggerType.Callbacks);
                // If the gagType is not none, 
                if (callbackDto.AppearanceData.GagSlots[(int)callbackGagLayer].GagType.ToGagType() is not GagType.None)
                {
                    // This means the gag is still applied, so we should see if we want to auto remove it.
                    _logger.LogDebug("Gag is still applied. Checking if we should remove it.", LoggerType.Callbacks);
                    if (_clientConfigs.GagspeakConfig.RemoveGagUponLockExpiration)
                        await _appearanceHandler.GagRemoved(callbackGagLayer, currentGagType, isSelfApplied: true);
                }
                else
                {
                    _logger.LogTrace("Gag is already removed. No need to remove again. Update ClientSide Only", LoggerType.Callbacks);
                    // The GagType is none, meaning this was removed via a mimic, so only update client side removal
                    await _appearanceHandler.GagRemoved(callbackGagLayer, currentGagType, publishRemoval: false, isSelfApplied: true);
                }
            }

            if (callbackGagState is NewState.Disabled)
            {
                _logger.LogDebug("SelfApplied GAG DISABLED Verified by Server Callback.", LoggerType.Callbacks);
                UnlocksEventManager.AchievementEvent(UnlocksEvent.GagRemoval, callbackGagLayer, currentGagType, true);
            }
            return;
        }

        // This is just for automatically removing a gag once it's unlocked.
        if (callbackWasFromSelf)
        {
            _logger.LogTrace($"Callback for self-appearance data from server handled successfully.", LoggerType.Callbacks);
            if (!_clientConfigs.GagspeakConfig.RemoveGagUponLockExpiration) return;

            var layer = callbackDto.UpdateKind switch
            {
                DataUpdateKind.AppearanceGagUnlockedLayerOne => GagLayer.UnderLayer,
                DataUpdateKind.AppearanceGagUnlockedLayerTwo => GagLayer.MiddleLayer,
                DataUpdateKind.AppearanceGagUnlockedLayerThree => GagLayer.TopLayer,
                _ => (GagLayer?)null
            };

            if (layer.HasValue)
            {
                _gagManager.OnGagTypeChanged(layer.Value, GagType.None, false);
            }
            return;
        }

        var callbackGagSlot = callbackDto.AppearanceData.GagSlots[(int)callbackGagLayer];

        _logger.LogDebug(
            "Callback State: "+callbackGagState +
            " | Callback Layer: "+callbackGagLayer +
            " | Callback GagType: "+callbackGagSlot.GagType +
            " | Current GagType: "+currentGagType, LoggerType.Callbacks);

        // let's start handling the cases. For starters, if the NewState is apply..
        if (callbackGagState is NewState.Enabled)
        {
            // handle the case where we need to reapply, then...
            if (_playerData.AppearanceData!.GagSlots[(int)callbackGagLayer].GagType.ToGagType() != GagType.None)
            {
                _logger.LogDebug("Gag is already applied. Swapping Gag.", LoggerType.Callbacks);
                await _appearanceHandler.GagSwapped(callbackGagLayer, currentGagType, callbackGagSlot.GagType.ToGagType(), isSelfApplied: false);
            }
            else
            {
                // Apply Gag
                _logger.LogDebug("Applying Gag to Character Appearance.", LoggerType.Callbacks);
                await _appearanceHandler.GagApplied(callbackGagLayer, callbackGagSlot.GagType.ToGagType(), isSelfApplied: false);
            }
        }
        else if (callbackGagState is NewState.Locked)
        {
            _logger.LogTrace("A Padlock has been applied that will expire in : " + (callbackGagSlot.Timer - DateTime.UtcNow).TotalSeconds, LoggerType.Callbacks);
            var padlockData = new PadlockData(callbackGagLayer, callbackGagSlot.Padlock.ToPadlock(), callbackGagSlot.Password, callbackGagSlot.Timer, callbackDto.User.UID);
            _gagManager.OnGagLockChanged(padlockData, callbackGagState, false);
        }
        else if (callbackGagState is NewState.Unlocked)
        {
            var padlockData = new PadlockData(callbackGagLayer, callbackGagSlot.Padlock.ToPadlock(), callbackGagSlot.Password, callbackGagSlot.Timer, callbackDto.User.UID);
            _gagManager.OnGagLockChanged(padlockData, callbackGagState, false);
        }
        else if (callbackGagState is NewState.Disabled)
        {
            await _appearanceHandler.GagRemoved(callbackGagLayer, currentGagType, isSelfApplied: false);
        }
    }

    public async void CallbackWardrobeUpdate(OnlineUserCharaWardrobeDataDto callbackDto, bool callbackWasFromSelf)
    {
        var data = callbackDto.WardrobeData;
        int callbackSetIdx = _clientConfigs.GetRestraintSetIdxByName(data.ActiveSetName);
        RestraintSet? callbackSet = null;
        if(callbackSetIdx is not -1) callbackSet = _clientConfigs.GetRestraintSet(callbackSetIdx);

        if (callbackWasFromSelf)
        {
            if (callbackDto.UpdateKind is DataUpdateKind.WardrobeRestraintApplied)
            {
                _logger.LogDebug("SelfApplied RESTRAINT APPLY Verified by Server Callback.", LoggerType.Callbacks);
                // Achievement Event Trigger
                if(callbackSet != null)
                    UnlocksEventManager.AchievementEvent(UnlocksEvent.RestraintApplicationChanged, callbackSet, true, Globals.SelfApplied);
            }

            if (callbackDto.UpdateKind is DataUpdateKind.WardrobeRestraintLocked)
            {
                _logger.LogDebug("SelfApplied RESTRAINT LOCKED Verified by Server Callback.", LoggerType.Callbacks);
                // Achievement Event Trigger
                UnlocksEventManager.AchievementEvent(UnlocksEvent.RestraintLockChange, callbackSet, data.Padlock.ToPadlock(), true, Globals.SelfApplied);
            }

            if (callbackDto.UpdateKind is DataUpdateKind.WardrobeRestraintUnlocked)
            {
                _logger.LogDebug("SelfApplied RESTRAINT UNLOCK Verified by Server Callback.", LoggerType.Callbacks);

                if (_playerData.CoreDataNull || !_playerData.GlobalPerms!.RestraintSetAutoEquip)
                    return;

                // fire trigger if valid
                if(callbackSet != null)
                {
                    Padlocks unlock = callbackSet.LockType.ToPadlock();
                    UnlocksEventManager.AchievementEvent(UnlocksEvent.RestraintLockChange, callbackSet, unlock, false, Globals.SelfApplied);
                }

                // auto remove the restraint set after unlocking if we have just finished unlocking it.
                if (!_clientConfigs.GagspeakConfig.DisableSetUponUnlock) 
                    return;

                await _wardrobeHandler.DisableRestraintSet(callbackSetIdx, Globals.SelfApplied, true);
            }

            if (callbackDto.UpdateKind is DataUpdateKind.WardrobeRestraintDisabled)
            {
                _logger.LogDebug("SelfApplied RESTRAINT DISABLED Verified by Server Callback.", LoggerType.Callbacks);
            }
            return;
        }

        ////////// Callback was not from self past this point.

        var matchedPair = _pairManager.DirectPairs.FirstOrDefault(p => p.UserData.UID == callbackDto.User.UID);
        if (matchedPair == null) {
            _logger.LogError("Received Update by player is no longer present.");
            return;
        }

        if (!CanDoWardrobeInteract()) {
            _logger.LogError("Player does not have permission to update their own wardrobe.");
            return;
        }

        try
        {
            switch (callbackDto.UpdateKind)
            {
                case DataUpdateKind.WardrobeRestraintOutfitsUpdated:
                    _logger.LogError("Unexpected UpdateKind: WardrobeRestraintOutfitsUpdated.");
                    break;

                case DataUpdateKind.WardrobeRestraintApplied:
                    _logger.LogDebug($"{callbackDto.User.UID} has forcibly applied your [{data.ActiveSetName}] restraint set!", LoggerType.Callbacks);
                    await _wardrobeHandler.EnableRestraintSet(callbackSetIdx, callbackDto.User.UID, false);

                    UnlocksEventManager.AchievementEvent(UnlocksEvent.RestraintApplicationChanged, callbackSet, true, callbackDto.User.UID);
                    break;

                case DataUpdateKind.WardrobeRestraintLocked:
                    _logger.LogDebug($"{callbackDto.User.UID} has forcibly locked your [{data.ActiveSetName}] restraint set!", LoggerType.Callbacks);
                    _clientConfigs.LockRestraintSet(callbackSetIdx, data.Padlock, data.Password, data.Timer, callbackDto.User.UID, false);

                    UnlocksEventManager.AchievementEvent(UnlocksEvent.RestraintLockChange, callbackSet, data.Padlock.ToPadlock(), true, callbackDto.User.UID); 
                    break;

                case DataUpdateKind.WardrobeRestraintUnlocked:
                    _logger.LogDebug($"{callbackDto.User.UID} has force unlocked your [{data.ActiveSetName}] restraint set!", LoggerType.Callbacks);
                    _clientConfigs.UnlockRestraintSet(callbackSetIdx, callbackDto.User.UID, false);
                    if(callbackSet != null)
                    {
                        Padlocks unlock = callbackSet.LockType.ToPadlock();
                        UnlocksEventManager.AchievementEvent(UnlocksEvent.RestraintLockChange, callbackSet, unlock, false, callbackDto.User.UID);
                    }
                    break;

                case DataUpdateKind.WardrobeRestraintDisabled:
                    _logger.LogDebug($"{callbackDto.User.UID} has force disabled your restraint set!", LoggerType.Callbacks);
                    var activeIdx = _clientConfigs.GetActiveSetIdx();
                    var activeSet = _clientConfigs.GetRestraintSet(activeIdx);
                    if (activeIdx != -1)
                    {
                        await _wardrobeHandler.DisableRestraintSet(activeIdx, callbackDto.User.UID, false);
                        UnlocksEventManager.AchievementEvent(UnlocksEvent.RestraintApplicationChanged, activeSet, false, callbackDto.User.UID);
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while processing Wardrobe Update.");
        }

    }

    public void CallbackAliasStorageUpdate(OnlineUserCharaAliasDataDto callbackDto)
    {
        // this call should only ever be used for updating the registered name of a pair. if used for any other purpose, log error.
        if (callbackDto.UpdateKind is DataUpdateKind.PuppeteerPlayerNameRegistered)
        {
            // do the update for name registration of this pair.
            _mediator.Publish(new UpdateCharacterListenerForUid(callbackDto.User.UID, callbackDto.AliasData.CharacterName, callbackDto.AliasData.CharacterWorld));
            _logger.LogDebug("Player Name Registered Successfully processed by Server!", LoggerType.Callbacks);
        }
        else if (callbackDto.UpdateKind is DataUpdateKind.PuppeteerAliasListUpdated)
        {
            _logger.LogDebug("Alias List Update Success retrieved from Server for UID: " + callbackDto.User.UID, LoggerType.Callbacks);
        }
        else
        {
            _logger.LogError("Another Player should not be attempting to update your own alias list. Report this if you see it.", LoggerType.Callbacks);
            return;
        }
    }

    public void CallbackToyboxUpdate(OnlineUserCharaToyboxDataDto callbackDto)
    {
        // One of our pairs has just forced us to change a setting (we know it is because the server-side validates this)

        // Update Appearance without calling any events so we don't loop back to the server.
        switch (callbackDto.UpdateKind)
        {
            case DataUpdateKind.ToyboxPatternExecuted:
                {
                    var patternId = callbackDto.ToyboxInfo.ActivePatternGuid;
                    var patternData = _clientConfigs.FetchPatternById(patternId);
                    if (patternData == null)
                    {
                        _logger.LogError("Tried to activate pattern but pattern does not exist? How is this even possible.");
                        return;
                    }
                    _playbackService.PlayPattern(patternData.UniqueIdentifier, patternData.StartPoint, patternData.Duration, false);
                    UnlocksEventManager.AchievementEvent(UnlocksEvent.PatternAction, PatternInteractionKind.Started, patternData.UniqueIdentifier, false);
                    _logger.LogInformation("Pattern Executed by Server Callback.", LoggerType.Callbacks);
                }
                break;
            case DataUpdateKind.ToyboxPatternStopped:
                {
                    var patternId = callbackDto.ToyboxInfo.ActivePatternGuid;
                    var patternData = _clientConfigs.FetchPatternById(patternId);
                    if (patternData == null)
                    {
                        _logger.LogError("Tried to activate pattern but pattern does not exist? How is this even possible.");
                        return;
                    }
                    _playbackService.StopPattern(patternId, false);
                    UnlocksEventManager.AchievementEvent(UnlocksEvent.PatternAction, PatternInteractionKind.Stopped, patternData.UniqueIdentifier, false);
                    _logger.LogInformation("Pattern Stopped by Server Callback.", LoggerType.Callbacks);
                }
                break;
            case DataUpdateKind.ToyboxAlarmListUpdated:
                _logger.LogInformation("ToyboxAlarmListUpdated Callback Received...", LoggerType.Callbacks);
                break;
            case DataUpdateKind.ToyboxAlarmToggled:
                _logger.LogInformation("ToyboxAlarmToggled Callback Received...", LoggerType.Callbacks);
                break;
            case DataUpdateKind.ToyboxTriggerListUpdated:
                _logger.LogInformation("ToyboxTriggerListUpdated Callback Received...", LoggerType.Callbacks);
                break;
            case DataUpdateKind.ToyboxTriggerToggled:
                _logger.LogInformation("ToyboxTriggerToggled Callback Received...", LoggerType.Callbacks);
                break;
        }
    }

}
