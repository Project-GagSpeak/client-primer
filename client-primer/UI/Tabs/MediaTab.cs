using Dalamud.Interface.Utility.Raii;
using ImGuiNET;
using OtterGui.Widgets;
using FFStreamViewer.Utils;
using FFStreamViewer.Livestream;
using Dalamud.Plugin.Services;
using Dalamud.Game.Config;
using Dalamud.Interface.Internal;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFStreamViewer.WebAPI.Services.Mediator;
using Dalamud.Interface.Textures.TextureWraps;


namespace FFStreamViewer.UI.Tabs.MediaTab;
/// <summary> This class is used to handle the general tab for the FFStreamViewer plugin. </summary>
public class MediaTab : ITab, IDisposable
{
    #region General Vairables
    private readonly    ILogger<MediaTab>       _logger;             // the logger for the plugin
    private readonly    ILoggerFactory          _loggerFactory;     // logger factory for constructing MediaManager loggers
    private readonly    FFSV_Config             _config;            // the config for the plugin
    private readonly    IChatGui                _chat;              // the chat service for the plugin
    private readonly    IGameConfig             _gameConfig;        // the game config for the plugin
    private readonly    IClientState            _clientState;       // the client state for the plugin
    private             MediaGameObject         _playerObject;      // the media object (REPLACES IGAMEOBJECT)
    private             MediaManager            _mediaManager;      // the media manager for the plugin
    private readonly    Stopwatch               _streamSetCooldown = new Stopwatch(); // the cooldown for setting the stream
    private             string?                 _tmpWatchLink;      // the language we are translating to
    #endregion General Variables
    #region Video Variables
    private             IDalamudTextureWrap     _textureWrap;       // the texture wrap for the video
    private readonly    ITextureProvider        _textureProvider;   // the texture provider for the texture wraps.
    private             IDalamudPluginInterface _pluginInterface;   // the plugin interface for the plugin
    private readonly    Stopwatch               _deadStreamTimer = new Stopwatch();   // set to determine when a stream is dead
    private             System.Numerics.Vector2?_windowSize;        // the adjustable window size
    private             System.Numerics.Vector2?_initialSize;       // the initial window size
    private bool                                _taskAlreadyRunning = false; // for dumb apiX update
    private byte[]                              _lastFrameBytes;    // the last frame in memory bytes
    #endregion Video Variables
    #region Abstract Attributes
    private unsafe      Camera*                 _camera;
    private             MediaCameraObject       _playerCamera;
    #endregion Abstract Attributes
    public MediaTab(ILogger<MediaTab> logger, ILoggerFactory loggerFactory,
        FFSV_Config config, IChatGui chat, IGameConfig gameConfig,
        IClientState clientState, MediaGameObject playerObject, MediaManager mediaManager,
        IDalamudPluginInterface dalamudPluginInterface, ITextureProvider textureProvider)
    {
        // set the service collection instances
        _logger = logger;
        _config = config;
        _chat = chat;
        _gameConfig = gameConfig;
        _clientState = clientState;
        _textureProvider = textureProvider;
        _playerObject = playerObject;
        _mediaManager = mediaManager;
        _pluginInterface = dalamudPluginInterface;
        _tmpWatchLink = "";
        // set the video instances
        _windowSize = new System.Numerics.Vector2(640, 360); // read below
        _initialSize = new System.Numerics.Vector2(640, 360); // this should be able to be used in a unique way, look into more later

        // call constructor functions
        CheckDependancies(true);
    }

    /// <summary> This function is called when the tab is disposed. </summary>
    public void Dispose() { 
        // Unsubscribe from any events
    }

    // checks and updates any dependancies the feed needs
    unsafe private void CheckDependancies(bool forceNewAssignments = false) {
        // check if our local player in client state is not null.
        // If forceNewAssignments is true, all statements in this will execute regardless.
        if (_clientState.LocalPlayer != null) {
            // if the playerobject is null (true at the start of the plugin) 
            if (_playerObject == null || forceNewAssignments) {
                // create a new playerobject of the clientstate local player
                _playerObject = new MediaGameObject(_clientState.LocalPlayer);
                _logger.LogDebug("New Player Object Created!");
            }
            // if the media manager is null (true at the start of the plugin)
            if (_mediaManager == null || forceNewAssignments) {
                // obtain the camera, and the player (be sure to set the camera object!)
                _camera = CameraManager.Instance()->GetActiveCamera();
                _playerCamera = new MediaCameraObject();
                // set the camera object
                _playerCamera.SetCameraObject(_camera);
                _logger.LogDebug("New Cemera & Player Camera Created!");
                // create a new media manager if it does exist already and we are forcing new assignment
                if (_mediaManager != null) {
                    _logger.LogDebug("The media manager is not null, so we'll create a new one and replace the current.");
                }
                // create the new media manager with the correct info.
                _mediaManager = new MediaManager(
                    _loggerFactory.CreateLogger<MediaManager>(), _loggerFactory, _playerObject, _playerCamera, _config);

                _logger.LogDebug("New Media Manager Created!");
                // set the VLC path
                try{
                    _mediaManager.SetLibVLCPath(Path.GetDirectoryName(_pluginInterface.AssemblyLocation.FullName));
                    _logger.LogDebug("LibVLC Path Set!");
                } catch {
                    _logger.LogError("An error occurred while attempting to set the LibVLC path.");
                }
            }
            // finished our checks!
        }
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
            _logger.LogDebug($"Attempting to tune into stream: {tmpWatchLink}");
            // first, we need to make sure the link contains an RTMP in the link
            if (!tmpWatchLink.Contains("rtmp://")) {
                _logger.LogError("The link you provided is not a valid RTMP link. Please provide a valid RTMP link.");
            }
            // secondly, we need to see if the link sucessfully connected to the other end
            else {
                _logger.LogDebug("our stream had a valid RTMP link, so we will attempt to connect to the server.");
                // Attempt to tune into stream
                try {
                    TuneIntoStream(tmpWatchLink, _playerObject);
                    // if we reached this point, the tune in was sucessful
                    _logger.LogInformation("Tuned into stream successfully! Enjoy!");
                } catch (Exception ex) {
                    _logger.LogError($"An error occurred while attempting to tune into the stream: {ex.Message}");
                }
            }
            _config.Save();
        }
        ImGui.SameLine();
        if (ImGui.Button("Stop Stream")) {
            // stop the stream
            _config.IsStreamPlaying = false;
            _mediaManager.StopStream();
            _config.Save();
            // let the user know the stream has been stopped
            _logger.LogInformation("Stream has been stopped!");
        }

        // below this, we need to draw the video display
        ImGui.Text($"Stream Set Cooldown: {_streamSetCooldown.ElapsedMilliseconds}");
        ImGui.Text($"Dead Stream Timer: {_deadStreamTimer.ElapsedMilliseconds}");
        ImGui.Text($"Texture Wrap: {_textureWrap}");
        if(ImGui.Button("Refresh Dependancies")) { CheckDependancies(true); }
        DrawLivestreamDisplay();

    }
    #endregion MediaTab Draw
    #region Stream Tuning
    private void TuneIntoStream(string url, MediaGameObject audioGameObject) {
        // try and run this task asynchronously
        Task.Run(async () => {
            string cleanedURL = UIHelpers.RemoveSpecialSymbols(url);
            string streamURL = url; //TwitchFeedManager.GetServerResponse(url, TwitchFeedManager.TwitchFeedType._360p);
            _logger.LogDebug($"The stream URL is: {streamURL}");
            if (!string.IsNullOrEmpty(streamURL)) {
                _logger.LogDebug("The stream URL is not null or empty, so we will attempt to play the stream to the window.");
                // if we reached this point, the link is valid and we can tune into the stream
                _mediaManager.PlayStream(audioGameObject, streamURL);
                _config.LastStreamURL = url;
                _chat.Print(@"Tuning into the stream!");
            }
        });
        // set isplaying to true
        _config.IsStreamPlaying = true; // update config accordingly

        try { // attempt to turn on the BGM in the system settings
            _gameConfig.Set(SystemConfigOption.IsSndBgm, true);
        } catch (Exception e) { // if we reached this point, the BGM could not be turned on
            _logger.LogError($"An error occurred while attempting to turn on the BGM: {e.Message}");
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
        if (_mediaManager != null && _mediaManager.LastFrame != null && _mediaManager.LastFrame.Length > 0)
        {
            try
            {
                if (!_taskAlreadyRunning)
                {
                    _ = Task.Run(async () =>
                    {
                        _taskAlreadyRunning = true;
                        ReadOnlyMemory<byte> bytes = null;
                        lock (_mediaManager.LastFrame)
                        {
                            bytes = _mediaManager.LastFrame;
                        }
                        if (_lastFrameBytes.Length > 0)
                        {
                            if (_lastFrameBytes != _mediaManager.LastFrame)
                            {
                                _textureWrap = await _textureProvider.CreateFromImageAsync(_mediaManager.LastFrame);
                                _lastFrameBytes = _mediaManager.LastFrame;
                            }
                        }
                        _taskAlreadyRunning = false;
                    });
                }
                if (_textureWrap != null)
                {
                    ImGui.Image(_textureWrap.ImGuiHandle, new System.Numerics.Vector2(500, 281));
                }
            }
            catch (Exception e)
            {
                _logger.LogError($"An error occurred while attempting to draw the livestream display. {e.Message}");
            }
            // If the dead stream timer is running, stop and reset it
            if (_deadStreamTimer.IsRunning) {
                _deadStreamTimer.Stop();
                _deadStreamTimer.Reset();
            }
            // Indicate that we're currently streaming
        _config.WasStreaming = true;
        } else {
            // If we were previously streaming but aren't anymore
            if (_config.WasStreaming) {
                // If the dead stream timer isn't running, start it
                if (!_deadStreamTimer.IsRunning) {
                    _deadStreamTimer.Start();
                }
                // If the dead stream timer has been running for more than 10 seconds
                if (_deadStreamTimer.ElapsedMilliseconds > 10000) {
                    // Update the FPS count and reset the frame counter
                    _config.FPSCount = _config.CountedFrames + "";
                    _config.CountedFrames = 0;
                    // Stop and reset the dead stream timer
                    _deadStreamTimer.Stop();
                    _deadStreamTimer.Reset();
                    // Indicate that we're no longer streaming
                    _config.WasStreaming = false;
                }
            }
        }
    } catch (Exception e) {
        // If we reached this point, something went wrong
        _logger.LogDebug($"An error occurred while attempting to draw the livestream display. {e.Message}");
    }
}
    #endregion Livestream Display

    // Apply our lable for the tab
    public ReadOnlySpan<byte> Label => "Media"u8;
}

