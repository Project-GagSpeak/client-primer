using Dalamud.Game.ClientState.Objects;
using GagSpeak.GagspeakConfiguration;
using GagSpeak.GagspeakConfiguration.Models;
using GagSpeak.Hardcore.ForcedStay;
using GagSpeak.Hardcore.Movement;
using GagSpeak.PlayerData.Data;
using GagSpeak.PlayerData.Pairs;
using GagSpeak.PlayerData.Services;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using GagSpeak.UI;
using GagSpeak.UpdateMonitoring.Chat;
using GagSpeak.WebAPI;
using GagspeakAPI.Extensions;
using System.Numerics;
using static FFXIVClientStructs.FFXIV.Component.GUI.AtkResNode.Delegates;

namespace GagSpeak.PlayerData.Handlers;
/// <summary> Responsible for handling hardcore communication from stored data & ui to core logic. </summary>
public class HardcoreHandler : DisposableMediatorSubscriberBase
{
    private readonly ClientConfigurationManager _clientConfigs;
    private readonly PlayerCharacterData _playerData;
    private readonly PairManager _pairManager;
    private readonly ApiController _apiController; // for sending the updates.
    private readonly ITargetManager _targetManager; // for targetting pair on follows.

    public unsafe GameCameraManager* cameraManager = GameCameraManager.Instance(); // for the camera manager object
    public HardcoreHandler(ILogger<HardcoreHandler> logger, GagspeakMediator mediator,
        ClientConfigurationManager clientConfigs, PlayerCharacterData playerData,
        PairManager pairManager, ApiController apiController, ITargetManager targetManager) : base(logger, mediator)
    {
        _clientConfigs = clientConfigs;
        _playerData = playerData;
        _pairManager = pairManager;
        _apiController = apiController;
        _targetManager = targetManager;

        Mediator.Subscribe<HardcoreActionMessage>(this, (msg) =>
        {
            switch (msg.type)
            {
                case HardcoreAction.ForcedFollow: UpdateForcedFollow(msg.State); break;
                case HardcoreAction.ForcedSit: UpdateForcedSitState(msg.State, false); break;
                case HardcoreAction.ForcedGroundsit: UpdateForcedSitState(msg.State, true); break;
                case HardcoreAction.ForcedStay: UpdateForcedStayState(msg.State); break;
                case HardcoreAction.ForcedBlindfold: UpdateBlindfoldState(msg.State); break;
                case HardcoreAction.ChatboxHiding: UpdateChatboxVisibility(msg.State); break;
                case HardcoreAction.ChatInputHiding: UpdateChatInputVisibility(msg.State); break;
                case HardcoreAction.ChatInputBlocking: UpdateChatInputBlocking(msg.State); break;
            }
        });

        Mediator.Subscribe<SafewordHardcoreUsedMessage>(this, _ => OnSafewordUsed().ConfigureAwait(false));
    }

    public bool IsForcedToFollow => _playerData.GlobalPerms?.IsFollowing() ?? false;
    public bool IsForcedToSit => _playerData.GlobalPerms?.IsSitting() ?? false;
    public bool IsForcedToStay => _playerData.GlobalPerms?.IsStaying() ?? false;
    public bool IsBlindfolded => _playerData.GlobalPerms?.IsBlindfolded() ?? false;
    public bool IsHidingChat => _playerData.GlobalPerms?.IsChatHidden() ?? false;
    public bool IsHidingChatInput => _playerData.GlobalPerms?.IsChatInputHidden() ?? false;
    public bool IsBlockingChatInput => _playerData.GlobalPerms?.IsChatInputBlocked() ?? false;

    public bool MonitorFollowLogic => IsForcedToFollow;
    public bool MonitorSitLogic => IsForcedToSit;
    public bool MonitorStayLogic => IsForcedToStay;
    public bool MonitorBlindfoldLogic => IsBlindfolded;
    public DateTimeOffset LastMovementTime { get; set; } = DateTimeOffset.Now;
    public Vector3 LastPosition { get; set; } = Vector3.Zero;
    public double StimulationMultiplier { get; set; } = 1.0;

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!_clientConfigs.GagspeakConfig.UsingLegacyControls && GameConfig.UiControl.GetBool("MoveMode"))
            GameConfig.UiControl.Set("MoveMode", (int)MovementMode.Standard);
    }

    private async Task OnSafewordUsed()
    {
        Logger.LogInformation("Turning all Hardcore Functionality in 3 seconds.", LoggerType.Safeword);
        await Task.Delay(2000);
        UpdateForcedFollow(NewState.Disabled);
        UpdateForcedSitState(NewState.Disabled, false);
        UpdateForcedStayState(NewState.Disabled);
        UpdateBlindfoldState(NewState.Disabled);
        UpdateChatboxVisibility(NewState.Disabled);
        UpdateChatInputVisibility(NewState.Disabled);
        UpdateChatInputBlocking(NewState.Disabled);
    }

    public void UpdateForcedFollow(NewState newState)
    {
        // toggle movement type to legacy if we are not on legacy (regardless of it being enable or disable.
        if (!_clientConfigs.GagspeakConfig.UsingLegacyControls)
        {
            // if forced follow is still on, dont switch it back to false
            uint mode = newState switch
            {
                NewState.Enabled => (uint)MovementMode.Legacy,
                NewState.Disabled => (uint)MovementMode.Standard,
                _ => (uint)MovementMode.Standard
            };
            GameConfig.UiControl.Set("MoveMode", mode);
        }

        // if we are enabling, adjust the lastMovementTime to now.
        if (newState is NewState.Enabled)
        {
            LastMovementTime = DateTimeOffset.UtcNow;
            // grab the pair from the pair manager to obtain its game object and begin following it.
            var pairToFollow = _pairManager.DirectPairs.FirstOrDefault(pair => pair.UserData.UID == _playerData.GlobalPerms?.FollowUID());
            if (pairToFollow is null)
            {
                Logger.LogWarning("Ordered to follow but the pair who did it is not visible or targetable.");
                return;
            }
            // Begin Following if we should.
            if (pairToFollow.VisiblePairGameObject?.IsTargetable ?? false)
            {
                _targetManager.Target = pairToFollow.VisiblePairGameObject;
                ChatBoxMessage.EnqueueMessage("/follow <t>");
                Logger.LogDebug("Enabled forced follow for pair.", LoggerType.HardcoreMovement);
            }
        }

        if (newState is NewState.Disabled)
        {
            // If we are still following someone when this triggers it means we were idle long enough for it to disable.
            if (_playerData.GlobalPerms?.IsFollowing() ?? false)
            {
                // set the client side first before the push to prevent infinite loops while disconnected
                _playerData.GlobalPerms.ForcedFollow = string.Empty;
                Logger.LogInformation("ForceFollow Disable was triggered manually before it naturally disabled. Forcibly shutting down.");
                _ = _apiController.UserUpdateOwnGlobalPerm(new(new(ApiController.UID), new KeyValuePair<string, object>("ForcedFollow", string.Empty)));
            }
            else
            {
                Logger.LogInformation("Disabled forced follow for pair.", LoggerType.HardcoreMovement);
            }
        }
    }

    public void UpdateForcedSitState(NewState newState, bool isGroundsit)
    {
        if (newState is NewState.Enabled)
        {
            Logger.LogDebug("Enabled forced " + (isGroundsit ? "groundsit" : "sit") + " for pair.", LoggerType.HardcoreMovement);
            ChatBoxMessage.EnqueueMessage(isGroundsit ? "/groundsit" : "/sit");
        }

        if (newState is NewState.Disabled)
            Logger.LogDebug("Pair has allowed you to stand again.", LoggerType.HardcoreMovement);
    }

    public void UpdateForcedStayState(NewState newState)
    {
        Logger.LogDebug(newState is NewState.Enabled
            ? "Enabled forced stay for pair." : "Disabled forced stay for pair", LoggerType.HardcoreMovement);
    }

    private void UpdateBlindfoldState(NewState newState)
    {
        Logger.LogDebug(newState is NewState.Enabled
            ? "Enabled forced blindfold for pair." : "Disabled forced blindfold for pair.", LoggerType.HardcoreMovement);
        // Call a glamour refresh
        IpcFastUpdates.InvokeGlamourer(GlamourUpdateType.RefreshAll);

        if (newState is NewState.Enabled && !BlindfoldUI.IsWindowOpen)
        {
            Task.Run(() => HandleBlindfoldLogic(newState)); // Fire and Forget
            return;
        }

        if (newState is NewState.Disabled && BlindfoldUI.IsWindowOpen)
        {
            Task.Run(() => HandleBlindfoldLogic(newState)); // Fire and Forget
            return;
        }
    }

    public void UpdateChatboxVisibility(NewState newState)
    {
        Logger.LogDebug(newState is NewState.Enabled ? "Enabled " : "Disabled "
            + "Chatbox Visibility", LoggerType.HardcoreActions);
        // set new visibility state
        var visibility = newState is NewState.Enabled ? false : true;
        ChatLogAddonHelper.SetChatLogPanelsVisibility(visibility);
    }

    public void UpdateChatInputVisibility(NewState newState)
    {
        Logger.LogDebug(newState is NewState.Enabled ? "Enabled " : "Disabled "
            + "Chat Input Visibility", LoggerType.HardcoreActions);
        // set new visibility state
        var visibility = newState is NewState.Enabled ? false : true;
        ChatLogAddonHelper.SetMainChatLogVisibility(visibility);
    }

    public void UpdateChatInputBlocking(NewState newState)
    {
        // No logic handled here. Instead it is handled in the framework updater.
        Logger.LogDebug(newState is NewState.Enabled ? "Enabled " : "Disabled " + "Chat Input Blocking", LoggerType.HardcoreActions);
    }

    public async Task HandleBlindfoldLogic(NewState newState)
    {
        // toggle our window based on conditions
        if (newState is NewState.Enabled)
        {
            // if the window isnt open, open it.
            if (!BlindfoldUI.IsWindowOpen)
                Mediator.Publish(new UiToggleMessage(typeof(BlindfoldUI), ToggleType.Show));
            // go in for camera voodoo.
            DoCameraVoodoo(newState);
        }
        else
        {
            if (BlindfoldUI.IsWindowOpen)
                Mediator.Publish(new HardcoreRemoveBlindfoldMessage());
            // wait a bit before doing the camera voodoo
            await Task.Delay(2000);
            DoCameraVoodoo(newState);
        }
    }

    private unsafe void DoCameraVoodoo(NewState newValue)
    {
        // force the camera to first person, but dont loop the force
        if (newValue is NewState.Enabled)
        {
            if (cameraManager is not null && cameraManager->Camera is not null && cameraManager->Camera->Mode is not (int)CameraControlMode.FirstPerson)
                cameraManager->Camera->Mode = (int)CameraControlMode.FirstPerson;
        }
        else
        {
            if (cameraManager is not null && cameraManager->Camera is not null && cameraManager->Camera->Mode is (int)CameraControlMode.FirstPerson)
                cameraManager->Camera->Mode = (int)CameraControlMode.ThirdPerson;
        }
    }

    public void ApplyMultiplier()
    {
        var activeSet = _clientConfigs.GetActiveSet();
        if (activeSet is null)
            return;

        var stimulationLvl = activeSet.SetProperties[activeSet.EnabledBy].StimulationLevel;
        StimulationMultiplier = stimulationLvl switch
        {
            StimulationLevel.None => 1.0,
            StimulationLevel.Light => 1.125,
            StimulationLevel.Mild => 1.25,
            StimulationLevel.Heavy => 1.5,
            _ => 1.0
        };
        Logger.LogDebug(stimulationLvl switch
        {
            StimulationLevel.None => "No Stimulation Multiplier applied from set, defaulting to 1.0x!",
            StimulationLevel.Light => "Light Stimulation Multiplier applied from set with factor of 1.125x!",
            StimulationLevel.Mild => "Mild Stimulation Multiplier applied from set with factor of 1.25x!",
            StimulationLevel.Heavy => "Heavy Stimulation Multiplier applied from set with factor of 1.5x!",
            _ => "No Stimulation Multiplier applied from set, defaulting to 1.0x!"
        }, LoggerType.HardcoreActions);
    }
}
