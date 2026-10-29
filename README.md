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
| 01-L03 | MonoBehaviour 생성자 호출 시점·스레드 | 대기 | | |
| 01-L04 | 연쇄 파괴와 이벤트 구독 누수 | 대기 | | |
| 01-L05 | 경계 비용: 캐싱, 배열 반환 API, `CompareTag` | 대기 | | |
| 01-L06 | UnityCsReference 바인딩 코드 읽기 | 대기 | | |
| 01-L07 | IL2CPP null·경계 검사와 `Il2CppSetOption` | 대기 | | |
| 01-L08 | `sealed`와 devirtualization | 대기 | | |
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
