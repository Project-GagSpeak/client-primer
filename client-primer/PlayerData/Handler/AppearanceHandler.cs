using GagSpeak.GagspeakConfiguration.Models;
using GagSpeak.Interop.Ipc;
using GagSpeak.Interop.IpcHelpers.Moodles;
using GagSpeak.PlayerData.Data;
using GagSpeak.PlayerData.Pairs;
using GagSpeak.PlayerData.Services;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using GagSpeak.UI.Components;
using GagSpeak.Utils;
using GagspeakAPI.Data.IPC;
using GagspeakAPI.Extensions;
using Penumbra.Api.Enums;
using Penumbra.GameData.Enums;
using static FFXIVClientStructs.FFXIV.Component.GUI.AtkCounterNode.Delegates;

namespace GagSpeak.PlayerData.Handlers;

/// <summary>
/// AppearanceHandler is responcible for handling any changes to the client player's appearance.
/// These changes can be made by self or other players.
/// <para>
/// Appearance Handler's Primary responcibility is to ensure that the data in the Appearance Service 
/// class remains synchronized with the most recent information.
/// </para>
/// </summary>
public sealed class AppearanceHandler : DisposableMediatorSubscriberBase
{
    private readonly ClientConfigurationManager _clientConfigs;
    private readonly PlayerCharacterData _playerData;
    private readonly GagManager _gagManager;
    private readonly PairManager _pairManager;
    private readonly IpcManager _ipcManager;
    private readonly AppearanceService _appearanceService;

    public AppearanceHandler(ILogger<AppearanceHandler> logger, GagspeakMediator mediator,
        ClientConfigurationManager clientConfigs, PlayerCharacterData playerData,
        GagManager gagManager, PairManager pairManager, IpcManager ipcManager,
        AppearanceService appearanceService) : base(logger, mediator)
    {
        _clientConfigs = clientConfigs;
        _playerData = playerData;
        _gagManager = gagManager;
        _pairManager = pairManager;
        _ipcManager = ipcManager;
        _appearanceService = appearanceService;

        //IpcFastUpdates.StatusManagerChangedEventFired += (addr) => UpdateLatestMoodleData(addr);
    }

    private List<RestraintSet> RestraintSets => _clientConfigs.WardrobeConfig.WardrobeStorage.RestraintSets;
    private List<CursedItem> CursedItems => _clientConfigs.CursedLootConfig.CursedLootStorage.CursedItems;

    /// <summary>
    /// The Finalized Glamourer Appearance that should be visible on the player.
    /// THIS CLASS IS RESPONCIBLE FOR MAINTAINING ITS SYNCRONIZATION.
    /// </summary>
    private Dictionary<EquipSlot, IGlamourItem> ItemsToApply => _appearanceService.ItemsToApply;
    /// <summary>
    /// Finalized MetaData to apply from highest priority item requesting it.
    /// </summary>
    private IpcCallerGlamourer.MetaData MetaToApply => _appearanceService.MetaToApply;

    /// <summary>
    /// The collective expected list of Moodles that should be applied to the player.
    /// </summary>
    private List<Guid> ExpectedMoodles => _appearanceService.ExpectedMoodles;

    /// <summary>
    /// The Latest Client Moodles Status List since the last update.
    /// This usually updates whenever the IPC updates, however if we need an immidate fast refresh, 
    /// the fast updater here updates it directly.
    /// </summary>
    public static List<MoodlesStatusInfo> LatestClientMoodleStatusList = new();

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        //IpcFastUpdates.StatusManagerChangedEventFired -= (addr) => UpdateLatestMoodleData(addr);
    }


    private async Task UpdateLatestMoodleData()
    {
        LatestClientMoodleStatusList = await _ipcManager.Moodles.GetStatusInfoAsync() ?? new();
    }


    /// <summary>
    /// This logic will occur after a Restraint Set has been enabled via the WardrobeHandler.
    /// </summary>
    public async Task EnableRestraintSet(Guid restraintID, string assignerUID = Globals.SelfApplied, bool pushToServer = true)
    {
        Logger.LogTrace("ENABLE-SET Executed");
        if (_clientConfigs.GetActiveSet() is not null)
        {
            Logger.LogError("You must Disable the active Set before calling this!", LoggerType.Restraints);
            return;
        }

        // Enable the set. For starters we should apply the mods.
        var setIdx = RestraintSets.FindIndex(x => x.RestraintId == restraintID);
        if (setIdx == -1)
        {
            Logger.LogWarning("Attempted to enable a restraint set that does not exist.", LoggerType.Restraints);
            return;
        }

        var setRef = RestraintSets[setIdx];
        Logger.LogInformation("ENABLE SET [" + RestraintSets[setIdx].Name + "] START", LoggerType.Restraints);

        setRef.Enabled = true;
        setRef.EnabledBy = assignerUID;
        _clientConfigs.SaveWardrobe();

        // Raise the priority of, and enable the mods bound to the active set.
        await PenumbraModsToggle(NewState.Enabled, setRef.AssociatedMods);

        // Enable the Hardcore Properties by invoking the ipc call.
        if (setRef.SetProperties.ContainsKey(setRef.EnabledBy) && _clientConfigs.PropertiesEnabledForSet(setIdx, setRef.EnabledBy))
            IpcFastUpdates.InvokeHardcoreTraits(NewState.Enabled, setRef.EnabledBy);

        UnlocksEventManager.AchievementEvent(UnlocksEvent.RestraintApplicationChanged, setRef, true, assignerUID);
        Logger.LogInformation("ENABLE SET [" + setRef.Name + "] END", LoggerType.Restraints);

        if (pushToServer) Mediator.Publish(new PlayerCharWardrobeChanged(DataUpdateKind.WardrobeRestraintApplied));

        // perform a recalculation to appearance data.
        await RecalculateAppearance();
        // refresh the appearance.
        await _appearanceService.RefreshAppearance(GlamourUpdateType.ReapplyAll);
    }

    public async Task DisableRestraintSet(Guid restraintID, string disablerUID = Globals.SelfApplied, bool pushToServer = true)
    {
        Logger.LogTrace("DISABLE-SET Executed");
        var setIdx = RestraintSets.FindIndex(x => x.RestraintId == restraintID);
        if (setIdx == -1) { Logger.LogWarning("Set Does not Exist, Skipping.", LoggerType.Restraints); return; }

        var setRef = RestraintSets[setIdx];
        if (!setRef.Enabled || setRef.Locked) { Logger.LogWarning(setRef.Name + " is already disabled or locked. Skipping!", LoggerType.Restraints); return; }

        Logger.LogInformation("DISABLE SET [" + setRef.Name + "] START", LoggerType.Restraints);

        // Lower the priority of, and if desired, disable, the mods bound to the active set.
        await PenumbraModsToggle(NewState.Disabled, setRef.AssociatedMods);
        // We dont put this inside the recalculation because we need to know what we
        // are removing, since players have non-gagspeak moodles.
        await RemoveMoodles(setRef);

        // Disable the Hardcore Properties by invoking the ipc call.
        if (setRef.SetProperties.ContainsKey(setRef.EnabledBy) && _clientConfigs.PropertiesEnabledForSet(setIdx, setRef.EnabledBy))
            IpcFastUpdates.InvokeHardcoreTraits(NewState.Disabled, setRef.EnabledBy);

        UnlocksEventManager.AchievementEvent(UnlocksEvent.RestraintApplicationChanged, setRef, false, disablerUID);
        setRef.Enabled = false;
        setRef.EnabledBy = string.Empty;
        _clientConfigs.SaveWardrobe();

        Logger.LogInformation("DISABLE SET [" + setRef.Name + "] END", LoggerType.Restraints);

        if (pushToServer) Mediator.Publish(new PlayerCharWardrobeChanged(DataUpdateKind.WardrobeRestraintDisabled));

        // perform a recalculation to appearance data.
        await RecalculateAppearance(fetchMoodlesManually: true);
        // refresh the appearance.
        await _appearanceService.RefreshAppearance(GlamourUpdateType.RefreshAll);

    }

    /// <summary>
    /// When a gag is applied, because its data has already been adjusted, we will simply
    /// need to do a refresh for appearance. The only thing that needs to be adjusted here is C+ Profile.
    /// </summary>
    public async Task GagApplied(GagLayer layer, GagType gagType, bool publishApply = true, bool isSelfApplied = true)
    {
        Logger.LogDebug("GAG-APPLIED triggered on slot ["+layer.ToString()+"] with a ["+gagType.GagName()+"]", LoggerType.GagManagement);
        // We first must change the gag to its new type within the gag manager to update the appearance.
        _gagManager.OnGagTypeChanged(layer, gagType, publishApply);
        
        // If the Gag is not Enabled, or our auto equip is disabled, dont do anything else and return.
        if (!_clientConfigs.IsGagEnabled(gagType) || !_playerData.GlobalPerms!.ItemAutoEquip)
            return;

        // perform a recalculation to appearance data.
        await RecalculateAppearance();
        await _appearanceService.RefreshAppearance(GlamourUpdateType.ReapplyAll);

        // Update C+ Profile if applicable
        var drawData = _clientConfigs.GetDrawData(gagType);
        if (drawData.CustomizeGuid != Guid.Empty)
            _ipcManager.CustomizePlus.EnableProfile(drawData.CustomizeGuid);

        // Send Achievement Event
        UnlocksEventManager.AchievementEvent(UnlocksEvent.GagAction, layer, gagType, isSelfApplied);
    }

    /// <summary>
    /// When a gag is removed, because its data has already been adjusted, we will simply
    /// need to do a refresh for appearance. The only thing that needs to be adjusted here is C+ Profile.
    /// </summary>
    public async Task GagRemoved(GagLayer layer, GagType gagType, bool publishRemoval = true, bool isSelfApplied = true)
    {
        Logger.LogDebug("GAG-REMOVE triggered on slot [" + layer.ToString() + "] with a [" + gagType.GagName() + "]", LoggerType.GagManagement);
        // We first must change the gag to its new type within the gag manager to update the appearance.
        _gagManager.OnGagTypeChanged(layer, GagType.None, publishRemoval);

        // Once it's been set to inactive, we should also remove our moodles.
        var drawData = _clientConfigs.GetDrawData(gagType);
        await RemoveMoodles(drawData);

        // Now Perform a recalculation to appearance data, with manual moodle fetching.
        await RecalculateAppearance(fetchMoodlesManually: true);
        await _appearanceService.RefreshAppearance(GlamourUpdateType.RefreshAll);

        // Remove the CustomizePlus Profile if applicable
        if (drawData.CustomizeGuid != Guid.Empty)
            _ipcManager.CustomizePlus.DisableProfile(drawData.CustomizeGuid);
        
        // Send Achievement Event
        UnlocksEventManager.AchievementEvent(UnlocksEvent.GagRemoval, layer, gagType, isSelfApplied);
    }

    public async Task GagSwapped(GagLayer layer, GagType curGag, GagType newGag, bool isSelfApplied = true)
    {
        Logger.LogTrace("GAG-SWAPPED Executed. Triggering GAG-REMOVE, then GAG-APPLIED");

        // First, remove the current gag.
        await GagRemoved(layer, curGag, publishRemoval: false, isSelfApplied: isSelfApplied);

        // Then, apply the new gag.
        await GagApplied(layer, newGag, isSelfApplied: isSelfApplied);
    }

    /// <summary>
    /// For applying cursed items.
    /// </summary>
    /// <param name="gagLayer"> Ignore this if the cursed item's IsGag is false. </param>
    public async Task CursedItemApplied(CursedItem cursedItem, GagLayer gagLayer = GagLayer.UnderLayer)
    {
        Logger.LogTrace("CURSED-APPLIED Executed");
        // If the cursed item is a gag item, handle it via the gag manager, otherwise, handle through mod toggle
        if(cursedItem.IsGag)
        {
            // Cursed Item was Gag, so handle it via GagApplied.
            await GagApplied(gagLayer, cursedItem.GagType);
        }
        else
        {
            // Cursed Item was Equip, so handle attached Mod Enable and recalculation here.
            await PenumbraModsToggle(NewState.Enabled, new List<AssociatedMod>() { cursedItem.AssociatedMod });
            await RecalculateAppearance();
            await _appearanceService.RefreshAppearance(GlamourUpdateType.ReapplyAll);
        }
    }

    public async Task CursedItemRemoved(CursedItem cursedItem)
    {
        Logger.LogTrace("CURSED-REMOVED Executed");
        // If the Cursed Item is a GagItem, it will be handled automatically by lock expiration. 
        // However, it also means none of the below will process, so we should return if it is.
        if(cursedItem.IsGag)
            return;

        // We are removing a Equip-based CursedItem
        await PenumbraModsToggle(NewState.Disabled, new List<AssociatedMod>() { cursedItem.AssociatedMod });

        // The attached Moodle will need to be removed as well. (need to handle seperately since it stores moodles differently)
        if (!_playerData.IpcDataNull && cursedItem.MoodleIdentifier != Guid.Empty)
        {
            if (cursedItem.MoodleType is IpcToggleType.MoodlesStatus)
                await _ipcManager.Moodles.RemoveOwnStatusByGuid(new List<Guid>() { cursedItem.MoodleIdentifier });
            else if (cursedItem.MoodleType is IpcToggleType.MoodlesPreset)
            {
                var statuses = _playerData.LastIpcData!.MoodlesPresets
                    .FirstOrDefault(p => p.Item1 == cursedItem.MoodleIdentifier).Item2;
                await _ipcManager.Moodles.RemoveOwnStatusByGuid(statuses);
            }
        }

        // perform a recalculation to appearance data.
        await RecalculateAppearance(fetchMoodlesManually: true);
        await _appearanceService.RefreshAppearance(GlamourUpdateType.RefreshAll);
    }

    private async Task RemoveMoodles(IMoodlesAssociable data)
    {
        Logger.LogTrace("Removing Moodles");
        if(_playerData.IpcDataNull) return;

        // if our preset is not null, store the list of guids respective of them.
        var statuses = new List<Guid>();
        if (data.AssociatedMoodlePreset != Guid.Empty)
        {
            statuses = _playerData.LastIpcData!.MoodlesPresets
                .FirstOrDefault(p => p.Item1 == data.AssociatedMoodlePreset).Item2;
        }
        // concat this list with the associated moodles.
        statuses.AddRange(data.AssociatedMoodles);

        // log the moodles we are removing.
        Logger.LogTrace("Removing Moodles: " + string.Join(", ", statuses), LoggerType.ClientPlayerData);

        // remove the moodles.
        await _ipcManager.Moodles.RemoveOwnStatusByGuid(statuses);
    }

    public async Task DisableAllDueToSafeword()
    {
        // if an active set exists we need to unlock and disable it.
        if (_clientConfigs.GetActiveSet() is not null)
        {
            var activeIdx = _clientConfigs.GetActiveSetIdx();
            var set = RestraintSets[activeIdx];
            Logger.LogInformation("Unlocking and Disabling Active Set [" + set.Name + "] due to Safeword.", LoggerType.Restraints);

            // unlock the set, dont push changes yet.
            _clientConfigs.UnlockRestraintSet(activeIdx, Globals.SelfApplied, false);

            // Disable the set, turning off any mods moodles ext and refreshing appearance.            
            await DisableRestraintSet(set.RestraintId, Globals.SelfApplied);
        }

        // Disable all Cursed Items.
        var activeCursedItems = CursedItems.Where(x => x.AppliedTime != DateTimeOffset.MinValue).ToList();
        Logger.LogInformation("Disabling all active Cursed Items due to Safeword.", LoggerType.Safeword);
        foreach (var cursedItem in activeCursedItems)
        {
            _clientConfigs.DeactivateCursedItem(cursedItem.LootId);
            await CursedItemRemoved(cursedItem);
        }
    }

    /// <summary> Applies associated mods to the client when a Restraint or Cursed Item is toggled. </summary>
    private Task PenumbraModsToggle(NewState state, List<AssociatedMod> associatedMods)
    {
        try
        {
            // if we are trying to enabling the Restraint/Cursed Item, then we should just enable.
            if (state is NewState.Enabled)
            {
                foreach (var associatedMod in associatedMods)
                    _ipcManager.Penumbra.ModifyModState(associatedMod);

                // if any of those mods wanted us to perform a redraw, then do so now. (PlayerObjectIndex is always 0)
                if (associatedMods.Any(x => x.RedrawAfterToggle))
                    _ipcManager.Penumbra.RedrawObject(0, RedrawType.Redraw);
            }

            // If we are trying to disable the Restraint/Cursed Item, we should disable only if we ask it to.
            if (state is NewState.Disabled)
            {
                // For each of the associated mods, if we marked it to disable when inactive, disable it.
                foreach (var associatedMod in associatedMods)
                    _ipcManager.Penumbra.ModifyModState(associatedMod,
                        modState: NewState.Disabled,
                        adjustPriorityOnly: associatedMod.DisableWhenInactive ? false : true);

                // if any of those mods wanted us to perform a redraw, then do so now. (PlayerObjectIndex is always 0)
                if (associatedMods.Any(x => x.RedrawAfterToggle))
                    _ipcManager.Penumbra.RedrawObject(0, RedrawType.Redraw);
            }
        }
        catch (Exception e)
        {
            Logger.LogError("Error while toggling mods: " + e.Message, LoggerType.Restraints);
            return Task.CompletedTask;
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Syncronizes the data to be updated with most recent information.
    /// </summary>
    /// <param name="fetchMoodlesManually"> If true, will fetch moodles manually via IPC call for true latest data. </param>
    public async Task RecalculateAppearance(bool fetchMoodlesManually = false)
    {
        // Return if the core data is null.
        if (_playerData.CoreDataNull)
            return;

        Logger.LogDebug("Recalculating Appearance Data.", LoggerType.ClientPlayerData);
        // Temp Storage for Data Collection during reapply
        Dictionary<EquipSlot, IGlamourItem> ItemsToApply = new Dictionary<EquipSlot, IGlamourItem>();
        IpcCallerGlamourer.MetaData MetaToApply = IpcCallerGlamourer.MetaData.None;
        List<Guid> ExpectedMoodles = new List<Guid>();

        // store the data to apply from the active set.
        if (_playerData.GlobalPerms!.WardrobeEnabled && _playerData.GlobalPerms.RestraintSetAutoEquip)
        {
            // we need to store a reference to the active sets draw data.
            var activeSetRef = _clientConfigs.GetActiveSet();
            if (activeSetRef is not null)
            {
                foreach (var item in activeSetRef.DrawData)
                {
                    if (!item.Value.IsEnabled && item.Value.GameItem.Equals(ItemIdVars.NothingItem(item.Value.Slot)))
                        continue;

                    Logger.LogTrace("Adding item to apply: " + item.Key, LoggerType.ClientPlayerData);
                    ItemsToApply[item.Key] = item.Value;
                }
                // Add the moodles from the active set.
                if (!_playerData.IpcDataNull)
                {
                    if(activeSetRef.AssociatedMoodles.Count > 0)
                        ExpectedMoodles.AddRange(activeSetRef.AssociatedMoodles);
                    if (activeSetRef.AssociatedMoodlePreset != Guid.Empty)
                    {
                        var statuses = _playerData.LastIpcData!.MoodlesPresets.FirstOrDefault(p => p.Item1 == activeSetRef.AssociatedMoodlePreset).Item2;
                        if (statuses is not null)
                            ExpectedMoodles.AddRange(statuses);
                    }
                }

                // Apply meta changes if any were on.

            }
        }

        // Collect gag info if used.
        if (_playerData.GlobalPerms.ItemAutoEquip)
        {
            // grab the active gags, should grab in order (underlayer, middle, uppermost)
            var gagSlots = _playerData.AppearanceData!.GagSlots.Where(slot => slot.GagType.ToGagType() != GagType.None).ToList();
            var gagActiveTypes = gagSlots.Select(slot => slot.GagType.ToGagType()).ToList();

            // update the stored data.
            foreach (var slot in gagSlots)
            {
                var data = _clientConfigs.GetDrawData(slot.GagType.ToGagType());
                if (data is not null)
                {
                    ItemsToApply[data.Slot] = data;

                    // continue if moodles data is not present.
                    if (!_playerData.IpcDataNull)
                    {
                        if(data.AssociatedMoodles.Count > 0)
                            ExpectedMoodles.AddRange(data.AssociatedMoodles);
                        if (data.AssociatedMoodlePreset != Guid.Empty)
                        {
                            var statuses = _playerData.LastIpcData!.MoodlesPresets.FirstOrDefault(p => p.Item1 == data.AssociatedMoodlePreset).Item2;
                            if (statuses is not null)
                                ExpectedMoodles.AddRange(statuses);
                        }
                    }

                    // Apply the metadata stored in this gag item. Any gags after it will overwrite previous meta set.
                    MetaToApply = (data.ForceHeadgearOnEnable && data.ForceVisorOnEnable) 
                        ? IpcCallerGlamourer.MetaData.Both : (data.ForceHeadgearOnEnable) 
                            ? IpcCallerGlamourer.MetaData.Hat : (data.ForceVisorOnEnable) 
                                ? IpcCallerGlamourer.MetaData.Visor : IpcCallerGlamourer.MetaData.None;
                }
            }
        }

        // Collect the data from the blindfold.
        if (_pairManager.DirectPairs.Any(x => x.UserPairOwnUniquePairPerms.IsBlindfolded))
        {
            var blindfoldData = _clientConfigs.GetBlindfoldItem();
            ItemsToApply[blindfoldData.Slot] = blindfoldData;
        }

        // collect the data from the cursed sets.
        if (_clientConfigs.GagspeakConfig.CursedDungeonLoot)
        {
            // track the items that will be applied.
            var cursedItems = _clientConfigs.CursedLootConfig.CursedLootStorage.CursedItems
                .Where(x => x.AppliedTime != DateTimeOffset.MinValue)
                .Take(6)
                .OrderBy(x => x.AppliedTime)
                .ToList();
            Logger.LogDebug("Found " + cursedItems.Count + " Cursed Items to Apply.", LoggerType.ClientPlayerData);
            var appliedItems = new Dictionary<EquipSlot, CursedItem>();

            foreach (var cursedItem in cursedItems)
            {
                if (appliedItems.TryGetValue(cursedItem.AppliedItem.Slot, out var existingItem))
                {
                    // if an item was already applied to that slot, only apply if it satisfied conditions.
                    if (existingItem.CanOverride && cursedItem.OverridePrecedence >= existingItem.OverridePrecedence)
                    {
                        Logger.LogDebug($"Slot: "+cursedItem.AppliedItem.Slot+" already had an item ["+existingItem.Name+"]. "
                            + "but ["+cursedItem.Name+"] had higher precedence", LoggerType.ClientPlayerData);
                        appliedItems[cursedItem.AppliedItem.Slot] = cursedItem;
                    }
                }
                else
                {
                    Logger.LogDebug($"Storing Cursed Item ["+cursedItem.Name+"] to Slot: "+cursedItem.AppliedItem.Slot, LoggerType.ClientPlayerData);
                    appliedItems[cursedItem.AppliedItem.Slot] = cursedItem;
                }

                // add in the moodle if it exists.
                if (!_playerData.IpcDataNull)
                {
                    if (cursedItem.MoodleType is IpcToggleType.MoodlesStatus && cursedItem.MoodleIdentifier != Guid.Empty)
                        ExpectedMoodles.Add(cursedItem.MoodleIdentifier);
                    else if (cursedItem.MoodleType is IpcToggleType.MoodlesPreset && cursedItem.MoodleIdentifier != Guid.Empty)
                        ExpectedMoodles.AddRange(
                            _playerData.LastIpcData!.MoodlesPresets
                            .Where(p => p.Item1 == cursedItem.MoodleIdentifier)
                            .SelectMany(p => p.Item2));
                }
            }

            // take the dictionary of applied items and replace any existing items in the ItemsToApply dictionary.
            foreach (var item in appliedItems)
            {
                Logger.LogInformation($"Applying Cursed Item to Slot: {item.Key}", LoggerType.ClientPlayerData);
                if(item.Value.IsGag)
                {
                    var drawData = _clientConfigs.GetDrawData(item.Value.GagType);
                    ItemsToApply[drawData.Slot] = drawData;
                }
                else
                {
                    ItemsToApply[item.Key] = item.Value.AppliedItem;
                }
            }
        }

        // if we are fetching moodles manually, we should do so now.
        await UpdateLatestMoodleData();

        // Update the stored data.
        _appearanceService.ItemsToApply = ItemsToApply;
        _appearanceService.MetaToApply = MetaToApply;
        _appearanceService.ExpectedMoodles = ExpectedMoodles;

        Logger.LogDebug("Appearance Data Recalculated.", LoggerType.ClientPlayerData);
        return;
    }
}
