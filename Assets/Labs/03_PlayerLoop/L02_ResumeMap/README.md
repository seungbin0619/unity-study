# 03-L02 콜백·재개 위치 지도

## 목표
코루틴·`Awaitable`·`Task`의 대기 뒤 코드(continuation)가 PlayerLoop의 **어느 하위 시스템 안에서**, 몇 프레임 뒤에, 어느 스레드에서 실행되는지 실측으로 지도를 만든다.

## 예측
- 질문: `Update`에서 아래 프로브를 동시에 시작할 때, 각 대기 뒤의 코드는 어느 하위 시스템 안에서 실행될까? (후보는 03-L01 덤프의 이름)
  - 프로브: A `yield return null` / B `yield return new WaitForSeconds(0.01f)` / C `WaitForFixedUpdate` / D `WaitForEndOfFrame` / E `Awaitable.NextFrameAsync()` / F `Awaitable.FixedUpdateAsync()` / G `Awaitable.EndOfFrameAsync()` / H `await Task.Yield()`
  - 후보: ① `Update/ScriptRunBehaviourUpdate` ② `Update/ScriptRunDelayedDynamicFrameRate` ③ `Update/ScriptRunDelayedTasks` ④ `FixedUpdate/ScriptRunBehaviourFixedUpdate` ⑤ `FixedUpdate/ScriptRunDelayedFixedFrameRate` ⑥ `PreLateUpdate/ScriptRunBehaviourLateUpdate` ⑦ `PostLateUpdate/ScriptRunDelayedDynamicFrameRate` ⑧ `PostLateUpdate/TriggerEndOfFrameCallbacks`
- 학습자 답 (실행 전, 원문 그대로): "모르겠어 그냥 알려줘 각 기능이 뭔지"
  - 예측 없이 실행 (학습자 요청). 각 하위 시스템의 역할은 결과로 설명함

## 실습 구성
- 코드
  - `LoopMarkers.cs` — 현재 PlayerLoop의 모든 하위 시스템 앞(과 단계 끝)에 "지금 여기" 이름만 적는 managed 마커 시스템을 끼운다. 코드가 실행될 때 마지막 마커 이름 = 그 코드를 부른 하위 시스템
  - `ResumeProbe.cs` — `Update`에서 프로브 A\~I를 한꺼번에 시작하고, 재개 순간의 `Time.frameCount`·마커·스레드를 기록. 콜백 `Update`/`LateUpdate`/`FixedUpdate`도 기록해 마커가 맞는지 대조
  - `ResumeMapLab.cs` — 워밍업 10프레임 뒤 3회 시행(시행마다 60프레임 대기), 끝나면 원래 PlayerLoop 복원
- 실행: 에디터 메뉴 `Labs/03/L02 Resume Map` 또는 플레이어 `-lab 03-L02`

## 결과
시행 3회 모두 위치가 같았다. Δ프레임은 시작 프레임(`Update`에서 시작) 기준.

| 프로브 | 재개한 하위 시스템 (Editor) | 재개한 하위 시스템 (Mono·IL2CPP 창 모드) | Δ프레임 | 스레드 |
|---|---|---|---|---|
| (대조) `Update` 콜백 | `Update/ScriptRunBehaviourUpdate` | 같음 | 0 | main |
| (대조) `LateUpdate` 콜백 | `PreLateUpdate/ScriptRunBehaviourLateUpdate` | 같음 | 0 | main |
| (대조) `FixedUpdate` 콜백 | `FixedUpdate/ScriptRunBehaviourFixedUpdate` | 같음 | 1 이상 | main |
| A 코루틴 첫 구간 | `Update/ScriptRunBehaviourUpdate` | 같음 | 0 | main |
| E `Awaitable` 첫 구간 | `Update/ScriptRunBehaviourUpdate` | 같음 | 0 | main |
| A `yield return null` | `Update/ScriptRunDelayedDynamicFrameRate` | 같음 | 1 | main |
| E `Awaitable.NextFrameAsync` | `Update/ScriptRunDelayedDynamicFrameRate` | 같음 | 1 | main |
| B `WaitForSeconds(0.01)` | `Update/ScriptRunDelayedDynamicFrameRate` | 같음 | 1\~7 | main |
| I `Awaitable.WaitForSecondsAsync(0.01)` | `Update/ScriptRunDelayedDynamicFrameRate` | 같음 | B와 같은 프레임 | main |
| C `WaitForFixedUpdate` | `FixedUpdate/ScriptRunDelayedFixedFrameRate` | 같음 | 1\~9 (`FixedUpdate` 콜백과 같은 프레임) | main |
| F `Awaitable.FixedUpdateAsync` | `FixedUpdate/ScriptRunDelayedFixedFrameRate` | 같음 | C와 같은 프레임 | main |
| D `WaitForEndOfFrame` | `PostLateUpdate/(end)` (모든 하위 시스템 뒤) | **`PostLateUpdate/PlayerSendFrameComplete`** | **0** | main |
| G `Awaitable.EndOfFrameAsync` | D와 같음 | D와 같음 | 0 | main |
| H `await Task.Yield()` | `Update/ScriptRunDelayedTasks` | 같음 | **0** | main |

배치모드(`-batchmode -nographics`) Mono 플레이어:

| 시행 | 60프레임 실제 시간 | `FixedUpdate` 콜백·C·F | B·I (0.01 s) |
|---|---|---|---|
| 1 | 16.33 ms | 재개 안 됨 | +1 |
| 2 | 3.47 ms | +60 | 재개 안 됨 |
| 3 | 3.38 ms | 재개 안 됨 | 재개 안 됨 |

창 모드 Mono는 60프레임에 96\~135 ms였다. 나머지 프로브(A·D·E·G·H)의 위치·Δ는 배치모드도 창 모드와 같다.

원본: `Notes/results/03-L02_Editor_2026-11-03.csv`, `03-L02_Mono_2026-11-03.csv`(창 모드 2회·배치모드 2회, 프레임 시간 기록은 19:29 실행부터), `03-L02_IL2CPP_2026-11-03.csv`

## 해석
- **예측**: 학습자 요청으로 생략.
- **하위 시스템별 역할 (이 실측 기준)**
  - `ScriptRunBehaviour*` = **콜백**(`Update`/`LateUpdate`/`FixedUpdate`)을 부른다. 코루틴·`Awaitable`의 **첫 구간**도 여기서 실행된다 — 시작은 호출자 안에서 동기로 일어나기 때문 (3장 개념 질문의 실측 확인).
  - `ScriptRunDelayedDynamicFrameRate` (`Update` 단계) = **프레임마다 확인하는 대기**를 푼다. `yield return null`, `NextFrameAsync`, `WaitForSeconds`류(시간이 됐는지를 프레임마다 확인).
  - `ScriptRunDelayedFixedFrameRate` (`FixedUpdate` 단계) = **물리 스텝마다 확인하는 대기**를 푼다. `FixedUpdate` 단계 안에 있으므로 그 단계와 함께 0\~N회 반복된다 (03-L01). 위치가 `PhysicsFixedUpdate` **뒤**라서 `WaitForFixedUpdate` 뒤의 코드는 "물리 스텝 직후"를 본다.
  - `ScriptRunDelayedTasks` (`Update` 단계) = `UnitySynchronizationContext`에 `Post`된 **Task continuation**을 실행한다.
  - 프레임 끝 대기(`WaitForEndOfFrame`, `EndOfFrameAsync`)는 플레이어에서 `PlayerSendFrameComplete` 안에서 풀린다. 이름이 그럴듯한 `TriggerEndOfFrameCallbacks`는 아니었다 — **이름으로 단정하지 않고 잰 이유**. `TriggerEndOfFrameCallbacks`와 `PostLateUpdate/ScriptRunDelayedDynamicFrameRate`가 무엇을 실행하는지는 이 실습으로 확인 못 함 (확인 필요).
- **코루틴과 `Awaitable`은 같은 자리**에서 재개된다 (A=E, B=I, C=F, D=G). `Awaitable`은 새 스케줄러가 아니라 **엔진의 기존 대기 지점**에 continuation을 거는 방식으로 보인다.
- **`await Task.Yield()`는 "다음 프레임"이 아니다.** 같은 프레임의 `ScriptRunDelayedTasks`, 즉 모든 `Update` 콜백·코루틴 재개 직후, `LateUpdate`보다 앞에서 실행된다 (Δ 0). 프레임을 넘기려면 `Awaitable.NextFrameAsync()`(Δ 1)를 써야 한다.
- **에디터와 플레이어의 차이**: 프레임 끝이 에디터는 `PostLateUpdate` 단계를 다 돈 뒤, 플레이어는 `PlayerSendFrameComplete` 안. 에디터는 Game 뷰 렌더링이 플레이어와 다른 경로라서로 추정 (확인 필요). 01-L06·03-L01에 이어 "에디터 결과를 플레이어 결론으로 쓰지 않는다"의 사례.
- **배치모드에서 `FixedUpdate`가 안 돈 이유는 시간이다.** 그래픽이 없으면 60프레임이 3.4 ms(프레임당 약 0.06 ms)에 끝나서 20 ms가 쌓이지 않았다. 배치모드가 물리를 끄는 것이 아니다 — 시행 2에서 누적 시간이 20 ms를 넘은 프레임(+60)에 `FixedUpdate`가 돌았다. `FixedUpdate`·`WaitForSeconds`의 Δ프레임이 시행마다 다른 것도 같은 이유(프레임 길이에 따라 달라짐). 03-L04에서 이 누적을 직접 다룬다.
- **한 단계 더**
  - 콜백과 그 대기를 푸는 시스템이 **같은 단계 안에서 콜백 → 지연 실행 순서**로 붙어 있다 (`ScriptRunBehaviourUpdate` → `ScriptRunDelayedDynamicFrameRate` → `ScriptRunDelayedTasks`). 그래서 `yield return null` 뒤의 코드는 다음 프레임의 **모든 `Update()`가 끝난 뒤**에 돈다. "다음 프레임 `Update` 시점"이 아니라 "다음 프레임 `Update` 직후"다.
  - 이번 측정은 managed 마커를 하위 시스템 사이에 끼운 것이라, 한 하위 시스템 **안**의 세부 순서(예: 여러 코루틴끼리의 순서)는 알 수 없다.
- **근거**: 이 프로젝트의 실측 (Editor·Mono·IL2CPP 6000.3.25f1), 03-L01 트리 덤프. [Awaitable 문서](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Awaitable.html), [Order of execution](https://docs.unity3d.com/6000.3/Documentation/Manual/execution-order.html)
