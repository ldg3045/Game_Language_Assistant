# 로컬 번역 실행과 구조

**모델 선정 상태:** qwen3:4b와 현재 기본값 qwen3:8b는 모두 MVP 후보이며 최종 확정 모델이 아니다. 다른 로컬 모델과 외부 API 비교는 후속 작업이다.

## 처음 준비할 때

Ollama는 로컬 언어 모델 실행 도구다. Whisper와 달리 별도 프로세스로 실행하며 앱은 `127.0.0.1:11434`로만 요청한다. 클라우드 모델이나 외부 API로 자동 전환하지 않는다.

```powershell
winget install --id Ollama.Ollama --exact --source winget
ollama pull qwen3:8b
```

Ollama 설치 직후에는 새 터미널에서 실행해야 PATH가 반영될 수 있다. 기본 설치 경로는 `%LOCALAPPDATA%\Programs\Ollama\ollama.exe`다. 8B 모델은 약 5.2GB이고 Ollama가 사용자 폴더에 관리한다. 프로젝트 Git에는 올리지 않는다. 모델이 준비된 뒤에는 로컬 번역에 인터넷 연결·계정·API 키가 필요하지 않다.

Ollama가 실행된 상태에서 게임 영어 도우미를 실행한다. 연결 실패 또는 모델 누락은 상태 문구로 안내하며, 원문은 유지하므로 준비 후 다시 번역할 수 있다.

## 사용 흐름

마이크 시작 → 한국어 말하기 → 종료 → 원문 확인·수정 → 번역하기 → 영어·한글 발음 표시.

- 원문을 고치면 이전 번역을 지운다. 진행 중인 옛 원문의 결과도 표시하지 않는다.
- 번역 취소와 실패 후 재시도를 지원한다. 중복 요청과 음성 인식·번역 동시 시작을 막는다.
- 원문은 최대 2000자이며 120초 응답 제한을 둔다. 모델 응답이 잘리거나 영어·발음 필드가 없으면 완료로 표시하지 않는다.
- 현재 발화 한 개만 번역한다. 최근 대화 문맥은 아직 전달하지 않는다. 모델에 영어 번역을 먼저 요청하고, 확정된 영어의 한글 발음을 두 번째 요청으로 생성한다. 두 단계가 모두 끝나야 한 쌍의 결과를 표시한다.
- 앱에서 음성·원문·번역을 파일로 기록하지 않는다. 원문 텍스트만 로컬 Ollama에 보낸다. Ollama 자체 실행 로그·모델 파일은 별도 도구의 관리 범위다.
- 모델은 요청 후 약 1분간 메모리에 유지하도록 요청한다. 실제 CPU/GPU 선택은 Ollama가 관리하므로 게임 실행 중 성능을 따로 확인한다.

## 최소 교체 구조

`ITranslationService.TranslateAsync(한국어, 취소 토큰)` → `TranslationResult(영어, 한글 발음)`.

- `Translation/ITranslationService.cs`: 화면이 사용하는 공통 규약과 결과 형식.
- `Translation/OllamaTranslationService.cs`: 로컬 주소, 모델, 지시문, JSON 응답 검증. 생성자의 `model` 인자로 다른 로컬 후보를 지정한다. `DefaultModel`은 임시 기본값이며 `TranslationPrompt`, `PronunciationPrompt`가 표현을 조정할 주요 위치다. `RequestTextAsync`가 두 단계의 공통 HTTP 처리를 맡는다.
- `MainWindow.Translation.cs`: 번역 버튼, 원문 변경 시 결과 무효화, 취소·상태·시간 표시.
- `MainWindow.xaml.cs`: 현재 로컬 구현을 생성한다. 추후 API 구현을 연결할 위치이며 별도 DI 프레임워크나 플러그인 구조는 도입하지 않았다.

한글 발음은 모델이 생성하는 근사 표기다. 형식 검사가 의미나 발음의 정확성까지 보장하지 않는다. 명사·숫자·부정문과 읽기 쉬운 발음을 실제 사용으로 확인해야 한다. 번역기가 답변을 대신하거나 원문에 없는 상황을 추가하지 않도록 지시했지만 모델이 항상 준수한다고 보장하지 않는다.

현재 방식은 **LLM 직접 발음 생성 + 프로그램의 형식 검증** 조합이다. 공백 정리 외에 발음을 교정하는 후처리는 없다. ‘콜럼’처럼 틀려도 한글만으로 구성된 출력은 통과할 수 있다. 영어 번역과 발음 중 한 단계라도 형식 검증이 실패하면 결과 쌍을 표시하지 않는다.

향후 안정성 개선 시 번역 서비스와 발음 변환 모듈을 분리하면 번역 모델을 바꿔도 동일한 영어 문장에 같은 발음 규칙을 적용할 수 있다. 그때 발음 사전·G2P(철자에서 음소로 변환)·한글 표기 규칙과 현재 LLM 방식을 비교한다. 아직 이 모듈을 구현하거나 사전 방식을 선택한 것은 아니다.

## 검사

```powershell
dotnet run --project tests/Translation.Checks/Translation.Checks.csproj
dotnet run --project tests/Translation.Checks/Translation.Checks.csproj -- --local-model
```

첫 명령은 가짜 응답으로 오류·취소·원문 수정 후 늦은 결과·재시도·창 닫기를 검사하며 네트워크와 마이크를 사용하지 않는다. 두 번째는 로컬 모델에 짧은 시험 문장 7개를 요청한다. 전체 언어 품질 평가나 게임 성능 검사는 아니다.

2026-09-15의 최종 두 단계 구성 시험은 첫 문장 약 2.9초, 이후 짧은 문장은 약 0.3~0.4초였다. 이전 첫 로딩 시험에서는 더 오래 걸렸으므로 일정한 응답 시간을 보장하지 않는다. `Ammo is low. Wait a moment.`와 부정문은 생성됐지만 `come`을 ‘콜럼’처럼 표기하거나 긴 발음에서 단어가 틀리는 사례가 남았다. **현재 한글 발음은 품질 검증을 통과한 기능이 아니라 초기 자동 생성 결과**다. 모델 또는 발음 생성 방식의 후속 개선이 필요하다.

공식 참고: [Ollama Windows](https://docs.ollama.com/windows), [Chat API](https://docs.ollama.com/api/chat), [구조화 응답](https://docs.ollama.com/capabilities/structured-outputs), [Qwen3 모델](https://ollama.com/library/qwen3).
