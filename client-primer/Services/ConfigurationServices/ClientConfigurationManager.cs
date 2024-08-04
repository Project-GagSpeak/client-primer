using GagSpeak.GagspeakConfiguration;
using GagSpeak.GagspeakConfiguration.Configurations;
using GagSpeak.GagspeakConfiguration.Models;
using GagSpeak.Interop.Ipc;
using GagSpeak.Services.Mediator;
using GagSpeak.UpdateMonitoring;
using GagSpeak.Utils;
using GagspeakAPI.Data;
using GagspeakAPI.Data.Enum;
using Microsoft.Extensions.FileSystemGlobbing.Internal;
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
        if (UIDtoCheckPropertiesFor == "SelfApplied") return false;

        HardcoreSetProperties setProperties = WardrobeConfig.WardrobeStorage.RestraintSets[setIndexToCheck].SetProperties[UIDtoCheckPropertiesFor];
        // if no object for this exists, return false
        if (setProperties == null) return false;
        // check if any properties are enabled
        return setProperties.LegsRestrained || setProperties.ArmsRestrained || setProperties.Gagged || setProperties.Blindfolded || setProperties.Immobile
            || setProperties.Weighty || setProperties.LightStimulation || setProperties.MildStimulation || setProperties.HeavyStimulation;
    }

    internal void SetRestraintSetState(UpdatedNewState newState, int setIndex, string UIDofPair)
    {
        if (newState == UpdatedNewState.Disabled)
        {
            WardrobeConfig.WardrobeStorage.RestraintSets[setIndex].Enabled = false;
            WardrobeConfig.WardrobeStorage.RestraintSets[setIndex].EnabledBy = string.Empty;
            _wardrobeConfig.Save();
            // publish toggle to mediator
            _mediator.Publish(new RestraintSetToggledMessage(newState, setIndex, UIDofPair));
        }
        else
        {
            // be sure to set the other existing active set, if any, to false first.

            WardrobeConfig.WardrobeStorage.RestraintSets[setIndex].Enabled = true;
            WardrobeConfig.WardrobeStorage.RestraintSets[setIndex].EnabledBy = UIDofPair;
            _wardrobeConfig.Save();
            // publish toggle to mediator
            _mediator.Publish(new RestraintSetToggledMessage(newState, setIndex, UIDofPair));
        }
        // publish to the mediator the toggle message for updates TODO
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

    public void SetPatternState(int idx, bool newState)
    {
        _patternConfig.Current.PatternStorage.Patterns[idx].IsActive = newState;
        _patternConfig.Save();
        if(newState) _mediator.Publish(new PatternActivedMessage(idx));
        else _mediator.Publish(new PatternDeactivedMessage(idx));
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
        // publish to mediator one was modified
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
        _mediator.Publish(new PatternAddedMessage(newPattern));
    }

    public void RemovePattern(int indexToRemove)
    {
        // grab the patternData of the pattern we are removing.
        var patternToRemove = _patternConfig.Current.PatternStorage.Patterns[indexToRemove];
        _patternConfig.Current.PatternStorage.Patterns.RemoveAt(indexToRemove);
        _patternConfig.Save();
        // publish to mediator one was removed
        _mediator.Publish(new PatternRemovedMessage(patternToRemove));
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
        _mediator.Publish(new PlayerCharToyboxChanged(DataUpdateKind.ToyboxAlarmListUpdated));
    }

    public void RemoveAlarm(int indexToRemove)
    {
        _alarmConfig.Current.AlarmStorage.Alarms.RemoveAt(indexToRemove);
        _alarmConfig.Save();
        _mediator.Publish(new PlayerCharToyboxChanged(DataUpdateKind.ToyboxAlarmListUpdated));
    }

    public void SetAlarmState(int idx, bool newState)
    {
        _alarmConfig.Current.AlarmStorage.Alarms[idx].Enabled = newState;
        _alarmConfig.Save();
        _mediator.Publish(new PlayerCharToyboxChanged(DataUpdateKind.ToyboxAlarmListUpdated));
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

}
