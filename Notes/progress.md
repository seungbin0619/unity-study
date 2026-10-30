# 진행 기록

## 완료한 개념
- 1. 네이티브–매니지드 경계: 래퍼 구조, 가짜 null, 생성자 금지·Destroy 지연·연쇄 파괴, 마샬링 비용, IL2CPP 파이프라인·생성 코드·sealed·제네릭 공유·스트리핑
- 2. 메모리와 GC: 메모리 영역, Boehm GC 특성, 숨은 할당, 측정·감소 기법, 블록/크기 클래스, 정밀·보수적 마킹, pointer-free 객체, Used/Reserved, Incremental GC와 쓰기 배리어, 파이널라이저

## 퀴즈 기록
- [정답] Destroy 후 같은 프레임/다음 프레임/is null 예측
- [정답] 값 타입 3 + 참조 타입 공유 1 = List<T>.Add 4벌
- [오답→풀이] GetComponent 캐싱을 "경계 넘기" 관점으로 설명하기
- [부분 정답] 매 프레임 할당 줄 고르기 — 인터페이스 타입 foreach의 열거자 박싱(F)을 놓침
- [부분 정답] (2026-10-29) 10만 개 GC 마킹 순서 — 답: 2<1<3, 1≈2. 설계3이 참조 때문에 스캔된다는 이유는 맞음. 설계1도 객체 10만 개 방문 + 참조 배열 스캔이라 2와 비슷하지 않다는 점을 놓침 (정답 2 < 3 ≤ 1, 02-L05로 실측 예정)
- [부분 정답 7/12] (2026-10-29) 01-L01 null 연산자 6개 × 2프레임 — `==`/`is null`/`ReferenceEquals`는 맞음. `(bool)go`(A)와 `?.`/`??`(A·B)를 틀림: `?.`/`??`를 Unity 오버로드를 타는 쪽으로 분류함
- [정답 (에디터 기준)] (2026-10-29) 01-L02 `new` vs `AddComponent` — 4문항 모두 맞음. 01-L01 약점(연산자 그룹)을 바로 적용함. 경고가 플레이어에서 안 나온다는 점은 고려하지 않음
- [힌트 후 정답] (2026-10-29) 01-L06 Q1 에디터 예외 분기 조건 — 1차에 네이티브 포인터로 답하고 비교 대상도 `new` vs `AddComponent`로 잘못 잡음. 힌트 후 `m_InstanceID != 0` 정답
- [힌트 후 정답] (2026-10-29) 01-L06 Q2 플레이어에서 다른 이유 — "모르겠음" → 힌트(DLL이 다름, 함수 이름) 후 "플레이어 DLL에서 기능 제거" 정답
- [부분 정답] (2026-10-29) 01-L03 생성자 횟수·스레드·순서 — 횟수 4/4, 스레드 3/4(동기 `LoadScene`을 메인으로 예측), 순서 1/4(2~4를 "Awake 이후"로 예측 — 생성자는 항상 Awake보다 먼저이고 사이에 역직렬화가 낀다)
- [정답] (2026-10-29) 01-L04 연쇄 파괴·구독 누수 — 4문항 모두 맞음. 에디터/플레이어 예외 타입 차이를 스스로 적용함 (01-L01·01-L06 약점 회복 신호)
- [부분 정답 5/7] (2026-10-29) 01-L05 경계 비용 할당·속도 — 1a를 "수십 B"로 예측(실제 0), 1c의 에디터 전용 할당을 놓침. 나머지 맞음
- [정답] (2026-10-29) 약점 재확인: `GetComponent` vs 캐싱을 경계 관점으로 설명 — "경계를 넘어 컴포넌트 목록에서 타입 비교 후 반환 vs 단순 필드 읽기". 타입 변환·래퍼 반환 단계는 보충함

- [정답] (2026-10-30) 01-L07 `NullCheck` 개수 (A 3 / B 2 / C 0) — 개수는 모두 맞음. (C)의 이유를 "참조 x"로 답했으나(`this`도 역참조), 후속 질문에서 "this가 null이면 호출 자체가 안 됨"으로 정답
- [정답] (2026-10-30) 01-L07 추가: 직접 `if (a == null)`을 넣어도 `NullCheck` 2개가 남는다 — IL2CPP는 명령어 단위로 변환하고 앞의 검사 결과를 전달하지 않음. 측정에서도 E ≈ B ≫ D
- [설명 들음] (2026-10-30) 검사 개수(정적)와 실행 횟수(루프 안이면 원소마다) 구분, IL2CPP는 hoist·경계 검사 제거를 하지 않음 (RyuJIT과 대비)

## 약점 (관련 실습에서 재확인)
- 어떤 연산자가 `UnityEngine.Object` 오버로드를 타는지 구분 (`==`·`!=`·`bool`만 탐 / `is null`·`?.`·`??`·`ReferenceEquals`는 안 탐), `==`와 `bool`이 같은 검사라는 점
- 에디터와 플레이어의 차이를 "다른 DLL, 다른 코드"로 떠올리는 것 (Q2에서 힌트 필요) — 에디터 결과를 볼 때마다 "플레이어에서도 같은가?"를 묻기
- 객체 생성 파이프라인 순서: 생성자(필드 초기화) → 역직렬화(값 덮어쓰기) → Awake. "동기 API = 메인 스레드에서 실행"이라고 가정하는 것 (동기 LoadScene도 로딩 스레드에서 역직렬화)
- 성능 이슈를 네이티브 경계 비용 관점으로 설명하는 것 — 01-L05에서 핵심은 설명함(개선). 다음엔 "경계를 넘는 방향과 넘어오는 데이터 종류가 할당을 결정한다"까지 말하는지 확인
- 인터페이스를 거칠 때 struct가 박싱되는 지점 찾기
- 참조 타입 원소 컬렉션(List<class>)이 GC에게는 "객체 N개 + 참조 N개"라는 점 — 원소 클래스가 pointer-free여도 방문 비용은 남음

## 미응답 질문 (다음 세션 첫 질문으로)
(없음)

## 실습 기록
(실습별 상세는 각 실습 README. 여기에는 날짜와 링크만)
- 2026-10-29 01-L01 에디터 + Mono 플레이어 실행 — [README](../Assets/Labs/01_NativeManaged/L01_FakeNull/README.md) (IL2CPP 미실행, 예외 타입 차이 원인은 01-L06에서 확인)
- 2026-10-29 01-L02 에디터 + Mono 플레이어 실행 — [README](../Assets/Labs/01_NativeManaged/L02_NewVsAddComponent/README.md)
- 2026-10-29 01-L06 소스 읽기 + 에디터·Mono 플레이어 CoreModule 비교 — [README](../Assets/Labs/01_NativeManaged/L06_ReadBindings/README.md)
- 2026-10-29 01-L03 에디터 + Mono 플레이어 실행 — [README](../Assets/Labs/01_NativeManaged/L03_CtorTiming/README.md)
- 2026-10-29 01-L04 에디터 + Mono 플레이어 실행 — [README](../Assets/Labs/01_NativeManaged/L04_CascadeDestroy/README.md)
- 2026-10-29 01-L05 에디터 + Mono 플레이어 측정 — [README](../Assets/Labs/01_NativeManaged/L05_BoundaryCost/README.md)
- 2026-10-30 01-L07 IL2CPP 플레이어 실행 + 생성 코드 읽기 — [README](../Assets/Labs/01_NativeManaged/L07_NullChecks/README.md)

## Player Settings 변경 이력
(변경 시 날짜, 항목, 원래 값 → 바꾼 값, 복원 여부)
- 2026-10-29 Run In Background: `false` → `true`, **복원 안 함 (학습자 요청으로 상시 유지)**. 에디터가 포커스를 잃으면 플레이 모드가 멈춰 실습이 매우 오래 걸리던 문제 해결용. 플레이어 빌드에도 적용되지만 측정 기준(Backend, Incremental GC, Stripping)과 무관하고, 플레이어는 원래 `-batchmode`로 실행함
- 2026-10-30 Scripting Backend: Mono → IL2CPP (01-L07 빌드 동안만), **복원됨** (`LabBuilder`가 빌드 후 Mono로 되돌림)
- 2026-10-30 Scripting Backend: Mono → IL2CPP (01-L07 (E) 추가 빌드 동안만), **복원됨** — 수정한 `LabBuilder`가 디스크에도 `Standalone: 0`으로 저장함을 확인
