using FFXIVClientStructs.FFXIV.Client.Game.Character;
using GagSpeak.GagspeakConfiguration.Models;
using GagSpeak.Interop.Ipc;
using GagSpeak.Interop.IpcHelpers.Moodles;
using GagSpeak.PlayerData.Data;
using GagSpeak.PlayerData.Factories;
using GagSpeak.PlayerData.Handlers;
using GagSpeak.PlayerData.Pairs;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using GagSpeak.UpdateMonitoring;
using GagSpeak.Utils;
using GagspeakAPI.Data.Character;
using GagspeakAPI.Data.Enum;
using Glamourer.Api.Enums;
using Interop.Ipc;
using System.Reflection;

namespace GagSpeak.PlayerData.Services;

// this is a sealed scoped class meaning the cache service would be unique for every player assigned to it.
public class GlamourChangedService : DisposableMediatorSubscriberBase
{
    private readonly IpcManager _Interop; // can upgrade this to IpcManager if needed later.
    private readonly MoodlesAssociations _moodlesAssociations;
    private readonly ClientConfigurationManager _clientConfigs;
    private readonly PlayerCharacterManager _playerManager;
    private readonly PairManager _pairManager;
    private readonly OnFrameworkService _frameworkUtils;
    private readonly GlamourFastUpdate _glamourFastUpdate;

    public GlamourChangedService(ILogger<GlamourChangedService> logger,
        GagspeakMediator mediator, PlayerCharacterManager playerManager,
        PairManager pairManager, ClientConfigurationManager clientConfigs, 
        OnFrameworkService frameworkUtils, IpcManager interop, 
        MoodlesAssociations moodlesAssociations,
        GlamourFastUpdate fastUpdate) : base(logger, mediator)
    {
        _clientConfigs = clientConfigs;
        _playerManager = playerManager;
        _pairManager = pairManager;
        _frameworkUtils = frameworkUtils;
        _Interop = interop;
        _moodlesAssociations = moodlesAssociations;

        _glamourFastUpdate = fastUpdate;
        _cts = new CancellationTokenSource(); // for handling gearset changes

        // subscribe to our mediator for glamourchanged
        _glamourFastUpdate.GlamourEventFired += UpdateGenericAppearance;
        // various glamour update types.
        Mediator.Subscribe<UpdateGlamourMessage>(this, (msg) => { });// UpdateGenericAppearance(msg));
        // gag glamour updates
        Mediator.Subscribe<UpdateGlamourGagsMessage>(this, (msg) => UpdateGagsAppearance(msg));
        // restraint set glamour updates
        Mediator.Subscribe<UpdateGlamourRestraintsMessage>(this, (msg) => UpdateRestraintSetAppearance(msg));
        // blindfold glamour updates
        Mediator.Subscribe<UpdateGlamourBlindfoldMessage>(this, (msg) => UpdateGlamourerBlindfoldAppearance(msg));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        // unsub
        _glamourFastUpdate.GlamourEventFired -= UpdateGenericAppearance;
    }


    // public async void UpdateGenericAppearance(UpdateGlamourMessage msg)
    private CancellationTokenSource _cts;
    private static SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);

    private async Task ExecuteWithSemaphore(Func<Task> action)
    {
        _cts.Cancel();
        OnFrameworkService.GlamourChangeEventsDisabled = true;
        await semaphore.WaitAsync();
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error during semaphore execution: {ex}");
        }
        finally
        {
            semaphore.Release();
            OnFrameworkService.GlamourChangeFinishedDrawing = true;
        }
    }


    public async void UpdateGenericAppearance(object sender, GlamourFastUpdateArgs msg)
    {
        await ExecuteWithSemaphore(async () =>
        {
            if (_playerManager.GlobalPerms == null || !_playerManager.GlobalPerms.WardrobeEnabled)
            {
                Logger.LogDebug("Wardrobe is disabled, so not processing Generic Update");
                return;
            }

            // For Generic UpdateAllGags
            if (msg.GenericUpdateType == GlamourUpdateType.UpdateAllGags)
            {
                Logger.LogDebug($"Updating Update Gags");
                await ApplyGagItemsToCachedCharacterData();
            }

            // For Generic JobChange call
            if (msg.GenericUpdateType == GlamourUpdateType.JobChange)
            {
                Logger.LogDebug($"Updating due to Job Change");
                await Task.Run(() => _frameworkUtils.RunOnFrameworkThread(UpdateCachedCharacterData));
            }

            // condition 7 --> it was a refresh all event, we should reapply all the gags and restraint sets
            if (msg.GenericUpdateType == GlamourUpdateType.RefreshAll ||
                msg.GenericUpdateType == GlamourUpdateType.ZoneChange ||
                msg.GenericUpdateType == GlamourUpdateType.Login)
            {
                Logger.LogDebug($"Processing Refresh All // Zone Change // Login // Job Change");
                // apply all the gags and restraint sets
                await Task.Run(() => _frameworkUtils.RunOnFrameworkThread(UpdateCachedCharacterData));
            }

            // Triggered by using safeword, will undo everything.
            if (msg.GenericUpdateType == GlamourUpdateType.Safeword)
            {
                Logger.LogDebug($"Processing Safeword Update");
                await _Interop.Glamourer.GlamourerRevertCharacter(_frameworkUtils._playerAddr);
            }
        });
    }


    public async void UpdateGagsAppearance(UpdateGlamourGagsMessage msg)
    {
        // reference the completion source.
        await ExecuteWithSemaphore(async () =>
        {
            // do not accept if we have enable wardrobe turned off.
            if (_playerManager.GlobalPerms == null || !_playerManager.GlobalPerms.WardrobeEnabled)
            {
                Logger.LogDebug("Wardrobe is disabled, so not processing Gag Update");
                OnFrameworkService.GlamourChangeFinishedDrawing = true;
                semaphore.Release();
                return;
            }

            // For GagEquip
            if (msg.NewState == NewState.Enabled && _playerManager.GlobalPerms.ItemAutoEquip)
            {
                Logger.LogDebug($"Processing Gag Equipped");
                // see if the gag we are equipping is not none
                if (msg.GagType.GetGagAlias() != "None")
                {
                    // get the draw data for the gag
                    var drawData = _clientConfigs.GetDrawData(msg.GagType);
                    // equip the gag
                    await EquipWithSetItem(msg.Layer, msg.GagType.GetGagAlias(), msg.AssignerName);

                    if (drawData.ForceHeadgearOnEnable) await _Interop.Glamourer.ForceSetMetaData(IpcCallerGlamourer.MetaData.Hat, true);

                    if (drawData.ForceVisorOnEnable) await _Interop.Glamourer.ForceSetMetaData(IpcCallerGlamourer.MetaData.Visor, true);
                }
                // otherwise, do the same as the unequip function
                else
                {
                    Logger.LogDebug($"GagType is None, but you try setting a gag. What are you doing?");
                }
            }

            // For Gag Unequip
            if (msg.NewState == NewState.Disabled && _playerManager.GlobalPerms.ItemAutoEquip)
            {
                Logger.LogDebug($"Processing Gag UnEquip");
                var gagType = Enum.GetValues(typeof(GagList.GagType)).Cast<GagList.GagType>().First(g => g.GetGagAlias() == msg.GagType.GetGagAlias());
                // this should replace it with nothing
                await _Interop.Glamourer.SetItemToCharacterAsync((ApiEquipSlot)_clientConfigs.GetGagTypeEquipSlot(gagType),
                    ItemIdVars.NothingItem(_clientConfigs.GetGagTypeEquipSlot(gagType)).Id.Id, [0, 0], 0);
                // reapply any restraints hiding under them, if any
                await ApplyRestrainSetToCachedCharacterData();
                // update blindfold (TODO)
                if (false)
                {
                    await EquipBlindfold();
                }
            }

            // let the completion source know we are done.
            if (msg.GagToggleTask != null)
            {
                msg.GagToggleTask.SetResult(true);
            }
        });
    }


    public async void UpdateRestraintSetAppearance(UpdateGlamourRestraintsMessage msg)
    {
        await ExecuteWithSemaphore(async () =>
        {
            // do not accept if we have enable wardrobe turned off.
            if (_playerManager.GlobalPerms == null || !_playerManager.GlobalPerms.WardrobeEnabled)
            {
                Logger.LogDebug("Wardrobe is disabled, so not processing Restraint Set Update");
                OnFrameworkService.GlamourChangeFinishedDrawing = true;
                semaphore.Release();
                return;
            }

            if (msg.NewState == NewState.Enabled)
            {
                Logger.LogDebug($"Processing Restraint Set Update");
                // apply restraint set data to character.
                await ApplyRestrainSetToCachedCharacterData();

                // if they allow item auto equip and have any gags equipped, apply them after
                if (_playerManager.GlobalPerms.ItemAutoEquip && _playerManager.IsPlayerGagged())
                {
                    // apply the gag items overtop.
                    await ApplyGagItemsToCachedCharacterData();
                }

                // finally, we need to update our blindfold (TODO:)
                if (false)
                {
                    await EquipBlindfold();
                }
            }
            else if (msg.NewState == NewState.Disabled)
            {
                // now perform a revert based on our customization option
                switch (_clientConfigs.GagspeakConfig.RevertStyle)
                {
                    case RevertStyle.ToGameOnly:
                        await _Interop.Glamourer.GlamourerRevertCharacter(_frameworkUtils._playerAddr);
                        break;
                    case RevertStyle.ToAutomationOnly:
                        await _Interop.Glamourer.GlamourerRevertCharacterToAutomation(_frameworkUtils._playerAddr);
                        break;
                    case RevertStyle.ToGameThenAutomation:
                        await _Interop.Glamourer.GlamourerRevertCharacter(_frameworkUtils._playerAddr);
                        await _Interop.Glamourer.GlamourerRevertCharacterToAutomation(_frameworkUtils._playerAddr);
                        break;
                }
            }
            // now reapply the gags
            if (_playerManager.GlobalPerms.ItemAutoEquip && _playerManager.IsPlayerGagged())
            {
                Logger.LogDebug($"Reapplying gags");
                await ApplyGagItemsToCachedCharacterData();
                Logger.LogDebug($"Reapplying blindfold");
                // TODO: Blindfold logic
                if (false)
                {
                    await EquipBlindfold();
                }
            }

            // let the completion source know we are done.
            if (msg.CompletionTaskSource != null)
            {

                if (msg.CompletionTaskSource != null)
                {
                    Logger.LogInformation("Restraint Set GlamourChangeTask completed.");
                    msg.CompletionTaskSource.SetResult(true);
                }
            }
        });
    }

    // there was a semaphore slim here before, but don't worry about it now.
    public async void UpdateGlamourerBlindfoldAppearance(UpdateGlamourBlindfoldMessage msg)
    {
        await ExecuteWithSemaphore(async () =>
        {

            // do not accept if we have enable wardrobe turned off.
            if (_playerManager.GlobalPerms == null || !_playerManager.GlobalPerms.WardrobeEnabled)
            {
                Logger.LogDebug("Wardrobe is disabled, so not processing Blindfold Update");
                OnFrameworkService.GlamourChangeFinishedDrawing = true;
                return;
            }

            if (msg.NewState == NewState.Enabled)
            {
                Logger.LogDebug($"Processing Blindfold Equipped");
                // get the index of the person who equipped it onto you (FIXLOGIC TODO)
                if (_pairManager.DirectPairs.Any(x => x.UserPairOwnUniquePairPerms.IsBlindfolded))
                { 
                    await EquipBlindfold();
                }
                else
                {
                    Logger.LogDebug($"Assigner is not on your whitelist, so not setting item.");
                }
            }

            if (msg.NewState == NewState.Disabled)
            {
                Logger.LogDebug($"Processing Blindfold UnEquip");
                // get the index of the person who equipped it onto you (FIXLOGIC TODO)
                if (false)
                {
                    await UnequipBlindfold(-1);
                }
                else
                {
                    Logger.LogDebug($"Assigner is not on your whitelist, so not setting item.");
                }
            }
        });
    }

    #region Task Methods for Glamour Updates
    public async Task EquipBlindfold()
    {
        Logger.LogDebug($"Equipping blindfold");
        // attempt to equip the blindfold to the player
        await _Interop.Glamourer.SetItemToCharacterAsync((ApiEquipSlot)_clientConfigs.GetBlindfoldItem().Slot, 
            _clientConfigs.GetBlindfoldItem().GameItem.Id.Id,
            [_clientConfigs.GetBlindfoldItem().GameStain.Stain1.Id, _clientConfigs.GetBlindfoldItem().GameStain.Stain2.Id], 0);
    }

    public async Task UnequipBlindfold(int idxOfBlindfold)
    {
        Logger.LogDebug($"Unequipping blindfold");
        // attempt to unequip the blindfold from the player
        await _Interop.Glamourer.SetItemToCharacterAsync((ApiEquipSlot)_clientConfigs.GetBlindfoldItem().Slot,
            ItemIdVars.NothingItem(_clientConfigs.GetBlindfoldItem().Slot).Id.Id, [0], 0);
    }
    /// <summary> Updates the raw glamourer customization data with our gag items and restraint sets, if applicable </summary>
    public async Task UpdateCachedCharacterData()
    {
        // for privacy reasons, we must first make sure that our options for allowing such things are enabled.
        if (_playerManager.GlobalPerms.RestraintSetAutoEquip)
        {
            await ApplyRestrainSetToCachedCharacterData();
        }

        if (_playerManager.GlobalPerms.ItemAutoEquip)
        {
            await ApplyGagItemsToCachedCharacterData();
        }

        if (_pairManager.DirectPairs.Any(x => x.UserPairOwnUniquePairPerms.IsBlindfolded))
        {
            await EquipBlindfold();
        }
    }

    /// <summary> Applies the only enabled restraint set to your character on update trigger. </summary>
    public async Task ApplyRestrainSetToCachedCharacterData()
    { // dummy placeholder line
        // Find the restraint set with the matching name
        var tasks = new List<Task>();
        foreach (var restraintSet in _clientConfigs.StoredRestraintSets)
        {
            // If the restraint set is enabled
            if (restraintSet.Enabled)
            {
                // Iterate over each EquipDrawData in the restraint set
                foreach (var pair in restraintSet.DrawData)
                {
                    // see if the item is enabled or not (controls it's visibility)
                    if (pair.Value.IsEnabled)
                    {
                        // because it is enabled, we will still apply nothing items
                        tasks.Add(_Interop.Glamourer.SetItemToCharacterAsync(
                            (ApiEquipSlot)pair.Key, // the key (EquipSlot)
                            pair.Value.GameItem.Id.Id, // Set this slot to nothing (naked)
                            [pair.Value.GameStain.Stain1.Id, pair.Value.GameStain.Stain2.Id], // The _drawData._gameStain.Id
                            0));
                    }
                    else
                    {
                        // Because it was disabled, we will treat it as an overlay, ignoring it if it is a nothing item
                        if (!pair.Value.GameItem.Equals(ItemIdVars.NothingItem(pair.Value.Slot)))
                        {
                            // Apply the EquipDrawData
                            Logger.LogTrace($"Calling on Helmet Placement {pair.Key}!");
                            tasks.Add(_Interop.Glamourer.SetItemToCharacterAsync(
                                (ApiEquipSlot)pair.Key, // the key (EquipSlot)
                                pair.Value.GameItem.Id.Id, // The _drawData._gameItem.Id.Id
                                [pair.Value.GameStain.Stain1.Id, pair.Value.GameStain.Stain2.Id],
                                0));
                        }
                        else
                        {
                            Logger.LogTrace($"Skipping over {pair.Key}!");
                        }
                    }
                }
                // early exit, we only want to apply one restraint set
                Logger.LogDebug($"Applying Restraint Set to Cached Character Data");
                await Task.WhenAll(tasks);
                return;
            }
        }
        Logger.LogDebug($"No restraint sets are enabled, skipping!");
    }

    /// <summary> Applies the gag items to the cached character data. </summary>
    public async Task ApplyGagItemsToCachedCharacterData()
    {
        if(_playerManager.AppearanceData == null)
        {
            Logger.LogError($"Appearance Data is null, so not applying Gag Items");
            return;
        }
        Logger.LogDebug($"Applying Gag Items to Cached Character Data");
        // temporary code until glamourer update's its IPC changes
        await EquipWithSetItem(GagLayer.UnderLayer, _playerManager.AppearanceData.GagSlots[0].GagType, "SelfApplied");
        await EquipWithSetItem(GagLayer.MiddleLayer, _playerManager.AppearanceData.GagSlots[1].GagType, "SelfApplied");
        await EquipWithSetItem(GagLayer.TopLayer, _playerManager.AppearanceData.GagSlots[2].GagType, "SelfApplied");
    }

    /// <summary> Equips a gag by the defined gag name </summary>
    /// <param name="gagName"> The name of the gag to equip. </param>
    /// <param name="assignerName"> The assigner issuing the equip. defaults to SelfApplied if no assigner is given. </param>
    public async Task EquipWithSetItem(GagLayer gagLayer, string gagName, string assignerName = "SelfApplied")
    {
        // if the gagName is none, then just don't set it and return
        if (gagName == "None") return;

        // Get the ENUM based GagType equivalent.
        var gagType = Enum.GetValues(typeof(GagList.GagType)).Cast<GagList.GagType>().First(g => g.GetGagAlias() == gagName);

        // verify we are allowed to equip the item's glamour in the first place.
        if (!_clientConfigs.IsGagEnabled(gagType)) // Keep this property, but maybe separate the others.
        {
            Logger.LogDebug($"GagType {gagName} is not enabled, so not setting item.");
            return;
        }

        // we no longer need to worry about setting the enabled by and locked by because we will depend on mediator communication with player manager.
        // Likewise, we will ensure that to even call this event in the first place that they must be a valid pair with permissions. This should be done before
        // this is even called.

        try
        {
            await _Interop.Glamourer.SetItemToCharacterAsync((ApiEquipSlot)_clientConfigs.GetGagTypeEquipSlot(gagType),
                _clientConfigs.GetGagTypeEquipItem(gagType).Id.Id, _clientConfigs.GetGagTypeStainIds(gagType), 0);

            Logger.LogDebug($"Set item {_clientConfigs.GetGagTypeEquipItem(gagType)} to slot {_clientConfigs.GetGagTypeEquipSlot(gagType)} for gag {gagName}");
        }
        catch (TargetInvocationException ex)
        {
            Logger.LogError($"[Interop - SetItem]: Error setting item: {ex.InnerException}");
        }

    }
    #endregion Task Methods for Glamour Updates
}
