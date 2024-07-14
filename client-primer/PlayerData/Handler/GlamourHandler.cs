using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Threading;
using GagSpeak.Events;
using GagSpeak.Utility;
using GagSpeak.Wardrobe;
using GagspeakAPI.Data.Enum;
using GagSpeak.Gagsandlocks;
using GagSpeak.UI.Equipment;
using System.Collections.Generic;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using GagSpeak.Services;
using GagSpeak.Hardcore;
using Glamourer.Api.Helpers;
using Glamourer.Api.Enums;
using Glamourer.Api.IpcSubscribers;
using Interop.Ipc;
using GagSpeak.Services.Mediator;
using GagSpeak.PlayerData.Data;
using GagSpeak.UpdateMonitoring;
using GagSpeak.GagspeakConfiguration.Models;
using GagSpeak.Services.ConfigurationServices;

namespace PlayerData.Handler;

public class GlamourerHandler : DisposableMediatorSubscriberBase
{
    private readonly IpcCallerGlamourer _Interop; // can upgrade this to ipccallermanager if needed later.
    private readonly ClientConfigurationManager _clientConfigs;
    private readonly PlayerCharacterManager _playerManager;
    private readonly OnFrameworkService _frameworkUtils;
    private readonly IFramework _framework;

    public GlamourerHandler(ILogger<GlamourerHandler> logger,
        GagspeakMediator mediator, PlayerCharacterManager playerManager,
        ClientConfigurationManager clientConfigs, OnFrameworkService frameworkUtils,
        IpcCallerGlamourer interop, IFramework framework) : base(logger, mediator)
    {
        _clientConfigs = clientConfigs;
        _playerManager = playerManager;
        _frameworkUtils = frameworkUtils;
        _Interop = interop;
        _framework = framework;

        // subscribe to our mediator for glamourchanged
        Mediator.Subscribe<GlamourerChangedMessage>(this, (msg) => GlamourerChanged(msg));

        // various glamour update types.
        Mediator.Subscribe<UpdateGlamourMessage>(this, (msg) => UpdateGlamourerAppearance(msg));
        // gag glamour updates
        Mediator.Subscribe<UpdateGlamourGagsMessage>(this, (msg) => UpdateGagsAppearance(msg));
        // restraint set glamour updates
        Mediator.Subscribe<UpdateGlamourRestraintsMessage>(this, (msg) => UpdateRestraintSetAppearance(msg));
        // sub to framework update
        _framework.Update += FrameworkUpdate; // might not even need this????
    }
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        _framework.Update -= FrameworkUpdate;
    }

    public void FrameworkUpdate(IFramework framework)
    {
        /* Damn i dont think we need this */
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
        || msg.ChangeType == StateChangeType.Stain
        || msg.ChangeType == StateChangeType.Weapon)
        {
            Logger.LogTrace($"StateChangeType is {msg.ChangeType}");

            // fire the disable glamour change event
            Mediator.Publish(new DisableGlamourChangeEvents());
            // call the update glamourer appearance message.
            Mediator.Publish(new UpdateGlamourerMessage(GlamourUpdateType.RefreshAll));
        }
        else // it is not a type we care about, so ignore
        {
            Logger.LogTrace($"GlamourerChanged event was not a type we care about, "+
                $"so skipping (Type was: {msg.ChangeType})");
        }
    }

    public async void UpdateGenericAppearance(UpdateGlamourerMessage msg)
    {
        // can never be too sure lol, i might forget somewhere.
        Mediator.Publish(new DisableGlamourChangeEvents());


    }

    public async void UpdateGagsAppearance(UpdateGlamourGagsMessage msg)
    {
        // can never be too sure lol, i might forget somewhere.
        Mediator.Publish(new DisableGlamourChangeEvents());


    }


    public async void UpdateRestraintSetAppearance(UpdateGlamourRestraintsMessage msg)
    {
        // can never be too sure lol, i might forget somewhere.
        Mediator.Publish(new DisableGlamourChangeEvents());

        try
        {
            if (msg.NewState == RestraintSetState.Enabled)
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
                    await EquipBlindfold(-1);
                }
            }
            else if (msg.NewState == RestraintSetState.Disabled)
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
                    await EquipBlindfold(-1);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error Modifying RestraintSet Glamour {ex}");
        }
    }

    // there was a semiphore slim here before, but dont worry about it now.
    public async void UpdateGlamourerAppearance(UpdateGlamourMessage msg)
    {
        // can never be too sure lol, i might forget somewhere.
        Mediator.Publish(new DisableGlamourChangeEvents());

        // do not accept if we have enable wardrobe turned off.
        if (!_playerManager.GlobalPerms.WardrobeEnabled) return;

        Logger.LogDebug($"Glamourer Appearance Update Called");
        // conditionals:
        try
        {


            // condition 3 --> it was an equip gag type event, we should take the gag type, and replace it with the item
            if (e.UpdateType == UpdateType.GagEquipped && _characterHandler.playerChar._allowItemAutoEquip)
            {
                _logger.LogDebug($"[GlamourEventFired]: Processing Gag Equipped");
                if (e.GagType != "None")
                {
                    await EquipWithSetItem(e.GagType, e.AssignerName);
                }
                // otherwise, do the same as the unequip function
                else
                {
                    _logger.LogDebug($"[GlamourEventFired]: GagType is None, not setting item.");
                    // if it is none, then do the same as the unequip function.
                    var gagType = Enum.GetValues(typeof(GagList.GagType))
                                    .Cast<GagList.GagType>()
                                    .First(g => g.GetGagAlias() == e.GagType);
                    // unequip it
                    await _Interop.SetItemToCharacterAsync(
                            _frameworkUtils.GetPlayerPointer(),
                            (ApiEquipSlot)_gagStorageManager._gagEquipData[gagType]._slot,
                            ItemIdVars.NothingItem(_gagStorageManager._gagEquipData[gagType]._slot).Id.Id,
                            0,
                            0
                    );
                }
            }

            // condition 4 --> it was an disable gag type event, we should take the gag type, and replace it with a nothing item
            if (e.UpdateType == UpdateType.GagUnEquipped && _characterHandler.playerChar._allowItemAutoEquip)
            {
                // get the gagtype
                _logger.LogDebug($"[GlamourEventFired]: Processing Gag UnEquipped");
                var gagType = Enum.GetValues(typeof(GagList.GagType)).Cast<GagList.GagType>().First(g => g.GetGagAlias() == e.GagType);
                // this should replace it with nothing
                await _Interop.SetItemToCharacterAsync(
                        _frameworkUtils.GetPlayerPointer(),
                        (ApiEquipSlot)_gagStorageManager._gagEquipData[gagType]._slot,
                        ItemIdVars.NothingItem(_gagStorageManager._gagEquipData[gagType]._slot).Id.Id,
                        0,
                        0
                );
                // reapply any restraints hiding under them, if any
                await ApplyRestrainSetToCachedCharacterData();
                // update blindfold
                if (_hardcoreManager.IsBlindfoldedForAny(out var enabledIdx, out var playerWhoBlindfoldedYou))
                {
                    await EquipBlindfold(enabledIdx);
                }
                else
                {
                    _logger.LogDebug($"[GlamourEventFired]: Player was not blindfolded, IGNORING");
                }
            }

            // condition 5 --> it was a gag refresh event, we should reapply all the gags
            if (e.UpdateType == UpdateType.UpdateGags)
            {
                _logger.LogDebug($"[GlamourEventFired]: Processing Update Gags");
                await ApplyGagItemsToCachedCharacterData();
            }

            // condition 6 --> it was a job change event, refresh all, but wait for the framework thread first
            if (e.UpdateType == UpdateType.JobChange)
            {
                _logger.LogDebug($"[GlamourEventFired]: Processing Job Change");
                await Task.Run(() => _onFrameworkService.RunOnFrameworkThread(UpdateCachedCharacterData));
            }

            // condition 7 --> it was a refresh all event, we should reapply all the gags and restraint sets
            if (e.UpdateType == UpdateType.RefreshAll || e.UpdateType == UpdateType.ZoneChange || e.UpdateType == UpdateType.Login)
            {
                _logger.LogDebug($"[GlamourEventFired]: Processing Refresh All // Zone Change // Login // Job Change");
                // apply all the gags and restraint sets
                await Task.Run(() => _onFrameworkService.RunOnFrameworkThread(UpdateCachedCharacterData));
            }

            // condition 8 --> it was a safeword event, we should revert to the game, then to game and disable toys
            if (e.UpdateType == UpdateType.Safeword)
            {
                _logger.LogDebug($"[GlamourEventFired]: Processing Safeword");
                await _Interop.GlamourerRevertCharacter(_frameworkUtils.GetPlayerPointer());
                // this might be blocking disable restraint sets so look into.
            }

            // condition 9 -- > it was a blindfold equipped event, we should apply the blindfold
            if (e.UpdateType == UpdateType.BlindfoldEquipped)
            {
                _logger.LogDebug($"[GlamourEventFired]: Processing Blindfold Equipped");
                // get the index of the person who equipped it onto you
                if (AltCharHelpers.IsPlayerInWhitelist(e.AssignerName, out int whitelistCharIdx))
                {
                    await EquipBlindfold(whitelistCharIdx);
                }
                else
                {
                    _logger.LogDebug($"[GlamourEventFired]: Assigner {e.AssignerName} is not on your whitelist, so not setting item.");
                }
            }

            // condition 10 -- > it was a blindfold unequipped event, we should remove the blindfold
            if (e.UpdateType == UpdateType.BlindfoldUnEquipped)
            {
                _logger.LogDebug($"[GlamourEventFired]: Processing Blindfold UnEquipped");
                // get the index of the person who equipped it onto you
                if (AltCharHelpers.IsPlayerInWhitelist(e.AssignerName, out int whitelistCharIdx))
                {
                    await UnequipBlindfold(whitelistCharIdx);
                }
                else
                {
                    _logger.LogDebug($"[GlamourEventFired]: Assigner {e.AssignerName} is not on your whitelist, so not setting item.");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"[GlamourEventFired]: Error processing glamour event: {ex}");
        }
        finally
        {
            _gagSpeakGlamourEvent.IsGagSpeakGlamourEventExecuting = false;
            finishedDrawingGlamChange = true;
            semaphore.Release();
        }
    }
    #region Task Methods for Glamour Updates
    public async Task EquipBlindfold(int idxOfBlindfold)
    {
        _logger.LogDebug($"[GlamourEventFired]: Found index {idxOfBlindfold} for blindfold");
        // attempt to equip the blindfold to the player
        await _Interop.SetItemToCharacterAsync(
            _frameworkUtils.GetPlayerPointer(),
            (ApiEquipSlot)_hardcoreManager._perPlayerConfigs[idxOfBlindfold]._blindfoldItem._slot,
            _hardcoreManager._perPlayerConfigs[idxOfBlindfold]._blindfoldItem._gameItem.Id.Id, // The _drawData._gameItem.Id.Id
            _hardcoreManager._perPlayerConfigs[idxOfBlindfold]._blindfoldItem._gameStain.Id, // The _drawData._gameStain.Id
            0
        );
    }

    public async Task UnequipBlindfold(int idxOfBlindfold)
    {
        _logger.LogDebug($"[GlamourEventFired]: Found index {idxOfBlindfold} for blindfold");
        // attempt to unequip the blindfold from the player
        await _Interop.SetItemToCharacterAsync(
            _frameworkUtils.GetPlayerPointer(),
            (ApiEquipSlot)_hardcoreManager._perPlayerConfigs[idxOfBlindfold]._blindfoldItem._slot,
            ItemIdVars.NothingItem(_hardcoreManager._perPlayerConfigs[idxOfBlindfold]._blindfoldItem._slot).Id.Id, // The _drawData._gameItem.Id.Id
            0,
            0
        );
    }
    /// <summary> Updates the raw glamourer customization data with our gag items and restraint sets, if applicable </summary>
    public async Task UpdateCachedCharacterData()
    {
        // for privacy reasons, we must first make sure that our options for allowing such things are enabled.
        if (_characterHandler.playerChar._allowRestraintSetAutoEquip)
        {
            await ApplyRestrainSetToCachedCharacterData();
        }
        else
        {
            _logger.LogDebug($"[GlamourerChanged]: Restraint Set Auto-Equip disabled, IGNORING");
        }

        if (_characterHandler.playerChar._allowItemAutoEquip)
        {
            await ApplyGagItemsToCachedCharacterData();
        }
        else
        {
            _logger.LogDebug($"[GlamourerChanged]: Item Auto-Equip disabled, IGNORING");
        }

        // try and get the blindfolded status to see if we are blindfolded
        if (_hardcoreManager.IsBlindfoldedForAny(out var enabledIdx, out var playerWhoBlindfoldedYou))
        {
            await EquipBlindfold(enabledIdx);
        }
        else
        {
            _logger.LogDebug($"[GlamourerChanged]: Player was not blindfolded, IGNORING");
        }
    }

    /// <summary> Applies the only enabled restraint set to your character on update trigger. </summary>
    public async Task ApplyRestrainSetToCachedCharacterData()
    { // dummy placeholder line
        // Find the restraint set with the matching name
        var tasks = new List<Task>();
        foreach (var restraintSet in _restraintSetManager._restraintSets)
        {
            // If the restraint set is enabled
            if (restraintSet._enabled)
            {
                // Iterate over each EquipDrawData in the restraint set
                foreach (var pair in restraintSet._drawData)
                {
                    // see if the item is enabled or not (controls it's visibility)
                    if (pair.Value._isEnabled)
                    {
                        // because it is enabled, we will still apply nothing items
                        tasks.Add(_Interop.SetItemToCharacterAsync(
                            _frameworkUtils.GetPlayerPointer(),
                            (ApiEquipSlot)pair.Key, // the key (EquipSlot)
                            pair.Value._gameItem.Id.Id, // Set this slot to nothing (naked)
                            pair.Value._gameStain.Id, // The _drawData._gameStain.Id
                            0));
                    }
                    else
                    {
                        // Because it was disabled, we will treat it as an overlay, ignoring it if it is a nothing item
                        if (!pair.Value._gameItem.Equals(ItemIdVars.NothingItem(pair.Value._slot)))
                        {
                            // Apply the EquipDrawData
                            _logger.LogDebug($"[ApplyRestrainSetToData] Calling on Helmet Placement {pair.Key}!");
                            tasks.Add(_Interop.SetItemToCharacterAsync(
                                _frameworkUtils.GetPlayerPointer(),
                                (ApiEquipSlot)pair.Key, // the key (EquipSlot)
                                pair.Value._gameItem.Id.Id, // The _drawData._gameItem.Id.Id
                                pair.Value._gameStain.Id, // The _drawData._gameStain.Id
                                0));
                        }
                        else
                        {
                            _logger.LogDebug($"[ApplyRestrainSetToData] Skipping over {pair.Key}!");
                        }
                    }
                }
                // early exit, we only want to apply one restraint set
                _logger.LogDebug($"[ApplyRestrainSetToData]: Applying Restraint Set to Cached Character Data");
                await Task.WhenAll(tasks);
                return;
            }
        }
        _logger.LogDebug($"[ApplyRestrainSetToData]: No restraint sets are enabled, skipping!");
    }

    /// <summary> Applies the gag items to the cached character data. </summary>
    public async Task ApplyGagItemsToCachedCharacterData()
    {
        _logger.LogDebug($"[ApplyGagItems]: Applying Gag Items to Cached Character Data");
        // temporary code until glamourer update's its IPC changes
        await EquipWithSetItem(_characterHandler.playerChar._selectedGagTypes[0], "self");
        await EquipWithSetItem(_characterHandler.playerChar._selectedGagTypes[1], "self");
        await EquipWithSetItem(_characterHandler.playerChar._selectedGagTypes[2], "self");
    }

    public async Task EquipWithSetItem(string gagName, string assignerName = "")
    {
        // if the gagName is none, then just dont set it and return
        if (gagName == "None")
        {
            _logger.LogDebug($"[OnGagEquippedEvent]: GagLayer Not Equipped.");
            return;
        }
        // Get the gagtype (enum) where it's alias matches the gagName
        var gagType = Enum.GetValues(typeof(GagList.GagType)).Cast<GagList.GagType>().First(g => g.GetGagAlias() == gagName);
        // See if the GagType is in our dictionary & that the gagName is not "None" (because .First() would make gagType==BallGag when gagName==None)
        if (_gagStorageManager._gagEquipData[gagType]._isEnabled == false)
        {
            _logger.LogDebug($"[Interop - SetItem]: GagType {gagName} is not enabled, so not setting item.");
            return;
        }
        // otherwise let's do the rest of the stuff
        if (assignerName == "self") _gagStorageManager.ChangeGagDrawDataWasEquippedBy(gagType, "self");
        if (assignerName != "self") _gagStorageManager.ChangeGagDrawDataWasEquippedBy(gagType, assignerName);

        // see if assigner is valid
        if (ValidGagAssignerUser(gagName, _gagStorageManager._gagEquipData[gagType]))
        {
            try
            {
                await _Interop.SetItemToCharacterAsync(_frameworkUtils.GetPlayerPointer(),
                                                       (ApiEquipSlot)_gagStorageManager._gagEquipData[gagType]._slot,
                                                       _gagStorageManager._gagEquipData[gagType]._gameItem.Id.Id,
                                                       _gagStorageManager._gagEquipData[gagType]._gameStain.Id,
                                                       0
                );
                _logger.LogDebug($"[Interop - SetItem]: Set item {_gagStorageManager._gagEquipData[gagType]._gameItem} to slot {_gagStorageManager._gagEquipData[gagType]._slot} for gag {gagName}");
            }
            catch (TargetInvocationException ex)
            {
                _logger.LogError($"[Interop - SetItem]: Error setting item: {ex.InnerException}");
            }
        }
        else
        {
            _logger.LogDebug($"[Interop - SetItem]: Assigner {assignerName} is not valid, so not setting item.");
        }
    }

    /// <summary> A Helper function that will return true if ItemAutoEquip should occur based on the assigner name, or if it shouldnt. </summary>
    public bool ValidGagAssignerUser(string gagName, EquipDrawData equipDrawData)
    {
        // next, we need to see if the gag is being equipped.
        if (equipDrawData._wasEquippedBy == "self")
        {
            _logger.LogDebug($"[ValidGagAssignerUser]: GagType {gagName} is being equipped by yourself, abiding by your config settings for Item Auto-Equip.");
            return true;
        }
        // if we hit here, it was an assignerName
        string tempAssignerName = equipDrawData._wasEquippedBy;
        var words = tempAssignerName.Split(' ');
        tempAssignerName = string.Join(" ", words.Take(2)); // should only take the first two words
        // if the name matches anyone in the Whitelist:
        if (AltCharHelpers.IsPlayerInWhitelist(tempAssignerName, out int whitelistCharIdx))
        {
            // if the _yourStatusToThem is pet or slave, and the _theirStatusToYou is Mistress, then we can equip the gag.
            if (_characterHandler.whitelistChars[whitelistCharIdx].IsRoleLeanSubmissive(_characterHandler.whitelistChars[whitelistCharIdx]._yourStatusToThem)
            && _characterHandler.whitelistChars[whitelistCharIdx].IsRoleLeanDominant(_characterHandler.whitelistChars[whitelistCharIdx]._theirStatusToYou))
            {
                _logger.LogDebug($"[ValidGagAssignerUser]: You are a pet/slave to the gag assigner, and {tempAssignerName} is your Mistress. Because this two way relationship is established, allowing Item Auto-Eqiup.");
                return true;
            }
            else
            {
                _logger.LogDebug($"[ValidGagAssignerUser]: {tempAssignerName} is not someone you are a pet or slave to, nor are they defined as your Mistress. Thus, Item Auto-Equip being disabled for this gag.");
                return false;
            }
        }
        else
        {
            _logger.LogDebug($"[ValidGagAssignerUser]: GagType {gagName} is being equipped by {tempAssignerName}, but they are not on your whitelist, so we are not doing Item-AutoEquip.");
            return false;
        }
    }
    #endregion Task Methods for Glamour Updates
}
