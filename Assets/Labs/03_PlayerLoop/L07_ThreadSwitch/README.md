# 03-L07 스레드 전환과 continuation 위치

## 목표
`await` 다음 줄이 어느 스레드에서, 몇 프레임 뒤, PlayerLoop의 어느 위치에서 실행되는지 잰다. `SynchronizationContext` 캡처 규칙, `ConfigureAwait(false)`, `Awaitable.BackgroundThreadAsync`/`MainThreadAsync`를 비교한다.

## 예측
- 질문: 메인 스레드에서 시작한 async 메서드가 차례로 실행할 때, 각 줄 **다음 줄**은 메인/워커 중 어디서?
  ```csharp
  await Task.Run(Work);                         // A
  await Task.Run(Work).ConfigureAwait(false);   // B
  await Task.Delay(10);                         // C
  await Awaitable.MainThreadAsync();            // D
  await Task.Delay(10);                         // E
  ```
- 학습자 답 (실행 전, 원문 그대로): "A 메인, B 워커, C 워커, D 메인, E 메인"

## 실습 구성
- 코드: `ThreadSwitchLab.cs` — 위 A\~E 순서 + 추가 `F await Awaitable.BackgroundThreadAsync()` → `G await Awaitable.MainThreadAsync()`를 5회 반복. 각 `await` 직후 관리 스레드 ID, 메인 여부, `SynchronizationContext.Current` 타입, (메인일 때만) `Time.frameCount`와 PlayerLoop 위치(03-L02 `LoopMarkers`), 시작부터의 경과 ms를 기록. `Work()`는 Unity API 없이 3 ms 바쁜 대기
- 실행: 에디터 메뉴 `Labs/03/L07 Thread Switch` 또는 플레이어 `-lab 03-L07`

## 결과
2026-11-12, 배치모드 Development 플레이어, 5라운드. 다섯 라운드 모두 스레드 종류·위치가 같았다 (아래는 3라운드 이후 대표값). 에디터는 실행하지 않음.

| 단계 | 다음 줄의 스레드 (Mono / IL2CPP) | `SynchronizationContext.Current` | 재개 위치 (메인일 때) | 라운드 시작부터 ms |
|---|---|---|---|---|
| 0 시작 | 메인 | `UnitySynchronizationContext` | `Update/ScriptRunDelayedDynamicFrameRate` | 0 |
| **A** `await Task.Run` | **메인** | `UnitySynchronizationContext` | **`Update/ScriptRunDelayedTasks`** | 3.1 |
| **B** `Task.Run(...).ConfigureAwait(false)` | **워커** (풀, id 8·9 / 22·23) | `null` | – | 6.1 |
| **C** `Task.Delay(10)` (B의 워커에서) | **워커** (B와 다른 풀 스레드일 때가 많음) | `null` | – | 16.2 |
| **D** `Awaitable.MainThreadAsync()` | **메인** | `UnitySynchronizationContext` | `Update/ScriptRunDelayedTasks` | 16.3 |
| **E** `Task.Delay(10)` (메인에서) | **메인** | `UnitySynchronizationContext` | `Update/ScriptRunDelayedTasks` | 26.4 |
| F `Awaitable.BackgroundThreadAsync()` | 워커 (풀) | `null` | – | 26.4 |
| G `Awaitable.MainThreadAsync()` | 메인 | `UnitySynchronizationContext` | `Update/ScriptRunDelayedTasks`, **F 다음 프레임** | 26.6 |

- 배치모드라 프레임이 수십 µs로 짧아 "몇 프레임 뒤"는 경과 시간 ÷ 프레임 시간에 불과하다 (A가 24\~60프레임 뒤). 의미 있는 것은 **재개 위치**와 **E → G가 한 프레임**이라는 점.
- 1라운드 A만 Mono 20 ms (첫 `Task.Run`의 스레드 풀 기동 비용으로 보임, 2라운드부터 3 ms = `Work` 시간).

원본: `Notes/results/03-L07_Mono_2026-11-12.csv`, `03-L07_IL2CPP_2026-11-12.csv`

## 해석
- **예측과 비교: 5/5 맞음.** 규칙 하나로 모두 설명된다. **`await` 순간 그 스레드의 `SynchronizationContext.Current`가 있으면 그곳으로 돌아오고, 없으면 작업을 끝낸 스레드에서 계속한다.**
  - A: 메인 → 컨텍스트 캡처 → 메인의 `ScriptRunDelayedTasks`에서 재개.
  - B: 캡처하지 않음 → `Task.Run`의 워커가 그대로 이어서 실행.
  - C: 워커의 `Current`는 `null` → `Task.Delay` 타이머를 완료시킨 풀 스레드에서 계속. B와 스레드 ID가 바뀌기도 한다. **한 번 메인을 떠나면 `await`만으로는 돌아오지 않는다.**
  - D: `MainThreadAsync`가 continuation을 메인의 같은 큐(`ScriptRunDelayedTasks`)에 넣는다. → E는 메인에서 다시 캡처하므로 메인.
- **메인으로 돌아오는 길은 하나다.** `Task` continuation(A·E)과 `Awaitable.MainThreadAsync`(D·G) 모두 `Update/ScriptRunDelayedTasks`에서 재개됐다. 03-L02의 `Task.Yield` 재개 위치와 같다. 따라서 워커 → 메인 전환은 즉시 일어나지 않고, **메인 스레드가 다음에 그 하위 시스템을 지날 때까지** 기다린다. G가 F 다음 프레임이었던 것이 그 예다. 60fps 게임이라면 최대 약 16 ms다.
- **한 단계 더**
  - **`ConfigureAwait(false)`의 함정.** 라이브러리 코드에서는 권장 관례지만, Unity 게임 코드에서 쓰면 그 뒤의 `transform`·`Time` 같은 메인 전용 API가 워커에서 불려 `UnityException`이 난다. 이 실습도 그래서 워커에서는 `Time.frameCount`를 읽지 않았다. 반대로 `Awaitable.BackgroundThreadAsync()` → 무거운 계산 → `MainThreadAsync()`는 의도를 명시하는 Unity식 패턴이다.
  - **왕복 비용은 프레임 경계다.** 원소마다 백그라운드 ↔ 메인을 오가면 원소마다 한 프레임씩 늦어진다. 계산을 백그라운드에서 한 번에 끝내고 결과만 한 번 넘기는 식으로 묶어야 한다.
  - **"`await` 다음 줄 = 원래 스레드"는 .NET 일반 규칙이 아니다.** 컨텍스트가 있을 때만 그렇다. 콘솔 앱이나 워커처럼 컨텍스트가 없으면 아무 풀 스레드에서 계속된다. Unity 메인 스레드가 "돌아오는" 이유는 엔진이 `UnitySynchronizationContext`를 설치해 두었기 때문이다.
- **근거**: 이 프로젝트의 실측. [Awaitable 스레드 전환](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Awaitable.BackgroundThreadAsync.html)