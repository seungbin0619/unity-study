# 03-L08 수명과 취소

## 목표
`Destroy`·`SetActive(false)`·`enabled = false` 뒤에 코루틴 / `async Awaitable` / `async Task` 루프가 계속 도는지 센다. `destroyCancellationToken`의 효과와 파괴된 객체 접근 예외(에디터 vs 플레이어)도 함께 본다.

## 예측
- 질문: 한 프레임마다 `Time.frameCount`를 로그하는 루프 3개(`Co` = `yield return null`, `Aw` = `Awaitable.NextFrameAsync()`, `Task` = `Task.Yield()`)를 한 `MonoBehaviour`에서 돌리다 `Destroy(gameObject)`하면, 이후에도 계속 도는 것은?
- 학습자 답 (실행 전, 원문 그대로): "Aw, Task. Co는 해당 MonoBehaviour가 실행할 지점을 들고 있어서. 나머지는 context상태로 엔진이 들고잇음"
- 질문 2 (결과 공개 전): `Destroy` 대신 `enabled = false`면 `Co`는 계속 도나? `SetActive(false)`면?
- 학습자 답 (결과 공개 전, 원문 그대로): "O, 이유는 뭐라못하겟네, 그냥 그렇게 설계했을듯" / "SetActive도 미리 O로 답변"

- 질문 3 (심화 03-L08b, 실행 전): `AwToken`(`NextFrameAsync(token)`)은 파괴 프레임(+0)에, `TaskToken`(`Task.Yield()` 뒤 `ThrowIfCancellationRequested`)은 +1에 끝났다. `Cancel()`부터 `catch`까지 무슨 일이 일어났나?
- 학습자 답 (실행 전, 원문 그대로): "AwToken은 Cancel이 호출되면 PlayerLoop에서 DelayedScript가 호출되면서 토큰의 상태를 확인하고 취소처리가 됐을듯? 근데 반면에 Yield는 왜 1프레임 뒤에 되는지 모르겠네"
- 힌트 후 TaskToken 답 (원문 그대로): "TaskToken의 토큰을 보는 줄이 DelayedScript에 있고, 그 줄이 실행되려면 Destoy된 이후 (1프레임이 지나서) 다시 그 시스템이 실행되어야하는건가"

## 실습 구성
- 코드: `LifetimeLab.cs` — 시나리오(`Destroy` / `SetActive(false)` / `enabled = false`)마다 새 Host를 만들고 루프 7종을 건다: `Co`, `CoOnOtherHost`(Host의 이터레이터를 다른 MonoBehaviour가 `StartCoroutine`), `Aw`, `Task`, `AwToken`(`NextFrameAsync(destroyCancellationToken)`), `TaskToken`(`Task.Yield` 뒤 `ThrowIfCancellationRequested`), `AwTouchTransform`(매 반복 `transform.position` 읽기). 3프레임 → 조작 → 5프레임 → 복원(`SetActive(true)`/`enabled = true`) → 5프레임 → 정지 플래그. 구간별 반복 횟수, 종료 사유(예외 타입), `finally` 실행 여부 기록
- 실행: 에디터 메뉴 `Labs/03/L08 Lifetime` 또는 플레이어 `-lab 03-L08`
- 심화 코드: `TokenOrderLab.cs` (03-L08b) — Host에 `AwToken`·`TaskToken` 루프와 직접 `token.Register` 콜백을 걸고 `Destroy`. `OnDisable`·`OnDestroy`·콜백·각 `catch`의 순번, 프레임, PlayerLoop 위치(03-L02 `LoopMarkers`), 호출 스택(`StackTrace`)을 기록. 메뉴 `Labs/03/L08b Token Order` 또는 `-lab 03-L08b`
- 소스: `UnityCsReference` 태그 `6000.3`의 `Runtime/Export/Scripting/MonoBehaviour.bindings.cs`, `Awaitable.cs`, `Awaitable.DelayedCall.cs`

## 결과
2026-11-14, 에디터 + 배치모드 Development 플레이어(Mono·IL2CPP). 세 환경의 반복 횟수·종료 시점이 모두 같고, 다른 것은 `AwTouchTransform`의 예외 타입 하나뿐이다.

### Destroy (N프레임에 `Destroy(gameObject)`, 이후 10프레임 관찰)
| 루프 | 조작 뒤 반복 (5프레임 / 다음 5프레임) | 마지막 반복 (조작 프레임 기준) | 종료 | `finally` |
|---|---|---|---|---|
| `Co` | 1 / 0 | +0 (같은 프레임에 한 번 더) | 재개되지 않음 (버려짐) | **실행 안 됨** |
| `CoOnOtherHost` | 5 / 5 | 계속 | 플래그로 정지 | 실행 |
| `Aw` | 5 / 5 | 계속 | 플래그로 정지 | 실행 |
| `Task` | 5 / 5 | 계속 | 플래그로 정지 | 실행 |
| `AwToken` | 0 / 0 | +0 | `OperationCanceledException` (+0 프레임) | 실행 |
| `TaskToken` | 1 / 0 | +0 | `OperationCanceledException` (+1 프레임) | 실행 |
| `AwTouchTransform` | 1 / 0 | +1 | 에디터 `MissingReferenceException` / Mono·IL2CPP `NullReferenceException` (+1 프레임) | 실행 |

### `SetActive(false)` / `enabled = false` (5프레임 뒤 `SetActive(true)` / `enabled = true`로 복원)
| 루프 | `SetActive(false)` 동안 / 복원 뒤 | `enabled = false` 동안 / 복원 뒤 |
|---|---|---|
| `Co` | **0 / 0** — 조작 프레임에도 안 돎, 다시 켜도 재개 안 됨, `finally` 실행 안 됨 | **5 / 5** — 영향 없음 |
| `CoOnOtherHost` | 5 / 5 | 5 / 5 |
| `Aw` · `Task` | 5 / 5 | 5 / 5 |
| `AwToken` · `TaskToken` | 5 / 5 (토큰은 취소되지 않음) | 5 / 5 |
| `AwTouchTransform` | 5 / 5 (비활성 객체의 `transform`은 정상) | 5 / 5 |

원본: `Notes/results/03-L08_Editor_2026-11-14.csv`, `03-L08_Mono_2026-11-14.csv`, `03-L08_IL2CPP_2026-11-14.csv`

### 03-L08b 파괴 프레임 안의 순서 (2026-11-14, 에디터·Mono·IL2CPP 동일)
| 순번 | 사건 | 프레임 | PlayerLoop 위치 | 비고 |
|---|---|---|---|---|
| 0 | `Destroy(gameObject)` 호출 | +0 | `Update/ScriptRunDelayedDynamicFrameRate` | |
| 1 | `OnDisable` | +0 | 같음 — **`Destroy()` 호출 안** | 토큰 아직 취소 안 됨 |
| 2 | `Destroy()` 반환 | +0 | 같음 | `host == null`은 `false` |
| 3 | `AwToken` catch | +0 | `PostLateUpdate` 이후 실제 파괴 처리 | 스택: `MonoBehaviour.RaiseCancellation` → `CTS.Cancel` → `<WireupCancellation>` 람다 → `Awaitable.Cancel` → `RaiseManagedCompletion` → `RunOrScheduleContinuation` → `MoveNext` |
| 4 | 직접 건 `token.Register` 콜백 | +0 | 같음 | 나중에 등록된 `AwToken`이 먼저 실행됨. `this == null`은 `false` |
| 5 | `OnDestroy` | +0 | 같음 | 토큰 **이미 취소됨**, `this == null`은 `false` |
| 6 | `TaskToken` catch | **+1** | `Update/ScriptRunDelayedTasks` | 스택: `UnitySynchronizationContext.Exec` → `YieldAwaiter` → `MoveNext` |

IL2CPP 스택에는 인라이닝과 동일 코드 병합 때문에 엉뚱한 이름(`ObjectEqualityComparer.LastIndexOf`)이 끼어 있고 중간 프레임이 빠진다. 순서와 위치는 같다.

원본: `Notes/results/03-L08b_Editor_2026-11-14.csv`, `03-L08b_Mono_2026-11-14.csv`, `03-L08b_IL2CPP_2026-11-14.csv`

## 해석
- 예측과 비교 (Destroy): 3/3 정답. `Co`는 멈추고 `Aw`·`Task`는 계속 돈다.
  - 이유 중 `Co`는 맞다. 코루틴은 `StartCoroutine`을 **호출한** MonoBehaviour에 묶인다. 같은 이터레이터라도 다른 MonoBehaviour가 돌리면(`CoOnOtherHost`) Host가 파괴된 뒤에도 계속 돈다. 기준은 메서드가 정의된 곳이 아니라 스케줄을 건 쪽이다.
  - `Aw`·`Task`의 이유는 다듬을 부분이 있다. continuation을 들고 있는 것은 `Task`는 `UnitySynchronizationContext`의 대기열, `Awaitable`은 Awaitable 프레임 대기열이다. 핵심은 둘 다 **그 MonoBehaviour의 수명을 모른다**는 점이다. 상태 머신은 `this`를 필드 하나로 들고 있을 뿐이고, `Destroy`는 네이티브 객체만 없앨 뿐 매니지드 객체에는 손대지 않는다.
- 한 단계 더:
  - `Destroy`는 지연 실행이라 `Co`도 같은 프레임 안에서는 한 번 더 돌았다(+0).
  - 버려진 코루틴은 `Dispose()`가 불리지 않아 `finally`가 실행되지 않는다. 코루틴 안의 `try/finally`·`using`으로 하는 정리는 Destroy나 비활성화 때 실행되지 않는다.
  - `destroyCancellationToken`은 파괴되는 프레임에 취소된다. 토큰을 받는 `NextFrameAsync(ct)`는 그 프레임에 바로 예외로 끝나고(+0), 토큰을 직접 확인하는 `Task` 루프는 다음 재개 때(+1) 끝난다. 토큰은 **멈추는 지점에 넘겨야** 즉시 끝난다.
  - 파괴된 객체에 접근하면 에디터는 `MissingReferenceException`, 플레이어는 `NullReferenceException`을 던진다. 01-L01·01-L06의 "에디터와 플레이어는 다른 코드"와 같은 이유다.
  - 계속 도는 async 루프는 상태 머신이 `this`(래퍼)를 붙잡고 있어 GC가 셸을 회수하지 못한다. 02-L08의 구독 누수와 같은 모양이다.
- 예측과 비교 (질문 2): 1/2. `enabled = false`는 O로 맞음. `SetActive(false)`는 O로 답했으나 X다. 비활성화하는 순간 그 GameObject의 MonoBehaviour들에 걸린 코루틴이 **정지(stop)**된다. 일시정지가 아니라서 다시 켜도 재개되지 않고, `finally`도 실행되지 않는다(Destroy와 같은 "버림").
  - 두 조작이 다른 이유: `enabled`는 컴포넌트 하나에 대한 "엔진 콜백(`Update` 등)을 받을지" 플래그다. 코루틴 목록은 그 콜백 목록과 별개라서 영향을 받지 않는다. GameObject 비활성화는 객체를 씬에서 빼는 것에 가깝다. 모든 컴포넌트에 `OnDisable`이 불리고 엔진 처리 대상에서 빠지며, 코루틴도 함께 정지된다(공식 매뉴얼 Coroutines). 정지인지 일시정지인지는 문서 문구만으로는 애매했는데, 실측으로 "정지, 재개 안 됨"을 확인했다. Unity가 왜 이렇게 설계했는지는 문서에 없다(추론).
  - 비활성 GameObject에서는 `StartCoroutine` 자체가 오류로 거부된다. 그래서 "다시 켤 때 재시작"이 필요하면 `OnEnable`에서 `StartCoroutine`을 하는 패턴을 쓴다.
  - `async` 쪽은 두 조작 모두 영향이 없다. `destroyCancellationToken`도 이름대로 **파괴**에만 반응하고 비활성화에는 취소되지 않는다. 비활성화 때 멈추려면 `OnDisable`에서 직접 취소하는 `CancellationTokenSource`가 필요하다.
- 예측과 비교 (질문 3, 03-L08b):
  - `AwToken`: 오답. "PlayerLoop에서 토큰 상태를 확인한다"는 **확인(polling)** 모델이었지만, `Awaitable`은 **등록(registration)** 방식이다. `NextFrameAsync(token)`은 대기열에 넣으면서 `token.Register(a => a.Cancel())`을 건다. 엔진이 파괴 처리 중에 `RaiseCancellation` → `CTS.Cancel()`을 부르면 콜백이 그 자리에서 awaitable을 예외로 완료시키고, 메인 스레드라서 `MoveNext`까지 같은 호출 스택에서 동기로 실행된다. 그래서 `catch`가 PlayerLoop 재개 지점이 아니라 **엔진의 파괴 처리 한가운데**에서 돈다.
  - `TaskToken`: 힌트 후 정답. 학습자의 처음 모델이 정확히 이 경우다. `Task.Yield()`는 토큰을 받지 않아 등록할 곳이 없고, 토큰은 재개된 **뒤에** `ThrowIfCancellationRequested`로 확인할 수밖에 없다. 위치를 다듬으면 `ScriptRunDelayedDynamicFrameRate`가 아니라 `Update/ScriptRunDelayedTasks`(SynchronizationContext 대기열)다. 파괴 프레임의 `ScriptRunDelayedTasks`는 `PostLateUpdate`의 파괴보다 **앞**이라 그때는 아직 취소 전이므로 다시 `Yield`한다. 그래서 다음 프레임의 `ScriptRunDelayedTasks`에서야 `throw`한다.
- 한 단계 더 (03-L08b):
  - 확인과 등록의 차이는 **지연**뿐 아니라 **대기열 정리**에도 있다. 확인 방식에서는 취소된 작업도 대기열에 남아 있다가 깨어나야 끝난다. 등록 방식은 `Cancel` 안에서 대기열에서 빼낸다(`_managedCompletionQueue.Remove`). 오래 기다리는 `WaitForSecondsAsync(60, token)`이라면 확인 방식은 60초를 기다려야 끝나지만 등록 방식은 즉시 끝난다.
  - `OnDisable`은 `Destroy()` 호출 안에서 즉시 불리고, 미뤄지는 것은 실제 파괴(토큰 취소 → `OnDestroy`)다. 토큰 취소가 `OnDestroy`보다 먼저라서 `OnDestroy`가 돌 때 토큰을 건 async 루프는 이미 `catch`를 지났다.
  - 등록 방식의 대가: 취소 콜백과 그 뒤 continuation이 **엔진의 파괴 처리 안에서** 동기로 돈다. `catch` 뒤에 무거운 작업을 두면 파괴 처리가 그만큼 길어진다(추론, 측정 안 함).
  - `destroyCancellationToken` getter는 CTS를 **지연 생성**하고, 그때 네이티브에 `OnCancellationTokenCreated`로 등록한다. 한 번도 읽지 않으면 비용이 0이고 `RaiseCancellation`도 불리지 않는다(소스 기준). 파괴 뒤에 처음 읽으면 `this == null` 검사에서 `MissingReferenceException`을 던진다.
  - `Task` 쪽에서 즉시 취소하려면 토큰을 받는 API(`Task.Delay(ms, token)`)를 쓰거나, `token.Register`로 `TaskCompletionSource`를 끝내는 식으로 직접 등록해야 한다.
- 근거: 이 프로젝트 실측 (세 환경). Unity 매뉴얼 Coroutines(비활성화 시 코루틴 정지, `enabled = false`는 정지 안 함). `MonoBehaviour.destroyCancellationToken` — Unity 6 Scripting API.
