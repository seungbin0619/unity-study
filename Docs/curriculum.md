# 커리큘럼과 실습 계획

아래 표의 "실습" 열은 **구현 설계 메모**(무엇을 비교할지)다. 짧은 실습명과 상태·결론의 원본은 최상위 `README.md`다. 실습을 추가하면 두 곳 모두에 행을 넣되, 진행 상태는 README에서만 관리한다.

예측 질문의 정답은 [answer-keys.md](answer-keys.md)에 있다.

## 1. 네이티브–매니지드 경계 — 개념 완료, 실습 대기

| ID | 실습 | 예측 질문 예시 |
|---|---|---|
| 01-L01 | 가짜 null: `Destroy` 직후/다음 프레임에 `==`, `is null`, `?.`, `??` 결과 | 각 줄 true/false? |
| 01-L02 | `new MyBehaviour()` vs `AddComponent`, `ReferenceEquals` | `new`로 만든 객체의 `== null`은? |
| 01-L03 | MonoBehaviour 생성자 호출 시점·스레드 (씬 로드, 스크립트 리로드, 플레이 진입) | 몇 번, 어느 스레드에서? |
| 01-L04 | 부모 파괴로 인한 컴포넌트 연쇄 파괴 + 이벤트 구독 미해제 → MissingReferenceException, Leaked Managed Shell | 캐싱한 컴포넌트는 언제 null이 되나? |
| 01-L05 | 경계 비용: `GetComponent` 캐싱 유무, `mesh.vertices` vs `GetVertices(list)`, `.tag ==` vs `CompareTag` | 어느 쪽이 매 프레임 할당? |
| 01-L06 | UnityCsReference 읽기: `UnityEngineObject.bindings.cs`의 `==`, `Transform.bindings.cs`의 `extern` 멤버 구분 | `==`는 무엇을 검사하나? |
| 01-L07 | IL2CPP 출력에서 `NullCheck` 세기, `Il2CppSetOption` 전후 비교 | 검사 몇 개가 박힐까? |
| 01-L08 | `sealed` 유무에 따른 호출 코드 차이 | 어느 쪽이 직접 호출이 될까? |
| 01-L09 | 제네릭 인스턴스: `List<int/float/string/GameObject/Vector3>`의 `Add` 생성 코드 수, full generic sharing 설정 비교 | 몇 벌 생성? |
| 01-L10 | Stripping High + 리플렉션 전용 클래스 → 빌드에서만 `MissingMethodException` → `[Preserve]` / `link.xml`로 수정 | 에디터와 빌드 결과 차이? |

## 2. 메모리와 GC — 개념 완료(심화 포함), 실습 대기

| ID | 실습 | 예측 질문 예시 |
|---|---|---|
| 02-L01 | 숨은 할당 측정: 박싱, 문자열 보간, 클로저, 메서드 그룹 → 델리게이트, 인터페이스 foreach, LINQ, `new WaitForSeconds`, `params` | 어느 줄이 할당? 몇 바이트? |
| 02-L02 | 클로저 할당 시점: 람다가 실행되지 않는 `if` 분기에 있어도 할당되는가 | 할당이 생길까? |
| 02-L03 | `Instantiate/Destroy` 반복 vs 풀링 — 빌드에서 GC 스파이크 비교 | 스파이크 주기와 크기? |
| 02-L04 | 50MB `byte[]` 할당 후 해제 — GC Used vs Reserved 변화 | Reserved는 줄어드나? |
| 02-L05 | 10만 개 데이터: `List<class>` vs `struct[]` vs 참조 필드 포함 `struct[]` — `GC.Collect()` 시간 | 마킹 비용 순서? |
| 02-L06 | Incremental GC on/off — 매 프레임 1MB 할당 시 프레임 타임 그래프 | 그래프 모양이 어떻게 달라지나? |
| 02-L07 | `GarbageCollector.Mode.Manual` + `CollectIncremental(ns)` 패턴 | 슬라이스를 줄이면? |
| 02-L08 | Memory Profiler 스냅샷 비교로 누수 찾기 (Claude는 절차 안내, GUI 조작은 학습자) | 무엇이 남아 있을까? |
| 02-L09 | 파이널라이저가 있는 객체의 수거 지연 (GC 사이클 수 확인) | 몇 번째 GC에서 해제? |

## 3. PlayerLoop — 실습 목록 확정, 개념 대기

| ID | 실습 | 예측 질문 예시 |
|---|---|---|
| 03-L01 | `PlayerLoop.GetCurrentPlayerLoop()` 트리 덤프 — 에디터 vs Mono·IL2CPP 플레이어 (최상위 단계, 하위 시스템 수·이름) | 최상위 단계 몇 개, `Update`는 몇 번째? 에디터와 플레이어의 시스템 수가 같을까? |
| 03-L02 | 각 하위 시스템 앞뒤에 마커 시스템을 넣어 콜백·재개 위치를 순번으로 지도화: `Update`/`LateUpdate`/`FixedUpdate`, 코루틴 `yield null`·`WaitForEndOfFrame`·`WaitForFixedUpdate`, `Awaitable.NextFrameAsync`·`EndOfFrameAsync`·`FixedUpdateAsync`, `await Task.Yield()` | 각 재개 지점은 어느 하위 시스템 안에서 실행될까? |
| 03-L03 | `Instantiate` 호출 시점(`Awake`·`Update`·`LateUpdate`·코루틴)별로 새 객체의 `Awake`/`OnEnable`/`Start`/첫 `Update`가 오는 시점 | `Update`에서 만든 객체의 `Start`는 같은 프레임? 다음 프레임? |
| 03-L04 | `FixedUpdate` 따라잡기: `Thread.Sleep`으로 프레임을 늘릴 때 프레임당 `FixedUpdate` 횟수, `Time.deltaTime`, `maximumDeltaTime` 효과 | `fixedDeltaTime` 0.02, 프레임 100 ms·500 ms면 프레임당 몇 번? |
| 03-L05 | `MonoBehaviour.Update` 1만 개 vs 매니저 하나가 1만 개 루프 vs 커스텀 PlayerLoop 시스템 하나, 빈 `Update`·`Update` 미정의 포함 (Mono·IL2CPP) | 순서와 대략적 배수? 빈 `Update`도 비용이 드나? |
| 03-L06 | 대기 방식별 반복당 할당: `yield return null`·`0`·`new WaitForSeconds`·캐시한 `WaitForSeconds`, `StartCoroutine` 자체, `async Awaitable` vs `async Task` | 어느 줄이 반복마다 할당? 몇 바이트? |
| 03-L07 | 스레드 전환: `Awaitable.BackgroundThreadAsync`→`MainThreadAsync`, `Task.Run` 후 continuation, `ConfigureAwait(false)` — 스레드 ID, 재개 프레임, 재개 PlayerLoop 단계 | `await Task.Run(...)` 다음 줄은 어느 스레드에서, 몇 프레임 뒤에? |
| 03-L08 | 수명과 취소: `SetActive(false)`·`enabled = false`·`Destroy` 후 코루틴 / `Awaitable` / `Task`가 계속 도는지, `destroyCancellationToken`, 파괴된 객체 접근 예외 | `Destroy` 후에도 계속 도는 것은? |
| 03-L09 | 커스텀 시스템 수명 (에디터): 도메인 리로드 끈 Enter Play Mode Options에서 재진입 시 시스템 누적, `SubsystemRegistration`·`playModeStateChanged`로 정리 | 정리 없이 3번 재진입하면 시스템은 몇 개? |
| 03-L10 | 편집 모드 루프 (에디터, 툴): `[ExecuteAlways]` `Update` 호출 빈도, `EditorApplication.update`, `QueuePlayerLoopUpdate`, 입력 유무 비교 | 편집 모드에서 아무것도 안 하면 `Update`는 초당 몇 번? |

- 약점 재확인: 03-L01 에디터/플레이어 DLL 차이, 03-L06 박싱(`yield return 0`)·델리게이트, 03-L07 "동기 API = 메인 스레드" 가정, 03-L08 가짜 null·예외 타입 차이.
- 03-L09·L10은 에디터 기준으로 결론을 낸다 (README에 명시).
- Player Settings: 03-L05·L06의 IL2CPP 빌드에서만 Backend를 바꾸고 복원한다.
- 확인 필요: `Task` continuation(`UnitySynchronizationContext`)의 실행 하위 시스템 — 소스를 미리 보지 않고 03-L02 마커 실측으로 정한다.

## 4\~6. 예정

4. 직렬화와 에셋 파이프라인 (`SerializedObject`, YAML/GUID, `AssetDatabase`, `ScriptedImporter`, Addressables)
5. 렌더링 파이프라인 (SRP, Render Graph, SRP Batcher, GPU Resident Drawer, Frame Debugger)
6. DOTS (Job System, Burst, ECS, 데이터 지향 설계)

새 주제를 시작할 때는 개념 설명 → 실습 목록 제안 → 학습자 동의 후 이 표와 최상위 README에 추가하고, `Assets/Labs/<번호>_<이름>/` 폴더를 만든다.
