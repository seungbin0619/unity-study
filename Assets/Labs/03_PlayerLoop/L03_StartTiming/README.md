# 03-L03 `Instantiate` 시점별 `Start` 호출 시점

## 목표
`Instantiate`를 어디서 부르느냐(`Awake`·`Start`·`FixedUpdate`·`Update`·`LateUpdate`·코루틴·Task continuation)에 따라 새 객체의 `Awake`/`OnEnable`/`Start`와 첫 `FixedUpdate`/`Update`/`LateUpdate`가 언제, PlayerLoop의 어디서 불리는지 확인한다.

## 예측
- 질문: `Update`에서 `Instantiate`한 객체의 `Start`는 언제 불릴까? (a) `Instantiate` 안에서 즉시 (b) 같은 프레임 안 나중에 (c) 다음 프레임. 그리고 그 객체의 `LateUpdate`는 같은 프레임에 불릴까?
  - 추론 재료: "`Start`는 그 객체의 첫 `Update` 직전에 한 번" 규칙, 03-L01 트리의 `EarlyUpdate/ScriptRunDelayedStartupFrame`, 03-L02에서 본 "시작은 호출자 안에서 동기, 이후는 PlayerLoop가 재개"
- 학습자 답 (실행 전, 원문 그대로): "유니티가 제공하는 라이프사이클에 의하면 Start -> Update니까 Update에서 Instantiate하더라도 다음 프레임에 실행될 것 같긴하네"
  - `Start`는 (c) 다음 프레임. 추가 답 (실행 전, 원문 그대로): "아 추가로 LateUpdate는 못불릴거같음 Start가 아직 호출되지 않았어서"
  - 추가 답은 에디터 실행이 끝난 뒤, 결과를 보기 전에 받음 (예측으로 유효)

## 실습 구성
- 코드
  - `LifecycleProbe.cs` — 생성된 객체. 각 콜백이 불린 프레임과 위치(03-L02 `LoopMarkers`의 하위 시스템 이름, `Instantiate` 호출 중이면 "inside Instantiate")를 기록
  - `Spawner.cs` — 생성 지점 8곳(`Awake`·`Start`·첫 `FixedUpdate`·첫 `Update`·첫 `LateUpdate`·`yield return null` 뒤·`WaitForEndOfFrame` 뒤·`await Task.Yield()` 뒤)에서 활성 템플릿을 한 번씩 `Instantiate`
  - `StartTimingLab.cs` — 마커 설치 → 워밍업 10프레임 → `Spawner` 생성 → 60프레임 대기 → 생성 프레임 기준 Δ와 위치 기록
- 실행: 에디터 메뉴 `Labs/03/L03 Start Timing` 또는 플레이어 `-lab 03-L03` (`FixedUpdate`가 돌도록 창 모드로 실행 — 03-L02 배치모드 참고)

## 결과
Editor·Mono·IL2CPP(창 모드) 세 실행에서 **위치가 모두 같았다**. 첫 `FixedUpdate`의 Δ만 실행마다 달랐다(프레임 길이에 따라 1\~7). 위치 이름의 `ScriptRun` 접두어는 생략.

| 생성 지점 (생성한 하위 시스템) | `Awake`·`OnEnable` | `Start` | 첫 `LateUpdate` | 첫 `Update` |
|---|---|---|---|---|
| `Spawner.FixedUpdate` (`FixedUpdate/BehaviourFixedUpdate`) | Instantiate 안 | +0 `FixedUpdate/DelayedFixedFrameRate` | +0 | **+0** |
| `Spawner.Update` (`Update/BehaviourUpdate`) | Instantiate 안 | **+0 `Update/DelayedDynamicFrameRate`** | **+0** | +1 |
| 코루틴 `yield return null` 뒤 (`Update/DelayedDynamicFrameRate`) | Instantiate 안 | +0 `PreLateUpdate/BehaviourLateUpdate` | +0 | +1 |
| `Spawner.Awake` (실습 `Awaitable` 재개 중 = `Update/DelayedDynamicFrameRate`) | Instantiate 안 | +0 `PreLateUpdate/BehaviourLateUpdate` | +0 | +1 |
| `await Task.Yield()` 뒤 (`Update/DelayedTasks`) | Instantiate 안 | +0 `PreLateUpdate/BehaviourLateUpdate` | +0 | +1 |
| `Spawner.Start` (`PreLateUpdate/BehaviourLateUpdate`) | Instantiate 안 | +0 `PostLateUpdate/DelayedDynamicFrameRate` | +1 | +1 |
| `Spawner.LateUpdate` (`PreLateUpdate/BehaviourLateUpdate`) | Instantiate 안 | +0 `PostLateUpdate/DelayedDynamicFrameRate` | +1 | +1 |
| 코루틴 `WaitForEndOfFrame` 뒤 (`PostLateUpdate/PlayerSendFrameComplete`) | Instantiate 안 | **+1 `EarlyUpdate/DelayedStartupFrame`** | +1 | +1 |

60프레임 실제 시간: Editor 336 ms, Mono 129 ms, IL2CPP 92 ms.

원본: `Notes/results/03-L03_Editor_2026-11-04.csv`, `03-L03_Mono_2026-11-04.csv`, `03-L03_IL2CPP_2026-11-04.csv`

## 해석
- **예측과 비교**
  - `Start` (c) 다음 프레임 → **틀림**. `Update`에서 만든 객체의 `Start`는 **같은 프레임**, `Update` 콜백들이 끝난 직후(`Update/ScriptRunDelayedDynamicFrameRate`)에 불렸다. 다음 프레임으로 넘어간 것은 `WaitForEndOfFrame` 뒤에 만든 경우뿐이다.
  - `LateUpdate`는 못 불린다 → **틀림**. 같은 프레임에 불렸고, `Start`가 그보다 먼저 불렸다.
  - 어긋난 곳: "`Start` → `Update` 순서" 규칙 자체는 맞게 썼다. 그런데 이 순서를 지키는 방법을 "객체를 다음 프레임까지 기다리게 한다"로 봤다. Unity는 반대로 **`Start`를 앞당겨** 다음 스크립트 실행 지점에서 바로 부른다. 순서 제약은 "그 객체의 **어떤 콜백보다도** 먼저 `Start`"이지, "다음 프레임 `Update` 직전"이 아니다.
- **관찰된 규칙**: `Awake`·`OnEnable`은 항상 `Instantiate` 안에서 동기로 불린다. `Start`는 미뤄 두었다가 **생성 뒤 처음 오는 스크립트 실행 지점**에서 몰아서 부른다. 이번 실측에서 그런 지점은 아래와 같다.
  - `FixedUpdate/ScriptRunDelayedFixedFrameRate`
  - `Update/ScriptRunDelayedDynamicFrameRate`
  - `PreLateUpdate/ScriptRunBehaviourLateUpdate`
  - `PostLateUpdate/ScriptRunDelayedDynamicFrameRate`
  - 다음 프레임의 `EarlyUpdate/ScriptRunDelayedStartupFrame`
  - 각 지점은 **자기 일을 하기 전에** 쌓인 `Start`를 먼저 처리하는 것으로 보인다. 그 지점 안에서 만든 객체(예: 코루틴 재개 중)는 다음 지점까지 기다린다.
  - `Update/ScriptRunDelayedTasks`는 처리 지점이 아니었다 (`Task.Yield` 뒤에 만든 객체는 `LateUpdate`까지 기다림).
  - `ScriptRunBehaviourUpdate`·`ScriptRunBehaviourFixedUpdate`가 처리 지점인지는 이 배치로는 알 수 없다 (확인 필요 — 그 직전에 객체를 만드는 생성 지점이 없었다).
- **03-L02에서 남긴 질문 하나가 풀렸다**: `PostLateUpdate/ScriptRunDelayedDynamicFrameRate`는 `LateUpdate`·`Start` 중에 만든 객체의 `Start`를 그 프레임 안에 처리한다. `EarlyUpdate/ScriptRunDelayedStartupFrame`은 그 뒤(프레임 끝)에 만든 객체를 다음 프레임 맨 앞에서 받아 준다.
- **한 단계 더**
  - `FixedUpdate`에서 만든 객체는 `Start`와 첫 `Update`·`LateUpdate`는 같은 프레임에 받지만, 첫 `FixedUpdate`는 **다음 물리 스텝**까지 기다린다 (Δ 1\~7). 같은 프레임에 물리 스텝이 더 남아 있어도 이번 실측에서는 그 프레임 안에서 받지 못했다. 프레임이 길어 스텝이 여러 번 도는 경우는 03-L04에서 확인 필요.
  - 실무 함의: `Instantiate` 직후 같은 프레임의 `LateUpdate`에서 새 객체를 쓰는 코드는 **`Start`에서 초기화한 값이 이미 들어 있다고 가정해도 된다**. 또 `Instantiate` → `Init(...)` 패턴에서 `Init`은 항상 `Start`보다 먼저다 (`Start`는 `Instantiate` 안에서 불리지 않음). 하지만 `Awake`·`OnEnable`보다는 늦으므로, 넣어 준 값에 의존하는 초기화는 `Awake`가 아니라 `Start`에 둬야 한다.
- **근거**: 이 프로젝트의 실측 (Editor·Mono·IL2CPP 6000.3.25f1), 03-L01 트리, 03-L02 마커. [Order of execution](https://docs.unity3d.com/6000.3/Documentation/Manual/execution-order.html)은 "`Start`는 첫 `Update` 전"이라고만 적고 있다
