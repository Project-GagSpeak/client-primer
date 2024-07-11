using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using FFStreamViewer.WebAPI.GagspeakConfiguration;
using FFStreamViewer.WebAPI.Services.Mediator;
using FFStreamViewer.WebAPI.UI;
// using FFStreamViewer.WebAPI.UI.Components.Popup;

namespace FFStreamViewer.WebAPI.Services;

/// <summary> A sealed class dictating the UI service for the plugin. </summary>
public sealed class UiService : DisposableMediatorSubscriberBase
{
    private readonly List<WindowMediatorSubscriberBase> _createdWindows = [];   // the list of created windows as mediator subscribers
    private readonly IUiBuilder _uiBuilder;                                      // the basic dalamud UI builder for the plugin
    private readonly ILogger<UiService> _logger;                                // our logger for the UI service.
    private readonly GagspeakConfigService _gagspeakConfigService;              // our configuration service for the gagspeak plugin
    private readonly WindowSystem _windowSystem;                                // the window system for our dalamud plugin.
    private readonly UiFactory _uiFactory;                                      // the factory for the UI window creation.

    public UiService(ILogger<UiService> logger, IUiBuilder uiBuilder,
        GagspeakConfigService gagspeakConfigService, WindowSystem windowSystem,
        IEnumerable<WindowMediatorSubscriberBase> windows, UiFactory uiFactory,
        GagspeakMediator gagspeakMediator) : base(logger, gagspeakMediator)
    {
        _logger = logger;
        _logger.LogTrace("Creating {type}", GetType().Name);
        _uiBuilder = uiBuilder;
        _gagspeakConfigService = gagspeakConfigService;
        _windowSystem = windowSystem;
        _uiFactory = uiFactory;

        // disable the UI builder while in gpose 
        _uiBuilder.DisableGposeUiHide = true;
        // add the event handlers for the UI builder's draw event
        _uiBuilder.Draw += Draw;
        // subscribe to the UI builder's open config UI event
        _uiBuilder.OpenConfigUi += ToggleUi;
        // subscribe to the UI builder's open main UI event
        _uiBuilder.OpenMainUi += ToggleMainUi;

        // for eachn window in the collection of window mediator subscribers
        foreach (var window in windows)
        {
            // add the window to the window system.
            _windowSystem.AddWindow(window);
        }

        // subscribe to the event message for opening a standalone profile
        Mediator.Subscribe<ProfileOpenStandaloneMessage>(this, (msg) =>
        {
            /*if (!_createdWindows.Exists(p => p is StandaloneProfileUi ui
                && string.Equals(ui.Pair.UserData.AliasOrUID, msg.Pair.UserData.AliasOrUID, StringComparison.Ordinal)))
            {
                var window = _uiFactory.CreateStandaloneProfileUi(msg.Pair);
                _createdWindows.Add(window);
                _windowSystem.AddWindow(window);
            }*/
        });

        // subscribe to the event message for removing a window
        Mediator.Subscribe<RemoveWindowMessage>(this, (msg) =>
        {
            // remove it from the system and the creaed windows list, then dispose of the window.
            _windowSystem.RemoveWindow(msg.Window);
            _createdWindows.Remove(msg.Window);
            msg.Window.Dispose();
        });
    }

    /// <summary>
    /// Method to toggle the main UI for the plugin.
    /// <para>
    /// This will check to see if the user has a valid setup 
    /// (meaning it sees if they are up to date), and will either 
    /// open the introUI or the main window UI
    /// </para>
    /// </summary>
    public void ToggleMainUi()
    {
        if (_gagspeakConfigService.Current.HasValidSetup())
        {
            Mediator.Publish(new UiToggleMessage(typeof(CompactUi)));
        }
        else
        {
            Mediator.Publish(new UiToggleMessage(typeof(IntroUi)));
        }
    }

    /// <summary>
    /// Method to toggle the subset UI for the plugin. AKA Settings window.
    /// <para>
    /// This will check to see if the user has a valid setup
    /// (meaning it sees if they are up to date), and will either
    /// open the settings window UI or the intro UI
    /// </para>
    /// </summary>
    public void ToggleUi()
    {
        if (_gagspeakConfigService.Current.HasValidSetup())
        {
            Mediator.Publish(new UiToggleMessage(typeof(SettingsUi)));
        }
        else
        {
            Mediator.Publish(new UiToggleMessage(typeof(IntroUi)));
        }
    }

    /// <summary> Disposes of the UI service. </summary>
    protected override void Dispose(bool disposing)
    {
        // dispose of the base class
        base.Dispose(disposing);

        _logger.LogTrace("Disposing {type}", GetType().Name);

        // then remove all windows from the windows system
        _windowSystem.RemoveAllWindows();

        // for each of the created windows, dispose of them.
        foreach (var window in _createdWindows)
        {
            window.Dispose();
        }

        // unsubscribe from the draw, open config UI, and main UI
        _uiBuilder.Draw -= Draw;
        _uiBuilder.OpenConfigUi -= ToggleUi;
        _uiBuilder.OpenMainUi -= ToggleMainUi;
    }

    /// <summary> Draw the windows system and file dialogue managers (not file dialogue hopefully) </summary>
    private void Draw()
    {
        _windowSystem.Draw();
    }
}
