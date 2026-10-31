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
- [부분 정답 3/4] (2026-10-31) 01-L08 가상/직접 호출 — A·B·C를 가상, D를 직접으로 예측. 실제로는 IL2CPP가 넷 다 가상 호출을 생성함. D의 추론(메서드 sealed → 구현 확정)은 이론상 옳음. C는 "자식에서 상속 가능"이라는 틀린 이유로 답이 맞음
- [힌트 후 정답] (2026-10-31) 01-L08 `SealedDog d`에 들어올 수 있는 런타임 타입 — "모르겠음" → 힌트(대입하려면 상속해야 함, sealed는 상속 금지) 후 "SealedDog 하나"
- [정답] (2026-10-31) 01-L08 가설 검증 — (E) `callvirt Fox::Speak`(final) 직접 / (F) `callvirt OpenFox::Speak`(final 아님) 가상, 이유 포함 정답. 결론: IL2CPP 6000.3은 IL 호출 대상 메서드가 final인지만 보고, 수신 객체의 정적 타입은 보지 않음
- [오답] (2026-10-31) 01-L09 `Faster (smaller) builds`에서 `Add` 본문 수 — "여전히 값 타입 3개, 참조 타입1개"로 답함(이유 없음). 실제 1벌. full generic sharing이 크기를 런타임 메타데이터에서 읽어 값 타입까지 공유한다는 점을 몰랐음
- [힌트 후 정답] (2026-10-31) 01-L09 기본 설정에서 `List<string>.Add`가 `List<int>.Add`보다 4배 느린 이유 — 1차 "참조 타입은 타입마다 크기가 달라서"(오답: 배열에는 8바이트 포인터가 들어감) → 힌트(`SetAt`의 `Il2CppCodeGenWriteBarrier`, `gcIncremental: 1`) 후 "참조가 중간에 바뀌면 마킹을 다시 해야 해서 배리어" 정답. 배리어는 dirty 표시만 하고 마킹 중이 아니어도 항상 실행된다는 점은 보충함
- [부분 정답 1/2] (2026-10-31) 01-L10 Stripping High에서 리플렉션 대상 — "둘 다 null". B(런타임 조합)는 맞음. A(상수 문자열)는 UnityLinker가 패턴을 알아봐서 타입은 남고, 생성자가 지워져 `MissingMethodException`
- [정답] (2026-10-31) 01-L10 `[Preserve]` 붙인 클래스에서 `Ping`이 지워진 이유 — "각 메서드도 사용 안 되면 삭제해도 된다고 판단". `[Preserve]`는 붙인 대상(타입 + 기본 생성자)에만 적용된다는 점은 보충함
- [부분 정답 8/11] (2026-10-31) 02-L01 숨은 할당 A~K — B의 할당 2회(string + 박싱)까지 맞음. E(static 메서드 그룹)를 X로(실제 매번 델리게이트 128 B, C# 9는 캐시 안 함), H(LINQ `Count(pred)`)를 X로(실제 박싱된 열거자 40 B), K(인자 없는 `params`)를 O로(실제 `Array.Empty` → 0) 틀림. C를 클로저만으로 설명함(델리게이트 128 B가 대부분)
- [부분 정답] (2026-10-31) 02-L01 G에서 할당되는 객체 — F가 0인 이유("`List<T>` 자체 struct `Enumerator`")는 맞음. G를 "yield 상태 할당"으로 답함. 실제로는 같은 struct `Enumerator`가 인터페이스 반환에서 박싱됨 (IL2CPP 생성 코드의 `Box(...)`로 확인)
- [부분 정답] (2026-10-31) 02-L01 추가: `x => IsMatch(x)`(인스턴스 메서드를 감싼 람다)의 호출당 할당 — "`_target` 때문에 1번". 횟수는 맞음. 원인은 필드가 아니라 `this` 캡처(필드를 안 읽는 N도 128 B), 객체는 클로저가 아니라 델리게이트(대상 = `this`) 하나
- [설명 들음] (2026-10-30) 검사 개수(정적)와 실행 횟수(루프 안이면 원소마다) 구분, IL2CPP는 hoist·경계 검사 제거를 하지 않음 (RyuJIT과 대비)

## 약점 (관련 실습에서 재확인)
- 어떤 연산자가 `UnityEngine.Object` 오버로드를 타는지 구분 (`==`·`!=`·`bool`만 탐 / `is null`·`?.`·`??`·`ReferenceEquals`는 안 탐), `==`와 `bool`이 같은 검사라는 점
- 에디터와 플레이어의 차이를 "다른 DLL, 다른 코드"로 떠올리는 것 (Q2에서 힌트 필요) — 에디터 결과를 볼 때마다 "플레이어에서도 같은가?"를 묻기
- 객체 생성 파이프라인 순서: 생성자(필드 초기화) → 역직렬화(값 덮어쓰기) → Awake. "동기 API = 메인 스레드에서 실행"이라고 가정하는 것 (동기 LoadScene도 로딩 스레드에서 역직렬화)
- 성능 이슈를 네이티브 경계 비용 관점으로 설명하는 것 — 01-L05에서 핵심은 설명함(개선). 다음엔 "경계를 넘는 방향과 넘어오는 데이터 종류가 할당을 결정한다"까지 말하는지 확인
- 인터페이스를 거칠 때 struct가 박싱되는 지점 찾기 — 02-L01에서도 재발: G는 맞혔지만 원인을 "yield 상태"로 봤고, 같은 원인인 H(LINQ가 `IEnumerable<T>`로 받음)는 놓침. "API가 매개변수를 인터페이스로 받으면 내부 foreach에서 박싱"을 02-L02 이후 다시 묻기
- 델리게이트 생성 시점: 캐시되는 것(캡처 없는 람다)과 매번 생성되는 것(메서드 그룹(C# 9), 캡처 람다)을 구분, 델리게이트가 클로저보다 훨씬 크다는 점 (02-L01). 추가 질문에서도 "람다가 무엇을 캡처하는가"를 읽은 필드로 판단함 — 인스턴스 멤버를 호출하면 `this` 캡처. 02-L02에서 재확인
- 정적 타입(static type)으로부터 "들어올 수 있는 런타임 타입의 범위"를 추론하는 것 — `sealed class`면 런타임 타입이 하나로 확정된다는 점을 바로 떠올리지 못함 (01-L08, 힌트 필요). 01-L09·인터페이스 호출 관련 실습에서 다시 묻기
- 참조 타입 원소 컬렉션(List<class>)이 GC에게는 "객체 N개 + 참조 N개"라는 점 — 원소 클래스가 pointer-free여도 방문 비용은 남음
- 참조 타입 배열·컬렉션에 저장되는 것은 객체가 아니라 8바이트 포인터라는 점 (01-L09에서 "참조 타입은 크기가 달라서"라고 답함). 02-L05·02-L06에서 다시 묻기
- 런타임에 타입 정보를 읽어 처리하는 방식(full generic sharing, rgctx)의 존재 — "값 타입 = 반드시 전용 코드"로 고정해서 생각함
- 빌드 도구(UnityLinker, IL2CPP)가 하는 분석의 수준을 "전부 아니면 전무"로 가정하는 것 — 01-L10에서 링커가 상수 문자열 리플렉션은 알아본다는 점, 01-L08에서 IL2CPP가 sealed를 쓰지 않는다는 점. 도구가 "무엇을 보고 무엇을 못 보는지"를 단계별로 묻기

## 미응답 질문 (다음 세션 첫 질문으로)
(없음)

## 보류한 작업
- 2026-10-31 `Docs/build-and-environment.md`에 `Labs/Build/IL2CPP (Release)` 메뉴(출력 `Builds/IL2CPP-Release`, 생성 코드 비교용, 프로파일러 카운터 없음)를 추가하기 — 학습자가 "나중에"로 보류. Docs 수정이라 할 때 다시 묻는다

## 실습 기록
(실습별 상세는 각 실습 README. 여기에는 날짜와 링크만)
- 2026-10-29 01-L01 에디터 + Mono 플레이어 실행 — [README](../Assets/Labs/01_NativeManaged/L01_FakeNull/README.md) (IL2CPP 미실행, 예외 타입 차이 원인은 01-L06에서 확인)
- 2026-10-29 01-L02 에디터 + Mono 플레이어 실행 — [README](../Assets/Labs/01_NativeManaged/L02_NewVsAddComponent/README.md)
- 2026-10-29 01-L06 소스 읽기 + 에디터·Mono 플레이어 CoreModule 비교 — [README](../Assets/Labs/01_NativeManaged/L06_ReadBindings/README.md)
- 2026-10-29 01-L03 에디터 + Mono 플레이어 실행 — [README](../Assets/Labs/01_NativeManaged/L03_CtorTiming/README.md)
- 2026-10-29 01-L04 에디터 + Mono 플레이어 실행 — [README](../Assets/Labs/01_NativeManaged/L04_CascadeDestroy/README.md)
- 2026-10-29 01-L05 에디터 + Mono 플레이어 측정 — [README](../Assets/Labs/01_NativeManaged/L05_BoundaryCost/README.md)
- 2026-10-30 01-L07 IL2CPP 플레이어 실행 + 생성 코드 읽기 — [README](../Assets/Labs/01_NativeManaged/L07_NullChecks/README.md)
- 2026-10-31 01-L08 IL2CPP 플레이어 실행 + 생성 코드·IL 읽기 — [README](../Assets/Labs/01_NativeManaged/L08_Sealed/README.md) (예상과 달리 devirtualization 없음 → 같은 날 가설 검증: IL 호출 대상이 final일 때만 직접 호출. 2016년 블로그 예제를 그대로 옮겨도 재현 안 됨. 범위: 6000.3 Development·Release 빌드, Windows x64)
- 2026-10-31 01-L09 IL2CPP 플레이어 실행 (Code Generation 두 설정) + 생성 코드 읽기 — [README](../Assets/Labs/01_NativeManaged/L09_GenericSharing/README.md) (같은 날 학습자 요청으로 Incremental GC On/Off 비교: string−int 차이 약 6 ns 중 약 90%가 쓰기 배리어. 배리어 포함 여부는 빌드 시점 define `IL2CPP_ENABLE_WRITE_BARRIERS`로 결정)
- 2026-10-31 01-L10 에디터 + IL2CPP 플레이어(Stripping High) 실행 + 생성 코드 확인 — [README](../Assets/Labs/01_NativeManaged/L10_Stripping/README.md) (1장 실습 모두 완료)
- 2026-10-31 02-L01 에디터(참고용) + Mono·IL2CPP 플레이어 측정 + 생성 코드 확인 — [README](../Assets/Labs/02_GC/L01_HiddenAlloc/README.md)

## Player Settings 변경 이력
(변경 시 날짜, 항목, 원래 값 → 바꾼 값, 복원 여부)
- 2026-10-29 Run In Background: `false` → `true`, **복원 안 함 (학습자 요청으로 상시 유지)**. 에디터가 포커스를 잃으면 플레이 모드가 멈춰 실습이 매우 오래 걸리던 문제 해결용. 플레이어 빌드에도 적용되지만 측정 기준(Backend, Incremental GC, Stripping)과 무관하고, 플레이어는 원래 `-batchmode`로 실행함
- 2026-10-30 Scripting Backend: Mono → IL2CPP (01-L07 빌드 동안만), **복원됨** (`LabBuilder`가 빌드 후 Mono로 되돌림)
- 2026-10-30 Scripting Backend: Mono → IL2CPP (01-L07 (E) 추가 빌드 동안만), **복원됨** — 수정한 `LabBuilder`가 디스크에도 `Standalone: 0`으로 저장함을 확인
- 2026-10-31 Scripting Backend: Mono → IL2CPP (01-L08 빌드 동안만), **복원됨** (`Standalone: 0` 확인)
- 2026-10-31 Scripting Backend: Mono → IL2CPP (01-L08 가설 검증 빌드 동안만), **복원됨** (`Standalone: 0` 확인)
- 2026-10-31 Scripting Backend: Mono → IL2CPP (01-L08 블로그 조건 재현 빌드 동안만), **복원됨** (`Standalone: 0` 확인)
- 2026-10-31 Scripting Backend: Mono → IL2CPP (01-L08 Release 빌드 동안만), **복원됨** (`Standalone: 0` 확인)
- 2026-10-31 Scripting Backend: Mono → IL2CPP (01-L09 기본 설정 빌드 동안만), **복원됨** (`Standalone: 0` 확인)
- 2026-10-31 IL2CPP Code Generation: `OptimizeSpeed`(Faster runtime) → `OptimizeSize`(Faster (smaller) builds) (01-L09 비교 빌드 동안만), **복원됨** (`il2cppCodeGeneration: Standalone: 0`. 원래 `{}`였으나 값은 같은 OptimizeSpeed). 이 비교 빌드에서도 Backend는 `LabBuilder`가 복원함 (`Standalone: 0` 확인)
- 2026-10-31 Incremental GC: `On` → `Off` (01-L09 쓰기 배리어 비용 검증 빌드 동안만), **복원됨** (`gcIncremental: 1` 확인, 복원 후 On 빌드로 재측정)
- 2026-10-31 Managed Stripping Level: 기본값(`managedStrippingLevel: {}`, API로는 `Disabled`) → `High` (01-L10 빌드 동안만), **복원됨** (`Disabled`로 되돌림. 파일에는 `{}` 대신 `Standalone: 0`으로 남음, 값은 같음). Backend도 `Standalone: 0` 확인
- 2026-10-31 Scripting Backend: Mono → IL2CPP (02-L01 빌드 동안만), **복원됨** (`Standalone: 0` 확인)
- 2026-10-31 Scripting Backend: Mono → IL2CPP (02-L01 추가 케이스 L~P 빌드 동안만), **복원됨** (`Standalone: 0` 확인)
