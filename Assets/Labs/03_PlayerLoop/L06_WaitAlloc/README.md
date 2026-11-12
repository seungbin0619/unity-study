# 03-L06 대기 방식별 할당 (코루틴·Awaitable·Task)

## 목표
반복 대기(`yield return X` / `await X`)가 반복마다 GC 할당을 하는지, 시작(`StartCoroutine`, async 메서드 호출) 자체가 호출당 얼마를 할당하는지 잰다. `Awaitable` 풀링이 실제로 재사용되는지, 학습자 가설("async 메서드 여러 개를 한 번씩만 쓰면 풀링 의미 없음")도 확인한다.

## 예측
- 질문: 이미 돌고 있는 코루틴/async 메서드의 루프 안 한 줄이 워밍업 후 **반복마다** GC 할당을 하는가 (O/X)
  1. `yield return null;` 2. `yield return 0;` 3. `yield return new WaitForSeconds(0.01f);` 4. `yield return _cached;` (캐시한 `WaitForSeconds`) 5. `await Awaitable.NextFrameAsync();` 6. `await Task.Yield();`
- 학습자 답 (실행 전, 원문 그대로): "1. X / 2. O (박싱) / 3. O (새 객체) / 4. X / 5. X (최초 실행은 O?) / 6. X"

## 실습 구성
- 코드
  - `WaitHost.cs` — 코루틴을 돌릴 `MonoBehaviour`. 대기 방식별 무한 루프(코루틴 4종, `async Awaitable`, `async Task`)와 시작 비용·가설용 메서드
  - `WaitAllocLab.cs`
    - **1부 반복당**: 대기자 100개를 동시에 돌리고, 워밍업 뒤 반복 횟수(정적 카운터)와 그동안의 `GC Allocated In Frame`·할당 횟수 합계를 잰다 → 반복당 바이트·횟수. 기준선(대기자 없음)도 측정
    - **2부 시작당**: 한 프레임에 1000개 시작(한 번 대기하고 끝나는 코루틴 / `async Awaitable` / `async Task`), 그 프레임 할당 ÷ 1000. 다 끝날 때까지 기다렸다가 5라운드 반복 (풀 재사용 확인)
    - **3부 학습자 가설**: 순차로 8번 — 같은 async 메서드 8회 vs 서로 다른 async 메서드 8개 1회씩. 호출마다 그 프레임 할당
- 실행: 에디터 메뉴 `Labs/03/L06 Wait Alloc` 또는 플레이어 `-lab 03-L06`

## 결과
2026-11-12, 배치모드 **Development** 플레이어 (Incremental GC On). 에디터·Release 빌드는 실행하지 않음.

**1부 반복당** (대기자 100개, 반복 3000\~12100회)

| | 루프 안의 줄 | Mono | IL2CPP |
|---|---|---|---|
| 1 | `yield return null` | 0 | 0 |
| 2 | `yield return 0` | **20 B, 1회** | **20 B, 1회** |
| 3 | `yield return new WaitForSeconds(0.01f)` | **20 B, 1회** | **24 B, 1회** |
| 4 | `yield return _cached` | 0 | 0 |
| 5 | `await Awaitable.NextFrameAsync()` | 0 | 0 |
| 6 | `await Task.Yield()` | 0 | 0 |

- 추가 (같은 날, 확인 질문 검증): `IEnumerator<int>`로 선언한 `yield return 0`(`CoZeroGeneric`) — Mono·IL2CPP 모두 **반복마다 20 B, 1회** (`CoZero`와 같음). 재빌드한 실행이라 CSV에 두 번째 실행 행이 추가됨 (다른 조건 값은 첫 실행과 같음).
- 기준선(대기자 없음): 프레임당 0 B. 2의 측정값 19.8 B는 카운트를 켠 프레임 경계 오차이고, 실제로는 반복마다 20 B, 1회.

**2부 시작당** (한 프레임에 1000개 시작, 한 번 대기하고 끝남)

| 시작 방식 | 1라운드 | 2라운드 | 3\~5라운드 |
|---|---|---|---|
| `StartCoroutine(CoOnce())` | 64 B, 2회 | 64 B | 64 B |
| `async Awaitable` | 428 B, 4.9회 | 173 B | **144 B, 2회** |
| `async Task` (Mono / IL2CPP) | 366 / 382 B, 4회 | 288 / 304 B | **288 / 304 B, 4회** |

**3부 학습자 가설** (순차, 호출마다 그 프레임 할당. Mono / IL2CPP)

| | 1회째 | 2\~8회째 |
|---|---|---|
| 같은 메서드 8회 | 1964 / 1396 B (32 / 20회) | **48 B, 1회** |
| 서로 다른 메서드 8개 1회씩 | 1836 / 1268 B | **매번 1676 / 1108 B (31 / 19회)** |

- `Awaitable`과 `StateMachineBox<T>` 풀은 모두 `ObjectPool`이고 `m_MaxSize` = 10000이다 (에디터 리플렉션).
- IL2CPP Development 생성 코드에서 `<AwSame>d__14`는 `: public RuntimeObject`, 즉 **class**였다 (필드 `state`, `builder`, awaiter `<>u__1`). 에디터 컴파일에서는 struct(`IsValueType = True`).

원본: `Notes/results/03-L06_Mono_2026-11-12.csv`, `03-L06_IL2CPP_2026-11-12.csv`

## 해석
- **예측과 비교: 6/6 맞음.** 반복마다 할당하는 것은 2(박싱)와 3(새 객체)뿐이고, 둘 다 객체 하나(헤더 16 B + 값 4 B = 20 B)다. 5의 "최초 실행은 O?"도 맞다. 반복은 0이지만 **시작**(2부)과 **메서드를 처음 쓸 때**(3부)는 할당한다.
- **반복 대기 쪽.** `yield return 0`이 박싱하는 이유는 `IEnumerator.Current`의 타입이 `object`라서다. 2장의 "값이 `object`/인터페이스 자리로 가면 박싱"과 같은 규칙이다. `yield return null`이나 캐시한 객체는 참조를 넘길 뿐이라 0이다. 엔진은 `Current`의 **타입**만 보고 다음 재개 위치를 정하기 때문에, 같은 `WaitForSeconds` 인스턴스를 계속 넘겨도 된다. (단, 여러 코루틴이 공유해도 되는지는 이 실습 범위 밖이다.)
  - **`IEnumerator<int>`로 바꿔도 박싱은 그대로다.** `<>2__current`가 `int` 필드가 되어 `MoveNext`에서는 박싱이 사라진다(`__current = 0`). 대신 컴파일러가 만든 비제네릭 `IEnumerator.Current` getter가 읽을 때마다 `Box(int32)`를 한다. `StartCoroutine`은 `IEnumerator`를 받으므로 엔진은 이 getter를 반복마다 부른다. 박싱 위치가 "쓰는 쪽"에서 "읽는 쪽"으로 옮겨갈 뿐이다. **받는 쪽의 시그니처가 `object`인 한 값 타입은 어디선가 박싱된다.**
  - `Task.Yield`도 반복당 0이다. 상태 머신 상자는 첫 멈춤 때 한 번만 만들어지고, 이후 await는 같은 상자를 쓴다. `UnitySynchronizationContext`로 `Post`할 때도 반복마다 새 객체가 생기지 않았다.
  - `WaitForSeconds`가 Mono 20 B / IL2CPP 24 B로 다른 이유는 확인 필요다(같은 4 B 필드인 박싱 `int`는 둘 다 20 B).
- **시작 쪽.** 정상 상태(3라운드 이후)에서 가장 싼 것은 코루틴(64 B)이다.
  - 코루틴 64 B, 2회: 이터레이터 클래스(`state`, `current`, `this`) + `Coroutine` 래퍼 객체. 이터레이터는 컴파일러가 항상 class로 만든다.
  - `async Awaitable` 144 B, 2회 (Development): 그중 48 B는 **class 상태 머신**이다. 3부의 "2회째부터 48 B, 1회"가 이것 하나다. 나머지 96 B·1회는 1000개를 동시에 시작할 때만 생긴다. 풀 크기(10000)는 충분하므로 원인은 **확인 필요**다. 동시 개수를 1·10·100·1000으로 바꿔 보면 가를 수 있다.
  - `async Task` 288\~304 B, 4회: `Task`(상자 겸용)와 상태 머신 등이다. 풀링이 없어 라운드가 지나도 줄지 않는다.
- **학습자 가설 검증: 맞음. 실제로는 더 나쁘다.** 서로 다른 async 메서드를 한 번씩 부르면 **매번 약 1.1\~1.7 KB, 19\~31회**를 할당한다. 이는 상자 하나(수십 B)가 아니라 그 상태 머신 타입의 **풀 인프라를 처음 만드는 비용**이다(`ThreadLocal`, `ObjectPool`, 내부 `List`, 팩토리 델리게이트, 제네릭 정적 초기화 등. 구성은 리플렉션으로 본 필드에서 추론). 같은 메서드는 1회째만 이 비용을 내고 2회째부터 48 B다. 즉 풀링은 **같은 메서드를 반복 호출할 때** 이득이고, 한 번만 쓰는 메서드에는 일회성 비용과 정적으로 잡아 두는 메모리만 남는다. 다만 이 비용은 메서드 종류당 한 번이라, 매 프레임 쌓이는 할당은 아니다.
- **한 단계 더**
  - **Development와 Release는 다를 수 있다.** 이 프로젝트에서 IL2CPP Release 빌드의 상태 머신은 struct였다(2026-11-04 확인). struct라면 상태 머신이 풀링된 상자 안에 복사되므로, 2회째부터 48 B도 0이 될 가능성이 있다(**확인 필요**). "결론은 Development Build에서 낸다"는 규칙이 async 할당에는 맞지 않는 경우다. Release에는 프로파일러 카운터가 없어서 측정 방법을 따로 찾아야 한다.
  - **면접 포인트.** "코루틴 vs async 중 뭐가 GC에 좋나?"에 대한 정답은 둘 중 하나가 아니다. 반복은 둘 다 0으로 만들 수 있다(`null`·캐시, `Awaitable`). 남는 차이는 **시작 비용**과 **빌드 구성**이고, `async Task`는 시작할 때마다 수백 B를 할당하며 풀링도 없다.
- **근거**: 이 프로젝트의 실측, IL2CPP 생성 코드(`Builds/IL2CPP/..._BackUpThisFolder_.../il2cppOutput`), 에디터 리플렉션(`Awaitable._pool`, `AwaitableAsyncMethodBuilder.StateMachineBox<T>._pool`). [Awaitable 문서](https://docs.unity3d.com/6000.3/Documentation/Manual/async-awaitable-introduction.html)
