using FFStreamViewer.Audio;
using FFStreamViewer.Events;
using FFStreamViewer.Utils;
using LibVLCSharp.Shared;
using Lumina.Data.Parsing.Scd;
using NAudio.Dmo;
using NAudio.Vorbis;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace FFStreamViewer.Livestream;

public class MediaManager : IDisposable {
    byte[] _lastFrame;
    // get the media for the native game audio and the playback stream
    ConcurrentDictionary<string, MediaObject> _playbackStreams = new ConcurrentDictionary<string, MediaObject>();
    ConcurrentDictionary<string, MediaObject> _nativeGameAudio = new ConcurrentDictionary<string, MediaObject>();
    Stopwatch mainPlayerCombatCooldownTimer = new Stopwatch();
    private             MediaGameObject     _mainPlayer;
    private             MediaCameraObject   _camera;
    private readonly    FFSVLogHelper       _logHelper;
    private readonly    FFSV_Config         _config;
    private             Task                _updateLoop;               
    private             bool                notDisposed = true;
    private             bool                alreadyConfiguringSound;
    public byte[] LastFrame { get => _lastFrame; set => _lastFrame = value; }

    public event EventHandler OnNewMediaTriggered; // event handler for media being triggered
    public MediaManager(MediaGameObject mainPlayer, MediaCameraObject camera, FFSVLogHelper logHelper, FFSV_Config config) {
        _mainPlayer = mainPlayer;
        _camera = camera;
        _logHelper = logHelper;
        _config = config;
        // begin the update loop for the media manager object
        _updateLoop = Task.Run(() => Update());
    }

    public void SetLibVLCPath(string libVLCPath) {
        _config.LibVLCPath = libVLCPath;
        _logHelper.LogDebug($"LibVLC path set to {_config.LibVLCPath}", "Media Manager");
    }

    // public async void PlayAudio(MediaGameObject playerObject, string audioPath, SoundType soundType, int delay = 0, TimeSpan skipAhead = new TimeSpan()) {
    //     _ = Task.Run(() => {
    //         if (!string.IsNullOrEmpty(audioPath)) {
    //             if ((File.Exists(audioPath) && Directory.Exists(Path.GetDirectoryName(audioPath)))) {
    //                 switch (soundType) {
    //                     case SoundType.MainPlayerVoice:
    //                     case SoundType.OtherPlayer:
    //                     case SoundType.Emote:
    //                     case SoundType.Loop:
    //                     case SoundType.LoopWhileMoving:
    //                         ConfigureAudio(playerObject, audioPath, soundType, _voicePackSounds, delay);
    //                         break;
    //                     case SoundType.MainPlayerCombat:
    //                     case SoundType.OtherPlayerCombat:
    //                         ConfigureAudio(playerObject, audioPath, soundType, _combatVoicePackSounds, delay);
    //                         break;
    //                 }
    //             }
    //         }
    //         OnNewMediaTriggered?.Invoke(this, EventArgs.Empty);
    //     });
    // }

    // public async void PlayAudioStream(MediaGameObject playerObject, WaveStream audioStream, SoundType soundType, int delay = 0) {
    //     try {
    //         if (playerObject != null) {
    //             if (_nativeGameAudio.ContainsKey(playerObject.Name)) {
    //                 _nativeGameAudio[playerObject.Name].Stop();
    //             }
    //             _nativeGameAudio[playerObject.Name] = new MediaObject(
    //                 this, playerObject, _camera,
    //                 soundType, "", _config.LibVLCPath);
    //             lock (_nativeGameAudio[playerObject.Name]) {
    //                 float volume = GetVolume(_nativeGameAudio[playerObject.Name].SoundType, _nativeGameAudio[playerObject.Name].PlayerObject);
    //                 _nativeGameAudio[playerObject.Name].OnErrorReceived += MediaManager_OnErrorReceived;
    //                 _nativeGameAudio[playerObject.Name].Play(audioStream, volume, delay);
    //             }
    //         }
    //     } catch (Exception e) {
    //         OnErrorReceived?.Invoke(this, new MediaError() { Exception = e });
    //     }
    // }

    // private void MediaManager_OnErrorReceived(object? sender, MediaError e) {
    //     OnErrorReceived?.Invoke(this, new MediaError() { Exception = e.Exception });
    // }

    /// <summary> This function initializes the playstream component of our code. 
    /// <list type="bullet">
    /// <item><c>playerObject</c><param name="playerObject"> - The game player object.</param></item>
    /// <item><c>audioPath</c><param name="audioPath"> - The stream URL path.</param></item>
    /// <item><c>delay</c><param name="delay"> - The delay a stream may have at any point, if any.</param></item> </list></summary>
    public async void PlayStream(MediaGameObject playerObject, string audioPath, int delay = 0) {
        #pragma warning disable CS4014 // Because this call is not awaited, 
        // execution of the current method continues before the call is completed
        Task.Run( () => {
            _logHelper.LogDebug("PlayStream method called.", "Media Manager");
            // trigger the event letting us know we have a new media that was triggered
            OnNewMediaTriggered?.Invoke(this, EventArgs.Empty);
            // not sure why this is here tbh
            if (!string.IsNullOrEmpty(audioPath)) {
                _logHelper.LogDebug("Audio path is not empty.", "Media Manager");
                if (audioPath.StartsWith("rtmp")) {
                    _logHelper.LogDebug("Audio path starts with rtmp.", "Media Manager");
                    foreach (var sound in _playbackStreams) {
                        sound.Value?.Stop();
                    }
                    _playbackStreams.Clear();
                    _logHelper.LogDebug("Configuring audio for the next stream.", "Media Manager");
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

    // public bool IsAllowedToStartStream(MediaGameObject playerObject) {
    //     if (_playbackStreams.ContainsKey(playerObject.Name)) {
    //         return true;
    //     } else {
    //         if (_playbackStreams.Count == 0) {
    //             return true;
    //         } else {
    //             foreach (string key in _playbackStreams.Keys) {
    //                 bool noStream = _playbackStreams[key].PlaybackState == PlaybackState.Stopped;
    //                 return noStream;
    //             }
    //         }
    //     }
    //     return false;
    // }

    // public void StopAudio(MediaGameObject playerObject) {
    //     if (playerObject != null) {
    //         if (_voicePackSounds.ContainsKey(playerObject.Name)) {
    //             _voicePackSounds[playerObject.Name].Stop();
    //         }
    //         if (_nativeGameAudio.ContainsKey(playerObject.Name)) {
    //             _nativeGameAudio[playerObject.Name].Stop();
    //         }
    //     }
    // }

    // public void LoopEarly(MediaGameObject playerObject) {
    //     if (playerObject != null) {
    //         if (_voicePackSounds.ContainsKey(playerObject.Name)) {
    //             _voicePackSounds[playerObject.Name].LoopEarly();
    //         }
    //         if (_nativeGameAudio.ContainsKey(playerObject.Name)) {
    //             _nativeGameAudio[playerObject.Name].LoopEarly();
    //         }
    //     }
    // }

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
            _logHelper.LogDebug("ConfigureAudio's initial conditional statement passed. We are able to configure sound now.", "Media Manager");
            // Set alreadyconfiguringsound to true while we are configuring the audio.
            alreadyConfiguringSound = true;
            // set our bool tracking if a sound is already playing to false, since we are not playing a sound yet
            bool soundIsPlayingAlready = false;
            // see if the player object is already in the sounds dictionary. If so, log it.
            if (sounds.ContainsKey(playerObject.Name)) {
                _logHelper.LogDebug("Player already has a sound", "Media Manager");
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
                        _logHelper.LogDebug($"Error occurred while trying to stop sound: {e}", "Media Manager");
                    }
                }
            }
            // If the sound is not already playing
            if (!soundIsPlayingAlready) {
                _logHelper.LogDebug("Sound is not already playing", "Media Manager");
                try {
                    // Create a new MediaObject for the sound
                    sounds[playerObject.Name] = new MediaObject( this, playerObject, _camera, _logHelper, _config);
                    sounds[playerObject.Name]._soundType = soundType;           // set the soundtype to the input soundtype
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
                            _logHelper.LogError("Low performance detected, enabling low performance mode.", "Media Manager");
                        }
                    }
                } catch (Exception e) {
                    // If an error occurs, raise the OnErrorReceived event
                    _logHelper.LogError($"Error occurred while trying to configure audio: {e}", "Media Manager");
                }
            }
            // We're done configuring the sound
            alreadyConfiguringSound = false;
            _logHelper.LogDebug("Done configuring sound", "Media Manager");
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
                        _logHelper.LogError($"Error occurred while trying to update volumes (inner): {e}", "Media Manager");
                    }
                }
            }
        } catch (Exception e) { 
            _logHelper.LogError($"Error occurred while trying to update volumes: {e}", "Media Manager");
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
            _logHelper.LogError($"Error occurred while trying to get volume: {e}", "Media Manager");
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
            _logHelper.LogError($"Error occurred while trying to dispose: {e}", "Media Manager");
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
            _logHelper.LogError($"Error occurred while trying to clean non-streaming sounds: {e}", "Media Manager");
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
            _logHelper.LogError($"Error occurred while trying to clean sounds: {e}", "Media Manager");
        }
    }
}