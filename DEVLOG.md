# 개발 일지

## 2026-09-13 — 첫 WPF 앱과 화면 수정 실습

### 날짜

2026-09-13 (한국 시간)

### 오늘 목표

기준 명세를 읽고 Windows 기술 스택을 선택한다. 작은 창에서 버튼을 누르면 영어와 한글 발음이 표시되게 만들고, 직접 값을 수정해 결과를 확인한다.

### 오늘 한 것

- `PROJECT_SPEC.md` 전체를 읽고 C#/WPF, Python/PySide6, TypeScript/Electron 후보를 비교했다.
- C# + .NET 10 + WPF로 첫 앱을 만들기로 했다. NAudio/WASAPI는 후속 오디오 단계의 추천 방향이며 아직 설치하지 않았다.
- 기존 PC에는 .NET 런타임만 있고 SDK가 없어 .NET SDK 10.0.401을 설치했다.
- Codex가 WPF 프로젝트, 기본 창, 버튼 클릭 함수, README, .gitignore를 작성했다.
- 사용자가 Cursor에서 영어 글자 크기를 24 → 30으로 수정하고 저장했다.
- 사용자가 예시를 `I'll come with you.` / `아일 컴 위드 유`로 수정하고 저장했다.
- Codex가 빌드·실행하고 사용자가 클릭 전후 화면을 확인했다.

### 성공한 것

- 최초 앱과 수정 후 앱의 빌드 성공. 마지막 빌드는 경고 0개, 오류 0개였다.
- 사용자 제공 화면으로 글자 크기 변경과 새 문장 표시를 확인했다.
- 화면 설정(XAML)과 클릭 동작(C#)을 각각 수정하는 첫 실습을 완료했다.

### 막힌 것

- SDK가 없어 설치가 필요했다. 최초 .NET 설정에는 사용자 폴더 접근 승인이 필요했다.
- 사이드바 직접 편집 안내가 부정확해 Cursor에서 수정하는 흐름으로 정정했다.
- 실행 중인 앱이 남아 파일 교체가 막혔다. 해당 프로세스를 종료한 뒤 재빌드했다.
- Cursor와 실행 앱 중 어느 창을 닫아야 하는지 혼동이 있어 ‘Cursor는 유지, 게임 영어 도우미는 종료’로 설명했다.

### 새로 알게 된 것

C#/WPF/XAML의 역할, 속성과 함수의 차이, xmlns의 기본 의미, 문자열 대입, 저장과 빌드의 차이를 설명하고 일부를 직접 실습했다. 독립적으로 전체 코드를 작성할 수 있게 되었다는 의미는 아니다.

자세한 복습은 [오늘의 TIL](docs/til/2026-09-13.md)에 기록했다.

### 다음 작업

마이크 입력 시작·종료 방법을 먼저 정하고, 오디오 라이브러리를 검토하여 마이크 소리 수신부터 구현한다.

### 현재 범위와 검증 한계

고정 문장 표시만 구현했다. 외부 API 호출, 마이크 녹음, STT, 번역, 자동 발음 생성, 게임 오버레이는 아직 없다. AI API 사용 비용이나 지연시간을 측정할 단계도 아니다. 자동 UI 테스트는 작성하지 않았으며 빌드와 사용자 화면 확인으로 검증했다. Git 커밋·원격 게시·배포는 수행하지 않았다.

### 관련 파일

- `src/GameLanguageAssistant/GameLanguageAssistant.csproj`: .NET/WPF 빌드 설정.
- `src/GameLanguageAssistant/App.xaml`, `App.xaml.cs`: 앱 시작 설정과 클래스.
- `src/GameLanguageAssistant/MainWindow.xaml`: 화면 구성.
- `src/GameLanguageAssistant/MainWindow.xaml.cs`: 클릭 시 고정 문장 표시.
- `README.md`: 실행과 파일 역할 안내.
- `.gitignore`: 빌드 결과물 등 제외 설정.

추천 커밋 메시지: `feat: add first WPF example window`
