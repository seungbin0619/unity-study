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
| [2. 메모리와 GC](#2-메모리와-gc) | 0 / 9 |
| 3. PlayerLoop · 4. 직렬화와 에셋 파이프라인 · 5. 렌더링 파이프라인 · 6. DOTS | 예정 |

## 실행 방법

- **에디터**: 메뉴 `Labs/<주제>/<실습>`
- **플레이어**: 모든 실습이 들어간 빌드를 `-lab <실습 ID>` 인자로 실행

  ```
  Labs.exe -lab 02-L05 -logFile Notes/results/player_02-L05.log
  ```

- **결과**: 원본 CSV는 `Notes/results/`, 요약과 해석은 각 실습 README

## 1. 네이티브–매니지드 경계

C# 래퍼와 C++ 네이티브 객체의 관계, 경계를 넘는 비용, IL2CPP가 만드는 코드.

> [!TIP]
> **[01-L01 가짜 null과 null 연산자](Assets/Labs/01_NativeManaged/L01_FakeNull/README.md)** · `Mono`\
> `Destroy` 다음 프레임에 `==`·`bool`만 null로 판정. `is null`·`?.`·`??`는 래퍼를 통과시켜 예외

> [!TIP]
> **[01-L02 `new` vs `AddComponent`](Assets/Labs/01_NativeManaged/L02_NewVsAddComponent/README.md)** · `Mono`\
> `new`로 만든 컴포넌트는 `== null`인 래퍼만 남음. `'new' keyword` 경고는 에디터에서만 출력

> [!TIP]
> **[01-L03 생성자 호출 시점·스레드](Assets/Labs/01_NativeManaged/L03_CtorTiming/README.md)** · `Mono`\
> 생성자는 항상 `Awake`보다 먼저. 씬 로드는 **동기 `LoadScene`도** 로딩 스레드에서 생성자 호출

> [!TIP]
> **[01-L04 연쇄 파괴와 이벤트 구독 누수](Assets/Labs/01_NativeManaged/L04_CascadeDestroy/README.md)** · `Mono`\
> 해제하지 않은 정적 이벤트 구독이 파괴된 래퍼를 GC 후에도 살려 둠 (Leaked Managed Shell)

> [!TIP]
> **[01-L05 경계 비용](Assets/Labs/01_NativeManaged/L05_BoundaryCost/README.md)** · `Mono`\
> 호출당 `mesh.vertices` 12 KB·`tag ==` 34 B 할당, 대체 API는 0 B. `GetComponent`는 캐싱보다 약 25배 느림

> [!TIP]
> **[01-L06 바인딩 코드 읽기](Assets/Labs/01_NativeManaged/L06_ReadBindings/README.md)** · `Mono`\
> `MissingReferenceException`은 에디터 전용. 플레이어 DLL에서는 해당 메서드 본문이 비어 있음 (IL 2바이트)

> [!TIP]
> **[01-L07 null·경계 검사와 `Il2CppSetOption`](Assets/Labs/01_NativeManaged/L07_NullChecks/README.md)** · `IL2CPP`\
> 역참조마다 `NullCheck` 1개. 검사를 끄면 배열 루프가 원소당 약 2.9배 빨라짐

> [!TIP]
> **[01-L08 `sealed`와 devirtualization](Assets/Labs/01_NativeManaged/L08_Sealed/README.md)** · `IL2CPP`\
> `sealed`를 붙여도 가상 호출로 남음. IL 호출 대상 메서드가 final일 때만 직접 호출 (약 4배 빠름)

> [!TIP]
> **[01-L09 제네릭 공유와 full generic sharing](Assets/Labs/01_NativeManaged/L09_GenericSharing/README.md)** · `IL2CPP`\
> `List<T>.Add` 본문 4벌 → full sharing 1벌 (DLL 43 → 24 MB, 값 타입 5배 느림). 참조 타입 `Add` 비용의 약 90%는 쓰기 배리어

> [!TIP]
> **[01-L10 코드 스트리핑과 `[Preserve]`](Assets/Labs/01_NativeManaged/L10_Stripping/README.md)** · `IL2CPP`\
> Stripping High에서 상수 문자열 리플렉션 대상은 타입만 남아 `MissingMethodException`. `[Preserve]`는 타입과 생성자만 보존

백엔드는 결론을 낸 플레이어 기준입니다. 01-L01~L06은 IL2CPP 플레이어에서 아직 실행하지 않았습니다.

## 2. 메모리와 GC

Boehm GC의 특성, 숨은 할당, 힙 크기와 단편화, Incremental GC.

> 02-L01 숨은 할당 측정

> 02-L02 클로저 할당 시점

> 02-L03 `Instantiate`/`Destroy` vs 풀링

> 02-L04 GC Used vs Reserved

> 02-L05 데이터 배치에 따른 GC 마킹 비용

> 02-L06 Incremental GC on/off 프레임 타임

> 02-L07 `GarbageCollector.Mode.Manual`과 `CollectIncremental`

> 02-L08 Memory Profiler 스냅샷으로 누수 찾기

> 02-L09 파이널라이저와 수거 지연
