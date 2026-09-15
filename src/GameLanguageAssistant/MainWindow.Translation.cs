using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace GameLanguageAssistant;

public partial class MainWindow
{
    private CancellationTokenSource? translationCancellation;
    private int translationVersion;

    private void KoreanTranscript_TextChanged(object sender, TextChangedEventArgs e)
    {
        ResetTranslation();
        UpdateControls();
    }

    private void ResetTranslation()
    {
        translationVersion++;
        translationCancellation?.Cancel();
        EnglishText.Text = "여기에 영어 번역이 표시됩니다.";
        PronunciationText.Text = "여기에 한글 발음이 표시됩니다.";
        TranslationStatus.Text = "원문을 확인한 뒤 번역하기를 누르세요.";
    }

    private async void Translate_Click(object sender, RoutedEventArgs e)
    {
        if (closing || microphone is not null || recognitionCancellation is not null || translationCancellation is not null) return;
        var source = KoreanTranscript.Text.Trim();
        if (source.Length == 0) return;
        ResetTranslation();
        var version = translationVersion;
        using var cancellation = new CancellationTokenSource();
        translationCancellation = cancellation;
        TranslationStatus.Text = "영어와 한글 발음을 생성하고 있습니다. 첫 실행은 모델 로딩으로 더 걸릴 수 있습니다.";
        UpdateControls();
        var clock = Stopwatch.StartNew();
        try
        {
            var result = await translationService.TranslateAsync(source, cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            if (closing || version != translationVersion) return;
            EnglishText.Text = result.English;
            PronunciationText.Text = result.Pronunciation;
            TranslationStatus.Text = $"번역 완료 · {clock.Elapsed.TotalSeconds:F1}초 — 한글 발음은 아직 오차가 있어 확인이 필요합니다.";
        }
        catch (OperationCanceledException)
        {
            if (!closing && version == translationVersion) TranslationStatus.Text = "번역을 취소했습니다.";
        }
        catch (Exception ex)
        {
            if (!closing && version == translationVersion) TranslationStatus.Text = $"번역 실패: {ex.Message}";
        }
        finally
        {
            translationCancellation = null;
            if (!closing) UpdateControls();
        }
    }

    private void CancelTranslation_Click(object sender, RoutedEventArgs e)
    {
        translationCancellation?.Cancel();
        TranslationStatus.Text = "번역 취소 중…";
        UpdateControls();
    }
}
