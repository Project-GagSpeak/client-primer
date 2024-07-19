using GagSpeak.GagspeakConfiguration;
using GagSpeak.GagspeakConfiguration.Configurations;
using GagSpeak.GagspeakConfiguration.Models;
using GagSpeak.Interop.Ipc;
using GagSpeak.Services.Mediator;
using GagSpeak.UpdateMonitoring;
using GagSpeak.Utils;
using GagspeakAPI.Data;
using GagspeakAPI.Data.Enum;
using Microsoft.IdentityModel.Tokens;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using static GagspeakAPI.Data.Enum.GagList;

namespace GagSpeak.Services.ConfigurationServices;

/// <summary>
/// This configuration manager helps manage the various interactions with all config files related to server-end activity.
/// <para> It provides a comprehensive interface for configuring servers, managing tags and nicknames, and handling authentication keys. </para>
/// </summary>
public class ClientConfigurationManager
{
    private readonly OnFrameworkService _frameworkUtils;            // a utilities class with methods that work with the Dalamud framework
    private readonly ILogger<ClientConfigurationManager> _logger;   // the logger for the server config manager
    private readonly GagspeakMediator _mediator;            // the mediator for our Gagspeak Mediator
    private readonly GagspeakConfigService _configService;          // the primary gagspeak config service.
    private readonly GagStorageConfigService _gagStorageConfig;     // the config for the gag storage service (toybox gag storage)
    private readonly WardrobeConfigService _wardrobeConfig;         // the config for the wardrobe service (restraint sets)
    private readonly AliasConfigService _aliasConfig;               // the config for the alias lists (puppeteer stuff)
    private readonly PatternConfigService _patternConfig;           // the config for the pattern service (toybox pattern storage))

    public ClientConfigurationManager(ILogger<ClientConfigurationManager> logger, 
        OnFrameworkService onFrameworkService, GagspeakMediator GagspeakMediator, 
        GagspeakConfigService configService, GagStorageConfigService gagStorageConfig,
        WardrobeConfigService wardrobeConfig, AliasConfigService aliasConfig,
        PatternConfigService patternConfig)
    {
        _logger = logger;
        _frameworkUtils = onFrameworkService;
        _mediator = GagspeakMediator;
        _configService = configService;
        _gagStorageConfig = gagStorageConfig;
        _wardrobeConfig = wardrobeConfig;
        _aliasConfig = aliasConfig;
        _patternConfig = patternConfig;

        // insure the nicknames and tag configs exist in the main server.
        if (_gagStorageConfig.Current.GagStorage == null) { _gagStorageConfig.Current.GagStorage = new(); }
        // create a new storage file
        if (_gagStorageConfig.Current.GagStorage.GagEquipData.IsNullOrEmpty())
        {
            _logger.LogWarning("Gag Storage Config is empty, creating a new one.");
            try
            {
                _gagStorageConfig.Current.GagStorage.GagEquipData =
                    Enum.GetValues(typeof(GagList.GagType))
                        .Cast<GagList.GagType>()
                        .ToDictionary(gagType => gagType, gagType => new GagDrawData(ItemIdVars.NothingItem(EquipSlot.Head)));
                // print the keys in the dictionary
                _logger.LogInformation("Gag Storage Config Created with {count} keys", _gagStorageConfig.Current.GagStorage.GagEquipData.Count);
                _gagStorageConfig.Save();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to create Gag Storage Config");
            }
        }
        if (_wardrobeConfig.Current.WardrobeStorage == null) { _wardrobeConfig.Current.WardrobeStorage = new(); }
        if (_aliasConfig.Current.AliasStorage == null) { _aliasConfig.Current.AliasStorage = new(); }
        if (_patternConfig.Current.PatternStorage == null) { _patternConfig.Current.PatternStorage = new(); }
    }

    // define public access to various storages (THESE ARE ONLY GETTERS, NO SETTERS)
    public GagspeakConfig GagspeakConfig => _configService.Current;
    public GagStorageConfig GagStorageConfig => _gagStorageConfig.Current;
    private WardrobeConfig WardrobeConfig => _wardrobeConfig.Current;
    private AliasConfig AliasConfig => _aliasConfig.Current;
    private PatternConfig PatternConfig => _patternConfig.Current;

    public bool HasCreatedConfigs()
    {
        return (GagspeakConfig != null && WardrobeConfig != null && AliasConfig != null && PatternConfig != null);
    }

    /// <summary> Saves the GagspeakConfig. </summary>
    public void Save()
    {
        var caller = new StackTrace().GetFrame(1)?.GetMethod()?.ReflectedType?.Name ?? "Unknown";
        _logger.LogDebug("{caller} Calling config save", caller);
        _configService.Save();
    }

    /* --------------------- Gag Storage Config Methods --------------------- */
    internal bool IsGagEnabled(GagType gagType) => _gagStorageConfig.Current.GagStorage.GagEquipData.FirstOrDefault(x => x.Key == gagType).Value.IsEnabled;
    internal GagDrawData GetDrawData(GagType gagType) => _gagStorageConfig.Current.GagStorage.GagEquipData[gagType];
    internal EquipSlot GetGagTypeEquipSlot(GagType gagType) => _gagStorageConfig.Current.GagStorage.GagEquipData.FirstOrDefault(x => x.Key == gagType).Value.Slot;
    internal EquipItem GetGagTypeEquipItem(GagType gagType) => _gagStorageConfig.Current.GagStorage.GagEquipData.FirstOrDefault(x => x.Key == gagType).Value.GameItem;
    internal StainIds GetGagTypeStain(GagType gagType) => _gagStorageConfig.Current.GagStorage.GagEquipData.FirstOrDefault(x => x.Key == gagType).Value.GameStain;
    internal IReadOnlyList<byte> GetGagTypeStainIds(GagType gagType)
    {
        var GameStains = _gagStorageConfig.Current.GagStorage.GagEquipData.FirstOrDefault(x => x.Key == gagType).Value.GameStain;
        return [GameStains.Stain1.Id, GameStains.Stain2.Id];
    }
    internal int GetGagTypeSlotId(GagType gagType) => _gagStorageConfig.Current.GagStorage.GagEquipData.FirstOrDefault(x => x.Key == gagType).Value.ActiveSlotId;

    internal void SetGagEnabled(GagType gagType, bool isEnabled)
    {
        _gagStorageConfig.Current.GagStorage.GagEquipData.FirstOrDefault(x => x.Key == gagType).Value.IsEnabled = isEnabled;
        _gagStorageConfig.Save();
    }

    internal void SetGagTypeEquipSlot(GagType gagType, EquipSlot slot)
    {
        _gagStorageConfig.Current.GagStorage.GagEquipData.FirstOrDefault(x => x.Key == gagType).Value.Slot = slot;
        _gagStorageConfig.Save();
    }

    internal void SetGagTypeEquipItem(GagType gagType, EquipItem item)
    {
        _gagStorageConfig.Current.GagStorage.GagEquipData.FirstOrDefault(x => x.Key == gagType).Value.GameItem = item;
        _gagStorageConfig.Save();
    }

    internal void SetGagTypeStain(GagType gagType, StainId stain)
    {
        _gagStorageConfig.Current.GagStorage.GagEquipData.FirstOrDefault(x => x.Key == gagType).Value.GameStain = stain;
        _gagStorageConfig.Save();
    }

    internal void SetGagTypeSlotId(GagType gagType, int slotId)
    {
        _gagStorageConfig.Current.GagStorage.GagEquipData.FirstOrDefault(x => x.Key == gagType).Value.ActiveSlotId = slotId;
        _gagStorageConfig.Save();
    }

    internal void UpdateGagItem(GagType gagType, GagDrawData newData)
    {
        _gagStorageConfig.Current.GagStorage.GagEquipData[gagType] = newData;
        _gagStorageConfig.Save();
        _logger.LogInformation("GagStorage Config Saved");
    }

    /* --------------------- Wardrobe Config Methods --------------------- */
    /// <summary> 
    /// I swear to god, so not set anything inside this object through this fetch. Treat it as readonly.
    /// </summary>
    public List<RestraintSet> StoredRestraintSets => WardrobeConfig.WardrobeStorage.RestraintSets;

    internal int GetActiveSetIdx() => WardrobeConfig.WardrobeStorage.RestraintSets.FindIndex(x => x.Enabled);
    internal RestraintSet GetActiveSet()
    {
        var activeSetIndex = WardrobeConfig.WardrobeStorage.RestraintSets.FindIndex(x => x.Enabled);
        if(activeSetIndex == -1) return null;

        return WardrobeConfig.WardrobeStorage.RestraintSets[activeSetIndex];
    }

    internal RestraintSet GetRestraintSet(int setIndex) => WardrobeConfig.WardrobeStorage.RestraintSets[setIndex];

    // make to see if a set has hardcore properties bound for it.
    internal bool PropertiesEnabledForSet(int setIndexToCheck, string UIDtoCheckPropertiesFor)
    {
        HardcoreSetProperties setProperties = WardrobeConfig.WardrobeStorage.RestraintSets[setIndexToCheck].SetProperties[UIDtoCheckPropertiesFor];
        // if no object for this exists, return false
        if (setProperties == null) return false;
        // check if any properties are enabled
        return setProperties.LegsRestrained || setProperties.ArmsRestrained || setProperties.Gagged || setProperties.Blindfolded || setProperties.Immobile 
            || setProperties.Weighty || setProperties.LightStimulation || setProperties.MildStimulation || setProperties.HeavyStimulation;
    }

    /// <summary> Changes variables for the restraint set to disable them, then saves the config. </summary>
    internal void SetRestraintSetState(UpdatedNewState newState, int setIndex, string UIDofPair)
    {
        if(newState == UpdatedNewState.Disabled)
        {
            WardrobeConfig.WardrobeStorage.RestraintSets[setIndex].Enabled = false;
            WardrobeConfig.WardrobeStorage.RestraintSets[setIndex].EnabledBy = string.Empty;
            _wardrobeConfig.Save();
        }
        else
        {
            WardrobeConfig.WardrobeStorage.RestraintSets[setIndex].Enabled = true;
            WardrobeConfig.WardrobeStorage.RestraintSets[setIndex].EnabledBy = UIDofPair;
            _wardrobeConfig.Save();
        }
    }

    /// <summary> Gets the total count of restraint sets in the wardrobe. </summary>
    internal int GetRestraintSetCount() => WardrobeConfig.WardrobeStorage.RestraintSets.Count;

    /// <summary> Gets index of selected restraint set. </summary>
    internal int GetSelectedSetIdx() => WardrobeConfig.WardrobeStorage.SelectedRestraintSet;

    internal bool IsSetEnabled(int setIndex) => WardrobeConfig.WardrobeStorage.RestraintSets[setIndex].Enabled;

    /// <summary> Gets the DrawData from a wardrobes restraint set. </summary>
    internal List<AssociatedMod> GetAssociatedMods(int setIndex)
    {
        return WardrobeConfig.WardrobeStorage.RestraintSets[setIndex].AssociatedMods;
    }

    /// <summary> adds a mod to the restraint set's associated mods. </summary>
    internal void AddAssociatedMod(int setIndex, AssociatedMod mod)
    {
        // make sure the associated mods list is not already in the list, and if not, add & save.
        if (!WardrobeConfig.WardrobeStorage.RestraintSets[setIndex].AssociatedMods.Contains(mod))
            WardrobeConfig.WardrobeStorage.RestraintSets[setIndex].AssociatedMods.Add(mod);

        _wardrobeConfig.Save();
        _mediator.Publish(new RestraintSetModified(setIndex));
    }

    /// <summary> removes a mod from the restraint set's associated mods. </summary>
    internal void RemoveAssociatedMod(int setIndex, Mod mod)
    {
        // make sure the associated mods list is not already in the list, and if not, add & save.
        var ModToRemove = WardrobeConfig.WardrobeStorage.RestraintSets[setIndex].AssociatedMods.FirstOrDefault(x => x.Mod == mod);
        if (ModToRemove == null) return;

        WardrobeConfig.WardrobeStorage.RestraintSets[setIndex].AssociatedMods.Remove(ModToRemove);
        _wardrobeConfig.Save();
        _mediator.Publish(new RestraintSetModified(setIndex));
    }

    /// <summary> Updates a mod in the restraint set's associated mods. </summary>
    internal void UpdateAssociatedMod(int setIndex, AssociatedMod mod)
    {
        // make sure the associated mods list is not already in the list, and if not, add & save.
        int associatedModIdx = WardrobeConfig.WardrobeStorage.RestraintSets[setIndex].AssociatedMods.FindIndex(x => x == mod);
        if (associatedModIdx == -1) return;

        WardrobeConfig.WardrobeStorage.RestraintSets[setIndex].AssociatedMods[associatedModIdx] = mod;
        _wardrobeConfig.Save();
        _mediator.Publish(new RestraintSetModified(setIndex));
    }

    internal bool IsBlindfoldActive()
    {
        return WardrobeConfig.WardrobeStorage.BlindfoldInfo.IsActive;
    }

    internal void SetBlindfoldState(bool newState, string applierUID)
    {
        WardrobeConfig.WardrobeStorage.BlindfoldInfo.IsActive = newState;
        WardrobeConfig.WardrobeStorage.BlindfoldInfo.BlindfoldedBy = applierUID;
        _wardrobeConfig.Save();
    }

    // this logic is flawed, and so is above, as this should not be manipulated by the client.
    // rework later to fix and make it scan against pair list.
    internal string GetBlindfoldedBy()
    {
        return WardrobeConfig.WardrobeStorage.BlindfoldInfo.BlindfoldedBy;
    }

    internal void SetBlindfoldedBy(string player)
    {
        WardrobeConfig.WardrobeStorage.BlindfoldInfo.BlindfoldedBy = player;
        _wardrobeConfig.Save();
    }

    internal EquipDrawData GetBlindfoldItem()
    {
       return WardrobeConfig.WardrobeStorage.BlindfoldInfo.BlindfoldItem;
    }

    internal void SetBlindfoldItem(EquipDrawData drawData)
    {
        WardrobeConfig.WardrobeStorage.BlindfoldInfo.BlindfoldItem = drawData;
        _wardrobeConfig.Save();
    }

    /* --------------------- Puppeteer Alias Configs --------------------- */
    public List<AliasTrigger> FetchListForPair(string userId)
    {
        if (!_aliasConfig.Current.AliasStorage.ContainsKey(userId))
        {
            _logger.LogDebug("User {userId} does not have an alias list, creating one.", userId);
            // If not, initialize it with a new AliasList object
            _aliasConfig.Current.AliasStorage[userId] = new AliasStorage();
            _aliasConfig.Save();
        }
        return _aliasConfig.Current.AliasStorage[userId].AliasList;
    }
    public void AddAlias(string userId, AliasTrigger alias)
    {
        // Check if the userId key exists in the AliasStorage dictionary
        if (!_aliasConfig.Current.AliasStorage.ContainsKey(userId))
        {
            _logger.LogDebug("User {userId} does not have an alias list, creating one.", userId);
            // If not, initialize it with a new AliasList object
            _aliasConfig.Current.AliasStorage[userId] = new AliasStorage();
        }
        // Add alias logic
        _aliasConfig.Current.AliasStorage[userId].AliasList.Add(alias);
        _aliasConfig.Save();
        _mediator.Publish(new AliasListUpdated(userId));
    }

    public void RemoveAlias(string userId, AliasTrigger alias)
    {
        // Remove alias logic
        _aliasConfig.Current.AliasStorage[userId].AliasList.Remove(alias);
        _aliasConfig.Save();
        _mediator.Publish(new AliasListUpdated(userId));
    }

    public void UpdateAliasInput(string userId, int aliasIndex, string input)
    {
        // Update alias input logic
        _aliasConfig.Current.AliasStorage[userId].AliasList[aliasIndex].InputCommand = input;
        _aliasConfig.Save();
        _mediator.Publish(new AliasListUpdated(userId));
    }

    public void UpdateAliasOutput(string userId, int aliasIndex, string output)
    {
        // Update alias output logic
        _aliasConfig.Current.AliasStorage[userId].AliasList[aliasIndex].OutputCommand = output;
        _aliasConfig.Save();
        _mediator.Publish(new AliasListUpdated(userId));
    }


    /* --------------------- Toybox Pattern Configs --------------------- */
}
