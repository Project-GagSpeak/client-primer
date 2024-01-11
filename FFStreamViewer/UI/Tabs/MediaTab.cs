using System;
using System.Numerics;
using System.Linq;
using System.Collections.Generic;
using Dalamud.Interface.Utility.Raii;
using ImGuiNET;
using OtterGui;
using OtterGui.Widgets;
using FFStreamViewer.Services;
using FFStreamViewer.Utils;
using FFStreamViewer.Livestream;
using Dalamud.Plugin.Services;
using System.Diagnostics;
using Dalamud.Game.Config;
using Dalamud.Interface.Internal;
using Dalamud.Plugin;
using System.Drawing;
using LibVLCSharp.Shared;
using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;

namespace FFStreamViewer.UI.Tabs.MediaTab;
/// <summary> This class is used to handle the general tab for the FFStreamViewer plugin. </summary>
public class MediaTab : ITab, IDisposable
{
    #region General Vairables
    private readonly FFSV_Config            _config;            // the config for the plugin
    private readonly FFSVLogHelper          _logHelper;         // the log helper for the plugin
    private readonly IChatGui               _chat;              // the chat service for the plugin
    private readonly IGameConfig            _gameConfig;        // the game config for the plugin
    private readonly MediaGameObject        _playerObject;      // the media object for the plugin
    private readonly MediaManager           _mediaManager;      // the media manager for the plugin
    private readonly Stopwatch              _streamSetCooldown; // the cooldown for setting the stream
    private          string?                _tmpWatchLink;      // the language we are translating to
    private          bool                   _isStreamPlaying;   // is the stream playing?
    private          string                 _lastStreamURL;     // the last stream URL
    #endregion General Variables
    #region Video Variables
    IDalamudTextureWrap                     _textureWrap;       // the texture wrap for the video
    private          DalamudPluginInterface _pluginInterface;   // the plugin interface for the plugin
    private readonly Stopwatch              _deadStreamTimer;   // set to determine when a stream is dead
    private          Vector2?               _windowSize;        // the adjustable window size
    private          Vector2?               _initialSize;       // the initial window size
    private          string                 _fpsCount;          // the FPS of the media
    private          int                    _countedFrames;     // the counted frames of the media
    private          bool                   _wasStreaming;      // was the stream playing?
    #endregion Video Variables
    /// <summary>
    /// Initializes a new instance of the <see cref="MediaTab"/> class.
    /// </summary>
    public MediaTab(FFSV_Config config, FFSVLogHelper logHelper, IChatGui chat, IGameConfig gameConfig,
    MediaGameObject playerObject, Stopwatch streamSetCooldown, MediaManager mediaManager,
    DalamudPluginInterface dalamudPluginInterface, IDalamudTextureWrap textureWrap, Stopwatch deadStreamTimer) {
        // set the service collection instances
        _config = config;
        _logHelper = logHelper;
        _chat = chat;
        _gameConfig = gameConfig;
        _playerObject = playerObject;
        _streamSetCooldown = streamSetCooldown;
        _mediaManager = mediaManager;
        _tmpWatchLink = "";
        _isStreamPlaying = false;
        _lastStreamURL = "";

        // set the video related attirubtes
        _textureWrap = textureWrap;
        _pluginInterface = dalamudPluginInterface;
        _deadStreamTimer = deadStreamTimer;
        _windowSize = new Vector2(640, 360); // read below
        _initialSize = new Vector2(640, 360); // this should be able to be used in a unique way, look into more later
        // something about size condition was in the original code, but I don't know what it was for
        // also was something about setting position? idk
        _fpsCount = "0";
        _countedFrames = 0;
        _wasStreaming = false;
    }

    /// <summary> This function is called when the tab is disposed. </summary>
    public void Dispose() { 
        // Unsubscribe from any events
    }

    #region MediaTab Draw
    /// <summary> This Function draws the content for the Media Tab </summary>
    public void DrawContent() {
        // Create a child for the Main Window (not sure if we need this without a left selection panel)
        using var child = ImRaii.Child("MainWindowChild");
        if (!child)
            return;
        // Draw the child grouping for the ConfigSettings Tab
        using (var child2 = ImRaii.Child("MediaTabChild")) {
            DrawHeader();
            DrawGeneral();
        }
    }

    private void DrawHeader()
        => WindowHeader.Draw("Stream Viewer Window", 0, ImGui.GetColorU32(ImGuiCol.FrameBg));

    /// <summary> This function draws the media tab contents 
    /// <para> FUTURE CONCEPT: Have a dropdown to spesify certain stream link types.
    /// However, this leads to complications </para> 
    /// </summary>
    private void DrawGeneral() {
        // define the message
        var tmpWatchLink  = _tmpWatchLink ?? _config.WatchLink; // temp storage to hold until we de-select the text input
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X-250);
        if (ImGui.InputText("##WatchLinkInput", ref tmpWatchLink, 400, ImGuiInputTextFlags.None))
            _tmpWatchLink = tmpWatchLink;
        if(ImGui.IsItemDeactivatedAfterEdit()) {
            _config.WatchLink = tmpWatchLink;
            _tmpWatchLink     = null;
            _config.Save();
        }
        ImGui.SameLine();
        if (ImGui.Button("Watch Stream")) {
            // first, we need to make sure the link contains an RTMP in the link
            if (!tmpWatchLink.Contains("rtmp://")) {
                _logHelper.PrintError("The link you provided is not a valid RTMP link. Please provide a valid RTMP link.", "Media Tab");
            }
            // secondly, we need to see if the link sucessfully connected to the other end
            else {
                // Attempt to tune into stream
                try {
                    TuneIntoStream(tmpWatchLink, _playerObject);
                } catch (Exception ex) {
                    _logHelper.PrintError($"An error occurred while attempting to tune into the stream: {ex.Message}", "Media Tab");
                }
                // if we reached this point, the tune in was sucessful
                _logHelper.PrintInfo("Tuned into stream sucessfully! Enjoy!", "Media Tab");
            }
        }
        ImGui.SameLine();
        if (ImGui.Button("Stop Stream")) {
            // stop the stream
            _mediaManager.StopStream();
            // let the user know the stream has been stopped
            _logHelper.PrintInfo("Stream has been stopped!", "Media Tab");
        }

        // below this, we need to draw the video display
        DrawLivestreamDisplay();

    }
    #endregion MediaTab Draw
    #region Stream Tuning
    private void TuneIntoStream(string url, IGameObject audioGameObject) {
        // first, we need to make sure the link contains an RTMP in the link
        string streamURL = url; //TwitchFeedManager.GetServerResponse(url, TwitchFeedManager.TwitchFeedType._360p);
        if (!string.IsNullOrEmpty(streamURL)) {
            // if we reached this point, the link is valid and we can tune into the stream
            _mediaManager.PlayStream(audioGameObject, streamURL);
            _lastStreamURL = url;
            _chat.Print(@"Tuning into the stream!");
        }
        // set isplaying to true
        _isStreamPlaying = true;

        try { // attempt to turn on the BGM in the system settings
            _gameConfig.Set(SystemConfigOption.IsSndBgm, true);
        } catch (Exception e) { // if we reached this point, the BGM could not be turned on
            _logHelper.PrintError($"An error occurred while attempting to turn on the BGM: {e.Message}", "Media Tab");
        }

        // stop the cooldown, reset it, then start it again
        _streamSetCooldown.Stop();
        _streamSetCooldown.Reset();
        _streamSetCooldown.Start();
    }
    #endregion Stream Tuning

    #region Livestream Display
public void DrawLivestreamDisplay() {
    try {
        // Check if the media manager exists and has a valid last frame
        if (_mediaManager != null && _mediaManager.LastFrame != null && _mediaManager.LastFrame.Length > 0) {
            // Lock the last frame to prevent other threads from modifying it while we're using it
            lock (_mediaManager.LastFrame) {
                // Load the last frame as an image
                _textureWrap = _pluginInterface.UiBuilder.LoadImage(_mediaManager.LastFrame);
                // Draw the image in the ImGui interface (may be able to make this interchangable in size but right now it looks limited)
                ImGui.Image(_textureWrap.ImGuiHandle, new Vector2(500, 281));
            }
            // If the dead stream timer is running, stop and reset it
            if (_deadStreamTimer.IsRunning) {
                _deadStreamTimer.Stop();
                _deadStreamTimer.Reset();
            }
            // Indicate that we're currently streaming
            _wasStreaming = true;
        } else {
            // If we were previously streaming but aren't anymore
            if (_wasStreaming) {
                // If the dead stream timer isn't running, start it
                if (!_deadStreamTimer.IsRunning) {
                    _deadStreamTimer.Start();
                }
                // If the dead stream timer has been running for more than 10 seconds
                if (_deadStreamTimer.ElapsedMilliseconds > 10000) {
                    // Update the FPS count and reset the frame counter
                    _fpsCount = _countedFrames + "";
                    _countedFrames = 0;
                    // Stop and reset the dead stream timer
                    _deadStreamTimer.Stop();
                    _deadStreamTimer.Reset();
                    // Indicate that we're no longer streaming
                    _wasStreaming = false;
                }
            }
        }
    } catch (Exception e) {
        // If we reached this point, something went wrong
        _logHelper.LogDebug($"An error occurred while attempting to draw the livestream display. {e.Message}", "Media Tab");
    }
}
    #endregion Livestream Display

    // Apply our lable for the tab
    public ReadOnlySpan<byte> Label => "General"u8;
}

