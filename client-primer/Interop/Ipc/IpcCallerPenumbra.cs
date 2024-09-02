using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin;
using GagSpeak.Services;
using Penumbra.Api;
using Penumbra.Api.Enums;
using Penumbra.Api.Helpers;
using Penumbra.GameData.Interop;
using Penumbra.GameData.Structs;
using Penumbra.Api.IpcSubscribers;
using Dalamud.Interface.ImGuiNotification;
using GagSpeak.Services.Mediator;
using GagSpeak.UpdateMonitoring;
using GagSpeak.GagspeakConfiguration.Models;
using Lumina.Excel.GeneratedSheets;

namespace GagSpeak.Interop.Ipc;


/// <summary> reads/gets the name and directory name of the mod. </summary>
public readonly record struct Mod(string Name, string DirectoryName) : IComparable<Mod>
{
    public int CompareTo(Mod other)
    {
        var nameComparison = string.Compare(Name, other.Name, StringComparison.Ordinal);
        if (nameComparison != 0)
            return nameComparison;

        return string.Compare(DirectoryName, other.DirectoryName, StringComparison.Ordinal);
    }
}

/// <summary> gets the settings for the mod, including all details about it. </summary>
public readonly record struct ModSettings(Dictionary<string, List<string>> Settings, int Priority, bool Enabled)
{
    public ModSettings() : this(new Dictionary<string, List<string>>(), 0, false) { }

    public static ModSettings Empty
        => new();
}

// the penumbra service that we will use to interact with penumbra
public unsafe class IpcCallerPenumbra : DisposableMediatorSubscriberBase, IIpcCaller
{
    /* ------- Class Attributes ---------- */
    private readonly IDalamudPluginInterface _pi;
    private readonly OnFrameworkService _frameworkService;
    private readonly GagspeakMediator _mediator;
    private bool _shownPenumbraUnavailable = false; // safety net to prevent notification spam.

    /* ------- Penumbra API Event Subscribers ---------- */
    private readonly EventSubscriber _penumbraInitialized;
    private readonly EventSubscriber _penumbraDisposed;
    private readonly EventSubscriber<ChangedItemType, uint> _tooltipSubscriber;
    private readonly EventSubscriber<MouseButton, ChangedItemType, uint> _clickSubscriber;

    /* -------- Penumbra IPC Event Subscribers */
    private RedrawObject? _redrawSubscriber;           // when a target redraws
    private GetModList? _getMods;                      // gets the mod list for our table
    private GetCollection? _currentCollection;         // gets the current collection of our character (0)
    private GetCurrentModSettings? _getCurrentSettings;// we shouldnt need this necessarily  
    private TrySetMod? _setMod;                        // set the mod to be enabled or disabled
    private TrySetModPriority? _setModPriority;        // change the mod priority while active to that it overrides other things
    private ApiVersion _penumbraApiVersion;            // Version of penumbra's API

    public IpcCallerPenumbra(ILogger<IpcCallerPenumbra> logger, 
        IDalamudPluginInterface pi, OnFrameworkService frameworkService, 
        GagspeakMediator mediator) : base(logger, mediator)
    {
        _pi = pi;
        _frameworkService = frameworkService;
        _mediator = mediator;


        _penumbraInitialized = Initialized.Subscriber(pi, PenumbraInitialized);
        _penumbraDisposed = Disposed.Subscriber(pi, PenumbraDisposed);
        
        _tooltipSubscriber = ChangedItemTooltip.Subscriber(pi);
        _clickSubscriber = ChangedItemClicked.Subscriber(pi);

        _penumbraApiVersion = new ApiVersion(pi);

        CheckAPI();
        // possibly remove this. 
        PenumbraInitialized();
    }

    public bool APIAvailable { get; private set; } = false;
    public int API_CurrentMajor { get; private set; }
    public int API_CurrentMinor { get; private set; }
    public const int RequiredPenumbraAPIBreakingVersion = 5;
    public const int RequiredPenumbraAPIFeatureVersion = 0;

    public void CheckAPI()
    {
        try
        {
            try
            {
                (API_CurrentMajor, API_CurrentMinor) = _penumbraApiVersion.Invoke();
            }
            catch
            {
                try
                {
                    (API_CurrentMajor, API_CurrentMinor) = new global::Penumbra.Api.IpcSubscribers.Legacy.ApiVersions(_pi).Invoke();
                }
                catch
                {
                    API_CurrentMajor = 0;
                    API_CurrentMinor = 0;
                    throw;
                }
            }
            // if its broken, dont reattach
            if (API_CurrentMajor != RequiredPenumbraAPIBreakingVersion || API_CurrentMinor < RequiredPenumbraAPIFeatureVersion)
            {
                throw new Exception(
                    $"Invalid Version {API_CurrentMajor}.{API_CurrentMinor:D4}, required major " +
                    $"Version {RequiredPenumbraAPIBreakingVersion} with feature greater or equal to {RequiredPenumbraAPIFeatureVersion}.");
            }
            // API check sucessful.
            APIAvailable = true;
            _shownPenumbraUnavailable = _shownPenumbraUnavailable && !APIAvailable;
        }
        catch // caught by the exception thrown if not compatible.
        {
            if (!APIAvailable && !_shownPenumbraUnavailable)
            {
                _shownPenumbraUnavailable = true;

                _mediator.Publish(new NotificationMessage("Penumbra inactive", "Features using Penumbra will not function properly.", NotificationType.Error));
            }
        }
    }


    public event Action<MouseButton, ChangedItemType, uint> Click
    {
        add => _clickSubscriber.Event += value;
        remove => _clickSubscriber.Event -= value;
    }

    public event Action<ChangedItemType, uint> Tooltip
    {
        add => _tooltipSubscriber.Event += value;
        remove => _tooltipSubscriber.Event -= value;
    }

    // for our get mod list for the table
    public IReadOnlyList<(Mod Mod, ModSettings Settings)> GetMods()
    {
        if (!APIAvailable)
            return Array.Empty<(Mod Mod, ModSettings Settings)>();

        try
        {
            var allMods = _getMods!.Invoke();
            var collection = _currentCollection!.Invoke(ApiCollectionType.Current);
            return allMods
                .Select(m => (m.Key, m.Value, _getCurrentSettings!.Invoke(collection!.Value.Id, m.Key)))
                .Where(t => t.Item3.Item1 is PenumbraApiEc.Success)
                .Select(t => (new Mod(t.Item2, t.Item1),
                    !t.Item3.Item2.HasValue
                        ? ModSettings.Empty
                        : new ModSettings(t.Item3.Item2!.Value.Item3, t.Item3.Item2!.Value.Item2, t.Item3.Item2!.Value.Item1)))
                .OrderByDescending(p => p.Item2.Enabled)
                .ThenBy(p => p.Item1.Name)
                .ThenBy(p => p.Item1.DirectoryName)
                .ThenByDescending(p => p.Item2.Priority)
                .ToList();
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error fetching mods from Penumbra:\n{ex}");
            return Array.Empty<(Mod Mod, ModSettings Settings)>();
        }
    }

    public (Guid Id, string Name) CurrentCollection
        => APIAvailable ? _currentCollection!.Invoke(ApiCollectionType.Current)!.Value : (Guid.Empty, "<Unavailable>"); // gets the current collection type

    /// <summary>
    /// Try to set all mod settings as desired. Only sets when the mod should be enabled.
    /// If it is disabled, ignore all other settings.
    /// </summary>
    public string SetMod(AssociatedMod AssociatedMod, bool newModState)
    {
        if (!APIAvailable)
            return "Penumbra is not available.";

        var sb = new StringBuilder();
        try
        {
            // get the collection of our character
            var collection = _currentCollection!.Invoke(ApiCollectionType.Current)!.Value.Id;
            // create error code, assume success
            var errorCode = PenumbraApiEc.Success;
            // now, if the newsetstate is true, we should enable the mod
            if (newModState == true)
            {
                // enable the mod
                errorCode = _setMod!.Invoke(collection, AssociatedMod.Mod.DirectoryName, true, AssociatedMod.Mod.Name);
                // get the recieved message
                switch (errorCode)
                {
                    case PenumbraApiEc.ModMissing: return $"The mod {AssociatedMod.Mod.Name} [{AssociatedMod.Mod.DirectoryName}] could not be found.";
                    case PenumbraApiEc.CollectionMissing: return $"The collection {collection} could not be found.";
                }
                // after this, raise the priority to 99
                errorCode = _setModPriority!.Invoke(collection, AssociatedMod.Mod.DirectoryName, AssociatedMod.ModSettings.Priority + 50, AssociatedMod.Mod.Name);
                Debug.Assert(errorCode is PenumbraApiEc.Success or PenumbraApiEc.NothingChanged, "Setting Priority should not be able to fail.");
            }
            // otherwise, we are attempting to disable the mod
            else
            {
                // disable the mod, but ONLY if disabledMods is true
                if (AssociatedMod.DisableWhenInactive == true)
                {
                    errorCode = _setMod!.Invoke(collection, AssociatedMod.Mod.DirectoryName, false, AssociatedMod.Mod.Name);
                    // get the recieved message
                    switch (errorCode)
                    {
                        case PenumbraApiEc.ModMissing: return $"The mod {AssociatedMod.Mod.Name} [{AssociatedMod.Mod.DirectoryName}] could not be found.";
                        case PenumbraApiEc.CollectionMissing: return $"The collection {collection} could not be found.";
                    }
                }
                // regardless of if that was on or not, we want to reset it back to their original priority
                errorCode = _setModPriority!.Invoke(collection, AssociatedMod.Mod.DirectoryName, AssociatedMod.ModSettings.Priority, AssociatedMod.Mod.Name);
                Debug.Assert(errorCode is PenumbraApiEc.Success or PenumbraApiEc.NothingChanged, "Setting Priority should not be able to fail.");
            }

            // get the recieved message
            switch (errorCode)
            {
                case PenumbraApiEc.ModMissing: return $"The mod {AssociatedMod.Mod.Name} [{AssociatedMod.Mod.DirectoryName}] could not be found.";
                case PenumbraApiEc.CollectionMissing: return $"The collection {collection} could not be found.";
            }
            // return the invoke message code now built as an SE string
            return sb.ToString();
        }
        catch (Exception ex)
        {
            return sb.AppendLine(ex.Message).ToString();
        }
    }

    /// <summary> 
    /// Try to redraw the given actor. 
    /// We force this method to trigger a immediate redraw from Mare, so it can redraw a player the moment their changes are applied.
    /// This allows animation mods to be updated instantly.
    /// </summary>
    public void RedrawObject(int objectIndex, RedrawType settings)
    {
        Logger.LogDebug($"Redrawing object {objectIndex} due to set toggle!");
        _redrawSubscriber!.Invoke(objectIndex, settings);
    }

    /// <summary> Reattach to the currently running Penumbra IPC provider. Unattaches before if necessary. </summary>
    public void PenumbraInitialized()
    {
        try
        {
            // unattach from the current penumbra to reset subscribers.
            PenumbraDisposed();

            CheckAPI();
            // attach to the penumbra
            _tooltipSubscriber.Enable();
            _clickSubscriber.Enable();
            _redrawSubscriber   = new RedrawObject(_pi);
            _getMods            = new GetModList(_pi);
            _currentCollection  = new GetCollection(_pi);
            _getCurrentSettings = new GetCurrentModSettings(_pi);
            _setMod             = new TrySetMod(_pi);
            _setModPriority     = new TrySetModPriority(_pi);

            _mediator.Publish(new PenumbraInitializedMessage());
        }
        catch (Exception e)
        {
            Logger.LogDebug($"Could not attach to Penumbra:\n{e}");
        }
    }

    /// <summary> Unattach from the currently running Penumbra IPC provider. </summary>
    private void PenumbraDisposed()
    {
        _tooltipSubscriber.Disable();
        _clickSubscriber.Disable();
        if (APIAvailable)
        {
            APIAvailable = false;
            _mediator.Publish(new PenumbraDisposedMessage());
        }
    }


    protected override void Dispose(bool disposing)
    {
        // call disposal of IPC subscribers
        base.Dispose(disposing);

        // call the penumbra dispose to disable the enabled the API Event subscribers
        PenumbraDisposed();
        // dispose of the penumbra event subscribers
        _tooltipSubscriber.Dispose();
        _clickSubscriber.Dispose();
        _penumbraInitialized.Dispose();
        _penumbraDisposed.Dispose();
    }
}
