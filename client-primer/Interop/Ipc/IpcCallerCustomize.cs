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
using Dalamud.Plugin.Ipc;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using GagSpeak.Interop;
using GagSpeak.Interop.IpcHelpers;

namespace Interop.Ipc;

// NOTICE: THE LOGIC FOR THIS IS INCOMPLETE, BUT AT LEAST FUNCTIONAL


/// <summary>
/// Create a sealed class for our interop manager.
/// </summary>
public sealed class IpcCallerCustomize : DisposableMediatorSubscriberBase, IIpcCaller
{
    /* ------------- Class Attributes ------------- */
    private readonly OnFrameworkService _frameworkUtils;
    private bool _shownCustomizeUnavailable = false; // prevent notifcation spam

    /* --------- Glamourer API Event Subscribers -------- */
    // called when our client updates state of any profile in their customizePlus
    // use this to prevent unwanted sets or disables while restrained.
    private readonly ICallGateSubscriber<ICharacter, Guid, object> _onProfileUpdate;

    /* ---------- Glamourer API IPC Subscribers --------- */
    // MAINTAINERS NOTE: Majority of the IPC calls here are made with the intent for YOU to call upon CUSTOMIZE+ to execute actions for YOURSELF.
    // This means that most of the actions we will call here, are triggered by client callbacks coming from the server forcing us to change something.

    private readonly ICallGateSubscriber<(int, int)> _apiVersion;
    private readonly ICallGateSubscriber<IList<CustomizePlusProfileData>> _getProfileList; // fetches our clients profileList
    private readonly ICallGateSubscriber<ICharacter, (int, Guid?)> _getActiveProfile; // fetches currently active profile.
    private readonly ICallGateSubscriber<Guid, int> _enableProfileByUniqueId; // enabled a particular profile via its GUID
    private readonly ICallGateSubscriber<Guid, int> _disableProfileByUniqueId; // disables a particular profile via its GUID

    public IpcCallerCustomize(ILogger<IpcCallerCustomize> logger,
        IDalamudPluginInterface pluginInterface, IClientState clientState, 
        OnFrameworkService OnFrameworkService, GagspeakMediator mediator) : base(logger, mediator)
    {
        // remember that we made a disposable mediator subscriber base. if we no longer need it when all is said and done, remove it.
        _frameworkUtils = OnFrameworkService;
        
        // setup IPC subscribers
        _apiVersion = pluginInterface.GetIpcSubscriber<(int, int)>("CustomizePlus.General.GetApiVersion");
        _getProfileList = pluginInterface.GetIpcSubscriber<IList<CustomizePlusProfileData>>("CustomizePlus.General.GetProfileList");
        _getActiveProfile = pluginInterface.GetIpcSubscriber<ICharacter, (int, Guid?)>("CustomizePlus.Profile.GetActiveProfileIdOnCharacter");
        _enableProfileByUniqueId = pluginInterface.GetIpcSubscriber<Guid, int>("CustomizePlus.General.EnableProfileByUniqueId");
        _disableProfileByUniqueId = pluginInterface.GetIpcSubscriber<Guid, int>("CustomizePlus.General.DisableProfileByUniqueId");

        // set up event subscribers
        _onProfileUpdate = pluginInterface.GetIpcSubscriber<ICharacter, Guid, object>("CustomizePlus.Profile.OnUpdate");

        // subscribe to events.
        _onProfileUpdate.Subscribe(OnProfileUpdate);
    
        // check API status.
        CheckAPI();
    }

    public bool APIAvailable { get; private set; } = false;

    public void CheckAPI()
    {
        try
        {
            var version = _apiVersion.InvokeFunc();
            APIAvailable = (version.Item1 == 4 && version.Item2 >= 0);
        }
        catch
        {
            APIAvailable = false;
        }
        finally
        {
            _shownCustomizeUnavailable = _shownCustomizeUnavailable && !APIAvailable;

            if (!APIAvailable && !_shownCustomizeUnavailable)
            {
                _shownCustomizeUnavailable = true;

                Mediator.Publish(new NotificationMessage("Glamourer inactive", "Your Glamourer "+
                    "installation is not active or out of date. If you want to interact with modules "+
                    "that use Glamourer, update Glamourer. If you just updated Glamourer, ignore "+
                    "this message.", NotificationType.Warning));
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        _onProfileUpdate.Unsubscribe(OnProfileUpdate);
    }

    public async Task<IList<CustomizePlusProfileData>> GetProfileListAsync()
    {
        Logger.LogInformation("Fetching profile list.");
        // return blank list if no api is available.
        if (!APIAvailable)
        {
            Logger.LogWarning("Customize+ API is not available, returning empty list.");
            return new List<CustomizePlusProfileData>();
        }
        
        // otherwise, return the list of profiles.
        return await _frameworkUtils.RunOnFrameworkThread(() =>
        {
            Logger.LogTrace("IPC-Customize is fetching profile list.");
            return _getProfileList.InvokeFunc();
        }).ConfigureAwait(false);
    }

    public async Task<Guid?> GetActiveProfileAsync(ICharacter c)
    {
        if (!APIAvailable) return Guid.Empty;

        var result = await _frameworkUtils.RunOnFrameworkThread(() =>
        {
            return _getActiveProfile.InvokeFunc(c);
        }).ConfigureAwait(false);
        // log result and return it
        Logger.LogTrace("IPC-Customize obtained active profile [{profile}] with error code [{code}]", result.Item2, result.Item1);
        return result.Item2;
    }

    public async Task EnableProfileAsync(string profileName, Guid profileIdentifier)
    {
        if (!APIAvailable) return;
        await _frameworkUtils.RunOnFrameworkThread(() =>
        {
            Logger.LogTrace("IPC-Customize is enabling profile {profileName} [{profileID}]", profileName, profileIdentifier);   
            _enableProfileByUniqueId!.InvokeAction(profileIdentifier);
        }).ConfigureAwait(false);
    }

    public async Task DisableProfileAsync(string profileName, Guid profileIdentifier)
    {
        if (!APIAvailable) return;
        await _frameworkUtils.RunOnFrameworkThread(() =>
        {
            Logger.LogTrace("IPC-Customize is disabling profile {profileName} [{profileID}]", profileName, profileIdentifier);   
            _disableProfileByUniqueId!.InvokeAction(profileIdentifier);
        }).ConfigureAwait(false);
    }

    private void OnProfileUpdate(ICharacter c, Guid g)
    {
        Logger.LogTrace("IPC-Customize received profile update for character {char} with profile {profile}", c, g);
        Mediator.Publish(new CustomizeProfileChanged());
    }
}
