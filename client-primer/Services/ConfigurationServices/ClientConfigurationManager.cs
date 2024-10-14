using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using GagSpeak.GagspeakConfiguration;
using GagSpeak.GagspeakConfiguration.Configurations;
using GagSpeak.GagspeakConfiguration.Models;
using GagSpeak.Services.Mediator;
using GagSpeak.UI;
using GagSpeak.UpdateMonitoring;
using GagSpeak.Utils;
using GagspeakAPI.Data;
using GagspeakAPI.Data.Character;
using GagspeakAPI.Extensions;
using ImGuiNET;
using Microsoft.IdentityModel.Tokens;
using Penumbra.GameData.Enums;

namespace GagSpeak.Services.ConfigurationServices;

/// <summary>
/// This configuration manager helps manage the various interactions with all config files related to server-end activity.
/// <para> It provides a comprehensive interface for configuring servers, managing tags and nicknames, and handling authentication keys. </para>
/// </summary>
public class ClientConfigurationManager : DisposableMediatorSubscriberBase
{
    private readonly OnFrameworkService _frameworkUtils;            // a utilities class with methods that work with the Dalamud framework
    private readonly GagspeakConfigService _configService;          // the primary gagspeak config service.
    private readonly GagStorageConfigService _gagStorageConfig;     // the config for the gag storage service (gag storage)
    private readonly WardrobeConfigService _wardrobeConfig;         // the config for the wardrobe service (restraint sets)
    private readonly CursedLootConfigService _cursedLootConfig;     // the config for the cursed loot service (cursed loot storage)
    private readonly AliasConfigService _aliasConfig;               // the config for the alias lists (puppeteer stuff)
    private readonly PatternConfigService _patternConfig;           // the config for the pattern service (toybox pattern storage))
    private readonly AlarmConfigService _alarmConfig;               // the config for the alarm service (toybox alarm storage)
    private readonly TriggerConfigService _triggerConfig;           // the config for the triggers service (toybox triggers storage)

    public ClientConfigurationManager(ILogger<ClientConfigurationManager> logger,
        GagspeakMediator GagspeakMediator, OnFrameworkService onFrameworkService,
        GagspeakConfigService configService, GagStorageConfigService gagStorageConfig,
        WardrobeConfigService wardrobeConfig, CursedLootConfigService cursedLootConfig,
        AliasConfigService aliasConfig, PatternConfigService patternConfig,
        AlarmConfigService alarmConfig, TriggerConfigService triggersConfig) : base(logger, GagspeakMediator)
    {
        // create a new instance of the static universal logger that pulls from the client logger.
        // because this loads our configs before the logger initialized, we use a simply hack to
        // set the static logger to the clientConfigManager logger.
        // its not ideal, but it works. If there is a better way please tell me.
        StaticLogger.Logger = logger;

        _frameworkUtils = onFrameworkService;
        _configService = configService;
        _gagStorageConfig = gagStorageConfig;
        _wardrobeConfig = wardrobeConfig;
        _cursedLootConfig = cursedLootConfig;
        _aliasConfig = aliasConfig;
        _patternConfig = patternConfig;
        _alarmConfig = alarmConfig;
        _triggerConfig = triggersConfig;
        InitConfigs();

        // Subscribe to the connected message update so we know when to update our global permissions
        Mediator.Subscribe<ConnectedMessage>(this, (msg) =>
        {
            // update our configs to point to the new user.
            if (msg.Connection.User.UID != _configService.Current.LastUidLoggedIn)
                UpdateConfigs(msg.Connection.User.UID);
            // update the last logged in UID
            _configService.Current.LastUidLoggedIn = msg.Connection.User.UID;
            Save();
        });

        Mediator.Publish(new UpdateChatListeners());
    }

    // define public access to various storages (THESE ARE ONLY GETTERS, NO SETTERS)
    public GagspeakConfig GagspeakConfig => _configService.Current; // UNIVERSAL
    public GagStorageConfig GagStorageConfig => _gagStorageConfig.Current; // PER PLAYER
    public WardrobeConfig WardrobeConfig => _wardrobeConfig.Current; // PER PLAYER
    public CursedLootConfig CursedLootConfig => _cursedLootConfig.Current; // PER PLAYER
    private AliasConfig AliasConfig => _aliasConfig.Current; // PER PLAYER
    private PatternConfig PatternConfig => _patternConfig.Current; // PER PLAYER
    private AlarmConfig AlarmConfig => _alarmConfig.Current; // PER PLAYER
    private TriggerConfig TriggerConfig => _triggerConfig.Current; // PER PLAYER

    public void UpdateConfigs(string loggedInPlayerUID)
    {
        _gagStorageConfig.UpdateUid(loggedInPlayerUID);
        _wardrobeConfig.UpdateUid(loggedInPlayerUID);
        _cursedLootConfig.UpdateUid(loggedInPlayerUID);
        _aliasConfig.UpdateUid(loggedInPlayerUID);
        _triggerConfig.UpdateUid(loggedInPlayerUID);
        _alarmConfig.UpdateUid(loggedInPlayerUID);

        InitConfigs();
    }

    /// <summary> Saves the GagspeakConfig. </summary>
    public void Save()
    {
        var caller = new StackTrace().GetFrame(1)?.GetMethod()?.ReflectedType?.Name ?? "Unknown";
        Logger.LogDebug("{caller} Calling config save", caller);
        _configService.Save();
    }

    // This is a terrible approach and there is probably a way to deal with it better,
    // but at the moment this handles any commonly known possible holes in config generation.
    public void InitConfigs()
    {
        if (_configService.Current.LoggerFilters.Count == 0)
        {
            Logger.LogWarning("Logger Filters are empty, adding all loggers.");
            _configService.Current.LoggerFilters = LoggerFilter.GetAllRecommendedFilters();
            _configService.Save();
        }

        if (_configService.Current.ChannelsGagSpeak.Count == 0)
        {
            Logger.LogWarning("Channel list is empty, adding Say as the default channel.");
            _configService.Current.ChannelsGagSpeak = new List<ChatChannels> { ChatChannels.Say };
            _configService.Save();

        }
        if (_configService.Current.ChannelsPuppeteer.Count == 0)
        {
            Logger.LogWarning("Channel list is empty, adding Say as the default channel.");
            _configService.Current.ChannelsPuppeteer = new List<ChatChannels> { ChatChannels.Say };
            _configService.Save();
        }

        // create a new storage file
        if (_gagStorageConfig.Current.GagStorage.GagEquipData.IsNullOrEmpty())
        {
            Logger.LogWarning("Gag Storage Config is empty, creating a new one.");
            try
            {
                _gagStorageConfig.Current.GagStorage.GagEquipData = Enum.GetValues(typeof(GagType))
                    .Cast<GagType>().ToDictionary(gagType => gagType, gagType => new GagDrawData(ItemIdVars.NothingItem(EquipSlot.Head)));
                // print the keys in the dictionary
                Logger.LogInformation("Gag Storage Config Created with " + _gagStorageConfig.Current.GagStorage.GagEquipData.Count + " keys", LoggerType.GagManagement);
                _gagStorageConfig.Save();
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Failed to create Gag Storage Config");
            }
        }

        if (_wardrobeConfig.Current.WardrobeStorage.RestraintSets.Any(x => x.RestraintId == Guid.Empty))
        {
            Logger.LogWarning("Wardrobe Storage Config has a restraint set with an empty GUID. Creating a new GUID for it.");
            foreach (var set in _wardrobeConfig.Current.WardrobeStorage.RestraintSets.Where(x => x.RestraintId == Guid.Empty))
            {
                set.RestraintId = Guid.NewGuid();
            }
            _wardrobeConfig.Save();
        }

        // Correct any pattern storage errors that occurs between logins or version updates.
        if (_patternConfig.Current.PatternStorage.Patterns.Any(x => x.UniqueIdentifier == Guid.Empty))
        {
            Logger.LogWarning("Pattern Storage Config has a pattern with an empty GUID. Creating a new GUID for it.");
            foreach (var pattern in _patternConfig.Current.PatternStorage.Patterns.Where(x => x.UniqueIdentifier == Guid.Empty))
            {
                pattern.UniqueIdentifier = Guid.NewGuid();
            }
            _patternConfig.Save();
        }
    }

    public List<string> GetPlayersToListenFor()
    {
        // select from the aliasStorages, the character name and world where the values are not string.empty.
        return AliasConfig.AliasStorage
            .Where(x => x.Value.CharacterName != string.Empty && x.Value.CharacterWorld != string.Empty)
            .Select(x => x.Value.NameWithWorld)
            .ToList();
    }

    /* --------------------- Gag Storage Config Methods --------------------- */
    #region Gag Storage Methods
    internal bool IsGagEnabled(GagType gagType)
        => GagStorageConfig.GagStorage.GagEquipData.FirstOrDefault(x => x.Key == gagType).Value.IsEnabled;
    internal GagDrawData GetDrawData(GagType gagType)
        => GagStorageConfig.GagStorage.GagEquipData[gagType];
    internal GagDrawData GetDrawDataWithHighestPriority(List<GagType> gagTypes)
        => GagStorageConfig.GagStorage.GagEquipData.Where(x => gagTypes.Contains(x.Key)).OrderBy(x => x.Value.CustomizePriority).FirstOrDefault().Value;
    internal EquipSlot GetGagTypeEquipSlot(GagType gagType)
        => _gagStorageConfig.Current.GagStorage.GagEquipData.FirstOrDefault(x => x.Key == gagType).Value.Slot;

    internal void UpdateGagStorageDictionary(Dictionary<GagType, GagDrawData> newGagStorage)
    {
        _gagStorageConfig.Current.GagStorage.GagEquipData = newGagStorage;
        _gagStorageConfig.Save();
        Logger.LogInformation("GagStorage Config Saved");
    }

    internal IReadOnlyList<byte> GetGagTypeStainIds(GagType gagType)
    {
        var GameStains = _gagStorageConfig.Current.GagStorage.GagEquipData.FirstOrDefault(x => x.Key == gagType).Value.GameStain;
        return [GameStains.Stain1.Id, GameStains.Stain2.Id];
    }

    internal void UpdateGagItem(GagType gagType, GagDrawData newData)
    {
        _gagStorageConfig.Current.GagStorage.GagEquipData[gagType] = newData;
        _gagStorageConfig.Save();
        Logger.LogInformation("GagStorage Config Saved");
    }

    internal void SaveGagStorage()
    {
        _gagStorageConfig.Save();
        Logger.LogInformation("GagStorage Config Saved");
    }

    #endregion Gag Storage Methods
    /* --------------------- Wardrobe Config Methods --------------------- */
    #region Wardrobe Config Methods
    /// <summary> 
    /// I swear to god, so not set anything inside this object through this fetch. Treat it as readonly.
    /// </summary>
    internal List<RestraintSet> StoredRestraintSets => WardrobeConfig.WardrobeStorage.RestraintSets;
    public List<string> GetRestraintSetNames() => WardrobeConfig.WardrobeStorage.RestraintSets.Select(set => set.Name).ToList();
    internal int GetActiveSetIdx() => WardrobeConfig.WardrobeStorage.RestraintSets.FindIndex(x => x.Enabled);
    internal int GetSetIdxByGuid(Guid id) => WardrobeConfig.WardrobeStorage.RestraintSets.FindIndex(x => x.RestraintId == id);
    internal RestraintSet? GetActiveSet() => WardrobeConfig.WardrobeStorage.RestraintSets.FirstOrDefault(x => x.Enabled)!; // this can be null.
    internal RestraintSet GetRestraintSet(int setIndex) => WardrobeConfig.WardrobeStorage.RestraintSets[setIndex];
    internal int GetRestraintSetIdxByName(string name) => WardrobeConfig.WardrobeStorage.RestraintSets.FindIndex(x => x.Name == name);

    internal void CloneRestraintSet(RestraintSet setToClone)
    {
        var clonedSet = setToClone.DeepCloneSet();

        clonedSet.Name = EnsureUniqueRestraintName(clonedSet.Name);

        _wardrobeConfig.Current.WardrobeStorage.RestraintSets.Add(clonedSet);
        _wardrobeConfig.Save();
        Logger.LogInformation("Restraint Set added to wardrobe", LoggerType.Restraints);
        // publish to mediator
        Mediator.Publish(new PlayerCharWardrobeChanged(DataUpdateKind.WardrobeRestraintOutfitsUpdated));
    }

    public string EnsureUniqueRestraintName(string baseName)
    {
        // Regex to match the base name and the (X) suffix if it exists
        var suffixPattern = @"^(.*?)(?: \((\d+)\))?$";
        var match = System.Text.RegularExpressions.Regex.Match(baseName, suffixPattern);

        string namePart = match.Groups[1].Value; // The base part of the name
        int currentNumber = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : 0;

        // Increment current number for the new copy
        currentNumber = Math.Max(1, currentNumber);

        string newName = baseName;

        // Ensure the name is unique by appending (X) and incrementing if necessary
        while (WardrobeConfig.WardrobeStorage.RestraintSets.Any(set => set.Name == newName))
        {
            newName = $"{namePart} ({currentNumber++})";
        }

        return newName;
    }

    internal void AddNewRestraintSet(RestraintSet newSet)
    {
        // Ensure the set has a unique name before adding it.
        newSet.Name = EnsureUniqueRestraintName(newSet.Name);

        _wardrobeConfig.Current.WardrobeStorage.RestraintSets.Add(newSet);
        _wardrobeConfig.Save();
        Logger.LogInformation("Restraint Set added to wardrobe", LoggerType.Restraints);
        // publish to mediator
        Mediator.Publish(new PlayerCharWardrobeChanged(DataUpdateKind.WardrobeRestraintOutfitsUpdated));
    }

    internal void AddNewRestraintSets(List<RestraintSet> newSets)
    {
        foreach (var newSet in newSets)
        {
            // add 1 to the name until it is unique.
            while (WardrobeConfig.WardrobeStorage.RestraintSets.Any(x => x.Name == newSet.Name))
            {
                newSet.Name += "(copy)";
            }
            _wardrobeConfig.Current.WardrobeStorage.RestraintSets.Add(newSet);
        }
        _wardrobeConfig.Save();
        Logger.LogInformation("Added " + newSets.Count + " Restraint Sets to wardrobe", LoggerType.Restraints);
        // publish to mediator
        Mediator.Publish(new PlayerCharWardrobeChanged(DataUpdateKind.WardrobeRestraintOutfitsUpdated));
    }

    // remove a restraint set
    internal void RemoveRestraintSet(int setIndex)
    {
        _wardrobeConfig.Current.WardrobeStorage.RestraintSets.RemoveAt(setIndex);
        _wardrobeConfig.Save();
        Mediator.Publish(new PlayerCharWardrobeChanged(DataUpdateKind.WardrobeRestraintOutfitsUpdated));
    }

    // Called whenever set is saved.
    internal void UpdateRestraintSet(int setIndex, RestraintSet updatedSet)
    {
        _wardrobeConfig.Current.WardrobeStorage.RestraintSets[setIndex] = updatedSet;
        _wardrobeConfig.Save();
        Mediator.Publish(new PlayerCharWardrobeChanged(DataUpdateKind.WardrobeRestraintOutfitsUpdated));
    }

    internal bool PropertiesEnabledForSet(int setIndexToCheck, string UIDtoCheckPropertiesFor)
    {
        // do not allow hardcore properties for self.
        if (UIDtoCheckPropertiesFor == Globals.SelfApplied) return false;

        HardcoreSetProperties setProperties = WardrobeConfig.WardrobeStorage.RestraintSets[setIndexToCheck].SetProperties[UIDtoCheckPropertiesFor];
        // if no object for this exists, return false
        if (setProperties == null) return false;
        // check if any properties are enabled
        return setProperties.LegsRestrained || setProperties.ArmsRestrained || setProperties.Gagged || setProperties.Blindfolded || setProperties.Immobile
            || setProperties.Weighty || setProperties.LightStimulation || setProperties.MildStimulation || setProperties.HeavyStimulation;
    }

    internal void SaveWardrobe() => _wardrobeConfig.Save();

    internal void LockRestraintSet(int setIndex, string lockType, string password,
        DateTimeOffset endLockTimeUTC, string UIDofPair, bool pushToServer = true)
    {
        var set = GetRestraintSet(setIndex);
        // set the locked and locked-by status.
        WardrobeConfig.WardrobeStorage.RestraintSets[setIndex].LockType = lockType;
        WardrobeConfig.WardrobeStorage.RestraintSets[setIndex].LockPassword = password;
        WardrobeConfig.WardrobeStorage.RestraintSets[setIndex].LockedUntil = endLockTimeUTC;
        WardrobeConfig.WardrobeStorage.RestraintSets[setIndex].LockedBy = UIDofPair;
        _wardrobeConfig.Save();

        Logger.LogDebug("Restraint Set " + set.Name + " Locked by " + UIDofPair + " with a Padlock of Type: " + lockType
            + "and a password [" + password + "] with [" + (endLockTimeUTC - DateTimeOffset.UtcNow) + "] by " + UIDofPair, LoggerType.Restraints);

        Mediator.Publish(new RestraintSetToggledMessage(setIndex, UIDofPair, NewState.Locked));

        if (pushToServer) Mediator.Publish(new PlayerCharWardrobeChanged(DataUpdateKind.WardrobeRestraintLocked));
    }

    internal void UnlockRestraintSet(int setIndex, string UIDofPair, bool pushToServer = true)
    {
        var set = GetRestraintSet(setIndex);
        // Clear all locked states. (making the assumption this is only called when the UIDofPair matches the LockedBy)
        WardrobeConfig.WardrobeStorage.RestraintSets[setIndex].LockType = Padlocks.None.ToName();
        WardrobeConfig.WardrobeStorage.RestraintSets[setIndex].LockPassword = string.Empty;
        WardrobeConfig.WardrobeStorage.RestraintSets[setIndex].LockedUntil = DateTimeOffset.MinValue;
        WardrobeConfig.WardrobeStorage.RestraintSets[setIndex].LockedBy = string.Empty;
        _wardrobeConfig.Save();

        Mediator.Publish(new RestraintSetToggledMessage(setIndex, UIDofPair, NewState.Unlocked));

        if (pushToServer) Mediator.Publish(new PlayerCharWardrobeChanged(DataUpdateKind.WardrobeRestraintUnlocked));

    }

    internal List<AssociatedMod> GetAssociatedMods(int setIndex) => WardrobeConfig.WardrobeStorage.RestraintSets[setIndex].AssociatedMods;
    internal List<Guid> GetAssociatedMoodles(int setIndex) => WardrobeConfig.WardrobeStorage.RestraintSets[setIndex].AssociatedMoodles;
    internal EquipDrawData GetBlindfoldItem() => WardrobeConfig.WardrobeStorage.BlindfoldInfo.BlindfoldItem;
    internal void SetBlindfoldItem(EquipDrawData drawData)
    {
        WardrobeConfig.WardrobeStorage.BlindfoldInfo.BlindfoldItem = drawData;
        _wardrobeConfig.Save();
    }

    #endregion Wardrobe Config Methods

    /* --------------------- Cursed Loot Config Methods --------------------- */
    #region Cursed Loot Config Methods

    internal string EnsureUniqueLootName(string baseName)
    {
        // Regex to match the base name and the (X) suffix if it exists
        var suffixPattern = @"^(.*?)(?: \((\d+)\))?$";
        var match = System.Text.RegularExpressions.Regex.Match(baseName, suffixPattern);

        string namePart = match.Groups[1].Value; // The base part of the name
        int currentNumber = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : 0;

        // Increment current number for the new copy
        currentNumber = Math.Max(1, currentNumber);

        string newName = baseName;

        // Ensure the name is unique by appending (X) and incrementing if necessary
        while (CursedLootConfig.CursedLootStorage.CursedItems.Any(set => set.Name == newName))
        {
            newName = $"{namePart} ({currentNumber++})";
        }

        return newName;
    }

    internal void AddCursedItem(CursedItem newItem)
    {
        newItem.Name = EnsureUniqueLootName(newItem.Name);
        CursedLootConfig.CursedLootStorage.CursedItems.Add(newItem);
        _cursedLootConfig.Save();
    }

    internal void RemoveCursedItem(Guid lootIdToRemove)
    {
        var idx = CursedLootConfig.CursedLootStorage.CursedItems.FindIndex(x => x.LootId == lootIdToRemove);
        CursedLootConfig.CursedLootStorage.CursedItems.RemoveAt(idx);
        _cursedLootConfig.Save();
    }

    internal void ActivateCursedItem(Guid lootId, DateTimeOffset endTimeUtc)
    {
        var idx = CursedLootConfig.CursedLootStorage.CursedItems.FindIndex(x => x.LootId == lootId);
        if (idx != -1)
        {
            CursedLootConfig.CursedLootStorage.CursedItems[idx].AppliedTime = DateTimeOffset.UtcNow;
            CursedLootConfig.CursedLootStorage.CursedItems[idx].ReleaseTime = endTimeUtc;
            _cursedLootConfig.Save();
        }
    }

    internal void DeactivateCursedItem(Guid lootId)
    {
        var idx = CursedLootConfig.CursedLootStorage.CursedItems.FindIndex(x => x.LootId == lootId);
        if (idx != -1)
        {
            CursedLootConfig.CursedLootStorage.CursedItems[idx].AppliedTime = DateTimeOffset.MinValue;
            CursedLootConfig.CursedLootStorage.CursedItems[idx].ReleaseTime = DateTimeOffset.MinValue;
            _cursedLootConfig.Save();
        }
    }

    internal void SaveCursedLoot() => _cursedLootConfig.Save();

    // Occurs due to safeword, will deactivate all active sets whose apply time is not dateTimeOffset.MinValue.
    internal void ClearCursedItems()
    {
        foreach (var item in CursedLootConfig.CursedLootStorage.CursedItems.Where(x => x.AppliedTime != DateTimeOffset.MinValue))
        {
            item.AppliedTime = DateTimeOffset.MinValue;
            item.ReleaseTime = DateTimeOffset.MinValue;
        }
        _cursedLootConfig.Save();
    }

    #endregion Cursed Loot Config Methods




    /* --------------------- Puppeteer Alias Configs --------------------- */
    #region Alias Config Methods
    public string? GetUidMatchingSender(string name, string world)
        => AliasConfig.AliasStorage.FirstOrDefault(x => x.Value.CharacterName == name && x.Value.CharacterWorld == world).Key;

    public AliasStorage FetchAliasStorageForPair(string userId)
    {
        if (!AliasConfig.AliasStorage.ContainsKey(userId))
        {
            Logger.LogDebug("User " + userId + " does not have an alias list, creating one.", LoggerType.Puppeteer);
            // If not, initialize it with a new AliasList object
            AliasConfig.AliasStorage[userId] = new AliasStorage();
            _aliasConfig.Save();
        }
        return AliasConfig.AliasStorage[userId];
    }

    // Called whenever set is saved.
    internal void UpdateAliasStorage(string userId, AliasStorage newStorage)
    {
        AliasConfig.AliasStorage[userId] = newStorage;
        _aliasConfig.Save();
        Mediator.Publish(new PlayerCharAliasChanged(userId, DataUpdateKind.PuppeteerAliasListUpdated));
    }

    // called from a callback update from other paired client. Never called by self. Meant to set another players name to our config.
    internal void UpdateAliasStoragePlayerInfo(string userId, string charaName, string charaWorld)
    {
        AliasConfig.AliasStorage[userId].CharacterName = charaName;
        AliasConfig.AliasStorage[userId].CharacterWorld = charaWorld;
        _aliasConfig.Save();
        Mediator.Publish(new UpdateChatListeners());
    }

    internal void AddNewAliasTrigger(string userUid, AliasTrigger newTrigger)
    {
        AliasConfig.AliasStorage[userUid].AliasList.Add(newTrigger);
        _aliasConfig.Save();
        Mediator.Publish(new PlayerCharAliasChanged(userUid, DataUpdateKind.PuppeteerAliasListUpdated));
    }

    internal void RemoveAliasTrigger(string userUid, AliasTrigger triggerToRemove)
    {
        // locate where it is.
        var idx = AliasConfig.AliasStorage[userUid].AliasList.FindIndex(x => x == triggerToRemove);
        AliasConfig.AliasStorage[userUid].AliasList.RemoveAt(idx);
        _aliasConfig.Save();
        Mediator.Publish(new PlayerCharAliasChanged(userUid, DataUpdateKind.PuppeteerAliasListUpdated));
    }

    // Called upon whenever we modify the properties of an alias list for a userUID.
    // TODO: Restucture this later to only send updates upon a list save to make less calls.
    internal void AliasDataModified(string userUid)
    {
        _aliasConfig.Save();
        Mediator.Publish(new PlayerCharAliasChanged(userUid, DataUpdateKind.PuppeteerAliasListUpdated));
    }


    #endregion Alias Config Methods
    /* --------------------- Toybox Pattern Configs --------------------- */
    #region Pattern Config Methods

    /// <summary> Fetches the currently Active Alarm to have as a reference accessor. Can be null. </summary>
    public PatternData? GetActiveRunningPattern() => PatternConfig.PatternStorage.Patterns.FirstOrDefault(p => p.IsActive);
    public List<PatternData> GetPatternsForSearch() => PatternConfig.PatternStorage.Patterns;
    public PatternData FetchPattern(int idx) => PatternConfig.PatternStorage.Patterns[idx];
    public PatternData? FetchPatternById(Guid id) => PatternConfig.PatternStorage.Patterns.FirstOrDefault(p => p.UniqueIdentifier == id);
    public Guid GetPatternGuidByName(string name) => PatternConfig.PatternStorage.Patterns.FirstOrDefault(p => p.Name == name)?.UniqueIdentifier ?? Guid.Empty;
    public bool PatternExists(Guid id) => PatternConfig.PatternStorage.Patterns.Any(p => p.UniqueIdentifier == id);
    public bool IsAnyPatternPlaying() => PatternConfig.PatternStorage.Patterns.Any(p => p.IsActive);
    public Guid ActivePatternGuid() => PatternConfig.PatternStorage.Patterns.Where(p => p.IsActive).Select(p => p.UniqueIdentifier).FirstOrDefault();
    public int GetPatternCount() => PatternConfig.PatternStorage.Patterns.Count;
    public TimeSpan GetPatternLength(Guid id) => PatternConfig.PatternStorage.Patterns.FirstOrDefault(p => p.UniqueIdentifier == id)?.Duration ?? TimeSpan.Zero;

    public void AddNewPattern(PatternData newPattern)
    {
        // if a pattern from the patternstorage with the same name already exists, continue.
        if (PatternConfig.PatternStorage.Patterns.Any(x => x.Name == newPattern.Name)) return;
        // otherwise, add it
        PatternConfig.PatternStorage.Patterns.Add(newPattern);
        _patternConfig.Save();
        // publish to mediator one was added
        Logger.LogInformation("Pattern Added: " + newPattern.Name, LoggerType.ToyboxPatterns);
        Mediator.Publish(new PlayerCharToyboxChanged(DataUpdateKind.ToyboxPatternListUpdated));
    }

    // Bulk variant.
    public void AddNewPatterns(List<PatternData> newPattern)
    {
        foreach (var pattern in newPattern)
        {
            // if a pattern from the patternstorage with the same name already exists, continue.
            if (PatternConfig.PatternStorage.Patterns.Any(x => x.Name == pattern.Name)) continue;
            // otherwise, add it
            PatternConfig.PatternStorage.Patterns.Add(pattern);
        }
        _patternConfig.Save();
        // publish to mediator one was added
        Logger.LogInformation("Added: " + newPattern.Count + " Patterns to Toybox", LoggerType.ToyboxPatterns);
        Mediator.Publish(new PlayerCharToyboxChanged(DataUpdateKind.ToyboxPatternListUpdated));
    }

    public void RemovePattern(Guid identifierToRemove)
    {
        // find the pattern to remove by scanning the storage for the unique identifier.
        var indexToRemove = PatternConfig.PatternStorage.Patterns.FindIndex(x => x.UniqueIdentifier == identifierToRemove);
        PatternConfig.PatternStorage.Patterns.RemoveAt(indexToRemove);
        _patternConfig.Save();
        // publish to mediator one was removed
        Mediator.Publish(new PatternRemovedMessage(identifierToRemove));
        Mediator.Publish(new PlayerCharToyboxChanged(DataUpdateKind.ToyboxPatternListUpdated));
    }

    public void SetPatternState(Guid identifier, bool newState, bool shouldPublishToMediator = true)
    {
        // find the pattern to remove by scanning the storage for the unique identifier.
        var idx = PatternConfig.PatternStorage.Patterns.FindIndex(x => x.UniqueIdentifier == identifier);
        PatternConfig.PatternStorage.Patterns[idx].IsActive = newState;
        _patternConfig.Save();
        if (shouldPublishToMediator)
        {
            Mediator.Publish(new PlayerCharToyboxChanged(DataUpdateKind.ToyboxPatternListUpdated));
        }
    }

    public void UpdatePattern(PatternData pattern, int idx)
    {
        PatternConfig.PatternStorage.Patterns[idx] = pattern;
        _patternConfig.Save();
        Mediator.Publish(new PlayerCharToyboxChanged(DataUpdateKind.ToyboxPatternListUpdated));
    }


    public string EnsureUniquePatternName(string baseName)
    {
        // Regex to match the base name and the (X) suffix if it exists
        var suffixPattern = @"^(.*?)(?: \((\d+)\))?$";
        var match = System.Text.RegularExpressions.Regex.Match(baseName, suffixPattern);

        string namePart = match.Groups[1].Value; // The base part of the name
        int currentNumber = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : 0;

        // Increment current number for the new copy
        currentNumber = Math.Max(1, currentNumber);

        string newName = baseName;

        // Ensure the name is unique by appending (X) and incrementing if necessary
        while (PatternConfig.PatternStorage.Patterns.Any(set => set.Name == newName))
        {
            newName = $"{namePart} ({currentNumber++})";
        }

        return newName;
    }

    #endregion Pattern Config Methods
    /* --------------------- Toybox Alarm Configs --------------------- */
    #region Alarm Config Methods
    public int ActiveAlarmCount => AlarmConfig.AlarmStorage.Alarms.Count(x => x.Enabled);
    public Alarm FetchAlarm(int idx) => AlarmConfig.AlarmStorage.Alarms[idx];
    public int FetchAlarmCount() => AlarmConfig.AlarmStorage.Alarms.Count;
    public string GetAlarmPatternName(Guid id) => PatternConfig.PatternStorage.Patterns.FirstOrDefault(p => p.UniqueIdentifier == id)?.Name ?? string.Empty;

    public void RemovePatternNameFromAlarms(Guid patternIdentifier)
    {
        for (int i = 0; i < AlarmConfig.AlarmStorage.Alarms.Count; i++)
        {
            var alarm = AlarmConfig.AlarmStorage.Alarms[i];
            if (alarm.PatternToPlay == patternIdentifier)
            {
                alarm.PatternToPlay = Guid.Empty;
                alarm.PatternDuration = TimeSpan.Zero;
                _alarmConfig.Save();
                Mediator.Publish(new PlayerCharToyboxChanged(DataUpdateKind.ToyboxAlarmListUpdated));
            }
        }
    }

    public void AddNewAlarm(Alarm alarm)
    {
        // ensure the alarm has a unique name.
        int copyNumber = 1;
        string newName = alarm.Name;

        while (AlarmConfig.AlarmStorage.Alarms.Any(set => set.Name == newName))
            newName = alarm.Name + $"(copy{copyNumber++})";

        alarm.Name = newName;
        AlarmConfig.AlarmStorage.Alarms.Add(alarm);
        _alarmConfig.Save();

        Logger.LogInformation("Alarm Added: " + alarm.Name, LoggerType.ToyboxAlarms);
        Mediator.Publish(new PlayerCharToyboxChanged(DataUpdateKind.ToyboxAlarmListUpdated));
    }

    public void RemoveAlarm(int indexToRemove)
    {
        Logger.LogInformation("Alarm Removed: " + AlarmConfig.AlarmStorage.Alarms[indexToRemove].Name, LoggerType.ToyboxAlarms);
        AlarmConfig.AlarmStorage.Alarms.RemoveAt(indexToRemove);
        _alarmConfig.Save();

        Mediator.Publish(new PlayerCharToyboxChanged(DataUpdateKind.ToyboxAlarmListUpdated));
    }

    public void SetAlarmState(int idx, bool newState, bool shouldPublishToMediator = true)
    {
        AlarmConfig.AlarmStorage.Alarms[idx].Enabled = newState;
        _alarmConfig.Save();

        // publish the alarm added/removed based on state
        if (shouldPublishToMediator)
        {
            Mediator.Publish(new PlayerCharToyboxChanged(DataUpdateKind.ToyboxAlarmToggled));
        }
    }

    public void UpdateAlarmStatesFromCallback(List<AlarmInfo> callbackAlarmList)
    {
        // iterate over each alarmInfo in the alarmInfo list. If any of the AlarmStorages alarms have a different enabled state than the alarm info's, change it.
        foreach (AlarmInfo alarmInfo in callbackAlarmList)
        {
            // if the alarm is found in the list,
            if (AlarmConfig.AlarmStorage.Alarms.Any(x => x.Name == alarmInfo.Name))
            {
                // grab the alarm reference
                var alarmRef = AlarmConfig.AlarmStorage.Alarms.FirstOrDefault(x => x.Name == alarmInfo.Name);
                // update the enabled state if the values are different.
                if (alarmRef != null && alarmRef.Enabled != alarmInfo.Enabled)
                {
                    alarmRef.Enabled = alarmInfo.Enabled;
                }
            }
            else
            {
                Logger.LogWarning("Failed to match an Alarm in your list with an alarm in the callbacks list. This shouldnt be possible?");
            }
        }
    }


    public void UpdateAlarm(Alarm alarm, int idx)
    {
        AlarmConfig.AlarmStorage.Alarms[idx] = alarm;
        _alarmConfig.Save();
        Mediator.Publish(new PlayerCharToyboxChanged(DataUpdateKind.ToyboxAlarmListUpdated));
    }

    #endregion Alarm Config Methods

    /* --------------------- Toybox Trigger Configs --------------------- */
    #region Trigger Config Methods
    public List<Trigger> ActiveTriggers => TriggerConfig.TriggerStorage.Triggers.Where(x => x.Enabled).ToList();
    public IEnumerable<ChatTrigger> ActiveChatTriggers => TriggerConfig.TriggerStorage.Triggers.OfType<ChatTrigger>().Where(x => x.Enabled);
    public IEnumerable<SpellActionTrigger> ActiveSpellActionTriggers => TriggerConfig.TriggerStorage.Triggers.OfType<SpellActionTrigger>().Where(x => x.Enabled);
    public IEnumerable<HealthPercentTrigger> ActiveHealthPercentTriggers => TriggerConfig.TriggerStorage.Triggers.OfType<HealthPercentTrigger>().Where(x => x.Enabled);
    public IEnumerable<RestraintTrigger> ActiveRestraintTriggers => TriggerConfig.TriggerStorage.Triggers.OfType<RestraintTrigger>().Where(x => x.Enabled);
    public IEnumerable<GagTrigger> ActiveGagStateTriggers => TriggerConfig.TriggerStorage.Triggers.OfType<GagTrigger>().Where(x => x.Enabled);
    public IEnumerable<SocialTrigger> ActiveSocialTriggers => TriggerConfig.TriggerStorage.Triggers.OfType<SocialTrigger>().Where(x => x.Enabled);

    public List<Trigger> GetTriggersForSearch() => TriggerConfig.TriggerStorage.Triggers; // readonly accessor
    public Trigger FetchTrigger(int idx) => TriggerConfig.TriggerStorage.Triggers[idx];
    public int FetchTriggerCount() => TriggerConfig.TriggerStorage.Triggers.Count;

    public void AddNewTrigger(Trigger alarm)
    {
        // ensure the alarm has a unique name.
        int copyNumber = 1;
        string newName = alarm.Name;

        while (TriggerConfig.TriggerStorage.Triggers.Any(set => set.Name == newName))
            newName = alarm.Name + $"(copy{copyNumber++})";

        alarm.Name = newName;
        TriggerConfig.TriggerStorage.Triggers.Add(alarm);
        _triggerConfig.Save();

        Logger.LogInformation("Trigger Added: " + alarm.Name, LoggerType.ToyboxTriggers);
        Mediator.Publish(new PlayerCharToyboxChanged(DataUpdateKind.ToyboxTriggerListUpdated));
    }

    public void RemoveTrigger(int indexToRemove)
    {
        Logger.LogInformation("Trigger Removed: " + TriggerConfig.TriggerStorage.Triggers[indexToRemove].Name, LoggerType.ToyboxTriggers);
        TriggerConfig.TriggerStorage.Triggers.RemoveAt(indexToRemove);
        _triggerConfig.Save();

        Mediator.Publish(new PlayerCharToyboxChanged(DataUpdateKind.ToyboxTriggerListUpdated));
    }

    public void SetTriggerState(int idx, bool newState, bool shouldPublishToMediator = true)
    {
        TriggerConfig.TriggerStorage.Triggers[idx].Enabled = newState;
        _triggerConfig.Save();

        // publish the alarm added/removed based on state
        if (shouldPublishToMediator)
        {
            Mediator.Publish(new PlayerCharToyboxChanged(DataUpdateKind.ToyboxTriggerToggled));
        }
    }

    public void UpdateTriggerStatesFromCallback(List<TriggerInfo> callbackTriggerList)
    {
        // iterate over each alarmInfo in the alarmInfo list. If any of the TriggerStorages alarms have a different enabled state than the alarm info's, change it.
        foreach (TriggerInfo triggerInfo in callbackTriggerList)
        {
            // if the trigger is found in the list,
            if (TriggerConfig.TriggerStorage.Triggers.Any(x => x.Name == triggerInfo.Name))
            {
                // grab the trigger reference
                var triggerRef = TriggerConfig.TriggerStorage.Triggers.FirstOrDefault(x => x.Name == triggerInfo.Name);
                // update the enabled state if the values are different.
                if (triggerRef != null && triggerRef.Enabled != triggerInfo.Enabled)
                {
                    triggerRef.Enabled = triggerInfo.Enabled;
                }
            }
            else
            {
                Logger.LogWarning("Failed to match an Trigger in your list with an trigger in the callbacks list. This shouldnt be possible?");
            }
        }
    }


    public void UpdateTrigger(Trigger trigger, int idx)
    {
        TriggerConfig.TriggerStorage.Triggers[idx] = trigger;
        _triggerConfig.Save();
        Mediator.Publish(new TriggersModifiedMessage());
        Mediator.Publish(new PlayerCharToyboxChanged(DataUpdateKind.ToyboxTriggerListUpdated));
    }

    #endregion Trigger Config Methods

    public Task DisableEverythingDueToSafeword()
    {
        // disable any active alarms.
        foreach (var alarm in AlarmConfig.AlarmStorage.Alarms)
            alarm.Enabled = false;
        _alarmConfig.Save();

        // disable any active triggers.
        foreach (var trigger in TriggerConfig.TriggerStorage.Triggers)
            trigger.Enabled = false;
        _triggerConfig.Save();
        Mediator.Publish(new PlayerCharToyboxChanged(DataUpdateKind.Safeword));

        return Task.CompletedTask;
    }


    #region API Compilation
    public CharacterToyboxData CompileToyboxToAPI()
    {
        // Map PatternConfig to PatternInfo
        var patternList = new List<PatternInfo>();
        foreach (var pattern in PatternConfig.PatternStorage.Patterns)
        {
            patternList.Add(new PatternInfo
            {
                Identifier = pattern.UniqueIdentifier,
                Name = pattern.Name,
                Description = pattern.Description,
                Duration = pattern.Duration,
                ShouldLoop = pattern.ShouldLoop
            });
        }

        // Map TriggerConfig to TriggerInfo
        var triggerList = new List<TriggerInfo>();
        foreach (var trigger in TriggerConfig.TriggerStorage.Triggers)
        {
            triggerList.Add(new TriggerInfo
            {
                Identifier = trigger.TriggerIdentifier,
                Enabled = trigger.Enabled,
                Name = trigger.Name,
                Description = trigger.Description,
                Type = trigger.Type,
                CanViewAndToggleTrigger = trigger.CanToggleTrigger,
            });
        }

        // Map AlarmConfig to AlarmInfo
        var alarmList = new List<AlarmInfo>();
        foreach (var alarm in AlarmConfig.AlarmStorage.Alarms)
        {
            alarmList.Add(new AlarmInfo
            {
                Identifier = alarm.Identifier,
                Enabled = alarm.Enabled,
                Name = alarm.Name,
                SetTimeUTC = alarm.SetTimeUTC,
                PatternToPlay = alarm.PatternToPlay,
                PatternDuration = alarm.PatternDuration,
                RepeatFrequency = alarm.RepeatFrequency
            });
        }

        // Create and return CharacterToyboxData
        return new CharacterToyboxData
        {
            PatternList = patternList,
            AlarmList = alarmList,
            TriggerList = triggerList
        };
    }
    #endregion API Compilation

    #region UI Prints
    public void DrawWardrobeInfo()
    {
        ImGui.Text("Wardrobe Outfits:");
        ImGui.Indent();
        foreach (var item in WardrobeConfig.WardrobeStorage.RestraintSets)
        {
            ImGui.Text(item.Name);
        }
        ImGui.Unindent();
        var ActiveSet = WardrobeConfig.WardrobeStorage.RestraintSets.FirstOrDefault(x => x.Enabled);
        if (ActiveSet != null)
        {
            ImGui.Text("Active Set Info: ");
            ImGui.Indent();
            ImGui.Text($"Name: {ActiveSet.Name}");
            ImGui.Text($"Description: {ActiveSet.Description}");
            ImGui.Text($"Enabled By: {ActiveSet.EnabledBy}");
            ImGui.Text($"Is Locked: {ActiveSet.Locked}");
            ImGui.Text($"Lock Type: {ActiveSet.LockType}");
            ImGui.Text($"Lock Password: {ActiveSet.LockPassword}");
            ImGui.Text($"Locked By: {ActiveSet.LockedBy}");
            ImGui.Text($"Locked Until: ");
            ImGui.SameLine();
            UiSharedService.DrawTimeLeftFancy(ActiveSet.LockedUntil);
            ImGui.Unindent();
        }
    }


    public void DrawAliasLists()
    {
        using var indent = ImRaii.PushIndent();

        foreach (var alias in AliasConfig.AliasStorage)
        {
            if (ImGui.TreeNode($"Alias Data for {alias.Key}"))
            {
                ImGui.Text("List of Alias's For this User:");
                // begin a table.
                using var table = ImRaii.Table($"##table-for-{alias.Key}", 2);
                if (!table) { return; }

                using var spacing = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, ImGui.GetStyle().ItemInnerSpacing);
                ImGui.TableSetupColumn("If You Say:", ImGuiTableColumnFlags.WidthFixed, ImGuiHelpers.GlobalScale * 100);
                ImGui.TableSetupColumn("They will Execute:", ImGuiTableColumnFlags.WidthStretch);

                foreach (var aliasTrigger in alias.Value.AliasList)
                {
                    ImGui.Separator();
                    ImGui.Text("[INPUT TRIGGER]: ");
                    ImGui.SameLine();
                    ImGui.Text(aliasTrigger.InputCommand);
                    ImGui.NewLine();
                    ImGui.Text("[OUTPUT RESPONSE]: ");
                    ImGui.SameLine();
                    ImGui.Text(aliasTrigger.OutputCommand);
                }
                ImGui.TreePop();
            }
        }
    }

    public void DrawPatternsInfo()
    {
        foreach (var item in PatternConfig.PatternStorage.Patterns)
        {
            ImGui.Text($"Info for Pattern: {item.Name}");
            ImGui.Indent();
            ImGui.Text($"Description: {item.Description}");
            ImGui.Text($"Duration: {item.Duration}");
            ImGui.Text($"Is Active: {item.IsActive}");
            ImGui.Text($"Should Loop: {item.ShouldLoop}");
            ImGui.Unindent();
        }
    }
    #endregion UI Prints
}
