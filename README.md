# 게임 영어 도우미

기준 명세는 [PROJECT_SPEC.md](PROJECT_SPEC.md)입니다.

번역용 Ollama 설치·모델 준비와 사용법은 [로컬 번역 안내](docs/LOCAL_TRANSLATION.md)를 참고하세요.

현재는 마이크 선택, 입력 시작·종료, 음량 확인과 로컬 Whisper 한국어 인식을 지원합니다.
인식된 한국어 원문은 직접 수정할 수 있습니다. **번역하기**를 누르면 로컬 Ollama 모델이 영어와 한글 발음을 생성합니다. 게임 오버레이는 아직 구현하지 않았습니다.

## 실행

Windows와 .NET 10 SDK가 필요합니다. 프로젝트 폴더의 PowerShell에서 실행합니다.

첫 실행 전에 모델을 준비합니다. 다국어 Whisper base 약 148MB를 Hugging Face의 ggerganov/whisper.cpp에서 내려받고 SHA-256을 검증합니다. 모델은 Git에 올리지 않습니다.

```powershell
./scripts/Get-WhisperModel.ps1
```

그다음 아래 명령으로 실행하면 `models/ggml-base.bin`이 실행 폴더에 복사됩니다. 모델 준비 후 앱의 음성 인식에는 인터넷이나 API 키가 필요하지 않습니다. 모델이 누락되면 상태 영역에서 안내합니다. Whisper.net 1.9.1 CPU 런타임의 Windows 11 및 Visual C++ 2022 x64 런타임 요구사항은 [공식 문서](https://github.com/sandrohanea/whisper.net)를 확인하세요. 현재 구성은 AVX 계열 명령을 지원하는 CPU를 대상으로 검증했으며 구형 CPU 지원은 검증하지 않았습니다.

```powershell
dotnet run --project .\src\GameLanguageAssistant\GameLanguageAssistant.csproj
```

1. 입력 마이크를 선택하고 **마이크 시작**을 누릅니다.
2. 말할 때 음량 막대가 움직이는지 확인합니다. dBFS는 디지털 음량 단위로 0에 가까울수록 큰 입력입니다. 실제 소음계의 데시벨 값은 아닙니다.
3. **마이크 종료**를 누르면 길이·WAV 용량을 표시하고 자동으로 한국어 인식을 시작합니다.
4. 완료된 **한국어 원문**을 읽고 필요하면 직접 수정합니다. 처리 시간은 모델 로딩·오디오 변환·인식을 포함하며 녹음 시간은 제외합니다.
5. **인식 취소**는 진행 중인 내부 계산이 끝나면 결과를 버립니다. 취소 완료 후 보관한 음성으로 **다시 인식**하거나 새로 녹음할 수 있습니다. 다시 인식하면 수정한 원문도 새 결과로 교체됩니다.
6. 다른 장치를 연결했다면 입력 종료 후 **새로고침**을 누릅니다.
7. 원문을 확인·수정한 뒤 **번역하기**를 누릅니다. 영어와 한글 발음을 함께 표시합니다. 원문을 수정하면 이전 번역은 지워지므로 다시 번역해야 합니다.

입력은 최대 60초 또는 원시 오디오 16 MiB까지 메모리에 모읍니다. 한도에 도달하면 자동 종료합니다.
종료 시 WAV 데이터를 메모리에 준비하며 음성 파일 저장·재생·서버 전송을 하지 않습니다. 인식 시 16kHz 모노 실수 샘플로 변환합니다.
최신 음성 하나만 유지하며 **수집한 음성 지우기**, 새 입력 시작, 창 닫기로 음성과 원문을 제거합니다. 인식 작업은 별도 복사본을 사용하고 종료 시 비웁니다. 오류로 종료한 마이크 입력은 폐기하며, 인식만 실패하면 음성을 유지해 재시도할 수 있습니다.
표시 시간은 수신한 오디오 길이이며, 무음도 포함됩니다. 말소리 인식 성공을 의미하지 않습니다.
게임 마이크의 음소거 상태는 제어하지 않습니다. 기존 **예시 문장 보기**는 실제 **번역하기**로 교체했습니다.

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

수동 확인: 시작·종료 반복, 말소리에 따른 음량 변화, 입력 중 장치 분리 후 복구, 입력 중 창 닫기, 원문 수정 후 번역 버튼 동작.

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

## 로컬 음성 인식 구현과 한계

- `Speech/ISpeechRecognizer.cs`: 녹음 데이터를 원문으로 바꾸는 공통 규약. API 엔진 추가 시 화면과 녹음 부분을 유지하기 위한 경계입니다.
- `Speech/WhisperAudio.cs`: PCM/실수/확장 WAV를 모노 16kHz로 변환합니다.
- `Speech/LocalWhisperRecognizer.cs`: 한국어(`ko`)로 인식하며 CPU 스레드는 최대 4개를 사용합니다. 현재는 요청마다 모델을 열고 해제합니다.
- `MainWindow.xaml.cs`의 `RecognizeRecordingAsync`: 자동 인식, 취소, 실패, 재시도와 수정 가능한 결과 표시를 연결합니다. 수정된 원문은 번역 버튼을 누를 때 사용합니다.

base는 첫 검증 모델입니다. 게임 용어·작은 목소리·잡음에서 오인식할 수 있으며 무음에 문장을 만들어 낼 수도 있습니다. 디지털 무음만 간단히 제외하며 VAD(실제 발화 구간 감지)는 구현하지 않았습니다. 게임 중 성능·한국어 정확도와 실제 사용자 입력의 응답 시간은 수동 확인해야 합니다.

모델 실행 검사(마이크나 외부 전송 없이 합성 신호 사용):

```powershell
dotnet run --project ./tests/AudioLevel.Checks/AudioLevel.Checks.csproj -- --whisper-smoke
```

수동 확인: 짧은 한국어 녹음 후 원문 표시·수정, 새 녹음 시 이전 원문 삭제, 인식 중 취소·지우기·창 닫기, 실패 후 재시도. 모델 및 라이브러리 출처는 [외부 구성요소 안내](docs/THIRD_PARTY_NOTICES.md)를 참조하세요.
