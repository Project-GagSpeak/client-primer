using Dalamud.Utility;
using GagSpeak.GagspeakConfiguration.Models;
using GagSpeak.Interop.Ipc;
using GagSpeak.PlayerData.Handlers;
using GagSpeak.PlayerData.Pairs;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using GagSpeak.Toybox.Services;
using GagSpeak.Utils;
using GagSpeak.WebAPI;
using GagspeakAPI.Data;
using GagspeakAPI.Data.Character;
using GagspeakAPI.Data.Enum;
using GagspeakAPI.Data.Permissions;
using GagspeakAPI.Dto.Connection;
using GagspeakAPI.Dto.IPC;
using GagspeakAPI.Dto.Permissions;
using GagspeakAPI.Dto.User;
using System.Reflection;
using static FFXIVClientStructs.FFXIV.Component.GUI.AtkComponentNumericInput.Delegates;

namespace GagSpeak.PlayerData.Data;

/// <summary>
/// Handles the player character data.
/// Must not use extra handlers to avoid circular dependancy on the ones that must access player data. 
/// <para>
/// Applies callback updates to clientConfig data
/// Compiles client config data into API format for server transfer.
/// </para>
/// </summary>
public class PlayerCharacterManager : DisposableMediatorSubscriberBase
{
    private readonly PairManager _pairManager;
    private readonly PatternPlaybackService _playbackService;
    private readonly AlarmHandler _alarmHandler;
    private readonly TriggerHandler _triggerHandler;
    private readonly ClientConfigurationManager _clientConfigs;
    private readonly PiShockProvider _piShockProvider;
    private readonly IpcCallerMoodles _ipcCallerMoodles; // used to make moodles calls.

    // Stored data as retrieved from the server upon connection:
    private UserGlobalPermissions _playerCharGlobalPerms { get; set; }
    private CharacterAppearanceData _playerCharAppearance { get; set; }
    public PlayerCharacterManager(ILogger<PlayerCharacterManager> logger,
        GagspeakMediator mediator, PairManager pairManager,
        PatternPlaybackService playbackService, AlarmHandler alarmHandler,
        TriggerHandler triggerHandler, ClientConfigurationManager clientConfiguration,
        PiShockProvider piShockProvider, IpcCallerMoodles ipcCallerMoodles) : base(logger, mediator)
    {
        _pairManager = pairManager;
        _playbackService = playbackService;
        _alarmHandler = alarmHandler;
        _triggerHandler = triggerHandler;
        _clientConfigs = clientConfiguration;
        _piShockProvider = piShockProvider;
        _ipcCallerMoodles = ipcCallerMoodles;

        // Subscribe to the connected message update so we know when to update our global permissions
        Mediator.Subscribe<ConnectedMessage>(this, (msg) =>
        {
            logger.LogTrace("Connected message received. Updating global permissions.");
            _playerCharGlobalPerms = msg.Connection.UserGlobalPermissions;
            _playerCharAppearance = msg.Connection.CharacterAppearanceData;
            Mediator.Publish(new UpdateActiveGags());
            Task.Run(async () => await GetGlobalPiShockPerms());
        });

        // These are called whenever we update our own data.
        // (Server callbacks handled separately to avoid looping calls to and from server infinitely)
        Mediator.Subscribe<PlayerCharAppearanceChanged>(this, (msg) => PushAppearanceDataToAPI(msg));
        Mediator.Subscribe<PlayerCharWardrobeChanged>(this, (msg) => PushWardrobeDataToAPI(msg));
        Mediator.Subscribe<PlayerCharAliasChanged>(this, (msg) => PushAliasListDataToAPI(msg));
        Mediator.Subscribe<PlayerCharToyboxChanged>(this, (msg) => PushToyboxDataToAPI(msg));

        Mediator.Subscribe<CharacterIpcDataCreatedMessage>(this, (msg) => LastIpcData = msg.CharacterIPCData);
    }
    // used to track a reflection of the sealed cache creation service for our player.
    public CharacterIPCData? LastIpcData = null;
    public PiShockPermissions GlobalPiShockPerms = new();

    // public access definitions.
    public UserGlobalPermissions? GlobalPerms => _playerCharGlobalPerms ?? null;
    public CharacterAppearanceData? AppearanceData => _playerCharAppearance ?? null;
    public bool ShouldRemoveGagUponLockExpiration => _clientConfigs.GagspeakConfig.RemoveGagUponLockExpiration;
    public bool ShouldDisableSetUponUnlock => _clientConfigs.GagspeakConfig.DisableSetUponUnlock;
    public bool IsPlayerGagged() => AppearanceData?.GagSlots.Any(x => x.GagType != "None") ?? false;
    public void UpdateGlobalPermsInBulk(UserGlobalPermissions newGlobalPerms) => _playerCharGlobalPerms = newGlobalPerms;


    private async Task<PiShockPermissions> GetGlobalPiShockPerms()
    {
        if (GlobalPiShockPerms.MaxIntensity != -1)
        {
            // potentially edit this to always grab refreshed info on each connect, but idk.
            Logger.LogDebug("Global PiShockPerms already initialized. Returning.");
            return GlobalPiShockPerms;
        }

        GlobalPiShockPerms = await _piShockProvider.GetPermissionsFromCode(_playerCharGlobalPerms.GlobalShockShareCode);
        return GlobalPiShockPerms;
    }

    public async void UpdateGlobalPiShockPerms()
    {
        GlobalPiShockPerms = await _piShockProvider.GetPermissionsFromCode(_playerCharGlobalPerms.GlobalShockShareCode);
        Mediator.Publish(new CharacterPiShockGlobalPermDataUpdatedMessage(GlobalPiShockPerms, DataUpdateKind.PiShockGlobalUpdated));
    }

    private async Task<PiShockPermissions> GetPairPiShockPerms(Pair pair)
    {
        // Return the permissions as they are already initialized
        if (pair.LastOwnPiShockPermsForPair.MaxIntensity != -1 && !pair.UserPairOwnUniquePairPerms.ShockCollarShareCode.IsNullOrEmpty())
        {
            return pair.LastOwnPiShockPermsForPair;
        }
        // otherwise, if the code is not null or empty but the permissions are not initialized, initialize them.
        else if (!pair.UserPairOwnUniquePairPerms.ShockCollarShareCode.IsNullOrEmpty())
        {
            pair.LastOwnPiShockPermsForPair =
                await _piShockProvider.GetPermissionsFromCode(pair.UserPairOwnUniquePairPerms.ShockCollarShareCode);
            return pair.LastOwnPiShockPermsForPair;
        }
        // otherwise, if the code is null or empty, so return default
        else
        {
            return new();
        }
    }


    #region Compile & Push Data for Server Transfer
    // helper method to decompile a received composite data message
    public async Task<CharacterCompositeData> CompileCompositeDataToSend()
    {
        // make use of the various compiling methods to construct our composite data.
        CharacterAppearanceData appearanceData = CompileAppearanceToAPI();
        CharacterWardrobeData wardrobeData = CompileWardrobeToAPI();

        Dictionary<string, CharacterAliasData> aliasData = new();
        Dictionary<string, PiShockPermissions> pairShockData = new();

        var userPairs = _pairManager.GetOnlineUserPairs();

        bool hasApiOn = !string.IsNullOrEmpty(_clientConfigs.GagspeakConfig.PiShockApiKey)
            && !string.IsNullOrEmpty(_clientConfigs.GagspeakConfig.PiShockUsername)
            && !string.IsNullOrEmpty(_playerCharGlobalPerms.GlobalShockShareCode);

        List<Task<(string UID, PiShockPermissions)>> getPermissionsTasks = new();

        foreach (var user in userPairs)
        {
            aliasData[user.UserData.UID] = CompileAliasToAPI(user.UserData.UID);

            // Only fetch permissions if API credentials are available
            if (hasApiOn) getPermissionsTasks.Add(GetPairPiShockPerms(user).ContinueWith(task => (user.UserData.UID, task.Result)));
        }

        if (hasApiOn)
        {
            // Wait for all tasks to complete if API credentials are available
            var permissionsResults = await Task.WhenAll(getPermissionsTasks);

            // Populate pairShockData with the results
            foreach (var result in permissionsResults)
            {
                pairShockData[result.UID] = result.Item2 ?? new PiShockPermissions(); // Default or null handling
            }
        }
        else
        {
            // If no API credentials, populate with default permissions
            foreach (var user in userPairs)
            {
                pairShockData[user.UserData.UID] = new PiShockPermissions(); // Default or fallback
            }
        }

        CharacterToyboxData toyboxData = CompileToyboxToAPI();

        PiShockPermissions globalShockPerms = hasApiOn ? await GetGlobalPiShockPerms() : new PiShockPermissions();

        return new CharacterCompositeData
        {
            AppearanceData = appearanceData,
            WardrobeData = wardrobeData,
            AliasData = aliasData,
            ToyboxData = toyboxData,
            GlobalShockPermissions = globalShockPerms,
            PairShockPermissions = pairShockData
        };
    }

    private CharacterAppearanceData CompileAppearanceToAPI()
    {
        if (AppearanceData == null)
        {
            Logger.LogError("Appearance data is null. This should not be possible.");
            return new CharacterAppearanceData();
        }

        CharacterAppearanceData dataToPush = new CharacterAppearanceData
        {
            GagSlots = new GagSlot[3]
            {
                new GagSlot
                {
                    GagType = AppearanceData.GagSlots[0].GagType,
                    Padlock = AppearanceData.GagSlots[0].Padlock,
                    Password = AppearanceData.GagSlots[0].Password,
                    Timer = AppearanceData.GagSlots[0].Timer,
                    Assigner = AppearanceData.GagSlots[0].Assigner
                },
                new GagSlot
                {
                    GagType = AppearanceData.GagSlots[1].GagType,
                    Padlock = AppearanceData.GagSlots[1].Padlock,
                    Password = AppearanceData.GagSlots[1].Password,
                    Timer = AppearanceData.GagSlots[1].Timer,
                    Assigner = AppearanceData.GagSlots[1].Assigner
                },
                new GagSlot
                {
                    GagType = AppearanceData.GagSlots[2].GagType,
                    Padlock = AppearanceData.GagSlots[2].Padlock,
                    Password = AppearanceData.GagSlots[2].Password,
                    Timer = AppearanceData.GagSlots[2].Timer,
                    Assigner = AppearanceData.GagSlots[2].Assigner
                }
            }
        };

        return dataToPush;
    }

    private CharacterWardrobeData CompileWardrobeToAPI()
    {
        CharacterWardrobeData dataToPush = new CharacterWardrobeData
        {
            OutfitNames = _clientConfigs.GetRestraintSetNames()
        };

        // attempt to locate the active restraint set
        var activeSetIdx = _clientConfigs.GetActiveSetIdx();

        // make sure the value is not -1, or greater than the outfitNames count. If it is in bounds, assign variables. Otherwise, use the defaults.
        if (activeSetIdx != -1 && activeSetIdx <= dataToPush.OutfitNames.Count)
        {
            // grab the set and set the variables.
            RestraintSet activeSet = _clientConfigs.GetRestraintSet(activeSetIdx);
            dataToPush.ActiveSetName = activeSet.Name;
            dataToPush.ActiveSetDescription = activeSet.Description;
            dataToPush.ActiveSetEnabledBy = activeSet.EnabledBy;
            dataToPush.Padlock = activeSet.LockType;
            dataToPush.Password = activeSet.LockPassword;
            dataToPush.Timer = activeSet.LockedUntil;
            dataToPush.Assigner = activeSet.LockedBy;
        }

        return dataToPush;
    }

    private CharacterAliasData CompileAliasToAPI(string UserUID)
    {
        var AliasStoage = _clientConfigs.FetchAliasStorageForPair(UserUID);
        CharacterAliasData dataToPush = new CharacterAliasData
        {
            CharacterName = AliasStoage.CharacterName,
            CharacterWorld = AliasStoage.CharacterWorld,
            AliasList = AliasStoage.AliasList
        };

        return dataToPush;
    }

    private CharacterToyboxData CompileToyboxToAPI()
    {
        return _clientConfigs.CompileToyboxToAPI();
    }

    public void PushAppearanceDataToAPI(PlayerCharAppearanceChanged msg)
    {
        var dataToPush = CompileAppearanceToAPI();
        Mediator.Publish(new CharacterAppearanceDataCreatedMessage(dataToPush, msg.UpdateKind));
    }

    public void PushWardrobeDataToAPI(PlayerCharWardrobeChanged msg)
    {
        CharacterWardrobeData dataToPush = CompileWardrobeToAPI();
        Mediator.Publish(new CharacterWardrobeDataCreatedMessage(dataToPush, msg.UpdateKind));
    }

    public void PushAliasListDataToAPI(PlayerCharAliasChanged msg)
    {
        UserData? userPair = _pairManager.GetUserDataFromUID(msg.UpdatedPairUID);
        if (userPair == null)
        {
            Logger.LogError("User pair not found for Alias update.");
            return;
        }

        var dataToPush = CompileAliasToAPI(userPair.UID);
        Mediator.Publish(new CharacterAliasDataCreatedMessage(dataToPush, userPair, DataUpdateKind.PuppeteerAliasListUpdated));
    }

    public void PushToyboxDataToAPI(PlayerCharToyboxChanged msg)
    {
        var dataToPush = _clientConfigs.CompileToyboxToAPI();
        Mediator.Publish(new CharacterToyboxDataCreatedMessage(dataToPush, msg.UpdateKind));
    }
    #endregion Compile & Push Data for Server Transfer

    //////////////// Moodles & Statuses ////////////////
    public void ApplyStatusesByGuid(ApplyMoodlesByGuidDto dto)
    {
        if (!_pairManager.GetVisibleUsers().Select(u => u.UID).Contains(dto.User.UID)) { Logger.LogError("Received Update by player is no longer present."); return; }

        _ = _ipcCallerMoodles.ApplyOwnStatusByGUID(dto.Statuses).ConfigureAwait(false);
    }

    public void ApplyStatusesToSelf(ApplyMoodlesByStatusDto dto, string clientPlayerNameWithWorld)
    {
        string nameWithWorldOfApplier = _pairManager.DirectPairs.FirstOrDefault(p => p.UserData.UID == dto.User.UID)?.PlayerNameWithWorld ?? string.Empty;
        if (nameWithWorldOfApplier.IsNullOrEmpty()) { Logger.LogError("Received Update by player is no longer present."); return; }

        _ = _ipcCallerMoodles.ApplyStatusesFromPairToSelf(nameWithWorldOfApplier, clientPlayerNameWithWorld, dto.Statuses).ConfigureAwait(false);
    }

    public void RemoveStatusesFromSelf(RemoveMoodlesDto dto)
    {
        if (!_pairManager.GetVisibleUsers().Select(u => u.UID).Contains(dto.User.UID)) { Logger.LogError("Received Update by player is no longer present."); return; }

        _ = _ipcCallerMoodles.RemoveOwnStatusByGuid(dto.Statuses).ConfigureAwait(false);
    }

    public void ClearStatusesFromSelf(UserDto dto)
    {
        if (!_pairManager.GetVisibleUsers().Select(u => u.UID).Contains(dto.User.UID)) { Logger.LogError("Received Update by player is no longer present."); return; }

        bool CanClearMoodles = _pairManager.DirectPairs.First(p => p.UserData.UID == dto.User.UID).UserPairUniquePairPerms.AllowRemovingMoodles;
        if (!CanClearMoodles) { Logger.LogError("Player does not have permission to clear their own moodles."); return; }

        _ = _ipcCallerMoodles.ClearStatusAsync().ConfigureAwait(false);
    }

    /// <summary> Updates the changed permission from server callback to global permissions </summary>
    public void ApplyGlobalPermChange(UserGlobalPermChangeDto changeDto)
    {
        // Ensure the global permissions object is not null
        if (_playerCharGlobalPerms == null)
        {
            Logger.LogError("Global permissions object is null. This should not be possible!");
            return;
        }

		// establish the key-value pair from the Dto so we know what is changing.
		string propertyName = changeDto.ChangedPermission.Key;
		object newValue = changeDto.ChangedPermission.Value;
		PropertyInfo? propertyInfo = typeof(UserGlobalPermissions).GetProperty(propertyName);

        if(propertyName == "GlobalShockShareCode")
        {
            Logger.LogDebug($"Attempting to grab latest PiShockPerms for Global");
            Task.Run(async () => GlobalPiShockPerms = await GetGlobalPiShockPerms());
            return;
        }
        

        if (propertyInfo != null)
        {
            // If the property exists and is found, update its value
            if (newValue is UInt64 && propertyInfo.PropertyType == typeof(TimeSpan))
            {
                long ticks = (long)(ulong)newValue;
                propertyInfo.SetValue(_playerCharGlobalPerms, TimeSpan.FromTicks(ticks));
            }
            // char recognition. (these are converted to byte for Dto's instead of char)
            else if (changeDto.ChangedPermission.Value.GetType() == typeof(byte) && propertyInfo.PropertyType == typeof(char))
            {
                propertyInfo.SetValue(_playerCharGlobalPerms, Convert.ToChar(newValue));
            }
            else if (propertyInfo != null && propertyInfo.CanWrite)
            {
                // Convert the value to the appropriate type before setting
                var value = Convert.ChangeType(newValue, propertyInfo.PropertyType);
                propertyInfo.SetValue(_playerCharGlobalPerms, value);
                Logger.LogDebug($"Updated global permission '{propertyName}' to '{newValue}'");
            }
            else
            {
                Logger.LogError($"Property '{propertyName}' not found or cannot be updated.");
            }
        }
        else
        {
            Logger.LogError($"Property '{propertyName}' not found or cannot be updated.");
        }
    }

    public async void UpdateAppearanceFromCallback(OnlineUserCharaAppearanceDataDto callbackDto, bool callbackWasFromSelf)
    {
        // handle the cases where we get a callback from ourselves to log successful interactions with the server.
        if (callbackWasFromSelf)
        {
            Logger.LogTrace($"Callback for self-appearance data from server handled successfully.");
            if (!ShouldRemoveGagUponLockExpiration) return;

            var layer = callbackDto.UpdateKind switch
            {
                DataUpdateKind.AppearanceGagUnlockedLayerOne => GagLayer.UnderLayer,
                DataUpdateKind.AppearanceGagUnlockedLayerTwo => GagLayer.MiddleLayer,
                DataUpdateKind.AppearanceGagUnlockedLayerThree => GagLayer.TopLayer,
                _ => (GagLayer?)null
            };

            if (layer.HasValue)
            {
                Mediator.Publish(new GagTypeChanged(GagList.GagType.None, layer.Value));
            }
            return;
        }

        GagList.GagType prevGagType = GagList.GagType.None;
        var gagUpdateOnRemovalCompletion = new TaskCompletionSource<bool>();
        var gagGlamourUpdateOnRemovalCompletion = new TaskCompletionSource<bool>();

        switch (callbackDto.UpdateKind)
        {
            case DataUpdateKind.AppearanceGagAppliedLayerOne:
                await HandleGagApplication(0, callbackDto.AppearanceData.GagSlots[0].GagType, GagLayer.UnderLayer);
                break;
            case DataUpdateKind.AppearanceGagAppliedLayerTwo:
                await HandleGagApplication(1, callbackDto.AppearanceData.GagSlots[1].GagType, GagLayer.MiddleLayer);
                break;
            case DataUpdateKind.AppearanceGagAppliedLayerThree:
                await HandleGagApplication(2, callbackDto.AppearanceData.GagSlots[2].GagType, GagLayer.TopLayer);
                break;
            case DataUpdateKind.AppearanceGagLockedLayerOne:
                HandleGagLock(0, GagLayer.UnderLayer);
                break;
            case DataUpdateKind.AppearanceGagLockedLayerTwo:
                HandleGagLock(1, GagLayer.MiddleLayer);
                break;
            case DataUpdateKind.AppearanceGagLockedLayerThree:
                HandleGagLock(2, GagLayer.TopLayer);
                break;
            case DataUpdateKind.AppearanceGagUnlockedLayerOne:
                HandleGagUnlock(0, GagLayer.UnderLayer);
                break;
            case DataUpdateKind.AppearanceGagUnlockedLayerTwo:
                HandleGagUnlock(1, GagLayer.MiddleLayer);
                break;
            case DataUpdateKind.AppearanceGagUnlockedLayerThree:
                HandleGagUnlock(2, GagLayer.TopLayer);
                break;
            case DataUpdateKind.AppearanceGagRemovedLayerOne:
                HandleGagRemoval(0, GagLayer.UnderLayer);
                break;
            case DataUpdateKind.AppearanceGagRemovedLayerTwo:
                HandleGagRemoval(1, GagLayer.MiddleLayer);
                break;
            case DataUpdateKind.AppearanceGagRemovedLayerThree:
                HandleGagRemoval(2, GagLayer.TopLayer);
                break;
        }


        async Task HandleGagApplication(int slotIndex, string gagType, GagLayer layer)
        {
            if (_playerCharAppearance.GagSlots[slotIndex].Padlock == "None")
            {
                if (_playerCharAppearance.GagSlots[slotIndex].GagType == "None")
                {
                    _playerCharAppearance.GagSlots[slotIndex].GagType = gagType;
                    Mediator.Publish(new UpdateActiveGags());
                    Mediator.Publish(new UpdateGlamourGagsMessage(NewState.Enabled, layer, gagType.GetGagFromAlias(), callbackDto.User.UID));
                }
                else
                {
                    prevGagType = _playerCharAppearance.GagSlots[slotIndex].GagType.GetGagFromAlias();
                    _playerCharAppearance.GagSlots[slotIndex].GagType = "None";
                    Mediator.Publish(new UpdateActiveGags(gagUpdateOnRemovalCompletion));
                    await gagUpdateOnRemovalCompletion.Task;
                    Mediator.Publish(new UpdateGlamourGagsMessage(NewState.Disabled, layer, prevGagType, callbackDto.User.UID, gagGlamourUpdateOnRemovalCompletion));
                    await gagGlamourUpdateOnRemovalCompletion.Task;

                    _playerCharAppearance.GagSlots[slotIndex].GagType = gagType;
                    Mediator.Publish(new UpdateActiveGags());
                    Mediator.Publish(new UpdateGlamourGagsMessage(NewState.Enabled, layer, gagType.GetGagFromAlias(), callbackDto.User.UID));
                }
            }
        }
        void HandleGagLock(int slotIndex, GagLayer layer)
        {
            Enum.TryParse<Padlocks>(_playerCharAppearance.GagSlots[slotIndex].Padlock, out var lockType);
            Mediator.Publish(new GagLockToggle(new PadlockData(layer, lockType, _playerCharAppearance.GagSlots[slotIndex].Password, _playerCharAppearance.GagSlots[slotIndex].Timer, _playerCharAppearance.GagSlots[slotIndex].Assigner), false, false));
        }

        void HandleGagUnlock(int slotIndex, GagLayer layer)
        {
            Enum.TryParse<Padlocks>(_playerCharAppearance.GagSlots[slotIndex].Padlock, out var unlockType);
            Mediator.Publish(new GagLockToggle(new PadlockData(layer, unlockType, string.Empty, DateTimeOffset.MinValue, string.Empty), true, false));
        }

        void HandleGagRemoval(int slotIndex, GagLayer layer)
        {
            prevGagType = _playerCharAppearance.GagSlots[slotIndex].GagType.GetGagFromAlias();
            _playerCharAppearance.GagSlots[slotIndex].GagType = "None";
            Mediator.Publish(new UpdateActiveGags());
            Mediator.Publish(new UpdateGlamourGagsMessage(NewState.Disabled, layer, prevGagType, callbackDto.User.UID));
        }
    }

    public async void UpdateWardrobeFromCallback(OnlineUserCharaWardrobeDataDto callbackDto, bool callbackWasFromSelf)
    {
        // handle the cases where another pair has updated our data:
        if (!callbackWasFromSelf)
        {
            var matchedPair = _pairManager.DirectPairs.FirstOrDefault(p => p.UserData.UID == callbackDto.User.UID);
            if (matchedPair == null) return;
            switch (callbackDto.UpdateKind)
            {
                case DataUpdateKind.WardrobeRestraintOutfitsUpdated: Logger.LogError("You should never reach here. Report if you get this."); break;
                case DataUpdateKind.WardrobeRestraintApplied:
                    Logger.LogDebug($"{callbackDto.User.UID} has forced you to enable your [{callbackDto.WardrobeData.ActiveSetName}] restraint set!");
                    // make sure they have the permissions to.
                    if (GlobalPerms == null || !GlobalPerms.WardrobeEnabled || !GlobalPerms.RestraintSetAutoEquip) break;
                    var idx = _clientConfigs.GetRestraintSetIdxByName(callbackDto.WardrobeData.ActiveSetName);
                    await _clientConfigs.SetRestraintSetState(idx, callbackDto.User.UID, NewState.Enabled, false);
                    break;
                case DataUpdateKind.WardrobeRestraintLocked:
                    Logger.LogDebug($"{callbackDto.User.UID} has forced locked your [{callbackDto.WardrobeData.ActiveSetName}] restraint set!");
                    // make sure they have the permissions to.
                    if (GlobalPerms == null || !GlobalPerms.WardrobeEnabled || !GlobalPerms.RestraintSetAutoEquip) break;
                    var idxLock = _clientConfigs.GetRestraintSetIdxByName(callbackDto.WardrobeData.ActiveSetName);
                    _clientConfigs.LockRestraintSet(idxLock, callbackDto.WardrobeData.Padlock, callbackDto.WardrobeData.Password, callbackDto.WardrobeData.Timer, callbackDto.User.UID, false);
                    break;
                case DataUpdateKind.WardrobeRestraintUnlocked:
                    Logger.LogDebug($"{callbackDto.User.UID} has forced unlocked your [{callbackDto.WardrobeData.ActiveSetName}] restraint set!");
                    // make sure they have the permissions to.
                    if (GlobalPerms == null || !GlobalPerms.WardrobeEnabled || !GlobalPerms.RestraintSetAutoEquip) break;
                    var idxUnlock = _clientConfigs.GetRestraintSetIdxByName(callbackDto.WardrobeData.ActiveSetName);
                    _clientConfigs.UnlockRestraintSet(idxUnlock, callbackDto.User.UID, false);
                    break;
                case DataUpdateKind.WardrobeRestraintDisabled:
                    Logger.LogDebug($"{callbackDto.User.UID} has force disabled your [{callbackDto.WardrobeData.ActiveSetName}] restraint set!");
                    // make sure they have the permissions to.
                    if (GlobalPerms == null || !GlobalPerms.WardrobeEnabled || !GlobalPerms.RestraintSetAutoEquip) break;
                    var idxDisable = _clientConfigs.GetActiveSetIdx();
                    await _clientConfigs.SetRestraintSetState(idxDisable, callbackDto.User.UID, NewState.Disabled, false);
                    break;
            }
            return; // don't process self callbacks if it was a pair callback.
        }

        // Handle the cases in where we updated our own data.
        switch (callbackDto.UpdateKind)
        {
            case DataUpdateKind.WardrobeRestraintOutfitsUpdated: Logger.LogDebug("ListUpdate Successfully processed by Server!"); break;
            case DataUpdateKind.WardrobeRestraintApplied: Logger.LogDebug("Restraint Set Apply Successfully processed by Server!"); break;
            case DataUpdateKind.WardrobeRestraintLocked: Logger.LogDebug("Restraint Set Lock Successfully processed by Server!"); break;
            case DataUpdateKind.WardrobeRestraintUnlocked:
                {
                    // for unlock, if we have enabled the setting for automatically removing unlocked sets, do so.
                    if (ShouldDisableSetUponUnlock)
                    {
                        // make sure they have the permissions to.
                        if (GlobalPerms == null || !GlobalPerms.RestraintSetAutoEquip) break;
                        int activeSetIdx = _clientConfigs.GetRestraintSetIdxByName(callbackDto.WardrobeData.ActiveSetName);
                        await _clientConfigs.SetRestraintSetState(activeSetIdx, "SelfApplied", NewState.Disabled, true);

                    }
                    Logger.LogDebug("Restraint Set Unlock Successfully processed by Server!");
                }
                break;
            case DataUpdateKind.WardrobeRestraintDisabled: Logger.LogDebug("Restraint Set Remove Successfully processed by Server!"); break;
        }
    }

    public void UpdateAliasStorageFromCallback(OnlineUserCharaAliasDataDto callbackDto)
    {
        // this call should only ever be used for updating the registered name of a pair. if used for any other purpose, log error.
        if (callbackDto.UpdateKind == DataUpdateKind.PuppeteerPlayerNameRegistered)
        {
            // do the update for name registration of this pair.
            Mediator.Publish(new UpdateCharacterListenerForUid(callbackDto.User.UID, callbackDto.AliasData.CharacterName, callbackDto.AliasData.CharacterWorld));
            Logger.LogDebug("Player Name Registered Successfully processed by Server!");
        }
        else
        {
            Logger.LogError("Another Player should not be attempting to update your own alias list. Report this if you see it.");
            return;
        }
    }

    public void UpdateToyboxFromCallback(OnlineUserCharaToyboxDataDto callbackDto)
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
                        Logger.LogError("Tried to activate pattern but pattern does not exist? How is this even possible.");
                        return;
                    }
                    _playbackService.PlayPattern(patternData.UniqueIdentifier, patternData.StartPoint, patternData.Duration, false);
                }
                break;
            case DataUpdateKind.ToyboxPatternStopped:
                {
                    var patternId = callbackDto.ToyboxInfo.ActivePatternGuid;
                    var patternData = _clientConfigs.FetchPatternById(patternId);
                    if (patternData == null)
                    {
                        Logger.LogError("Tried to activate pattern but pattern does not exist? How is this even possible.");
                        return;
                    }
                    _playbackService.StopPattern(patternId, false);
                }
                break;
            case DataUpdateKind.ToyboxAlarmListUpdated:
                Logger.LogError("Why are you trying to do this, you shouldnt be able to possibly reach here.");
                break;
            case DataUpdateKind.ToyboxAlarmToggled:
                _alarmHandler.UpdateAlarmStatesFromCallback(callbackDto.ToyboxInfo.AlarmList);
                break;
            case DataUpdateKind.ToyboxTriggerListUpdated:
                Logger.LogError("Why are you trying to do this, you shouldnt be able to possibly reach here.");
                // Might be feasible if you are sent a new pattern as well?  Idk. Look into later.
                break;
            case DataUpdateKind.ToyboxTriggerToggled:
                // not done yet.
                //_clientConfigs.CallbackToggleTrigger(callbackDto.ToyboxData.TriggerName, callbackDto.User.UID);
                break;
        }
    }
}
