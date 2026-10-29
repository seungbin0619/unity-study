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

## 3~6. 예정

3. PlayerLoop (`PlayerLoop` API, 커스텀 시스템 삽입, Awaitable/코루틴 동작 위치)
4. 직렬화와 에셋 파이프라인 (`SerializedObject`, YAML/GUID, `AssetDatabase`, `ScriptedImporter`, Addressables)
5. 렌더링 파이프라인 (SRP, Render Graph, SRP Batcher, GPU Resident Drawer, Frame Debugger)
6. DOTS (Job System, Burst, ECS, 데이터 지향 설계)

새 주제를 시작할 때는 개념 설명 → 실습 목록 제안 → 학습자 동의 후 이 표와 최상위 README에 추가하고, `Assets/Labs/<번호>_<이름>/` 폴더를 만든다.
