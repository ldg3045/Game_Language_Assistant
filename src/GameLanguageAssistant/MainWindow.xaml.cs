using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using GameLanguageAssistant.Audio;
using NAudio.Wave;
using System.Diagnostics;
using System.IO;
using GameLanguageAssistant.Speech;
using GameLanguageAssistant.Translation;

namespace GameLanguageAssistant;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer meterTimer = new() { Interval = TimeSpan.FromMilliseconds(50) };
    private MicrophoneInput? microphone;
    private bool closing;
    private RecordedAudio? recordedAudio;
    private readonly Stopwatch recordingClock = new();
    private bool stopRequested;
    private readonly ISpeechRecognizer recognizer = new LocalWhisperRecognizer(Path.Combine(AppContext.BaseDirectory, "models", "ggml-base.bin"));
    private CancellationTokenSource? recognitionCancellation;
    private int transcriptVersion;
    private readonly System.Net.Http.HttpClient translationClient = OllamaTranslationService.CreateLocalClient();
    private readonly ITranslationService translationService;

    public MainWindow() : this(null) { }

    public MainWindow(ITranslationService? translationService)
    {
        this.translationService = translationService ?? new OllamaTranslationService(translationClient);
        InitializeComponent();
        KoreanTranscript.TextChanged += KoreanTranscript_TextChanged;
        meterTimer.Tick += UpdateMeter;
        Loaded += (_, _) => RefreshDevices();
        Closed += (_, _) =>
        {
            closing = true;
            ReleaseMicrophone();
            ClearRecording();
            translationClient.Dispose();
        };
    }

    private void RefreshDevices_Click(object sender, RoutedEventArgs e) => RefreshDevices();

    private void RefreshDevices()
    {
        if (microphone is not null) return;
        var previousId = (DeviceList.SelectedItem as MicrophoneDevice)?.Id;
        try
        {
            var devices = MicrophoneInput.GetDevices();
            DeviceList.ItemsSource = devices;
            DeviceList.SelectedItem = devices.FirstOrDefault(d => d.Id == previousId) ?? devices.FirstOrDefault();
            MicrophoneStatus.Text = devices.Count == 0
                ? "사용 가능한 마이크가 없습니다. 장치를 연결한 뒤 새로고침하세요."
                : "대기 — 사용할 마이크를 선택하세요.";
        }
        catch (Exception ex)
        {
            DeviceList.ItemsSource = null;
            ShowMicrophoneError(ex);
        }
        UpdateControls();
    }

    private void DeviceList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (StartButton is not null) UpdateControls();
    }

    private void StartMicrophone_Click(object sender, RoutedEventArgs e)
    {
        if (microphone is not null || recognitionCancellation is not null || translationCancellation is not null || DeviceList.SelectedItem is not MicrophoneDevice selected) return;
        ClearRecording();
        stopRequested = false;
        microphone = new MicrophoneInput();
        microphone.Stopped += MicrophoneStopped;
        try
        {
            microphone.Start(selected.Id);
            recordingClock.Restart();
            RecordingStatus.Text = "음성 수집 중 · 최대 60초 / 16 MiB";
            MicrophoneStatus.Text = "입력 중 — 마이크에 말하면 음량 막대가 움직입니다.";
            TranscriptionStatus.Text = "녹음 종료 후 한국어를 인식합니다.";
            meterTimer.Start();
        }
        catch (Exception ex)
        {
            ReleaseMicrophone();
            ShowMicrophoneError(ex);
        }
        UpdateControls();
    }

    private void StopMicrophone_Click(object sender, RoutedEventArgs e)
        => RequestStop();

    private void RequestStop()
    {
        if (microphone is null || stopRequested) return;
        stopRequested = true;
        StopButton.IsEnabled = false;
        MicrophoneStatus.Text = "마이크 종료 중…";
        try { microphone.Stop(); }
        catch (Exception ex)
        {
            ReleaseMicrophone();
            ShowMicrophoneError(ex);
            UpdateControls();
        }
    }

    private void MicrophoneStopped(object? sender, StoppedEventArgs e)
    {
        // Never synchronously wait for the UI from the audio thread (Dispose joins that thread).
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (closing || !ReferenceEquals(sender, microphone)) return;
            try
            {
                if (e.Exception is not null) throw e.Exception;
                recordedAudio = microphone!.CompleteRecording();
                var limited = recordedAudio?.LimitReached == true || recordingClock.Elapsed.TotalSeconds >= RecordingBuffer.MaxSeconds;
                RecordingStatus.Text = recordedAudio is null
                    ? "수집된 오디오가 없습니다. 마이크를 확인하고 다시 시작하세요."
                    : $"수집 완료: {recordedAudio.Duration.TotalSeconds:F1}초 · {recordedAudio.WaveBytes.Length / 1024d:F1} KiB (WAV)";
                MicrophoneStatus.Text = limited ? "입력 한도에 도달해 자동 종료했습니다." : "대기 — 마이크 입력을 종료했습니다.";
            }
            catch (Exception ex)
            {
                ClearRecording();
                ShowMicrophoneError(ex);
            }
            finally
            {
                ReleaseMicrophone();
                UpdateControls();
            }
            if (recordedAudio is not null) _ = RecognizeRecordingAsync();
        }));
    }

    private void UpdateMeter(object? sender, EventArgs e)
    {
        RecordingStatus.Text = $"음성 수집 중: {recordingClock.Elapsed.TotalSeconds:F1}초 / {RecordingBuffer.MaxSeconds}초";
        if (recordingClock.Elapsed.TotalSeconds >= RecordingBuffer.MaxSeconds) RequestStop();
        var peak = microphone?.ReadPeak() ?? 0;
        var db = peak > 0 ? 20 * Math.Log10(peak) : -60;
        LevelMeter.Value = Math.Clamp((db + 60) / 60 * 100, 0, 100);
        LevelText.Text = peak <= 0.001f ? "입력 음량: 매우 작음 / 무음" : $"입력 음량: {db:F0} dBFS";
    }

    private void ReleaseMicrophone()
    {
        meterTimer.Stop();
        recordingClock.Stop();
        if (microphone is not null)
        {
            microphone.Stopped -= MicrophoneStopped;
            microphone.Dispose();
            microphone = null;
        }
        LevelMeter.Value = 0;
        LevelText.Text = "입력 음량: —";
    }

    private void UpdateControls()
    {
        var active = microphone is not null;
        var recognizing = recognitionCancellation is not null;
        var translating = translationCancellation is not null;
        StartButton.IsEnabled = !active && !recognizing && !translating && DeviceList.SelectedItem is MicrophoneDevice;
        StopButton.IsEnabled = active && !stopRequested;
        DeviceList.IsEnabled = !active;
        RefreshButton.IsEnabled = !active;
        if (ClearRecordingButton is not null) ClearRecordingButton.IsEnabled = !active && recordedAudio is not null;
        if (RetryRecognitionButton is not null) RetryRecognitionButton.IsEnabled = !active && !recognizing && !translating && recordedAudio is not null;
        if (CancelRecognitionButton is not null) CancelRecognitionButton.IsEnabled = recognizing && !recognitionCancellation!.IsCancellationRequested;
        if (TranslateButton is not null) TranslateButton.IsEnabled = !active && !recognizing && !translating && !string.IsNullOrWhiteSpace(KoreanTranscript.Text);
        if (CancelTranslationButton is not null) CancelTranslationButton.IsEnabled = translating && !translationCancellation!.IsCancellationRequested;
    }

    private void ClearRecording_Click(object sender, RoutedEventArgs e)
    {
        ClearRecording();
        UpdateControls();
    }

    private void ClearRecording()
    {
        transcriptVersion++;
        recognitionCancellation?.Cancel();
        ResetTranslation();
        KoreanTranscript.Clear();
        KoreanTranscript.IsReadOnly = true;
        TranscriptionStatus.Text = "마이크를 시작하고 한국어로 말해 주세요.";
        if (recordedAudio is not null) Array.Clear(recordedAudio.WaveBytes);
        recordedAudio = null;
        RecordingStatus.Text = "수집한 음성 없음 · 최대 60초 / 16 MiB";
    }

    private async void RetryRecognition_Click(object sender, RoutedEventArgs e) => await RecognizeRecordingAsync();

    private void CancelRecognition_Click(object sender, RoutedEventArgs e)
    {
        recognitionCancellation?.Cancel();
        TranscriptionStatus.Text = "인식 취소 중… 진행 중인 계산이 끝나면 결과를 버립니다.";
        UpdateControls();
    }

    private async Task RecognizeRecordingAsync()
    {
        if (closing || microphone is not null || recordedAudio is null || recognitionCancellation is not null || translationCancellation is not null) return;
        // The worker owns a copy: clearing/closing can erase the UI's recording safely.
        var snapshot = recordedAudio with { WaveBytes = recordedAudio.WaveBytes.ToArray() };
        var version = ++transcriptVersion;
        var cancellation = new CancellationTokenSource();
        recognitionCancellation = cancellation;
        KoreanTranscript.Clear();
        KoreanTranscript.IsReadOnly = true;
        TranscriptionStatus.Text = "한국어를 인식하고 있습니다. 잠시 기다려 주세요.";
        UpdateControls();
        var clock = Stopwatch.StartNew();
        try
        {
            var text = await recognizer.RecognizeAsync(snapshot, cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            if (closing || version != transcriptVersion) return;
            KoreanTranscript.Text = text;
            KoreanTranscript.IsReadOnly = string.IsNullOrWhiteSpace(text);
            TranscriptionStatus.Text = string.IsNullOrWhiteSpace(text)
                ? "인식된 말이 없습니다. 다시 녹음해 주세요."
                : $"인식 완료 · {clock.Elapsed.TotalSeconds:F1}초 — 필요하면 원문을 수정하세요.";
        }
        catch (OperationCanceledException)
        {
            if (!closing && version == transcriptVersion)
                TranscriptionStatus.Text = "인식을 취소했습니다. 다시 인식하거나 새로 녹음할 수 있습니다.";
        }
        catch (Exception ex)
        {
            if (!closing && version == transcriptVersion)
                TranscriptionStatus.Text = $"음성 인식 실패: {ex.Message}\n모델과 실행 환경을 확인한 뒤 다시 인식하세요.";
        }
        finally
        {
            Array.Clear(snapshot.WaveBytes);
            recognitionCancellation = null;
            cancellation.Dispose();
            if (!closing) UpdateControls();
        }
    }

    private void ShowMicrophoneError(Exception ex)
    {
        RecordingStatus.Text = "음성 수집 실패 — 다시 시작하세요.";
        MicrophoneStatus.Text = $"마이크 오류: {ex.Message}\n장치 연결과 Windows 마이크 접근 권한을 확인한 뒤 새로고침하세요.";
    }

}
