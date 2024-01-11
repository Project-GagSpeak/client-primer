using FFStreamViewer.Audio;
using FFStreamViewer.Events;
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
using System.Threading.Tasks;

namespace FFStreamViewer.Livestream;

public class MediaManager : IDisposable {
    byte[] _lastFrame;
    // get the media for the native game audio and the playback stream
    ConcurrentDictionary<string, MediaObject> _nativeGameAudio = new ConcurrentDictionary<string, MediaObject>();
    ConcurrentDictionary<string, MediaObject> _playbackStreams = new ConcurrentDictionary<string, MediaObject>();

    public event EventHandler<MediaError> OnErrorReceived;
    private IGameObject _mainPlayer = null;
    private IGameObject _camera = null;
    private LoopStream _loopStream;
    private string _libVLCPath;
    private Task _updateLoop;
    float _mainPlayerVolume = 1.0f;
    float _otherPlayerVolume = 1.0f;
    float _unfocusedPlayerVolume = 1.0f;
    float _sfxVolume = 1.0f;
    private bool notDisposed = true;
    private float _liveStreamVolume = 1;
    private bool alreadyConfiguringSound;
    Stopwatch mainPlayerCombatCooldownTimer = new Stopwatch();
    private bool _lowPerformanceMode;

    public float MainPlayerVolume { get => _mainPlayerVolume; set => _mainPlayerVolume = value; }
    public float OtherPlayerVolume { get => _otherPlayerVolume; set => _otherPlayerVolume = value; }
    public float UnfocusedPlayerVolume { get => _unfocusedPlayerVolume; set => _unfocusedPlayerVolume = value; }
    public float SFXVolume { get => _sfxVolume; set => _sfxVolume = value; }
    public float LiveStreamVolume { get => _liveStreamVolume; set => _liveStreamVolume = value; }
    public byte[] LastFrame { get => _lastFrame; set => _lastFrame = value; }
    public bool LowPerformanceMode { get => _lowPerformanceMode; set => _lowPerformanceMode = value; }

    public event EventHandler OnNewMediaTriggered; // event handler for media being triggered
    public MediaManager() {
        _libVLCPath = "";
        _updateLoop = Task.Run(() => Update());
    }

    // public async void PlayAudio(IGameObject playerObject, string audioPath, SoundType soundType, int delay = 0, TimeSpan skipAhead = new TimeSpan()) {
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

    // public async void PlayAudioStream(IGameObject playerObject, WaveStream audioStream, SoundType soundType, int delay = 0) {
    //     try {
    //         if (playerObject != null) {
    //             if (_nativeGameAudio.ContainsKey(playerObject.Name)) {
    //                 _nativeGameAudio[playerObject.Name].Stop();
    //             }
    //             _nativeGameAudio[playerObject.Name] = new MediaObject(
    //                 this, playerObject, _camera,
    //                 soundType, "", _libVLCPath);
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

    private void MediaManager_OnErrorReceived(object? sender, MediaError e) {
        OnErrorReceived?.Invoke(this, new MediaError() { Exception = e.Exception });
    }

    // the one thing we actually care about Anyways, was originally a public async void function, if we get errors, revert it back
    public void PlayStream(IGameObject playerObject, string audioPath, int delay = 0) {
        #pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        // invoke the new media trigger
        OnNewMediaTriggered?.Invoke(this, EventArgs.Empty);
        // tell the media manager to set its cancel trigger to false, because we have not yet canceled it.
        bool cancelOperation = false;
        // if the stored stream path is not empty, then an existing stream is already up, so we need to replace it.
        if (!string.IsNullOrEmpty(audioPath)) {
            // to replace it, first go through the playback streams and stop all of sound sources
            if (audioPath.StartsWith("http")) {
                foreach (var sound in _playbackStreams) {
                    sound.Value?.Stop();
                }
                // clear out the previous stream
                _playbackStreams.Clear();
                // now configure the audio for the next stream to the player
                ConfigureAudio(playerObject, audioPath, SoundType.Livestream, _playbackStreams, delay);
            }
        }
    }

    public void StopStream() {
        foreach (var sound in _playbackStreams) {
            sound.Value?.Stop();
        }
        _playbackStreams.Clear();
    }

    // public bool IsAllowedToStartStream(IGameObject playerObject) {
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

    // public void StopAudio(IGameObject playerObject) {
    //     if (playerObject != null) {
    //         if (_voicePackSounds.ContainsKey(playerObject.Name)) {
    //             _voicePackSounds[playerObject.Name].Stop();
    //         }
    //         if (_nativeGameAudio.ContainsKey(playerObject.Name)) {
    //             _nativeGameAudio[playerObject.Name].Stop();
    //         }
    //     }
    // }

    // public void LoopEarly(IGameObject playerObject) {
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
    /// <item><c>audioPath</c><param name="audioPath"> - The audio path for the stream.</param></item>
    /// <item><c>soundType</c><param name="soundType"> - The sound type we are placing the stream audio into.</param></item>
    /// <item><c>sounds</c><param name="sounds"> - The dictionary of media object sounds.</param></item>
    /// <item><c>delay</c><param name="delay"> - The delay a stream may have at any point.</param></item>
    /// <item><c>skipAhead</c><param name="skipAhead"> - The skip ahead to cut forward once we stop having delay.</param></item>
    /// </list></summary> <returns>The newly configured audio.</returns>
    public void ConfigureAudio(IGameObject playerObject, string audioPath, SoundType soundType,
        ConcurrentDictionary<string, MediaObject> sounds, int delay = 0, TimeSpan skipAhead = new TimeSpan()) {
        // Check if we're not already configuring a sound and if the sound type is not MainPlayerCombat or 
        // if it is MainPlayerCombat, then either the cooldown timer has elapsed more than 400 milliseconds or the timer is not running
        if (!alreadyConfiguringSound && (soundType != SoundType.MainPlayerCombat ||
        (soundType == SoundType.MainPlayerCombat && mainPlayerCombatCooldownTimer.ElapsedMilliseconds > 400 || !mainPlayerCombatCooldownTimer.IsRunning))) {
            // We're now configuring a sound, so set it to true
            alreadyConfiguringSound = true;
            // Assume the sound is not already playing
            bool soundIsPlayingAlready = false;
            // If the player already has a sound
            if (sounds.ContainsKey(playerObject.Name)) {
                // If the sound type is MainPlayerVoice or MainPlayerCombat
                if (soundType == SoundType.MainPlayerVoice || soundType == SoundType.MainPlayerCombat) {
                    // Check if the sound is already playing
                    soundIsPlayingAlready = sounds[playerObject.Name].PlaybackState == PlaybackState.Playing;
                    // If the sound type is MainPlayerCombat, restart the cooldown timer
                    if (soundType == SoundType.MainPlayerCombat) {
                        mainPlayerCombatCooldownTimer.Restart();
                    }
                } else {
                    // For other sound types, try to stop the sound
                    try {
                        sounds[playerObject.Name]?.Stop();
                    } catch (Exception e) {
                        // If an error occurs, raise the OnErrorReceived event
                        OnErrorReceived?.Invoke(this, new MediaError() { Exception = e });
                    }
                }
            }
            // If the sound is not already playing
            if (!soundIsPlayingAlready) {
                try {
                    // Create a new MediaObject for the sound
                    sounds[playerObject.Name] = new MediaObject(
                        this, playerObject, _camera, _loopStream,
                        soundType, audioPath, _libVLCPath);
                    lock (sounds[playerObject.Name]) {
                        // Get the volume for the sound
                        float volume = GetVolume(sounds[playerObject.Name].SoundType, sounds[playerObject.Name].PlayerObject);
                        // If the volume is 0, set it to 1
                        if (volume == 0) {
                            volume = 1;
                        }
                        // Subscribe to the OnErrorReceived event of the sound
                        sounds[playerObject.Name].OnErrorReceived += MediaManager_OnErrorReceived;
                        // Start a timer to measure the playback time
                        Stopwatch soundPlaybackTimer = Stopwatch.StartNew();
                        // Play the sound
                        sounds[playerObject.Name].Play(audioPath, volume, delay, skipAhead, _lowPerformanceMode);
                        // If the playback time is more than 2 seconds, enable low performance mode
                        if (soundPlaybackTimer.ElapsedMilliseconds > 2000) {
                            _lowPerformanceMode = true;
                            OnErrorReceived?.Invoke(this, new MediaError() { Exception = new Exception("Low performance detected, enabling low performance mode.") });
                        }
                    }
                } catch (Exception e) {
                    // If an error occurs, raise the OnErrorReceived event
                    OnErrorReceived?.Invoke(this, new MediaError() { Exception = e });
                }
            }
            // We're done configuring the sound
            alreadyConfiguringSound = false;
        }
    }


    private async void Update() {
        while (notDisposed) {
            UpdateVolumes(_playbackStreams);
            UpdateVolumes(_nativeGameAudio);
            //Thread.Sleep(!_lowPerformanceMode ? 100 : 400);
        }
    }
    public void UpdateVolumes(ConcurrentDictionary<string, MediaObject> sounds) {
        for (int i = 0; i < sounds.Count; i++) {
            string playerName = sounds.Keys.ElementAt<string>(i);
            if (sounds.ContainsKey(playerName)) {
                try {
                    lock (sounds[playerName]) {
                        if (sounds[playerName].PlayerObject != null) {
                            Vector3 dir = sounds[playerName].PlayerObject.Position - _camera.Position;
                            float direction = AngleDir(_camera.Forward, dir, _camera.Top);
                            sounds[playerName].Volume = CalculateObjectVolume(playerName, sounds[playerName]);
                            sounds[playerName].Pan = Math.Clamp(direction / 3, -1, 1);
                        }
                    }
                } catch (Exception e) { OnErrorReceived?.Invoke(this, new MediaError() { Exception = e }); }
            }
        }
    }

    public float CalculateObjectVolume(string playerName, MediaObject mediaObject) {
        float maxDistance = (playerName == _mainPlayer.Name ||
        mediaObject.SoundType == SoundType.Livestream) ? 100 : 20;
        float volume = GetVolume(mediaObject.SoundType, mediaObject.PlayerObject);
        float distance = Vector3.Distance(_camera.Position, mediaObject.PlayerObject.Position);
        return Math.Clamp(volume * ((maxDistance - distance) / maxDistance), 0f, 1f);
    }

    public float AngleDir(Vector3 fwd, Vector3 targetDir, Vector3 up) {
        Vector3 perp = Vector3.Cross(fwd, targetDir);
        float dir = Vector3.Dot(perp, up);
        return dir;
    }

    public float GetVolume(SoundType soundType, IGameObject playerObject) {
        if (playerObject != null) {
            if (_mainPlayer.FocusedPlayerObject == null ||
                playerObject.Name == _mainPlayer.Name ||
                _mainPlayer.FocusedPlayerObject == playerObject.Name) {
                switch (soundType) {
                    case SoundType.MainPlayerTts:
                        return _mainPlayerVolume;
                    case SoundType.Emote:
                    case SoundType.MainPlayerVoice:
                    case SoundType.MainPlayerCombat:
                        return _mainPlayerVolume * 1f;
                    case SoundType.OtherPlayerTts:
                    case SoundType.OtherPlayer:
                    case SoundType.OtherPlayerCombat:
                        return _otherPlayerVolume;
                    case SoundType.Loop:
                        return _sfxVolume;
                    case SoundType.LoopWhileMoving:
                        return _sfxVolume;
                    case SoundType.Livestream:
                        return _liveStreamVolume;
                }
            } else {
                switch (soundType) {
                    case SoundType.MainPlayerTts:
                        return _mainPlayerVolume;
                    case SoundType.Emote:
                    case SoundType.MainPlayerVoice:
                    case SoundType.MainPlayerCombat:
                        return _mainPlayerVolume * 1f;
                    case SoundType.OtherPlayerTts:
                    case SoundType.OtherPlayer:
                    case SoundType.OtherPlayerCombat:
                        return _unfocusedPlayerVolume;
                    case SoundType.Loop:
                        return _sfxVolume;
                    case SoundType.LoopWhileMoving:
                        return _sfxVolume;
                    case SoundType.Livestream:
                        return _liveStreamVolume;
                }
            }
        }
        return 1;
    }

    public void Dispose() {
        notDisposed = false;
        CleanSounds();
        try {
            if (_updateLoop != null) {
                _updateLoop?.Dispose();
            }
        } catch (Exception e) { OnErrorReceived?.Invoke(this, new MediaError() { Exception = e }); }
    }

    public void CleanNonStreamingSounds() {
        try {
            List<KeyValuePair<string, MediaObject>> cleanupList = new List<KeyValuePair<string, MediaObject>>();
            cleanupList.AddRange(_nativeGameAudio);
            foreach (var sound in cleanupList) {
                if (sound.Value != null) {
                    sound.Value?.Stop();
                    sound.Value.OnErrorReceived -= MediaManager_OnErrorReceived;
                }
            }
            _lastFrame = null;
            _nativeGameAudio?.Clear();
        } catch (Exception e) { OnErrorReceived?.Invoke(this, new MediaError() { Exception = e }); }
    }

    public void CleanSounds() {
        try {
            List<KeyValuePair<string, MediaObject>> cleanupList = new List<KeyValuePair<string, MediaObject>>();
            cleanupList.AddRange(_playbackStreams);
            cleanupList.AddRange(_nativeGameAudio);
            foreach (var sound in cleanupList) {
                if (sound.Value != null) {
                    sound.Value?.Stop();
                    sound.Value.OnErrorReceived -= MediaManager_OnErrorReceived;
                }
            }
            _lastFrame = null;
            _playbackStreams?.Clear();
            _nativeGameAudio?.Clear();
        } catch (Exception e) { OnErrorReceived?.Invoke(this, new MediaError() { Exception = e }); }
    }
}