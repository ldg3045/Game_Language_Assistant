# 게임 영어 도우미

기준 명세는 [PROJECT_SPEC.md](PROJECT_SPEC.md)입니다.

현재는 마이크 선택, 입력 시작·종료, 실시간 음량 확인을 지원합니다.
고정된 영어 예시와 한글 발음 표시도 유지합니다. AI API와 게임 오버레이는 아직 구현하지 않았습니다.

## 실행

Windows와 .NET 10 SDK가 필요합니다. 프로젝트 폴더의 PowerShell에서 실행합니다.

```powershell
dotnet run --project .\src\GameLanguageAssistant\GameLanguageAssistant.csproj
```

1. 입력 마이크를 선택하고 **마이크 시작**을 누릅니다.
2. 말할 때 음량 막대가 움직이는지 확인합니다. dBFS는 디지털 음량 단위로 0에 가까울수록 큰 입력입니다. 실제 소음계의 데시벨 값은 아닙니다.
3. **마이크 종료**를 누르면 입력이 멈추고 수집한 오디오의 길이와 WAV 용량이 표시됩니다.
4. 다른 장치를 연결했다면 입력 종료 후 **새로고침**을 누릅니다.

입력은 최대 60초 또는 원시 오디오 16 MiB까지 메모리에 모읍니다. 한도에 도달하면 자동 종료합니다.
종료 시 WAV 데이터를 메모리에 준비하며 파일 저장·재생·서버 전송을 하지 않습니다. STT 서비스에 따라 이후 형식 변환이 필요할 수 있습니다.
최신 음성 하나만 유지하며 **수집한 음성 지우기**, 새 입력 시작, 창 닫기로 제거합니다. 오류로 종료한 입력은 폐기합니다.
표시 시간은 수신한 오디오 길이이며, 무음도 포함됩니다. 말소리 인식 성공을 의미하지 않습니다.
게임 마이크의 음소거 상태는 제어하지 않습니다. 기존 **예시 문장 보기**는 마이크와 관계없이 고정 문장을 표시합니다.

장치가 없으면 시작 버튼을 사용할 수 없습니다. 입력 실패 시 장치 연결 및 Windows의 마이크 접근 권한을 확인하세요.
장치 분리 오류가 보고되면 입력을 종료하고 다시 선택할 수 있도록 합니다. 드라이버가 오류를 보고하지 않는 경우 무음으로 보일 수 있습니다.

## 검증

```powershell
dotnet run --project .\tests\AudioLevel.Checks\AudioLevel.Checks.csproj
```

실제 첫 번째 마이크를 잠시 사용하는 시작·종료 검사(선택):

```powershell
dotnet run --project .\tests\AudioLevel.Checks\AudioLevel.Checks.csproj -- --microphone-smoke
```

수동 확인: 시작·종료 반복, 말소리에 따른 음량 변화, 입력 중 장치 분리 후 복구, 입력 중 창 닫기, 기존 예시 버튼 동작.

## 처음 읽을 파일

- `src/GameLanguageAssistant/MainWindow.xaml`: 창과 버튼, 글자의 배치. XAML은 '자멜'이라고 읽습니다.
- `src/GameLanguageAssistant/MainWindow.xaml.cs`: 버튼을 눌렀을 때 실행할 C# 코드.
- `src/GameLanguageAssistant/Audio/MicrophoneInput.cs`: WASAPI 공유 모드 입력과 장치 자원 관리.
- `src/GameLanguageAssistant/Audio/AudioLevel.cs`: PCM 및 32비트 실수 오디오의 최대 음량 계산.
- `src/GameLanguageAssistant/Audio/RecordingBuffer.cs`: 길이·용량 제한이 있는 메모리 수집과 WAV 변환.
- `src/GameLanguageAssistant/App.xaml`: 앱 시작 시 열 창을 지정합니다.
- `src/GameLanguageAssistant/App.xaml.cs`: 앱 클래스의 시작 틀입니다.
- `src/GameLanguageAssistant/GameLanguageAssistant.csproj`: .NET 버전과 WPF 사용 등 빌드 설정입니다.

마이크 흐름: 시작 버튼 → MicrophoneInput.Start → 오디오 패킷 음량 계산 → 50ms 간격 화면 갱신 → 종료 버튼 → 입력 정지 및 자원 해제.

프로그램을 닫고 수정한 뒤 같은 명령으로 다시 실행하면 됩니다.
외부 API 요청과 사용 요금은 없습니다.
