using GagSpeak.PlayerData.Pairs;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using GagspeakAPI.Data.Character;
using GagspeakAPI.Data.Permissions;
using GagspeakAPI.Dto.Connection;
using GagspeakAPI.Dto.Permissions;
using System.Reflection;

namespace GagSpeak.PlayerData.Data;

// unsure atm why we would need this, but we will find out soon.
public class PlayerCharacterManager : DisposableMediatorSubscriberBase
{
    private readonly PairManager _pairManager;
    private readonly ClientConfigurationManager _clientConfigManager;
    private CharacterIPCData _playerCharIpcData { get; set; } // the IPC data for our player character
    private CharacterAppearanceData _playerCharAppearanceData { get; set; } // the appearance data for our player character
    private CharacterWardrobeData _playerCharWardrobeData { get; set; } // the wardrobe data for our player character
    private Dictionary<string, CharacterAliasData> _playerCharAliasData { get; set; } // the alias data for our player character
    private CharacterPatternInfo _playerCharPatternData { get; set; } // the pattern data for our player character
    private UserGlobalPermissions _playerCharGlobalPerms { get; set; } // the global permissions for our player character

    public PlayerCharacterManager(ILogger<PlayerCharacterManager> logger, GagspeakMediator mediator,
        PairManager pairManager, ClientConfigurationManager clientConfiguration) : base(logger, mediator)
    {
        _pairManager = pairManager;
        _clientConfigManager = clientConfiguration;

        // temp initializers since we are not migrating from the saved files yet.
        _playerCharIpcData = new CharacterIPCData(); // Initialize the IPC data
        _playerCharWardrobeData = new CharacterWardrobeData(); // Initialize the wardrobe data
        _playerCharPatternData = new CharacterPatternInfo(); // Initialize the pattern data

        _playerCharAliasData = new Dictionary<string, CharacterAliasData>(); // Initialize the dictionary

        // we will need to call upon some initialization functions for assigning the data based on the configs.



        // Subscribe to the connected message update so we know when to update our global permissions
        Mediator.Subscribe<ConnectedMessage>(this, (msg) =>
        {
            logger.LogTrace("Connected message received. Updating global permissions.");
            // update our permissions
            _playerCharGlobalPerms = msg.Connection.UserGlobalPermissions;
            _playerCharAppearanceData = msg.Connection.CharacterAppearanceData;

            // maybe assign them here? If they need to get connected to establish permissions at all then perhaps it may help.
        });

        // At most we should subscribe to IPC updates so we can keep our IPC at the latest.
        Mediator.Subscribe<PlayerCharIpcChanged>(this, (msg) =>
        {
            _playerCharIpcData = msg.IPCData;
        });

        Mediator.Subscribe<PlayerCharAppearanceChanged>(this, (msg) =>
        {
            _playerCharAppearanceData = msg.AppearanceData;
        });

        Mediator.Subscribe<PlayerCharWardrobeChanged>(this, (msg) =>
        {
            _playerCharWardrobeData = msg.WardrobeData;
        });

        Mediator.Subscribe<PlayerCharAliasChanged>(this, (msg) =>
        {
            _playerCharAliasData[msg.playerUID] = msg.AliasData;
        });

        Mediator.Subscribe<PlayerCharPatternChanged>(this, (msg) =>
        {
            _playerCharPatternData = msg.PatternData;
        });

    }

    // public access definitions.
    public CharacterIPCData IpcData => _playerCharIpcData; // public var to access IPC data
    public CharacterAppearanceData AppearanceData => _playerCharAppearanceData; // public var to access appearance data
    public CharacterWardrobeData WardrobeData => _playerCharWardrobeData; // public var to access wardrobe data
    public CharacterPatternInfo PatternData => _playerCharPatternData; // public var to access pattern data
    public UserGlobalPermissions GlobalPerms => _playerCharGlobalPerms; // public var to access global permissions
    public IEnumerable<string> GetAllAliasListKeys() => _playerCharAliasData.Keys; // public method to get all alias list keys
    public CharacterAliasData GetAliasData(string userUID)
    {
        if (_playerCharAliasData.TryGetValue(userUID, out var aliasData))
        {
            return aliasData;
        }
        else
        {
            throw new KeyNotFoundException($"Alias data for key '{userUID}' not found.");
        }
    }


    // helper method to decompile a received composite data message
    public void UpdateCharWithCompositeData(OnlineUserCharaCompositeDataDto compositeData)
    {
        // decompose the composite data into its parts and update them
        _playerCharIpcData = compositeData.CompositeData.IPCData;
        _playerCharAppearanceData = compositeData.CompositeData.AppearanceData;
        _playerCharWardrobeData = compositeData.CompositeData.WardrobeData;
        _playerCharAliasData[compositeData.User.UID] = compositeData.CompositeData.AliasData;
        _playerCharPatternData = compositeData.CompositeData.PatternData;
    }

    /* helper method to update player characters relevant player data. Called upon by API Controller (and also mediator (but maybe make that separate idk)) */
    public void UpdateCharIpcData(OnlineUserCharaIpcDataDto ipcData)
    {
        _playerCharIpcData = ipcData.IPCData;
    }

    public void UpdateCharAppearanceData(OnlineUserCharaAppearanceDataDto appearanceData)
    {
        _playerCharAppearanceData = appearanceData.AppearanceData;
    }

    public void UpdateCharWardrobeData(OnlineUserCharaWardrobeDataDto wardrobeData)
    {
        _playerCharWardrobeData = wardrobeData.WardrobeData;
    }

    public void UpdateCharAliasData(OnlineUserCharaAliasDataDto aliasData)
    {
        _playerCharAliasData[aliasData.User.UID] = aliasData.AliasData;
    }

    public void UpdateCharPatternData(OnlineUserCharaPatternDataDto patternData)
    {
        // going to need to go a lot more in-depth on the logic for this later but we will figure it all out.
        _playerCharPatternData = patternData.PatternInfo;
    }


    /// <summary>
    /// Helper method to update the player characters global permission from the permission change Dto
    /// </summary>
    /// <param name="changeDto">The DTO containing the permission change.</param>
    public void ApplyGlobalPermChange(UserGlobalPermChangeDto changeDto)
    {
        // Ensure the global permissions object is not null
        if (_playerCharGlobalPerms == null)
        {
            Logger.LogError("Global permissions object is null. This should not be possible!");
            return;
        }
        // Use reflection to find the property that matches the key in ChangedPermission
        PropertyInfo? propertyInfo = typeof(UserGlobalPermissions).GetProperty(changeDto.ChangedPermission.Key);

        // If the property exists and is found, update its value
        if (changeDto.ChangedPermission.Value.GetType() == typeof(UInt64) && propertyInfo.PropertyType == typeof(TimeSpan))
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
            object value = Convert.ChangeType(changeDto.ChangedPermission.Value, propertyInfo.PropertyType);
            propertyInfo.SetValue(_playerCharGlobalPerms, value);
            Logger.LogDebug($"Updated global permission '{changeDto.ChangedPermission.Key}' to '{changeDto.ChangedPermission.Value}'");
        }
        else
        {
            Logger.LogError($"Property '{changeDto.ChangedPermission.Key}' not found or cannot be updated.");
        }
    }
}
