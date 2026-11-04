# Unity Engine Internals Labs

Unity 엔진 내부 동작을 문서로만 읽지 않고 **직접 측정해서 확인하는** 실습 모음입니다.
각 실습은 **예측 → 측정 → 비교 → 해석** 순서로 진행하고, 결과는 실습별 README에 기록합니다.

| 환경 | 값 |
|---|---|
| Unity | 6000.3.25f1 (Unity 6) |
| 플랫폼 | Windows 11, Windows Standalone x64 |
| 측정 기준 | Development Build 플레이어. 에디터 측정은 참고용 |

## 진행 현황

| 주제 | 진행 |
|---|---|
| [1. 네이티브–매니지드 경계](#1-네이티브매니지드-경계) | 10 / 10 완료 |
| [2. 메모리와 GC](#2-메모리와-gc) | 9 / 9 완료 |
| [3. PlayerLoop](#3-playerloop) | 3 / 10 완료 |
| 4. 직렬화와 에셋 파이프라인 · 5. 렌더링 파이프라인 · 6. DOTS | 예정 |

## 실행 방법

- **에디터**: 메뉴 `Labs/<주제>/<실습>`
- **플레이어**: 모든 실습이 들어간 빌드를 `-lab <실습 ID>` 인자로 실행

  ```
  Labs.exe -lab 02-L05 -logFile Notes/results/player_02-L05.log
  ```

- **결과**: 원본 CSV는 `Notes/results/`, 요약과 해석은 각 실습 README

## 1. 네이티브–매니지드 경계

C# 래퍼와 C++ 네이티브 객체의 관계, 경계를 넘는 비용, IL2CPP가 만드는 코드.

> ✅ **[01-L01 가짜 null과 null 연산자](Assets/Labs/01_NativeManaged/L01_FakeNull/README.md)** · `Mono`\
> `Destroy` 다음 프레임에 `==`·`bool`만 null로 판정. `is null`·`?.`·`??`는 래퍼를 통과시켜 예외

> ✅ **[01-L02 `new` vs `AddComponent`](Assets/Labs/01_NativeManaged/L02_NewVsAddComponent/README.md)** · `Mono`\
> `new`로 만든 컴포넌트는 `== null`인 래퍼만 남음. `'new' keyword` 경고는 에디터에서만 출력

> ✅ **[01-L03 생성자 호출 시점·스레드](Assets/Labs/01_NativeManaged/L03_CtorTiming/README.md)** · `Mono`\
> 생성자는 항상 `Awake`보다 먼저. 씬 로드는 **동기 `LoadScene`도** 로딩 스레드에서 생성자 호출

> ✅ **[01-L04 연쇄 파괴와 이벤트 구독 누수](Assets/Labs/01_NativeManaged/L04_CascadeDestroy/README.md)** · `Mono`\
> 해제하지 않은 정적 이벤트 구독이 파괴된 래퍼를 GC 후에도 살려 둠 (Leaked Managed Shell)

> ✅ **[01-L05 경계 비용](Assets/Labs/01_NativeManaged/L05_BoundaryCost/README.md)** · `Mono`\
> 호출당 `mesh.vertices` 12 KB·`tag ==` 34 B 할당, 대체 API는 0 B. `GetComponent`는 캐싱보다 약 25배 느림

> ✅ **[01-L06 바인딩 코드 읽기](Assets/Labs/01_NativeManaged/L06_ReadBindings/README.md)** · `Mono`\
> `MissingReferenceException`은 에디터 전용. 플레이어 DLL에서는 해당 메서드 본문이 비어 있음 (IL 2바이트)

> ✅ **[01-L07 null·경계 검사와 `Il2CppSetOption`](Assets/Labs/01_NativeManaged/L07_NullChecks/README.md)** · `IL2CPP`\
> 역참조마다 `NullCheck` 1개. 검사를 끄면 배열 루프가 원소당 약 2.9배 빨라짐

> ✅ **[01-L08 `sealed`와 devirtualization](Assets/Labs/01_NativeManaged/L08_Sealed/README.md)** · `IL2CPP`\
> `sealed`를 붙여도 가상 호출로 남음. IL 호출 대상 메서드가 final일 때만 직접 호출 (약 4배 빠름)

> ✅ **[01-L09 제네릭 공유와 full generic sharing](Assets/Labs/01_NativeManaged/L09_GenericSharing/README.md)** · `IL2CPP`\
> `List<T>.Add` 본문 4벌 → full sharing 1벌 (DLL 43 → 24 MB, 값 타입 5배 느림). 참조 타입 `Add` 비용의 약 90%는 쓰기 배리어

> ✅ **[01-L10 코드 스트리핑과 `[Preserve]`](Assets/Labs/01_NativeManaged/L10_Stripping/README.md)** · `IL2CPP`\
> Stripping High에서 상수 문자열 리플렉션 대상은 타입만 남아 `MissingMethodException`. `[Preserve]`는 타입과 생성자만 보존

백엔드는 결론을 낸 플레이어 기준입니다. 01-L01\~L06은 IL2CPP 플레이어에서 아직 실행하지 않았습니다.

## 2. 메모리와 GC

Boehm GC의 특성, 숨은 할당, 힙 크기와 단편화, Incremental GC.

> ✅ **[02-L01 숨은 할당 측정](Assets/Labs/02_GC/L01_HiddenAlloc/README.md)** · `Mono` `IL2CPP`\
> 델리게이트 생성이 128 B로 가장 큼 (C# 9는 메서드 그룹·`this` 캡처 람다를 캐시 안 함, 필드 캐시 시 0). `IEnumerable<T>` foreach·LINQ는 열거자 박싱 40 B, `List.Sort()`도 매번 128 B

> ✅ **[02-L02 클로저 할당 시점](Assets/Labs/02_GC/L02_ClosureTiming/README.md)** · `Mono` `IL2CPP`\
> 클로저는 캡처 변수의 스코프 진입 시 생성. 매개변수를 캡처하면 람다 분기를 안 타도 호출마다 20\~24 B

> ✅ **[02-L03 `Instantiate`/`Destroy` vs 풀링](Assets/Labs/02_GC/L03_Pooling/README.md)** · `Mono` `IL2CPP`\
> `Instantiate`의 GC 할당은 GameObject 래퍼 24 B + 스크립트당 40 B뿐(나머지는 네이티브). 풀링 이득은 주로 CPU: 약 2.1\~2.6배

> ✅ **[02-L04 GC Used vs Reserved](Assets/Labs/02_GC/L04_UsedVsReserved/README.md)** · `Mono` `IL2CPP`\
> 50 MB 해제 + Collect 후 Reserved는 51.8 MB 유지. IL2CPP는 수집 6\~12회 뒤 반납, Mono는 32회 뒤에도 유지. 단편화 시 빈 25 MB에 20 MB가 안 들어감

> ✅ **[02-L05 데이터 배치에 따른 GC 마킹 비용](Assets/Labs/02_GC/L05_MarkCost/README.md)** · `Mono` `IL2CPP`\
> 참조 없는 `struct[]`는 100만 개여도 기준선과 같음. 참조 필드 하나 든 `struct[]`가 문자열을 공유해도 `List<class>`보다 약 1.7배 비쌈 (훑는 바이트가 3배)

> ✅ **[02-L06 Incremental GC On/Off 프레임 타임](Assets/Labs/02_GC/L06_IncrementalGC/README.md)** · `Mono` `IL2CPP`\
> Off는 GC마다 10 ms 스파이크. On은 부분(세대별) 수집이라 GC 프레임 약 1 ms, 20회에 한 번 full 마킹이 3\~6 ms × 2\~3프레임. 총 시간도 On이 적음. 단 30프레임 사는 객체가 많으면 이점이 사라지고, 장수 데이터를 넓게 고치면 On이 약 3배 손해

> ✅ **[02-L07 `GarbageCollector.Mode.Manual`과 `CollectIncremental`](Assets/Labs/02_GC/L07_ManualGC/README.md)** · `Mono` `IL2CPP`\
> 매 프레임 호출하면 호출마다 새 수집을 시작해 Used는 평평하지만 CPU는 Enabled의 2.5\~3.7배. slice는 상한이 아님 (마무리 바닥 0.6\~1 ms). 60프레임마다 부르면 GC 시간은 1/30이 되지만 Used가 80 MB 톱니

> ✅ **[02-L08 Memory Profiler 스냅샷으로 누수 찾기](Assets/Labs/02_GC/L08_SnapshotLeak/README.md)** · `Mono` `IL2CPP`\
> 구독 누수는 파괴된 셸 + 필드를 매니지드에 남김. 런타임 `Texture2D`는 구독과 무관하게 네이티브 50 MB 잔존, 셸이 참조하면 `UnloadUnusedAssets`로도 안 풀림

> ✅ **[02-L09 파이널라이저와 수거 지연](Assets/Labs/02_GC/L09_Finalizer/README.md)** · `Mono` `IL2CPP`\
> 파이널라이저 객체는 "Collect → 파이널라이저 스레드 실행 → 다음 Collect"에서 회수. 매 프레임 Collect하면 스레드가 틈에만 돌아 Mono 약 170회·IL2CPP 약 16회, 60fps면 2\~3회. 첫 수집이 3\~4배 비싸고(다시 마킹), IL2CPP는 `trackResurrection` 미지원

## 3. PlayerLoop

엔진 프레임 루프의 구조, 생명주기 콜백·코루틴·Awaitable이 실행되는 위치, 커스텀 시스템과 편집 모드 루프.

> ✅ **[03-L01 PlayerLoop 트리 덤프](Assets/Labs/03_PlayerLoop/L01_TreeDump/README.md)** · `Mono` `IL2CPP`\
> 최상위 8단계·하위 131개, 기본 트리는 에디터·플레이어 동일. 에디터만 Input System이 `#if UNITY_EDITOR`로 managed 시스템 1개 삽입

> ✅ **[03-L02 콜백·재개 위치 지도](Assets/Labs/03_PlayerLoop/L02_ResumeMap/README.md)** · `Mono` `IL2CPP`\
> 코루틴과 `Awaitable`은 같은 자리에서 재개. `await Task.Yield()`는 같은 프레임 `ScriptRunDelayedTasks`(Δ 0), 프레임 끝은 플레이어에서 `PlayerSendFrameComplete` 안

> ✅ **[03-L03 `Instantiate` 시점별 `Start` 호출 시점](Assets/Labs/03_PlayerLoop/L03_StartTiming/README.md)** · `Mono` `IL2CPP`\
> `Awake`·`OnEnable`은 `Instantiate` 안. `Start`는 다음 프레임이 아니라 그다음 스크립트 실행 지점: `Update`에서 만들면 같은 프레임에 `Start` → `LateUpdate`

> ⏳ 03-L04 `FixedUpdate` 따라잡기와 `maximumDeltaTime`

> ⏳ 03-L05 `Update` 1만 개 vs 매니저 vs 커스텀 시스템

> ⏳ 03-L06 대기 방식별 할당 (코루틴·Awaitable·Task)

> ⏳ 03-L07 스레드 전환과 continuation 위치

> ⏳ 03-L08 비활성화·파괴 시 코루틴·Awaitable·Task 수명

> ⏳ 03-L09 커스텀 시스템 수명과 도메인 리로드 (에디터)

> ⏳ 03-L10 편집 모드 루프와 `[ExecuteAlways]` (에디터)
