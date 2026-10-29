# 01-L04 연쇄 파괴와 이벤트 구독 누수

## 목표
부모 GameObject를 `Destroy`하면 자식의 컴포넌트도 함께 파괴된다. 이때 (1) 다른 곳에 캐싱해 둔 자식 컴포넌트 참조가 언제 null로 보이는지, (2) 정적 C# 이벤트 구독을 해제하지 않은 컴포넌트가 파괴된 뒤 어떻게 동작하고 메모리에 남는지(Leaked Managed Shell) 확인한다.

## 예측
- 질문:

  ```csharp
  public static class GameEvents { public static event Action Tick; }

  class Listener : MonoBehaviour
  {
      void OnEnable() => GameEvents.Tick += OnTick;   // 해제하지 않음 (LAB: 의도적)
      void OnTick() => Debug.Log(name);               // Unity API 접근
  }

  var parent = new GameObject("Parent");
  var child  = new GameObject("Child");
  child.transform.SetParent(parent.transform);
  var cached = child.AddComponent<Listener>();       // 다른 시스템이 캐싱해 둔 참조

  Object.Destroy(parent);                            // 부모만 파괴
  ```

  | # | 시점 | 식 / 동작 | 예측 |
  |---|---|---|---|
  | 1 | `Destroy(parent)` 직후 (같은 프레임) | `cached == null` | |
  | 2 | 다음 프레임 | `cached == null` | |
  | 3 | 다음 프레임 | `GameEvents.Tick()` 호출 시 `OnTick` 안의 `name` 접근 결과 (에디터 / 플레이어) | |
  | 4 | 파괴 후 `GC.Collect()` | `cached`를 포함해 모든 강한 참조를 버린 뒤 `Listener` 매니지드 객체가 GC로 회수되는가? (a) 구독 유지 (b) `OnDisable`에서 구독 해제 | |

- 학습자 답 (실행 전, 2026-10-29):

  | # | 예측 |
  |---|---|
  | 1 | `false` |
  | 2 | `true` |
  | 3 | 에디터 `MissingReferenceException` / 플레이어 `NullReferenceException` |
  | 4 | (a) 회수 안 됨 — 이벤트의 `OnTick` 델리게이트가 들고 있음 / (b) 회수됨 |

## 실습 구성
- 코드:
  - `GameEvents.cs` — 정적 `event Action Tick`, 구독자 수 조회
  - `LeakyListener.cs` — `OnEnable`에서 구독, 해제 안 함 (a) / `CleanListener.cs` — `OnDisable`에서 해제 (b). 둘 다 `OnTick`에서 `name` 접근 결과(또는 예외 타입)와 `OnDestroy` 호출 여부를 기록
  - `CascadeDestroyLab.cs` — (a)(b) 각각 Parent/Child를 만들고 부모만 `Destroy`. 같은 프레임·다음 프레임에 캐싱 참조를 검사하고, 이벤트를 발생시킨 뒤, 강한 참조를 버리고 `WeakReference.IsAlive`로 GC 회수 여부를 본다. 이벤트가 실제 루트인지 확인하려고 `GameEvents.Clear()` 후에도 한 번 더 본다
- 실행: 에디터 메뉴 `Labs/01/L04 Cascade Destroy` 또는 플레이어 `-lab 01-L04`

## 결과
| 실행일 | 백엔드 | 빌드 | # | 항목 | (a) 구독 유지 | (b) `OnDisable`에서 해제 |
|---|---|---|---|---|---|---|
| 2026-10-29 | Editor (Mono) | 에디터 플레이 모드 | 1 | 같은 프레임 `cached == null` / `child == null` | `false` / `false` | `false` / `false` |
| | | | 2 | 다음 프레임 `cached == null` / `child == null` | `true` / `true` | `true` / `true` |
| | | | – | 자식 컴포넌트의 `OnDestroy` 호출 | 예 | 예 |
| | | | – | 파괴 후 구독자 수 (생성 직후 2) | 1 (남음) | 0 (해제됨) |
| | | | 3 | `Tick()` → `OnTick`의 `name` | `MissingReferenceException` | 호출 안 됨 |
| | | | 4 | GC 후 매니지드 객체 생존 | **살아 있음** → `Clear()` 후 회수됨 | 회수됨 |
| 2026-10-29 | Mono | Development 플레이어 | 1~4 | | 3번만 `NullReferenceException`, 나머지 에디터와 동일 | 에디터와 동일 |

원본: `Notes/results/01-L04_Editor_2026-10-29.csv`, `Notes/results/01-L04_Mono_2026-10-29.csv`

## 해석
- 예측과 비교: 4문항 모두 맞음. 3번은 01-L01·01-L06 결과(에디터/플레이어 예외 타입 차이)를 그대로 적용했다.
- **연쇄 파괴**: 부모만 파괴해도 자식 GameObject와 그 컴포넌트가 함께 파괴되고 `OnDestroy`도 불린다. 캐싱해 둔 참조는 01-L01과 똑같이 같은 프레임에는 살아 있고, 다음 프레임부터 `== null`이 `true`가 된다.
- **Leaked Managed Shell**: 구독을 해제하지 않으면 정적 이벤트의 델리게이트가 `Target`으로 `Listener` 인스턴스를 강하게 참조한다. 그래서 네이티브 객체는 파괴됐는데 매니지드 래퍼는 GC 루트(정적 필드)에서 도달 가능한 상태로 남는다. `GameEvents.Clear()`로 델리게이트를 끊자마자 회수된 것으로, 이 이벤트가 유일한 루트였음을 확인했다.
- 예외는 핸들러가 호출되는 순간이 아니라 **Unity API(`name`)에 접근하는 순간** 난다. C# 메서드 호출 자체는 살아 있는 매니지드 객체에 대한 평범한 호출이고, 네이티브 경계를 넘을 때 래퍼에 네이티브 객체가 없다는 게 드러난다.
- 한 단계 더:
  - **멀티캐스트 델리게이트는 한 핸들러가 예외를 던지면 나머지 핸들러를 호출하지 않는다.** 파괴된 리스너 하나가 남아 있으면, 그 뒤에 구독한 정상 리스너들까지 이벤트를 못 받는다. 누수가 메모리 문제에서 끝나지 않고 기능 버그로 번지는 이유다.
  - 새는 것은 래퍼 하나가 아니다. 그 래퍼의 필드가 가리키는 모든 매니지드 객체도 함께 남는다. 또 `Resources.UnloadUnusedAssets`는 스크립트가 참조하는 에셋을 "사용 중"으로 보므로, 누수된 셸이 텍스처나 메시 같은 에셋을 참조하면 그 **네이티브 메모리도 해제되지 않는다** (Unity Scripting API 문서의 `Resources.UnloadUnusedAssets` 설명. 실측은 02-L08 Memory Profiler에서).
  - 구독 해제는 `OnDisable`에 두는 것이 `OnEnable`과 짝이 맞는다. `SetActive(false)`일 때도 이벤트를 받지 않게 되고, 파괴 시에도 `OnDisable`이 먼저 불린다.
  - "Leaked Managed Shell"은 Memory Profiler가 이 상태(네이티브 객체 없이 남은 `UnityEngine.Object` 래퍼)를 표시하는 이름이다. 02-L08에서 스냅샷으로 다시 확인한다.
- 남은 확인: IL2CPP 플레이어.
- 근거: 실측 CSV. C# 언어 동작(멀티캐스트 델리게이트 호출, 델리게이트의 `Target` 참조).
