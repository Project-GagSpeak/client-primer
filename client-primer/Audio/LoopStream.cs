using NAudio.Wave;

namespace FFStreamViewer.Audio;
public class LoopStream : WaveStream {
    private readonly WaveStream _sourceStream;
    private bool _LoopEarly;

    public LoopStream(WaveStream sourceStream) {
        _sourceStream = sourceStream;
        this.EnableLooping = true;
    }

    /// <summary>
    /// Use this to turn looping on or off
    /// </summary>
    public bool EnableLooping { get; set; }

    /// <summary>
    /// Return source stream's wave format
    /// </summary>
    public override WaveFormat WaveFormat {
        get { return _sourceStream.WaveFormat; }
    }

    /// <summary>
    /// LoopStream simply returns
    /// </summary>
    public override long Length {
        get { return _sourceStream.Length; }
    }

    /// <summary>
    /// LoopStream simply passes on positioning to source stream
    /// </summary>
    public override long Position {
        get { return _sourceStream.Position; }
        set { _sourceStream.Position = value; }
    }

    public override int Read(byte[] buffer, int offset, int count) {
        int totalBytesRead = 0;

        while (totalBytesRead < count) {
            int bytesRead = _sourceStream.Read(buffer, offset + totalBytesRead, count - totalBytesRead);
            if (bytesRead == 0 || _LoopEarly) {
                if (_sourceStream.Position == 0 || !EnableLooping) {
                    // something wrong with the source stream
                    break;
                }
                // loop
                _sourceStream.Position = 0;
                _LoopEarly = false;
            }
            totalBytesRead += bytesRead;
        }
        return totalBytesRead;
    }

    internal void LoopEarly() {
        _LoopEarly = true;
    }
}