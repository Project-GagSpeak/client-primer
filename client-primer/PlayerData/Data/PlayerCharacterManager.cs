using GagSpeak.GagspeakConfiguration.Models;
using GagSpeak.Interop.IpcHelpers;
using GagSpeak.PlayerData.Handlers;
using GagSpeak.PlayerData.Pairs;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using GagSpeak.Utils;
using GagspeakAPI.Data;
using GagspeakAPI.Data.Character;
using GagspeakAPI.Data.Enum;
using GagspeakAPI.Data.Permissions;
using GagspeakAPI.Dto.Connection;
using GagspeakAPI.Dto.Permissions;

namespace GagSpeak.PlayerData.Data;

/// <summary>
/// Handles the player character data.
/// 
/// <para>
/// Applies callback updates to clientConfig data
/// Compiles client config data into API format for server transfer.
/// </para>
/// 
/// To avoid circular dependency, the following objects are linked:
/// 
/// <list type="bullet">
/// <item>ONLINE_PAIR_MANAGER => PLAYER_CHARACTER_DATA_HANDLER</item>
/// <item>ONLINE_PAIR_MANAGER => API_CONTROLLER</item>
/// <item>VISIBLE_PAIR_MANAGER => PAIR_MANAGER</item>
/// <item>VISIBLE_PAIR_MANAGER => API_CONTROLLER</item>
/// <item>API_CONTROLLER => PLAYER_CHARACTER_DATA_HANDLER</item>
/// <item>API_CONTROLLER => PAIR_MANAGER</item>
/// </list>
/// </summary>
public class PlayerCharacterManager : DisposableMediatorSubscriberBase
{
    private readonly PairManager _pairManager;
    private readonly WardrobeHandler _wardrobeHandler;
    private readonly PatternHandler _patternHandler;
    private readonly AlarmHandler _alarmHandler;
    private readonly TriggerHandler _triggerHandler;
    private readonly ClientConfigurationManager _clientConfigManager;

    // Stored data as retrieved from the server upon connection:
    private UserGlobalPermissions _playerCharGlobalPerms { get; set; }
    private CharacterAppearanceData _playerCharAppearance { get; set; }

    // TODO: expand this to store more than just the Moodles string, but IPC information
    private CharacterIPCData _playerCharIpc { get; set; }


    // TEMP STORAGE: Make this part of the IPC transfer object later! (Once C+ works again)
    IList<CustomizePlusProfileData> ClientCustomizeProfileList { get; set; }

    public PlayerCharacterManager(ILogger<PlayerCharacterManager> logger,
        GagspeakMediator mediator, PairManager pairManager,
        WardrobeHandler wardrobeHandler, PatternHandler patternHandler,
        AlarmHandler alarmHandler, TriggerHandler triggerHandler,
        ClientConfigurationManager clientConfiguration) : base(logger, mediator)
    {
        _pairManager = pairManager;
        _wardrobeHandler = wardrobeHandler;
        _patternHandler = patternHandler;
        _clientConfigManager = clientConfiguration;

        // Subscribe to the connected message update so we know when to update our global permissions
        Mediator.Subscribe<ConnectedMessage>(this, (msg) =>
        {
            logger.LogTrace("Connected message received. Updating global permissions.");
            _playerCharGlobalPerms = msg.Connection.UserGlobalPermissions;
            _playerCharAppearance = msg.Connection.CharacterAppearanceData;
        });

        // init the IPC
        _playerCharIpc = new CharacterIPCData();

        // These are called whenever we update our own data.
        // (Server callbacks handled separately to avoid looping calls to and from server infinitely)
        Mediator.Subscribe<PlayerCharIpcChanged>(this, (msg) => PushIpcDataToAPI(msg));
        Mediator.Subscribe<PlayerCharAppearanceChanged>(this, (msg) => PushAppearanceDataToAPI(msg));
        Mediator.Subscribe<PlayerCharWardrobeChanged>(this, (msg) => PushWardrobeDataToAPI(msg));
        Mediator.Subscribe<PlayerCharAliasChanged>(this, (msg) => PushAliasListDataToAPI(msg));
        Mediator.Subscribe<PlayerCharToyboxChanged>(this, (msg) => PushToyboxDataToAPI(msg));

        // Update the active Gags in the Gag Manager
        Mediator.Publish(new UpdateActiveGags());
    }

    // public access definitions.
    public UserGlobalPermissions GlobalPerms => _playerCharGlobalPerms;
    public CharacterAppearanceData AppearanceData => _playerCharAppearance;
    public CharacterIPCData IpcData => _playerCharIpc;
    public bool IsPlayerGagged() => AppearanceData.SlotOneGagType != "None"
                                 || AppearanceData.SlotTwoGagType != "None"
                                 || AppearanceData.SlotThreeGagType != "None";


    #region Compile & Push Data for Server Transfer
    // helper method to decompile a received composite data message
    public CharacterCompositeData CompileCompositeDataToSend()
    {
        // make use of the various compiling methods to construct our composite data.
        CharacterIPCData ipcData = CompileIpcToAPI();
        CharacterAppearanceData appearanceData = CompileAppearanceToAPI();
        CharacterWardrobeData wardrobeData = CompileWardrobeToAPI();

        Dictionary<string, CharacterAliasData> aliasData = new();
        // Compile the dictionary
        foreach (var user in _pairManager.GetOnlineUserUids())
        {
            aliasData[user] = CompileAliasToAPI(user);
        }

        CharacterToyboxData toyboxData = CompileToyboxToAPI();

        return new CharacterCompositeData
        {
            IPCData = ipcData,
            AppearanceData = appearanceData,
            WardrobeData = wardrobeData,
            AliasData = aliasData,
            ToyboxData = toyboxData
        };
    }

    // TODO: When working with Moodles, ensure that this & the cache creation service are handled properly.
    // In practice, this should not exist, but only temporarily does to help with compiling data.
    // The IPC Data itself should be handled within the cache creation service. As it is stored within the GameObjectHandler.
    private CharacterIPCData CompileIpcToAPI()
    {
        CharacterIPCData dataToPush = new CharacterIPCData
        {
            MoodlesData = IpcData.MoodlesData
        };

        return dataToPush;
    }

    private CharacterAppearanceData CompileAppearanceToAPI()
    {
        CharacterAppearanceData dataToPush = new CharacterAppearanceData
        {
            SlotOneGagType = AppearanceData.SlotOneGagType,
            SlotOneGagPadlock = AppearanceData.SlotOneGagPadlock,
            SlotOneGagPassword = AppearanceData.SlotOneGagPassword,
            SlotOneGagTimer = AppearanceData.SlotOneGagTimer,
            SlotOneGagAssigner = AppearanceData.SlotOneGagAssigner,
            SlotTwoGagType = AppearanceData.SlotTwoGagType,
            SlotTwoGagPadlock = AppearanceData.SlotTwoGagPadlock,
            SlotTwoGagPassword = AppearanceData.SlotTwoGagPassword,
            SlotTwoGagTimer = AppearanceData.SlotTwoGagTimer,
            SlotTwoGagAssigner = AppearanceData.SlotTwoGagAssigner,
            SlotThreeGagType = AppearanceData.SlotThreeGagType,
            SlotThreeGagPadlock = AppearanceData.SlotThreeGagPadlock,
            SlotThreeGagPassword = AppearanceData.SlotThreeGagPassword,
            SlotThreeGagTimer = AppearanceData.SlotThreeGagTimer,
            SlotThreeGagAssigner = AppearanceData.SlotThreeGagAssigner
        };

        return dataToPush;
    }

    private CharacterWardrobeData CompileWardrobeToAPI()
    {
        CharacterWardrobeData dataToPush = new CharacterWardrobeData
        {
            OutfitNames = _wardrobeHandler.GetRestraintSetsByName()
        };

        // attempt to locate the active restraint set
        var activeSetIdx = _wardrobeHandler.GetActiveSetIndex();

        // make sure the value is not -1, or greater than the outfitNames count. If it is in bounds, assign variables. Otherwise, use the defaults.
        if (activeSetIdx != -1 && activeSetIdx <= dataToPush.OutfitNames.Count)
        {
            // grab the set and set the variables.
            RestraintSet activeSet = _wardrobeHandler.GetRestraintSet(activeSetIdx);
            dataToPush.ActiveSetName = activeSet.Name;
            dataToPush.ActiveSetDescription = activeSet.Description;
            dataToPush.ActiveSetEnabledBy = activeSet.EnabledBy;
            dataToPush.ActiveSetIsLocked = activeSet.Locked;
            dataToPush.ActiveSetLockedBy = activeSet.LockedBy;
            dataToPush.ActiveSetLockTime = activeSet.LockedUntil;
        }

        return dataToPush;
    }

    private CharacterAliasData CompileAliasToAPI(string UserUID)
    {
        CharacterAliasData dataToPush = new CharacterAliasData
        {
            AliasList = _clientConfigManager.FetchListForPair(UserUID)
        };

        return dataToPush;
    }

    private CharacterToyboxData CompileToyboxToAPI()
    {
        return _clientConfigManager.CompileToyboxToAPI();
    }

    public void PushIpcDataToAPI(PlayerCharIpcChanged msg)
    {
        var dataToPush = CompileIpcToAPI();
        Mediator.Publish(new CharacterIpcDataCreatedMessage(dataToPush, msg.UpdateKind));
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
        Mediator.Publish(new CharacterAliasDataCreatedMessage(dataToPush, userPair));
    }

    public void PushToyboxDataToAPI(PlayerCharToyboxChanged msg)
    {
        var dataToPush = _clientConfigManager.CompileToyboxToAPI();
        Mediator.Publish(new CharacterToyboxDataCreatedMessage(dataToPush, msg.UpdateKind));
    }
    #endregion Compile & Push Data for Server Transfer

    /// <summary> Updates the changed permission from server callback to global permissions </summary>
    public void ApplyGlobalPermChange(UserGlobalPermChangeDto changeDto)
    {
        // Ensure the global permissions object is not null
        if (_playerCharGlobalPerms == null)
        {
            Logger.LogError("Global permissions object is null. This should not be possible!");
            return;
        }

        // Use reflection to find the property that matches the key in ChangedPermission
        var propertyInfo = typeof(UserGlobalPermissions).GetProperty(changeDto.ChangedPermission.Key);

        // If the property exists and is found, update its value
        if (changeDto.ChangedPermission.Value.GetType() == typeof(ulong) && propertyInfo.PropertyType == typeof(TimeSpan))
        {
            // property should be converted back from its Uint64 [MaxLockTime, 36000000000] to the timespan.
            propertyInfo.SetValue(_playerCharGlobalPerms, TimeSpan.FromTicks((long)(ulong)changeDto.ChangedPermission.Value));
        }
        // char recognition. (these are converted to byte for Dto's instead of char)
        else if (changeDto.ChangedPermission.Value.GetType() == typeof(byte) && propertyInfo.PropertyType == typeof(char))
        {
            propertyInfo.SetValue(_playerCharGlobalPerms, Convert.ToChar(changeDto.ChangedPermission.Value));
        }
        else if (propertyInfo != null && propertyInfo.CanWrite)
        {
            // Convert the value to the appropriate type before setting
            var value = Convert.ChangeType(changeDto.ChangedPermission.Value, propertyInfo.PropertyType);
            propertyInfo.SetValue(_playerCharGlobalPerms, value);
            Logger.LogDebug($"Updated global permission '{changeDto.ChangedPermission.Key}' to '{changeDto.ChangedPermission.Value}'");
        }
        else
        {
            Logger.LogError($"Property '{changeDto.ChangedPermission.Key}' not found or cannot be updated.");
        }
    }

    public void UpdateAppearanceFromCallback(OnlineUserCharaAppearanceDataDto callbackDto)
    {
        // One of our pairs has just forced us to change a setting (we know it is because the server-side validates this)

        // Update Appearance without calling any events so we don't loop back to the server.
        switch (callbackDto.UpdateKind)
        {
            case DataUpdateKind.AppearanceGagAppliedLayerOne:
                _playerCharAppearance.SlotOneGagType = callbackDto.AppearanceData.SlotOneGagType;
                Mediator.Publish(new UpdateActiveGags());
                Mediator.Publish(new UpdateGlamourGagsMessage(UpdatedNewState.Enabled, GagLayer.UnderLayer,
                    callbackDto.AppearanceData.SlotOneGagType.GetGagFromAlias(), callbackDto.User.UID));
                break;
            case DataUpdateKind.AppearanceGagAppliedLayerTwo:
                _playerCharAppearance.SlotTwoGagType = callbackDto.AppearanceData.SlotTwoGagType;
                Mediator.Publish(new UpdateActiveGags());
                Mediator.Publish(new UpdateGlamourGagsMessage(UpdatedNewState.Enabled, GagLayer.MiddleLayer,
                    callbackDto.AppearanceData.SlotTwoGagType.GetGagFromAlias(), callbackDto.User.UID));
                break;
            case DataUpdateKind.AppearanceGagAppliedLayerThree:
                _playerCharAppearance.SlotThreeGagType = callbackDto.AppearanceData.SlotThreeGagType;
                Mediator.Publish(new UpdateActiveGags());
                Mediator.Publish(new UpdateGlamourGagsMessage(UpdatedNewState.Enabled, GagLayer.TopLayer,
                    callbackDto.AppearanceData.SlotThreeGagType.GetGagFromAlias(), callbackDto.User.UID));
                break;

            case DataUpdateKind.AppearanceGagLockedLayerOne:
                _playerCharAppearance.SlotOneGagPadlock = callbackDto.AppearanceData.SlotOneGagPadlock;
                _playerCharAppearance.SlotOneGagPassword = callbackDto.AppearanceData.SlotOneGagPassword;
                _playerCharAppearance.SlotOneGagTimer = callbackDto.AppearanceData.SlotOneGagTimer;
                _playerCharAppearance.SlotOneGagAssigner = callbackDto.AppearanceData.SlotOneGagAssigner;
                break;
            case DataUpdateKind.AppearanceGagLockedLayerTwo:
                _playerCharAppearance.SlotTwoGagPadlock = callbackDto.AppearanceData.SlotTwoGagPadlock;
                _playerCharAppearance.SlotTwoGagPassword = callbackDto.AppearanceData.SlotTwoGagPassword;
                _playerCharAppearance.SlotTwoGagTimer = callbackDto.AppearanceData.SlotTwoGagTimer;
                _playerCharAppearance.SlotTwoGagAssigner = callbackDto.AppearanceData.SlotTwoGagAssigner;
                break;
            case DataUpdateKind.AppearanceGagLockedLayerThree:
                _playerCharAppearance.SlotThreeGagPadlock = callbackDto.AppearanceData.SlotThreeGagPadlock;
                _playerCharAppearance.SlotThreeGagPassword = callbackDto.AppearanceData.SlotThreeGagPassword;
                _playerCharAppearance.SlotThreeGagTimer = callbackDto.AppearanceData.SlotThreeGagTimer;
                _playerCharAppearance.SlotThreeGagAssigner = callbackDto.AppearanceData.SlotThreeGagAssigner;
                break;

            case DataUpdateKind.AppearanceGagUnlockedLayerOne:
                _playerCharAppearance.SlotOneGagPadlock = "None";
                _playerCharAppearance.SlotOneGagPassword = string.Empty;
                _playerCharAppearance.SlotOneGagTimer = DateTimeOffset.MinValue;
                _playerCharAppearance.SlotOneGagAssigner = string.Empty;
                break;
            case DataUpdateKind.AppearanceGagUnlockedLayerTwo:
                _playerCharAppearance.SlotTwoGagPadlock = "None";
                _playerCharAppearance.SlotTwoGagPassword = string.Empty;
                _playerCharAppearance.SlotTwoGagTimer = DateTimeOffset.MinValue;
                _playerCharAppearance.SlotTwoGagAssigner = string.Empty;
                break;
            case DataUpdateKind.AppearanceGagUnlockedLayerThree:
                _playerCharAppearance.SlotThreeGagPadlock = "None";
                _playerCharAppearance.SlotThreeGagPassword = string.Empty;
                _playerCharAppearance.SlotThreeGagTimer = DateTimeOffset.MinValue;
                _playerCharAppearance.SlotThreeGagAssigner = string.Empty;
                break;

            case DataUpdateKind.AppearanceGagRemovedLayerOne:
                _playerCharAppearance.SlotOneGagType = "None";
                Mediator.Publish(new UpdateActiveGags());
                Mediator.Publish(new UpdateGlamourGagsMessage(UpdatedNewState.Disabled, GagLayer.UnderLayer,
                    GagList.GagType.None, callbackDto.User.UID));
                break;
            case DataUpdateKind.AppearanceGagRemovedLayerTwo:
                _playerCharAppearance.SlotTwoGagType = "None";
                Mediator.Publish(new UpdateActiveGags());
                Mediator.Publish(new UpdateGlamourGagsMessage(UpdatedNewState.Disabled, GagLayer.MiddleLayer,
                    GagList.GagType.None, callbackDto.User.UID));
                break;
            case DataUpdateKind.AppearanceGagRemovedLayerThree:
                _playerCharAppearance.SlotThreeGagType = "None";
                Mediator.Publish(new UpdateActiveGags());
                Mediator.Publish(new UpdateGlamourGagsMessage(UpdatedNewState.Disabled, GagLayer.TopLayer,
                    GagList.GagType.None, callbackDto.User.UID));
                break;
        }
    }

    public void UpdateWardrobeFromCallback(OnlineUserCharaWardrobeDataDto callbackDto)
    {
        // One of our pairs has just forced us to change a setting (we know it is because the server-side validates this)

        // Update Appearance without calling any events so we don't loop back to the server.
        switch (callbackDto.UpdateKind)
        {
            // The below shouldnt necessarily even happen. Its redundant to.(unless we want verification feedback.)
            case DataUpdateKind.WardrobeRestraintOutfitsUpdated:
                Logger.LogError("Why are you trying to do this, you shouldnt be able to possibly reach here.");
                //_wardrobeHandler.UpdateRestraintSets(callbackDto.WardrobeData.OutfitNames);
                break;
            case DataUpdateKind.WardrobeRestraintApplied:
                _wardrobeHandler.CallbackForceEnableRestraintSet(callbackDto.WardrobeData.ActiveSetName, callbackDto.User.UID);
                break;
            case DataUpdateKind.WardrobeRestraintLocked:
                _wardrobeHandler.CallbackForceLockRestraintSet(callbackDto.WardrobeData.ActiveSetName, callbackDto.User.UID, callbackDto.WardrobeData.ActiveSetLockTime);
                break;
            case DataUpdateKind.WardrobeRestraintUnlocked:
                _wardrobeHandler.CallbackForceUnlockRestraintSet(callbackDto.WardrobeData.ActiveSetName, callbackDto.User.UID); // TODO: This is bug
                break;
            case DataUpdateKind.WardrobeRestraintRemoved:
                _wardrobeHandler.CallbackForceDisableRestraintSet(callbackDto.WardrobeData.ActiveSetName, callbackDto.User.UID);
                break;
        }
    }

    public void UpdateToyboxFromCallback(OnlineUserCharaToyboxDataDto callbackDto)
    {
        // One of our pairs has just forced us to change a setting (we know it is because the server-side validates this)

        // Update Appearance without calling any events so we don't loop back to the server.
        switch (callbackDto.UpdateKind)
        {
            case DataUpdateKind.ToyboxPatternActivated:
                {
                    var pattern = callbackDto.ToyboxInfo.PatternList.Where(p => p.IsActive).Select(p => p.Name).FirstOrDefault();
                    if (pattern == null)
                    {
                        Logger.LogError("Tried to activate pattern but pattern does not exist? How is this even possible.");
                        return;
                    }
                    _patternHandler.CallbackActivatePattern(pattern);
                }
                break;
            case DataUpdateKind.ToyboxPatternDeactivated:
                {
                    var pattern = callbackDto.ToyboxInfo.PatternList.Where(p => p.IsActive).Select(p => p.Name).FirstOrDefault();
                    if (pattern == null)
                    {
                        Logger.LogError("Tried to activate pattern but pattern does not exist? How is this even possible.");
                        return;
                    }
                    _patternHandler.CallbackDeactivatePattern(pattern);
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
            case DataUpdateKind.ToyboxTriggerActiveStatusChanged:
                // not done yet.
                //_clientConfigManager.CallbackToggleTrigger(callbackDto.ToyboxData.TriggerName, callbackDto.User.UID);
                break;
        }
    }
}
