using Dalamud.Utility;
using GagSpeak.Interop.Ipc;
using GagSpeak.PlayerData.Data;
using GagSpeak.PlayerData.Pairs;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using GagSpeak.Toybox.Services;
using GagspeakAPI.Data.Character;
using GagspeakAPI.Enums;
using GagspeakAPI.Extensions;
using GagspeakAPI.Data.Permissions;
using GagspeakAPI.Dto.Connection;
using GagspeakAPI.Dto.IPC;
using GagspeakAPI.Dto.Permissions;
using GagspeakAPI.Dto.User;

namespace GagSpeak.PlayerData.Services;

// A class to help with callbacks received from the server.
public class ClientCallbackService
{
    private readonly ILogger<ClientCallbackService> _logger;
    private readonly GagspeakMediator _mediator;
    private readonly ClientConfigurationManager _clientConfigs;
    private readonly PlayerCharacterData _playerData;
    private readonly PairManager _pairManager;
    private readonly GagManager _gagManager;
    private readonly IpcManager _ipcManager;
    private readonly IpcFastUpdates _ipcFastUpdates;
    private readonly AppearanceChangeService _visualUpdater;
    private readonly PlaybackService _playbackService;

    public ClientCallbackService(ILogger<ClientCallbackService> logger,
        GagspeakMediator mediator, ClientConfigurationManager clientConfigs,
        PlayerCharacterData playerData, PairManager pairManager,
        GagManager gagManager, IpcManager ipcManager, IpcFastUpdates ipcFastUpdates,
        AppearanceChangeService visualUpdater, PlaybackService playbackService)
    {
        _logger = logger;
        _mediator = mediator;
        _clientConfigs = clientConfigs;
        _playerData = playerData;
        _pairManager = pairManager;
        _gagManager = gagManager;
        _ipcManager = ipcManager;
        _ipcFastUpdates = ipcFastUpdates;
        _visualUpdater = visualUpdater;
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

        if (callbackWasFromSelf)
        {
            _logger.LogDebug("Appearance Update Verified by Server Callback.");
            return;
        }
        // I honestly forgot why i ever had this but i guess ill find out when debugging later.
        /*
        if (callbackWasFromSelf)
        {
            _logger.LogTrace($"Callback for self-appearance data from server handled successfully.");
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
                Mediator.Publish(new GagTypeChanged(GagType.None, layer.Value));
            }
            return;
        }*/


        // we should start by extracting what the actionType is, and the layer, from the DataUpdateKind in a efficient Manner.
        NewState callbackGagState = callbackDto.UpdateKind.ToNewState();
        GagLayer callbackGagLayer = callbackDto.UpdateKind.ToSlot();

        var callbackGagSlot = callbackDto.AppearanceData.GagSlots[(int)callbackGagLayer];
        var currentGagType = _playerData.AppearanceData!.GagSlots[(int)callbackGagLayer].GagType.ToGagType();

        _logger.LogDebug("Callback State: {0} | Callback Layer: {1} | Callback GagType: {2} | Current GagType: {3}",
            callbackGagState, callbackGagLayer, callbackGagSlot.GagType, currentGagType);


        // let's start handling the cases. For starters, if the NewState is apply..
        if (callbackGagState is NewState.Enabled)
        {
            // handle the case where we need to reapply, then...
            if (_playerData.AppearanceData!.GagSlots[(int)callbackGagLayer].GagType.ToGagType() != GagType.None)
            {
                _logger.LogDebug("Gag is already applied. Removing before reapplying.");
                // set up a task for removing and reapplying the gag glamours, and the another for updating the GagManager.
                await _visualUpdater.UpdateGagsAppearance(callbackGagLayer, currentGagType, NewState.Disabled);
                // after its disabled,...
            }

            // ...apply the new version.
            _logger.LogDebug("Applying Gag to Character Appearance.");
            await _visualUpdater.UpdateGagsAppearance(callbackGagLayer, callbackGagSlot.GagType.ToGagType(), NewState.Enabled);
            _gagManager.OnGagTypeChanged(callbackGagLayer, callbackGagSlot.GagType.ToGagType(), false);
        }
        else if (callbackGagState is NewState.Locked)
        {
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
            await _visualUpdater.UpdateGagsAppearance(callbackGagLayer, currentGagType, NewState.Disabled);
            _gagManager.OnGagTypeChanged(callbackGagLayer, GagType.None, false);
        }
    }

    public async void CallbackWardrobeUpdate(OnlineUserCharaWardrobeDataDto callbackDto, bool callbackWasFromSelf)
    {
        if (callbackWasFromSelf)
        {
            if (callbackDto.UpdateKind is DataUpdateKind.WardrobeRestraintUnlocked && _clientConfigs.GagspeakConfig.DisableSetUponUnlock)
            {
                // auto remove the restraint set after unlocking if we have just finished unlocking it.
                if (_playerData.CoreDataNull || !_playerData.GlobalPerms!.RestraintSetAutoEquip) return;

                int activeSetIdx = _clientConfigs.GetRestraintSetIdxByName(callbackDto.WardrobeData.ActiveSetName);
                await _clientConfigs.SetRestraintSetState(activeSetIdx, "SelfApplied", NewState.Disabled, true);
            }
            _logger.LogDebug("Received Callback for Self-Wardrobe Data with DataUpdateKind: {0}", callbackDto.UpdateKind.ToName());
            return;
        }

        var matchedPair = _pairManager.DirectPairs.FirstOrDefault(p => p.UserData.UID == callbackDto.User.UID);
        if (matchedPair == null)
        {
            _logger.LogError("Received Update by player is no longer present.");
            return;
        }

        if (!CanDoWardrobeInteract())
        {
            _logger.LogError("Player does not have permission to update their own wardrobe.");
            return;
        }

        int idx = _clientConfigs.GetRestraintSetIdxByName(callbackDto.WardrobeData.ActiveSetName);
        try
        {
            switch (callbackDto.UpdateKind)
            {
                case DataUpdateKind.WardrobeRestraintOutfitsUpdated:
                    _logger.LogError("Unexpected UpdateKind: WardrobeRestraintOutfitsUpdated.");
                    break;

                case DataUpdateKind.WardrobeRestraintApplied:
                    _logger.LogDebug($"{callbackDto.User.UID} has forcibly applied your [{callbackDto.WardrobeData.ActiveSetName}] restraint set!");
                    await _clientConfigs.SetRestraintSetState(idx, callbackDto.User.UID, NewState.Enabled, false);
                    break;

                case DataUpdateKind.WardrobeRestraintLocked:
                    _logger.LogDebug($"{callbackDto.User.UID} has forcibly locked your [{callbackDto.WardrobeData.ActiveSetName}] restraint set!");
                    _clientConfigs.LockRestraintSet(idx, callbackDto.WardrobeData.Padlock, callbackDto.WardrobeData.Password,
                        callbackDto.WardrobeData.Timer, callbackDto.User.UID, false);
                    break;

                case DataUpdateKind.WardrobeRestraintUnlocked:
                    _logger.LogDebug($"{callbackDto.User.UID} has force unlocked your [{callbackDto.WardrobeData.ActiveSetName}] restraint set!");
                    _clientConfigs.UnlockRestraintSet(idx, callbackDto.User.UID, false);
                    break;

                case DataUpdateKind.WardrobeRestraintDisabled:
                    _logger.LogDebug($"{callbackDto.User.UID} has force disabled your restraint set!");
                    var activeIdx = _clientConfigs.GetActiveSetIdx();
                    if (activeIdx != -1)
                    {
                        await _clientConfigs.SetRestraintSetState(activeIdx, callbackDto.User.UID, NewState.Disabled, false);
                    }
                    else
                    {
                        _logger.LogWarning("Somehow Made it to here, trying to remove the active set while it's already removed?");
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while processing Wardrobe Update.");
        }

    }

    public void CallbackAliasStorageUpdate(OnlineUserCharaAliasDataDto callbackDto, bool callbackFromSelf)
    {
        // this call should only ever be used for updating the registered name of a pair. if used for any other purpose, log error.
        if (callbackDto.UpdateKind == DataUpdateKind.PuppeteerPlayerNameRegistered)
        {
            // do the update for name registration of this pair.
            _mediator.Publish(new UpdateCharacterListenerForUid(callbackDto.User.UID, callbackDto.AliasData.CharacterName, callbackDto.AliasData.CharacterWorld));
            _logger.LogDebug("Player Name Registered Successfully processed by Server!");
        }
        else if (callbackFromSelf)
        {
            // if the update was an aliasList updated, log the successful update.
            if (callbackDto.UpdateKind == DataUpdateKind.PuppeteerAliasListUpdated)
            {
                _logger.LogDebug("Alias List Updated Successfully processed by Server!");
            }
            else
            {
                _logger.LogError("Unexpected UpdateKind: {0}", callbackDto.UpdateKind.ToName());
            }
        }
        else
        {
            _logger.LogError("Another Player should not be attempting to update your own alias list. Report this if you see it.");
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
                    _logger.LogInformation("Pattern Executed by Server Callback.");
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
                    _logger.LogInformation("Pattern Stopped by Server Callback.");
                }
                break;
            case DataUpdateKind.ToyboxAlarmListUpdated:
                _logger.LogInformation("ToyboxAlarmListUpdated Callback Received...");
                break;
            case DataUpdateKind.ToyboxAlarmToggled:
                _logger.LogInformation("ToyboxAlarmToggled Callback Received...");
                break;
            case DataUpdateKind.ToyboxTriggerListUpdated:
                _logger.LogInformation("ToyboxTriggerListUpdated Callback Received...");
                break;
            case DataUpdateKind.ToyboxTriggerToggled:
                _logger.LogInformation("ToyboxTriggerToggled Callback Received...");
                break;
        }
    }

}
