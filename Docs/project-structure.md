# 프로젝트 구조와 파일 규칙

## 폴더 구조

```
CLAUDE.md                      Claude 지침 (핵심 규칙 + 문서 지도)
README.md                      최상위 목차: 주제별 실습 상태·핵심 결론·링크 (아래 "최상위 README")
Docs/                          분야별 지침 문서 (이 파일 포함)
Assets/Main.unity               유일한 공용 씬. Build Settings에 이것 하나만 등록 (아래 "씬 운영")
Assets/Labs/
  Labs.asmdef                  런타임 실습 어셈블리 (rootNamespace: Labs)
  Common/
    LabRunner.cs               공통 실행기: ILab/[Lab]/LabContext 정의, -lab 인자 또는 SessionState로 실습 선택, CSV 출력, 종료
    LabEval.cs                 식 평가 결과·예외 타입 문자열화, 콘솔 출력 캡처
    LabMetrics.cs              ProfilerRecorder 래퍼 (GC Allocated In Frame / Allocation Count / GC Used / GC Reserved)
    Editor/
      Labs.Editor.asmdef       에디터 전용 (rootNamespace: Labs.Editor)
      LabBuilder.cs            Development 빌드(Mono/IL2CPP), Main + 실습 전용 씬 포함, Scripting Backend 복원
      LabMenu.cs               에디터 메뉴 "Labs/..."로 Main 씬을 열고 플레이 모드에서 실습 실행
  Tests/
    Labs.Tests.asmdef          EditMode 테스트 (Is.Not.AllocatingGCMemory 등)
  01_NativeManaged/
    L01_FakeNull/
      README.md                실습 README (lab-authoring.md 템플릿)
      FakeNullLab.cs
  02_GC/
    ...
Notes/
  progress.md                  학습 진행 기록: 퀴즈·약점·미응답 질문·Player Settings 변경 이력
  results/                     실습 결과 CSV, 플레이어·빌드·테스트 로그
Builds/                        빌드 산출물 (git 제외)
```

### 현재 상태 (2026-10-29)

- 폴더와 asmdef(`Labs`, `Labs.Editor`, `Labs.Tests`), `Notes/progress.md`는 만들어져 있다.
- 공통 인프라 `LabRunner`, `LabEval`, `LabMetrics`, `LabBuilder`, `LabMenu`는 구현되어 있다 (01-L01~L05에서 사용). `Labs.Tests`는 아직 테스트가 없다.
- 새 실습 추가 방법:
  1. `Assets/Labs/<주제>/L<번호>_<이름>/`에 `ILab`을 구현한 클래스를 만들고 `[Preserve, Lab("<ID>")]`를 붙인다. `Run`은 `async Awaitable`이고, 프레임을 넘길 때 `await Awaitable.NextFrameAsync()`를 쓴다. 결과는 `ctx.Record(condition, metric, value)`로 남긴다.
  2. 할당·시간 측정에는 `LabMetrics`를 쓴다. 프레임 카운터는 직전 프레임 값이므로 프레임 F에서 돌린 코드는 F+1에서 읽는다.
  3. `LabMenu`에 `[MenuItem("Labs/<주제>/L<번호> <이름>")]` 항목을 추가한다.
  4. 씬 로드 자체를 관찰할 때만 실습 폴더에 전용 씬을 둔다. 씬 파일은 손으로 편집하지 말고 열린 에디터에서 코드로 만든다. `LabBuilder`가 빌드할 때 자동으로 포함한다.
- `LabRunner`는 MonoBehaviour가 아닌 정적 클래스라 씬에 GameObject를 만들지 않는다.
- 빈 폴더는 git이 추적하지 않으므로 `.gitkeep`을 둔다. Unity는 `.`으로 시작하는 파일을 임포트하지 않는다. 폴더에 실제 파일이 생기면 `.gitkeep`은 지워도 된다.
- `Assets`에는 `Labs`, `Main.unity` 외에 URP 템플릿 파일(`Settings/`, `InputSystem_Actions.inputactions`)이 남아 있다. 템플릿의 `TutorialInfo/`, `Readme.asset`, `Scenes/SampleScene`은 2026-10-29에 삭제했다.
  - 템플릿이라 URP(`com.unity.render-pipelines.universal` 17.3.0), Input System 등이 들어 있다. GC 실습에서는 이들의 초기화 할당이 노이즈가 될 수 있으니 워밍업으로 배제하고, 렌더링 주제(5)에서는 이 URP 설정을 기반으로 쓴다.
  - `com.unity.pipeline` 0.9.1-exp.1은 실험적 패키지다. 의도적으로 넣은 것인지 확인 필요.

## 씬 운영

- **공용 씬은 `Assets/Main.unity` 하나다.** 모든 실습이 같은 씬에서 돌아야 프레임 시간·GC Used 같은 기준선이 실습마다 달라지지 않는다.
- Main 씬은 거의 비워 둔다. `LabRunner`도 씬에 배치하지 않고 `[RuntimeInitializeOnLoadMethod]`로 스스로 부트스트랩한다. 그래서 실습을 추가해도 씬 파일은 수정하지 않는다.
- 실습 간 격리는 실행 단위로 보장한다. 플레이어는 `-lab <ID>`로 실습 하나만 실행하고 종료하며, 에디터 메뉴도 실행마다 플레이 모드에 새로 진입한다.
- **예외: 씬 로드 자체가 실험 대상인 실습**(예: 01-L03 씬 로드 시 생성자 호출)은 실습 폴더 안에 전용 씬을 둔다(`L03_.../L03_Scene.unity`). 저장된 씬의 역직렬화가 필요하므로 `SceneManager.CreateScene`으로는 대체할 수 없다.
  - 전용 씬은 Build Settings에 수동으로 넣지 않는다. `LabBuilder`가 `BuildPlayerOptions.scenes`에 `Main`과 `Assets/Labs` 아래 씬을 넘기므로 Build Settings는 바뀌지 않는다. 에디터에서는 `EditorSceneManager.LoadScene(Async)InPlayMode`로 경로 로드한다.
- `Main.unity`를 옮기거나 이름을 바꿀 때는 Build Settings 경로가 함께 바뀌도록 Unity 안에서 한다.

## 이름 규칙

- 주제 폴더: `<주제번호 2자리>_<영문이름>` (예: `02_GC`)
- 실습 폴더: `L<번호>_<짧은이름>`, 실습 ID: `<주제번호>-L<번호>` (예: `02-L05`)
- 결과 파일: `Notes/results/<실습ID>_<백엔드>_<날짜>.csv`

## 파일 위치 원칙

- `Assets` 안의 파일은 Unity가 임포트·리프레시한다. 그래서 **측정이 돌아가는 동안 계속 쓰이는 파일**(CSV, 플레이어 로그, 빌드 로그)만 `Assets` 밖 `Notes/results`에 둔다.
- 실습 README는 실습 전후에만 수정하므로 코드 옆에 둔다. 단, **측정 실행 중에는 README를 포함해 `Assets` 안 파일을 수정하지 않는다.** 결과 반영은 실행이 끝난 뒤에 한다.
- `Notes/progress.md`는 전체 진행 현황과 약점·퀴즈 기록용이고, 실습별 상세 내용은 각 README에 둔다. progress.md에서는 README로 링크만 건다.
- git: `.gitignore`가 `*.log`와 `/Builds/`를 제외한다. 따라서 `Notes/results`에서 **커밋되는 것은 CSV뿐**이고, 로그는 로컬에만 남는다. 해석에 필요한 로그 내용은 실습 README에 발췌한다.

## 최상위 README

프로젝트 루트의 `README.md`는 **실습 전체 목차이자 결론 모음**이다. 처음 보는 사람(면접관 포함)이 이 파일만 읽어도 무엇을 실험했고 무엇을 확인했는지 알 수 있어야 한다.

- 구성: 프로젝트 소개 → 환경 표(Unity 버전, 플랫폼, 측정 기준) → 진행 현황 표(주제별 완료 수, 각 장 앵커 링크) → 실행 방법 요약 → 주제별 실습 표
- 실습 표 열: `ID | 실습 | 상태 | 백엔드 | 핵심 결론`
  - ID: 실습 README 상대 경로로 링크 (README가 생기기 전인 `대기` 실습은 링크 없음)
  - 실습: 짧은 이름. 자세한 제목은 실습 README에
  - 상태: `대기` / `진행 중` / `완료`. 괄호 설명을 붙이지 않는다 (백엔드 정보는 백엔드 열로)
  - 백엔드: 결론을 낸 플레이어 백엔드 (`Mono` / `IL2CPP`). 실행하지 않은 백엔드가 있으면 표 아래 한 줄로 적는다
  - 핵심 결론: 측정으로 확인한 사실을 **한 줄로**(대략 100자 이내), 대표 수치 1~2개만 포함 (예: "참조 필드가 없는 `struct[]`는 GC가 내용물을 훑지 않음. 10만 개 기준 Collect N배 차이"). 자세한 수치·조건·해석은 실습 README에 둔다. 표 칸이 길어지면 렌더링에서 여러 줄로 감겨 읽기 어렵다
- 표 안에 빈 줄을 넣지 않는다. 빈 줄 뒤의 행은 헤더 없는 새 표가 되어 글자로 렌더링된다 (실습 README의 결과 표도 같다)
- **실습 표의 원본은 이 README다.** 실습을 추가·완료할 때 여기부터 갱신한다. [curriculum.md](curriculum.md)의 표는 계획용이며 상태·결론을 적지 않는다.
- 실습이 끝나 실습 README의 해석을 채운 직후, 최상위 README의 상태와 핵심 결론도 같이 갱신한다.
- 학습자의 오답·약점 같은 개인 기록은 넣지 않는다 (그건 `Notes/progress.md`).
- 핵심 결론은 결과가 나오기 전에는 비워둔다. 예상이나 이론을 결론처럼 적지 않는다.
