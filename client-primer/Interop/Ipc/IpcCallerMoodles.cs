using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using GagSpeak.Services.Mediator;
using GagSpeak.UpdateMonitoring;

namespace GagSpeak.Interop.Ipc;

public sealed class IpcCallerMoodles : IIpcCaller
{
    private readonly ICallGateSubscriber<int> _moodlesApiVersion;
    private readonly ICallGateSubscriber<IPlayerCharacter, object> _moodlesOnChange;
    private readonly ICallGateSubscriber<nint, string> _moodlesGetStatus;
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
        _moodlesOnChange = pi.GetIpcSubscriber<IPlayerCharacter, object>("Moodles.StatusManagerModified");
        _moodlesGetStatus = pi.GetIpcSubscriber<nint, string>("Moodles.GetStatusManagerByPtr");
        _moodlesSetStatus = pi.GetIpcSubscriber<nint, string, object>("Moodles.SetStatusManagerByPtr");
        _moodlesRevertStatus = pi.GetIpcSubscriber<nint, object>("Moodles.ClearStatusManagerByPtr");

        _moodlesOnChange.Subscribe(OnMoodlesChange); // listen for any time when anyone changes their moodles.

        CheckAPI(); // check to see if we have a valid API
    }

    /// <summary> This method is called when the moodles change </summary>
    /// <param name="character">The character that had modified moodles.</param>
    private void OnMoodlesChange(IPlayerCharacter character)
    {
        // publish a new moodles message with the playercharacters address pointer.
        _gagspeakMediator.Publish(new MoodlesMessage(character.Address));
    }

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
        _moodlesOnChange.Unsubscribe(OnMoodlesChange);
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
