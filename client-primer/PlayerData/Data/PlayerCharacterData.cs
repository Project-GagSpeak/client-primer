using Dalamud.Utility;
using GagSpeak.GagspeakConfiguration.Models;
using GagSpeak.PlayerData.Pairs;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using GagSpeak.WebAPI;
using GagspeakAPI.Data;
using GagspeakAPI.Data.Character;
using GagspeakAPI.Enums;
using GagspeakAPI.Data.Permissions;
using GagspeakAPI.Data.Struct;
using GagspeakAPI.Dto.Permissions;
using System.Reflection;
using GagspeakAPI.Extensions;

namespace GagSpeak.PlayerData.Data;

/// <summary>
/// Handles the player character data.
/// <para>
/// Applies callback updates to clientConfig data
/// Compiles client config data into API format for server transfer.
/// </para>
/// </summary>
public class PlayerCharacterData : DisposableMediatorSubscriberBase
{
    private readonly ClientConfigurationManager _clientConfigs;
    private readonly PairManager _pairManager;
    private readonly PiShockProvider _piShockProvider;

    public PlayerCharacterData(ILogger<PlayerCharacterData> logger,
        GagspeakMediator mediator, ClientConfigurationManager clientConfigs,
        PairManager pairManager, PiShockProvider piShockProvider) : base(logger, mediator)
    {
        _clientConfigs = clientConfigs;
        _pairManager = pairManager;
        _piShockProvider = piShockProvider;

        Mediator.Subscribe<PlayerCharAppearanceChanged>(this, (msg) => PushAppearanceDataToAPI(msg));

        Mediator.Subscribe<PlayerCharWardrobeChanged>(this, (msg) => PushWardrobeDataToAPI(msg));

        Mediator.Subscribe<PlayerCharAliasChanged>(this, (msg) => PushAliasListDataToAPI(msg));

        Mediator.Subscribe<PlayerCharToyboxChanged>(this, (msg) => PushToyboxDataToAPI(msg));

        Mediator.Subscribe<CharacterIpcDataCreatedMessage>(this, (msg) => LastIpcData = msg.CharacterIPCData);
    }

    public UserGlobalPermissions? GlobalPerms { get; set; } = null;
    public CharacterAppearanceData? AppearanceData { get; set; } = null;
    public CharacterIPCData? LastIpcData { get; set; } = null;
    public List<CustomizeProfile> CustomizeProfiles { get; set; } = new();
    public PiShockPermissions GlobalPiShockPerms { get; set; } = new();

    public bool CoreDataNull => GlobalPerms is null || AppearanceData is null;
    public bool IpcDataNull => LastIpcData is null;
    private bool CustomizeNull => CustomizeProfiles is null || CustomizeProfiles.Count == 0;
    private bool ShockPermsNull => GlobalPiShockPerms.MaxIntensity == -1;
    public bool IsPlayerGagged => AppearanceData?.GagSlots.Any(x => x.GagType != GagType.None.GagName()) ?? false;
    public int TotalGagsEquipped => AppearanceData?.GagSlots.Count(x => x.GagType != GagType.None.GagName()) ?? 0;

    // Method Helpers For Data Compilation
    public async Task<PiShockPermissions> GetGlobalPiShockPerms()
    {
        if (CoreDataNull || ShockPermsNull)
        {
            // potentially edit this to always grab refreshed info on each connect, but idk.
            Logger.LogDebug("Global PiShockPerms already initialized. Returning.", LoggerType.PiShock);
            return GlobalPiShockPerms;
        }

        GlobalPiShockPerms = await _piShockProvider.GetPermissionsFromCode(GlobalPerms!.GlobalShockShareCode);
        return GlobalPiShockPerms;
    }

    public async void UpdateGlobalPiShockPerms()
    {
        if (CoreDataNull) return;

        GlobalPiShockPerms = await _piShockProvider.GetPermissionsFromCode(GlobalPerms!.GlobalShockShareCode);
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
            pair.LastOwnPiShockPermsForPair = await _piShockProvider.GetPermissionsFromCode(pair.UserPairOwnUniquePairPerms.ShockCollarShareCode);
            return pair.LastOwnPiShockPermsForPair;
        }
        // otherwise, if the code is null or empty, so return default
        else
        {
            return new();
        }
    }

    /// <summary> Updates the changed permission from server callback to global permissions </summary>
    public void ApplyGlobalPermChange(UserGlobalPermChangeDto changeDto)
    {
        if (CoreDataNull) return;

        // establish the key-value pair from the Dto so we know what is changing.
        string propertyName = changeDto.ChangedPermission.Key;
        object newValue = changeDto.ChangedPermission.Value;
        PropertyInfo? propertyInfo = typeof(UserGlobalPermissions).GetProperty(propertyName);

        if (propertyName == "GlobalShockShareCode")
        {
            Logger.LogDebug($"Attempting to grab latest PiShockPerms for Global", LoggerType.PiShock);
            Task.Run(async () => GlobalPiShockPerms = await GetGlobalPiShockPerms());
            return;
        }


        if (propertyInfo != null)
        {
            // If the property exists and is found, update its value
            if (newValue is UInt64 && propertyInfo.PropertyType == typeof(TimeSpan))
            {
                long ticks = (long)(ulong)newValue;
                propertyInfo.SetValue(GlobalPerms, TimeSpan.FromTicks(ticks));
            }
            // char recognition. (these are converted to byte for Dto's instead of char)
            else if (changeDto.ChangedPermission.Value.GetType() == typeof(byte) && propertyInfo.PropertyType == typeof(char))
            {
                propertyInfo.SetValue(GlobalPerms, Convert.ToChar(newValue));
            }
            else if (propertyInfo != null && propertyInfo.CanWrite)
            {
                // Convert the value to the appropriate type before setting
                var value = Convert.ChangeType(newValue, propertyInfo.PropertyType);
                propertyInfo.SetValue(GlobalPerms, value);
                Logger.LogDebug($"Updated global permission '{propertyName}' to '{newValue}'", LoggerType.ClientPlayerData);
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



    // helper method to decompile a received composite data message
    public async Task<CharacterCompositeData> CompileCompositeDataToSend()
    {
        // make use of the various compiling methods to construct our composite data.
        CharacterAppearanceData appearanceData = CompileAppearanceToAPI();
        CharacterWardrobeData wardrobeData = CompileWardrobeToAPI();

        Dictionary<string, CharacterAliasData> aliasData = new();
        Dictionary<string, PiShockPermissions> pairShockData = new();

        var userPairs = _pairManager.GetOnlineUserPairs();

        bool hasApiOn = !string.IsNullOrEmpty(_clientConfigs.GagspeakConfig.PiShockApiKey) && !string.IsNullOrEmpty(_clientConfigs.GagspeakConfig.PiShockUsername);

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
        var AliasStorage = _clientConfigs.FetchAliasStorageForPair(UserUID);
        CharacterAliasData dataToPush = new CharacterAliasData
        {
            // don't include names here to secure privacy.
            AliasList = AliasStorage.AliasList
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
}
