using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using GagSpeak.PlayerData.Services;
using GagSpeak.Services.Mediator;
using GagSpeak.UpdateMonitoring;
using GagspeakAPI.Data.Enum;
using Glamourer.Api.Enums;
using Glamourer.Api.Helpers;
using Glamourer.Api.IpcSubscribers;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using FFXIVClientStructs.FFXIV.Client.Game.Character;

namespace GagSpeak.Interop.Ipc;

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
    private readonly Glamourer.Api.IpcSubscribers.GetState _glamourerGetState;
    private readonly ApplyState _ApplyState; // apply a state to the character using JObject
    private readonly GetDesignList _GetDesignList; // get lists of designs from player's Glamourer.
    private readonly ApplyDesignName _ApplyCustomizationsFromDesignName; // apply customization data from a design.
    private readonly SetItem _SetItem; // for setting an item to the character
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
        _glamourerGetState = new Glamourer.Api.IpcSubscribers.GetState(pluginInterface);
        _ApplyState = new ApplyState(_pi);
        _GetDesignList = new GetDesignList(_pi);
        _ApplyCustomizationsFromDesignName = new ApplyDesignName(_pi);
        _SetItem = new SetItem(_pi);
        _RevertCharacter = new RevertState(_pi);
        _RevertToAutomation = new RevertToAutomation(_pi);

        // check API status.
        CheckAPI();

        // set event subscribers
        _glamourChanged = StateChangedWithType.Subscriber(_pi, GlamourerChanged);
        _glamourChanged.Enable();
    }

    public enum MetaData { Hat, Visor }
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

    private void OnGlamourerReady()
    {
        Logger.LogWarning("Glamourer is now Ready!");
        Mediator.Publish(new GlamourerReady());
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
    public async Task SetItemToCharacterAsync(ApiEquipSlot slot, ulong item, IReadOnlyList<byte> dye, uint variant)
    {
/*        // if the glamourerApi is not active, then return an empty string for the customization
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
        }*/
    }

    private GlamourerApiEc _lastError;
    private JObject? _state;
    private string? _stateString;

    public Newtonsoft.Json.Linq.JObject? GetState()
    {
        try
        {
            var success = _glamourerGetState.Invoke(_clientState.LocalPlayer!.ObjectIndex);
            _stateString = _state?.ToString(Newtonsoft.Json.Formatting.Indented) ?? "No State Available";
            return _state;
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Error during GetState: {ex}");
            return null;
        }
    }



    public async Task<bool> ForceSetMetaData(MetaData metaData, bool? forcedState = null)
    {
        // if the glamourerApi is not active, then return an empty string for the customization
        if (!APIAvailable || _onFrameworkService.IsZoning) return false;
        try
        {
            return await _onFrameworkService.RunOnFrameworkThread(() =>
            {
                // grab character pointer (do this here so we do it inside the framework thread)
                var character = _onFrameworkService._playerAddr;
                // set the game object to the character
                var gameObj = _onFrameworkService.CreateGameObject(character);
                // if the game object is the character, then get the customization for it.
                if (gameObj is ICharacter c)
                {
                    var objectIndex = c.ObjectIndex;
                    // grab the JObject of the character state.
                    var playerState = GetState();

                    // determine the metadata field we are editing.
                    var metaDataFieldToFind = (metaData == MetaData.Hat) ? "Show" : "IsToggled";
                    // if we are forcing the state, set the newvalue to the forced state. Otherwise, grab the opposite of current state value.
                    var newValue = forcedState ?? !(((bool?)playerState?["Equipment"]?[metaData.ToString()]?[metaDataFieldToFind]) ?? false);

                    // set the new properties in the JObject.
                    playerState!["Equipment"]![metaData.ToString()]![metaDataFieldToFind] = newValue;
                    playerState!["Equipment"]![metaData.ToString()]!["Apply"] = true;

                    var ret = _ApplyState.Invoke(playerState, objectIndex);
                    return ret == GlamourerApiEc.Success;
                }
                return false;
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Error during SetMetaData: {ex}");
            return false;
        }
    }


    public async Task GlamourerRevertCharacterToAutomation(nint character)
    {
/*        // if the glamourerApi is not active, then return an empty string for the customization
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
        }*/
    }

    public async Task GlamourerRevertCharacter(nint character)
    {
/*        // if the glamourerApi is not active, then return an empty string for the customization
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
        }*/
    }

    /// <summary> Fired upon by the IPC event subscriber when the glamourer changes. </summary>
    /// <param name="address"> The address of the character that changed. </param>
    /// <param name="changeType"> The type of change that occurred. </param>"
    private void GlamourerChanged(nint address, StateChangeType changeType)
    {
/*        // do not accept if coming from other player besides us.
        if (address != _onFrameworkService._playerAddr) return;

        // block if we are not desiring to listen to changes yet.
        if (OnFrameworkService.GlamourChangeEventsDisabled)
        {
            Logger.LogTrace($"GlamourEvent Blocked: {changeType}");
            return;
        }

        // See if the change type is a type we are looking for
        if (changeType == StateChangeType.Design
        || changeType == StateChangeType.Reapply
        || changeType == StateChangeType.Reset
        || changeType == StateChangeType.Equip
        || changeType == StateChangeType.Stains)
        {
            Logger.LogTrace($"StateChangeType is {changeType}");

            // call the update glamourer appearance message.
            _fastUpdates.Invoke(GlamourUpdateType.RefreshAll);
        }
        else // it is not a type we care about, so ignore
        {
            Logger.LogTrace($"GlamourerChanged event was not a type we care about, " +
                $"so skipping (Type was: {changeType})");
        }*/
    }
}
