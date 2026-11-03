# 용어집

장별 실습에서 쓴 용어 모음. 각 항목은 "뜻 → 헷갈리기 쉬운 것 → 확인한 실습" 순서다.
새 장을 시작하면 그 장의 용어를 아래에 추가한다.

## 0. 변수의 종류 (가장 먼저)

C# 공식 용어는 **필드(field)**다. "인스턴스 변수", "클래스 변수"는 Java·일반 OOP에서 쓰는 말이고 C#에서는 각각 **인스턴스 필드**, **정적 필드**에 해당한다.

```csharp
class Enemy
{
    static int s_count;          // 정적 필드 (static field)   = "클래스 변수"
    int _hp;                     // 인스턴스 필드 (instance field) = "인스턴스 변수", "멤버 변수"

    void Hit(int damage)         // damage: 매개변수 (parameter)
    {
        int left = _hp - damage; // left: 지역 변수 (local variable)
        _hp = left;              // _hp는 사실 this._hp — this를 거쳐 읽는다
    }
}
```

| 종류 | 몇 개 존재하나 | 수명 | 값이 사는 곳 | GC 관점 |
|---|---|---|---|---|
| 지역 변수 (local variable) | 메서드 호출마다 1개 | 메서드가 끝날 때까지 | 스택(stack) 또는 레지스터 | 실행 중인 스택은 GC 루트 |
| 매개변수 (parameter) | 호출마다 1개 | 메서드가 끝날 때까지 | 지역 변수와 같음 | 지역 변수와 같음 |
| 인스턴스 필드 (instance field) | **객체마다** 1개 | 그 객체가 살아 있는 동안 | 힙(heap)의 객체 안 | 객체가 도달 가능하면 같이 살아 있음 |
| 정적 필드 (static field) | **타입마다** 1개 | 프로그램(도메인)이 끝날 때까지 | 타입별 정적 저장소 | GC 루트 — 여기서 가리키는 건 안 죽는다 |
| 캡처된 지역 변수·매개변수 | 스코프 진입마다 1개 | 클로저 객체가 살아 있는 동안 | **힙의 클로저 객체 필드**로 옮겨짐 | 클로저 객체를 따라감 |

- 참조 타입 지역 변수 `var e = new Enemy();`에서 스택에 있는 건 **참조(8 B 포인터)**이고, 객체 본체는 힙에 있다.
- 인스턴스 필드를 읽는 코드는 항상 `this`를 거친다. 그래서 람다 안에서 `_hp`를 쓰면 람다는 `_hp`가 아니라 **`this`를 캡처**한다 ([02-L01](../Assets/Labs/02_GC/L01_HiddenAlloc/README.md), [02-L02](../Assets/Labs/02_GC/L02_ClosureTiming/README.md)).
- 정적 필드에 이벤트 구독이나 리스트를 두면 거기 담긴 객체는 씬이 바뀌어도 회수되지 않는다. 대표적인 매니지드 누수 경로다.

## 1. 타입과 객체

| 용어 | 뜻 | 헷갈리기 쉬운 것 · 실습 |
|---|---|---|
| 클래스 / 객체 / 인스턴스 (class / object / instance) | 클래스는 설계도(타입), 객체와 인스턴스는 `new`로 만든 실체. 객체 = 인스턴스 | "클래스를 할당한다"가 아니라 "인스턴스를 할당한다" |
| 값 타입 (value type) | `struct`, `int`, `enum`. 변수나 배열 칸에 **값 자체**가 들어감 | 값 타입이라도 클래스의 필드면 힙에 있다(객체 안). "값 타입 = 스택"은 틀린 말 |
| 참조 타입 (reference type) | `class`, `string`, 배열, 델리게이트. 변수나 칸에는 **참조**만 들어가고 본체는 힙 | `List<class>`의 배열 칸은 8 B 포인터, 객체는 따로 ([01-L09](../Assets/Labs/01_NativeManaged/L09_GenericSharing/README.md), [02-L05](../Assets/Labs/02_GC/L05_MarkCost/README.md)) |
| 참조 (reference) / 포인터 (pointer) | 참조는 C#에서 객체를 가리키는 값, 런타임에서는 주소(64비트에서 8 B)로 구현됨 | GC가 "참조를 따라간다" = 이 주소를 읽고 그 객체를 방문한다 |
| 정적 타입 (static type) / 런타임 타입 (runtime type) | 정적 타입은 변수 선언에 적힌 타입(컴파일 시점), 런타임 타입은 실제로 들어 있는 객체의 타입 | `Animal a = new Dog();`: 정적 타입 `Animal`, 런타임 타입 `Dog`. "정적 타입"의 static은 `static` 키워드와 무관 ([01-L08](../Assets/Labs/01_NativeManaged/L08_Sealed/README.md)) |
| `sealed` | 상속 금지. 정적 타입이 sealed면 런타임 타입이 하나로 확정됨 | IL2CPP 6000.3은 이걸 써서 호출을 직접 호출로 바꾸지 않았음 ([01-L08](../Assets/Labs/01_NativeManaged/L08_Sealed/README.md)) |
| 가상 호출 (virtual call) | 런타임 타입의 메서드 테이블(vtable)에서 호출 대상을 찾는 호출 | 직접 호출보다 간접 점프 한 번만큼 비쌈 |
| 탈가상화 (devirtualization) | 런타임 타입이 확정될 때 가상 호출을 직접 호출로 바꾸는 최적화 | IL2CPP는 IL 호출 대상 메서드가 `final`일 때만 함 ([01-L08](../Assets/Labs/01_NativeManaged/L08_Sealed/README.md)) |
| 박싱 / 언박싱 (boxing / unboxing) | 값 타입을 `object`나 인터페이스로 다룰 때 힙에 상자 객체를 만들어 값을 복사 / 꺼내기 | **매개변수가 인터페이스**면 struct를 넘기는 순간 박싱. `IEnumerable<T>` foreach, LINQ, `Sort(IComparer<T>)` ([02-L01](../Assets/Labs/02_GC/L01_HiddenAlloc/README.md)) |
| 제네릭 제약 (generic constraint) | `where T : IFoo`처럼 타입 매개변수에 거는 조건 | `void F<T>(T x) where T : IFoo`는 박싱 없음, `void F(IFoo x)`는 박싱 ([02-L01](../Assets/Labs/02_GC/L01_HiddenAlloc/README.md)) |

## 2. 델리게이트·람다·열거자

| 용어 | 뜻 | 헷갈리기 쉬운 것 · 실습 |
|---|---|---|
| 델리게이트 (delegate) | 호출할 메서드를 담는 **객체**. 내부에 대상(target, 인스턴스 메서드면 그 객체)과 메서드 포인터를 가짐 | 클로저보다 훨씬 큼: 델리게이트 128 B, 클로저 20\~24 B (IL2CPP·Mono 실측) ([02-L01](../Assets/Labs/02_GC/L01_HiddenAlloc/README.md)) |
| 메서드 그룹 (method group) | `list.Find(IsMatch)`처럼 괄호 없이 쓴 메서드 이름. 델리게이트로 변환됨 | C# 9(Unity 6)는 static 메서드 그룹도 캐시하지 않아 쓸 때마다 델리게이트 할당 |
| 람다 (lambda) | `x => x > 3` 같은 익명 함수. 컴파일러가 숨은 메서드로 바꿈 | 아무것도 캡처하지 않는 람다는 델리게이트가 캐시돼 첫 호출 뒤 0 B |
| 캡처 (capture) | 람다가 바깥 지역 변수·매개변수·`this`를 쓰는 것 | 인스턴스 필드를 쓰면 필드가 아니라 `this`를 캡처 |
| 클로저 (closure) | 캡처된 변수를 담기 위해 컴파일러가 만드는 힙 객체(디스플레이 클래스 `<>c__DisplayClass`의 인스턴스) | 클로저는 **캡처 변수의 스코프 진입 시** 생성, 델리게이트는 **람다 줄 실행 시** 생성. 람다 분기를 안 타도 클로저는 생길 수 있음 ([02-L02](../Assets/Labs/02_GC/L02_ClosureTiming/README.md)) |
| 로워링 (lowering) | 컴파일러가 고수준 문법(람다, `foreach`, `yield`, `async`)을 더 단순한 C#·IL로 바꾸는 과정 | 클로저 할당 위치를 예측하려면 로워링 결과를 머릿속으로 그려야 함 |
| 열거자 (enumerator) | `foreach`가 쓰는 `MoveNext()`/`Current` 객체 | `List<T>`의 열거자는 struct라 직접 `foreach`하면 0 B. `IEnumerable<T>`로 받으면 박싱되어 40 B ([02-L01](../Assets/Labs/02_GC/L01_HiddenAlloc/README.md)) |
| 이터레이터 (iterator) | `yield return`으로 만든 메서드. 컴파일러가 상태 머신 클래스를 생성 | 호출할 때마다 상태 머신 객체 할당. struct 열거자 박싱과는 다른 원인 |

## 3. 네이티브–매니지드 경계 (1장)

| 용어 | 뜻 | 헷갈리기 쉬운 것 · 실습 |
|---|---|---|
| 매니지드 (managed) / 네이티브 (native) | 매니지드는 C# 런타임과 GC가 관리하는 쪽, 네이티브는 C++ 엔진이 직접 관리하는 메모리·코드 | Unity 객체의 본체(메시, 텍스처, Transform)는 네이티브. GC 힙에 잡히는 건 래퍼뿐 ([02-L03](../Assets/Labs/02_GC/L03_Pooling/README.md), [02-L08](../Assets/Labs/02_GC/L08_SnapshotLeak/README.md)) |
| 래퍼 / 셸 (wrapper / managed shell) | 네이티브 객체를 가리키는 C# `UnityEngine.Object` 인스턴스 | Destroy 후에도 래퍼는 C# 참조가 있으면 남음 → Memory Profiler의 "Leaked Managed Shell" ([02-L08](../Assets/Labs/02_GC/L08_SnapshotLeak/README.md)) |
| 가짜 null (fake null) | 네이티브는 파괴됐지만 래퍼는 남은 상태에서 `== null`이 true가 되는 것 | `==`·`!=`·`bool` 변환만 Unity 오버로드를 탐. `is null`·`?.`·`??`·`ReferenceEquals`는 안 탐 ([01-L01](../Assets/Labs/01_NativeManaged/L01_FakeNull/README.md)) |
| Instance ID | 래퍼가 네이티브 객체를 찾는 정수 키 (`m_InstanceID`) | `new`로 만든 컴포넌트는 0 → 에디터에서 경고·예외 분기 ([01-L06](../Assets/Labs/01_NativeManaged/L06_ReadBindings/README.md)) |
| 바인딩 (binding) | C# API에서 네이티브 함수를 부르는 연결 코드 (`[FreeFunction]`, `extern`) | 에디터와 플레이어는 **다른 DLL**이라 바인딩 동작이 다를 수 있음 ([01-L06](../Assets/Labs/01_NativeManaged/L06_ReadBindings/README.md)) |
| 마샬링 (marshalling) | 경계를 넘을 때 데이터를 변환·복사하는 것 (예: 네이티브 배열 → 새 C# 배열) | 경계 비용은 "넘는 방향과 넘어오는 데이터 종류"가 결정 ([01-L05](../Assets/Labs/01_NativeManaged/L05_BoundaryCost/README.md)) |
| 역직렬화 (deserialization) | 저장된 필드 값을 객체에 덮어쓰는 것 | 순서: 생성자(필드 초기화) → 역직렬화 → `Awake` ([01-L03](../Assets/Labs/01_NativeManaged/L03_CtorTiming/README.md)) |

## 4. 빌드와 스크립팅 백엔드 (1장)

| 용어 | 뜻 | 헷갈리기 쉬운 것 · 실습 |
|---|---|---|
| IL (CIL) | C# 컴파일러가 내는 중간 언어. DLL 안에 들어 있음 | C# → IL은 항상 같고, IL → 기계어가 백엔드마다 다름 |
| 스크립팅 백엔드 (scripting backend) | IL을 실행하는 방식. Mono 또는 IL2CPP | 결론은 백엔드별로 따로 냄 |
| JIT (just-in-time) | 실행 중에 IL을 기계어로 컴파일. Mono가 사용 | |
| AOT (ahead-of-time) | 빌드 시점에 미리 컴파일. IL2CPP가 사용(IL → C++ → 기계어) | 런타임에 새 코드를 만들 수 없어 일부 리플렉션·제네릭에 제약 |
| 생성 코드 (generated code) | IL2CPP가 만든 C++ 파일 | `NullCheck`, `Il2CppCodeGenWriteBarrier`, `Box(...)`가 보이는 곳 ([01-L07](../Assets/Labs/01_NativeManaged/L07_NullChecks/README.md)) |
| 제네릭 공유 (generic sharing) | 여러 타입 인자의 제네릭이 한 벌의 기계어를 같이 쓰는 것 | 기본은 참조 타입끼리만 공유. **full generic sharing**(Faster (smaller) builds)은 값 타입까지 공유 ([01-L09](../Assets/Labs/01_NativeManaged/L09_GenericSharing/README.md)) |
| RGCTX (runtime generic context) | 공유 코드가 실제 타입 인자 정보(크기 등)를 런타임에 읽어오는 표 | "값 타입 = 반드시 전용 코드"가 아닌 이유 |
| 코드 스트리핑 (code stripping) / UnityLinker | 쓰지 않는 코드를 빌드에서 지우는 것 / 그 도구 | 상수 문자열 리플렉션은 알아보지만 런타임 조합 문자열은 못 봄 ([01-L10](../Assets/Labs/01_NativeManaged/L10_Stripping/README.md)) |
| `[Preserve]` | 스트리핑에서 지우지 말라는 표시 | 붙인 대상(타입 + 기본 생성자)에만 적용, 멤버 메서드는 따로 |
| Development Build | 프로파일러 연결·카운터가 켜진 플레이어 빌드 | 할당·시간 결론은 이 플레이어에서 냄. 에디터 측정은 참고용 |

## 5. 메모리와 GC (2장)

### 메모리 영역

| 용어 | 뜻 | 헷갈리기 쉬운 것 · 실습 |
|---|---|---|
| 스택 (stack) | 메서드 호출마다 쌓이는 영역. 지역 변수·매개변수 | 할당·해제가 사실상 공짜. GC 대상 아님(단 GC 루트) |
| 힙 / 매니지드 힙 (heap / managed heap) | `new`한 참조 타입 객체가 사는 곳. GC가 관리 | "GC 할당" = 매니지드 힙에 할당 |
| GC Used / GC Reserved | Used는 살아 있는 객체가 쓰는 양, Reserved는 GC가 OS에서 받아 둔 전체 | 수집해도 Reserved는 바로 안 줄어듦. IL2CPP는 수집 6\~12회 뒤 반납, Mono는 유지 ([02-L04](../Assets/Labs/02_GC/L04_UsedVsReserved/README.md)) |
| 단편화 (fragmentation) | 빈 공간이 잘게 흩어져 큰 객체가 못 들어가는 상태 | Boehm GC는 객체를 옮기지 않아(non-moving) 단편화가 풀리지 않음 ([02-L04](../Assets/Labs/02_GC/L04_UsedVsReserved/README.md)) |
| 블록 / 크기 클래스 (block / size class) | 힙을 크기별 칸으로 나눠 관리하는 단위 | 작은 객체는 같은 크기 칸끼리 모임 |

### 수집 방식

| 용어 | 뜻 | 헷갈리기 쉬운 것 · 실습 |
|---|---|---|
| 추적 GC (tracing GC) | 루트에서 참조를 따라가 **닿는 것만 살린다**. 안 닿은 건 나중에 한꺼번에 재사용 | 쓰레기를 "표시"하지 않는다. 비용은 **산 것**에만 듦 ← 약점 |
| 참조 카운팅 (reference counting) | 객체마다 참조 수를 세다가 0이 되면 바로 해제 | Unity의 C# GC는 이 방식이 **아님**. "null 대입 → 바로 해제·마킹 해제"는 이 사고방식 |
| GC 루트 (GC root) | 추적의 출발점: 정적 필드, 실행 중인 스택·레지스터, 핸들 등 | |
| 도달 가능 (reachable) | 루트에서 참조를 따라 닿을 수 있음 = 살아 있음 | |
| 마킹 (mark) | 도달 가능한 객체에 "살아 있음" 표시를 하는 단계 | 비용 = 새로 표시하는 객체 수 + 훑는 바이트 × 스캔 방식 ([02-L05](../Assets/Labs/02_GC/L05_MarkCost/README.md)) |
| 스윕 (sweep) | 표시 안 된 칸을 빈 칸으로 돌려놓는 단계 | |
| 정밀 / 보수적 스캔 (precise / conservative) | 정밀은 타입 정보로 참조 위치만 읽음, 보수적은 포인터처럼 보이는 값을 모두 참조로 간주 | 스택은 보수적, 힙 객체는 타입 정보를 쓰는 편 |
| 포인터 없는 객체 (pointer-free) | 참조 필드가 없는 객체·배열. GC가 내부를 훑지 않음 | 참조 없는 `struct[]` 100만 개도 마킹 비용이 기준선과 같음 ([02-L05](../Assets/Labs/02_GC/L05_MarkCost/README.md)) |
| Boehm GC (bdwgc) | Unity Mono·IL2CPP가 쓰는 GC 라이브러리. 비세대·비이동 기반 | |
| stop-the-world | 수집하는 동안 앱 스레드를 멈추는 것 | Incremental Off면 수집 한 번이 한 프레임 스파이크(약 10 ms) ([02-L06](../Assets/Labs/02_GC/L06_IncrementalGC/README.md)) |

### Incremental GC

| 용어 | 뜻 | 헷갈리기 쉬운 것 · 실습 |
|---|---|---|
| 점진적 GC (incremental GC) | 마킹을 여러 프레임에 **나눠서** 하는 것 | "나눠서"(incremental)와 "다 안 하고 일부만"(부분 수집)은 다른 개념 |
| 부분 수집 (partial collection) | 지난 수집에서 표시한 객체를 다시 마킹하지 않고 새 객체·바뀐 곳만 보는 수집 | 세대별 GC(generational GC)와 비슷한 효과. 20회마다 full 수집(`GC_full_freq` 19) ([02-L06](../Assets/Labs/02_GC/L06_IncrementalGC/README.md)) |
| full 수집 (full collection) | 마크를 모두 지우고 루트부터 전부 다시 마킹 | Incremental On에서 주기적 스파이크 3\~6 ms의 정체 |
| 쓰기 배리어 (write barrier) | 참조 필드에 값을 쓸 때 GC에 "여기 바뀜"을 알리는 코드 | 마킹 중이 아니어도 매번 실행됨. 참조 타입 `List.Add` 비용의 약 90% ([01-L09](../Assets/Labs/01_NativeManaged/L09_GenericSharing/README.md)) |
| dirty (카드 / 페이지) | 쓰기 배리어가 "다시 훑어야 함"으로 표시한 영역 | 표시되는 건 **쓴 쪽**(`_live` 배열)이지, 끊긴 객체가 아님 ([02-L06](../Assets/Labs/02_GC/L06_IncrementalGC/README.md)) |
| 부유 쓰레기 (floating garbage) | 이미 죽었지만 이전 마크가 남아 이번 수집에서 회수되지 않는 객체 | 다음 full 수집까지 남음 |
| `GarbageCollector.Mode.Manual` / `CollectIncremental(ns)` | 자동 수집을 끄고 직접 수집을 부르는 모드 / 시간 예산(ns)을 주고 점진 수집을 진행 | 인자는 **호출 간격이 아니라 한 번에 쓸 시간 예산**이고, 상한도 아님 ([02-L07](../Assets/Labs/02_GC/L07_ManualGC/README.md)) |

### 수명 끝

| 용어 | 뜻 | 헷갈리기 쉬운 것 · 실습 |
|---|---|---|
| 파이널라이저 (finalizer) | `~C()`. 객체가 회수되기 전에 한 번 불리는 메서드 | C++ 소멸자(destructor)와 달리 **언제 불릴지 모름**. 별도 파이널라이저 스레드에서 실행 ([02-L09](../Assets/Labs/02_GC/L09_Finalizer/README.md)) |
| 부활 (resurrection) | 파이널라이저를 돌리려고 죽은 객체를 다시 살리는 것 | 그래서 첫 수집이 3\~4배 비쌈: 객체와 그 필드를 **새로 마킹**해야 함 |
| 약한 참조 (weak reference) | 객체를 살려 두지 않는 참조 (`WeakReference`) | 긴 약한 참조(`trackResurrection: true`)는 IL2CPP에서 `NotSupportedException` ([02-L09](../Assets/Labs/02_GC/L09_Finalizer/README.md)) |
| 매니지드 누수 (managed leak) | 쓰지 않는 객체가 루트에서 아직 닿아 회수되지 않는 것 | GC 언어에서 "누수"는 해제를 잊은 게 아니라 **참조를 안 끊은 것**. 이벤트 구독, 정적 컬렉션 ([02-L08](../Assets/Labs/02_GC/L08_SnapshotLeak/README.md)) |
| 풀링 (pooling) | 객체를 파괴하지 않고 재사용 | Unity 객체 풀링의 이득은 주로 CPU (약 2.1\~2.6배) ([02-L03](../Assets/Labs/02_GC/L03_Pooling/README.md)) |

## 6. PlayerLoop (3장)

실습 칸은 해당 실습을 마치면 채운다.

| 용어 | 뜻 | 헷갈리기 쉬운 것 · 실습 |
|---|---|---|
| PlayerLoop | 엔진이 한 프레임에 실행하는 일의 순서. 코드에 박힌 호출 순서가 아니라 **시스템 트리(데이터)**로 표현됨 | `GetCurrentPlayerLoop()`은 **사본**을 돌려줌. 고친 뒤 `SetPlayerLoop()`를 불러야 반영. default(엔진 고정 목록)와 current(패키지가 고친 것)는 다를 수 있음 — 툴은 current를 고칠 것 ([03-L01](../Assets/Labs/03_PlayerLoop/L01_TreeDump/README.md)) |
| `PlayerLoopSystem` | 트리의 노드(struct). `type`(식별용 마커 타입), `subSystemList`(자식 배열), `updateDelegate`(매니지드 콜백), `updateFunction`(네이티브 함수 포인터, `IntPtr`), `loopConditionFunction`(반복 조건, default 트리에서는 `FixedUpdate` 단계만 설정) | `type`은 실행되는 코드가 아니라 이름표. 엔진 시스템은 `UnityEngine.PlayerLoop` 네임스페이스의 빈 struct |
| 단계 / 하위 시스템 (phase / subsystem) | 최상위 노드(단계)와 그 아래에서 실제 일을 하는 노드 | `Update()` 콜백도 "Update 단계 안의 하위 시스템 하나"가 부르는 것 |
| 커스텀 시스템 (custom system) | `updateDelegate`에 C# 메서드를 넣어 트리에 끼운 노드 | 매 프레임 **네이티브 → 매니지드** 호출 1회. 1장 경계 비용의 반대 방향 |
| Unity 메시지 / 매직 메서드 (message / magic method) | `Update`, `Awake` 등 이름으로 찾아 부르는 콜백. 인터페이스·가상 메서드가 아님 | 매 프레임 리플렉션으로 찾지 않음. 스크립트 로드 때 조사해 둔 정보로 호출 |
| 코루틴 (coroutine) | 이터레이터(`IEnumerator`)를 엔진이 `MoveNext()`로 나눠 실행하는 것 | 실행 주체는 엔진의 코루틴 스케줄러. `yield` 값의 **종류**가 다음 재개 시점을 정함. 첫 구간은 `StartCoroutine` 안에서 동기 실행, 이후 재개는 `ScriptRunDelayed*` 시스템 ([03-L02](../Assets/Labs/03_PlayerLoop/L02_ResumeMap/README.md)) |
| yield instruction | 코루틴이 `yield return`으로 돌려주는 대기 조건 (`null`, `WaitForSeconds`, `WaitForEndOfFrame` 등) | 값 타입을 `yield return`하면 `object`로 반환 → 박싱 대상 |
| `Awaitable` | Unity 6의 엔진 전용 awaitable 타입 (2023.1 도입). 인스턴스를 풀링함 | 풀링되므로 **같은 인스턴스를 두 번 await하면 안 됨** (공식 문서) |
| continuation | `await` 다음 줄부터의 나머지 코드. 대기가 끝나면 누군가 이것을 호출해야 함 | "누가, 어느 스레드에서, 언제 부르나"가 Task·Awaitable·코루틴을 가르는 질문 |
| `SynchronizationContext` / `UnitySynchronizationContext` | continuation을 원래 스레드로 돌려보내는 .NET 장치 / Unity 메인 스레드용 구현. `Post`된 작업을 큐에 모았다가 PlayerLoop에서 실행 | `ConfigureAwait(false)`는 이것을 건너뜀. 실행 위치는 `Update/ScriptRunDelayedTasks` — `await Task.Yield()`는 같은 프레임 `LateUpdate` 전에 재개 ([03-L02](../Assets/Labs/03_PlayerLoop/L02_ResumeMap/README.md)) |
| 메인 스레드 (main thread) | PlayerLoop 전체를 순서대로 실행하는 스레드. 대부분의 Unity API는 여기서만 호출 가능 | 렌더 스레드·잡 워커·로딩 스레드는 별도. "동기 API = 메인 스레드에서 실행"은 아님 ([01-L03](../Assets/Labs/01_NativeManaged/L03_CtorTiming/README.md)) |
| 고정 시간 간격 (fixed timestep) | `FixedUpdate`·물리가 쓰는 일정한 시간 간격 (`Time.fixedDeltaTime`, 기본 0.02 s) | 프레임 수와 `FixedUpdate` 횟수는 1:1이 아님 |
| 도메인 리로드 (domain reload) | 에디터가 매니지드 상태(정적 필드 등)를 버리고 어셈블리를 다시 올리는 것 | Enter Play Mode Options로 끌 수 있음 → 정적 상태와 엔진 쪽 상태가 플레이 모드를 넘어 남을 수 있음 |
