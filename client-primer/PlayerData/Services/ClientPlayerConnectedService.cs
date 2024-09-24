using GagSpeak.Interop.Ipc;
using GagSpeak.PlayerData.Data;
using GagSpeak.PlayerData.Handlers;
using GagSpeak.PlayerData.Pairs;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using GagSpeak.UI;
using GagSpeak.Utils;
using GagspeakAPI.Data.Struct;
using GagspeakAPI.Dto.Connection;
using GagspeakAPI.Enums;
using Microsoft.Extensions.Hosting;

namespace GagSpeak.PlayerData.Services;

// A class intended to help execute any actions that should be performed by the client upon initial connection.
public sealed class OnConnectedService : DisposableMediatorSubscriberBase, IHostedService
{
    private readonly PlayerCharacterData _playerData;
    private readonly ClientConfigurationManager _clientConfigs;
    private readonly GagManager _gagManager;
    private readonly IpcManager _ipcManager;
    private readonly IpcFastUpdates _ipcFastUpdates;
    private readonly AppearanceChangeService _visualUpdater;
    private readonly HardcoreHandler _blindfold;

    public OnConnectedService(ILogger<OnConnectedService> logger,
        GagspeakMediator mediator, PlayerCharacterData playerData,
        ClientConfigurationManager clientConfigs, GagManager gagManager,
        IpcManager ipcManager, IpcFastUpdates ipcFastUpdates,
        AppearanceChangeService visualUpdater, HardcoreHandler blindfold) : base(logger, mediator)
    {
        _playerData = playerData;
        _clientConfigs = clientConfigs;
        _gagManager = gagManager;
        _ipcManager = ipcManager;
        _ipcFastUpdates = ipcFastUpdates;
        _visualUpdater = visualUpdater;
        _blindfold = blindfold;

        Mediator.Subscribe<ConnectedMessage>(this, (msg) => OnConnected(msg.Connection));

        Mediator.Subscribe<OnlinePairsLoadedMessage>(this, _ => CheckBlindfold());

        Mediator.Subscribe<CustomizeReady>(this, _ => _playerData.CustomizeProfiles = _ipcManager.CustomizePlus.GetProfileList());

        Mediator.Subscribe<CustomizeDispose>(this, _ => _playerData.CustomizeProfiles = new List<CustomizeProfile>());
    }

    private async void OnConnected(ConnectionDto connectionDto)
    {
        Logger.LogInformation("------- Connected Message Received. Processing State Synchronization -------");

        Logger.LogDebug("Setting Global Perms & Appearance from Server.", LoggerType.ClientPlayerData);
        _playerData.GlobalPerms = connectionDto.UserGlobalPermissions;
        _playerData.AppearanceData = connectionDto.CharacterAppearanceData;
        Logger.LogDebug("Data Set", LoggerType.ClientPlayerData);

        await _playerData.GetGlobalPiShockPerms();

        Logger.LogDebug("Setting up Update Tasks from GagSpeak Modules.", LoggerType.ClientPlayerData);
        // update the active gags
        await _gagManager.UpdateActiveGags();

        // if the player is gagged, update their appearance.
        if (_playerData.IsPlayerGagged())
        {
            // we should update the Gag Appearance.
            Logger.LogDebug("Player is Gagged. Updating Gag Appearance.", LoggerType.GagManagement);
            await _visualUpdater.ApplyGagItemsToCachedCharacterData();
        }

        Logger.LogInformation("Syncing Data with Connection DTO", LoggerType.ClientPlayerData);
        var serverData = connectionDto.CharacterActiveStateData;
        // We should sync the active Restraint Set with the server's Active Set Name.
        if (serverData.WardrobeActiveSetName != string.Empty)
        {
            int setIdx = _clientConfigs.GetRestraintSetIdxByName(serverData.WardrobeActiveSetName);

            // check to see if the active set stored on the server is locked with a timer padlock.
            if (GenericHelpers.TimerPadlocks.Contains(serverData.Padlock) && serverData.Timer < DateTimeOffset.UtcNow)
            {
                Logger.LogInformation("The stored active Restraint Set is locked with a Timer Padlock. Unlocking Set.", LoggerType.Restraints);
                // while this doesn't do anything client side, it will bump an update to the server, updating the active state data.
                _clientConfigs.UnlockRestraintSet(setIdx, serverData.Assigner);

                // if we have it set to remove sets that are unlocked automatically, do so.
                if (_clientConfigs.GagspeakConfig.DisableSetUponUnlock)
                {
                    Logger.LogInformation("Disabling Unlocked Set due to Config Setting.", LoggerType.Restraints);
                    // we should also update the active state data to disable the set.
                    await _clientConfigs.SetRestraintSetState(setIdx, serverData.WardrobeActiveSetAssigner, NewState.Disabled);
                }
            }
            // if it is not a set that had its time expired, then we should re-enable it, and re-lock it if it was locked.
            else
            {
                Logger.LogInformation("Re-Enabling the stored active Restraint Set", LoggerType.Restraints);
                await _clientConfigs.SetRestraintSetState(setIdx, serverData.WardrobeActiveSetAssigner, NewState.Enabled);
                // relock it if it had a timer.
                if (serverData.Padlock != Padlocks.None.ToName())
                {
                    Logger.LogInformation("Re-Locking the stored active Restraint Set", LoggerType.Restraints);
                    _clientConfigs.LockRestraintSet(setIdx, serverData.Padlock, serverData.Password, serverData.Timer, serverData.Assigner);
                }
            }
        }
    }

    private async void CheckBlindfold()
    { 
        // equip any blindfolds.
        if(_blindfold.IsCurrentlyBlindfolded())
        {
            if(_blindfold.BlindfoldPair != null)
                await _blindfold.HandleBlindfoldLogic(NewState.Enabled, _blindfold.BlindfoldPair.UserData.UID);
        }
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        Logger.LogInformation("Starting OnConnectedService");

        // grab the latest CustomizePlus Profile List.
        _playerData.CustomizeProfiles = _ipcManager.CustomizePlus.GetProfileList();

        Logger.LogInformation("Started OnConnectedService");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Logger.LogInformation("Stopping IpcProvider Service");

        return Task.CompletedTask;
    }


}
