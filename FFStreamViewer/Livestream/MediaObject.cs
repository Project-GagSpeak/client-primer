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

namespace FFStreamViewer.Livestream;

public class MediaObject {
    private IGameObject _playerObject;
    private IGameObject _camera;
    private SoundType _soundType;

    private VolumeSampleProvider _volumeSampleProvider;
    private PanningSampleProvider _panningSampleProvider;
    private WaveOutEvent _waveOutEvent;
    private LibVLC libVLC;
    private MediaPlayer _vlcPlayer;
    private MediaManager _parent;
    private WaveStream _player;

    private static MemoryMappedFile _currentMappedFile;
    private static MemoryMappedViewAccessor _currentMappedViewAccessor;
    public event EventHandler<MediaError> OnErrorReceived;

    private string _soundPath;
    private string _libVLCPath;

    private bool stopPlaybackOnMovement;
    private Vector3 lastPosition;

    private const uint _width = 640;
    private const uint _height = 360;

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
    private float offsetVolume = 1;

    private WasapiOut _wasapiOut;
    private LoopStream _loopStream;

    public MediaObject(MediaManager parent, IGameObject playerObject, IGameObject camera,
        LoopStream loopStream,SoundType soundType, string soundPath, string libVLCPath) {
        _playerObject = playerObject;
        _soundPath = soundPath;
        _camera = camera;
        _libVLCPath = libVLCPath;
        _parent = parent;
        _loopStream = loopStream;
        this._soundType = soundType;
        _pitch = Align(_width * _bytePerPixel);
        _lines = Align(_height);
    }

    private static uint Align(uint size) {
        if (size % 32 == 0) {
            return size;
        }
        return ((size / 32) + 1) * 32; // Align on the next multiple of 32
    }

    private void SoundLoopCheck() {
        //Task.Run(async () => {
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
            OnErrorReceived?.Invoke(this, new MediaError() { Exception = e });
        }
    }

    public IGameObject PlayerObject { get => _playerObject; set => _playerObject = value; }
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
                _volumeSampleProvider.Volume = value * offsetVolume;
            }
            if (_vlcPlayer != null) {
                try {
                    int newValue = (int)(value * 100f);
                    if (newValue != _vlcPlayer.Volume) {
                        _vlcPlayer.Volume = newValue;
                    }
                } catch (Exception e) { OnErrorReceived?.Invoke(this, new MediaError() { Exception = e }); }
            }
        }
    }
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
    public SoundType SoundType { get => _soundType; set => _soundType = value; }
    public bool StopPlaybackOnMovement { get => stopPlaybackOnMovement; set => stopPlaybackOnMovement = value; }
    public string SoundPath { get => _soundPath; set => _soundPath = value; }
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

    public IGameObject Camera { get => _camera; set => _camera = value; }

    public void Stop() {
        if (_waveOutEvent != null) {
            try {
                _waveOutEvent?.Stop();
            } catch { }
        }
        if (_vlcPlayer != null) {
            try {
                _vlcPlayer?.Stop();
            } catch (Exception e) { OnErrorReceived?.Invoke(this, new MediaError() { Exception = e }); }
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

                offsetVolume = 0.7f;
                float distance = Vector3.Distance(_camera.Position, PlayerObject.Position);
                float newVolume = volume * ((20 - distance) / 20) * offsetVolume;
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
                Vector3 dir = PlayerObject.Position - _camera.Position;
                float direction = AngleDir(_camera.Forward, dir, _camera.Top);
                _panningSampleProvider.Pan = Math.Clamp(direction / 3, -1, 1);
                // attempt to initialize and play the sound
                try {
                    _waveOutEvent?.Init(_panningSampleProvider);
                    _waveOutEvent?.Play();
                } catch (Exception e) {
                    OnErrorReceived?.Invoke(this, new MediaError() { Exception = e });
                }
            }
        } catch (Exception e) { OnErrorReceived?.Invoke(this, new MediaError() { Exception = e }); }
    }

    // was a public async before, switch back if problems arise (Used for the media player)
    public async void Play(string soundPath, float volume, int delay, TimeSpan skipAhead, bool lowPerformanceMode = false) {
        try {
            // this whole function confuses me greatly
            Stopwatch latencyTimer = Stopwatch.StartNew();
            if (!string.IsNullOrEmpty(soundPath) && PlaybackState == PlaybackState.Stopped) {
                if (!soundPath.StartsWith("http")) {
                    _player = soundPath.EndsWith(".ogg") ?
                    new VorbisWaveReader(soundPath) : new MediaFoundationReader(soundPath);
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
                    _waveOutEvent ??= new WaveOutEvent();
                    if (_soundType != SoundType.MainPlayerCombat && _soundType != SoundType.OtherPlayerCombat) {
                        if (delay > 0) {
                            Thread.Sleep(delay);
                        }
                    }
                    if (_soundType == SoundType.Loop || _soundType == SoundType.LoopWhileMoving) {
                        if (_soundType != SoundType.MainPlayerCombat && _soundType != SoundType.OtherPlayerCombat) {
                            SoundLoopCheck();
                        }
                        _loopStream = new LoopStream(_player) { EnableLooping = true };
                        desiredStream = _loopStream;
                    }
                    float distance = Vector3.Distance(_camera.Position, PlayerObject.Position);
                    float newVolume = _parent.CalculateObjectVolume(_playerObject.Name, this);
                    ISampleProvider sampleProvider = null;
                    if (!lowPerformanceMode || _soundType != SoundType.MainPlayerCombat && _soundType != SoundType.MainPlayerTts) {
                        _volumeSampleProvider = new VolumeSampleProvider(desiredStream.ToSampleProvider());
                        _volumeSampleProvider.Volume = volume;
                        _panningSampleProvider = new PanningSampleProvider(
                        _player.WaveFormat.Channels == 1 ? _volumeSampleProvider : _volumeSampleProvider.ToMono());
                        Vector3 dir = PlayerObject.Position - _camera.Position;
                        float direction = AngleDir(_camera.Forward, dir, _camera.Top);
                        _panningSampleProvider.Pan = Math.Clamp(direction / 3, -1, 1);
                        sampleProvider = _panningSampleProvider;
                    } else {
                        _volumeSampleProvider = new VolumeSampleProvider(desiredStream.ToSampleProvider());
                        _volumeSampleProvider.Volume = volume;
                        sampleProvider = _volumeSampleProvider;
                    }
                    if (_waveOutEvent != null) {
                        try {
                            _waveOutEvent?.Init(sampleProvider);
                            if (_soundType == SoundType.Loop ||
                                _soundType == SoundType.MainPlayerVoice ||
                                _soundType == SoundType.OtherPlayer) {
                                _player.CurrentTime = skipAhead;
                                if (_player.TotalTime.TotalSeconds > 13) {
                                    _player.CurrentTime += latencyTimer.Elapsed;
                                }
                            } else {
                                _player.Position = 0;
                            }
                            if (_soundType == SoundType.MainPlayerCombat ||
                                _soundType == SoundType.OtherPlayerCombat) {
                                _waveOutEvent.DesiredLatency = 50;
                            }
                            _waveOutEvent?.Play();
                        } catch (Exception e) { OnErrorReceived?.Invoke(this, new MediaError() { Exception = e }); }
                    }
                } else {
                    try {
                        _parent.LastFrame = Array.Empty<byte>();
                        string location = _libVLCPath + @"\libvlc\win-x64";
                        Core.Initialize(location);
                        libVLC = new LibVLC("--vout", "none");
                        var media = new Media(libVLC, soundPath, FromType.FromLocation);
                        await media.Parse(MediaParseOptions.ParseNetwork);
                        _vlcPlayer = new MediaPlayer(media);
                        var processingCancellationTokenSource = new CancellationTokenSource();
                        _vlcPlayer.Stopped += (s, e) => processingCancellationTokenSource.CancelAfter(1);
                        _vlcPlayer.Stopped += delegate { _parent.LastFrame = null; };
                        _vlcPlayer.SetVideoFormat("RV32", _width, _height, _pitch);
                        _vlcPlayer.SetVideoCallbacks(Lock, null, Display);
                        _vlcPlayer.Play();
                    } catch (Exception e) { OnErrorReceived?.Invoke(this, new MediaError() { Exception = e }); }
                }
            }
        } catch (Exception e) { OnErrorReceived?.Invoke(this, new MediaError() { Exception = e }); }
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
        } catch (Exception e) { OnErrorReceived?.Invoke(this, new MediaError() { Exception = e }); }
    }
}