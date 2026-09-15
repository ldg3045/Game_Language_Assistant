using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using GameLanguageAssistant;
using GameLanguageAssistant.Translation;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        CheckProtocolAsync().GetAwaiter().GetResult();
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
        CheckWindow();
        SynchronizationContext.SetSynchronizationContext(null);
        if (args.Contains("--local-model")) CheckLocalAsync().GetAwaiter().GetResult();
        Console.WriteLine("PASS: translation checks");
    }

    private static async Task CheckProtocolAsync()
    {
        var handler = new FakeHandler();
        using var client = new HttpClient(handler);
        var service = new OllamaTranslationService(client);
        handler.Body = Envelope("I'll come with you.", "아일 컴 위드 유.");
        var result = await service.TranslateAsync(" 나도 같이 갈게 ", CancellationToken.None);
        Require(result.English == "I'll come with you.", "Valid result");
        Require(handler.Requests.Count == 2, "Translation then pronunciation");
        using (var pronunciationRequest = JsonDocument.Parse(handler.Requests[1]))
            Require(pronunciationRequest.RootElement.GetProperty("messages")[1].GetProperty("content").GetString() == result.English, "Pronunciation uses exact translated English");
        using (var request = JsonDocument.Parse(handler.Requests[0]))
        {
            var root = request.RootElement;
            Require(root.GetProperty("messages")[1].GetProperty("content").GetString() == "나도 같이 갈게", "Current source sent");
            Require(root.GetProperty("model").GetString() == OllamaTranslationService.DefaultModel, "Local model");
            Require(!root.GetProperty("think").GetBoolean() && !root.GetProperty("stream").GetBoolean(), "Bounded output mode");
            Require(handler.Endpoint == "http://127.0.0.1:11434/api/chat", "Loopback only");
        }
        handler.Requests.Clear();
        var alternative = new OllamaTranslationService(client, "qwen3:4b");
        await alternative.TranslateAsync("나도 같이 갈게", CancellationToken.None);
        Require(handler.Requests.Count == 2 && handler.Requests.All(body =>
        {
            using var request = JsonDocument.Parse(body);
            return request.RootElement.GetProperty("model").GetString() == "qwen3:4b";
        }), "Both stages use injected candidate model without UI changes");
        foreach (var invalid in new[] { "", new string('가', 2001) })
            await ExpectAsync<ArgumentException>(() => service.TranslateAsync(invalid, CancellationToken.None));
        foreach (var invalid in new[] { "broken json", "{}", Envelope("", "발음"), Envelope("Hello", "hello"), Envelope("안녕", "안녕"), Envelope("Don't", "도न트"), Envelope("moment", "모 момент") })
        {
            handler.Body = invalid;
            await ExpectAsync<InvalidOperationException>(() => service.TranslateAsync("안녕", CancellationToken.None));
        }
        handler.Body = Envelope("Hello", "헬로").Replace("\"stop\"", "\"length\"");
        await ExpectAsync<InvalidOperationException>(() => service.TranslateAsync("안녕", CancellationToken.None));
        handler.Status = HttpStatusCode.NotFound;
        await ExpectAsync<InvalidOperationException>(() => service.TranslateAsync("안녕", CancellationToken.None));
        handler.Error = new HttpRequestException("offline");
        await ExpectAsync<InvalidOperationException>(() => service.TranslateAsync("안녕", CancellationToken.None));
        handler.Error = new TaskCanceledException();
        await ExpectAsync<TimeoutException>(() => service.TranslateAsync("안녕", CancellationToken.None));
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();
        await ExpectAsync<OperationCanceledException>(() => service.TranslateAsync("안녕", cancel.Token));
        Console.WriteLine("PASS: request, schema, invalid/empty/truncated output, missing model, connection, timeout, cancellation");
    }

    private static void CheckWindow()
    {
        var fake = new FakeTranslation();
        var window = new MainWindow(fake);
        var source = (TextBox)window.FindName("KoreanTranscript");
        var translate = (Button)window.FindName("TranslateButton");
        var cancel = (Button)window.FindName("CancelTranslationButton");
        var english = (TextBlock)window.FindName("EnglishText");
        var status = (TextBlock)window.FindName("TranslationStatus");
        source.Text = "수정된 원문";
        translate.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Require(fake.LastSource == "수정된 원문" && !translate.IsEnabled, "Edited source and double-submit protection");
        source.Text = "새 원문";
        Require(fake.Token.IsCancellationRequested, "Edit cancels pending translation");
        fake.Pending.SetResult(new("STALE", "이전 결과"));
        PumpUntil(() => translate.IsEnabled);
        Require(english.Text != "STALE", "Late result discarded");
        translate.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        fake.Pending.SetResult(new("New text.", "뉴 텍스트."));
        PumpUntil(() => translate.IsEnabled);
        Require(english.Text == "New text.", "Display result");
        source.Text = "다음 문장";
        Require(english.Text != "New text.", "Editing clears previous result");
        translate.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        cancel.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        fake.Pending.SetResult(new("CANCELED", "취소"));
        PumpUntil(() => translate.IsEnabled);
        Require(english.Text != "CANCELED" && status.Text.Contains("취소"), "Canceled result hidden");
        translate.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        fake.Pending.SetException(new InvalidOperationException("model unavailable"));
        PumpUntil(() => translate.IsEnabled);
        Require(status.Text.Contains("번역 실패"), "Failure permits retry");
        translate.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        window.Close();
        Require(fake.Token.IsCancellationRequested, "Close cancels request");
        fake.Pending.SetResult(new("CLOSED", "종료"));
        // Drain the queued continuation without opening a window or microphone.
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
        Require(english.Text != "CLOSED", "Closed window ignores result");
        Console.WriteLine("PASS: WPF edited source, pending edit, cancellation, retry, result invalidation, close");
    }

    private static async Task CheckLocalAsync()
    {
        using var client = OllamaTranslationService.CreateLocalClient();
        var service = new OllamaTranslationService(client);
        foreach (var source in new[] { "나도 같이 갈게.", "왼쪽에 두 명 있어.", "지금 들어가지 마. 여기서 기다려.", "탄약이 부족해. 잠깐 기다려 줘.", "오른쪽 계단으로 세 명이 올라가고 있어.", "나 재장전 중이야. 뒤를 봐 줘.", "아직 공격하지 않았어." })
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            var result = await service.TranslateAsync(source, CancellationToken.None).ConfigureAwait(false);
            Console.WriteLine($"LOCAL {clock.Elapsed.TotalSeconds:F1}s | {source} | {result.English} | {result.Pronunciation}");
        }
    }

    private static string Envelope(string english, string pronunciation) => JsonSerializer.Serialize(new
    {
        done = true, done_reason = "stop", message = new { content = JsonSerializer.Serialize(new { english, pronunciation }) }
    });

    private static async Task ExpectAsync<T>(Func<Task<TranslationResult>> action) where T : Exception
    {
        try { await action(); }
        catch (T) { return; }
        throw new Exception($"Expected {typeof(T).Name}");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static void PumpUntil(Func<bool> condition)
    {
        var frame = new DispatcherFrame();
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) };
        timer.Tick += (_, _) => { if (condition() || clock.Elapsed.TotalSeconds > 5) frame.Continue = false; };
        timer.Start();
        Dispatcher.PushFrame(frame);
        timer.Stop();
        Require(condition(), "UI continuation timeout");
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        public string Body = "";
        public List<string> Requests = [];
        public string? Endpoint;
        public HttpStatusCode Status = HttpStatusCode.OK;
        public Exception? Error;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (Error is not null) throw Error;
            Endpoint = request.RequestUri!.ToString();
            Requests.Add(await request.Content!.ReadAsStringAsync(token));
            return new HttpResponseMessage(Status) { Content = new StringContent(Body) };
        }
    }

    private sealed class FakeTranslation : ITranslationService
    {
        public string LastSource = "";
        public CancellationToken Token;
        public TaskCompletionSource<TranslationResult> Pending = new();
        public Task<TranslationResult> TranslateAsync(string korean, CancellationToken cancellationToken)
        {
            LastSource = korean;
            Token = cancellationToken;
            Pending = new TaskCompletionSource<TranslationResult>();
            return Pending.Task;
        }
    }
}
