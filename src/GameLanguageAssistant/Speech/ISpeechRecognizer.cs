using GameLanguageAssistant.Audio;

namespace GameLanguageAssistant.Speech;

public interface ISpeechRecognizer
{
    Task<string> RecognizeAsync(RecordedAudio audio, CancellationToken cancellationToken);
}
