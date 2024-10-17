using Dalamud.Game.ClientState.Objects;
using GagSpeak.GagspeakConfiguration;
using GagSpeak.GagspeakConfiguration.Models;
using GagSpeak.Hardcore.ForcedStay;
using GagSpeak.Hardcore.Movement;
using GagSpeak.PlayerData.Pairs;
using GagSpeak.PlayerData.Services;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using GagSpeak.UI;
using GagSpeak.UpdateMonitoring.Chat;
using GagSpeak.WebAPI;
using System.Numerics;

namespace GagSpeak.PlayerData.Handlers;
/// <summary> Responsible for handling hardcore communication from stored data & ui to core logic. </summary>
public class HardcoreHandler : DisposableMediatorSubscriberBase
{
    private readonly ClientConfigurationManager _clientConfigs;
    private readonly PairManager _pairManager;
    private readonly ApiController _apiController; // for sending the updates.
    private readonly ITargetManager _targetManager; // for targetting pair on follows.

    public unsafe GameCameraManager* cameraManager = GameCameraManager.Instance(); // for the camera manager object
    public HardcoreHandler(ILogger<HardcoreHandler> logger, GagspeakMediator mediator,
        ClientConfigurationManager clientConfigs, PairManager pairManager, 
        ApiController apiController, ITargetManager targetManager) : base(logger, mediator)
    {
        _clientConfigs = clientConfigs;
        _pairManager = pairManager;
        _apiController = apiController;
        _targetManager = targetManager;

        Mediator.Subscribe<HardcoreActionMessage>(this, (msg) =>
        {
            switch (msg.type)
            {
                case HardcoreActionType.ForcedFollow: SetForcedFollow(msg.State, msg.Pair); break;
                case HardcoreActionType.ForcedSit: SetForcedSitState(msg.State, false, msg.Pair); break;
                case HardcoreActionType.ForcedGroundSit: SetForcedSitState(msg.State, true, msg.Pair); break;
                case HardcoreActionType.ForcedStay: SetForcedStayState(msg.State, msg.Pair); break;
                case HardcoreActionType.ForcedBlindfold: SetBlindfoldState(msg.State, msg.Pair); break;
            }
        });

        Mediator.Subscribe<SafewordHardcoreUsedMessage>(this, _ => OnSafewordUsed().ConfigureAwait(false));
    }

    public bool IsForcedToFollow { get; set; } = false;
    public bool IsForcedToSit { get; set; } = false;
    public bool IsForcedToStay { get; set; } = false;
    public bool IsBlindfolded { get; set; } = false;
    public Pair? ForceFollowedPair { get; set; } = null;
    public Pair? ForceSitPair { get; set; } = null;
    public Pair? ForceStayPair { get; set; } = null;
    public Pair? BlindfoldPair { get; set; } = null;

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
        await Task.Delay(3000);

        if (IsForcedToFollow && ForceFollowedPair is not null)
            SetForcedFollow(NewState.Disabled, ForceFollowedPair);
        if (IsForcedToSit && ForceSitPair is not null)
            SetForcedSitState(NewState.Disabled, false, ForceSitPair);
        if (IsForcedToStay && ForceStayPair is not null)
            SetForcedStayState(NewState.Disabled, ForceStayPair);
        if (IsBlindfolded && BlindfoldPair is not null)
            SetBlindfoldState(NewState.Disabled, BlindfoldPair);
    }

    // when called for an enable, it will already be set to enabled, so we just need to update the state.
    // for disable, it can be auto, so we have to call it.
    public void SetForcedFollow(NewState newState, Pair pairToFollow)
    {
        // Get current forced follow state.
        if(newState is NewState.Enabled)
        {
            LastMovementTime = DateTimeOffset.UtcNow;
            IsForcedToFollow = true;
            ForceFollowedPair = pairToFollow;
            HandleForcedFollow(true, pairToFollow);
            Logger.LogDebug("Enabled forced follow for pair.", LoggerType.HardcoreMovement);
            return;
        }

        if (newState is NewState.Disabled)
        {
            IsForcedToFollow = false;
            ForceFollowedPair = null;
            // if we have not yet been requested by that pair to stop following, this was triggered manually by our idle timer.
            if (pairToFollow.UserPairOwnUniquePairPerms.IsForcedToFollow is true)
            {
                Logger.LogInformation("ForceFollow Disable was triggered manually before it naturally disabled. Forcibly shutting down.");
                _ = _apiController.UserUpdateOwnPairPerm(new(pairToFollow.UserData, new KeyValuePair<string, object>("IsForcedToFollow", false)));
                HandleForcedFollow(false, pairToFollow);
                Logger.LogDebug("Disabled forced follow for pair", LoggerType.HardcoreMovement);
                return;
            }
            else
            {
                // they disabled it, so just handle turning it off.
                if (pairToFollow.UserPairOwnUniquePairPerms.IsForcedToFollow is false)
                {
                    Logger.LogDebug("Disabled forced follow for pair", LoggerType.HardcoreMovement);
                    HandleForcedFollow(false, pairToFollow);
                    return;
                }
            }
        }
    }

    public void SetForcedSitState(NewState newState, bool isGroundsit, Pair pairToSitFor)
    {
        if (newState is NewState.Enabled)
        {
            IsForcedToSit = true;
            ForceSitPair = pairToSitFor;

            if (isGroundsit)
            {
                ChatBoxMessage.EnqueueMessage("/groundsit");
                Logger.LogDebug("Enabled forced kneeling for pair.", LoggerType.HardcoreMovement);

            }
            else
            {
                ChatBoxMessage.EnqueueMessage("/sit");
                Logger.LogDebug("Enabled forced sitting for pair.", LoggerType.HardcoreMovement);

            }
            return;
        }

        if (newState is NewState.Disabled)
        {
            IsForcedToSit = false;
            ForceSitPair = null;
            Logger.LogDebug("Pair has allowed you to stand again.", LoggerType.HardcoreMovement);
        }
    }

    public void SetForcedStayState(NewState newState, Pair pairToStayFor)
    {
        if (newState is NewState.Enabled)
        {
            IsForcedToStay = true;
            ForceStayPair = pairToStayFor;
            Logger.LogDebug("Enabled forced stay for pair.", LoggerType.HardcoreMovement);
            return;
        }

        if (newState is NewState.Disabled)
        {
            IsForcedToStay = false;
            ForceStayPair = null;
            Logger.LogDebug("Disabled forced stay for pair.", LoggerType.HardcoreMovement);
            return;
        }
    }

    private async void SetBlindfoldState(NewState newState, Pair pairBlindfolding)
    {
        if (newState is NewState.Enabled && !BlindfoldUI.IsWindowOpen)
        {
            IsBlindfolded = true;
            BlindfoldPair = pairBlindfolding;
            Logger.LogDebug("Enabled Forced Blindfold for pair.", LoggerType.HardcoreActions);
            await HandleBlindfoldLogic(NewState.Enabled, pairBlindfolding.UserData.UID);
            return;
        }

        if (newState is NewState.Disabled && BlindfoldUI.IsWindowOpen)
        {
            IsBlindfolded = false;
            BlindfoldPair = null;
            Logger.LogDebug("Disabled Forced Blindfold for pair.", LoggerType.HardcoreMovement);
            await HandleBlindfoldLogic(NewState.Disabled, pairBlindfolding.UserData.UID);
            return;
        }
    }

    // handles the forced follow logic.
    public void HandleForcedFollow(bool newState, Pair pairUserData)
    {
        if (newState is true)
        {
            if (pairUserData is null) { Logger.LogError("Somehow you still haven't set the forcedToFollowPair???"); return; }
            // target our pair and follow them.
            _targetManager.Target = pairUserData.VisiblePairGameObject;
            ChatBoxMessage.EnqueueMessage("/follow <t>");
        }

        // toggle movement type to legacy if we are not on legacy
        if (!_clientConfigs.GagspeakConfig.UsingLegacyControls)
        {
            // if forced follow is still on, dont switch it back to false
            uint mode = newState ? (uint)MovementMode.Legacy : (uint)MovementMode.Standard;
            GameConfig.UiControl.Set("MoveMode", mode);
        }
    }

    public async Task HandleBlindfoldLogic(NewState newState, string applierUID)
    {
        // toggle our window based on conditions
        if (newState is NewState.Enabled)
        {
            IpcFastUpdates.InvokeGlamourer(GlamourUpdateType.RefreshAll);

            // if the window isnt open, open it.
            if (!BlindfoldUI.IsWindowOpen)
            {
                Mediator.Publish(new UiToggleMessage(typeof(BlindfoldUI), ToggleType.Show));
            }
            // go in for camera voodoo.
            DoCameraVoodoo(newState);
            // log success.
            Logger.LogDebug("Applying Blindfold to Character");
        }
        else
        {
            if (BlindfoldUI.IsWindowOpen)
            {
                //if the window is open, close it.
                Mediator.Publish(new HardcoreRemoveBlindfoldMessage());
            }
            // wait a bit before doing the camera voodoo
            await Task.Delay(2000);
            DoCameraVoodoo(newState);
            // call a refresh all
            IpcFastUpdates.InvokeGlamourer(GlamourUpdateType.RefreshAll);
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

        switch (stimulationLvl)
        {
            case StimulationLevel.None:
                Logger.LogDebug("No Stimulation Multiplier applied from set, defaulting to 1.0x!", LoggerType.HardcoreActions);
                StimulationMultiplier = 1.0;
                break;
            case StimulationLevel.Light:
                Logger.LogDebug("Light Stimulation Multiplier applied from set with factor of 1.125x!", LoggerType.HardcoreActions);
                StimulationMultiplier = 1.125;
                break;
            case StimulationLevel.Mild:
                Logger.LogDebug("Mild Stimulation Multiplier applied from set with factor of 1.25x!", LoggerType.HardcoreActions);
                StimulationMultiplier = 1.25;
                break;
            case StimulationLevel.Heavy:
                Logger.LogDebug("Heavy Stimulation Multiplier applied from set with factor of 1.5x!", LoggerType.HardcoreActions);
                StimulationMultiplier = 1.5;
                break;
        }
    }
}
