using System;
using System.Linq;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Glamourer.Api.Enums;
using Glamourer.Api.IpcSubscribers;
using GagSpeak.Services;
using GagSpeak.Services.Mediator;
using GagSpeak.Interop.Ipc;
using GagSpeak.UpdateMonitoring;
using Glamourer.Api.Helpers;
using Dalamud.Interface.ImGuiNotification;
using GagSpeak.PlayerData.Services;
using FFXIVClientStructs.FFXIV.Client.LayoutEngine.Layer;
using GagspeakAPI.Data.Enum;

namespace Interop.Ipc;

/// <summary>
/// Create a sealed class for our interop manager.
/// </summary>
public sealed class IpcCallerGlamourer : DisposableMediatorSubscriberBase, IIpcCaller
{
    /* ------------- Class Attributes ------------- */
    private readonly IDalamudPluginInterface _pi;
    private readonly IClientState _clientState;
    private readonly OnFrameworkService _onFrameworkService;
    private readonly GlamourFastUpdate _fastUpdates;
    private bool _shownGlamourerUnavailable = false; // safety net to prevent notification spam.

    /* --------- Glamourer API Event Subscribers -------- */
    public readonly EventSubscriber<nint, StateChangeType> _glamourChanged;

    /* ---------- Glamourer API IPC Subscribers --------- */
    private readonly ApiVersion _ApiVersion; // the API version of glamourer
    // private readonly ApplyState? _ApplyOnlyEquipment; // for applying equipment to player
    private readonly SetItem _SetItem; // for setting an item to the character (not once by default, must setup with flags)
    private readonly RevertState _RevertCharacter; // for reverting the character
    private readonly RevertToAutomation _RevertToAutomation; // for reverting the character to automation

    public IpcCallerGlamourer(ILogger<IpcCallerGlamourer> logger,
        IDalamudPluginInterface pluginInterface, IClientState clientState, 
        OnFrameworkService OnFrameworkService, GagspeakMediator mediator, 
        GlamourFastUpdate fastUpdates) : base(logger, mediator)
    {
        _pi = pluginInterface; 
        _onFrameworkService = OnFrameworkService;
        _clientState = clientState;
        _fastUpdates = fastUpdates;

        // set IPC callers
        _ApiVersion = new ApiVersion(_pi);
        // _ApplyOnlyEquipment = new ApplyState(_pi);
        _SetItem = new SetItem(_pi);
        _RevertCharacter = new RevertState(_pi);
        _RevertToAutomation = new RevertToAutomation(_pi);
    
        // check API status.
        CheckAPI();

        // set event subscribers
        _glamourChanged = StateChangedWithType.Subscriber(_pi, GlamourerChanged);
        _glamourChanged.Enable();
    }
    public bool APIAvailable { get; private set; }

    public void CheckAPI()
    {
        bool apiAvailable = false; // assume false at first
        try
        {
            var version = _ApiVersion.Invoke();
            if (version is { Major: 1, Minor: >= 2 })
            {
                apiAvailable = true;
            }
            _shownGlamourerUnavailable = _shownGlamourerUnavailable && !apiAvailable;
        }
        catch { /* Do not allow legacy catch checks, consume */ }
        finally
        {
            APIAvailable = apiAvailable;
            if (!apiAvailable && !_shownGlamourerUnavailable)
            {
                _shownGlamourerUnavailable = true;
                Mediator.Publish(new NotificationMessage("Glamourer inactive", "Features Using Glamourer will not function.", NotificationType.Warning));
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);



        _glamourChanged.Disable();
        _glamourChanged?.Dispose();
        // revert our character back to the base game state
        if (_clientState.LocalPlayer != null && _clientState.LocalPlayer.Address != nint.Zero)
        {
            Task.Run(() => GlamourerRevertCharacterToAutomation(_clientState.LocalPlayer.Address));
        }
    }



    /// <summary> ========== BEGIN OUR IPC CALL MANAGEMENT UNDER ASYNC TASKS ========== </summary>
    // A note: ApplyAll and Customizations have been removed, as applyEquipmentOnly does not work
    // as intended and is more difficult to achieve with the way glamourer's API is structured.

    public async Task SetItemToCharacterAsync(ApiEquipSlot slot, ulong item, IReadOnlyList<byte> dye, uint variant)
    {
        // if the glamourerApi is not active, then return an empty string for the customization
        if (!APIAvailable || _onFrameworkService.IsZoning) return;
        try
        {
            // await for us to be running on the framework thread. Once we are:
            await _onFrameworkService.RunOnFrameworkThread(() =>
            {
                // grab character pointer (do this here so we do it inside the framework thread)
                var characterAddr = _onFrameworkService._playerAddr;
                // set the game object to the character
                var gameObj = _onFrameworkService.CreateGameObject(characterAddr);
                // if the game object is the character, then get the customization for it.
                if (gameObj is ICharacter c)
                {
                    _SetItem!.Invoke(c.ObjectIndex, slot, item, dye, 1337);
                }
            }).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            // if at any point this errors, return an empty string as well.
            Logger.LogWarning($"Failed to set item to character with slot {slot}, item {item}, dye {dye.ToArray().ToString()}, and key {variant}, {ex}");
            return;
        }
    }

    public async Task GlamourerRevertCharacterToAutomation(nint character)
    {
        // if the glamourerApi is not active, then return an empty string for the customization
        if (!APIAvailable || _onFrameworkService.IsZoning) return;
        try
        {
            // we specifically DONT want to wait for character to finish drawing because we want to revert before an automation is applied
            await _onFrameworkService.RunOnFrameworkThread(async () =>
            {
                try
                {
                    // set the game object to the character
                    var gameObj = _onFrameworkService.CreateGameObject(character);
                    // if the game object is the character, then get the customization for it.
                    if (gameObj is ICharacter c)
                    {
                        Logger.LogTrace("Calling on IPC: GlamourerRevertToAutomationCharacter");
                        var result = _RevertToAutomation.Invoke(c.ObjectIndex);
                        Logger.LogTrace($"Revert to automation result: {result}");
                        // if it doesnt return success, revert to game instead
                        if (result != GlamourerApiEc.Success)
                        {
                            Logger.LogWarning($"Revert to automation failed, reverting to game instead");
                            await GlamourerRevertCharacter(character);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"Error during GlamourerRevert: {ex}");
                }
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Error during GlamourerRevert: {ex}");
        }
    }

    public async Task GlamourerRevertCharacter(nint character)
    {
        // if the glamourerApi is not active, then return an empty string for the customization
        if (!APIAvailable || _onFrameworkService.IsZoning) return;
        try
        {
            // we specifically DONT want to wait for character to finish drawing because we want to revert before an automation is applied
            await _onFrameworkService.RunOnFrameworkThread(() =>
            {
                try
                {
                    // set the game object to the character
                    var gameObj = _onFrameworkService.CreateGameObject(character);
                    // if the game object is the character, then get the customization for it.
                    if (gameObj is ICharacter c)
                    {
                        _RevertCharacter.Invoke(c.ObjectIndex);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"Error during GlamourerRevert: {ex}");
                }
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Error during GlamourerRevert: {ex}");
        }
    }

    /// <summary> Fired upon by the IPC event subscriber when the glamourer changes. </summary>
    /// <param name="address"> The address of the character that changed. </param>
    /// <param name="changeType"> The type of change that occurred. </param>"
    private void GlamourerChanged(nint address, StateChangeType changeType)
    {
        if (OnFrameworkService.GlamourChangeEventsDisabled || !OnFrameworkService.GlamourChangeFinishedDrawing)
        {
            Logger.LogTrace($"GlamourEvent Blocked: {changeType}");
            return;
        }
        // do not accept if coming from other player besides us.
        if (address != _onFrameworkService._playerAddr) return;

        // See if the change type is a type we are looking for
        if (changeType == StateChangeType.Design
        || changeType == StateChangeType.Reapply
        || changeType == StateChangeType.Reset
        || changeType == StateChangeType.Equip
        || changeType == StateChangeType.Stains
        || changeType == StateChangeType.Weapon)
        {
            Logger.LogTrace($"StateChangeType is {changeType}");

            // call the update glamourer appearance message.
            _fastUpdates.Invoke(GlamourUpdateType.RefreshAll);
        }
        else // it is not a type we care about, so ignore
        {
            Logger.LogTrace($"GlamourerChanged event was not a type we care about, " +
                $"so skipping (Type was: {changeType})");
        }
    }

}
