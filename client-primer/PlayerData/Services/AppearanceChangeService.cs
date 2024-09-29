using GagSpeak.GagspeakConfiguration.Models;
using GagSpeak.Interop;
using GagSpeak.Interop.Ipc;
using GagSpeak.Interop.IpcHelpers.Moodles;
using GagSpeak.PlayerData.Data;
using GagSpeak.PlayerData.Pairs;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using GagSpeak.UpdateMonitoring;
using GagSpeak.Utils;
using GagspeakAPI.Extensions;
using Glamourer.Api.Enums;
using Penumbra.GameData.Enums;

namespace GagSpeak.PlayerData.Services;

// this is a sealed scoped class meaning the cache service would be unique for every player assigned to it.
public class AppearanceChangeService : DisposableMediatorSubscriberBase
{
    private readonly IpcManager _Interop; // can upgrade this to IpcManager if needed later.
    private readonly MoodlesAssociations _moodlesAssociations;
    private readonly ClientConfigurationManager _clientConfigs;
    private readonly PlayerCharacterData _playerManager;
    private readonly PairManager _pairManager;
    private readonly OnFrameworkService _frameworkUtils;
    private readonly IpcFastUpdates _ipcFastUpdates;

    public AppearanceChangeService(ILogger<AppearanceChangeService> logger,
        GagspeakMediator mediator, PlayerCharacterData playerManager,
        PairManager pairManager, ClientConfigurationManager clientConfigs,
        OnFrameworkService frameworkUtils, IpcManager interop,
        MoodlesAssociations moodlesAssociations,
        IpcFastUpdates fastUpdate) : base(logger, mediator)
    {
        _clientConfigs = clientConfigs;
        _playerManager = playerManager;
        _pairManager = pairManager;
        _frameworkUtils = frameworkUtils;
        _Interop = interop;
        _moodlesAssociations = moodlesAssociations;

        _ipcFastUpdates = fastUpdate;
        _cts = new CancellationTokenSource(); // for handling gearset changes

        // subscribe to our mediator for glamour changed
        IpcFastUpdates.GlamourEventFired += UpdateGenericAppearance;
        IpcFastUpdates.CustomizeEventFired += EnsureForcedCustomizeProfile;

        // gag glamour updates
        Mediator.Subscribe<UpdateGlamourGagsMessage>(this, async (msg) =>
        {
            await UpdateGagsAppearance(msg.Layer, msg.GagType, msg.NewState);
        });

        // restraint set glamour updates
        Mediator.Subscribe<UpdateGlamourRestraintsMessage>(this, (msg) => UpdateRestraintSetAppearance(msg));

        // blindfold glamour updates
        Mediator.Subscribe<UpdateGlamourBlindfoldMessage>(this, (msg) => UpdateGlamourerBlindfoldAppearance(msg));

        Mediator.Subscribe<ZoneSwitchEndMessage>(this, (_) => UpdateGenericAppearance(GlamourUpdateType.ZoneChange));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        IpcFastUpdates.GlamourEventFired -= UpdateGenericAppearance;
        IpcFastUpdates.CustomizeEventFired -= EnsureForcedCustomizeProfile;
    }

    private void EnsureForcedCustomizeProfile(Guid e)
    {
        // return if appearance data is not valid.
        if (_playerManager.AppearanceData == null || !IpcCallerCustomize.APIAvailable) return;

        // Fetch stored gag types equipped on the player, in the order of the layer.
        var gagTypes = _playerManager.AppearanceData.GagSlots
            .Select(slot => slot.GagType.ToGagType())
            .Where(gagType => gagType != GagType.None)
            .ToList();

        if (!_playerManager.IsPlayerGagged) return;

        // Fetch the drawData of gag with the highest Priority
        var highestPriorityData = _clientConfigs.GetDrawDataWithHighestPriority(gagTypes);
        if (highestPriorityData.CustomizeGuid == Guid.Empty) return; // return if the highest priority gag requires no customizeGuid.

        // Grab the active profile.
        var activeGuid = _Interop.CustomizePlus.GetActiveProfile();
        if (activeGuid == highestPriorityData.CustomizeGuid || activeGuid is null) return;

        // if it is not, we need to enforce the update.
        // Start by checking if the highestPriorityCustomizeId is in our stored profiles.
        if (!_playerManager.CustomizeProfiles.Any(x => x.ProfileGuid == highestPriorityData.CustomizeGuid))
        {
            _playerManager.CustomizeProfiles = _Interop.CustomizePlus.GetProfileList();
            // try and check again. if it fails. we should clear the customizeGuid from the draw data and save it.
            if (!_playerManager.CustomizeProfiles.Any(x => x.ProfileGuid == highestPriorityData.CustomizeGuid))
            {
                highestPriorityData.CustomizeGuid = Guid.Empty;
                _clientConfigs.SaveGagStorage();
                return;
            }
        }

        Logger.LogTrace("Enforcing Customize+ Profile " + highestPriorityData.CustomizeGuid + " for your equipped Gag", LoggerType.IpcCustomize);
        _Interop.CustomizePlus.EnableProfile(highestPriorityData.CustomizeGuid);
    }


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
            // Schedule the re-enabling of glamour change events using RunOnFrameworkTickDelayed
            await _frameworkUtils.RunOnFrameworkTickDelayed(() =>
            {
                Logger.LogDebug("Re-Allowing Glamour Change Event", LoggerType.IpcGlamourer);
                OnFrameworkService.GlamourChangeEventsDisabled = false;
            }, 1);
        }
    }


    public async void UpdateGenericAppearance(GlamourUpdateType updateType)
    {
        await ExecuteWithSemaphore(async () =>
        {
            // Triggered by using safeword, will undo everything.
            if (updateType == GlamourUpdateType.Safeword)
            {
                Logger.LogDebug($"Processing Safeword Update", LoggerType.Safeword);
                switch (_clientConfigs.GagspeakConfig.RevertStyle)
                {
                    case RevertStyle.RevertToGame:
                        await _Interop.Glamourer.GlamourerRevertToGame();
                        break;

                    case RevertStyle.RevertEquipToGame:
                        await _Interop.Glamourer.GlamourerRevertToGameEquipOnly();
                        break;

                    case RevertStyle.RevertToAutomation:
                        await _Interop.Glamourer.GlamourerRevertToAutomation();
                        break;

                    case RevertStyle.RevertEquipToAutomation:
                        await _Interop.Glamourer.GlamourerRevertToAutomationEquipOnly();
                        break;
                }
            }

            if (_playerManager.CoreDataNull) return;

            if (!_playerManager.GlobalPerms!.WardrobeEnabled)
            {
                Logger.LogDebug("Wardrobe is disabled, so not processing Generic Update");
                return;
            }

            // For Generic UpdateAllGags
            if (updateType == GlamourUpdateType.UpdateAllGags)
            {
                Logger.LogDebug($"Updating Update Gags", LoggerType.GagManagement);
                await ApplyGagItemsToCachedCharacterData();
            }

            // For Generic JobChange call
            if (updateType is GlamourUpdateType.JobChange or GlamourUpdateType.RefreshAll or GlamourUpdateType.ZoneChange or GlamourUpdateType.Login)
            {
                Logger.LogDebug("Processing Full Refresh due to UpdateType: [" + updateType.ToString() + "]");
                await Task.Run(() => _frameworkUtils.RunOnFrameworkThread(UpdateCachedCharacterData));
            }
        });
    }


    public async Task UpdateGagsAppearance(GagLayer layer, GagType gagType, NewState updatedState)
    {
        // reference the completion source.
        await ExecuteWithSemaphore(async () =>
        {
            if (_playerManager.CoreDataNull) return;

            // do not accept if we have enable wardrobe turned off.
            if (!_playerManager.GlobalPerms!.ItemAutoEquip)
            {
                Logger.LogDebug("Gag AutoEquip is disabled, so not processing Gag Update", LoggerType.GagManagement);
                return;
            }

            if (updatedState is NewState.Enabled)
            {
                Logger.LogDebug($"Processing Gag Equipped", LoggerType.GagManagement);
                await UpdateGagItem(gagType, NewState.Enabled);

                // reapply any restraints hiding under them, if any
                await ApplyRestrainSetToCachedCharacterData();
                // update blindfold (TODO)
                if (_pairManager.DirectPairs.Any(x => x.UserPairOwnUniquePairPerms.IsBlindfolded))
                {
                    await EquipBlindfold();
                }
            }

            if (updatedState is NewState.Disabled)
            {
                Logger.LogDebug($"Processing Gag UnEquip", LoggerType.GagManagement);
                await UpdateGagItem(gagType, NewState.Disabled);

                // reapply any restraints hiding under them, if any
                await ApplyRestrainSetToCachedCharacterData();
                // update blindfold (TODO)
                if (_pairManager.DirectPairs.Any(x => x.UserPairOwnUniquePairPerms.IsBlindfolded))
                {
                    await UnequipBlindfold();
                }
            }
        });
    }


    public async void UpdateRestraintSetAppearance(UpdateGlamourRestraintsMessage msg)
    {
        await ExecuteWithSemaphore(async () =>
        {
            // do not accept if we have enable wardrobe turned off.
            if (_playerManager.GlobalPerms == null || !_playerManager.GlobalPerms.WardrobeEnabled || !_playerManager.GlobalPerms.RestraintSetAutoEquip)
            {
                Logger.LogDebug("Wardrobe is disabled, or Restraint AutoEquip is not allowed, so skipping", LoggerType.Restraints);
                return;
            }

            if (msg.NewState == NewState.Enabled)
            {
                Logger.LogDebug($"Processing Restraint Set Update", LoggerType.Restraints);
                // apply restraint set data to character.
                await ApplyRestrainSetToCachedCharacterData();

                // if they allow item auto equip and have any gags equipped, apply them after
                if (_playerManager.GlobalPerms.ItemAutoEquip && _playerManager.IsPlayerGagged)
                {
                    // apply the gag items overtop.
                    await ApplyGagItemsToCachedCharacterData();
                }

                // finally, we need to update our blindfold (TODO:)
                if (_pairManager.DirectPairs.Any(x => x.UserPairOwnUniquePairPerms.IsBlindfolded))
                {
                    await EquipBlindfold();
                }
            }
            else if (msg.NewState == NewState.Disabled)
            {
                // now perform a revert based on our customization option
                switch (_clientConfigs.GagspeakConfig.RevertStyle)
                {
                    case RevertStyle.RevertToGame:
                        await _Interop.Glamourer.GlamourerRevertToGame();
                        break;

                    case RevertStyle.RevertEquipToGame:
                        await _Interop.Glamourer.GlamourerRevertToGameEquipOnly();
                        break;

                    case RevertStyle.RevertToAutomation:
                        await _Interop.Glamourer.GlamourerRevertToAutomation();
                        break;

                    case RevertStyle.RevertEquipToAutomation:
                        await _Interop.Glamourer.GlamourerRevertToAutomationEquipOnly();
                        break;
                }
            }
            // now reapply the gags
            if (_playerManager.GlobalPerms.ItemAutoEquip && _playerManager.IsPlayerGagged)
            {
                Logger.LogDebug($"Reapplying gags", LoggerType.GagManagement);
                await ApplyGagItemsToCachedCharacterData();
                Logger.LogDebug($"Reapplying blindfold", LoggerType.HardcoreActions);
                // TODO: Blindfold logic
                if (_pairManager.DirectPairs.Any(x => x.UserPairOwnUniquePairPerms.IsBlindfolded))
                {
                    await EquipBlindfold();
                }
            }

            // let the completion source know we are done.
            if (msg.CompletionTaskSource != null)
            {

                if (msg.CompletionTaskSource != null)
                {
                    Logger.LogInformation("Restraint Set GlamourChangeTask completed.", LoggerType.Restraints);
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
            if (_playerManager.CoreDataNull) return;

            // do not accept if we have enable wardrobe turned off.
            if (!_playerManager.GlobalPerms!.WardrobeEnabled)
            {
                Logger.LogDebug("Wardrobe is disabled, so not processing Blindfold Update");
                return;
            }

            var matchedPair = _pairManager.DirectPairs.FirstOrDefault(x => x.UserData.UID == msg.AssignerName);
            if (matchedPair == null)
            {
                Logger.LogDebug("Assigner is not on your whitelist, so not setting item.", LoggerType.HardcoreActions);
                return;
            }

            if (msg.NewState == NewState.Enabled)
            {
                Logger.LogDebug($"Processing Blindfold Equipped", LoggerType.HardcoreActions);
                // verify that they have permission to do so.
                if (matchedPair.UserPairOwnUniquePairPerms.AllowBlindfold)
                {
                    Logger.LogInformation("You are blindfolded by :" + matchedPair.UserData.AliasOrUID, LoggerType.HardcoreActions);
                    await EquipBlindfold();
                }
                else
                {
                    Logger.LogDebug("You haven't given this assigner permission to apply blindfolds to you!", LoggerType.HardcoreActions);
                }
            }

            if (msg.NewState == NewState.Disabled)
            {
                Logger.LogDebug($"Processing Blindfold UnEquip", LoggerType.HardcoreActions);
                // verify that they have permission to do so.
                if (matchedPair.UserPairOwnUniquePairPerms.AllowBlindfold)
                {
                    Logger.LogInformation("You are no longer blindfolded by :" + matchedPair.UserData.AliasOrUID, LoggerType.HardcoreActions);
                    await UnequipBlindfold();
                }
                else
                {
                    Logger.LogDebug("You haven't given this assigner permission to apply blindfolds to you!", LoggerType.HardcoreActions);
                }
            }
        });
    }


    private async Task EquipBlindfold()
    {
        Logger.LogDebug($"Equipping blindfold", LoggerType.HardcoreActions);
        // attempt to equip the blindfold to the player
        await _Interop.Glamourer.SetItemToCharacterAsync((ApiEquipSlot)_clientConfigs.GetBlindfoldItem().Slot,
            _clientConfigs.GetBlindfoldItem().GameItem.Id.Id,
            [_clientConfigs.GetBlindfoldItem().GameStain.Stain1.Id, _clientConfigs.GetBlindfoldItem().GameStain.Stain2.Id], 0);
    }

    private async Task UnequipBlindfold()
    {
        Logger.LogDebug($"Unequipping blindfold", LoggerType.HardcoreActions);
        // attempt to unequip the blindfold from the player
        await _Interop.Glamourer.SetItemToCharacterAsync((ApiEquipSlot)_clientConfigs.GetBlindfoldItem().Slot,
            ItemIdVars.NothingItem(_clientConfigs.GetBlindfoldItem().Slot).Id.Id, [0], 0);
    }
    /// <summary> Updates the raw glamourer customization data with our gag items and restraint sets, if applicable </summary>
    private async Task UpdateCachedCharacterData()
    {
        if (_playerManager.CoreDataNull) return;
        // for privacy reasons, we must first make sure that our options for allowing such things are enabled.
        if (_playerManager.GlobalPerms!.RestraintSetAutoEquip)
        {
            await ApplyRestrainSetToCachedCharacterData();
        }

        if (_playerManager.GlobalPerms!.ItemAutoEquip)
        {
            await ApplyGagItemsToCachedCharacterData();
        }

        if (_pairManager.DirectPairs.Any(x => x.UserPairOwnUniquePairPerms.IsBlindfolded))
        {
            await EquipBlindfold();
        }
    }

    /// <summary> Applies the only enabled restraint set to your character on update trigger. </summary>
    private async Task ApplyRestrainSetToCachedCharacterData()
    {
        // Find the first enabled restraint set, if any
        var enabledRestraintSet = _clientConfigs.StoredRestraintSets.FirstOrDefault(rs => rs.Enabled);

        if (enabledRestraintSet == null)
        {
            Logger.LogDebug("No restraint sets are enabled, skipping!", LoggerType.Restraints);
            return;
        }

        var tasks = enabledRestraintSet.DrawData
        .Select(pair =>
        {
            var equipSlot = (ApiEquipSlot)pair.Key;
            var gameItem = pair.Value.GameItem;
            // Handle the "enabled" or "disabled" logic
            if (pair.Value.IsEnabled || !gameItem.Equals(ItemIdVars.NothingItem(pair.Value.Slot)))
            {
                Logger.LogTrace($"Processing slot {equipSlot}", LoggerType.Restraints);
                return _Interop.Glamourer.SetItemToCharacterAsync(equipSlot, gameItem.Id.Id, [pair.Value.GameStain.Stain1.Id, pair.Value.GameStain.Stain2.Id], 0);
            }

            Logger.LogTrace($"Skipping over {equipSlot}!", LoggerType.Restraints);
            return Task.CompletedTask; // No-op task for skipped items
        })
        .ToList(); // Trigger execution

        Logger.LogDebug("Applying Restraint Set to Cached Character Data", LoggerType.Restraints);
        await Task.WhenAll(tasks);
        Logger.LogDebug("Restraint Set Applied", LoggerType.Restraints);
    }

    /// <summary> Applies the gag items to the cached character data. </summary>
    public async Task ApplyGagItemsToCachedCharacterData()
    {
        if (_playerManager.CoreDataNull) return;
        Logger.LogDebug($"Applying Gag Items to Player", LoggerType.GagManagement);
        // Collect only gags that are not of type None
        var gagSlots = _playerManager.AppearanceData!.GagSlots
            .Where(slot => slot.GagType.ToGagType() != GagType.None)
            .ToList();

        // Execute updates in parallel
        var updateTasks = gagSlots
            .Select(slot => UpdateGagItem(slot.GagType.ToGagType(), NewState.Enabled))
            .ToList();

        await Task.WhenAll(updateTasks);
        Logger.LogDebug($"Gag Items Applied to Player", LoggerType.GagManagement);
    }

    /// <summary> Equips a gag by the defined gag name </summary>
    private async Task UpdateGagItem(GagType gagType, NewState updateState)
    {
        // return if the gag in gag storage is not enabled.
        if (!_clientConfigs.IsGagEnabled(gagType)) return;

        // Get the drawData for the gag
        var drawData = _clientConfigs.GetDrawData(gagType);

        // Handle the logic.
        if (updateState is NewState.Enabled)
            await GagApplyAsync(gagType, drawData);
        else
            await GagRemoveAsync(gagType, drawData);
    }


    private async Task GagApplyAsync(GagType gagType, GagDrawData gagInfo)
    {
        Logger.LogInformation("Applying Gag of Type: "+gagType, LoggerType.GagManagement);
        if (gagType is GagType.None) return; // this avoids applying a nothing glamour when unintended.

        // Equip the gag and apply metadata in a separate task
        var equipAndMetaTask = EquipAndSetMetaAsync(gagType, gagInfo, NewState.Enabled);

        // Handle associated moodles if any
        var moodlesTask = HandleMoodlesAsync(gagInfo, NewState.Enabled);

        // Wait for both tasks to complete
        await Task.WhenAll(equipAndMetaTask, moodlesTask);

        // Apply the CustomizePlus profile if applicable
        if (gagInfo.CustomizeGuid != Guid.Empty)
            _Interop.CustomizePlus.EnableProfile(gagInfo.CustomizeGuid);
    }

    private async Task GagRemoveAsync(GagType gagType, GagDrawData gagInfo)
    {
        Logger.LogInformation("Removing Gag of type "+gagType, LoggerType.GagManagement);
        // Equip the gag and apply metadata in a separate task
        var equipAndMetaTask = EquipAndSetMetaAsync(gagType, gagInfo, NewState.Disabled);

        // Handle associated moodles if any
        var moodlesTask = HandleMoodlesAsync(gagInfo, NewState.Disabled);

        // Wait for both tasks to complete
        await Task.WhenAll(equipAndMetaTask, moodlesTask);

        // Apply the CustomizePlus profile if applicable
        if (gagInfo.CustomizeGuid != Guid.Empty)
            _Interop.CustomizePlus.DisableProfile(gagInfo.CustomizeGuid);
    }

    private async Task EquipAndSetMetaAsync(GagType gagType, GagDrawData drawData, NewState newState)
    {
        EquipSlot slot = _clientConfigs.GetGagTypeEquipSlot(gagType);
        // Set the gag
        if (newState is NewState.Enabled)
        {
            Logger.LogInformation($"Setting item {drawData.GameItem.Id.Id} to slot {(ApiEquipSlot)slot} " +
                $"with stain ID's {drawData.GameStain.Stain1.Id} and {drawData.GameStain.Stain2.Id} for gag {gagType.GagName()}", LoggerType.GagManagement);
            await _Interop.Glamourer.SetItemToCharacterAsync((ApiEquipSlot)slot, drawData.GameItem.Id.Id, [drawData.GameStain.Stain1.Id, drawData.GameStain.Stain2.Id], 0);
        }
        else
        {
            Logger.LogInformation($"Set Nothing item to slot {slot} for gag {gagType.GagName()}", LoggerType.GagManagement);
            await _Interop.Glamourer.SetItemToCharacterAsync((ApiEquipSlot)slot, ItemIdVars.NothingItem(slot).Id.Id, [0, 0], 0);
        }

        // Apply metadata if needed
        if (drawData.ForceHeadgearOnEnable || drawData.ForceVisorOnEnable)
        {
            var applyType = drawData.ForceHeadgearOnEnable && drawData.ForceVisorOnEnable
                ? IpcCallerGlamourer.MetaData.Both
                : drawData.ForceHeadgearOnEnable
                    ? IpcCallerGlamourer.MetaData.Hat
                    : IpcCallerGlamourer.MetaData.Visor;

            await _Interop.Glamourer.ForceSetMetaData(applyType, true);
        }
    }

    private async Task HandleMoodlesAsync(GagDrawData drawData, NewState newState)
    {
        if (_playerManager.IpcDataNull) return;

        Logger.LogDebug("Handling Moodles for Gag "+newState, LoggerType.IpcMoodles);

        // see if we are missing any moodles from the associated Moodles.
        bool missingMoodles = drawData.AssociatedMoodles
            .Any(moodle => !_playerManager.LastIpcData!.MoodlesDataStatuses.Any(x => x.GUID == moodle));

        if (newState == NewState.Disabled || missingMoodles)
        {
            Logger.LogDebug(missingMoodles
                ? "Missing moodles, updating."
                : "Disabling, updating moodles.", LoggerType.IpcMoodles);
            Logger.LogDebug("Missing Moodles, updating", LoggerType.IpcMoodles);
            await _moodlesAssociations.ToggleMoodlesTask(drawData.AssociatedMoodles, newState);
        }
    }
}
