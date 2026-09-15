using System.IO;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace GameLanguageAssistant.Speech;

public static class WhisperAudio
{
    // Whisper expects mono float samples at 16 kHz, regardless of the capture device format.
    public static float[] Convert(byte[] waveBytes, CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream(waveBytes, writable: false);
        using var reader = new WaveFileReader(stream);
        var format = NormalizeFormat(reader.WaveFormat);
        using var source = new RawSourceWaveStream(reader, format);
        ISampleProvider samples = new MonoProvider(source.ToSampleProvider());
        if (samples.WaveFormat.SampleRate != 16000)
            samples = new WdlResamplingSampleProvider(samples, 16000);
        var result = new float[16000 * 61];
        var length = 0;
        try
        {
            while (length < result.Length)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var count = samples.Read(result, length, Math.Min(4096, result.Length - length));
                if (count == 0) break;
                length += count;
            }
            return result.AsSpan(0, length).ToArray();
        }
        finally { Array.Clear(result); }
    }

    private static WaveFormat NormalizeFormat(WaveFormat format)
    {
        if (format is WaveFormatExtensible ext) return ext.ToStandardWaveFormat();
        // WaveFileReader preserves an extensible WAV header as WaveFormatExtraData.
        if (format.Encoding == WaveFormatEncoding.Extensible && format is WaveFormatExtraData extra && extra.ExtraSize >= 22)
        {
            var subtype = new Guid(extra.ExtraData.AsSpan(6, 16));
            if (subtype == new Guid("00000003-0000-0010-8000-00aa00389b71") && format.BitsPerSample == 32)
                return WaveFormat.CreateIeeeFloatWaveFormat(format.SampleRate, format.Channels);
            if (subtype == new Guid("00000001-0000-0010-8000-00aa00389b71"))
                return new WaveFormat(format.SampleRate, format.BitsPerSample, format.Channels);
            throw new NotSupportedException("변환할 수 없는 마이크 오디오 형식입니다.");
        }
        return format;
    }

    private sealed class MonoProvider(ISampleProvider source) : ISampleProvider
    {
        public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(source.WaveFormat.SampleRate, 1);

        public int Read(float[] buffer, int offset, int count)
        {
            var channels = source.WaveFormat.Channels;
            var input = new float[count * channels];
            try
            {
                var read = source.Read(input, 0, input.Length);
                var frames = read / channels;
                for (var i = 0; i < frames; i++)
                {
                    double sum = 0;
                    for (var ch = 0; ch < channels; ch++)
                    {
                        var value = input[i * channels + ch];
                        if (float.IsFinite(value)) sum += value;
                    }
                    buffer[offset + i] = (float)Math.Clamp(sum / channels, -1, 1);
                }
                return frames;
            }
            finally { Array.Clear(input); }
        }
    }
}
