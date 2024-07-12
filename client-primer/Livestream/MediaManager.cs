using NAudio.Wave;
using System.Numerics;
using FFStreamViewer.WebAPI.Services.Mediator;

namespace FFStreamViewer.Livestream;

public class MediaManager : IDisposable 
{
    byte[] _lastFrame;
    // get the media for the native game audio and the playback stream
    ConcurrentDictionary<string, MediaObject> _playbackStreams = new ConcurrentDictionary<string, MediaObject>();
    ConcurrentDictionary<string, MediaObject> _nativeGameAudio = new ConcurrentDictionary<string, MediaObject>();
    Stopwatch mainPlayerCombatCooldownTimer = new Stopwatch();
    private readonly    ILogger<MediaManager> _logger;
    private readonly    ILoggerFactory      _loggerFactory;
    private             MediaGameObject     _mainPlayer;
    private             MediaCameraObject   _camera;
    private readonly    FFSV_Config         _config;
    private             Task                _updateLoop;               
    private             bool                notDisposed = true;
    private             bool                alreadyConfiguringSound;
    public byte[] LastFrame { get => _lastFrame; set => _lastFrame = value; }

    public event EventHandler OnNewMediaTriggered; // event handler for media being triggered
    public MediaManager(ILogger<MediaManager> logger, ILoggerFactory loggerfactory,
        MediaGameObject mainPlayer, MediaCameraObject camera, FFSV_Config config)
    {
        _logger = logger;
        _loggerFactory = loggerfactory;
        _mainPlayer = mainPlayer;
        _camera = camera;
        _config = config;
        // begin the update loop for the media manager object
        _updateLoop = Task.Run(() => Update());
    }

    public void SetLibVLCPath(string libVLCPath) {
        _config.LibVLCPath = libVLCPath;
        _logger.LogDebug($"LibVLC path set to {_config.LibVLCPath}");
    }

    /// <summary> This function initializes the playstream component of our code. 
    /// <list type="bullet">
    /// <item><c>playerObject</c><param name="playerObject"> - The game player object.</param></item>
    /// <item><c>audioPath</c><param name="audioPath"> - The stream URL path.</param></item>
    /// <item><c>delay</c><param name="delay"> - The delay a stream may have at any point, if any.</param></item> </list></summary>
    public async void PlayStream(MediaGameObject playerObject, string audioPath, int delay = 0) {
        #pragma warning disable CS4014 // Because this call is not awaited, 
        // execution of the current method continues before the call is completed
        Task.Run( () => {
            _logger.LogDebug("PlayStream method called.");
            // trigger the event letting us know we have a new media that was triggered
            OnNewMediaTriggered?.Invoke(this, EventArgs.Empty);
            // not sure why this is here tbh
            if (!string.IsNullOrEmpty(audioPath)) {
                _logger.LogDebug("Audio path is not empty.");
                if (audioPath.StartsWith("rtmp")) {
                    _logger.LogDebug("Audio path starts with rtmp.");
                    foreach (var sound in _playbackStreams) {
                        sound.Value?.Stop();
                    }
                    _playbackStreams.Clear();
                    _logger.LogDebug("Configuring audio for the next stream.");
                    ConfigureAudio(playerObject, audioPath, SoundType.Livestream, _playbackStreams, delay);
                }
            }
            _config.Save();
        });
    }

    public void StopStream() {
        foreach (var sound in _playbackStreams) {
            sound.Value?.Stop();
        }
        _playbackStreams.Clear();
        _config.Save();
    }

    /// <summary>
    /// Function for configuring audio stuff (was async void before) [big one]
    /// <list type="bullet">
    /// <item><c>playerObject</c><param name="playerObject"> - The game player object.</param></item>
    /// <item><c>audioPath</c><param name="audioPath"> - The stream URL path.</param></item>
    /// <item><c>soundType</c><param name="soundType"> - The sound type we are placing the stream audio into.</param></item>
    /// <item><c>sounds</c><param name="sounds"> - The dictionary of media object sounds.</param></item>
    /// <item><c>delay</c><param name="delay"> - The delay a stream may have at any point.</param></item>
    /// <item><c>skipAhead</c><param name="skipAhead"> - The skip ahead to cut forward once we stop having delay.</param></item>
    /// </list></summary> <returns>The newly configured audio.</returns>
    public async void ConfigureAudio(MediaGameObject playerObject, string audioPath, SoundType soundType,
        ConcurrentDictionary<string, MediaObject> sounds, int delay = 0, TimeSpan skipAhead = new TimeSpan()) {
        // Check if we're not already configuring a sound and if the sound type is not MainPlayerCombat or 
        // if it is MainPlayerCombat, then either the cooldown timer has elapsed more than 400 milliseconds or the timer is not running
        if (!alreadyConfiguringSound && (soundType != SoundType.MainPlayerCombat ||
        (soundType == SoundType.MainPlayerCombat && mainPlayerCombatCooldownTimer.ElapsedMilliseconds > 400 || !mainPlayerCombatCooldownTimer.IsRunning))) {
            _logger.LogDebug("ConfigureAudio's initial conditional statement passed. We are able to configure sound now.");
            // Set alreadyconfiguringsound to true while we are configuring the audio.
            alreadyConfiguringSound = true;
            // set our bool tracking if a sound is already playing to false, since we are not playing a sound yet
            bool soundIsPlayingAlready = false;
            // see if the player object is already in the sounds dictionary. If so, log it.
            if (sounds.ContainsKey(playerObject.Name)) {
                _logger.LogDebug("Player already has a sound");
                // If the sound type is MainPlayerVoice or MainPlayerCombat
                if (soundType == SoundType.MainPlayerVoice || soundType == SoundType.MainPlayerCombat) {
                    // Check if the sound is already playing
                    soundIsPlayingAlready = sounds[playerObject.Name].PlaybackState == PlaybackState.Playing;
                    // If the sound type is MainPlayerCombat, restart the cooldown timer
                    if (soundType == SoundType.MainPlayerCombat) {
                        mainPlayerCombatCooldownTimer.Restart();
                    }
                } else {
                    // For other sound types, try to stop the sound. And throw an exception if it cant.
                    try {
                        sounds[playerObject.Name]?.Stop();
                    } catch (Exception e) {
                        // If an error occurs, raise the OnErrorReceived event
                        _logger.LogDebug($"Error occurred while trying to stop sound: {e}");
                    }
                }
            }
            // If the sound is not already playing
            if (!soundIsPlayingAlready) {
                _logger.LogDebug("Sound is not already playing");
                try {
                    // Create a new MediaObject for the sound
                    sounds[playerObject.Name] = new MediaObject(
                        _loggerFactory.CreateLogger<MediaObject>(), this, playerObject, _camera, _config);

                    // set the soundtype to the input soundtype
                    sounds[playerObject.Name]._soundType = soundType;
                    // lock the sound
                    lock (sounds[playerObject.Name]) {
                        // Get the volume for the sound
                        float volume = GetVolume(sounds[playerObject.Name]._soundType, sounds[playerObject.Name]._playerObject);
                        // If the volume is 0, set it to 1 (turn on the audio)
                        if (volume == 0) { volume = 1; }
                        // Start a timer to measure the playback time
                        Stopwatch soundPlaybackTimer = Stopwatch.StartNew();
                        // Play the sound
                        sounds[playerObject.Name].Play(audioPath, volume, delay, skipAhead, _config.LowPerformanceMode);
                        // If the playback time is more than 2 seconds, enable low performance mode
                        if (soundPlaybackTimer.ElapsedMilliseconds > 2000) {
                            _config.LowPerformanceMode = true;
                            _logger.LogError("Low performance detected, enabling low performance mode.");
                        }
                    }
                } catch (Exception e) {
                    // If an error occurs, raise the OnErrorReceived event
                    _logger.LogError($"Error occurred while trying to configure audio: {e}");
                }
            }
            // We're done configuring the sound
            alreadyConfiguringSound = false;
            _logger.LogDebug("Done configuring sound");
        }
    }

    // lacks await method, which might lead to some problems?
    private async void Update() {
        while (notDisposed) {
            UpdateVolumes(_playbackStreams);
            UpdateVolumes(_nativeGameAudio);
            Thread.Sleep(!_config.LowPerformanceMode ? 100 : 400);
        }
    }

    // currently throwing major error
    public void UpdateVolumes(ConcurrentDictionary<string, MediaObject> sounds) {
        // to update our volumes, let's first loop through all of our sounds (which are in a dictionary of media objects)
        try{
            // this is happening endlessly while the plugin is running.
            for (int i = 0; i < sounds.Count; i++) {
                // get the player name from the dictionary at the index
                string playerName = sounds.Keys.ElementAt<string>(i);
                // if the sounds dictionary contains the player name
                if (sounds.ContainsKey(playerName)) {
                    // lock the sound
                    try {
                        lock (sounds[playerName]) {
                            if (sounds[playerName]._playerObject != null) {
                                Vector3 dir = sounds[playerName]._playerObject.Position - _camera.Position;
                                float direction = AngleDir(_camera.Forward, dir, _camera.Top);
                                sounds[playerName].Volume = CalculateObjectVolume(playerName, sounds[playerName]);
                                sounds[playerName].Pan = Math.Clamp(direction / 3, -1, 1);
                            }
                        }
                    }
                    // if we cant, throw exception/ 
                    catch (Exception e) {
                        _logger.LogError($"Error occurred while trying to update volumes (inner): {e}");
                    }
                }
            }
        } catch (Exception e) { 
            _logger.LogError($"Error occurred while trying to update volumes: {e}");
        }
    }

    public float CalculateObjectVolume(string playerName, MediaObject mediaObject) {
        float maxDistance = (playerName == _mainPlayer.Name ||
        mediaObject._soundType == SoundType.Livestream) ? 100 : 20;
        float volume = GetVolume(mediaObject._soundType, mediaObject._playerObject);
        float distance = Vector3.Distance(_camera.Position, mediaObject._playerObject.Position);
        return Math.Clamp(volume * ((maxDistance - distance) / maxDistance), 0f, 1f);
    }

    public float AngleDir(Vector3 fwd, Vector3 targetDir, Vector3 up) {
        Vector3 perp = Vector3.Cross(fwd, targetDir);
        float dir = Vector3.Dot(perp, up);
        return dir;
    }

    public float GetVolume(SoundType soundType, MediaGameObject playerObject) {
        try{
            if (playerObject != null) {
                if (_mainPlayer.FocusedPlayerObject == null ||
                    playerObject.Name == _mainPlayer.Name ||
                    _mainPlayer.FocusedPlayerObject == playerObject.Name) {
                    switch (soundType) {
                        case SoundType.MainPlayerTts:
                            return _config.MainPlayerVolume;
                        case SoundType.Emote:
                        case SoundType.MainPlayerVoice:
                        case SoundType.MainPlayerCombat:
                            return _config.MainPlayerVolume * 1f;
                        case SoundType.OtherPlayerTts:
                        case SoundType.OtherPlayer:
                        case SoundType.OtherPlayerCombat:
                            return _config.OtherPlayerVolume;
                        case SoundType.Loop:
                            return _config.SfxVolume;
                        case SoundType.LoopWhileMoving:
                            return _config.SfxVolume;
                        case SoundType.Livestream:
                            return _config.LiveStreamVolume;
                    }
                } else {
                    switch (soundType) {
                        case SoundType.MainPlayerTts:
                            return _config.MainPlayerVolume;
                        case SoundType.Emote:
                        case SoundType.MainPlayerVoice:
                        case SoundType.MainPlayerCombat:
                            return _config.MainPlayerVolume * 1f;
                        case SoundType.OtherPlayerTts:
                        case SoundType.OtherPlayer:
                        case SoundType.OtherPlayerCombat:
                            return _config.UnfocusedPlayerVolume;
                        case SoundType.Loop:
                            return _config.SfxVolume;
                        case SoundType.LoopWhileMoving:
                            return _config.SfxVolume;
                        case SoundType.Livestream:
                            return _config.LiveStreamVolume;
                    }
                }
            }
        } catch (Exception e) { 
            _logger.LogError($"Error occurred while trying to get volume: {e}");
        }
        return 1;
    }

    public void Dispose() {
        notDisposed = false;
        CleanSounds();
        try {
            if (_updateLoop != null) {
                _updateLoop.Wait();
                _updateLoop?.Dispose();
            }
        } catch (Exception e) {
            _logger.LogError($"Error occurred while trying to dispose: {e}");
        }
    }

    public void CleanNonStreamingSounds() {
        try {
            List<KeyValuePair<string, MediaObject>> cleanupList = new List<KeyValuePair<string, MediaObject>>();
            cleanupList.AddRange(_nativeGameAudio);
            foreach (var sound in cleanupList) {
                if (sound.Value != null) {
                    sound.Value?.Stop();
                }
            }
            _lastFrame = null;
            _nativeGameAudio?.Clear();
        } catch (Exception e) { 
            _logger.LogError($"Error occurred while trying to clean non-streaming sounds: {e}");
        }
    }

    public void CleanSounds() {
        try {
            List<KeyValuePair<string, MediaObject>> cleanupList = new List<KeyValuePair<string, MediaObject>>();
            cleanupList.AddRange(_playbackStreams);
            cleanupList.AddRange(_nativeGameAudio);
            foreach (var sound in cleanupList) {
                if (sound.Value != null) {
                    sound.Value?.Stop();
                }
            }
            _lastFrame = null;
            _playbackStreams?.Clear();
            _nativeGameAudio?.Clear();
        } catch (Exception e) { 
            _logger.LogError($"Error occurred while trying to clean sounds: {e}");
        }
    }
}
