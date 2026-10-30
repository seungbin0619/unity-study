# Unity Engine Internals Labs

Unity 엔진 내부 동작을 문서로만 읽지 않고, **직접 측정해서 확인하는** 실습 모음입니다.
각 실습은 "실행 전 예측 → 측정 → 예측과 비교 → 해석" 순서로 진행하며, 결과는 실습별 README에 기록합니다.

## 환경

- Unity 6 (정확한 버전: 6000.3.25f1)
- 플랫폼: Windows 11, 빌드 타깃 Windows Standalone (x64)
- 측정 기준: 결론은 Development Build 플레이어 기준. 에디터 측정은 참고용.

## 실행 방법

- 에디터: 메뉴 `Labs/<주제>/<실습>`
- 플레이어: 전체 실습이 포함된 빌드를 실행하며 `-lab <실습 ID>` 인자로 선택
  ```
  Labs.exe -lab 02-L05 -logFile Notes/results/player_02-L05.log
  ```
- 결과 원본(CSV)은 `Notes/results/`, 실습별 요약과 해석은 각 실습 폴더의 README에 있습니다.

## 1. 네이티브–매니지드 경계

C# 래퍼와 C++ 네이티브 객체의 관계, 경계를 넘는 비용, IL2CPP가 만드는 코드.

| ID | 실습 | 상태 | 핵심 결론 | 링크 |
|---|---|---|---|---|
| 01-L01 | 가짜 null과 `==` / `is null` / `?.` / `??` | 완료 (Mono, IL2CPP 미실행) | `Destroy` 다음 프레임에 `==`·`bool`만 null로 보고, `is null`·`?.`·`??`는 래퍼를 그대로 통과시켜 예외 발생. 예외 타입은 에디터 `MissingReferenceException`, Mono 플레이어 `NullReferenceException` | [README](Assets/Labs/01_NativeManaged/L01_FakeNull/README.md) |
| 01-L02 | `new MonoBehaviour()` vs `AddComponent` | 완료 (Mono, IL2CPP 미실행) | `new`로 만든 컴포넌트는 `== null`이 `true`인 래퍼만 남음. `'new' keyword` 경고는 에디터에서만 출력되고 플레이어에서는 없음 | [README](Assets/Labs/01_NativeManaged/L02_NewVsAddComponent/README.md) |
| 01-L03 | MonoBehaviour 생성자 호출 시점·스레드 | 완료 (Mono, IL2CPP 미실행) | 생성자는 경로마다 1번, 항상 `Awake`보다 먼저. `AddComponent`·`Instantiate`는 메인 스레드, 씬 로드는 **동기 `LoadScene`도** 로딩 스레드에서 생성자 호출 | [README](Assets/Labs/01_NativeManaged/L03_CtorTiming/README.md) |
| 01-L04 | 연쇄 파괴와 이벤트 구독 누수 | 완료 (Mono, IL2CPP 미실행) | 부모 `Destroy` 시 자식 컴포넌트도 다음 프레임부터 `== null`. 정적 이벤트 구독을 해제하지 않으면 파괴된 래퍼가 GC 후에도 살아남고(Leaked Managed Shell), 이벤트 발생 시 Unity API 접근에서 예외 | [README](Assets/Labs/01_NativeManaged/L04_CascadeDestroy/README.md) |
| 01-L05 | 경계 비용: 캐싱, 배열 반환 API, `CompareTag` | 완료 (Mono, IL2CPP 미실행) | 플레이어 호출당: `GetComponent` 20 ns·0 B vs 캐싱 0.8 ns, `mesh.vertices` 12 KB 할당 vs `GetVertices` 0 B, `tag ==` 34 B vs `CompareTag` 0 B. 없는 컴포넌트 조회는 에디터에서만 약 600 B 할당(가짜 null)이고 `is null` 결과도 에디터·플레이어가 다름 | [README](Assets/Labs/01_NativeManaged/L05_BoundaryCost/README.md) |
| 01-L06 | UnityCsReference 바인딩 코드 읽기 | 완료 (Mono, IL2CPP 미실행) | `MissingReferenceException`은 instance ID ≠ 0일 때 에디터에서만 만들어짐. 플레이어 CoreModule의 `TryThrowEditorNullExceptionObject`는 IL 2바이트짜리 빈 메서드(에디터 234바이트) | [README](Assets/Labs/01_NativeManaged/L06_ReadBindings/README.md) |
| 01-L07 | IL2CPP null·경계 검사와 `Il2CppSetOption` | 완료 (IL2CPP) | 역참조마다 `NullCheck` 1개(`ReadChain` 3개, `SumArray` 루프 안 2개). `this`는 호출하는 쪽에서 검사하므로 0개. 배열 경계 검사는 `GetAt()` 안에 숨어 있음. 검사를 끄면 루프가 원소당 0.48 → 0.17 ns (약 2.9배) | [README](Assets/Labs/01_NativeManaged/L07_NullChecks/README.md) |
| 01-L08 | `sealed`와 devirtualization | 완료 (IL2CPP) | C# 컴파일러는 정적 타입과 관계없이 `callvirt Animal::Speak`를 냄. IL2CPP 6000.3은 `sealed class`·`sealed override` 모두 devirtualize하지 않고 `VirtualFuncInvoker`를 생성했으며, 호출 시간도 네 경우 모두 약 2.1 ns로 같음 (이론상 C·D는 직접 호출 가능) | [README](Assets/Labs/01_NativeManaged/L08_Sealed/README.md) |
| 01-L09 | 제네릭 인스턴스 생성과 full generic sharing | 대기 | | |
| 01-L10 | 코드 스트리핑과 `[Preserve]` / `link.xml` | 대기 | | |

## 2. 메모리와 GC

Boehm GC의 특성, 숨은 할당, 힙 크기와 단편화, Incremental GC.

| ID | 실습 | 상태 | 핵심 결론 | 링크 |
|---|---|---|---|---|
| 02-L01 | 숨은 할당 측정 | 대기 | | |
| 02-L02 | 클로저 할당 시점 | 대기 | | |
| 02-L03 | Instantiate/Destroy vs 풀링 | 대기 | | |
| 02-L04 | GC Used vs Reserved | 대기 | | |
| 02-L05 | 데이터 배치에 따른 GC 마킹 비용 | 대기 | | |
| 02-L06 | Incremental GC on/off 프레임 타임 | 대기 | | |
| 02-L07 | `GarbageCollector.Mode.Manual`과 `CollectIncremental` | 대기 | | |
| 02-L08 | Memory Profiler 스냅샷으로 누수 찾기 | 대기 | | |
| 02-L09 | 파이널라이저와 수거 지연 | 대기 | | |

## 예정

3. PlayerLoop · 4. 직렬화와 에셋 파이프라인 · 5. 렌더링 파이프라인 · 6. DOTS
