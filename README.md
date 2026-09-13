# 게임 영어 도우미

기준 명세는 [PROJECT_SPEC.md](PROJECT_SPEC.md)입니다.

현재는 버튼을 누르면 고정된 영어 예시와 한글 발음을 표시하는 첫 WPF 실습입니다.
마이크, AI API, 게임 오버레이는 아직 구현하지 않았습니다.

## 실행

Windows와 .NET 10 SDK가 필요합니다. 프로젝트 폴더의 PowerShell에서 실행합니다.

```powershell
dotnet run --project .\src\GameLanguageAssistant\GameLanguageAssistant.csproj
```

창이 열리면 **예시 문장 보기**를 누릅니다.
영어와 한글 발음이 나타나는지 확인하고, 창 크기를 줄였을 때 문장이 줄바꿈되는지도 확인합니다.

## 처음 읽을 파일

- `src/GameLanguageAssistant/MainWindow.xaml`: 창과 버튼, 글자의 배치. XAML은 '자멜'이라고 읽습니다.
- `src/GameLanguageAssistant/MainWindow.xaml.cs`: 버튼을 눌렀을 때 실행할 C# 코드.
- `src/GameLanguageAssistant/App.xaml`: 앱 시작 시 열 창을 지정합니다.
- `src/GameLanguageAssistant/App.xaml.cs`: 앱 클래스의 시작 틀입니다.
- `src/GameLanguageAssistant/GameLanguageAssistant.csproj`: .NET 버전과 WPF 사용 등 빌드 설정입니다.

실행 흐름: App → MainWindow → InitializeComponent로 화면 구성 → 버튼 클릭 → ShowExample_Click → 두 TextBlock의 Text 변경.

직접 바꿔볼 곳: MainWindow.xaml의 FontSize 또는 MainWindow.xaml.cs의 두 문장.
프로그램을 닫고 수정한 뒤 같은 명령으로 다시 실행하면 됩니다.
외부 API 요청과 사용 요금은 없습니다.
