using System.IO;
using System.Text;
using GameLanguageAssistant.Audio;
using Whisper.net;

namespace GameLanguageAssistant.Speech;

public sealed class LocalWhisperRecognizer(string modelPath) : ISpeechRecognizer
{
    public Task<string> RecognizeAsync(RecordedAudio audio, CancellationToken cancellationToken)
        => Task.Run(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!File.Exists(modelPath))
                throw new FileNotFoundException("Whisper 모델이 없습니다. README의 모델 준비 절차를 진행한 뒤 다시 인식하세요.");
            var samples = WhisperAudio.Convert(audio.WaveBytes, cancellationToken);
            try
            {
                // Skip digital silence only; this is not speech activity detection.
                if (samples.Length == 0 || samples.All(value => Math.Abs(value) < 0.0001f)) return "";
                using var factory = WhisperFactory.FromPath(modelPath, new WhisperFactoryOptions { UseGpu = false });
                using var processor = factory.CreateBuilder()
                    .WithLanguage("ko")
                    .WithThreads(Math.Clamp(Environment.ProcessorCount / 2, 1, 4))
                    .WithNoContext()
                    .Build();
                var text = new StringBuilder();
                // Native cancellation in this runtime can abort the process (GGML assertion).
                // Let inference finish, then discard canceled output before returning to the UI.
                cancellationToken.ThrowIfCancellationRequested();
                await foreach (var segment in processor.ProcessAsync(samples))
                {
                    var part = segment.Text.Trim();
                    if (part.Length == 0) continue;
                    if (text.Length > 0) text.Append(' ');
                    text.Append(part);
                }
                cancellationToken.ThrowIfCancellationRequested();
                return text.ToString();
            }
            finally { Array.Clear(samples); }
        }, cancellationToken);
}
