using GagSpeak.PlayerData.Data;
using GagSpeak.PlayerData.Handlers;
using GagSpeak.PlayerData.Pairs;
using GagSpeak.PlayerData.Services;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using GagSpeak.Toybox.Services;
using GagSpeak.WebAPI;
using GagspeakAPI.Data.Permissions;
using Microsoft.Extensions.Hosting;

namespace GagSpeak.Services;

// The most fundamentally important service in the entire application.
// helps revert any active states applied to the player when used.
public class SafewordService : MediatorSubscriberBase, IHostedService
{
    private readonly ApiController _apiController; // for sending the updates.
    private readonly PlayerCharacterData _playerManager; // has our global permissions.
    private readonly PairManager _pairManager; // for accessing the permissions of each pair.
    private readonly ClientConfigurationManager _clientConfigs;
    private readonly GagManager _gagManager; // for removing gags.
    private readonly PlaybackService _patternPlaybackService; // for stopping patterns.
    private readonly WardrobeHandler _wardrobeHandler;
    private readonly IpcFastUpdates _glamourFastEvent; // for reverting character.

    public SafewordService(ILogger<SafewordService> logger, GagspeakMediator mediator,
        ApiController apiController, PlayerCharacterData playerManager,
        PairManager pairManager, ClientConfigurationManager clientConfigs,
        GagManager gagManager, PlaybackService playbackService,
        WardrobeHandler wardrobeHandler, IpcFastUpdates glamourFastUpdate)
        : base(logger, mediator)
    {
        _apiController = apiController;
        _playerManager = playerManager;
        _pairManager = pairManager;
        _clientConfigs = clientConfigs;
        _gagManager = gagManager;
        _patternPlaybackService = playbackService;
        _wardrobeHandler = wardrobeHandler;
        _glamourFastEvent = glamourFastUpdate;

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

    private async void SafewordUsed()
    {
        try
        {
            // return if it has not yet been 5 minutes since the last use.
            if (SafewordIsUsed)
            {
                Logger.LogWarning("Hardcore Safeword was used too soon after the last use. Must wait 5 minutes.", LoggerType.Safeword);
                return;
            }

            // set the time of the last safeword used.
            TimeOfLastSafewordUsed = DateTime.Now;
            Logger.LogInformation("Safeword was used.", LoggerType.Safeword);

            // disable any active gags and push these updates to the API.
            Logger.LogInformation("Disabling any active gags.", LoggerType.Safeword);
            _gagManager.SafewordWasUsed();
            Logger.LogInformation("Active gags disabled.", LoggerType.Safeword);

            // disable any active restraints.
            Logger.LogInformation("Disabling all stored data and reverting character.", LoggerType.Safeword);

            // grab active pattern first if any.
            if (_patternPlaybackService.ActivePattern != null)
            {
                Logger.LogInformation("Stopping active pattern.", LoggerType.Safeword);
                _patternPlaybackService.StopPattern(_patternPlaybackService.ActivePattern.UniqueIdentifier, false);
                UnlocksEventManager.AchievementEvent(UnlocksEvent.PatternAction, PatternInteractionKind.Stopped, _patternPlaybackService.ActivePattern.UniqueIdentifier, false);
                Logger.LogInformation("Active pattern stopped.", LoggerType.Safeword);
            }

            // disable all other active things.
            await _clientConfigs.DisableEverythingDueToSafeword();
            _wardrobeHandler.UpdateActiveSet();

            // doesn't madder if we do direct updates, since after the push to the server the callback will set it back accordingly.
            if (_playerManager.GlobalPerms != null && _apiController.IsConnected)
            {
                _playerManager.GlobalPerms.SafewordUsed = true;
                _playerManager.GlobalPerms.LiveChatGarblerActive = false;
                _playerManager.GlobalPerms.LiveChatGarblerLocked = false;
                _playerManager.GlobalPerms.WardrobeEnabled = false;
                _playerManager.GlobalPerms.ItemAutoEquip = false;
                _playerManager.GlobalPerms.RestraintSetAutoEquip = false;
                _playerManager.GlobalPerms.PuppeteerEnabled = false;
                _playerManager.GlobalPerms.MoodlesEnabled = false;
                _playerManager.GlobalPerms.ToyboxEnabled = false;
                _playerManager.GlobalPerms.LockToyboxUI = false;
                _playerManager.GlobalPerms.ToyIntensity = 0;
                _playerManager.GlobalPerms.SpatialVibratorAudio = false;

                Logger.LogInformation("Pushing Global updates to the server.", LoggerType.Safeword);
                _ = _apiController.UserPushAllGlobalPerms(new(ApiController.PlayerUserData, _playerManager.GlobalPerms));
                Logger.LogInformation("Global updates pushed to the server.", LoggerType.Safeword);
            }
            Logger.LogInformation("Everything Disabled.", LoggerType.Safeword);

            // reverting character.
            IpcFastUpdates.InvokeGlamourer(GlamourUpdateType.Safeword);

            Logger.LogInformation("Character reverted.", LoggerType.Safeword);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "An error occurred while trying to process the safeword command.");
        }

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
        Logger.LogInformation("Hardcore Safeword was used.", LoggerType.Safeword);

        // push the permission update for the hardcore safeword to the server.
        UserGlobalPermissions newGlobalPerms = _playerManager.GlobalPerms ?? new UserGlobalPermissions();
        newGlobalPerms.HardcoreSafewordUsed = true;

        _playerManager.GlobalPerms = newGlobalPerms;

        if (ApiController.ServerState is ServerState.Connected)
        {
            _ = _apiController.UserUpdateOwnGlobalPerm(new(ApiController.PlayerUserData, new KeyValuePair<string, object>("HardcoreSafewordUsed", true)));
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
                if (ApiController.ServerState is ServerState.Connected)
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
