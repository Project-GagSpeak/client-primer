using GagSpeak.PlayerData.Data;
using GagSpeak.PlayerData.Pairs;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using GagSpeak.Toybox.Services;
using GagSpeak.WebAPI;
using GagSpeak.WebAPI.Utils;
using GagspeakAPI.Data.Enum;
using GagspeakAPI.Data.Permissions;
using GagspeakAPI.Dto.Permissions;
using JetBrains.Annotations;
using Microsoft.Extensions.Hosting;

namespace GagSpeak.Services;

// The most fundementally important service in the entire application.
// helps revert any active states applied to the player when used.
public class SafewordService : MediatorSubscriberBase, IHostedService
{
    private readonly ApiController _apiController; // for sending the updates.
    private readonly PlayerCharacterManager _playerManager; // has our global permissions.
    private readonly PairManager _pairManager; // for accessing the permissions of each pair.
    private readonly ClientConfigurationManager _clientConfigs;
    private readonly GagManager _gagManager; // for removing gags.
    private readonly PatternPlaybackService _patternPlaybackService; // for stopping patterns.

    public SafewordService(ILogger<SafewordService> logger, GagspeakMediator mediator,
        ApiController apiController, PlayerCharacterManager playerManager, 
        PairManager pairManager, ClientConfigurationManager clientConfigs, 
        GagManager gagManager, PatternPlaybackService playbackService) : base(logger, mediator)
    {
        _apiController = apiController;
        _playerManager = playerManager;
        _pairManager = pairManager;
        _clientConfigs = clientConfigs;
        _gagManager = gagManager;
        _patternPlaybackService = playbackService;

        // set the chat log up.
        Mediator.Subscribe<SafewordUsedMessage>(pairManager, (msg) => SafewordUsed());

        Mediator.Subscribe<SafewordHardcoreUsedMessage>(this, (msg) => HardcoreSafewordUsed());

        Mediator.Subscribe<DelayedFrameworkUpdateMessage>(this, (msg) => CheckCooldown());
    }

    private DateTime TimeOfLastSafewordUsed = DateTime.MinValue;
    private DateTime TimeOfLastHardcoreSafewordUsed = DateTime.MinValue;

    // May want to set this to true by default so things are disabled? Idk.
    public bool SafewordIsUsed => _playerManager.GlobalPerms == null ? false : _playerManager.GlobalPerms.SafewordUsed;
    public bool HardcoreSafewordIsUsed => _playerManager.GlobalPerms == null ? false : _playerManager.GlobalPerms.HardcoreSafewordUsed;

    // READ THE FOLLOWING BELOW WHILE CODING OUT THE LOGIC FOR THIS:
    //
    // The Global permissions of a pair has a <SafewordUsed> variable.
    // We should use this variable to determine if someone is under the safeword timer or not.
    //
    // It should NOT DISABLE any bulk permissions for other pairs. this would be too tedious to do.
    // 
    // Instead, this should handle disabling any currently active statuses through various handlers.
    //
    // A SAFEWORD COMMAND SHOULD DISABLE:
    // - Any and all Active Gags
    // - Any active Restraints
    // - Any active Toybox Vibrations
    // - Any Active Triggers
    // - Any Active Patterns
    // - Any Active Alarms
    // - Any Active Orders
    //
    // A HARDCORE SAFEWORD COMMAND SHOULD DISABLE:
    // - Any ForcedToFollow commands.
    // - Any ForcedToSit commands.
    // - Any ForcedToStay commands.
    // - Any Active Blindfolds.
    // - Hardcore mode for every pair.

    private async void SafewordUsed()
    {
        // return if it has not yet been 5 minutes since the last use.
        if (SafewordIsUsed)
        {
            Logger.LogWarning("Hardcore Safeword was used too soon after the last use. Must wait 5 minutes.");
            return;
        }

        // set the time of the last safeword used.
        TimeOfLastSafewordUsed = DateTime.Now;
        Logger.LogInformation("Safeword was used.");

        // we should normally update these via sends to the server, but in the case things are offline, we should update them on our client,
        // then send those updates to the server, to deal with emergency use cases.
        UserGlobalPermissions newGlobalPerms = _playerManager.GlobalPerms ?? new UserGlobalPermissions();
        // update any permissions that may be active to become inactive due to the safeword.
        newGlobalPerms.SafewordUsed = true;
        newGlobalPerms.LiveChatGarblerActive = false;
        newGlobalPerms.LiveChatGarblerLocked = false;
        newGlobalPerms.WardrobeEnabled = false;
        newGlobalPerms.PuppeteerEnabled = false;
        newGlobalPerms.MoodlesEnabled = false;
        newGlobalPerms.ToyboxEnabled = false;
        newGlobalPerms.LockToyboxUI = false;
        newGlobalPerms.ToyIntensity = 0;
        newGlobalPerms.SpatialVibratorAudio = false;
        // update our global permissions and send the new info to the server.
        _playerManager.UpdateGlobalPermsInBulk(newGlobalPerms); // the api callback will correct these with the same values we set after if any inconsistencies anyways.

        _ = _apiController.UserPushAllGlobalPerms(new(_apiController.PlayerUserData, newGlobalPerms));

        // disable any active gags and push these updates to the API.
        _gagManager.SafewordWasUsed();

        // disable any active restraints.
        await _clientConfigs.DisableActiveSetDueToSafeword();

        // disable any active patterns.
        if (_clientConfigs.IsAnyPatternPlaying())
        {
            _patternPlaybackService.StopPattern(_clientConfigs.ActivePatternGuid(), false);
            Mediator.Publish(new PlayerCharToyboxChanged(DataUpdateKind.Safeword));
        }

        // disable any active triggers.
        _clientConfigs.DisableAllTriggersDueToSafeword();

        // disable any active alarms.
        _clientConfigs.DisableAllActiveAlarmsDueToSafeword();

        // disable any active orders.
        // ADD ONCE ORDERS ARE IMPLEMENTED.

    }

    private void HardcoreSafewordUsed()
    {
        if (HardcoreSafewordIsUsed)
        {
            Logger.LogWarning("Hardcore Safeword was used too soon after the last use Wait 1m before using again.");
            return;
        }
        // set the time of the last hardcore safeword used.
        TimeOfLastHardcoreSafewordUsed = DateTime.Now;
        Logger.LogInformation("Hardcore Safeword was used.");

        // push the permission update for the hardcore safeword to the server.
        UserGlobalPermissions newGlobalPerms = _playerManager.GlobalPerms ?? new UserGlobalPermissions();
        newGlobalPerms.HardcoreSafewordUsed = true;
        _playerManager.UpdateGlobalPermsInBulk(newGlobalPerms);
        if(_apiController.ServerState is ServerState.Connected)
        {
            _ = _apiController.UserUpdateOwnGlobalPerm(new(_apiController.PlayerUserData, new KeyValuePair<string, object>("HardcoreSafewordUsed", true)));
        }

        // for each pair in our direct pairs, we should update any and all unique pair permissions to be set regarding Hardcore Status.
        foreach (var pair in _pairManager.DirectPairs)
        {
            if (pair.UserPair.OwnPairPerms.InHardcore)
            {
                // put us out of hardcore, and disable any active hardcore stuff.
                pair.UserPair.OwnPairPerms.InHardcore = false;
                pair.UserPair.OwnPairPerms.AllowForcedFollow = false;
                pair.UserPair.OwnPairPerms.IsForcedToFollow = false;
                pair.UserPair.OwnPairPerms.AllowForcedSit = false;
                pair.UserPair.OwnPairPerms.IsForcedToSit = false;
                pair.UserPair.OwnPairPerms.AllowForcedToStay = false;
                pair.UserPair.OwnPairPerms.IsForcedToStay = false;
                pair.UserPair.OwnPairPerms.AllowBlindfold = false;
                pair.UserPair.OwnPairPerms.IsBlindfolded = false;

                // send the updates to the server.
                if (_apiController.ServerState is ServerState.Connected)
                {
                    _ = _apiController.UserPushAllUniquePerms(new(pair.UserData, pair.UserPair.OwnPairPerms, pair.UserPair.OwnEditAccessPerms));
                }
            }
        }
    }

    private void CheckCooldown()
    {
        // check if it has been 5 minutes since the last safeword was used.
        if (SafewordIsUsed && TimeOfLastSafewordUsed.AddMinutes(5) < DateTime.Now)
        {
            if (_playerManager.GlobalPerms != null) _playerManager.GlobalPerms.SafewordUsed = false;
        }

        // check if it has been 5 minutes since the last hardcore safeword was used.
        if (HardcoreSafewordIsUsed && TimeOfLastHardcoreSafewordUsed.AddMinutes(1) < DateTime.Now)
        {
            if (_playerManager.GlobalPerms != null) _playerManager.GlobalPerms.HardcoreSafewordUsed = false;
        }
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        Logger.LogInformation("Started Safeword Service.");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Logger.LogInformation("Stopped Safeword Service.");
        return Task.CompletedTask;
    }
}
