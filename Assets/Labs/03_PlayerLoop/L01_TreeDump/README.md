# 03-L01 PlayerLoop 트리 덤프

## 목표
`PlayerLoop`가 실제로 어떤 단계·하위 시스템으로 이루어져 있는지 확인하고, 에디터 플레이 모드와 플레이어(Mono·IL2CPP)에서 트리가 같은지 비교한다.

## 예측
- 질문 1: 최상위 단계는 몇 개이고 `Update` 단계는 몇 번째인가?
- 학습자 답 (실행 전, 원문 그대로): "1은 모르지..걍 이런건 진짜 추측의영역인데 이런걸 왜물어보는건지 모르겠음."
  - 출제 문제로 철회: 원리에서 추론할 수 없는 암기형 질문이었다. 결과를 본 뒤 "왜 이 순서인가"를 묻는 질문으로 바꾼다.
- 질문 2: 에디터 플레이 모드와 플레이어의 시스템 총수는 같은가, 다르면 어느 쪽이 많은가?
- 학습자 답 (실행 전, 원문 그대로): "2는 에디터가 많겠지? EditorLoop가 존재하니까"

## 실습 구성
- 코드: `PlayerLoopDumpLab.cs` — 10프레임 뒤 `GetDefaultPlayerLoop()`·`GetCurrentPlayerLoop()`를 노드마다 한 행(경로, native/managed/group)으로 덤프
- 실행: 에디터 메뉴 `Labs/03/L01 Tree Dump` 또는 플레이어 `-lab 03-L01`

## 결과
| 실행일 | 백엔드 | 빌드 | 트리 | 최상위 단계 | native | managed | group |
|---|---|---|---|---|---|---|---|
| 2026-11-03 | Editor (플레이 모드) | - | default | 8 | 131 | 0 | 8 |
| 2026-11-03 | Editor (플레이 모드) | - | current | 8 | 131 | **1** | 8 |
| 2026-11-03 | Mono | Development | default | 8 | 131 | 0 | 8 |
| 2026-11-03 | Mono | Development | current | 8 | 131 | 0 | 8 |
| 2026-11-03 | IL2CPP | Development | default | 8 | 131 | 0 | 8 |
| 2026-11-03 | IL2CPP | Development | current | 8 | 131 | 0 | 8 |

- 세 실행의 default 트리는 노드 이름·순서까지 **완전히 같다**. Mono와 IL2CPP는 current도 같다.
- 에디터 current만 `Initialization` 맨 앞에 managed 시스템 1개가 더 있다: `UnityEngine.InputSystem.LowLevel.NativeInputRuntime+InputSystemPlayerLoopRunnerInitializationSystem` (Input System 1.20.0).

최상위 단계와 하위 시스템 수 (default, 세 실행 공통):

| 순번 | 단계 | 하위 시스템 | 대표 하위 시스템 |
|---|---|---|---|
| 0 | `TimeUpdate` | 1 | `WaitForLastPresentationAndUpdateTime` |
| 1 | `Initialization` | 7 | `ProfilerStartFrame`, `SynchronizeInputs` |
| 2 | `EarlyUpdate` | 34 | `UpdateInputManager`, `ScriptRunDelayedStartupFrame`, `UpdatePreloading` |
| 3 | `FixedUpdate` | 13 | `ScriptRunBehaviourFixedUpdate` → `PhysicsFixedUpdate` → `ScriptRunDelayedFixedFrameRate` |
| 4 | `PreUpdate` | 11 | `PhysicsUpdate`, `NewInputUpdate`, `SendMouseEvents` |
| 5 | `Update` | 4 | `ScriptRunBehaviourUpdate`, `ScriptRunDelayedDynamicFrameRate`, `ScriptRunDelayedTasks`, `DirectorUpdate` |
| 6 | `PreLateUpdate` | 13 | `DirectorUpdateAnimationBegin/End`, `ScriptRunBehaviourLateUpdate`(마지막) |
| 7 | `PostLateUpdate` | 48 | `UpdateAllRenderers`, `FinishFrameRendering`, `TriggerEndOfFrameCallbacks`, `PresentAfterDraw` |

원본: `Notes/results/03-L01_Editor_2026-11-03.csv`, `03-L01_Mono_2026-11-03.csv`, `03-L01_IL2CPP_2026-11-03.csv` (노드마다 한 행)

## 해석
- **예측과 비교 (질문 2)**: "에디터가 많다"는 **current 트리에서만, 1개** 맞았다. 이유는 예측과 다르다.
  - 에디터 고유의 일(인스펙터 갱신, `EditorApplication.update` 등)은 이 트리에 없다. 에디터 프로파일러에서 `EditorLoop`가 `PlayerLoop`와 **나란히** 따로 보이는 것처럼, 에디터 루프는 PlayerLoop 트리 바깥에 있다. 그래서 엔진이 정한 default 트리는 에디터와 플레이어가 노드 하나까지 같다.
  - 차이를 만든 것은 **패키지 코드의 `#if UNITY_EDITOR`**다. Input System의 `NativeInputRuntime.onPlayerLoopInitialization`이 에디터에서만 `GetCurrentPlayerLoop` → `Initialization` 맨 앞에 삽입 → `SetPlayerLoop`를 한다 (주석: 버그 1368559 등의 hot-fix). 플레이어 DLL에는 이 코드가 아예 없다. 01-L06과 같은 구조로, 에디터와 플레이어는 **다른 DLL, 다른 코드**다.
- **질문 1 (철회)**: 단계 순서는 추론할 수 있는 것이 아니라 결과로 보여 줄 사실이었다. 대신 "왜 이 순서인가"를 묻는다.
- **한 단계 더**
  - default는 엔진이 정한 **고정 목록**이고, current는 패키지·사용자 코드가 `SetPlayerLoop`로 고친 결과다. 툴·라이브러리를 만들 때는 반드시 current를 고쳐야 다른 패키지의 삽입을 지우지 않는다 (default에서 시작하면 Input System 같은 삽입이 사라진다).
  - Windows 빌드에도 `UpdateKinect`, `DeliverIosPlatformEvents`, `ARCoreUpdate`, `ExecuteGameCenterCallbacks`가 있다. 트리는 플랫폼·모듈과 관계없이 같고, 해당 기능이 없으면 시스템이 할 일 없이 지나가는 구조로 보인다 (하위 시스템별 비용은 확인 필요 — Profiler에서 마커로 볼 수 있음).
  - 이름만으로도 다음 실습의 지도가 보인다: `ScriptRunBehaviour*`는 콜백, `ScriptRunDelayed*`는 지연 실행(코루틴·Task 계열로 추정), `TriggerEndOfFrameCallbacks`는 프레임 끝 콜백으로 추정. **어느 것이 무엇을 재개하는지는 03-L02 마커 실측으로 확정한다** (이름만으로 단정하지 않음).
  - (2026-11-03 추가, 에디터) default 트리에서 `loopConditionFunction`이 설정된 노드는 **`FixedUpdate` 단계 하나**다. 트리는 위에서부터 한 번씩 순차 실행되지만, 이 노드만 조건 함수가 false를 돌려줄 때까지 하위 시스템 13개를 **한 프레임 안에서 반복**한다(0회일 수도 있음). `FixedUpdate`와 `Update`의 주기가 다른 이유이고, 반복 횟수는 03-L04에서 잰다. 확인: `unity command script eval`로 트리를 돌며 `loopConditionFunction != IntPtr.Zero`인 노드 출력
- **근거**: [PlayerLoop 스크립팅 API](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/LowLevel.PlayerLoop.html), `Library/PackageCache/com.unity.inputsystem@*/InputSystem/Runtime/NativeInputRuntime.cs` (`#if UNITY_EDITOR` 블록), `PlayerLoopSystem` 필드는 리플렉션으로 확인 (`type`, `subSystemList`, `updateDelegate`, `updateFunction`, `loopConditionFunction`)
