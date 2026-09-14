using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace GameLanguageAssistant.Audio;

public sealed record MicrophoneDevice(string Id, string Name);

/// <summary>Shared-mode microphone capture with a bounded in-memory recording.</summary>
public sealed class MicrophoneInput : IDisposable
{
    private MMDevice? device;
    private WasapiCapture? capture;
    private float peak;
    private RecordingBuffer? recording;

    public event EventHandler<StoppedEventArgs>? Stopped;

    public static IReadOnlyList<MicrophoneDevice> GetDevices()
    {
        using var enumerator = new MMDeviceEnumerator();
        var result = new List<MicrophoneDevice>();
        foreach (var endpoint in enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
        {
            using (endpoint)
                result.Add(new MicrophoneDevice(endpoint.ID, endpoint.FriendlyName));
        }
        return result;
    }

    public void Start(string deviceId)
    {
        if (capture is not null)
            throw new InvalidOperationException("마이크가 이미 사용 중입니다.");

        try
        {
            using var enumerator = new MMDeviceEnumerator();
            device = enumerator.GetDevice(deviceId);
            // Keep the device's mix format; do not require a particular sample rate.
            capture = new WasapiCapture(device);
            var format = capture.WaveFormat;
            var encoding = GetEncoding(format);
            if (!((encoding == WaveFormatEncoding.IeeeFloat && format.BitsPerSample == 32) ||
                  (encoding == WaveFormatEncoding.Pcm && format.BitsPerSample is 16 or 24 or 32)))
                throw new NotSupportedException("이 마이크의 오디오 형식은 아직 지원하지 않습니다.");

            recording = new RecordingBuffer(format);
            capture.DataAvailable += OnDataAvailable;
            capture.RecordingStopped += OnRecordingStopped;
            capture.StartRecording();
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public float ReadPeak() => Interlocked.Exchange(ref peak, 0);

    public void Stop() => capture?.StopRecording();

    // Invoke after the Stopped event and before disposing this capture session.
    public RecordedAudio? CompleteRecording() => recording?.Complete();

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (sender is not WasapiCapture source) return;
        var format = source.WaveFormat;
        if (recording?.Append(e.Buffer.AsSpan(0, e.BytesRecorded)) == true)
            source.StopRecording();
        var measured = AudioLevel.MeasurePeak(e.Buffer.AsSpan(0, e.BytesRecorded),
            format.BitsPerSample, GetEncoding(format) == WaveFormatEncoding.IeeeFloat);
        // One capture producer; UI consumes at a fixed rate, without queuing every audio packet.
        float previous;
        do
        {
            previous = Volatile.Read(ref peak);
            if (measured <= previous) return;
        } while (Interlocked.CompareExchange(ref peak, measured, previous) != previous);
    }

    private static WaveFormatEncoding GetEncoding(WaveFormat format)
    {
        if (format is WaveFormatExtensible extended)
        {
            if (extended.SubFormat == new Guid("00000003-0000-0010-8000-00aa00389b71"))
                return WaveFormatEncoding.IeeeFloat;
            if (extended.SubFormat == new Guid("00000001-0000-0010-8000-00aa00389b71"))
                return WaveFormatEncoding.Pcm;
        }
        return format.Encoding;
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e) => Stopped?.Invoke(this, e);

    public void Dispose()
    {
        if (capture is not null)
        {
            capture.DataAvailable -= OnDataAvailable;
            capture.RecordingStopped -= OnRecordingStopped;
            capture.Dispose();
            capture = null;
        }
        device?.Dispose();
        device = null;
        recording?.Dispose();
        recording = null;
        Interlocked.Exchange(ref peak, 0);
    }
}
