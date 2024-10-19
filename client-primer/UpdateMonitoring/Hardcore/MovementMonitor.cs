using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Common.Lua;
using GagSpeak.GagspeakConfiguration.Models;
using GagSpeak.Hardcore.ForcedStay;
using GagSpeak.Hardcore.Movement;
using GagSpeak.PlayerData.Handlers;
using GagSpeak.PlayerData.Services;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using GagSpeak.Utils;
using GagSpeak.WebAPI;
using Microsoft.Extensions.Logging;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using XivControl = FFXIVClientStructs.FFXIV.Client.Game.Control;

namespace GagSpeak.UpdateMonitoring;
public class MovementMonitor : DisposableMediatorSubscriberBase
{
    private readonly ClientConfigurationManager _clientConfigs;
    private readonly HardcoreHandler _handler;
    private readonly WardrobeHandler _outfitHandler;
    private readonly SelectStringPrompt _promptsString;
    private readonly YesNoPrompt _promptsYesNo;
    private readonly RoomSelectPrompt _promptsRooms;
    private readonly OnFrameworkService _frameworkUtils;
    private readonly MoveController _MoveController;
    private readonly ICondition _condition;
    private readonly IClientState _clientState;
    private readonly IKeyState _keyState;
    private readonly IObjectTable _objectTable;
    private readonly ITargetManager _targetManager;

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
        ClientConfigurationManager clientConfigs, SelectStringPrompt stringPrompts,
        YesNoPrompt yesNoPrompts, RoomSelectPrompt rooms, OnFrameworkService frameworkUtils,
        MoveController moveController, ICondition condition, IClientState clientState,
        IKeyState keyState, IObjectTable objectTable, ITargetManager targetManager) : base(logger, mediator)
    {
        _clientConfigs = clientConfigs;
        _handler = hardcoreHandler;
        _outfitHandler = outfitHandler;
        _promptsString = stringPrompts;
        _promptsYesNo = yesNoPrompts;
        _promptsRooms = rooms;
        _frameworkUtils = frameworkUtils;
        _MoveController = moveController;
        _condition = condition;
        _clientState = clientState;
        _keyState = keyState;
        _objectTable = objectTable;
        _targetManager = targetManager;

        // attempt to set the value safely
        GenericHelpers.Safe(delegate
        {
            getRefValue = (GetRefValue)Delegate.CreateDelegate(typeof(GetRefValue), _keyState,
                            _keyState.GetType().GetMethod("GetRefValue", BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[] { typeof(int) }, null)!);
        });

        // try and see if we can remove this????
        Mediator.Subscribe<SafewordHardcoreUsedMessage>(this, _ => SafewordUsed());

        Mediator.Subscribe<FrameworkUpdateMessage>(this, _ => FrameworkUpdate());

        IpcFastUpdates.HardcoreTraitsEventFired += ToggleHardcoreTraits;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        // enable movement
        HandleMovementRelease();
        ResetCancelledMoveKeys();

        IpcFastUpdates.HardcoreTraitsEventFired -= ToggleHardcoreTraits;
    }

    public async void SafewordUsed()
    {
        // Wait 3 seconds to let everything else from the safeword process first.
        Logger.LogDebug("Safeword has been used, re-enabling movement in 3 seconds");
        await Task.Delay(3000);
        // Fix walking state
        HandleMovementRelease();
        ResetCancelledMoveKeys();
        HandleImmobilize = false;
    }

    public void ToggleHardcoreTraits(NewState newState, RestraintSet restraintSet)
    {
        if (restraintSet.EnabledBy is Globals.SelfApplied)
            return;

        // Grab properties.
        var properties = restraintSet.SetProperties[restraintSet.EnabledBy];
        // if the set has a weighty property, we need to disable the walking state
        if (properties.Weighty)
            HandleWeighty = newState is NewState.Enabled ? true : false;

        if(properties.Immobile)
            HandleImmobilize = newState is NewState.Enabled ? true : false;
    }

    /// <summary>
    /// A Static Accessor to be handled by the AppearanceHandler to know if we should handle immobilization or not.
    /// </summary>
    private bool HandleImmobilize = false;
    private bool HandleWeighty = false;

    #region Framework Updates
    private unsafe void FrameworkUpdate()
    {
        // make sure we only do checks when we are properly logged in and have a character loaded
        if (_clientState.LocalPlayer is null || _clientState.LocalPlayer.IsDead)
            return;

        // If we are immobile, forced to follow, or forced to sit, prevent movement
        //Logger.LogDebug("Movement Monitor Update: " + _handler.MonitorSitLogic + " " + _handler.MonitorFollowLogic + " " + HandleImmobilize, LoggerType.HardcoreMovement);
        //Logger.LogDebug("AllMovementForceDisabled:" + _MoveController.AllMovementForceDisabled + " Value: "+ _MoveController.ForceDisableMovement);
        
        if (_handler.MonitorFollowLogic || _handler.MonitorSitLogic || HandleImmobilize)
        {
            HandleMovementPrevention();
        }
        else
        {
            HandleMovementRelease();
            ResetCancelledMoveKeys();
        }

        // Handle forced Walk
        if(HandleWeighty || _handler.MonitorFollowLogic)
        {
            // get the byte that sees if the player is walking
            uint isWalking = Marshal.ReadByte((nint)gameControl, 24131);
            // and if they are not, force it.
            if (isWalking is 0)
                Marshal.WriteByte((nint)gameControl, 24131, 0x1);
        }

        // if player is in forced follow state, we need to track their position so we can auto turn it off if they are standing still for 6 seconds
        if (_handler.MonitorFollowLogic)
        {
            // if this value is 1, it means the player is moving, so we should reset the idle timer and update position.
            if (_clientState.LocalPlayer!.Position != _handler.LastPosition)
            {
                _handler.LastMovementTime = DateTimeOffset.Now;           // reset timer
                _handler.LastPosition = _clientState.LocalPlayer.Position;// update last position
            }

            // if we have been idle for longer than 6 seconds, we should release the player.
            if ((DateTimeOffset.UtcNow - _handler.LastMovementTime).TotalSeconds > 6)
            {
                // set the forced follow to false if we are still being forced to follow.
                _handler.UpdateForcedFollow(NewState.Disabled);
                Logger.LogDebug("Player has been standing still for longer than 6 seconds. Allowing them to move again", LoggerType.HardcoreMovement);
            }
        }

        if (_handler.MonitorStayLogic)
        {
            // enable the hooks for the option prompts
            if (!_promptsString.Enabled) _promptsString.Enable();
            if (!_promptsYesNo.Enabled) _promptsYesNo.Enable();
            if (!_promptsRooms.Enabled) _promptsRooms.Enable();

            // while they are active, if we are not in a dialog prompt option, scan to see if we are by an estate entrance
            if (!_condition[ConditionFlag.OccupiedInQuestEvent] && !_frameworkUtils._sentBetweenAreas)
            {
                // grab all the event object nodes (door interactions)
                var nodes = _objectTable.Where(x => x.ObjectKind == Dalamud.Game.ClientState.Objects.Enums.ObjectKind.EventObj && GetTargetDistance(x) < 2f).ToList();
                // Interact with the node Labeled "Entrance"
                foreach (var obj in nodes)
                    if (obj.Name.TextValue is "Entrance" or "Apartment Building Entrance" or "Entrance to Additional Chambers")
                    {
                        _targetManager.Target = obj;
                        TargetSystem.Instance()->InteractWithObject((GameObject*)obj.Address, false);
                    }
            }
        }
        else
        {
            if (_promptsString.Enabled) _promptsString.Disable();
            if (_promptsYesNo.Enabled) _promptsYesNo.Disable();
            if (_promptsRooms.Enabled) _promptsRooms.Disable();
        }

        // I'm aware the if statements are cancer, but its useful for the framework update to be as fast as possible.
        if (_handler.IsBlindfolded)
        {
            // if we are blindfolded and have forced first-person to true, force first person
            if (_clientConfigs.GagspeakConfig.ForceLockFirstPerson)
                if (cameraManager->Camera is not null && cameraManager->Camera->Mode is not (int)CameraControlMode.FirstPerson)
                    cameraManager->Camera->Mode = (int)CameraControlMode.FirstPerson;
        }
    }


    // Helper functions for minimizing the content in the framework update code section above
    public float GetTargetDistance(Dalamud.Game.ClientState.Objects.Types.IGameObject target)
    {
        Vector2 position = new(target.Position.X, target.Position.Z);
        Vector2 selfPosition = new(_clientState.LocalPlayer!.Position.X, _clientState.LocalPlayer.Position.Z);
        return Math.Max(0, Vector2.Distance(position, selfPosition) - target.HitboxRadius - _clientState.LocalPlayer.HitboxRadius);
    }

    // handle the prevention of our movement.
    private void HandleMovementPrevention()
    {
        //Logger.LogDebug("Both Mouse Buttons Pressed?:" + MoveController.IsBothMouseButtonsPressed());
        if (_handler.MonitorSitLogic)
        {
            // If we have not yet disabled Movement, we should do so.
            if(!_MoveController.AllMovementForceDisabled)
            {
                Logger.LogDebug("Disabling All Movement due to ForcedSit", LoggerType.HardcoreMovement);
                _MoveController.ForceDisableMovement++;
            }
        }

        // if we are being forced to follow, we should toggle the unfollow hook
        if (_handler.MonitorFollowLogic && MoveController.UnfollowHook is not null && MoveController.MovementUpdateHook is not null)
        {
            // in this case, we want to make sure to block players keys and force them to legacy mode.
            if (GameConfig.UiControl.GetBool("MoveMode") is false)
                GameConfig.UiControl.Set("MoveMode", (int)MovementMode.Legacy);

            // If the controllers UnFollow Hook is Disabled, Enable it.
            if(!MoveController.UnfollowHook.IsEnabled)
            {
                Logger.LogWarning("Enabling Unfollow Hook due to ForcedFollow ending", LoggerType.HardcoreMovement);
                MoveController.UnfollowHook.Enable();
            }
            if(!MoveController.MovementUpdateHook.IsEnabled)
            {
                Logger.LogWarning("Enabling Movement Update Hook due to ForcedFollow ending", LoggerType.HardcoreMovement);
                MoveController.MovementUpdateHook.Enable();
            }
        }

        // If we have been immobilized by our restraint set, we should block all movement when LMB+RMB.
        if (HandleImmobilize)
        {
            if(MoveController.IsBothMouseButtonsPressed())
            {
                if (!_MoveController.AllMovementForceDisabled)
                {
                    //Logger.LogDebug("Disabling All Movement due to LMB+RMB during Immobilize", LoggerType.HardcoreMovement);
                    _MoveController.ForceDisableMovement++;
                }
            }
            else
            {
                if(_MoveController.AllMovementForceDisabled)
                {
                    //Logger.LogDebug("Re-Enabling All Movement due to LMB+RMB during Immobilize ending", LoggerType.HardcoreMovement);
                    _MoveController.ForceDisableMovement--;
                }
            }
        }

        // cancel our set keys such as auto run ext, immobilization skips previous two and falls under this
        CancelMoveKeys();
    }

    private void HandleMovementRelease()
    {
        // if we are no longer being forced to sit.
        if (!_handler.MonitorSitLogic)
        {
            // If we have disabled Movement, we should re-enable it.
            if (_MoveController.AllMovementForceDisabled)
            {
                Logger.LogDebug("Re-Enabling All Movement due to ForcedSit ending", LoggerType.HardcoreMovement);
                _MoveController.ForceDisableMovement--;
            }
        }

        // if we are no longer being forced to follow.
        if (!_handler.MonitorFollowLogic && MoveController.UnfollowHook is not null && MoveController.MovementUpdateHook is not null)
        {
            // If the controllers UnFollow Hook is Enabled, Disable it.
            if (MoveController.UnfollowHook.IsEnabled)
            {
                Logger.LogWarning("Disabling Unfollow Hook due to ForcedFollow ending", LoggerType.HardcoreMovement);
                MoveController.UnfollowHook.Disable();
            }
            if (MoveController.MovementUpdateHook.IsEnabled)
            {
                Logger.LogWarning("Disabling Movement Update Hook due to ForcedFollow ending", LoggerType.HardcoreMovement);
                MoveController.MovementUpdateHook.Disable();
            }
        }

        // if we are no longer immobile, re-enable movement
        if (!HandleImmobilize)
        {
            if (_MoveController.AllMovementForceDisabled)
            {
                Logger.LogDebug("Re-Enabling All Movement due to LMB+RMB during Immobilize ending", LoggerType.HardcoreMovement);
                _MoveController.ForceDisableMovement--;
            }
        }
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
                //Logger.LogTrace("Cancelling key: " + x, LoggerType.HardcoreMovement);
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
                if (GenericHelpers.IsKeyPressed((int)(Keys)x))
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
