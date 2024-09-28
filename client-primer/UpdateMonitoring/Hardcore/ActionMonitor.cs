using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.Interop;
using GagSpeak.GagspeakConfiguration.Models;
using GagSpeak.Hardcore;
using GagSpeak.Hardcore.Hotbar;
using GagSpeak.PlayerData.Handlers;
using GagSpeak.PlayerData.Services;
using GagSpeak.Services.ConfigurationServices;
using GagSpeak.Services.Mediator;
using GagSpeak.UpdateMonitoring;
using System.Collections.Immutable;
using ClientStructFramework = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework;

namespace UpdateMonitoring;
public unsafe class ActionMonitor : DisposableMediatorSubscriberBase
{
    #region ClassIncludes
    private readonly ClientConfigurationManager _clientConfigs;
    private readonly HotbarLocker _hotbarLocker;
    private readonly HardcoreHandler _hardcoreHandler;
    private readonly WardrobeHandler _wardrobeHandler;
    private readonly OnFrameworkService _frameworkUtils;
    private readonly IClientState _clientState;
    private readonly IDataManager _dataManager;

    // attempt to get the rapture hotbar module so we can modify the display of hotbar items
    public RaptureHotbarModule* raptureHotbarModule = ClientStructFramework.Instance()->GetUIModule()->GetRaptureHotbarModule();

    // hook creation for the action manager
    // if sigs fuck up, reference https://github.com/PunishXIV/Orbwalker/blob/f850e04eb9371aa5d7f881e3024d7f5d0953820a/Orbwalker/Memory.cs#L15
    internal delegate bool UseActionDelegate(ActionManager* am, ActionType type, uint acId, long target, uint a5, uint a6, uint a7, void* a8);
    internal Hook<UseActionDelegate> UseActionHook;

    #endregion ClassIncludes

    public Dictionary<uint, AcReqProps[]> CurrentJobBannedActions = new Dictionary<uint, AcReqProps[]>(); // stores the current job actions
    public Dictionary<int, Tuple<float, DateTime>> CooldownList = new Dictionary<int, Tuple<float, DateTime>>(); // stores the recast timers for each action

    public unsafe ActionMonitor(ILogger<ActionMonitor> logger, GagspeakMediator mediator,
        ClientConfigurationManager clientConfigs, HotbarLocker hotbarLocker,
        HardcoreHandler handler, WardrobeHandler wardrobeHandler,
        OnFrameworkService frameworkUtils, IClientState clientState, IDataManager dataManager,
        IGameInteropProvider interop) : base(logger, mediator)
    {
        _clientConfigs = clientConfigs;
        _hotbarLocker = hotbarLocker;
        _hardcoreHandler = handler;
        _wardrobeHandler = wardrobeHandler;
        _frameworkUtils = frameworkUtils;
        _clientState = clientState;
        _dataManager = dataManager;

        // set up a hook to fire every time the address signature is detected in our game.
        UseActionHook = interop.HookFromAddress<UseActionDelegate>((nint)ActionManager.MemberFunctionPointers.UseAction, UseActionDetour);
        UseActionHook.Enable();

        // initialize
        UpdateJobList();
        // see if we should enable the sets incase we load this prior to the restraint set manager loading.
        if (_wardrobeHandler.ActiveSet != null && _wardrobeHandler.ActiveSet.EnabledBy != "SelfAssigned" &&
            _clientConfigs.PropertiesEnabledForSet(_clientConfigs.GetActiveSetIdx(), _wardrobeHandler.ActiveSet.EnabledBy))
        {
            Logger.LogDebug("Hardcore RestraintSet is now active", LoggerType.HardcoreActions);
            // apply stimulation modifier, if any (TODO)
            _hardcoreHandler.ApplyMultiplier();
            // activate hotbar lock, if we have any properties enabled (we always will since this subscriber is only called if there is)
            _hotbarLocker.SetHotbarLockState(true);
        }
        else
        {
            Logger.LogDebug("No restraint sets are active", LoggerType.HardcoreActions);
        }

        // subscribe to events.
        Mediator.Subscribe<FrameworkUpdateMessage>(this, (_) => FrameworkUpdate());

        Mediator.Subscribe<RestraintSetToggleHardcoreTraitsMessage>(this, (msg) =>
        {
            if (msg.AssignerUID != "SelfAssigned" && msg.State == NewState.Enabled)
            {
                // apply stimulation modifier, if any (TODO)
                _hardcoreHandler.ApplyMultiplier();
                // activate hotbar lock, if we have any properties enabled (we always will since this subscriber is only called if there is)
                _hotbarLocker.SetHotbarLockState(true);
                // begin allowing monitoring of properties
                MonitorHardcoreRestraintSetProperties = true;
            }
            if (msg.AssignerUID != "SelfAssigned" && msg.State == NewState.Disabled)
            {
                // reset multiplier
                _hardcoreHandler.StimulationMultiplier = 1.0;
                // we should also restore hotbar slots
                RestoreSavedSlots();
                // we should also unlock hotbar lock
                _hotbarLocker.SetHotbarLockState(false);
                // halt monitoring of properties
                MonitorHardcoreRestraintSetProperties = false;
            }

            if (msg.HardcoreTraitsTask != null)
            {
                msg.HardcoreTraitsTask.SetResult(true);
            }
        });
    }

    public bool MonitorHardcoreRestraintSetProperties = false;

    protected override void Dispose(bool disposing)
    {
        // set lock to visable again
        _hotbarLocker.SetHotbarLockState(false);
        // dispose of the hook
        if (UseActionHook != null)
        {
            if (UseActionHook.IsEnabled)
            {
                UseActionHook.Disable();
            }
            UseActionHook.Dispose();
            UseActionHook = null!;
        }
        // dispose of the base class
        base.Dispose(disposing);
    }

    public void RestoreSavedSlots()
    {
        if (_clientState.LocalPlayer != null && _clientState.LocalPlayer.ClassJob != null && raptureHotbarModule != null)
        {
            Logger.LogDebug("Restoring saved slots", LoggerType.HardcoreActions);
            var baseSpan = raptureHotbarModule->StandardHotbars; // the length of our hotbar count
            for (var i = 0; i < baseSpan.Length; i++)
            {
                var hotbarRow = baseSpan.GetPointer(i);
                // if the hotbar is not null, we can get the slots data
                if (hotbarRow != null)
                {
                    raptureHotbarModule->LoadSavedHotbar(_clientState.LocalPlayer.ClassJob.Id, (uint)i);
                }
            }
        }
    }

    // fired on framework tick while a set is active
    private void UpdateSlots(HardcoreSetProperties setProperties)
    {
        var hotbarSpan = raptureHotbarModule->StandardHotbars; // the length of our hotbar count
        for (var i = 0; i < hotbarSpan.Length; i++)
        {
            var hotbarRow = hotbarSpan.GetPointer(i);
            if (hotbarSpan == null) continue;

            // get the slots data...
            for (var j = 0; j < 16; j++)
            {
                var slot = hotbarRow->Slots.GetPointer(j);
                if (slot == null) break;
                // if the slot is not empty, get the command id
                var isAction = slot->CommandType == RaptureHotbarModule.HotbarSlotType.Action ||
                               slot->CommandType == RaptureHotbarModule.HotbarSlotType.GeneralAction;
                // if it is a valid action, scan to see if the commandID is equal to any of our banned actions
                if (isAction && CurrentJobBannedActions.TryGetValue(slot->CommandId, out var props))
                {
                    // see if any of the indexes in the array contain a AcReqPros
                    if (setProperties.Gagged && props.Contains(AcReqProps.Speech))
                    {
                        // speech should be restrained, so remove any actions requiring speech
                        slot->Set(raptureHotbarModule->UIModule, RaptureHotbarModule.HotbarSlotType.Action, 2886);
                        continue;
                    }
                    if (setProperties.Blindfolded && props.Contains(AcReqProps.Sight))
                    {
                        // sight should be restrained, so remove any actions requireing sight
                        slot->Set(raptureHotbarModule->UIModule, RaptureHotbarModule.HotbarSlotType.Action, 99);
                        continue;
                    }
                    if (setProperties.Weighty && props.Contains(AcReqProps.Weighted))
                    {
                        // weighted should be restrained, so remove any actions requireing weight
                        slot->Set(raptureHotbarModule->UIModule, RaptureHotbarModule.HotbarSlotType.Action, 151);
                        continue;
                    }
                    if (setProperties.Immobile && props.Contains(AcReqProps.Movement))
                    {
                        // immobile should be restrained, so remove any actions requireing movement
                        slot->Set(raptureHotbarModule->UIModule, RaptureHotbarModule.HotbarSlotType.Action, 2883);
                        continue;
                    }
                    if (setProperties.LegsRestrained && props.Contains(AcReqProps.LegMovement))
                    {
                        // legs should be restrained, so remove any actions requireing leg movement
                        slot->Set(raptureHotbarModule->UIModule, RaptureHotbarModule.HotbarSlotType.Action, 55);
                        continue;
                    }
                    if (setProperties.ArmsRestrained && props.Contains(AcReqProps.ArmMovement))
                    {
                        // arms should be restrained, so remove any actions requireing arm movement
                        slot->Set(raptureHotbarModule->UIModule, RaptureHotbarModule.HotbarSlotType.Action, 68);
                        continue;
                    }
                }
            }
        }
    }

    // for updating our stored job list dictionary
    private void UpdateJobList()
    {
        // this will be called by the job changed event. When it does, we will update our job list with the new job.
        if (_clientState.LocalPlayer != null && _clientState.LocalPlayer.ClassJob != null)
        {
            Logger.LogDebug("Updating job list", LoggerType.HardcoreActions);
            ActionData.GetJobActionProperties((JobType)_clientState.LocalPlayer.ClassJob.Id, out var bannedJobActions);
            CurrentJobBannedActions = bannedJobActions; // updated our job list
            // only do this if we are logged in
            if (_clientState.IsLoggedIn
            && _clientState.LocalPlayer != null
            && _clientState.LocalPlayer.Address != nint.Zero
            && raptureHotbarModule->StandardHotbars != null)
            {
                GenerateCooldowns();
            }
        }
        else
        {
            Logger.LogDebug("Player is null, returning", LoggerType.HardcoreActions);
        }
    }

    private void GenerateCooldowns()
    {
        // if our current dictionary is not empty, empty it
        if (CooldownList.Count > 0)
        {
            CooldownList.Clear();
        }
        // get the current job actions
        var baseSpan = raptureHotbarModule->StandardHotbars; // the length of our hotbar count
        for (var i = 0; i < baseSpan.Length; i++)
        {
            // get our hotbar row
            var hotbar = baseSpan.GetPointer(i);
            // if the hotbar is not null, we can get the slots data
            if (hotbar != null)
            {
                // get the slots data...
                for (var j = 0; j < 16; j++)
                {
                    var slot = hotbar->Slots.GetPointer(j);
                    if (slot == null) break;
                    if (slot->CommandType == RaptureHotbarModule.HotbarSlotType.Action)
                    {
                        // we will want to add the tuple for each slot, the tuple should contain the cooldown group
                        var adjustedId = ActionManager.Instance()->GetAdjustedActionId(slot->CommandId);
                        // get the cooldown group
                        var cooldownGroup = -1;
                        var action = _dataManager.Excel.GetSheet<Lumina.Excel.GeneratedSheets.Action>()!.GetRow(adjustedId);
                        if (action == null) { break; }
                        // there is a minus one offset for actions, while general actions do not have them.
                        cooldownGroup = action.CooldownGroup - 1;
                        // get recast time
                        var recastTime = ActionManager.GetAdjustedRecastTime(ActionType.Action, adjustedId);
                        recastTime = (int)(recastTime * _hardcoreHandler.StimulationMultiplier);
                        // if it is an action or general action, append it
                        // Logger.LogTrace($" SlotID {slot->CommandId} Cooldown group {cooldownGroup} with recast time {recastTime}");
                        if (!CooldownList.ContainsKey(cooldownGroup))
                        {
                            CooldownList.Add(cooldownGroup, new Tuple<float, DateTime>(recastTime, DateTime.MinValue));
                        }
                    }
                }
            }
        }
    }

    #region Framework Updates
    private unsafe void FrameworkUpdate()
    {
        // make sure we only do checks when we are properly logged in and have a character loaded
        if (_clientState.LocalPlayer?.IsDead ?? false)
        {
            return;
        }

        if (AllowFrameworkHardcoreUpdates())
        {
            // if the class job is different than the one stored, then we have a class job change (CRITICAL TO UPDATING PROPERLY)
            if (_clientState.LocalPlayer!.ClassJob.Id != _frameworkUtils.PlayerClassJobId)
            {
                // update the stored class job
                _frameworkUtils.PlayerClassJobId = _clientState.LocalPlayer.ClassJob.Id;
                // invoke jobChangedEvent to call the job changed glamour event
                IpcFastUpdates.InvokeGlamourer(GlamourUpdateType.JobChange);
                // regenerate our slots
                UpdateJobList();
                RestoreSavedSlots();
                return;
            }

            // see if any set is enabled
            // TODO: Fix this logic
            if (_clientConfigs.PropertiesEnabledForSet(_clientConfigs.GetActiveSetIdx(), _wardrobeHandler.ActiveSet.EnabledBy))
            {
                UpdateSlots(_wardrobeHandler.ActiveSet.SetProperties[_wardrobeHandler.ActiveSet.EnabledBy]);
            }
        }
    }
    #endregion Framework Updates
    private bool UseActionDetour(ActionManager* am, ActionType type, uint acId, long target, uint a5, uint a6, uint a7, void* a8)
    {
        try
        {
            //Logger.LogTrace($" UseActionDetour called {acId} {type}");

            // if we are allowing hardcore updates / in hardcore mode
            if (AllowFrameworkHardcoreUpdates())
            {
                // If someone is forcing us to stay, we should block access to teleports and other methods of death.
                if (_hardcoreHandler.IsForcedStay)
                {
                    // check if we are trying to hit teleport or return from hotbars /  menus
                    if (type == ActionType.GeneralAction && (acId == 7 || acId == 8))
                    {
                        Logger.LogTrace("You are currently locked away, canceling teleport/return execution", LoggerType.HardcoreActions);
                        return false;
                    }
                    // if we somehow managed to start executing it, then stop that too
                    if (type == ActionType.Action && (acId == 5 || acId == 6 || acId == 11408))
                    {
                        Logger.LogTrace("You are currently locked away, canceling teleport/return execution", LoggerType.HardcoreActions);
                        return false;
                    }
                }

                // because they are, lets see if the light, mild, or heavy stimulation is active
                if (_wardrobeHandler.ActiveSet.SetProperties[_wardrobeHandler.ActiveSet.EnabledBy].LightStimulation ||
                    _wardrobeHandler.ActiveSet.SetProperties[_wardrobeHandler.ActiveSet.EnabledBy].MildStimulation ||
                    _wardrobeHandler.ActiveSet.SetProperties[_wardrobeHandler.ActiveSet.EnabledBy].HeavyStimulation)
                {
                    // then let's check our action ID's to apply the modified cooldown timers
                    if (ActionType.Action == type && acId > 7)
                    {
                        var recastTime = ActionManager.GetAdjustedRecastTime(type, acId);
                        var adjustedId = am->GetAdjustedActionId(acId);
                        var recastGroup = am->GetRecastGroup((int)type, adjustedId);
                        if (CooldownList.ContainsKey(recastGroup))
                        {
                            // Logger.LogDebug($" GROUP FOUND - Recast Time: {recastTime} | Cast Group: {recastGroup}");
                            var cooldownData = CooldownList[recastGroup];
                            // if we are beyond our recast time from the last time used, allow the execution
                            if (DateTime.Now >= cooldownData.Item2.AddMilliseconds(cooldownData.Item1))
                            {
                                // Update the last execution time before execution
                                Logger.LogTrace("ACTION COOLDOWN FINISHED", LoggerType.HardcoreActions);
                                CooldownList[recastGroup] = new Tuple<float, DateTime>(cooldownData.Item1, DateTime.Now);
                            }
                            else
                            {
                                Logger.LogTrace("ACTION COOLDOWN NOT FINISHED", LoggerType.HardcoreActions);
                                return false; // Do not execute the action
                            }
                        }
                        else
                        {
                            Logger.LogDebug("GROUP NOT FOUND", LoggerType.HardcoreActions);
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
            Logger.LogError(e.ToString());
        }
        // return the original if we reach here
        var ret = UseActionHook.Original(am, type, acId, target, a5, a6, a7, a8);
        // invoke the action used event
        return ret;
    }

    private bool AllowFrameworkHardcoreUpdates()
    {
        return
           _clientState.IsLoggedIn                          // we must be logged in
        && _clientState.LocalPlayer != null                 // our character must not be null
        && _clientState.LocalPlayer.Address != nint.Zero    // our address must be valid
        && MonitorHardcoreRestraintSetProperties;               // is in hardcore for anyone.
    }
}
