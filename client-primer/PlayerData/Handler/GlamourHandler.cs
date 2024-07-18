using GagSpeak.GagspeakConfiguration.Models;
using GagSpeak.PlayerData.Data;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using GagSpeak.UpdateMonitoring;
using GagSpeak.Utils;
using GagspeakAPI.Data.Enum;
using Glamourer.Api.Enums;
using Interop.Ipc;
using System.Reflection;

namespace PlayerData.Handler;

public class GlamourerHandler : DisposableMediatorSubscriberBase
{
    private readonly IpcCallerGlamourer _Interop; // can upgrade this to IpcManager if needed later.
    private readonly ClientConfigurationManager _clientConfigs;
    private readonly PlayerCharacterManager _playerManager;
    private readonly OnFrameworkService _frameworkUtils;

    public GlamourerHandler(ILogger<GlamourerHandler> logger,
        GagspeakMediator mediator, PlayerCharacterManager playerManager,
        ClientConfigurationManager clientConfigs, OnFrameworkService frameworkUtils,
        IpcCallerGlamourer interop) : base(logger, mediator)
    {
        _clientConfigs = clientConfigs;
        _playerManager = playerManager;
        _frameworkUtils = frameworkUtils;
        _Interop = interop;

        // subscribe to our mediator for glamourchanged
        Mediator.Subscribe<GlamourerChangedMessage>(this, (msg) => GlamourerChanged(msg));

        // various glamour update types.
        Mediator.Subscribe<UpdateGlamourMessage>(this, (msg) => UpdateGenericAppearance(msg));
        // gag glamour updates
        Mediator.Subscribe<UpdateGlamourGagsMessage>(this, (msg) => UpdateGagsAppearance(msg));
        // restraint set glamour updates
        Mediator.Subscribe<UpdateGlamourRestraintsMessage>(this, (msg) => UpdateRestraintSetAppearance(msg));
        // blindfold glamour updates
        Mediator.Subscribe<UpdateGlamourBlindfoldMessage>(this, (msg) => UpdateGlamourerBlindfoldAppearance(msg));
    }

    private void GlamourerChanged(GlamourerChangedMessage msg)
    {
        // do not accept if coming from other player besides us.
        if (msg.Address != _frameworkUtils.GetPlayerPointer()) return;

        // do not accept if we have enable wardrobe turned off.
        if (!_playerManager.GlobalPerms.WardrobeEnabled) return;

        // See if the change type is a type we are looking for
        if (msg.ChangeType == StateChangeType.Design
        || msg.ChangeType == StateChangeType.Reapply
        || msg.ChangeType == StateChangeType.Reset
        || msg.ChangeType == StateChangeType.Equip
        || msg.ChangeType == StateChangeType.Stains
        || msg.ChangeType == StateChangeType.Weapon)
        {
            Logger.LogTrace($"StateChangeType is {msg.ChangeType}");

            // fire the disable glamour change event
            Mediator.Publish(new DisableGlamourChangeEvents());
            // call the update glamourer appearance message.
            Mediator.Publish(new UpdateGlamourMessage(GlamourUpdateType.RefreshAll));
        }
        else // it is not a type we care about, so ignore
        {
            Logger.LogTrace($"GlamourerChanged event was not a type we care about, " +
                $"so skipping (Type was: {msg.ChangeType})");
        }
    }

    public async void UpdateGenericAppearance(UpdateGlamourMessage msg)
    {
        // can never be too sure lol, i might forget somewhere.
        Mediator.Publish(new DisableGlamourChangeEvents());

        // do not accept if we have enable wardrobe turned off.
        if (!_playerManager.GlobalPerms.WardrobeEnabled) return;

        try
        {
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
                await _Interop.GlamourerRevertCharacter(_frameworkUtils.GetPlayerPointer());
                // this might be blocking disable restraint sets so look into.
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error Modifying Generic Glamour {ex}");
        }
        finally
        {
            Mediator.Publish(new GlamourChangeEventFinished());
        }
    }

    public async void UpdateGagsAppearance(UpdateGlamourGagsMessage msg)
    {
        // can never be too sure lol, i might forget somewhere.
        Mediator.Publish(new DisableGlamourChangeEvents());

        // do not accept if we have enable wardrobe turned off.
        if (!_playerManager.GlobalPerms.WardrobeEnabled)
        {
            Logger.LogDebug("Wardrobe is disabled, so not processing Gag Update");
            return;
        }

        try
        {
            // For GagEquip
            if (msg.NewState == UpdatedNewState.Enabled && _playerManager.GlobalPerms.ItemAutoEquip)
            {
                Logger.LogDebug($"Processing Gag Equipped");
                // see if the gag we are equipping is not none
                if (msg.GagType.GetGagAlias() != "None")
                {
                    await EquipWithSetItem(msg.Layer, msg.GagType.GetGagAlias(), msg.AssignerName);
                }
                // otherwise, do the same as the unequip function
                else
                {
                    Logger.LogDebug($"GagType is None, but you try setting a gag. What are you doing?");
                }
            }

            // For Gag Unequip
            if (msg.NewState == UpdatedNewState.Disabled && _playerManager.GlobalPerms.ItemAutoEquip)
            {
                Logger.LogDebug($"Processing Gag UnEquip");
                var gagType = Enum.GetValues(typeof(GagList.GagType)).Cast<GagList.GagType>().First(g => g.GetGagAlias() == msg.GagType.GetGagAlias());
                // this should replace it with nothing
                await _Interop.SetItemToCharacterAsync(_frameworkUtils.GetPlayerPointer(), (ApiEquipSlot)_clientConfigs.GetGagTypeEquipSlot(gagType),
                    ItemIdVars.NothingItem(_clientConfigs.GetGagTypeEquipSlot(gagType)).Id.Id, new byte[0], 0);
                // reapply any restraints hiding under them, if any
                await ApplyRestrainSetToCachedCharacterData();
                // update blindfold (TODO)
                if (false)
                {
                    await EquipBlindfold();
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error Modifying Gag Glamour {ex}");
        }
        finally
        {
            Mediator.Publish(new GlamourChangeEventFinished());
        }
    }


    public async void UpdateRestraintSetAppearance(UpdateGlamourRestraintsMessage msg)
    {
        // can never be too sure lol, i might forget somewhere.
        Mediator.Publish(new DisableGlamourChangeEvents());

        // do not accept if we have enable wardrobe turned off.
        if (!_playerManager.GlobalPerms.WardrobeEnabled) return;

        try
        {
            if (msg.NewState == UpdatedNewState.Enabled)
            {
                Logger.LogDebug($"Processing Restraint Set Update");
                // apply restraint set data to character.
                await ApplyRestrainSetToCachedCharacterData();

                // if they allow item auto equip and have any gags equipped, apply them.
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
            else if (msg.NewState == UpdatedNewState.Disabled)
            {
                // now perform a revert based on our customization option
                switch (_clientConfigs.GagspeakConfig.RevertStyle)
                {
                    case RevertStyle.ToGameOnly:
                        await _Interop.GlamourerRevertCharacter(_frameworkUtils.GetPlayerPointer());
                        break;
                    case RevertStyle.ToAutomationOnly:
                        await _Interop.GlamourerRevertCharacterToAutomation(_frameworkUtils.GetPlayerPointer());
                        break;
                    case RevertStyle.ToGameThenAutomation:
                        await _Interop.GlamourerRevertCharacter(_frameworkUtils.GetPlayerPointer());
                        await _Interop.GlamourerRevertCharacterToAutomation(_frameworkUtils.GetPlayerPointer());
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
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error Modifying RestraintSet Glamour {ex}");
        }
        finally
        {
            Mediator.Publish(new GlamourChangeEventFinished());
        }
    }

    // there was a semiphore slim here before, but dont worry about it now.
    public async void UpdateGlamourerBlindfoldAppearance(UpdateGlamourBlindfoldMessage msg)
    {
        // can never be too sure lol, i might forget somewhere.
        Mediator.Publish(new DisableGlamourChangeEvents());

        // do not accept if we have enable wardrobe turned off.
        if (!_playerManager.GlobalPerms.WardrobeEnabled) return;

        try
        {
            if (msg.NewState == UpdatedNewState.Enabled)
            {
                Logger.LogDebug($"Processing Blindfold Equipped");
                // get the index of the person who equipped it onto you (FIXLOGIC TODO)
                if (false)
                {
                    await EquipBlindfold();
                }
                else
                {
                    Logger.LogDebug($"Assigner is not on your whitelist, so not setting item.");
                }
            }

            if (msg.NewState == UpdatedNewState.Disabled)
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
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error processing blindfold update: {ex}");
        }
        finally
        {
            Mediator.Publish(new GlamourChangeEventFinished());
        }
    }

    #region Task Methods for Glamour Updates
    public async Task EquipBlindfold()
    {
        Logger.LogDebug($"Equipping blindfold");
        // attempt to equip the blindfold to the player
        await _Interop.SetItemToCharacterAsync(_frameworkUtils.GetPlayerPointer(),
            (ApiEquipSlot)_clientConfigs.GetBlindfoldItem().Slot, _clientConfigs.GetBlindfoldItem().GameItem.Id.Id,
            [_clientConfigs.GetBlindfoldItem().GameStain.Stain1.Id, _clientConfigs.GetBlindfoldItem().GameStain.Stain2.Id], 0);
    }

    public async Task UnequipBlindfold(int idxOfBlindfold)
    {
        Logger.LogDebug($"Unequipping blindfold");
        // attempt to unequip the blindfold from the player
        await _Interop.SetItemToCharacterAsync(_frameworkUtils.GetPlayerPointer(),
            (ApiEquipSlot)_clientConfigs.GetBlindfoldItem().Slot,
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

        if (_clientConfigs.GetBlindfoldedBy() != string.Empty && _clientConfigs.IsBlindfoldActive())
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
                        tasks.Add(_Interop.SetItemToCharacterAsync(
                            _frameworkUtils.GetPlayerPointer(),
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
                            tasks.Add(_Interop.SetItemToCharacterAsync(
                                _frameworkUtils.GetPlayerPointer(),
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
        Logger.LogDebug($"Applying Gag Items to Cached Character Data");
        // temporary code until glamourer update's its IPC changes
        await EquipWithSetItem(GagLayer.UnderLayer, _playerManager.AppearanceData.SlotOneGagType, "SelfApplied");
        await EquipWithSetItem(GagLayer.MiddleLayer, _playerManager.AppearanceData.SlotTwoGagType, "SelfApplied");
        await EquipWithSetItem(GagLayer.TopLayer, _playerManager.AppearanceData.SlotThreeGagType, "SelfApplied");
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
            await _Interop.SetItemToCharacterAsync(_frameworkUtils.GetPlayerPointer(), (ApiEquipSlot)_clientConfigs.GetGagTypeEquipSlot(gagType),
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
