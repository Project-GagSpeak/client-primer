using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using GagSpeak.GagspeakConfiguration;
using GagSpeak.GagspeakConfiguration.Configurations;
using GagSpeak.GagspeakConfiguration.Models;
using GagSpeak.Interop.Ipc;
using GagSpeak.PlayerData.Data;
using GagSpeak.Services.Mediator;
using GagSpeak.UpdateMonitoring;
using GagSpeak.Utils;
using GagspeakAPI.Data;
using GagspeakAPI.Data.Character;
using GagspeakAPI.Data.Enum;
using GagspeakAPI.Dto.Connection;
using ImGuiNET;
using JetBrains.Annotations;
using Microsoft.Extensions.FileSystemGlobbing.Internal;
using Microsoft.IdentityModel.Tokens;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using ProjectGagspeakAPI.Data.VibeServer;
using System.Security.Claims;
using static GagspeakAPI.Data.Enum.GagList;
using static PInvoke.User32;

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
    private readonly AlarmConfigService _alarmConfig;               // the config for the alarm service (toybox alarm storage)
    private readonly TriggerConfigService _triggersConfig;          // the config for the triggers service (toybox triggers storage)

    public ClientConfigurationManager(ILogger<ClientConfigurationManager> logger,
        OnFrameworkService onFrameworkService, GagspeakMediator GagspeakMediator,
        GagspeakConfigService configService, GagStorageConfigService gagStorageConfig,
        WardrobeConfigService wardrobeConfig, AliasConfigService aliasConfig,
        PatternConfigService patternConfig, AlarmConfigService alarmConfig,
        TriggerConfigService triggersConfig)
    {
        _logger = logger;
        _frameworkUtils = onFrameworkService;
        _mediator = GagspeakMediator;
        _configService = configService;
        _gagStorageConfig = gagStorageConfig;
        _wardrobeConfig = wardrobeConfig;
        _aliasConfig = aliasConfig;
        _patternConfig = patternConfig;
        _alarmConfig = alarmConfig;
        _triggersConfig = triggersConfig;

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
        if (_alarmConfig.Current.AlarmStorage == null) { _alarmConfig.Current.AlarmStorage = new(); }
        if (_triggersConfig.Current.TriggerStorage == null) { _triggersConfig.Current.TriggerStorage = new(); }

    }

    // define public access to various storages (THESE ARE ONLY GETTERS, NO SETTERS)
    public GagspeakConfig GagspeakConfig => _configService.Current;
    public GagStorageConfig GagStorageConfig => _gagStorageConfig.Current;
    private WardrobeConfig WardrobeConfig => _wardrobeConfig.Current;
    private AliasConfig AliasConfig => _aliasConfig.Current;
    private PatternConfig PatternConfig => _patternConfig.Current;
    private AlarmConfig AlarmConfig => _alarmConfig.Current;
    private TriggerConfig TriggerConfig => _triggersConfig.Current;


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

    #region ConnectionDto Update Methods
    public void SyncDataWithConnectionDto(ConnectionDto dto)
    {
        string assigner = (dto.WardrobeActiveSetAssigner == string.Empty) ? "SelfApplied" : dto.WardrobeActiveSetAssigner;
        // if the active set is not string.Empty, we should update our active sets.
        if (dto.WardrobeActiveSetName != string.Empty)
        {
            SetRestraintSetState(UpdatedNewState.Enabled, GetRestraintSetIdxByName(dto.WardrobeActiveSetName), assigner, false);
        }

        // if the set was locked, we should lock it with the appropriate time.
        if(dto.WardrobeActiveSetLocked)
        {
            LockRestraintSet(GetRestraintSetIdxByName(dto.WardrobeActiveSetName), assigner, dto.WardrobeActiveSetLockTime, false);
        }

        // if active pattern was playing, resume it at the stopped time. TODO: Implement this logic.

    }


    #endregion ConnectionDto Update Methods

    /* --------------------- Gag Storage Config Methods --------------------- */
    #region Gag Storage Methods
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

    internal void AddNewRestraintSet(RestraintSet newSet)
    {
        // add 1 to the name until it is unique.
        while (WardrobeConfig.WardrobeStorage.RestraintSets.Any(x => x.Name == newSet.Name))
        {
            newSet.Name += "(copy)";
        }
        _wardrobeConfig.Current.WardrobeStorage.RestraintSets.Add(newSet);
        _wardrobeConfig.Save();
        _logger.LogInformation("Restraint Set added to wardrobe");
        // publish to mediator
        _mediator.Publish(new RestraintSetAddedMessage(newSet));
    }

    // remove a restraint set
    internal void RemoveRestraintSet(int setIndex)
    {
        _wardrobeConfig.Current.WardrobeStorage.RestraintSets.RemoveAt(setIndex);
        _wardrobeConfig.Save();
        _mediator.Publish(new RestraintSetRemovedMessage(setIndex));
    }

    internal void UpdateRestraintSet(int setIndex, RestraintSet updatedSet)
    {
        _wardrobeConfig.Current.WardrobeStorage.RestraintSets[setIndex] = updatedSet;
        _wardrobeConfig.Save();
        _mediator.Publish(new RestraintSetModified(setIndex));
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

    internal void SetRestraintSetState(UpdatedNewState newState, int setIndex, string UIDofPair, bool shouldPublishToMediator = true)
    {
        if (newState == UpdatedNewState.Disabled)
        {
            WardrobeConfig.WardrobeStorage.RestraintSets[setIndex].Enabled = false;
            WardrobeConfig.WardrobeStorage.RestraintSets[setIndex].EnabledBy = string.Empty;
            _wardrobeConfig.Save();
            // publish toggle to mediator
            _mediator.Publish(new RestraintSetToggledMessage(newState, setIndex, UIDofPair, shouldPublishToMediator));
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
            // publish toggle to mediator
            _mediator.Publish(new RestraintSetToggledMessage(newState, setIndex, UIDofPair, shouldPublishToMediator));
        }
    }

    internal void LockRestraintSet(int setIndex, string UIDofPair, DateTimeOffset endLockTimeUTC, bool shouldPublishToMediator = true)
    {
        // Ensure we are doing this to ourselves. (Possibly change later to remove entirely once we handle callbacks)
        if (UIDofPair != "SelfApplied") return;

        // set the locked and locked-by status.
        WardrobeConfig.WardrobeStorage.RestraintSets[setIndex].Locked = true;
        WardrobeConfig.WardrobeStorage.RestraintSets[setIndex].LockedBy = UIDofPair;
        WardrobeConfig.WardrobeStorage.RestraintSets[setIndex].LockedUntil = endLockTimeUTC;
        _wardrobeConfig.Save();
        
        _mediator.Publish(new RestraintSetToggledMessage(UpdatedNewState.Locked, setIndex, UIDofPair, shouldPublishToMediator));
    }

    internal void UnlockRestraintSet(int setIndex, string UIDofPair, bool shouldPublishToMediator = true)
    {
        // Ensure we are doing this to ourselves. (Possibly change later to remove entirely once we handle callbacks)
        if (UIDofPair != "SelfApplied") return;

        // Clear all locked states. (making the assumption this is only called when the UIDofPair matches the LockedBy)
        WardrobeConfig.WardrobeStorage.RestraintSets[setIndex].Locked = false;
        WardrobeConfig.WardrobeStorage.RestraintSets[setIndex].LockedBy = string.Empty;
        WardrobeConfig.WardrobeStorage.RestraintSets[setIndex].LockedUntil = DateTimeOffset.MinValue;
        _wardrobeConfig.Save();
        
        _mediator.Publish(new RestraintSetToggledMessage(UpdatedNewState.Unlocked, setIndex, UIDofPair, shouldPublishToMediator));
    }



    internal int GetRestraintSetCount() => WardrobeConfig.WardrobeStorage.RestraintSets.Count;
    internal List<AssociatedMod> GetAssociatedMods(int setIndex) => WardrobeConfig.WardrobeStorage.RestraintSets[setIndex].AssociatedMods;

    internal void AddAssociatedMod(int setIndex, AssociatedMod mod)
    {
        // make sure the associated mods list is not already in the list, and if not, add & save.
        if (!WardrobeConfig.WardrobeStorage.RestraintSets[setIndex].AssociatedMods.Contains(mod))
            WardrobeConfig.WardrobeStorage.RestraintSets[setIndex].AssociatedMods.Add(mod);

        _wardrobeConfig.Save();
        _mediator.Publish(new RestraintSetModified(setIndex));
    }

    internal void RemoveAssociatedMod(int setIndex, Mod mod)
    {
        // make sure the associated mods list is not already in the list, and if not, add & save.
        var ModToRemove = WardrobeConfig.WardrobeStorage.RestraintSets[setIndex].AssociatedMods.FirstOrDefault(x => x.Mod == mod);
        if (ModToRemove == null) return;

        WardrobeConfig.WardrobeStorage.RestraintSets[setIndex].AssociatedMods.Remove(ModToRemove);
        _wardrobeConfig.Save();
        _mediator.Publish(new RestraintSetModified(setIndex));
    }

    internal void UpdateAssociatedMod(int setIndex, AssociatedMod mod)
    {
        // make sure the associated mods list is not already in the list, and if not, add & save.
        int associatedModIdx = WardrobeConfig.WardrobeStorage.RestraintSets[setIndex].AssociatedMods.FindIndex(x => x == mod);
        if (associatedModIdx == -1) return;

        WardrobeConfig.WardrobeStorage.RestraintSets[setIndex].AssociatedMods[associatedModIdx] = mod;
        _wardrobeConfig.Save();
        _mediator.Publish(new RestraintSetModified(setIndex));
    }

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
        _mediator.Publish(new PlayerCharAliasChanged(userId));
    }

    public void RemoveAlias(string userId, AliasTrigger alias)
    {
        // Remove alias logic
        _aliasConfig.Current.AliasStorage[userId].AliasList.Remove(alias);
        _aliasConfig.Save();
        _mediator.Publish(new PlayerCharAliasChanged(userId));
    }

    public void UpdateAliasInput(string userId, int aliasIndex, string input)
    {
        // Update alias input logic
        _aliasConfig.Current.AliasStorage[userId].AliasList[aliasIndex].InputCommand = input;
        _aliasConfig.Save();
        _mediator.Publish(new PlayerCharAliasChanged(userId));
    }

    public void UpdateAliasOutput(string userId, int aliasIndex, string output)
    {
        // Update alias output logic
        _aliasConfig.Current.AliasStorage[userId].AliasList[aliasIndex].OutputCommand = output;
        _aliasConfig.Save();
        _mediator.Publish(new PlayerCharAliasChanged(userId));
    }

    #endregion Alias Config Methods
    /* --------------------- Toybox Pattern Configs --------------------- */
    #region Pattern Config Methods
    public PatternData FetchPattern(int idx) => _patternConfig.Current.PatternStorage.Patterns[idx];
    public int GetPatternIdxByName(string name) => _patternConfig.Current.PatternStorage.Patterns.FindIndex(p => p.Name == name);
    public List<string> GetPatternNames() => _patternConfig.Current.PatternStorage.Patterns.Select(set => set.Name).ToList();
    public bool IsIndexInBounds(int index) => index >= 0 && index < _patternConfig.Current.PatternStorage.Patterns.Count;
    public bool IsAnyPatternPlaying() => _patternConfig.Current.PatternStorage.Patterns.Any(p => p.IsActive);
    public int ActivePatternIdx() => _patternConfig.Current.PatternStorage.Patterns.FindIndex(p => p.IsActive);
    public int GetPatternCount() => _patternConfig.Current.PatternStorage.Patterns.Count;

    public TimeSpan GetPatternLength(int idx)
    {
        var pattern = _patternConfig.Current.PatternStorage.Patterns[idx].Duration;

        if (string.IsNullOrWhiteSpace(pattern) || !TimeSpan.TryParseExact(pattern, "mm\\:ss", null, out var timespanDuration))
        {
            timespanDuration = TimeSpan.Zero; // Default to 0 minutes and 0 seconds
        }
        return timespanDuration;
    }

    // TODO: Make patterns mimic the similar edit and save state as the Alarms. Current implementation will spam API too much.
    public void SetPatternState(int idx, bool newState, bool shouldPublishToMediator = true)
    {
        _patternConfig.Current.PatternStorage.Patterns[idx].IsActive = newState;
        _patternConfig.Save();
        if (newState)
        {
            _mediator.Publish(new PatternActivedMessage(idx));
        }
        else
        {
            _mediator.Publish(new PatternDeactivedMessage(idx));
        }
        // Push update if we should publish
        if (shouldPublishToMediator)
        {
            _mediator.Publish(new PlayerCharToyboxChanged(DataUpdateKind.ToyboxPatternListUpdated));
        }
    }

    public void SetNameForPattern(int idx, string newName)
    {
        var newNameFinalized = EnsureUniqueName(newName);
        _patternConfig.Current.PatternStorage.Patterns[idx].Name = newNameFinalized;
        _patternConfig.Save();
        _mediator.Publish(new PatternDataChanged(idx));
    }

    public void ModifyDescription(int index, string newDescription)
    {
        _patternConfig.Current.PatternStorage.Patterns[index].Description = newDescription;
        _patternConfig.Save();
        _mediator.Publish(new PatternDataChanged(index));
    }

    public void SetAuthorForPattern(int idx, string newAuthor)
    {
        _patternConfig.Current.PatternStorage.Patterns[idx].Author = newAuthor;
        _patternConfig.Save();
        _mediator.Publish(new PatternDataChanged(idx));
    }

    public void AddTagToPattern(int idx, string newTag)
    {
        _patternConfig.Current.PatternStorage.Patterns[idx].Tags.Add(newTag);
        _patternConfig.Save();
        _mediator.Publish(new PatternDataChanged(idx));
    }

    public void RemoveTagFromPattern(int idx, string tagToRemove)
    {
        _patternConfig.Current.PatternStorage.Patterns[idx].Tags.Remove(tagToRemove);
        _patternConfig.Save();
        _mediator.Publish(new PatternDataChanged(idx));
    }

    public void SetPatternLoops(int idx, bool loops)
    {
        _patternConfig.Current.PatternStorage.Patterns[idx].ShouldLoop = loops;
        _patternConfig.Save();
        _mediator.Publish(new PatternDataChanged(idx));
    }

    public bool GetUserIsAllowedToView(int idx, string userId) =>
        _patternConfig.Current.PatternStorage.Patterns[idx].AllowedUsers.Contains(userId);


    public void AddTrustedUserToPattern(int idx, string userId)
    {
        _patternConfig.Current.PatternStorage.Patterns[idx].AllowedUsers.Add(userId);
        _patternConfig.Save();
        _mediator.Publish(new PatternDataChanged(idx));
    }

    public void RemoveTrustedUserFromPattern(int idx, string userId)
    {
        _patternConfig.Current.PatternStorage.Patterns[idx].AllowedUsers.Remove(userId);
        _patternConfig.Save();
        _mediator.Publish(new PatternDataChanged(idx));
    }

    public string EnsureUniqueName(string baseName)
    {
        int copyNumber = 1;
        string newName = baseName;

        while (_patternConfig.Current.PatternStorage.Patterns.Any(set => set.Name == newName))
            newName = baseName + $"(copy{copyNumber++})";

        return newName;
    }

    public void AddNewPattern(PatternData newPattern)
    {
        _patternConfig.Current.PatternStorage.Patterns.Add(newPattern);
        _patternConfig.Save();
        // publish to mediator one was added
        _mediator.Publish(new PlayerCharToyboxChanged(DataUpdateKind.ToyboxPatternListUpdated));
    }

    public void RemovePattern(int indexToRemove)
    {
        // grab the patternData of the pattern we are removing.
        var patternToRemove = _patternConfig.Current.PatternStorage.Patterns[indexToRemove];
        _patternConfig.Current.PatternStorage.Patterns.RemoveAt(indexToRemove);
        _patternConfig.Save();
        // publish to mediator one was removed
        _mediator.Publish(new PatternRemovedMessage(patternToRemove));

        // TODO: This will chain call 2 update pushes. Make sure that we either seperate them or handle accordingly.
        _mediator.Publish(new PlayerCharToyboxChanged(DataUpdateKind.ToyboxPatternListUpdated));
    }

    #endregion Pattern Config Methods
    /* --------------------- Toybox Alarm Configs --------------------- */
    #region Alarm Config Methods
    public List<Alarm> AlarmsRef => _alarmConfig.Current.AlarmStorage.Alarms; // readonly accessor
    public Alarm FetchAlarm(int idx) => _alarmConfig.Current.AlarmStorage.Alarms[idx];
    public int FetchAlarmCount() => _alarmConfig.Current.AlarmStorage.Alarms.Count;

    public void RemovePatternNameFromAlarms(string patternName)
    {
        for (int i = 0; i < _alarmConfig.Current.AlarmStorage.Alarms.Count; i++)
        {
            var alarm = _alarmConfig.Current.AlarmStorage.Alarms[i];
            if (alarm.PatternToPlay == patternName)
            {
                alarm.PatternToPlay = "";
                alarm.PatternDuration = "00:00";
                _alarmConfig.Save();
                _mediator.Publish(new PlayerCharToyboxChanged(DataUpdateKind.ToyboxAlarmListUpdated));
            }
        }
    }

    public void AddNewAlarm(Alarm alarm)
    {
        // ensure the alarm has a unique name.
        int copyNumber = 1;
        string newName = alarm.Name;

        while (_alarmConfig.Current.AlarmStorage.Alarms.Any(set => set.Name == newName))
            newName = alarm.Name + $"(copy{copyNumber++})";

        alarm.Name = newName;
        _alarmConfig.Current.AlarmStorage.Alarms.Add(alarm);
        _alarmConfig.Save();

        _logger.LogInformation("Alarm Added: {0}", alarm.Name);
        _mediator.Publish(new PlayerCharToyboxChanged(DataUpdateKind.ToyboxAlarmListUpdated));
    }

    public void RemoveAlarm(int indexToRemove)
    {
        _logger.LogInformation("Alarm Removed: {0}", _alarmConfig.Current.AlarmStorage.Alarms[indexToRemove].Name);
        _alarmConfig.Current.AlarmStorage.Alarms.RemoveAt(indexToRemove);
        _alarmConfig.Save();

        _mediator.Publish(new PlayerCharToyboxChanged(DataUpdateKind.ToyboxAlarmListUpdated));
    }

    public void SetAlarmState(int idx, bool newState, bool shouldPublishToMediator = true)
    {
        _alarmConfig.Current.AlarmStorage.Alarms[idx].Enabled = newState;
        _alarmConfig.Save();

        // publish the alarm added/removed based on state
        if (shouldPublishToMediator)
        {
            _mediator.Publish(new PlayerCharToyboxChanged(DataUpdateKind.ToyboxAlarmToggled));
        }
    }

    public void UpdateAlarmStatesFromCallback(List<AlarmInfo> callbackAlarmList)
    {
        // iterate over each alarmInfo in the alarmInfo list. If any of the AlarmStorages alarms have a different enabled state than the alarm info's, change it.
        foreach (AlarmInfo alarmInfo in callbackAlarmList)
        {
            // if the alarm is found in the list,
            if (_alarmConfig.Current.AlarmStorage.Alarms.Any(x => x.Name == alarmInfo.Name))
            {
                // grab the alarm reference
                var alarmRef = _alarmConfig.Current.AlarmStorage.Alarms.FirstOrDefault(x => x.Name == alarmInfo.Name);
                // update the enabled state if the values are different.
                if (alarmRef != null && alarmRef.Enabled != alarmInfo.Enabled)
                {
                    alarmRef.Enabled = alarmInfo.Enabled;
                }
            }
            else
            {
                _logger.LogWarning("Failed to match an Alarm in your list with an alarm in the callbacks list. This shouldnt be possible?");
            }
        }
    }


    public void UpdateAlarm(Alarm alarm, int idx)
    {
        _alarmConfig.Current.AlarmStorage.Alarms[idx] = alarm;
        _alarmConfig.Save();
        _mediator.Publish(new PlayerCharToyboxChanged(DataUpdateKind.ToyboxAlarmListUpdated));
    }

    #endregion Alarm Config Methods

    /* --------------------- Toybox Trigger Configs --------------------- */
    #region Trigger Config Methods

    // stuff

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
                CanViewAndToggleTrigger = trigger.CanTrigger,
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
