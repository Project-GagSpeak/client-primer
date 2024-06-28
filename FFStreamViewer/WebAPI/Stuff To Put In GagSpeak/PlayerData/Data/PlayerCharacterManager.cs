using FFStreamViewer.WebAPI.PlayerData.Pairs;
using FFStreamViewer.WebAPI.Services.Mediator;
using Gagspeak.API.Data;
using Gagspeak.API.Data.Enum;
using GagSpeak.API.Data.Character;
using GagSpeak.API.Data.Permissions;
using GagSpeak.API.Dto.Connection;
using GagSpeak.API.Dto.Permissions;
using System.Reflection;

namespace FFStreamViewer.WebAPI.PlayerData.Data;

// unsure atm why we would need this, but we will find out soon.
public class PlayerCharacterManager : DisposableMediatorSubscriberBase
{
    private readonly ApiController _apiController;
    private readonly PairManager _pairManager;
    private readonly OnlinePlayerManager _onlinePlayerManager; // may not need this but idk lets just be sure???
    private CharacterIPCData _playerCharIpcData { get; set; } // the IPC data for our player character
    private CharacterAppearanceData _playerCharAppearanceData { get; set; } // the appearance data for our player character
    private CharacterWardrobeData _playerCharWardrobeData { get; set; } // the wardrobe data for our player character
    private Dictionary<string, CharacterAliasData> _playerCharAliasData { get; set; } // the alias data for our player character
    private CharacterPatternInfo _playerCharPatternData { get; set; } // the pattern data for our player character
    private UserGlobalPermissions _playerCharGlobalPerms { get; set; } // the global permissions for our player character

    public PlayerCharacterManager(ILogger<PlayerCharacterManager> logger, GagspeakMediator mediator,
        ApiController apiController, PairManager pairManager, OnlinePlayerManager onlinePlayerManager) : base(logger, mediator)
    {
        _apiController = apiController;
        _pairManager = pairManager;
        _onlinePlayerManager = onlinePlayerManager;

        _playerCharAliasData = new Dictionary<string, CharacterAliasData>(); // Initialize the dictionary

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
        if (propertyInfo != null && propertyInfo.CanWrite)
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
