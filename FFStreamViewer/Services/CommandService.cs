using System;
using System.Linq;
using System.Text.RegularExpressions;
using Dalamud.Game.Command;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using OtterGui.Classes;
using FFStreamViewer.UI;
using FFStreamViewer.UI.Helpers;

namespace FFStreamViewer.Services;

/// <summary> The command manager for the plugin. </summary>
public class CommandManager : IDisposable // Our main command list manager
{
    private const string MainCommandString = "/ffsv"; // The primary command used for & displays
    private const string ActionsCommandString = "/ffstreamviewer"; // subcommand for more in-depth actions.
    private readonly    ICommandManager     _commands;
    private readonly    MainWindow          _mainWindow;
    private readonly    DebugWindow         _debugWindow;
    private readonly    IChatGui            _chat;
    private readonly    FFStreamViewerConfig      _config;
    private readonly    IClientState        _clientState;
    private readonly    IFramework          _framework; 

    // Constructor for the command manager
    public CommandManager(ICommandManager command, MainWindow mainwindow, DebugWindow debugWindow, IChatGui chat,
    FFStreamViewerConfig config, IClientState clientState, IFramework framework)
    {
        // set the private readonly's to the passed in data of the respective names
        _commands = command;
        _mainWindow = mainwindow;
        _debugWindow = debugWindow;
        _chat = chat;
        _config = config;
        _clientState = clientState;
        _framework = framework;

        // Add handlers to the main commands
        _commands.AddHandler(MainCommandString, new CommandInfo(OnFFStreamViewer) {
            HelpMessage = "Toggles main UI.",
            ShowInHelp = true
        });
        _commands.AddHandler(ActionsCommandString, new CommandInfo(OnFFSV) {
            HelpMessage = "Displays the list of FFStreamViewer commands. Effectively the help command.",
            ShowInHelp = true
        });

        FFStreamViewer.Log.Debug("[Command Manager] Constructor Finished Initializing");
    }

    // Dispose of the command manager
    public void Dispose() {
        _commands.RemoveHandler(MainCommandString);
        _commands.RemoveHandler(ActionsCommandString);
    }

    // Handler for the main FFStreamViewer command
    private void OnFFStreamViewer(string command, string arguments) {
        _mainWindow.Toggle(); // when [/FFStreamViewer] is typed, toggle the main window
    }


    // On the gag command
    private void OnFFSV(string command, string arguments) {
        var argumentList = arguments.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (argumentList.Length < 1) {
            PrintHelpFFSV("?");
            return;
        }
        // Below accounts for arguements for any future command usage
        /*
        var argument = argumentList.Length == 2 ? argumentList[1] : string.Empty;
        var _ = argumentList[0].ToLowerInvariant() switch
        {
            "1"         => GagApply(argumentList), // map to GagApply function
            "2"         => GagApply(argumentList), // map to GagApply function
            "3"         => GagApply(argumentList), // map to GagApply function
            "lock"      => GagLock(argument),      // map to GagLock function 
            "unlock"    => GagUnlock(argument),    // map to GagUnlock function
            "remove"    => GagRemove(argument),    // map to GagRemove function
            "removeall" => GagRemoveAll(argument), // map to GagRemoveAll function
            _           => PrintHelpGag("?"),      // if we didn't type help or ?, print the error
        };*/
    }

    private bool PrintHelpFFSV(string argument) { // Primary help command
        // print header for help
        _chat.Print(new SeStringBuilder().AddYellow(" -- Arguments for /FFStreamViewer --").BuiltString);
        // print command arguements
        _chat.Print(new SeStringBuilder().AddCommand("None", "Placeholder text.").BuiltString);
        _chat.Print(new SeStringBuilder().AddCommand("None", "Placeholder text.").BuiltString);
        _chat.Print(new SeStringBuilder().AddCommand("None", "Placeholder text.").BuiltString);
        return true;
    }
}
