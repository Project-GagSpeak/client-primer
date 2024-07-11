using System;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Numerics;
using System.Runtime.InteropServices;
using LibVLCSharp.Shared;
using NAudio.Vorbis;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using SixLabors.ImageSharp.Advanced;
using FFStreamViewer.Events;
using FFStreamViewer.Audio;
using FFStreamViewer.Utils;
using System.Threading;
using System.Drawing;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.IO;
using System.Threading.Tasks;

namespace FFStreamViewer.Livestream;

/// <summary>
/// Object for managing streams (hopefully)
/// </summary>
public class MediaObject {
    // video attributes
    private readonly ILogger<MediaObject>       _logger;
    private static  MemoryMappedFile            _currentMappedFile;
    private static  MemoryMappedViewAccessor    _currentMappedViewAccessor;
    public          SoundType                   _soundType;
    private         VolumeSampleProvider        _volumeSampleProvider;
    private         PanningSampleProvider       _panningSampleProvider;
    private         WaveOutEvent                _waveOutEvent;
    private         LibVLC                      _libVLC;
    private         MediaPlayer                 _vlcPlayer;
    private         WaveStream                  _player;
    private         WasapiOut                   _wasapiOut;
    private         LoopStream                  _loopStream;
    // General attributes
    private         MediaManager                _parent;
    public          MediaGameObject             _playerObject;
    public          MediaCameraObject           _camera;
    private         FFSV_Config                 _config;

    private         bool                        stopPlaybackOnMovement;
    private         Vector3                     lastPosition;

    /// <summary>
    /// RGBA is used, so 4 byte per pixel, or 32 bits.
    /// </summary>
    private const uint _bytePerPixel = 4;

    /// <summary>
    /// the number of bytes per "line"
    /// For performance reasons inside the core of VLC, it must be aligned to multiples of 32.
    /// </summary>
    private uint _pitch;

    /// <summary>
    /// The number of lines in the buffer.
    /// For performance reasons inside the core of VLC, it must be aligned to multiples of 32.
    /// </summary>
    private uint _lines;

    public const    uint                    Width = 640; // constant defining width for the streams
    public const    uint                    Height = 360;

    public MediaObject(ILogger<MediaObject> logger, MediaManager parent, MediaGameObject playerObject,
        MediaCameraObject camera, FFSV_Config config)
    {
        _parent = parent;
        _playerObject = playerObject;
        _camera = camera;
        _config = config;

        _pitch = Align(Width * _bytePerPixel);
        _lines = Align(Height);
    }

    #region Attributes
    public float Volume {
        get {
            if (_volumeSampleProvider == null) {
                return 0;
            } else {
                return _volumeSampleProvider.Volume;
            }
        }
        set {
            if (_volumeSampleProvider != null) {
                _volumeSampleProvider.Volume = value * _config.OffsetVolume;
            }
            if (_vlcPlayer != null) {
                try {
                    int newValue = (int)(value * 100f);
                    if (newValue != _vlcPlayer.Volume) {
                        _vlcPlayer.Volume = newValue;
                    }
                } catch (Exception e) {
                    _logger.LogError($"Error setting volume: {e}", "Media Object");
                }
            }
        }
    }
    // get the playback state of the sound
    public PlaybackState PlaybackState {
        get {
            if (_waveOutEvent != null) {
                try {
                    return _waveOutEvent.PlaybackState;
                } catch {
                    return PlaybackState.Stopped;
                }
            } else if (_vlcPlayer != null) {
                try {
                    return _vlcPlayer.IsPlaying ? PlaybackState.Playing : PlaybackState.Stopped;
                } catch {
                    return PlaybackState.Stopped;
                }
            } else if (_wasapiOut != null) {
                try {
                    return _wasapiOut.PlaybackState;
                } catch {
                    return PlaybackState.Stopped;
                }
            } else {
                return PlaybackState.Stopped;
            }
        }
    }
    public float Pan {
        get {
            if (_panningSampleProvider == null) {
                return 0;
            } else {
                return _panningSampleProvider.Pan;
            }
        }
        set {
            if (_panningSampleProvider != null) {
                _panningSampleProvider.Pan = value;
            }
        }
    }
    #endregion Attributes
    #region Methods

    private static uint Align(uint size) {
        if (size % 32 == 0) {
            return size;
        }
        return ((size / 32) + 1) * 32; // Align on the next multiple of 32
    }

    private void SoundLoopCheck() {
        Task.Run(async () => {
            try {
                Thread.Sleep(500);
                lastPosition = _playerObject.Position;
                Thread.Sleep(500);
                while (true) {
                    if (_playerObject != null && _waveOutEvent != null && _volumeSampleProvider != null) {
                        float distance = Vector3.Distance(lastPosition, _playerObject.Position);
                        if ((distance > 0.01f && _soundType == SoundType.Loop) ||
                        (distance < 0.1f && _soundType == SoundType.LoopWhileMoving)) {
                            _waveOutEvent.Stop();
                            break;
                        }
                    }
                    if (_soundType == SoundType.LoopWhileMoving) {
                        lastPosition = _playerObject.Position;
                    }
                    Thread.Sleep(200);
                }
            } catch (Exception e) {
                _logger.LogError($"Error in SoundLoopCheck: {e}", "Media Object");
            }
        });
    }

    public void Stop() {
        if (_waveOutEvent != null) {
            try {
                _waveOutEvent?.Stop();
            } catch { }
        }
        if (_vlcPlayer != null) {
            try {
                _vlcPlayer?.Stop();
            } catch (Exception e) { 
                _logger.LogError($"Error stopping VLC player: {e}", "Media Object");
            }
        }
    }
    public void LoopEarly() {
        _loopStream?.LoopEarly();
    }

    public async void Play(WaveStream soundPath, float volume, int delay) {
        try {
            if (PlaybackState == PlaybackState.Stopped) {
                _player = soundPath;
                // get the desired stream player sound
                WaveStream desiredStream = _player;
                if (_soundType != SoundType.MainPlayerTts &&
                    _soundType != SoundType.OtherPlayerTts &&
                    _soundType != SoundType.LoopWhileMoving &&
                    _soundType != SoundType.Livestream &&
                    _soundType != SoundType.MainPlayerCombat &&
                    _soundType != SoundType.OtherPlayerCombat &&
                    _player.TotalTime.TotalSeconds > 13)
                {
                    _soundType = SoundType.Loop;
                }

                _config.OffsetVolume = 0.7f;
                float distance = Vector3.Distance(_camera.Position, _playerObject.Position);
                float newVolume = volume * ((20 - distance) / 20) * _config.OffsetVolume;
                if (delay > 0) {
                    Thread.Sleep(delay); //MIGHT NEED LATER BUT FIND OUT
                }
                _waveOutEvent = new WaveOutEvent();
                // if the type of sound is a loop of sorts
                if (_soundType == SoundType.Loop || _soundType == SoundType.LoopWhileMoving) {
                    // do a sound loop check
                    SoundLoopCheck();
                    _loopStream = new LoopStream(_player) { EnableLooping = false };
                    desiredStream = _loopStream;
                }
                // gets a sample of the volume
                _volumeSampleProvider = new VolumeSampleProvider(desiredStream.ToSampleProvider());
                _volumeSampleProvider.Volume = newVolume;
                _panningSampleProvider =
                new PanningSampleProvider(_volumeSampleProvider.ToMono());
                Vector3 dir = _playerObject.Position - _camera.Position;
                float direction = AngleDir(_camera.Forward, dir, _camera.Top);
                _panningSampleProvider.Pan = Math.Clamp(direction / 3, -1, 1);
                // attempt to initialize and play the sound
                try {
                    _waveOutEvent?.Init(_panningSampleProvider);
                    _waveOutEvent?.Play();
                } catch (Exception e) {
                    _logger.LogError($"Error playing sound: {e}", "Media Object");
                }
            }
        } catch (Exception e) { 
            _logger.LogError($"Error playing sound: {e}", "Media Object");
        }
    }

    /// <summary> Plays the audio from the given sound path. (Stream URL in our case)
    /// <list type="bullet">
    /// <item> <description> <paramref name="soundPath"/> - The path of the stream RTMP. </description> </item>
    /// <item> <description> <paramref name="volume"/> - The volume to play the sound at. </description> </item>
    /// <item> <description> <paramref name="delay"/> - The delay to wait before playing the sound. </description> </item>
    /// <item> <description> <paramref name="skipAhead"/> - The amount of time to skip ahead in the stream. </description> </item>
    /// <item> <description> <paramref name="lowPerformanceMode"/> - Whether or not to use low performance mode. </description> </item> </list> </summary>
    public async void Play(string soundPath, float volume, int delay, TimeSpan skipAhead, bool lowPerformanceMode = false) {
        try { // a lot goes on here so be ready to catch any errors!
            // first startup a timer to calculate latency of the stream
            Stopwatch latencyTimer = Stopwatch.StartNew();
            // next, check if the sound path is empty or not and if the playback state is stopped
            if (!string.IsNullOrEmpty(soundPath) && PlaybackState == PlaybackState.Stopped) {
                // if the sound path does not start with rtmp, then it could be an audio file. (for RTMP, look at else statement)
                if (!soundPath.StartsWith("rtmp")) {
                    // if the sound path ends with .ogg, then we have a vorbis file, otherwise, we have a media foundation file.
                    // whichever the answer to the case is will be applied to the WAVESTREAM _player object!
                    _player = soundPath.EndsWith(".ogg") ?
                    new VorbisWaveReader(soundPath) : new MediaFoundationReader(soundPath);
                    // get the player sound and set desiredStream to it.
                    WaveStream desiredStream = _player;
                    if (_soundType != SoundType.MainPlayerTts &&
                        _soundType != SoundType.OtherPlayerTts &&
                        _soundType != SoundType.LoopWhileMoving &&
                        _soundType != SoundType.Livestream &&
                        _soundType != SoundType.MainPlayerCombat &&
                        _soundType != SoundType.OtherPlayerCombat &&
                        _player.TotalTime.TotalSeconds > 13) {
                        _soundType = SoundType.Loop;
                    }
                    // define or initialize the wave out event (looking into still)
                    _waveOutEvent ??= new WaveOutEvent();
                    // if the sound type is not a combat sound, then we can delay the sound
                    if (_soundType != SoundType.MainPlayerCombat && _soundType != SoundType.OtherPlayerCombat) {
                        if (delay > 0) {
                            Thread.Sleep(delay);
                        }
                    }
                    // if the sound type is a loop of sorts, then we need to do a sound loop check
                    if (_soundType == SoundType.Loop || _soundType == SoundType.LoopWhileMoving) {
                        // for our loop check, first check if the sound type is not a combat sound
                        if (_soundType != SoundType.MainPlayerCombat && _soundType != SoundType.OtherPlayerCombat) {
                            SoundLoopCheck();
                        }
                        // then initialize the loop stream and set desiredStream to it
                        _loopStream = new LoopStream(_player) { EnableLooping = true };
                        desiredStream = _loopStream;
                    }
                    // get the volume of the sound and the distance between the camera and the player object
                    float distance = Vector3.Distance(_camera.Position, _playerObject.Position);
                    // calculate the new volume based on the distance
                    float newVolume = _parent.CalculateObjectVolume(_playerObject.Name, this);

                    // initialize a sample provider
                    ISampleProvider sampleProvider = null;
                    // if we are not in low performance mode or the sound type is not a combat sound:
                    if (!lowPerformanceMode || _soundType != SoundType.MainPlayerCombat && _soundType != SoundType.MainPlayerTts) {
                        // let's set the vaolume of our sample provider to the valued stored in the desiredStream variable
                        _volumeSampleProvider = new VolumeSampleProvider(desiredStream.ToSampleProvider());
                        // update the new volume with the offset volume
                        _volumeSampleProvider.Volume = volume;
                        // we will need to make a panning sample provider as well, so define it.
                        _panningSampleProvider = new PanningSampleProvider(
                        _player.WaveFormat.Channels == 1 ? _volumeSampleProvider : _volumeSampleProvider.ToMono());
                        // get the direction of the player object
                        Vector3 dir = _playerObject.Position - _camera.Position;
                        // get the angle of the player object
                        float direction = AngleDir(_camera.Forward, dir, _camera.Top);
                        // set the pan of the panning sample provider to the direction
                        _panningSampleProvider.Pan = Math.Clamp(direction / 3, -1, 1);
                        // set the sample provider to the panning sample provider
                        sampleProvider = _panningSampleProvider;
                    } else {
                        // if we are in low performance mode, then we need to set the sample provider to the desired stream
                        _volumeSampleProvider = new VolumeSampleProvider(desiredStream.ToSampleProvider());
                        // update the new volume with the offset volume
                        _volumeSampleProvider.Volume = volume;
                        // set the sample provider to the volume sample provider
                        sampleProvider = _volumeSampleProvider;
                    }
                    // if our waveoutevent is not null, then we can initialize it. Or, try to at least.
                    if (_waveOutEvent != null) {
                        try {
                            // initialize the wave out event with the sample provider
                            _waveOutEvent?.Init(sampleProvider);
                            // if the soundtype is a loop, main player, or other player voice:
                            if (_soundType == SoundType.Loop ||
                            _soundType == SoundType.MainPlayerVoice ||
                            _soundType == SoundType.OtherPlayer) {
                                // then we set the currenttime var of the stream player to the skipahead var    
                                _player.CurrentTime = skipAhead;
                                // if the latency timer is greater than 13 seconds, then we can add the latency timer to the current time
                                if (_player.TotalTime.TotalSeconds > 13) {
                                    _player.CurrentTime += latencyTimer.Elapsed;
                                }
                            } else {
                                // if our soundtype was not a loop, main player, or other player voice, set the current time to 0
                                _player.Position = 0;
                            }

                            // if the sound type is a combat sound, set the desired latency to 50 on the waveoutEvent
                            if (_soundType == SoundType.MainPlayerCombat ||
                                _soundType == SoundType.OtherPlayerCombat) {
                                #pragma warning disable CS8602 // We already made sure it was not null.
                                _waveOutEvent.DesiredLatency = 50;
                                #pragma warning restore CS8602 // Dereference of a possibly null reference.
                            }
                            // attempt to play the waveout event. If it fails, it will throw an exception, so we're good.
                            _waveOutEvent?.Play();
                        } catch (Exception e) {
                            _logger.LogError($"Error playing sound: {e}", "Media Object");
                        }
                    } else {
                        // if the waveout event is null, then we can't initialize it, so throw an exception.
                        throw new Exception("WaveOutEvent is null!");
                    }
                } 
                // This is fired when the audio link IS an rtmp link.
                else {
                    // attempt to watch the RTMP stream.
                    try {
                        // try to initialize the media player
                        _parent.LastFrame = Array.Empty<byte>();
                        // set the location of the libvlc dll to the libvlc path
                        string location = _config.LibVLCPath + @"\libvlc\win-x64";
                        // initialize the core of the libvlc dll sharp
                        _logger.LogDebug($"Initializing libvlc core at path {location}", "Media Object");
                        Core.Initialize(location);
                        // initialize the libvlc dll sharp
                        _libVLC = new LibVLC("--vout", "none");
                        // define the media with a new media object
                        var media = new Media(_libVLC, soundPath, FromType.FromLocation);
                        // parse the media with the parse network option
                        await media.Parse(MediaParseOptions.ParseNetwork);
                        // define the actual vlc media player object as a new media player object
                        _vlcPlayer = new MediaPlayer(media);
                        // fetch a new cancellation token source
                        var processingCancellationTokenSource = new CancellationTokenSource();
                        // define the cancellation token
                        _vlcPlayer.Stopped += (s, e) => processingCancellationTokenSource.CancelAfter(1);
                        _vlcPlayer.Stopped += delegate { _parent.LastFrame = null; };
                        // set the format of the video to display
                        _vlcPlayer.SetVideoFormat("RV32", Width, Height, _pitch);
                        // set the callbacks for the video
                        _vlcPlayer.SetVideoCallbacks(Lock, null, Display);
                        // try and play the video
                        _vlcPlayer.Play();
                    } catch (Exception e) {
                        // if any of the VLC startup fails, throw an error. 
                        _logger.LogError($"Error playing RTMP stream: {e}", "Media Object");
                    }
                }
            }
        } catch (Exception e) {
            // if any of the above fails, throw an error.
            _logger.LogError($"Error playing sound: {e}", "Media Object");
        }
        _config.Save();
    }

    public static float AngleDir(Vector3 fwd, Vector3 targetDir, Vector3 up) {
        Vector3 perp = Vector3.Cross(fwd, targetDir);
        float dir = Vector3.Dot(perp, up);
        return dir;
    }

    private IntPtr Lock(IntPtr opaque, IntPtr planes) {
        try {
            _currentMappedFile = MemoryMappedFile.CreateNew(null, _pitch * _lines);
            _currentMappedViewAccessor = _currentMappedFile.CreateViewAccessor();
            Marshal.WriteIntPtr(planes, _currentMappedViewAccessor.SafeMemoryMappedViewHandle.DangerousGetHandle());
            return IntPtr.Zero;
        } catch {
            return IntPtr.Zero;
        }
    }

    private void Display(IntPtr opaque, IntPtr picture) {
        try {
            using (var image = new Image<Bgra32>((int)(_pitch / _bytePerPixel), (int)_lines))
            using (var sourceStream = _currentMappedFile.CreateViewStream()) {
                var mg = image.GetPixelMemoryGroup();
                for (int i = 0; i < mg.Count; i++) {
                    sourceStream.Read(MemoryMarshal.AsBytes(mg[i].Span));
                }
                lock (_parent.LastFrame) {
                    MemoryStream stream = new MemoryStream();
                    image.SaveAsJpeg(stream);
                    stream.Flush();
                    _parent.LastFrame = stream.ToArray();
                }
            }
            _currentMappedViewAccessor.Dispose();
            _currentMappedFile.Dispose();
            _currentMappedFile = null;
            _currentMappedViewAccessor = null;
        } catch (Exception e) { 
            _logger.LogError($"Error displaying video: {e}", "Media Object");
        }
    }
    #endregion Methods
}
