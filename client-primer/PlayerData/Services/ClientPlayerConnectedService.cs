using FFXIVClientStructs.FFXIV.Client.Game.Character;
using GagSpeak.GagspeakConfiguration.Configurations;
using GagSpeak.Interop.Ipc;
using GagSpeak.PlayerData.Data;
using GagSpeak.PlayerData.Factories;
using GagSpeak.PlayerData.Handlers;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using GagSpeak.UpdateMonitoring;
using GagSpeak.Utils;
using GagspeakAPI.Data.Character;
using GagspeakAPI.Enums;
using GagspeakAPI.Data.Permissions;
using GagspeakAPI.Data.Struct;
using GagspeakAPI.Dto.Connection;
using Interop.Ipc;
using Microsoft.Extensions.Hosting;
using Penumbra.GameData.Data;

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

    public OnConnectedService(ILogger<OnConnectedService> logger,
        GagspeakMediator mediator, PlayerCharacterData playerData,
        ClientConfigurationManager clientConfigs, GagManager gagManager,
        IpcManager ipcManager, IpcFastUpdates ipcFastUpdates,
        AppearanceChangeService visualUpdater) : base(logger, mediator)
    { 
        _playerData = playerData;
        _clientConfigs = clientConfigs;
        _gagManager = gagManager;
        _ipcManager = ipcManager;
        _ipcFastUpdates = ipcFastUpdates;
        _visualUpdater = visualUpdater;

        Mediator.Subscribe<ConnectedMessage>(this, (msg) => OnConnected(msg.Connection));
    }

    private async void OnConnected(ConnectionDto connectionDto)
    {
        Logger.LogTrace("------- Connected Message Received. Processing State Synchronization -------");

        Logger.LogDebug("Setting Global Perms & Appearance from Server.");
        _playerData.GlobalPerms = connectionDto.UserGlobalPermissions;
        _playerData.AppearanceData = connectionDto.CharacterAppearanceData;
        Logger.LogDebug("Data Set");

        await _playerData.GetGlobalPiShockPerms();

        Logger.LogDebug("Setting up Update Tasks from GagSpeak Modules.");
        // update the active gags
        await _gagManager.UpdateActiveGags();

        // if the player is gagged, update their appearance.
        if (_playerData.IsPlayerGagged())
        {
            // we should update the Gag Appearance.
            Logger.LogDebug("Player is Gagged. Updating Gag Appearance.");
            await _visualUpdater.ApplyGagItemsToCachedCharacterData();
        }

        Logger.LogInformation("Syncing Data with Connection DTO");
        var serverData = connectionDto.CharacterActiveStateData;
        // We should sync the active Restraint Set with the server's Active Set Name.
        if (serverData.WardrobeActiveSetName != string.Empty)
        {
            int setIdx = _clientConfigs.GetRestraintSetIdxByName(serverData.WardrobeActiveSetName);

            // check to see if the active set stored on the server is locked with a timer padlock.
            if (GenericHelpers.TimerPadlocks.Contains(serverData.Padlock) && serverData.Timer < DateTimeOffset.UtcNow)
            {
                Logger.LogInformation("The stored active Restraint Set is locked with a Timer Padlock. Unlocking Set.");
                // while this doesn't do anything client side, it will bump an update to the server, updating the active state data.
                _clientConfigs.UnlockRestraintSet(setIdx, serverData.Assigner);

                // if we have it set to remove sets that are unlocked automatically, do so.
                if (_clientConfigs.GagspeakConfig.DisableSetUponUnlock)
                {
                    Logger.LogInformation("Disabling Unlocked Set due to Config Setting.");
                    // we should also update the active state data to disable the set.
                    await _clientConfigs.SetRestraintSetState(setIdx, serverData.WardrobeActiveSetAssigner, NewState.Disabled);
                }
            }
            // if it is not a set that had its time expired, then we should re-enable it, and re-lock it if it was locked.
            else
            {
                Logger.LogInformation("Re-Enabling the stored active Restraint Set");
                await _clientConfigs.SetRestraintSetState(setIdx, serverData.WardrobeActiveSetAssigner, NewState.Enabled);
                // relock it if it had a timer.
                if (serverData.Padlock != Padlocks.None.ToName())
                {
                    Logger.LogInformation("Re-Locking the stored active Restraint Set");
                    _clientConfigs.LockRestraintSet(setIdx, serverData.Padlock, serverData.Password, serverData.Timer, serverData.Assigner);
                }
            }
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
