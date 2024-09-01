using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using GagSpeak.ChatMessages;
using GagSpeak.GagspeakConfiguration;
using GagSpeak.GagspeakConfiguration.Configurations;
using GagSpeak.GagspeakConfiguration.Models;
using GagSpeak.Services.Mediator;
using GagSpeak.UpdateMonitoring;
using GagSpeak.Utils;
using GagspeakAPI.Data;
using GagspeakAPI.Data.Character;
using GagspeakAPI.Data.Enum;
using GagspeakAPI.Dto.Connection;
using ImGuiNET;
using Microsoft.IdentityModel.Tokens;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using ProjectGagspeakAPI.Data.VibeServer;
using static GagspeakAPI.Data.Enum.GagList;

namespace GagSpeak.Services.ConfigurationServices;

/// <summary>
/// This configuration manager helps manage the various interactions with all config files related to server-end activity.
/// <para> It provides a comprehensive interface for configuring servers, managing tags and nicknames, and handling authentication keys. </para>
/// </summary>
public class ClientConfigurationManager : DisposableMediatorSubscriberBase
{
    private readonly OnFrameworkService _frameworkUtils;            // a utilities class with methods that work with the Dalamud framework
    private readonly GagspeakConfigService _configService;          // the primary gagspeak config service.
    private readonly GagStorageConfigService _gagStorageConfig;     // the config for the gag storage service (toybox gag storage)
    private readonly WardrobeConfigService _wardrobeConfig;         // the config for the wardrobe service (restraint sets)
    private readonly AliasConfigService _aliasConfig;               // the config for the alias lists (puppeteer stuff)
    private readonly PatternConfigService _patternConfig;           // the config for the pattern service (toybox pattern storage))
    private readonly AlarmConfigService _alarmConfig;               // the config for the alarm service (toybox alarm storage)
    private readonly TriggerConfigService _triggerConfig;          // the config for the triggers service (toybox triggers storage)

    public ClientConfigurationManager(ILogger<ClientConfigurationManager> logger,
        GagspeakMediator GagspeakMediator, OnFrameworkService onFrameworkService,
        GagspeakConfigService configService, GagStorageConfigService gagStorageConfig,
        WardrobeConfigService wardrobeConfig, AliasConfigService aliasConfig,
        PatternConfigService patternConfig, AlarmConfigService alarmConfig,
        TriggerConfigService triggersConfig) : base(logger, GagspeakMediator)
    {
        _frameworkUtils = onFrameworkService;
        _configService = configService;
        _gagStorageConfig = gagStorageConfig;
        _wardrobeConfig = wardrobeConfig;
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
            {
                UpdateConfigs(msg.Connection.User.UID);
            }
            // update the last logged in UID
            _configService.Current.LastUidLoggedIn = msg.Connection.User.UID;
            Save();

            // make sure bratty subs dont use disconnect to think they can get free.
            SyncDataWithConnectionDto(msg.Connection);
        });

        Mediator.Publish(new UpdateChatListeners());
    }

    // define public access to various storages (THESE ARE ONLY GETTERS, NO SETTERS)
    public GagspeakConfig GagspeakConfig => _configService.Current; // UNIVERSAL
    public GagStorageConfig GagStorageConfig => _gagStorageConfig.Current; // PER PLAYER
    private WardrobeConfig WardrobeConfig => _wardrobeConfig.Current; // PER PLAYER
    private AliasConfig AliasConfig => _aliasConfig.Current; // PER PLAYER
    private PatternConfig PatternConfig => _patternConfig.Current; // PER PLAYER
    private AlarmConfig AlarmConfig => _alarmConfig.Current; // PER PLAYER
    private TriggerConfig TriggerConfig => _triggerConfig.Current; // PER PLAYER

    public void UpdateConfigs(string loggedInPlayerUID)
    {
        _gagStorageConfig.UpdateUid(loggedInPlayerUID);
        _wardrobeConfig.UpdateUid(loggedInPlayerUID);
        _aliasConfig.UpdateUid(loggedInPlayerUID);
        _triggerConfig.UpdateUid(loggedInPlayerUID);
        _alarmConfig.UpdateUid(loggedInPlayerUID);

        InitConfigs();
    }

    public bool HasCreatedConfigs()
        => (GagspeakConfig != null && GagStorageConfig != null && WardrobeConfig != null && AliasConfig != null
          && PatternConfig != null && AlarmConfig != null && TriggerConfig != null);

    /// <summary> Saves the GagspeakConfig. </summary>
    public void Save()
    {
        var caller = new StackTrace().GetFrame(1)?.GetMethod()?.ReflectedType?.Name ?? "Unknown";
        Logger.LogDebug("{caller} Calling config save", caller);
        _configService.Save();
    }

    public void InitConfigs()
    {
        if (_configService.Current.ChannelsGagSpeak.Count == 0)
        {
            Logger.LogWarning("Channel list is empty, adding Say as the default channel.");
            _configService.Current.ChannelsGagSpeak = new List<ChatChannel.ChatChannels> { ChatChannel.ChatChannels.Say };
        }
        if (_configService.Current.ChannelsPuppeteer.Count == 0)
        {
            Logger.LogWarning("Channel list is empty, adding Say as the default channel.");
            _configService.Current.ChannelsPuppeteer = new List<ChatChannel.ChatChannels> { ChatChannel.ChatChannels.Say };
        }

        // insure the nicknames and tag configs exist in the main server.
        if (_gagStorageConfig.Current.GagStorage == null) { _gagStorageConfig.Current.GagStorage = new(); }
        // create a new storage file
        if (_gagStorageConfig.Current.GagStorage.GagEquipData.IsNullOrEmpty())
        {
            Logger.LogWarning("Gag Storage Config is empty, creating a new one.");
            try
            {
                _gagStorageConfig.Current.GagStorage.GagEquipData = Enum.GetValues(typeof(GagList.GagType))
                    .Cast<GagList.GagType>().ToDictionary(gagType => gagType, gagType => new GagDrawData(ItemIdVars.NothingItem(EquipSlot.Head)));
                // print the keys in the dictionary
                Logger.LogInformation("Gag Storage Config Created with {count} keys", _gagStorageConfig.Current.GagStorage.GagEquipData.Count);
                _gagStorageConfig.Save();
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Failed to create Gag Storage Config");
            }
        }

        if (_wardrobeConfig.Current.WardrobeStorage == null) { _wardrobeConfig.Current.WardrobeStorage = new(); }

        if (_aliasConfig.Current.AliasStorage == null) { _aliasConfig.Current.AliasStorage = new(); }

        if (_patternConfig.Current.PatternStorage == null) { _patternConfig.Current.PatternStorage = new(); }

        if (_alarmConfig.Current.AlarmStorage == null) { _alarmConfig.Current.AlarmStorage = new(); }
        // check to see if any loaded alarms contain a pattern no longer present.

        if (_triggerConfig.Current.TriggerStorage == null) { _triggerConfig.Current.TriggerStorage = new(); }

    }
    private void SyncDataWithConnectionDto(ConnectionDto dto)
    {
        // TODO: Update this after we turn the activestate into an object from its raw values.
        string assigner = (dto.CharacterActiveStateData.WardrobeActiveSetAssigner == string.Empty) ? "SelfApplied" : dto.CharacterActiveStateData.WardrobeActiveSetAssigner;
        // if the active set is not string.Empty, we should update our active sets.
        if (dto.CharacterActiveStateData.WardrobeActiveSetName != string.Empty)
        {
            SetRestraintSetState(GetRestraintSetIdxByName(dto.CharacterActiveStateData.WardrobeActiveSetName), assigner, UpdatedNewState.Enabled, false);
        }

        // if the set was locked, we should lock it with the appropriate time.
        if (dto.CharacterActiveStateData.WardrobeActiveSetLocked)
        {
            LockRestraintSet(GetRestraintSetIdxByName(dto.CharacterActiveStateData.WardrobeActiveSetName), assigner, dto.CharacterActiveStateData.WardrobeActiveSetLockTime, false);
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
    internal bool IsGagEnabled(GagType gagType) => _gagStorageConfig.Current.GagStorage.GagEquipData.FirstOrDefault(x => x.Key == gagType).Value.IsEnabled;
    internal GagDrawData GetDrawData(GagType gagType) => _gagStorageConfig.Current.GagStorage.GagEquipData[gagType];
    internal EquipSlot GetGagTypeEquipSlot(GagType gagType) => _gagStorageConfig.Current.GagStorage.GagEquipData.FirstOrDefault(x => x.Key == gagType).Value.Slot;
    internal EquipItem GetGagTypeEquipItem(GagType gagType) => _gagStorageConfig.Current.GagStorage.GagEquipData.FirstOrDefault(x => x.Key == gagType).Value.GameItem;
    internal StainIds GetGagTypeStain(GagType gagType) => _gagStorageConfig.Current.GagStorage.GagEquipData.FirstOrDefault(x => x.Key == gagType).Value.GameStain;

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
    internal RestraintSet GetActiveSet() => WardrobeConfig.WardrobeStorage.RestraintSets.FirstOrDefault(x => x.Enabled)!; // this can be null.
    internal RestraintSet GetRestraintSet(int setIndex) => WardrobeConfig.WardrobeStorage.RestraintSets[setIndex];
    internal int GetRestraintSetIdxByName(string name) => WardrobeConfig.WardrobeStorage.RestraintSets.FindIndex(x => x.Name == name);

    internal void DisableActiveSetDueToSafeword()
    {
        // unlock and disable the currently active restraint set due to us using the safeword command.
        var activeSetIdx = GetActiveSetIdx();
        if (activeSetIdx != -1)
        {
            // unlock
            UnlockRestraintSet(activeSetIdx, WardrobeConfig.WardrobeStorage.RestraintSets[activeSetIdx].LockedBy, false);
            // remove.
            SetRestraintSetState(activeSetIdx, WardrobeConfig.WardrobeStorage.RestraintSets[activeSetIdx].EnabledBy, UpdatedNewState.Disabled, false);
            // push mediator for compile.
            Mediator.Publish(new PlayerCharWardrobeChanged(DataUpdateKind.Safeword));
        }
    }

    internal void AddNewRestraintSet(RestraintSet newSet)
    {
        // add 1 to the name until it is unique.
        while (WardrobeConfig.WardrobeStorage.RestraintSets.Any(x => x.Name == newSet.Name))
        {
            newSet.Name += "(copy)";
        }
        _wardrobeConfig.Current.WardrobeStorage.RestraintSets.Add(newSet);
        _wardrobeConfig.Save();
        Logger.LogInformation("Restraint Set added to wardrobe");
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
        Logger.LogInformation("Added {count} Restraint Sets to wardrobe", newSets.Count);
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
        if (UIDtoCheckPropertiesFor == "SelfApplied") return false;

        HardcoreSetProperties setProperties = WardrobeConfig.WardrobeStorage.RestraintSets[setIndexToCheck].SetProperties[UIDtoCheckPropertiesFor];
        // if no object for this exists, return false
        if (setProperties == null) return false;
        // check if any properties are enabled
        return setProperties.LegsRestrained || setProperties.ArmsRestrained || setProperties.Gagged || setProperties.Blindfolded || setProperties.Immobile
            || setProperties.Weighty || setProperties.LightStimulation || setProperties.MildStimulation || setProperties.HeavyStimulation;
    }

    internal void SetRestraintSetState(int setIndex, string UIDofPair, UpdatedNewState newState, bool pushToServer = true)
    {
        if (newState == UpdatedNewState.Disabled)
        {
            WardrobeConfig.WardrobeStorage.RestraintSets[setIndex].Enabled = false;
            WardrobeConfig.WardrobeStorage.RestraintSets[setIndex].EnabledBy = string.Empty;
            _wardrobeConfig.Save();

            var pairHasHardcoreSetForUID = WardrobeConfig.WardrobeStorage.RestraintSets[setIndex].SetProperties.ContainsKey(UIDofPair);
            // see if the properties are enabled for this set for this user
            if (pairHasHardcoreSetForUID && PropertiesEnabledForSet(setIndex, UIDofPair))
            {
                Mediator.Publish(new RestraintSetToggledMessage(setIndex, UIDofPair, newState, true, pushToServer));
            }
            else
            {
                Mediator.Publish(new RestraintSetToggledMessage(setIndex, UIDofPair, newState, false, pushToServer));
            }
        }
        else
        {
            // disable all other restraint sets first
            WardrobeConfig.WardrobeStorage.RestraintSets
                .Where(set => set.Enabled)
                .ToList()
                .ForEach(set =>
                {
                    set.Enabled = false;
                    set.EnabledBy = string.Empty;
                });

            // then enable our set.
            WardrobeConfig.WardrobeStorage.RestraintSets[setIndex].Enabled = true;
            WardrobeConfig.WardrobeStorage.RestraintSets[setIndex].EnabledBy = UIDofPair;
            _wardrobeConfig.Save();

            var pairHasHardcoreSetForUID = WardrobeConfig.WardrobeStorage.RestraintSets[setIndex].SetProperties.ContainsKey(UIDofPair);

            // see if the properties are enabled for this set for this user
            if (pairHasHardcoreSetForUID && PropertiesEnabledForSet(setIndex, UIDofPair))
            {
                Mediator.Publish(new RestraintSetToggledMessage(setIndex, UIDofPair, newState, true, pushToServer));
            }
            else
            {
                Mediator.Publish(new RestraintSetToggledMessage(setIndex, UIDofPair, newState, false, pushToServer));
            }
        }
    }

    internal void LockRestraintSet(int setIndex, string UIDofPair, DateTimeOffset endLockTimeUTC, bool pushToServer = true)
    {
        // set the locked and locked-by status.
        WardrobeConfig.WardrobeStorage.RestraintSets[setIndex].Locked = true;
        WardrobeConfig.WardrobeStorage.RestraintSets[setIndex].LockedBy = UIDofPair;
        WardrobeConfig.WardrobeStorage.RestraintSets[setIndex].LockedUntil = endLockTimeUTC;
        _wardrobeConfig.Save();

        Mediator.Publish(new RestraintSetToggledMessage(setIndex, UIDofPair, UpdatedNewState.Locked, false, pushToServer));
    }

    internal void UnlockRestraintSet(int setIndex, string UIDofPair, bool pushToServer = true)
    {
        // Clear all locked states. (making the assumption this is only called when the UIDofPair matches the LockedBy)
        WardrobeConfig.WardrobeStorage.RestraintSets[setIndex].Locked = false;
        WardrobeConfig.WardrobeStorage.RestraintSets[setIndex].LockedBy = string.Empty;
        WardrobeConfig.WardrobeStorage.RestraintSets[setIndex].LockedUntil = DateTimeOffset.MinValue;
        _wardrobeConfig.Save();

        Mediator.Publish(new RestraintSetToggledMessage(setIndex, UIDofPair, UpdatedNewState.Unlocked, false, pushToServer));
    }



    internal int GetRestraintSetCount() => WardrobeConfig.WardrobeStorage.RestraintSets.Count;
    internal List<AssociatedMod> GetAssociatedMods(int setIndex) => WardrobeConfig.WardrobeStorage.RestraintSets[setIndex].AssociatedMods;
    internal bool IsBlindfoldActive() => WardrobeConfig.WardrobeStorage.BlindfoldInfo.IsActive;

    internal void SetBlindfoldState(bool newState, string applierUID)
    {
        WardrobeConfig.WardrobeStorage.BlindfoldInfo.IsActive = newState;
        WardrobeConfig.WardrobeStorage.BlindfoldInfo.BlindfoldedBy = applierUID;
        _wardrobeConfig.Save();
    }

    // TODO this logic is flawed, and so is above, as this should not be manipulated by the client.
    // rework later to fix and make it scan against pair list.
    internal string GetBlindfoldedBy() => WardrobeConfig.WardrobeStorage.BlindfoldInfo.BlindfoldedBy;
    internal EquipDrawData GetBlindfoldItem() => WardrobeConfig.WardrobeStorage.BlindfoldInfo.BlindfoldItem;
    internal void SetBlindfoldItem(EquipDrawData drawData)
    {
        WardrobeConfig.WardrobeStorage.BlindfoldInfo.BlindfoldItem = drawData;
        _wardrobeConfig.Save();
    }
    #endregion Wardrobe Config Methods

    /* --------------------- Puppeteer Alias Configs --------------------- */
    #region Alias Config Methods
    public string? GetUidMatchingSender(string name, string world)
        => AliasConfig.AliasStorage.FirstOrDefault(x => x.Value.CharacterName == name && x.Value.CharacterWorld == world).Key;

    public AliasStorage FetchAliasStorageForPair(string userId)
    {
        if (!AliasConfig.AliasStorage.ContainsKey(userId))
        {
            Logger.LogDebug("User {userId} does not have an alias list, creating one.", userId);
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


    #endregion Alias Config Methods
    /* --------------------- Toybox Pattern Configs --------------------- */
    #region Pattern Config Methods

    /// <summary> Fetches the currently Active Alarm to have as a reference accessor. Can be null. </summary>
    public PatternData? GetActiveRunningPattern() => PatternConfig.PatternStorage.Patterns.FirstOrDefault(p => p.IsActive);
    public List<PatternData> GetPatternsForSearch() => PatternConfig.PatternStorage.Patterns;
    public PatternData FetchPattern(int idx) => PatternConfig.PatternStorage.Patterns[idx];
    public int GetPatternIdxByName(string name) => PatternConfig.PatternStorage.Patterns.FindIndex(p => p.Name == name);
    public List<string> GetPatternNames() => PatternConfig.PatternStorage.Patterns.Select(set => set.Name).ToList();
    public bool IsIndexInBounds(int index) => index >= 0 && index < PatternConfig.PatternStorage.Patterns.Count;
    public bool IsAnyPatternPlaying() => PatternConfig.PatternStorage.Patterns.Any(p => p.IsActive);
    public int ActivePatternIdx() => PatternConfig.PatternStorage.Patterns.FindIndex(p => p.IsActive);
    public int GetPatternCount() => PatternConfig.PatternStorage.Patterns.Count;
    public TimeSpan GetPatternLength(int idx) => PatternConfig.PatternStorage.Patterns[idx].Duration;

    public void AddNewPattern(PatternData newPattern)
    {
        // if a pattern from the patternstorage with the same name already exists, continue.
        if (PatternConfig.PatternStorage.Patterns.Any(x => x.Name == newPattern.Name)) return;
        // otherwise, add it
        PatternConfig.PatternStorage.Patterns.Add(newPattern);
        _patternConfig.Save();
        // publish to mediator one was added
        Logger.LogInformation("Pattern Added: {0}", newPattern.Name);
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
        Logger.LogInformation("Added: {0} Patterns to Toybox", newPattern.Count);
        Mediator.Publish(new PlayerCharToyboxChanged(DataUpdateKind.ToyboxPatternListUpdated));
    }

    public void RemovePattern(int indexToRemove)
    {
        // grab the patternData of the pattern we are removing.
        var patternToRemove = PatternConfig.PatternStorage.Patterns[indexToRemove];
        PatternConfig.PatternStorage.Patterns.RemoveAt(indexToRemove);
        _patternConfig.Save();
        // publish to mediator one was removed
        Mediator.Publish(new PatternRemovedMessage(patternToRemove));
        Mediator.Publish(new PlayerCharToyboxChanged(DataUpdateKind.ToyboxPatternListUpdated));
    }

    public void SetPatternState(int idx, bool newState, bool shouldPublishToMediator = true)
    {
        // this updates the active state, allowing our playback service by knowing what pattern to play
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

    public string EnsureUniqueName(string baseName)
    {
        int copyNumber = 1;
        string newName = baseName;

        while (PatternConfig.PatternStorage.Patterns.Any(set => set.Name == newName))
            newName = baseName + $"(copy{copyNumber++})";

        return newName;
    }

    #endregion Pattern Config Methods
    /* --------------------- Toybox Alarm Configs --------------------- */
    #region Alarm Config Methods
    public List<Alarm> AlarmsRef => AlarmConfig.AlarmStorage.Alarms; // readonly accessor
    public Alarm FetchAlarm(int idx) => AlarmConfig.AlarmStorage.Alarms[idx];
    public int FetchAlarmCount() => AlarmConfig.AlarmStorage.Alarms.Count;

    internal void DisableAllActiveAlarmsDueToSafeword()
    {
        // disable all active triggers due to us using the safeword command.
        for (int i = 0; i < TriggerConfig.TriggerStorage.Triggers.Count; i++)
        {
            if (TriggerConfig.TriggerStorage.Triggers[i].Enabled)
            {
                SetTriggerState(i, false, false);
            }
        }
        Mediator.Publish(new PlayerCharToyboxChanged(DataUpdateKind.Safeword));
    }

    public void RemovePatternNameFromAlarms(string patternName)
    {
        for (int i = 0; i < AlarmConfig.AlarmStorage.Alarms.Count; i++)
        {
            var alarm = AlarmConfig.AlarmStorage.Alarms[i];
            if (alarm.PatternToPlay == patternName)
            {
                alarm.PatternToPlay = "";
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

        Logger.LogInformation("Alarm Added: {0}", alarm.Name);
        Mediator.Publish(new PlayerCharToyboxChanged(DataUpdateKind.ToyboxAlarmListUpdated));
    }

    public void RemoveAlarm(int indexToRemove)
    {
        Logger.LogInformation("Alarm Removed: {0}", AlarmConfig.AlarmStorage.Alarms[indexToRemove].Name);
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
    public List<Trigger> GetActiveTriggers() => TriggerConfig.TriggerStorage.Triggers.Where(x => x.Enabled).ToList();
    public List<Trigger> GetTriggersForSearch() => TriggerConfig.TriggerStorage.Triggers; // readonly accessor
    public Trigger FetchTrigger(int idx) => TriggerConfig.TriggerStorage.Triggers[idx];
    public int FetchTriggerCount() => TriggerConfig.TriggerStorage.Triggers.Count;

    internal void DisableAllTriggersDueToSafeword()
    {
        // disable all active triggers due to us using the safeword command.
        for (int i = 0; i < TriggerConfig.TriggerStorage.Triggers.Count; i++)
        {
            if (TriggerConfig.TriggerStorage.Triggers[i].Enabled)
            {
                SetTriggerState(i, false, false);
            }
        }
        Mediator.Publish(new PlayerCharToyboxChanged(DataUpdateKind.Safeword));
    }

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

        Logger.LogInformation("Trigger Added: {0}", alarm.Name);
        Mediator.Publish(new PlayerCharToyboxChanged(DataUpdateKind.ToyboxTriggerListUpdated));
    }

    public void RemoveTrigger(int indexToRemove)
    {
        Logger.LogInformation("Trigger Removed: {0}", TriggerConfig.TriggerStorage.Triggers[indexToRemove].Name);
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
            Mediator.Publish(new PlayerCharToyboxChanged(DataUpdateKind.ToyboxTriggerActiveStatusChanged));
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
        Mediator.Publish(new PlayerCharToyboxChanged(DataUpdateKind.ToyboxTriggerListUpdated));
    }

    #endregion Trigger Config Methods

    #region API Compilation
    public CharacterToyboxData CompileToyboxToAPI()
    {
        // Map PatternConfig to PatternInfo
        var patternList = new List<PatternInfo>();
        foreach (var pattern in PatternConfig.PatternStorage.Patterns)
        {
            patternList.Add(new PatternInfo
            {
                Name = pattern.Name,
                Description = pattern.Description,
                Duration = pattern.Duration,
                IsActive = pattern.IsActive,
                ShouldLoop = pattern.ShouldLoop
            });
        }

        // Map TriggerConfig to TriggerInfo
        var triggerList = new List<TriggerInfo>();
        foreach (var trigger in TriggerConfig.TriggerStorage.Triggers)
        {
            triggerList.Add(new TriggerInfo
            {
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
            ImGui.Text($"Locked By: {ActiveSet.LockedBy}");
            ImGui.Text($"Locked Until: {ActiveSet.LockedUntil}");
            ImGui.Unindent();
        }
    }


    public void DrawAliasLists()
    {
        foreach (var alias in AliasConfig.AliasStorage)
        {
            if (ImGui.CollapsingHeader($"Alias Data for {alias.Key}"))
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
