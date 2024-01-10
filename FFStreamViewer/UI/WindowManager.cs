using System;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using Dalamud.Game.Text.SeStringHandling;
using OtterGui.Classes;

namespace FFStreamViewer.UI;
/// <summary> This class is used to handle the window manager. </summary>
public class FFSV_WindowManager : IDisposable
{
    private readonly WindowSystem               _windowSystem = new("FFStreamViewer");
    private readonly UiBuilder                  _uiBuilder;
    private readonly MainWindow                 _ui;
    private readonly IChatGui                   _chatGui;

    /// <summary> Initializes a new instance of the <see cref="FFSV_WindowManager"/> class.
    /// <list type="bullet">
    /// <item><c>uiBuilder</c><param name="uiBuilder"> - The UiBuilder.</param></item>
    /// <item><c>ui</c><param name="ui"> - The main window.</param></item>
    /// <item><c>config</c><param name="config"> - The FFStreamViewer configuration.</param></item>
    /// <item><c>chatGui</c><param name="chatGui"> - The chat GUI.</param></item>
    /// <item><c>uiHistory</c><param name="uiHistory"> - The history window.</param></item>
    /// <item><c>helpPage</c><param name="helpPage"> - The help page tab.</param></item>
    /// <item><c>changelog</c><param name="changelog"> - The changelog.</param></item>
    /// <item><c>userProfile</c><param name="userProfile"> - The user profile window.</param></item>
    /// </list> </summary>
    public FFSV_WindowManager(UiBuilder uiBuilder, MainWindow ui, FFStreamViewerConfig config,
    IChatGui chatGui, DebugWindow uiDebug, FFStreamViewerChangelog changelog) {
        // set the main ui window
        _uiBuilder       = uiBuilder;
        _ui              = ui;          // for the fresh install display
        _chatGui         = chatGui;     // for the fresh install display
        _windowSystem.AddWindow(ui);
        _windowSystem.AddWindow(uiDebug);
        _windowSystem.AddWindow(changelog.Changelog);

        _uiBuilder.Draw                  += _windowSystem.Draw;     // for drawing the UI stuff
        _uiBuilder.OpenConfigUi          += _ui.Toggle;             // for toggling the UI stuff

        //handle a fresh install
        if (config.FreshInstall){
            // They are new, so print some nice messages
            _chatGui.Print(new SeStringBuilder().AddText("Thank you for installing ").AddBlue("FFStreamViewer!").BuiltString);
            _chatGui.Print(new SeStringBuilder().AddYellow("Instructions: ").AddText("You can use ").AddBlue("/FFStreamViewer help ")
                .AddText("to see main functions, ").BuiltString);
            config.FreshInstall = false;
            config.Save();
            _ui.Toggle();
        }
    }

    /// <summary> This function is used to dispose of the window manager. </summary>
    public void Dispose() {
        _uiBuilder.Draw         -= _windowSystem.Draw;
        _uiBuilder.OpenConfigUi -= _ui.Toggle;
    }
}
