using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using GagSpeak.PlayerData.Services;
using GagSpeak.Services.Mediator;
using GagSpeak.UpdateMonitoring;
using GagspeakAPI.Enums;
using Glamourer.Api.Enums;
using Glamourer.Api.Helpers;
using Glamourer.Api.IpcSubscribers;
using GagSpeak.GagspeakConfiguration.Models;
using GagSpeak.Utils;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;

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
    private readonly ItemIdVars _itemHelper;
    private readonly IpcFastUpdates _fastUpdates;
    private bool _shownGlamourerUnavailable = false; // safety net to prevent notification spam.

    /* --------- Glamourer API Event Subscribers -------- */
    public readonly EventSubscriber<nint, StateChangeType> _glamourChanged;

    /* ---------- Glamourer API IPC Subscribers --------- */
    private readonly ApiVersion _ApiVersion; // the API version of glamourer
    private readonly GetState _glamourerGetState;
    private readonly ApplyState _ApplyState; // apply a state to the character using JObject
    private readonly GetDesignList _GetDesignList; // get lists of designs from player's Glamourer.
    private readonly ApplyDesignName _ApplyCustomizationsFromDesignName; // apply customization data from a design.
    private readonly SetItem _SetItem; // for setting an item to the character
    private readonly RevertState _RevertCharacter; // for reverting the character
    private readonly RevertToAutomation _RevertToAutomation; // for reverting the character to automation

    public IpcCallerGlamourer(ILogger<IpcCallerGlamourer> logger,
        IDalamudPluginInterface pluginInterface, IClientState clientState,
        OnFrameworkService OnFrameworkService, GagspeakMediator mediator,
        ItemIdVars itemHelper, IpcFastUpdates fastUpdates) : base(logger, mediator)
    {
        _pi = pluginInterface;
        _onFrameworkService = OnFrameworkService;
        _clientState = clientState;
        _itemHelper = itemHelper;
        _fastUpdates = fastUpdates;

        // set IPC callers
        _ApiVersion = new ApiVersion(_pi);
        _glamourerGetState = new GetState(_pi);
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

    public enum MetaData { Hat, Visor, Both }
    public static bool APIAvailable { get; private set; } = false;

    public void CheckAPI()
    {
        bool apiAvailable = false; // assume false at first
        try
        {
            var version = _ApiVersion.Invoke();
            if (version is { Major: 1, Minor: >= 3 })
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
        Task.Run(async () => await GlamourerRevertToAutomation());
    }

    /// <summary> ========== BEGIN OUR IPC CALL MANAGEMENT UNDER ASYNC TASKS ========== </summary>
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

    public JObject? GetState()
    {
        try
        {
            var success = _glamourerGetState.Invoke(_clientState.LocalPlayer!.ObjectIndex);
            return success.Item2;
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Error during GetState: {ex}");
            return null;
        }
    }

    public bool SetRestraintEquipmentFromState(RestraintSet setToEdit)
    {
        // if the glamourerApi is not active, then return false
        if (!APIAvailable || _onFrameworkService.IsZoning) return false;

        // Get the player state and equipment JObject
        var playerState = GetState();
        var equipment = playerState?["Equipment"];
        if (equipment == null) return false;

        var slots = new[] { "MainHand", "OffHand", "Head", "Body", "Hands", "Legs", "Feet", "Ears", "Neck", "Wrists", "RFinger", "LFinger" };

        // Update each slot
        foreach (var slotName in slots)
        {
            var item = equipment[slotName];
            var equipDrawData = UpdateItem(item, slotName);

            if (equipDrawData != null)
                setToEdit.DrawData[equipDrawData.Slot] = equipDrawData;
        }

        return true;
    }

    private EquipDrawData? UpdateItem(JToken? item, string slotName)
    {
        if (item == null) return null;

        var customItemId = item["ItemId"]?.Value<ulong>() ?? 4294967164;
        var stain = item["Stain"]?.Value<int>() ?? 0;
        var stain2 = item["Stain2"]?.Value<int>() ?? 0;

        StainIds gameStain = new StainIds((StainId)stain, (StainId)stain2);
        return new EquipDrawData(_itemHelper, ItemIdVars.NothingItem((EquipSlot)Enum.Parse(typeof(EquipSlot), slotName)))
        {
            Slot = (EquipSlot)Enum.Parse(typeof(EquipSlot), slotName),
            IsEnabled = true,
            GameItem = _itemHelper.Resolve((EquipSlot)Enum.Parse(typeof(EquipSlot), slotName), new CustomItemId(customItemId)),
            GameStain = gameStain
        };
    }

    public async Task<bool> ForceSetMetaData(MetaData metaData, bool? forcedState = null)
    {
        // if the glamourerApi is not active, then return an empty string for the customization
        if (!APIAvailable || _onFrameworkService.IsZoning || _clientState.LocalPlayer is null) return false;
        try
        {
            return await _onFrameworkService.RunOnFrameworkThread(() =>
            {
                // grab the JObject of the character state.
                var playerState = GetState();

                if (metaData == MetaData.Both || metaData == MetaData.Hat)
                {
                    playerState!["Equipment"]!["Hat"]!["Show"] = forcedState ?? !(((bool?)playerState?["Equipment"]?["Hat"]?["Show"]) ?? false); ;
                    playerState!["Equipment"]!["Hat"]!["Apply"] = true;
                }
                if (metaData == MetaData.Both || metaData == MetaData.Visor)
                {
                    playerState!["Equipment"]!["Visor"]!["IsToggled"] = forcedState ?? !(((bool?)playerState?["Equipment"]?["Visor"]?["IsToggled"]) ?? false); ;
                    playerState!["Equipment"]!["Visor"]!["Apply"] = true;
                }

                var ret = _ApplyState.Invoke(playerState!, 0);
                return ret == GlamourerApiEc.Success;
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Error during SetMetaData: {ex}");
            return false;
        }
    }

    public async Task GlamourerRevertToAutomationEquipOnly()
    {
        if (!APIAvailable || _onFrameworkService.IsZoning) return;

        try
        {
            await _onFrameworkService.RunOnFrameworkThread(() =>
            {
                Logger.LogTrace("Calling on IPC: RevertToAutomationEquipOnly");
                JObject? playerState = GetState();

                // revert player after obtaining state.
                GlamourerRevertToAutomation().ConfigureAwait(false);

                // if the state we grabbed is null, return.
                if (playerState == null)
                {
                    Logger.LogWarning("Failed to get player state. Reverting to Automation with full Appearance.");
                    return;
                }

                // otherwise, get the new state POST-REVERT.
                JObject? newState = GetState();
                if(newState != null)
                {
                    newState["Customize"] = playerState["Customize"];
                    newState["Parameters"] = playerState["Parameters"];
                    newState["Materials"] = playerState["Materials"];
                    // apply the modified "Re-Applied" state.
                    _ApplyState.Invoke(newState, 0);
                }
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Error during GlamourerRevertToAutomationEquipOnly: {ex}");
        }
    }


    public async Task GlamourerRevertToAutomation()
    {
        if (!APIAvailable || _onFrameworkService.IsZoning) return;

        try
        {
            await _onFrameworkService.RunOnFrameworkThread(() =>
            {
                Logger.LogTrace("Calling on IPC: GlamourerRevertToAutomation");
                var result = _RevertToAutomation.Invoke(0, 0);

                // do a fallback to a base reset if the automation fails.
                if (result != GlamourerApiEc.Success)
                {
                    Logger.LogWarning($"Revert to automation failed, reverting to game instead");
                    _RevertCharacter.Invoke(0, 0);
                }
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Error during GlamourerRevertToAutomation: {ex}");
        }
    }

    public async Task GlamourerRevertToGameEquipOnly()
    {
        if (!APIAvailable || _onFrameworkService.IsZoning) return;

        try
        {
            await _onFrameworkService.RunOnFrameworkThread(() =>
            {
                Logger.LogTrace("Calling on IPC: GlamourerRevertToGameEquipOnly");
                JObject? playerState = GetState();

                GlamourerRevertToGame().ConfigureAwait(false);

                if (playerState == null)
                {
                    Logger.LogWarning("Failed to get player state. Reverting full Appearance.");
                    return;
                }

                // otherwise, get the new state POST-REVERT.
                JObject? newState = GetState();
                if (newState != null)
                {
                    newState["Customize"] = playerState["Customize"];
                    newState["Parameters"] = playerState["Parameters"];
                    newState["Materials"] = playerState["Materials"];
                    // apply the modified "Re-Applied" state.
                    _ApplyState.Invoke(newState, 0);
                }
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Error during GlamourerRevertToGame: {ex}");
        }
    }

    public async Task GlamourerRevertToGame()
    {
        if (!APIAvailable || _onFrameworkService.IsZoning) return;

        try
        {
            await _onFrameworkService.RunOnFrameworkThread(() =>
            {
                Logger.LogTrace("Calling on IPC: GlamourerRevertToGame");
                _RevertCharacter.Invoke(0, 0);
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Error during GlamourerRevertToGame: {ex}");
        }
    }

    /// <summary> Fired upon by the IPC event subscriber when the glamourer changes. </summary>
    /// <param name="address"> The address of the character that changed. </param>
    /// <param name="changeType"> The type of change that occurred. </param>"
    private void GlamourerChanged(nint address, StateChangeType changeType)
    {
        // do not accept if coming from other player besides us.
        if (address != _onFrameworkService._playerAddr) return;

        // block if we are not desiring to listen to changes yet.
        if (OnFrameworkService.GlamourChangeEventsDisabled)
        {
            Logger.LogTrace($"GlamourEvent Blocked: {changeType}");
            return;
        }

        // See if the change type is a type we are looking for
        if (changeType is StateChangeType.Design or StateChangeType.Reapply or StateChangeType.Reset or StateChangeType.Equip or StateChangeType.Stains)
        {
            Logger.LogTrace($"StateChangeType is {changeType}");
            _fastUpdates.InvokeGlamourer(GlamourUpdateType.RefreshAll);
            return;
        }
        
        Logger.LogTrace($"GlamourerChanged event was not a type we care about, so skipping (Type was: {changeType})");
    }
}
