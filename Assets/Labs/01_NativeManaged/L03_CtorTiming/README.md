# 01-L03 MonoBehaviour 생성자 호출 시점·스레드

## 목표
MonoBehaviour의 C# 생성자(와 필드 초기화자)를 Unity가 언제, 몇 번, 어느 스레드에서 호출하는지 생성 경로별로 측정한다. "생성자에서 Unity API를 쓰지 말라"는 규칙의 근거를 실측으로 확인한다.

## 예측
- 질문: `ProbeBehaviour`의 생성자가 호출될 때마다 스레드를 기록한다. 아래 4가지 경로에서 객체 **1개**를 만들 때, 생성자는 **몇 번**, **어느 스레드(메인 / 다른 스레드)**에서 호출될까? `Awake`와 비교한 순서도 함께.

  | # | 경로 | 생성자 호출 횟수 | 스레드 |
  |---|---|---|---|
  | 1 | `go.AddComponent<ProbeBehaviour>()` | | |
  | 2 | `Instantiate(probeGo)` (이미 있는 객체 복제) | | |
  | 3 | `SceneManager.LoadScene(전용 씬, Additive)` (씬에 Probe 1개 저장됨) | | |
  | 4 | `SceneManager.LoadSceneAsync(전용 씬, Additive)` | | |

- 학습자 답 (실행 전, 2026-10-29, 에디터·플레이어 차이는 언급 없음):

  | # | 생성자 호출 횟수 | `Awake` 대비 | 스레드 |
  |---|---|---|---|
  | 1 | 1 | `Awake` 이전 | 메인 |
  | 2 | 1 | `Awake` 이후 | 메인 |
  | 3 | 1 | `Awake` 이후 | 메인 |
  | 4 | 1 | `Awake` 이후 | 다른 스레드 |

## 실습 구성
- 코드:
  - `CtorProbe.cs` — 생성자와 `Awake`에서 (단계, 종류, 스레드 ID)를 `ConcurrentQueue`에 넣는다. 생성자에서는 Unity API를 쓰지 않는다.
  - `CtorTimingLab.cs` — 4가지 경로를 차례로 실행하고, 단계마다 큐를 비워 순서·횟수·스레드를 기록한다. 메인 스레드는 실습 시작 시점의 스레드 ID로 판정한다.
  - `L03_Scene.unity` — `CtorProbe` 1개가 저장된 전용 씬. 에디터에서는 `EditorSceneManager.LoadScene(Async)InPlayMode`로 경로 로드하고, 플레이어에서는 `LabBuilder`가 Main 뒤에 포함시켜 `SceneManager.LoadScene(Async)`로 로드한다.
- 실행: 에디터 메뉴 `Labs/01/L03 Ctor Timing` 또는 플레이어 `-lab 01-L03`

## 결과

| 실행일 | 백엔드 | 빌드 | # | 경로 | 생성자 횟수 | 순서 (스레드) |
|---|---|---|---|---|---|---|
| 2026-10-29 | Editor (Mono) | 에디터 플레이 모드 | 1 | `AddComponent` | 1 | ctor(main) → Awake(main) |
| | | | 2 | `Instantiate` | 1 | ctor(main) → Awake(main) |
| | | | 3 | `LoadScene` (동기) | 1 | **ctor(other, id=881) → Awake(main)** |
| | | | 4 | `LoadSceneAsync` | 1 | ctor(other, id=881) → Awake(main) |
| 2026-10-29 | Mono | Development 플레이어 | 1~4 | | 모두 1 | 에디터와 같은 패턴. 3·4의 생성자는 둘 다 같은 다른 스레드(id=6) |

원본: `Notes/results/01-L03_Editor_2026-10-29.csv`, `Notes/results/01-L03_Mono_2026-10-29.csv`

## 해석
- 예측과 비교: 횟수 4/4 맞음. 스레드는 3번(동기 `LoadScene`)을 메인으로 예측했지만 **다른 스레드**였다. 순서는 2~4를 "`Awake` 이후"로 예측했지만 모든 경로에서 생성자가 먼저였다.
  - 순서: `Awake`는 인스턴스 메서드라서 객체가 만들어진 뒤에만 불릴 수 있다. 따라서 생성자는 어떤 경로든 항상 `Awake`보다 먼저다. 2~4에서 실제로 생성자와 `Awake` 사이에 끼는 것은 **역직렬화(deserialization)**다. 생성자와 필드 초기화자가 먼저 기본값을 쓰고, 그 위에 저장된(또는 복제 원본의) 필드 값이 덮어써진 다음 `Awake`가 불린다.
  - 3번 스레드: "동기"는 호출한 쪽이 기다린다는 뜻이지, 작업이 메인 스레드에서 실행된다는 뜻이 아니다. 동기 `LoadScene`도 비동기 로드와 **같은 스레드 ID**에서 생성자가 불렸다. 씬 로드의 역직렬화는 로드 방식과 관계없이 로딩 전용 스레드에서 일어난다.
- `AddComponent`와 `Instantiate`는 메인 스레드에서 객체를 만들고 바로 (복제라면 값 복사 후) `Awake`까지 부른다. 반면 씬 로드는 로딩 스레드에서 객체를 만들고 역직렬화한 뒤, 메인 스레드로 넘어와 `Awake`를 부른다. 그래서 같은 생성자 코드가 경로에 따라 다른 스레드에서 실행된다.
- 한 단계 더:
  - **"생성자·필드 초기화자에서 Unity API 금지"의 실제 근거가 이것이다.** 대부분의 Unity API는 메인 스레드에서만 호출할 수 있다. 그런데 씬이나 프리팹 에셋을 로드할 때는 생성자가 로딩 스레드에서 돈다. 에디터에서 `AddComponent`로만 테스트하면 메인 스레드라 문제가 안 보이다가, 씬에 배치된 객체에서만 터진다. 이때 Unity가 내는 오류("… is not allowed to be called from a MonoBehaviour constructor …")는 이번에 측정하지 않았다(확장 실험 후보).
  - 같은 이유로 생성자에서 정적 컬렉션에 `this`를 등록하는 패턴은 스레드 안전하지 않다. 이 실습의 `CtorProbe`도 그래서 `ConcurrentQueue`를 쓴다.
  - 확장 후보: `Object.InstantiateAsync`(Unity 2022.3.20+ / 6)에서 생성자가 어느 스레드에서 불리는지, 에디터의 도메인 리로드·플레이 모드 진입 때 생성자가 몇 번 불리는지.
- 남은 확인: IL2CPP 플레이어. 로딩 스레드의 내부 이름(이번 측정에서 관리 스레드 이름은 `null`)은 확인 필요.
- 근거: 실측 CSV. Unity 매뉴얼 [Script serialization](https://docs.unity3d.com/6000.3/Documentation/Manual/script-serialization.html)이 생성자·필드 초기화자에서 Unity API를 호출하지 말라고 안내한다(로딩 스레드와의 관계를 설명하는 정확한 문구는 확인 필요).
