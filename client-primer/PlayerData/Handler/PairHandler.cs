using GagSpeak.Interop.Ipc;
using GagSpeak.PlayerData.Data;
using GagSpeak.PlayerData.Factories;
using GagSpeak.Services.Events;
using GagSpeak.Services.Mediator;
using GagSpeak.UpdateMonitoring;
using GagSpeak.WebAPI.Utils;
using GagspeakAPI.Data.Character;
using GagspeakAPI.Dto.Connection;
using Microsoft.Extensions.Hosting;
using GagSpeak.UpdateMonitoring;

namespace GagSpeak.PlayerData.Handlers;

/// <summary>
/// The handler for a client pair.
/// </summary>
public sealed class PairHandler : DisposableMediatorSubscriberBase
{
    /// <summary> Record which helps with allowing us to recieve updates for moodles or customize+ updates while in performance mode or combat </summary>
    private sealed record CombatData(Guid ApplicationId, CharacterCompositeData CharacterData, bool Forced);

    private readonly OnFrameworkService _frameworkUtil;                             // frameworkUtil for actions to be done on dalamud framework thread
    private readonly GameObjectHandlerFactory _gameObjectHandlerFactory;            // the game object handler factory
    private readonly IpcManager _ipcManager;                                        // the IPC manager for the pair handler
    private readonly IHostApplicationLifetime _lifetime;                            // the lifetime of the host 
    private Guid _applicationId;                                                    // the unique application id
    private Task? _applicationTask;                                                 // the application task
    private CancellationTokenSource? _applicationCTS = new();

    // the cached data for the paired player. This is where it is stored. Right here. Yup. Not the pair class, here.
    private CharacterIPCData? _cachedData = null;

    // will only need a very basic level of this for now storing minimum data and minimum interactions
    // primarily used for initialization and address checking for visibility
    private GameObjectHandler? _charaHandler;
    private bool _isVisible;                                                        // if the pair is visible

    public PairHandler(ILogger<PairHandler> logger, OnlineUserIdentDto onlineUser,
        GameObjectHandlerFactory gameObjectHandlerFactory, IpcManager ipcManager,
        OnFrameworkService dalamudUtil, IHostApplicationLifetime lifetime,
        GagspeakMediator mediator) : base(logger, mediator)
    {
        OnlineUser = onlineUser;
        _gameObjectHandlerFactory = gameObjectHandlerFactory;
        _ipcManager = ipcManager;
        _frameworkUtil = dalamudUtil;
        _lifetime = lifetime;
        // subscribe to the framework update Message 
        Mediator.Subscribe<FrameworkUpdateMessage>(this, (_) => FrameworkUpdate());
        // other methods were subscribed here, but for now they are being left out until i can understand this more.
    }

    // determines if a paired user is visible. (if they are in renderable range) [ can keep for maybe some featurecreep fun]
    public bool IsVisible
    {
        get => _isVisible;
        private set
        {
            if (_isVisible != value)
            {
                _isVisible = value;
                string text = "User Visibility Changed, now: " + (_isVisible ? "Is Visible" : "Is not Visible");
                // publish an event message to the mediator for logging purposes
                Mediator.Publish(new EventMessage(new Event(PlayerName, OnlineUser.User, nameof(PairHandler),
                    EventSeverity.Informational, text)));
                // publish a refresh ui message to the mediator
                Mediator.Publish(new RefreshUiMessage());
            }
        }
    }

    public OnlineUserIdentDto OnlineUser { get; private set; }  // the online user Dto. Set when pairhandler is made for the cached player in the pair object.
    public nint IPlayerCharacter => _charaHandler?.Address ?? nint.Zero; // the player character object address
    public unsafe uint IPlayerCharacterId => (uint)((_charaHandler?.Address ?? nint.Zero) == nint.Zero  // the player character object id
        ? uint.MaxValue
        : ((FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)_charaHandler!.Address)->GetGameObjectId());
    public string? PlayerName { get; private set; }                                         // the player name
    public string PlayerNameHash => OnlineUser.Ident;                                       // the player name hash

    public override string ToString()
    {
        return OnlineUser == null
            ? base.ToString() ?? string.Empty
            : OnlineUser.User.AliasOrUID + ":" + PlayerName + ":" + (IPlayerCharacter != nint.Zero ? "HasChar" : "NoChar");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        var name = PlayerName;
        Logger.LogDebug("Disposing {name} ({user})", name, OnlineUser);
        try
        {
            Guid applicationId = Guid.NewGuid();
            _applicationCTS?.CancelDispose();
            _applicationCTS = null;
            _charaHandler?.Dispose();
            _charaHandler = null;

            // if the player name is not null or empty, publish an event message to the mediator for logging purposes
            if (!string.IsNullOrEmpty(name))
            {
                Mediator.Publish(new EventMessage(new Event(name, OnlineUser.User, nameof(PairHandler), EventSeverity.Informational, "Disposing User")));
            }

            // if the hosted service lifetime is ending, return
            if (_lifetime.ApplicationStopping.IsCancellationRequested) return;
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Error on disposal of {name}", name);
        }
        finally
        {
            // ensure the player name is null and cachedData gets set to null
            PlayerName = null;
            _cachedData = null;
            Logger.LogDebug("Disposing {name} complete", name);
        }
    }

    /// <summary> Method responsible for applying the stored character data to the paired user.
    /// <para> 
    /// Will first check to see if This includes any IPC calls that are to be made to the character.
    /// At the moment it does not support much else, but easily could.
    /// </para>
    /// <para> Method is the ONLY METHOD which should be calling CallAlterationsToIpcAsync </para>
    /// </summary>
    /// <param name="applicationBase"></param>
    /// <param name="characterData"></param>
    /// <param name="forceApplyCustomization"></param>
    public void ApplyCharacterData(Guid applicationBase, CharacterIPCData characterData)
    {
        // publish the message to the mediator that we are applying character data
        Mediator.Publish(new EventMessage(new Event(PlayerName, OnlineUser.User, nameof(PairHandler), EventSeverity.Informational,
            "Applying Character Data")));

        // check update data to see what character data we will need to update.
        // We pass in _cachedData?.DeepClone() to send what the past data was,
        // so we can compare it against the new data to know if its different.
        var charaDataChangesToUpdate = characterData.CheckUpdatedData(applicationBase, _cachedData?.DeepClone() ?? new(), Logger, this);

        Logger.LogDebug("[BASE-{appbase}] Downloading and applying character for {name}", applicationBase, this);

        // process the changes to the character data (look further into the purpose of the deep cloning at a later time)
        ApplyAlterationsForCharacter(applicationBase, characterData, charaDataChangesToUpdate);
    }


    /// <summary> Method responsible for applying any and all alterations to a paired character.
    /// <para> 
    /// This includes any IPC calls that are to be made to the character.
    /// At the moment it does not support much else, but easily could.
    /// </para>
    /// <para> Method is the ONLY METHOD which should be calling CallAlterationsToIpcAsync </para>
    /// </summary>
    /// <param name="applicationBase">The base of the application for alterations</param>
    /// <param name="charaData">The character data information of the paired user</param>
    /// <param name="updatedData">the kinds of data from the character data to used in the update.</param>
    private void ApplyAlterationsForCharacter(Guid applicationBase, CharacterIPCData charaData, HashSet<PlayerChanges> updatedData)
    {
        if (!updatedData.Any())
        {
            Logger.LogDebug("[BASE-{appBase}] Nothing to update for {obj}", applicationBase, this);
            return;
        }

        // recreate the application cancellation token source
        _applicationCTS = _applicationCTS.CancelRecreate() ?? new CancellationTokenSource();
        var token = _applicationCTS.Token;
        // run the application task in async to apply the customization data to the paired user.
        _applicationTask = Task.Run(async () =>
        {
            // await for the customization data to be applied
            await CallAlterationsToIpcAsync(_applicationId, updatedData, charaData, token).ConfigureAwait(false);
            // throw if canceled
            token.ThrowIfCancellationRequested();

            // update the cachedData 
            _cachedData = charaData;

            Logger.LogDebug("[{applicationId}] Application finished", _applicationId);
        }, token);
    }


    /// <summary> Method that will apply any visual alterations such as moodles onto other paired users.
    /// <para> Primarily responsible for calling upon the various IPC's we have linked when there is a change to be made and apply them</para>
    /// <para> THIS IS TO BE CALLED ONLY FROM THE APPLYDATA FUNCTION</para>
    /// </summary>
    /// <param name="applicationId">the ID of the application being made</param>
    /// <param name="changes">the kinds of changes to be updated onto the paired user</param>
    /// <param name="charaData">the data the user should have altared onto their apperance.</param>
    /// <param name="token">the cancelation token.</param>
    private async Task CallAlterationsToIpcAsync(Guid applicationId, HashSet<PlayerChanges> changes, CharacterIPCData charaData, CancellationToken token)
    {
        // if the player character is zero, return
        if (IPlayerCharacter == nint.Zero) return;
        // set the pointer to the player character we are updating the information for
        var ptr = IPlayerCharacter;
        // set the handler to the characterData of the paired user
        var handler = _charaHandler!;

        // try and apply the alterations to the character data
        try
        {
            // if the handler address is zero, return
            if (handler.Address == nint.Zero) { return; }

            // otherwise, log that we are applying the customization data for the handlers
            Logger.LogDebug("[{applicationId}] Applying Customization Data for {handler}", applicationId, handler);

            // otherwise, for each change in the changes, apply the changes
            foreach (var change in changes.OrderBy(p => (int)p))
            {
                // log that we are processing the change for the handler
                Logger.LogDebug("[{applicationId}] Processing {change} for {handler}", applicationId, change, handler);
                switch (change)
                {
                    case PlayerChanges.Glamourer:
                        break;
                    case PlayerChanges.Moodles:
                        await _ipcManager.Moodles.SetStatusAsync(handler.Address, charaData.MoodlesData).ConfigureAwait(false);
                        break;
                    default:
                        break;
                }
                token.ThrowIfCancellationRequested();
            }
        }
        finally
        {
            if (handler != _charaHandler) handler.Dispose();
        }
    }

    /// <summary> Called every framework update for the pair handler. 
    /// <para> I really dont understand this so you'll have to seriously debug this later.</para>
    /// </summary>
    private void FrameworkUpdate()
    {
        // if the player name is null or empty
        if (string.IsNullOrEmpty(PlayerName))
        {
            // then try and find the name by the online user identity
            var pc = _frameworkUtil.FindPlayerByNameHash(OnlineUser.Ident);

            // Logger.LogTrace("pc pulled from onlineuserIdent: {pc}", pc);
            // if the player character is null, return
            if (pc == default((string, nint))) return;

            // otherwise, call a one-time initialization
            Logger.LogDebug("One-Time Initializing {this}", this);
            // initialize the player character
            Initialize(pc.Name);
            Logger.LogDebug("One-Time Initialized {this}", this);
            // publish an event message to the mediator for logging purposes
            Mediator.Publish(new EventMessage(new Event(PlayerName, OnlineUser.User, nameof(PairHandler), EventSeverity.Informational,
                $"Initializing User For Character {pc.Name}")));
        }

        // if the game object for this pair has a pointer that is not zero (meaning they are present) but the pair is marked as not visible
        if (_charaHandler?.Address != nint.Zero && !IsVisible) // in other words, we apply this the first time they render into our view
        {
            // then we need to create appData for it.
            Guid appData = Guid.NewGuid();
            // and update their visibility to true
            IsVisible = true;
            // publish the pairHandlerVisible message to the mediator, passing in this pair handler object
            Mediator.Publish(new PairHandlerVisibleMessage(this));
            // if the pairs cachedData is not null
            if (_cachedData != null)
            {
                Logger.LogTrace("[BASE-{appBase}] {this} visibility changed, now: {visi}, cached IPC data exists", appData, this, IsVisible);
                // then we should apply it to the character data
                _ = Task.Run(() =>
                {
                    ApplyCharacterData(appData, _cachedData!);
                });
            }
            else
            {
                // otherwise, do not apply it to the character as they are not present
                Logger.LogTrace("{this} visibility changed, now: {visi}, no cached IPC data exists", this, IsVisible);
            }
        }
        // if the player address is 0 but they are visible, invalidate them
        else if (_charaHandler?.Address == nint.Zero && IsVisible)
        {
            // set is visible to false and invalidate the pair handler
            IsVisible = false;
            _charaHandler.Invalidate();
            Logger.LogTrace("{this} visibility changed, now: {visi}", this, IsVisible);
        }
    }

    /// <summary> Initializes a pair handler object </summary>
    private void Initialize(string name)
    {
        Logger.LogTrace("Initializing PairHandler Character {this}", this);
        // set the player name to the name
        PlayerName = name;
        // create a new game object handler for the player character
        Logger.LogTrace("Using Factory to make new GameObjectHandler {this}", this);
        _charaHandler = _gameObjectHandlerFactory.Create(() =>
            _frameworkUtil.GetIPlayerCharacterFromCachedTableByIdent(OnlineUser.Ident), isWatched: false).GetAwaiter().GetResult();
    }


    /// <summary> Method responsible for reverting all alteration data for paired users.
    /// <para> Method is ONLY EVER CALLED IN THE PAIR HANDLERS DISPOSE METHOD </para>
    /// <para> Method will call the IPC's revert calls so all states are put back to normal.</para>
    /// </summary>
    /// <param name="name">the name of the pair to dispose</param>
    /// <param name="applicationId">the ID of the application</param>
    /// <param name="cancelToken">the CancellationToken</param>
    /// <returns></returns>
    private async Task RevertIpcDataAsync(string name, Guid applicationId, CancellationToken cancelToken)
    {
        // get the player character address from the cached table by the pairs online user identity
        nint address = _frameworkUtil.GetIPlayerCharacterFromCachedTableByIdent(OnlineUser.Ident);
        // if the address is zero, return
        if (address == nint.Zero) return;

        Logger.LogDebug("[{applicationId}] Reverting all Customization for {alias}/{name}", applicationId, OnlineUser.User.AliasOrUID, name);

        Logger.LogDebug("[{applicationId}] Restoring Moodles for {alias}/{name}", applicationId, OnlineUser.User.AliasOrUID, name);
        await _ipcManager.Moodles.RevertStatusAsync(address).ConfigureAwait(false);
    }
}
