namespace GameLanguageAssistant.Translation;

public sealed record TranslationResult(string English, string Pronunciation);

public interface ITranslationService
{
    Task<TranslationResult> TranslateAsync(string korean, CancellationToken cancellationToken);
}
