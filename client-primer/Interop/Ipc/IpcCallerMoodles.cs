using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using GagSpeak.Services.Mediator;
using GagSpeak.UpdateMonitoring;

namespace GagSpeak.Interop.Ipc;

public sealed class IpcCallerMoodles : IIpcCaller
{
    // TERMINOLOGY:
    // StatusManager == The manager handling the current active statuses on you.
    // Status == The invidual "Moodle" in your Moodles tab under the Moodles UI.
    // Preset == The collection of Statuses to apply at once. Stored in a preset.
    
    // Remember, all these are called only when OUR client changes. Not other pairs.
    private readonly ICallGateSubscriber<int> _moodlesApiVersion;
    
    private readonly ICallGateSubscriber<IPlayerCharacter, object> _onStatusManagerModified;
    private readonly ICallGateSubscriber<Guid, object> _onStatusSettingsModified;
    private readonly ICallGateSubscriber<Guid, object> _onPresetModified;

    // API Getter Functions
    private readonly ICallGateSubscriber<Guid, MoodlesStatusInfo> _getMoodleInfo;
    private readonly ICallGateSubscriber<List<MoodlesStatusInfo>> _getMoodlesInfo;
    private readonly ICallGateSubscriber<Guid, (Guid, List<Guid>)> _getPresetInfo;
    private readonly ICallGateSubscriber<List<(Guid, List<Guid>)>> _getPresetsInfo;
    private readonly ICallGateSubscriber<nint, string> _moodlesGetStatus;

    // API Enactor Functions
    private readonly ICallGateSubscriber<nint, string, object> _moodlesSetStatus;
    private readonly ICallGateSubscriber<nint, object> _moodlesRevertStatus;


    private readonly ILogger<IpcCallerMoodles> _logger;
    private readonly OnFrameworkService _frameworkUtil;
    private readonly GagspeakMediator _gagspeakMediator;
    private bool _shownMoodlesUnavailable = false; // safety net to prevent notification spam.

    public IpcCallerMoodles(ILogger<IpcCallerMoodles> logger, IDalamudPluginInterface pi,
        OnFrameworkService frameworkUtil, GagspeakMediator gagspeakMediator)
    {
        _logger = logger;
        _frameworkUtil = frameworkUtil;
        _gagspeakMediator = gagspeakMediator;

        _moodlesApiVersion = pi.GetIpcSubscriber<int>("Moodles.Version");

        // TODO: Change nint to name@world later.
        // API Getter Functions
        _getMoodleInfo = pi.GetIpcSubscriber<Guid, MoodlesStatusInfo>("Moodles.GetRegisteredMoodleInfo");
        _getMoodlesInfo = pi.GetIpcSubscriber<List<MoodlesStatusInfo>>("Moodles.GetRegisteredMoodlesInfo");
        _getPresetInfo = pi.GetIpcSubscriber<Guid, (Guid, List<Guid>)>("Moodles.GetRegisteredPresetInfo");
        _getPresetsInfo = pi.GetIpcSubscriber<List<(Guid, List<Guid>)>>("Moodles.GetRegisteredPresetsInfo");
        _moodlesGetStatus = pi.GetIpcSubscriber<nint, string>("Moodles.GetStatusManagerByPtr");
        
        // API Enactor Functions
        _moodlesSetStatus = pi.GetIpcSubscriber<nint, string, object>("Moodles.SetStatusManagerByPtr");
        _moodlesRevertStatus = pi.GetIpcSubscriber<nint, object>("Moodles.ClearStatusManagerByPtr");


        // API Action Events:
        _onStatusManagerModified = pi.GetIpcSubscriber<IPlayerCharacter, object>("Moodles.StatusManagerModified");
        _onStatusSettingsModified = pi.GetIpcSubscriber<Guid, object>("Moodles.StatusModified");
        _onPresetModified = pi.GetIpcSubscriber<Guid, object>("Moodles.PresetModified");

        _onStatusManagerModified.Subscribe(OnStatusManagerModified); // fires whenever our client's status manager changes.
        _onStatusSettingsModified.Subscribe(OnStatusModified); // fires whenever our client's changes the settings of a Moodle.
        _onPresetModified.Subscribe(OnPresetModified); // fires whenever our client's changes the settings of a Moodle preset.

        CheckAPI(); // check to see if we have a valid API
    }

    /// <summary> This method is called when the moodles change </summary>
    /// <param name="character">The character that had modified moodles.</param>
    private void OnStatusManagerModified(IPlayerCharacter character) => 
        _gagspeakMediator.Publish(new MoodlesStatusManagerChangedMessage(character.Address));

    /// <summary> This method is called when the moodles change </summary>
    private void OnStatusModified(Guid guid) 
        => _gagspeakMediator.Publish(new MoodlesStatusModified(guid));

    /// <summary> This method is called when the moodles change </summary>
    private void OnPresetModified(Guid guid)
        => _gagspeakMediator.Publish(new MoodlesPresetModified(guid));


    /// <summary> this boolean determines if the moodles API is available or not.</summary>
    public bool APIAvailable { get; private set; } = false;

    /// <summary> This method checks if the API is available </summary>
    public void CheckAPI()
    {
        try
        {
            APIAvailable = _moodlesApiVersion.InvokeFunc() >= 1;
        }
        catch
        {
            APIAvailable = false;
        }
    }

    /// <summary> This method disposes of the IPC caller moodles</summary>
    public void Dispose()
    {
        _onStatusManagerModified.Unsubscribe(OnStatusManagerModified);
        _onStatusSettingsModified.Unsubscribe(OnStatusModified);
        _onPresetModified.Unsubscribe(OnPresetModified);
    }

    /// <summary> This method gets the moodles info for a provided GUID from the client. </summary>
    public async Task<MoodlesStatusInfo?> GetMoodleInfoAsync(Guid guid)
    {
        if (!APIAvailable) return null; // return if the API isnt available

        try // otherwise, try and return an awaited task that gets the moodles info for a provided GUID
        {
            return await _frameworkUtil.RunOnFrameworkThread(() => _getMoodleInfo.InvokeFunc(guid)).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            // log it if we failed.
            _logger.LogWarning(e, "Could not Get Moodles Info");
            return null;
        }
    }

    /// <summary> This method gets the list of all our clients Moodles Info </summary>
    public async Task<List<MoodlesStatusInfo>?> GetMoodlesInfoAsync()
    {
        if (!APIAvailable) return null; // return if the API isnt available

        try // otherwise, try and return an awaited task that gets the list of all our clients Moodles Info
        {
            return await _frameworkUtil.RunOnFrameworkThread(() => _getMoodlesInfo.InvokeFunc()).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            // log it if we failed.
            _logger.LogWarning(e, "Could not Get Moodles Info");
            return null;
        }
    }

    /// <summary> This method gets the preset info for a provided GUID from the client. </summary>
    public async Task<(Guid, List<Guid>)?> GetPresetInfoAsync(Guid guid)
    {
        if (!APIAvailable) return null; // return if the API isnt available

        try // otherwise, try and return an awaited task that gets the preset info for a provided GUID
        {
            return await _frameworkUtil.RunOnFrameworkThread(() => _getPresetInfo.InvokeFunc(guid)).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            // log it if we failed.
            _logger.LogWarning(e, "Could not Get Moodles Preset Info");
            return null;
        }
    }

    /// <summary> This method gets the list of all our clients Presets Info </summary>
    public async Task<List<(Guid, List<Guid>)>?> GetPresetsInfoAsync()
    {
        if (!APIAvailable) return null; // return if the API isnt available

        try // otherwise, try and return an awaited task that gets the list of all our clients Presets Info
        {
            return await _frameworkUtil.RunOnFrameworkThread(() => _getPresetsInfo.InvokeFunc()).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            // log it if we failed.
            _logger.LogWarning(e, "Could not Get Moodles Presets Info");
            return null;
        }
    }



    /// <summary> This method gets the status of the moodles for a partiular address</summary>
    public async Task<string?> GetStatusAsync(nint address)
    {
        if (!APIAvailable) return null; // return if the API isnt available

        try // otherwise, try and return an awaited task that gets the status of the moodles for a particular address
        {
            return await _frameworkUtil.RunOnFrameworkThread(() => _moodlesGetStatus.InvokeFunc(address)).ConfigureAwait(false);

        }
        catch (Exception e)
        {
            // log it if we failed.
            _logger.LogWarning(e, "Could not Get Moodles Status");
            return null;
        }
    }

    /// <summary> Sets the moodles status for a gameobject spesified by the pointer</summary>
    /// <para> This will be what allows us to forcible set moodles to other players. </para>
    /// </summary>
    /// <param name="pointer">the pointer address of the player to set the status for</param>
    /// <param name="status">the moodles status information to apply</param>
    /// <returns></returns>
    public async Task SetStatusAsync(nint pointer, string status)
    {
        // if the API is not available, return
        if (!APIAvailable) return;
        try
        {
            await _frameworkUtil.RunOnFrameworkThread(() => _moodlesSetStatus.InvokeAction(pointer, status)).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "Could not Set Moodles Status");
        }
    }

    /// <summary> Reverts the status of the moodles for a gameobject spesified by the pointer</summary>
    /// <param name="pointer">the pointer address of the player to revert the status for</param>
    public async Task RevertStatusAsync(nint pointer)
    {
        if (!APIAvailable) return;
        try
        {
            await _frameworkUtil.RunOnFrameworkThread(() => _moodlesRevertStatus.InvokeAction(pointer)).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "Could not Set Moodles Status");
        }
    }
}
