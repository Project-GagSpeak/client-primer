using Dalamud.Game.ClientState.Keys;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using GagSpeak.Hardcore;
using GagSpeak.Hardcore.Movement;
using GagSpeak.PlayerData.Data;
using GagSpeak.PlayerData.Handlers;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using GagSpeak.Utils;
using GagspeakAPI.Enums;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Condition = Dalamud.Game.ClientState.Conditions.ConditionFlag;
using XivControl = FFXIVClientStructs.FFXIV.Client.Game.Control;

namespace GagSpeak.UpdateMonitoring;
public class MovementMonitor : DisposableMediatorSubscriberBase
{
    private readonly ClientConfigurationManager _clientConfigs;
    private readonly HardcoreHandler _handler;
    private readonly WardrobeHandler _outfitHandler;
    private readonly OptionPromptListeners _autoDialogSelect;
    private readonly OnFrameworkService _frameworkUtils;
    private readonly MoveController _MoveController;
    private readonly ICondition _condition;
    private readonly IClientState _clientState;
    private readonly IKeyState _keyState;
    private readonly IObjectTable _objectTable;

    // for controlling walking speed, follow movement manager, and sitting/standing.
    public unsafe GameCameraManager* cameraManager = GameCameraManager.Instance(); // for the camera manager object
    public unsafe XivControl.Control* gameControl = XivControl.Control.Instance(); // instance to have control over our walking

    // get the keystate ref values
    delegate ref int GetRefValue(int vkCode);
    private static GetRefValue? getRefValue;
    private bool WasCancelled = false; // if true, we have cancelled any movement keys

    // the list of keys that are blocked while movement is disabled. Req. to be static, must be set here.
    public MovementMonitor(ILogger<MovementMonitor> logger, GagspeakMediator mediator,
        HardcoreHandler hardcoreHandler, WardrobeHandler outfitHandler,
        ClientConfigurationManager clientConfigs, OptionPromptListeners autoDialogSelect,
        OnFrameworkService frameworkUtils, MoveController moveController, ICondition condition,
        IClientState clientState, IKeyState keyState, IObjectTable objectTable) : base(logger, mediator)
    {
        _clientConfigs = clientConfigs;
        _handler = hardcoreHandler;
        _outfitHandler = outfitHandler;
        _autoDialogSelect = autoDialogSelect;
        _frameworkUtils = frameworkUtils;
        _MoveController = moveController;
        _condition = condition;
        _clientState = clientState;
        _keyState = keyState;
        _objectTable = objectTable;

        // attempt to set the value safely
        GenericHelpers.Safe(delegate
        {
            getRefValue = (GetRefValue)Delegate.CreateDelegate(typeof(GetRefValue), _keyState,
                            _keyState.GetType().GetMethod("GetRefValue", BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[] { typeof(int) }, null)!);
        });

        // run an async task that will await to apply affects until we are logged in, after which it will fire the restraint effect logic
        Task.Run(async () =>
        {
            if (!_frameworkUtils.IsLoggedIn)
            {
                Logger.LogDebug("Waiting for login to complete before activating restraint set", LoggerType.HardcoreMovement);
                while (!_clientState.IsLoggedIn || _clientState.LocalPlayer == null || _clientState.LocalPlayer.Address == nint.Zero && _clientState.LocalContentId == 0)
                {
                    await Task.Delay(2000); // Wait for 1 second before checking the login status again
                }
            }
            // There was logic here to reenable forced follow, but in reality, we should disable forced follow on dispose to prevent following a null target we cant escape.

            // if we are blindfolded by anyone, we should apply that as well
            if (_handler.IsCurrentlyBlindfolded() && _handler.BlindfoldPair != null)
            {
                await _handler.HandleBlindfoldLogic(NewState.Enabled, _handler.BlindfoldPair.UserData.UID);
            }
        });

        // subscribe to the mediator events
        Mediator.Subscribe<RestraintSetToggleHardcoreTraitsMessage>(this, (msg) =>
        {
            if (msg.State == NewState.Disabled && msg.AssignerUID != "SelfAssigned")
            {
                // might need to add back in another variable to pass through that references if it had weighty or not?
                Logger.LogDebug("Letting you run again", LoggerType.HardcoreMovement);
                Task.Delay(200);
                unsafe // temp fix to larger issue, if experiencing problems, refer to old code.
                {
                    Marshal.WriteByte((nint)gameControl, 24131, 0x0);
                }
            }
        });

        Mediator.Subscribe<MovementRestrictionChangedMessage>(this, (msg) =>
        {
            // if the new state type is not disabled, we do not care about it.
            if (msg.NewState != NewState.Disabled) return;

            // Movement type shouldnt madder here since its's handled by the framework right away afterward?

            // we don't need to worry about if ForcedSit or immobile is active here,
            // because it will be reactivated if it is turned off right away.
            Logger.LogDebug("ForcedFollow has been disabled, re-enabling movement", LoggerType.HardcoreMovement);
            _MoveController.CompletelyEnableMovement();
            ResetCancelledMoveKeys();
        });

        Mediator.Subscribe<FrameworkUpdateMessage>(this, (_) => FrameworkUpdate());
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        // enable movement
        _MoveController.CompletelyEnableMovement();
        ResetCancelledMoveKeys();
    }

    #region Framework Updates
    private unsafe void FrameworkUpdate()
    {
        // make sure we only do checks when we are properly logged in and have a character loaded
        if (_clientState.LocalPlayer?.IsDead ?? false || _frameworkUtils._sentBetweenAreas)
        {
            return;
        }

        // if we are able to update our hardcore effects
        if (AllowFrameworkHardcoreUpdates())
        {
            var immobile = isImmobile();
            if (_handler.IsForcedFollow || _handler.IsForcedSit || immobile)
            {
                HandleMovementPrevention(_handler.IsForcedFollow, _handler.IsForcedSit, immobile);
            }
            else
            {
                _MoveController.CompletelyEnableMovement();
                ResetCancelledMoveKeys();
            }

            // if any conditions that would affect your walking state are active, then force walking to occur
            HandleWalkingState();

            // if player is in forced follow state, we need to track their position so we can auto turn it off if they are standing still for 6 seconds
            if (_handler.IsForcedFollow)
            {
                // if the player is not moving...
                if (_clientState.LocalPlayer!.Position != _handler.LastPosition)
                {
                    _handler.LastMovementTime = DateTimeOffset.Now;           // reset timer
                    _handler.LastPosition = _clientState.LocalPlayer.Position;// update last position
                }
                // otherwise, they are not moving, so check if the timer has gone past 6000ms
                else
                {
                    if ((DateTimeOffset.Now - _handler.LastMovementTime).TotalMilliseconds > 6000)
                    {
                        // set the forced follow to false
                        _handler.SetForcedFollow(NewState.Disabled, _handler.ForceFollowedPair);
                        Logger.LogDebug("Player has been standing still for too long, forcing them to move again", LoggerType.HardcoreMovement);
                    }
                }
            }

            if (_handler.IsForcedStay)
            {
                // enable the hooks for the option prompts
                _autoDialogSelect.Enable();
                // while they are active, if we are not in a dialog prompt option, scan to see if we are by an estate entrance
                if (_handler.IsForcedStay && !_condition[Condition.OccupiedInQuestEvent] && !_frameworkUtils._sentBetweenAreas)
                {
                    // grab all the event object nodes (door interactions)
                    List<Dalamud.Game.ClientState.Objects.Types.IGameObject>? nodes =
                        _objectTable.Where(x => x.ObjectKind == Dalamud.Game.ClientState.Objects.Enums.ObjectKind.EventObj && GetTargetDistance(x) < 3.5f).ToList();

                    foreach (var obj in nodes)
                    {
                        // only follow through with the node named "Entrance"
                        if (obj.Name.TextValue == "Entrance")
                        {
                            TargetSystem.Instance()->InteractWithObject((GameObject*)obj.Address, false);
                        }
                    }
                }
            }
            else
            {
                _autoDialogSelect.Disable();
            }
        }

        // I'm aware the if statements are cancer, but its useful for the framework update to be as fast as possible.
        if (_handler.IsCurrentlyBlindfolded())
        {
            // if we are blindfolded and have forced first-person to true, force first person
            if (_handler.BlindfoldPair != null && _handler.BlindfoldPair.UserPair.OwnPairPerms.ForceLockFirstPerson)
            {
                if (cameraManager != null && cameraManager->Camera != null && cameraManager->Camera->Mode != (int)CameraControlMode.FirstPerson)
                {
                    // force first person
                    cameraManager->Camera->Mode = (int)CameraControlMode.FirstPerson;
                }
            }
        }
    }


    // Helper functions for minimizing the content in the framework update code section above
    public float GetTargetDistance(Dalamud.Game.ClientState.Objects.Types.IGameObject target)
    {
        Vector2 position = new(target.Position.X, target.Position.Z);
        Vector2 selfPosition = new(_clientState.LocalPlayer!.Position.X, _clientState.LocalPlayer.Position.Z);
        return Math.Max(0, Vector2.Distance(position, selfPosition) - target.HitboxRadius - _clientState.LocalPlayer.HitboxRadius);
    }

    public unsafe void TryInteract(GameObject* baseObj)
    {
        if (baseObj->GetIsTargetable()) TargetSystem.Instance()->InteractWithObject(baseObj, true);
    }

    private bool isImmobile() 
        => _outfitHandler.ActiveSet != null 
        && _outfitHandler.ActiveSet.SetProperties.ContainsKey(_outfitHandler.ActiveSet.EnabledBy)
        && _outfitHandler.ActiveSet.SetProperties[_outfitHandler.ActiveSet.EnabledBy].Immobile;

    private bool AllowFrameworkHardcoreUpdates()
    {
        return _clientState.IsLoggedIn && _clientState.LocalPlayer != null && _clientState.LocalPlayer.Address != nint.Zero
        && (_handler.IsForcedFollow || _handler.IsForcedSit || _handler.IsForcedStay || isImmobile());
    }

    // handles the walking state
    private unsafe void HandleWalkingState()
    {
        if (_handler.IsForcedFollow || (_outfitHandler.ActiveSet != null && _outfitHandler.ActiveSet.SetProperties[_outfitHandler.ActiveSet.EnabledBy].Weighty))
        {
            // get the byte that sees if the player is walking
            uint isWalking = Marshal.ReadByte((nint)gameControl, 24131);
            // and if they are not, force it.
            if (isWalking == 0)
            {
                Marshal.WriteByte((nint)gameControl, 24131, 0x1);
            }
        }
    }

    // handle the prevention of our movement.
    private void HandleMovementPrevention(bool following, bool sitting, bool immobile)
    {
        if (sitting)
        {
            _MoveController.CompletelyDisableMovement(true, true); // set pointer and turn off mouse and disable emotes
        }
        else if (immobile)
        {
            _MoveController.CompletelyDisableMovement(true, true); // set pointer but dont turn off mouse
        }
        // otherwise if we are forced to follow
        else if (following)
        {
            // in this case, we want to make sure to block players keys and force them to legacy mode.
            if (GameConfig.UiControl.GetBool("MoveMode") == false)
            {
                GameConfig.UiControl.Set("MoveMode", (int)MovementMode.Legacy);
            }
            // don't set pointer, but disable mouse
            _MoveController.CompletelyDisableMovement(false, true); // disable mouse
        }
        // otherwise, we should re-enable the mouse blocking and immobilization traits
        else
        {
            _MoveController.CompletelyEnableMovement(); // re-enable both
        }
        // cancel our set keys such as auto run ext, immobilization skips previous two and falls under this
        CancelMoveKeys();

    }

    private void CancelMoveKeys()
    {
        MoveKeys.Each(x =>
        {
            // the action to execute for each of our moved keys
            if (_keyState.GetRawValue(x) != 0)
            {
                // if the value is set to execute, cancel it.
                _keyState.SetRawValue(x, 0);
                // set was canceled to true
                WasCancelled = true;
                Logger.LogTrace("Cancelling key: "+x, LoggerType.HardcoreMovement);
            }
        });
    }

    private void ResetCancelledMoveKeys()
    {
        // if we had any keys canceled
        if (WasCancelled)
        {
            // set was cancelled back to false
            WasCancelled = false;
            // and restore the state of the virtual keys
            MoveKeys.Each(x =>
            {
                // the action to execute for each key
                if (GenericHelpers.IsKeyPressed((Keys)x))
                {
                    SetKeyState(x, 3);
                }
            });
        }
    }

    // set the key state (if you start crashing when using this you probably have a fucked up getrefvalue)
    private static void SetKeyState(VirtualKey key, int state) => getRefValue!((int)key) = state;

    public HashSet<VirtualKey> MoveKeys = new() {
        VirtualKey.W,
        VirtualKey.A,
        VirtualKey.S,
        VirtualKey.D,
        VirtualKey.SPACE,
    };
    #endregion Framework Updates
}
