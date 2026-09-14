using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using GameLanguageAssistant.Audio;
using NAudio.Wave;
using System.Diagnostics;

namespace GameLanguageAssistant;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer meterTimer = new() { Interval = TimeSpan.FromMilliseconds(50) };
    private MicrophoneInput? microphone;
    private bool closing;
    private RecordedAudio? recordedAudio;
    private readonly Stopwatch recordingClock = new();
    private bool stopRequested;

    public MainWindow()
    {
        InitializeComponent();
        meterTimer.Tick += UpdateMeter;
        Loaded += (_, _) => RefreshDevices();
        Closed += (_, _) =>
        {
            closing = true;
            ReleaseMicrophone();
            ClearRecording();
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
        if (microphone is not null || DeviceList.SelectedItem is not MicrophoneDevice selected) return;
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
                    : $"수집 완료: {recordedAudio.Duration.TotalSeconds:F1}초 · {recordedAudio.WaveBytes.Length / 1024d:F1} KiB (WAV)\n음성 인식 서비스 연결 전입니다.";
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
        StartButton.IsEnabled = !active && DeviceList.SelectedItem is MicrophoneDevice;
        StopButton.IsEnabled = active && !stopRequested;
        DeviceList.IsEnabled = !active;
        RefreshButton.IsEnabled = !active;
        if (ClearRecordingButton is not null) ClearRecordingButton.IsEnabled = !active && recordedAudio is not null;
    }

    private void ClearRecording_Click(object sender, RoutedEventArgs e)
    {
        ClearRecording();
        UpdateControls();
    }

    private void ClearRecording()
    {
        if (recordedAudio is not null) Array.Clear(recordedAudio.WaveBytes);
        recordedAudio = null;
        RecordingStatus.Text = "수집한 음성 없음 · 최대 60초 / 16 MiB";
    }

    private void ShowMicrophoneError(Exception ex)
    {
        RecordingStatus.Text = "음성 수집 실패 — 다시 시작하세요.";
        MicrophoneStatus.Text = $"마이크 오류: {ex.Message}\n장치 연결과 Windows 마이크 접근 권한을 확인한 뒤 새로고침하세요.";
    }

    private void ShowExample_Click(object sender, RoutedEventArgs e)
    {
        // 첫 실습: AI 호출 없이 준비된 문장을 화면에 표시합니다.
        EnglishText.Text = "I'll come with you.";
        PronunciationText.Text = "아일 컴 위드 유";
    }
}
