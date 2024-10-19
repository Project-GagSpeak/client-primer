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
        ResetCancelledMoveKeys();

        IpcFastUpdates.HardcoreTraitsEventFired -= ToggleHardcoreTraits;
    }

    public async void SafewordUsed()
    {
        // Wait 3 seconds to let everything else from the safeword process first.
        Logger.LogDebug("Safeword has been used, re-enabling movement in 3 seconds");
        await Task.Delay(3000);
        // Fix walking state
        ResetCancelledMoveKeys();
        HandleImmobilize = false;
        HandleWeighty = false;
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
        {
            if (newState is NewState.Enabled)
            {
                Logger.LogDebug("Enabling Immobilization", LoggerType.HardcoreMovement);
                HandleImmobilize = true;
            }
            else
            {
                Logger.LogDebug("Disabling Immobilization", LoggerType.HardcoreMovement);
                HandleImmobilize = false;
                // Correct movement.
                _MoveController.DisableMovementLock();
            }
        }
    }

    /// <summary>
    /// A Static Accessor to be handled by the AppearanceHandler to know if we should handle immobilization or not.
    /// </summary>
    private bool HandleImmobilize = false;
    private bool HandleWeighty = false;

    #region Framework Updates
    /// <summary>
    /// Apologies in advance for the terrible overhead clutter in this framework update.
    /// Originally it was much cleaner, but due to other plugins such as Cammy and ECommons
    /// Interacting with similar pointers and signatures that I use in GagSpeak, I need to
    /// add checks to ensure proper synchronization to prevent using plugins in conjunction
    /// locking up your character.
    /// </summary>
    private unsafe void FrameworkUpdate()
    {
        // make sure we only do checks when we are properly logged in and have a character loaded
        if (_clientState.LocalPlayer is null || _clientState.LocalPlayer.IsDead)
            return;



        // FORCED FOLLOW LOGIC: Keep player following until idle for 6 seconds.
        if (_handler.MonitorFollowLogic)
        {
            // Ensure our movement and unfollow hooks are active.
            if (!GameConfig.UiControl.GetBool("MoveMode"))
                GameConfig.UiControl.Set("MoveMode", (int)MovementMode.Legacy);
            
            _MoveController.EnableUnfollowHook();

            // Check to see if the player is moving or not.
            if (_clientState.LocalPlayer.Position != _handler.LastPosition)
            {
                _handler.LastMovementTime = DateTimeOffset.Now;           // reset timer
                _handler.LastPosition = _clientState.LocalPlayer.Position;// update last position
            }

            // if we have been idle for longer than 6 seconds, we should release the player.
            if ((DateTimeOffset.UtcNow - _handler.LastMovementTime).TotalSeconds > 6)
                _handler.UpdateForcedFollow(NewState.Disabled);
        }



        // FORCED FOLLOW -- OR -- WEIGHTY RESTRAINT, Handle forced Walk
        if (_handler.MonitorFollowLogic || HandleWeighty)
        {
            // get the byte that sees if the player is walking
            uint isWalking = Marshal.ReadByte((nint)gameControl, 24131);
            // and if they are not, force it.
            if (isWalking is 0)
                Marshal.WriteByte((nint)gameControl, 24131, 0x1);
        }



        // FORCED SIT LOGIC Logic.
        if (_handler.MonitorSitLogic)
            _MoveController.EnableMovementLock();



        // FORCED STAY LOGIC: Handle Forced Stay
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



        // RESTRAINT IMMOBILIZATION, in where we need to prevent LMB+RMB movement and also cancel keys.
        if (HandleImmobilize)
        {
            CancelMoveKeys();
            // Stop all movement but only when LMB and RMB are down.
            if (KeyMonitor.IsBothMouseButtonsPressed())
                _MoveController.EnableMovementLock();
            // And release Movement Lock when they aren't pressed.
            else _MoveController.DisableMovementLock();
        }
        else ResetCancelledMoveKeys();



        // BLINDFOLDED STATE - Force Lock First Person if desired.
        if (_clientConfigs.GagspeakConfig.ForceLockFirstPerson && _handler.IsBlindfolded)
        {
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
                if (KeyMonitor.IsKeyPressed((int)(Keys)x))
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
