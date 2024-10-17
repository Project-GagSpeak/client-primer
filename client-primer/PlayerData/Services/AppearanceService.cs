using GagSpeak.Interop;
using GagSpeak.Interop.Ipc;
using GagSpeak.PlayerData.Data;
using GagSpeak.PlayerData.Handlers;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using GagSpeak.UI.Components;
using GagSpeak.UpdateMonitoring;
using GagSpeak.Utils;
using GagspeakAPI.Extensions;
using Glamourer.Api.Enums;
using Penumbra.GameData.Enums;

namespace GagSpeak.PlayerData.Services;

/// <summary>
/// Service for updating the appearance of the client player via any updates sent from modules in the plugin.
/// </summary>
public class AppearanceService : DisposableMediatorSubscriberBase
{
    private readonly IpcManager _Interop; // can upgrade this to IpcManager if needed later.
    private readonly ClientConfigurationManager _clientConfigs;
    private readonly PlayerCharacterData _playerManager;
    private readonly OnFrameworkService _frameworkUtils;

    public AppearanceService(ILogger<AppearanceService> logger,
        GagspeakMediator mediator, ClientConfigurationManager clientConfigs,
        PlayerCharacterData playerManager, OnFrameworkService frameworkUtils,
        IpcManager interop) : base(logger, mediator)
    {
        _Interop = interop;
        _clientConfigs = clientConfigs;
        _playerManager = playerManager;
        _frameworkUtils = frameworkUtils;

        _cts = new CancellationTokenSource(); // for handling gearset changes

        // subscribe to our mediator for glamour changed
        IpcFastUpdates.GlamourEventFired += (msg) => RefreshAppearance(msg).ConfigureAwait(false);
        IpcFastUpdates.CustomizeEventFired += EnsureForcedCustomizeProfile;
        Mediator.Subscribe<ZoneSwitchEndMessage>(this, (_) => RefreshAppearance(GlamourUpdateType.ZoneChange).ConfigureAwait(false));
    }

    /// <summary>
    /// The Finalized Glamourer Appearance that should be visible on the player.
    /// This is to be maintained by AppearanceHandler. 
    /// </summary>
    public Dictionary<EquipSlot, IGlamourItem> ItemsToApply { get; set; } = new Dictionary<EquipSlot, IGlamourItem>();
    public IpcCallerGlamourer.MetaData MetaToApply { get; set; } = IpcCallerGlamourer.MetaData.None;
    public List<Guid> ExpectedMoodles { get; set; } = new List<Guid>();


    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        IpcFastUpdates.GlamourEventFired -= (msg) => RefreshAppearance(msg).ConfigureAwait(false);
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

    public async Task RefreshAppearance(GlamourUpdateType updateKind)
    {
        await ExecuteWithSemaphore(async () =>
        {
            // Triggered by using safeword, will undo everything.
            if (updateKind is GlamourUpdateType.Safeword)
            {
                Logger.LogInformation($"Processing Safeword Update", LoggerType.Safeword);
                await RevertPlayer();
                return;
            }
            else if (updateKind is GlamourUpdateType.JobChange or GlamourUpdateType.RefreshAll)
            {
                Logger.LogInformation($"Processing [" + updateKind.ToString() + "]", LoggerType.ClientPlayerData);
                await RevertPlayer();
            }


            // if it is not a safeword, process if core data is not null.
            if (_playerManager.CoreDataNull) return;

            Logger.LogDebug("Processing Appearance Refresh due to UpdateType: [" + updateKind.ToString() + "]", LoggerType.ClientPlayerData);
            await Task.Run(() => _frameworkUtils.RunOnFrameworkThread(() => ReapplyAppearance()));
        });
    }

    private async Task RevertPlayer()
    {
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

    /// <summary>
    /// This function is a revamped version that optimizes all other appearnace update functions.
    /// Its purpose is to serve a more reliable and consistant update order for all appearance updates.
    /// 
    /// <para>
    /// When you have time, create an appearance monitor to centralize the stored collective data of what should be active.
    /// This way, its only updated once when applied or removed, and doesnt need to be fetched on every single update.
    /// (do this if you find noticable performance encounters)
    /// </para>
    /// </summary>
    private async Task ReapplyAppearance()
    {
        if (_playerManager.CoreDataNull) return;

        Logger.LogDebug("Refreshing Appearance", LoggerType.ClientPlayerData);
        // If the AppearanceHandler has done its job, all data should be stored to
        // the latest appearnace. We should only need to apply it now.

        // Store tasks in a list
        var tasks = new List<Task>();

        // Queue the task to apply glamour items and metadata.
        if (ItemsToApply.Any())
            tasks.Add(UpdateGlamour());

        // Queue the task to apply moodles.
        if (ExpectedMoodles.Any())
            tasks.Add(UpdateMoodles());

        // Run all tasks concurrently
        await Task.WhenAll(tasks);
        Logger.LogDebug("Appearance Refresh Completed", LoggerType.ClientPlayerData);
    }

    public async Task UpdateGlamour()
    {
        if (!IpcCallerGlamourer.APIAvailable) return;

        // configure the tasks to execute together instead of one by one.
        var tasks = ItemsToApply
        .Select(pair =>
        {
            var equipSlot = (ApiEquipSlot)pair.Key;
            var gameItem = pair.Value.GameItem;
            // Handle the "enabled" or "disabled" logic
            if (pair.Value.IsEnabled || !gameItem.Equals(ItemIdVars.NothingItem(pair.Value.Slot)))
            {
                Logger.LogTrace($"Updating slot {equipSlot}", LoggerType.ClientPlayerData);
                return _Interop.Glamourer.SetItemToCharacterAsync(equipSlot, gameItem.Id.Id, [pair.Value.GameStain.Stain1.Id, pair.Value.GameStain.Stain2.Id], 0);
            }
            Logger.LogTrace($"Skipping over {equipSlot}!", LoggerType.ClientPlayerData);
            return Task.CompletedTask;
        })
        .ToList(); // Trigger execution

        Logger.LogTrace("Applying Update to Glamour from current active Items", LoggerType.ClientPlayerData);
        await Task.WhenAll(tasks);

        // update the meta data.
        await _Interop.Glamourer.ForceSetMetaData(MetaToApply, true);
        Logger.LogDebug("Glamour Update Completed", LoggerType.ClientPlayerData);
    }

    public async Task UpdateMoodles()
    {
        if (_playerManager.IpcDataNull || !IpcCallerMoodles.APIAvailable) return;
        // Fetch the current list of moodles on our character
        var currentMoodles = AppearanceHandler.LatestClientMoodleStatusList.Select(x => x.GUID).ToList();

        // take the Expected moodles minus the current moodles to get the moodles we are missing.
        var missingMoodles = ExpectedMoodles.Except(currentMoodles).ToList();

        // apply those moodles.
        Logger.LogTrace("Missing Moodles, updating", LoggerType.ClientPlayerData);
        await _Interop.Moodles.ApplyOwnStatusByGUID(missingMoodles);
        Logger.LogTrace("Moodles Update Completed", LoggerType.ClientPlayerData);
    }
}
