using Dalamud.Plugin.Services;
using Dalamud.Game.Text.SeStringHandling;
using OtterGui.Classes;

namespace FFStreamViewer.Utils;
/// <summary> A class for all of the UI helpers, including basic functions for drawing repetative yet unique design elements </summary>
public class FFSVLogHelper {
    private readonly    IChatGui    _chat;

    public FFSVLogHelper(IChatGui chat) {
        _chat = chat;
    }

    // Function Concept: Log a debug message to the debug window
    public void LogDebug(string message, string component) {
        FFStreamViewer.Log.Debug($"[{component}] {message}");
    }

    // Function Concept: Print a spesified error message to the error window
    public void PrintError(string message, string component) {
        FFStreamViewer.Log.Error($"[{component}] {message}");
    }

    // Function Concept: Log a warning/error message to both the chat window and the debug window
    public void LogError(string message, string component) {
        _chat.Print(new SeStringBuilder().AddItalicsOn().AddYellow($"[FFSViewer] ").AddRed($"{message}").AddItalicsOff().BuiltString);
        FFStreamViewer.Log.Error($"[{component}] {message}");
    }
    // Function Concept: Print an spesified information message to the chatbox
    public void PrintInfo(string message, string component) {
        _chat.Print(new SeStringBuilder().AddItalicsOn().AddYellow($"[FFSViewer] ").AddText($"{message}").AddItalicsOff().BuiltString);
        FFStreamViewer.Log.Debug($"[{component}] {message}");
    }

}