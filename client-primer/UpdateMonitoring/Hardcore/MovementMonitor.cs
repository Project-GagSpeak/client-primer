using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
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
using GagspeakAPI.Data.Permissions;
using Microsoft.Extensions.Logging;
using System.Numerics;
using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using static Lumina.Data.Parsing.Layer.LayerCommon;
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
    private readonly EmoteMonitor _emoteMonitor;
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
        EmoteMonitor emoteMonitor, MoveController moveController, ICondition condition, 
        IClientState clientState, IKeyState keyState, IObjectTable objectTable, 
        ITargetManager targetManager) : base(logger, mediator)
    {
        _clientConfigs = clientConfigs;
        _handler = hardcoreHandler;
        _outfitHandler = outfitHandler;
        _promptsString = stringPrompts;
        _promptsYesNo = yesNoPrompts;
        _promptsRooms = rooms;
        _frameworkUtils = frameworkUtils;
        _emoteMonitor = emoteMonitor;
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
        Mediator.Subscribe<DelayedFrameworkUpdateMessage>(this, _ => EmoteUpdate());

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
        if (restraintSet.EnabledBy == MainHub.UID)
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
                _MoveController.DisableMouseAutoMoveHook();
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

            // Do not account for auto-disable logic if our Offset is .MinValue.
            if (_handler.LastMovementTime != DateTimeOffset.MinValue)
            {
                // Check to see if the player is moving or not.
                if (_clientState.LocalPlayer.Position != _handler.LastPosition)
                {
                    _handler.LastMovementTime = DateTimeOffset.UtcNow;           // reset timer
                    _handler.LastPosition = _clientState.LocalPlayer.Position;// update last position
                }

                // if we have been idle for longer than 6 seconds, we should release the player.
                if ((DateTimeOffset.UtcNow - _handler.LastMovementTime).TotalSeconds > 6)
                    _handler.UpdateForcedFollow(NewState.Disabled);
            }
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

        // FORCED STAY LOGIC: Handle Forced Stay
        if (_handler.MonitorStayLogic)
        {
            // while they are active, if we are not in a dialog prompt option, scan to see if we are by an estate entrance
            if (!_condition[ConditionFlag.OccupiedInQuestEvent] && !_frameworkUtils._sentBetweenAreas)
            {
                // grab all the event object nodes (door interactions)
                var nodes = _objectTable.Where(x => x.ObjectKind == Dalamud.Game.ClientState.Objects.Enums.ObjectKind.EventObj).ToList();
                foreach (var node in nodes)
                {
                    // Grab distance to object.
                    var distance = GetTargetDistance(node);
                    // If its a estate entrance, and we are within 3.5f, interact with it.
                    if (node.Name.TextValue is "Entrance" or "Apartment Building Entrance" && distance < 3.5f)
                    {
                        _targetManager.Target = node;
                        TargetSystem.Instance()->InteractWithObject((GameObject*)node.Address, false);
                        break;
                    }
                    // If its a node that is an Entrance to Additional Chambers.
                    if (node.Name.TextValue is "Entrance to Additional Chambers")
                    {
                        // if we are not within 2f of it, attempt to execute the task.
                        if (distance > 2f && _clientConfigs.GagspeakConfig.MoveToChambersInEstates)
                        {
                            if (_moveToChambersTask is not null && !_moveToChambersTask.IsCompleted)
                                return;
                            Logger.LogDebug("Moving to Additional Chambers", LoggerType.HardcoreMovement);
                            _moveToChambersTask = GoToChambersEntrance(node);
                        }

                        // if we are within 2f, interact with it.
                        if(distance <= 2f)
                        {
                            _targetManager.Target = node;
                            TargetSystem.Instance()->InteractWithObject((GameObject*)node.Address, false);
                        }
                        break;
                    }
                }
            }
        }

        // Handle Prompt Logic.
        if (_handler.MonitorStayLogic || _frameworkUtils.InCutsceneEvent)
        {
            // enable the hooks for the option prompts
            if (!_promptsString.Enabled) _promptsString.Enable();
            if (!_promptsYesNo.Enabled) _promptsYesNo.Enable();
            if (!_promptsRooms.Enabled) _promptsRooms.Enable();
        }
        else
        {
            if (_promptsString.Enabled) _promptsString.Disable();
            if (_promptsYesNo.Enabled) _promptsYesNo.Disable();
            if (_promptsRooms.Enabled) _promptsRooms.Disable();
        }


        // Cancel Keys if forced follow or immobilization is active. (Also disable our keys we are performing the Chambers Task)
        if (_handler.MonitorFollowLogic || HandleImmobilize || _moveToChambersTask is not null)
            CancelMoveKeys();
        else
            ResetCancelledMoveKeys();

        // RESTRAINT IMMOBILIZATION OR FORCED FOLLOW, in where we need to prevent LMB+RMB movement.
        if (HandleImmobilize)
            _MoveController.EnableMouseAutoMoveHook();
        else
            _MoveController.DisableMouseAutoMoveHook();

        // BLINDFOLDED STATE - Force Lock First Person if desired.
        if (_clientConfigs.GagspeakConfig.ForceLockFirstPerson && _handler.IsBlindfolded)
        {
            if (cameraManager->Camera is not null && cameraManager->Camera->Mode is not (int)CameraControlMode.FirstPerson)
                cameraManager->Camera->Mode = (int)CameraControlMode.FirstPerson;
        }

        // FORCED Emote LOGIC Logic.
        if (_handler.MonitorEmoteLogic)
            _MoveController.EnableMovementLock();
    }

    private unsafe void EmoteUpdate()
    {
        if (_handler.MonitorEmoteLogic)
        {
            // If our Forced Emote State UIS is not string.empty, we should attempt to execute the emote.
            if (_handler.ForcedEmoteState.UID != string.Empty)
            {
                // We should be executing an emote, so get our current EmoteID.
                ushort currentEmote = _emoteMonitor.CurrentEmoteId();
                if (currentEmote is 51)
                    return;

                // Check to see if we should be handling the expected at all.
                if (_emoteMonitor.ShouldHandleExpected(_handler.ForcedEmoteState, out bool doEmote, out bool doCyclePose, out bool ensureNoSit))
                {
                    // If we should do the emote, perform the emote check.
                    if (ensureNoSit)
                    {
                        Logger.LogDebug("Forcing Emote: /SIT. (Current emote was: " + currentEmote + ").");
                        EmoteMonitor.ExecuteEmote(50);
                    }
                    else if (doEmote)
                    {
                        Logger.LogDebug("Forcing Emote: " + _handler.ForcedEmoteState.EmoteID + "(Current emote was: " + currentEmote + ")");
                        EmoteMonitor.ExecuteEmote(_handler.ForcedEmoteState.EmoteID);
                    }

                    // If we should do the cycle pose, perform the cycle pose check.
                    if (doCyclePose && !EmoteMonitor.IsCyclePoseTaskRunning)
                    {
                        // Force the cycle pose state to the expected state.
                        if (_emoteMonitor.CurrentCyclePose() != _handler.ForcedEmoteState.CyclePoseByte)
                            _emoteMonitor.ForceCyclePose(_handler.ForcedEmoteState.CyclePoseByte);
                    }
                }
            }
        }
    }

    private Task? _moveToChambersTask;

    private async Task GoToChambersEntrance(IGameObject nodeToWalkTo)
    {
        try
        {
            Logger.LogDebug("Node for Chambers Detected, Auto Walking to it for 5 seconds.");
            // Set the target to the node.
            _targetManager.Target = nodeToWalkTo;
            // lock onto the object
            _handler.SendMessageHardcore("lockon");
            await Task.Delay(500);
            _handler.SendMessageHardcore("automove");
            // set mode to run
            unsafe
            {
                uint isWalking = Marshal.ReadByte((nint)gameControl, 24131);
                // they are walking, so make them run.
                if (isWalking is not 0) 
                    Marshal.WriteByte((nint)gameControl, 24131, 0x0);
            }
            // await for 5 seconds then complete the task.
            await Task.Delay(5000);
        }
        finally
        {
            _moveToChambersTask = null;
        }
    }

    // Helper functions for minimizing the content in the framework update code section above
    public float GetTargetDistance(IGameObject target)
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
