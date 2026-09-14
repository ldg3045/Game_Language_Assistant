using System.IO;
using NAudio.Wave;

namespace GameLanguageAssistant.Audio;

public sealed record RecordedAudio(byte[] WaveBytes, TimeSpan Duration, bool LimitReached);

/// <summary>Bounded, frame-aligned audio buffer. The caller owns completed WAV bytes.</summary>
public sealed class RecordingBuffer : IDisposable
{
    public const int MaxSeconds = 60;
    public const int MaxBytes = 16 * 1024 * 1024;
    private readonly MemoryStream data = new();
    private readonly WaveFormat format;
    private readonly int capacity;
    public bool LimitReached { get; private set; }

    public RecordingBuffer(WaveFormat format)
    {
        this.format = format;
        if (format.BlockAlign <= 0 || format.AverageBytesPerSecond <= 0)
            throw new ArgumentException("Invalid audio format", nameof(format));
        var limit = Math.Min((long)format.AverageBytesPerSecond * MaxSeconds, MaxBytes);
        capacity = (int)(limit / format.BlockAlign * format.BlockAlign);
    }

    public bool Append(ReadOnlySpan<byte> packet)
    {
        var count = Math.Min(packet.Length, capacity - (int)data.Length);
        count -= count % format.BlockAlign;
        data.Write(packet[..count]);
        LimitReached = data.Length >= capacity;
        return LimitReached;
    }

    // Called only after capture stops, so the audio producer cannot append concurrently.
    public RecordedAudio? Complete()
    {
        if (data.Length == 0) return null;
        var duration = TimeSpan.FromSeconds((double)data.Length / format.AverageBytesPerSecond);
        using var output = new MemoryStream();
        using (var writer = new WaveFileWriter(output, format))
        {
            data.Position = 0;
            data.CopyTo(writer);
        }
        // MemoryStream.ToArray remains valid after WaveFileWriter closes the stream.
        return new RecordedAudio(output.ToArray(), duration, LimitReached);
    }

    public void Dispose()
    {
        if (data.TryGetBuffer(out var bytes)) Array.Clear(bytes.Array!, bytes.Offset, bytes.Count);
        data.Dispose();
    }
}
