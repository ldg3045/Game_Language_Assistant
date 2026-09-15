using GameLanguageAssistant.Audio;

var checks = 0;
void Check(string name, byte[] data, int bits, bool floating, float expected)
{
    var actual = AudioLevel.MeasurePeak(data, bits, floating);
    if (Math.Abs(actual - expected) > 0.00001f)
        throw new Exception($"{name}: expected {expected}, got {actual}");
    checks++;
}

Check("silence", new byte[16], 16, false, 0);
Check("PCM16 negative full scale", [0, 128], 16, false, 1);
Check("PCM16 half scale with incomplete trailing byte", [0, 64, 255], 16, false, 0.5f);
Check("PCM24 negative full scale", [0, 0, 128], 24, false, 1);
Check("PCM24 negative half scale", [0, 0, 192], 24, false, 0.5f);
Check("PCM32 negative full scale", [0, 0, 0, 128], 32, false, 1);
Check("float channels", BitConverter.GetBytes(0.25f).Concat(BitConverter.GetBytes(-0.75f)).ToArray(), 32, true, 0.75f);
Check("nonfinite ignored", BitConverter.GetBytes(float.NaN), 32, true, 0);
Check("clipping", BitConverter.GetBytes(1.2f), 32, true, 1);
Check("empty", [], 32, true, 0);
try
{
    AudioLevel.MeasurePeak([], 8, false);
    throw new Exception("Unsupported format was accepted");
}
catch (NotSupportedException) { checks++; }
Console.WriteLine($"PASS: {checks} audio level checks");

using (var buffer = new RecordingBuffer(new NAudio.Wave.WaveFormat(16000, 16, 1)))
{
    if (buffer.Complete() is not null) throw new Exception("Empty capture must not produce a recording");
    var samples = new byte[32000];
    samples[0] = 123;
    buffer.Append(samples);
    samples[0] = 0; // NAudio reuses its packet buffer; captured data must be copied.
    var clip = buffer.Complete()!;
    using var reader = new NAudio.Wave.WaveFileReader(new System.IO.MemoryStream(clip.WaveBytes));
    var firstFrame = new byte[reader.WaveFormat.BlockAlign];
    reader.ReadExactly(firstFrame);
    if (clip.Duration.TotalSeconds != 1 || reader.Length != 32000 || firstFrame[0] != 123)
        throw new Exception("WAV round trip or packet ownership failed");
}
using (var buffer = new RecordingBuffer(new NAudio.Wave.WaveFormat(16000, 16, 1)))
{
    var limit = 32000 * RecordingBuffer.MaxSeconds;
    if (!buffer.Append(new byte[limit + 100]) || !buffer.LimitReached)
        throw new Exception("Recording must stop at duration limit");
    buffer.Append(new byte[100]);
    if (buffer.Complete()!.Duration.TotalSeconds != RecordingBuffer.MaxSeconds)
        throw new Exception("Recording exceeded limit");
}
using (var buffer = new RecordingBuffer(new NAudio.Wave.WaveFormat(192000, 32, 8)))
{
    buffer.Append(new byte[RecordingBuffer.MaxBytes + 64]);
    var clip = buffer.Complete()!;
    using var reader = new NAudio.Wave.WaveFileReader(new System.IO.MemoryStream(clip.WaveBytes));
    if (reader.Length > RecordingBuffer.MaxBytes || reader.Length % reader.WaveFormat.BlockAlign != 0)
        throw new Exception("Memory cap or frame alignment failed");
}
Console.WriteLine("PASS: empty buffer, WAV round trip, copied packets, duration/memory limits");

// Exercise capture-format conversion independently of the recognition model.
foreach (var format in new NAudio.Wave.WaveFormat[]
{
    new(48000, 16, 2), new(44100, 24, 1), new(32000, 32, 2),
    NAudio.Wave.WaveFormat.CreateIeeeFloatWaveFormat(48000, 2),
    new NAudio.Wave.WaveFormatExtensible(48000, 32, 2)
})
{
    using var wav = new System.IO.MemoryStream();
    using (var writer = new NAudio.Wave.WaveFileWriter(wav, format))
        writer.Write(new byte[format.AverageBytesPerSecond], 0, format.AverageBytesPerSecond);
    var samples = GameLanguageAssistant.Speech.WhisperAudio.Convert(wav.ToArray(), CancellationToken.None);
    if (Math.Abs(samples.Length - 16000) > 32 || samples.Any(x => x != 0))
        throw new Exception($"Silence resampling failed: {format}, {samples.Length}");
}
var toneWav = new System.IO.MemoryStream();
using (var writer = new NAudio.Wave.WaveFileWriter(toneWav, NAudio.Wave.WaveFormat.CreateIeeeFloatWaveFormat(48000, 2)))
{
    for (var i = 0; i < 48000 * 2; i++)
    {
        var sample = (float)(0.15 * Math.Sin(2 * Math.PI * 440 * i / 48000));
        writer.WriteSample(sample);
        writer.WriteSample(sample);
    }
}
var toneBytes = toneWav.ToArray();
var converted = GameLanguageAssistant.Speech.WhisperAudio.Convert(toneBytes, CancellationToken.None);
if (Math.Abs(converted.Length - 32000) > 32 || converted.Max() < 0.1 || converted.Max() > 0.2)
    throw new Exception("Resampling duration/amplitude failed");
using (var canceled = new CancellationTokenSource())
{
    canceled.Cancel();
    try
    {
        GameLanguageAssistant.Speech.WhisperAudio.Convert(toneBytes, canceled.Token);
        throw new Exception("Conversion ignored cancellation");
    }
    catch (OperationCanceledException) { }
}
Console.WriteLine("PASS: PCM/float/extensible mono 16k conversion, amplitude, duration, cancellation");

if (args.Contains("--whisper-smoke"))
{
    var engine = new GameLanguageAssistant.Speech.LocalWhisperRecognizer(
        System.IO.Path.Combine(AppContext.BaseDirectory, "models", "ggml-base.bin"));
    var clock = System.Diagnostics.Stopwatch.StartNew();
    var text = await engine.RecognizeAsync(new RecordedAudio(toneBytes, TimeSpan.FromSeconds(2), false), CancellationToken.None);
    Console.WriteLine($"PASS: actual Whisper model inference, {clock.Elapsed.TotalSeconds:F1}s, output length {text.Length} (synthetic tone, not an accuracy test)");
    using var canceled = new CancellationTokenSource();
    canceled.Cancel();
    try
    {
        await engine.RecognizeAsync(new RecordedAudio(toneBytes, TimeSpan.FromSeconds(2), false), canceled.Token);
        throw new Exception("Recognizer ignored cancellation");
    }
    catch (OperationCanceledException) { Console.WriteLine("PASS: recognizer cancellation"); }
    using var duringInference = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
    try
    {
        await engine.RecognizeAsync(new RecordedAudio(toneBytes, TimeSpan.FromSeconds(2), false), duringInference.Token);
        throw new Exception("Active recognizer ignored cancellation");
    }
    catch (OperationCanceledException) { Console.WriteLine("PASS: cancellation during model loading/inference"); }
    await engine.RecognizeAsync(new RecordedAudio(toneBytes, TimeSpan.FromSeconds(2), false), CancellationToken.None);
    Console.WriteLine("PASS: inference succeeds again after cancellation");
    var missing = new GameLanguageAssistant.Speech.LocalWhisperRecognizer(
        System.IO.Path.Combine(AppContext.BaseDirectory, "models", "does-not-exist.bin"));
    try
    {
        await missing.RecognizeAsync(new RecordedAudio(toneBytes, TimeSpan.FromSeconds(2), false), CancellationToken.None);
        throw new Exception("Missing model was not reported");
    }
    catch (System.IO.FileNotFoundException) { Console.WriteLine("PASS: missing model error"); }
}
Array.Clear(toneBytes);
Array.Clear(converted);

// Explicit opt-in: briefly captures the first device, without saving or transmitting audio.
if (args.Contains("--microphone-smoke"))
{
    var devices = MicrophoneInput.GetDevices();
    Console.WriteLine($"Active microphone devices: {devices.Count}");
    if (devices.Count == 0) throw new Exception("No microphone available for hardware check");
    using var mic = new MicrophoneInput();
    for (var attempt = 0; attempt < 2; attempt++)
    {
        var stopped = new TaskCompletionSource<Exception?>(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler<NAudio.Wave.StoppedEventArgs> handler = (_, e) => stopped.TrySetResult(e.Exception);
        mic.Stopped += handler;
        mic.Start(devices[0].Id);
        await Task.Delay(500);
        mic.Stop();
        var error = await stopped.Task.WaitAsync(TimeSpan.FromSeconds(5));
        if (error is null)
        {
            var clip = mic.CompleteRecording();
            if (clip is null || clip.Duration <= TimeSpan.Zero)
                throw new Exception("Hardware input produced no buffered audio");
            using var reader = new NAudio.Wave.WaveFileReader(new System.IO.MemoryStream(clip.WaveBytes));
            if (reader.Length <= 0) throw new Exception("Invalid hardware WAV");
            Array.Clear(clip.WaveBytes);
        }
        mic.Dispose();
        mic.Stopped -= handler;
        if (error is not null) throw error;
    }
    // Dispose during active input mirrors closing the app without pressing Stop.
    mic.Start(devices[0].Id);
    await Task.Delay(200);
    mic.Dispose();
    Console.WriteLine("PASS: start/stop twice, restart, dispose while capturing");
}
