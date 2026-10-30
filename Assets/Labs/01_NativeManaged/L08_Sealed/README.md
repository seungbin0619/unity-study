# 01-L08 `sealed`와 devirtualization

## 목표
같은 `virtual` 메서드를 정적 타입(static type)만 바꿔 호출했을 때, IL2CPP가 가상 호출(virtual call)과 직접 호출(direct call) 중 무엇을 생성하는지 확인한다. 호출당 시간 차이도 플레이어에서 잰다.

## 예측
- 질문: [SealedTargets.cs](SealedTargets.cs)의 네 메서드에서 `Speak()` 호출이 IL2CPP 생성 C++에서 **가상 호출(`VirtualFuncInvoker` 계열)**과 **직접 호출(`Dog_Speak_m...(d, NULL)` 같은 함수 호출)** 중 어느 쪽이 될까?
  - (A) `CallAnimal(Animal a) => a.Speak();`
  - (B) `CallDog(Dog d) => d.Speak();` — `Dog`는 봉인 안 됨
  - (C) `CallSealedDog(SealedDog d) => d.Speak();` — `sealed class`
  - (D) `CallWolf(Wolf w) => w.Speak();` — 클래스는 열려 있고 `public sealed override int Speak()`
- 학습자 답 (실행 전, 원문 그대로, 2026-10-31):
  ```
  (A) ~ (C) 가상 호출 - Speak이 virtual 또는 override라 자식에서 상속 가능하기 때문에 어느 구현인지 확인 필요
  (D) 직접 호출 - sealed가 붙어 더 이상 상속 불가능하기 때문에 구현 확정 가능
  ```

- 추가 질문 (2026-10-31, 가설 검증): IL의 호출 대상이 수신 타입 자신의 메서드인 두 경우, IL2CPP는 직접 호출과 가상 호출 중 무엇을 생성할까?
  - (E) `CallFox(Fox f) => f.Speak();` — `Fox : ISpeaker` 암시적 구현, IL `callvirt Fox::Speak`, `Fox.Speak` `IsFinal=True`
  - (F) `CallOpenFox(OpenFox f) => f.Speak();` — `public virtual int Speak()`, IL `callvirt OpenFox::Speak`, `IsFinal=False`
- 학습자 답 (실행 전, 원문 그대로, 2026-10-31):
  ```
  직접(Fox의 Speak는 더이상 override 불가능), 가상 (OpenFox의 Speak은 상속 가능)
  ```

- 추가 질문 (2026-10-31, 블로그 조건 재현): [Farm.cs](Farm.cs)는 2016년 블로그 예제를 그대로 옮겼다(`abstract` 기반, `string` 반환, `MonoBehaviour.Start` 안에서 `var cow = new Cow(); cow.Speak()`). `sealed class Cow`의 `cow.Speak()`는 6000.3 IL2CPP에서 직접 호출이 될까? (대조군: 봉인하지 않은 `OpenCow`, 기반 타입 루프 `animal.Speak()`)
- 학습자 답: 예측 없이 검토만 진행 (학습자 요청)

## 실습 구성
- 코드: `SealedTargets.cs` — `Animal`/`Dog`/`SealedDog`/`Wolf`와 호출 메서드 A~D, 시간 측정용 루프 메서드
- 코드: `SealedLab.cs` — A~D를 호출해 스트리핑을 막고, 호출당 시간(1천만 회 × 표본 20, 워밍업 3, 표본마다 케이스 순서를 돌림)을 기록
- 실행: IL2CPP Development 빌드 후 `Builds/IL2CPP/Labs.exe -lab 01-L08 -batchmode -nographics -logFile Notes/results/player_01-L08.log`, 생성 코드는 `Library/Bee` 아래 `il2cppOutput`에서 메서드 이름으로 검색

## 결과
생성 코드: `Library/Bee/artifacts/WinPlayerBuildProgram/il2cppOutput/cpp/Labs.cpp`. il2cpp 인자: `--emit-null-checks --enable-array-bounds-check --dotnetprofile=unityaot-win32` (IL2CPP Code Generation 등 다른 옵션은 기본값)

| 메서드 | C# 컴파일러가 낸 IL | IL2CPP 생성 C++ |
|---|---|---|
| (A) `CallAnimal` | `callvirt Animal::Speak` | `VirtualFuncInvoker0<int32_t>::Invoke(4, L_0)` |
| (B) `CallDog` | `callvirt Animal::Speak` | 같음 (가상 호출) |
| (C) `CallSealedDog` | `callvirt Animal::Speak` | 같음 (가상 호출) |
| (D) `CallWolf` | `callvirt Animal::Speak` | 같음 (가상 호출) |

| (E) `CallFox` (가설 검증) | `callvirt Fox::Speak` | **`Fox_Speak_m...(L_0, NULL)` 직접 호출** |
| (F) `CallOpenFox` (가설 검증) | `callvirt OpenFox::Speak` | `VirtualFuncInvoker0<int32_t>::Invoke(5, L_0)` |

리플렉션: `SealedDog.Speak` `IsFinal=False`, `Wolf.Speak` `IsFinal=True`, `Fox.Speak` `IsFinal=True`, `OpenFox.Speak` `IsFinal=False` (모두 `IsVirtual=True`)

| 실행일 | 백엔드 | Incremental GC | 빌드 | 조건 | 측정값 (ns/호출) |
|---|---|---|---|---|---|
| 2026-10-31 | IL2CPP | On | Development | (A) `Animal` 타입, `Dog` 인스턴스 | 평균 2.124 (최소 2.028, 최대 2.450) |
| 2026-10-31 | IL2CPP | On | Development | (B) `Dog` | 평균 2.124 (최소 2.015, 최대 2.418) |
| 2026-10-31 | IL2CPP | On | Development | (C) `SealedDog` | 평균 2.110 (최소 2.015, 최대 2.310) |
| 2026-10-31 | IL2CPP | On | Development | (D) `Wolf` | 평균 2.130 (최소 2.017, 최대 2.344) |
| 2026-10-31 (2차, E·F 추가) | IL2CPP | On | Development | (A) / (B) / (C) / (D) | 평균 1.965 / 2.069 / 2.060 / 1.873 |
| 2026-10-31 (2차, E·F 추가) | IL2CPP | On | Development | (E) `Fox` — 직접 호출 | 평균 **0.464** (최소 0.242, 최대 0.642) |
| 2026-10-31 (2차, E·F 추가) | IL2CPP | On | Development | (F) `OpenFox` — 가상 호출 | 평균 1.957 (최소 0.962, 최대 2.345) |

원본: `Notes/results/01-L08_IL2CPP_2026-10-31.csv`

## 해석
- 예측과 비교: 결과만 보면 A·B·C 맞음, D 틀림 (3/4). 그러나 이유로 보면 D의 추론("메서드가 sealed라 구현 확정")은 이론상 옳고, C는 답은 맞았지만 이유("자식에서 상속 가능")가 틀렸다. 후속 질문 "`SealedDog d`에 들어올 수 있는 런타임 타입은?"에 처음엔 "모르겠음", 힌트(상속해야 대입 가능 + sealed는 상속 금지) 후 "SealedDog 하나"로 정답.
- 이론상 직접 호출이 가능한 것은 C(타입이 봉인 → 런타임 타입이 하나)와 D(메서드가 봉인 → 어떤 파생 클래스든 `Wolf.Speak`가 최종 구현)다. A·B는 파생 클래스가 더 있을 수 있어서 불가능하다.
- 그런데 IL2CPP 6000.3.25f1은 네 경우 모두 `VirtualFuncInvoker`를 생성했고, 호출 시간도 2.11~2.13 ns로 같았다. C# 컴파일러가 네 경우 모두 `callvirt Animal::Speak`(처음 선언된 메서드)를 내고, IL2CPP는 이 패턴에서 수신 객체의 정적 타입을 보고 호출 대상을 좁히지 않았다.
  - 출처: Unity 블로그 [IL2CPP optimizations: Devirtualization](https://unity.com/blog/engine-platform/il2cpp-optimizations-devirtualization) (2016-07, Unity 5.3.5). `sealed class Cow : Animal`의 `override Speak()`를 `Cow` 타입 변수로 호출하면 `VirtFuncInvoker0::Invoke(4 /* Cow::Speak() */)`가 `Cow_Speak_m...(L_16, NULL)`로 바뀐다고 보여 준다. 우리 (C)와 같은 형태인데 6000.3에서는 재현되지 않았다. 글쓴이도 "게임이 극적으로 빨라지지는 않는다"고 했고, 수치 벤치마크는 없다.
  - 가설 (확인 필요): 블로그의 생성 코드 주석이 `Cow::Speak()`인 것으로 보아, 당시 Unity의 C# 컴파일러(Mono `mcs`)는 `callvirt Cow::Speak`를 냈던 것으로 보인다. 지금의 Roslyn은 `callvirt Animal::Speak`를 낸다(이번 실측). IL2CPP의 devirtualization이 "호출 대상으로 적힌 메서드나 그 선언 타입이 봉인됐는가"만 본다면, 컴파일러가 바뀌면서 이 최적화가 걸리지 않게 됐을 수 있다. 검증 방법: sealed 클래스에 새로 선언한 `virtual` 메서드를 부르면 IL이 `callvirt SealedX::M`이 되므로, 그 경우에 직접 호출이 나오는지 본다.
  - **가설 검증 결과 (E·F):** sealed 클래스에는 새 `virtual`을 선언할 수 없어서(CS0549) 인터페이스 구현으로 IL 대상을 바꿨다. IL이 `callvirt Fox::Speak`이고 그 메서드가 final인 (E)는 **직접 호출**이 됐고, IL이 `callvirt OpenFox::Speak`이지만 final이 아닌 (F)는 가상 호출로 남았다. 학습자 예측("직접 / 가상")은 둘 다 맞음.
  - 결론: IL2CPP 6000.3은 **IL에 적힌 호출 대상 메서드가 final인지(또는 그 선언 타입이 sealed인지)**만 보고 devirtualize한다. 수신 객체의 정적 타입(`SealedDog`, `Wolf`)은 보지 않는다. Roslyn은 override 호출의 대상을 처음 선언된 메서드(`Animal::Speak`)로 적기 때문에, `sealed class`나 `sealed override`를 붙여도 이 최적화가 걸리지 않는다. 선언 타입이 sealed인 경우만 따로 확인하지는 못했다(C#으로 그런 IL을 만들 수 없음).
  - 시간: (E) 0.46 ns vs 나머지 약 1.9~2.1 ns, 약 4배. 직접 호출이 되자 C++ 컴파일러가 상수를 반환하는 `Fox_Speak`를 인라인했을 가능성이 크다 — 디스어셈블리로 확인 필요. 이번 실행은 최소값 편차가 커서(A 최소 1.08) 가상 호출들끼리의 차이는 해석하지 않는다.
  - **블로그 조건 재현 (G, [Farm.cs](Farm.cs)):** 블로그 예제를 그대로 옮겨 봤다(`abstract` 기반, `string` 반환, `MonoBehaviour.Start` 안, `var cow = new Cow();`). `sealed class Cow`의 `cow.Speak()`가 `VirtualFuncInvoker0<String_t*>::Invoke(4, L_20)`로 남았고, 봉인하지 않은 `OpenCow`와 생성 코드가 같았다. IL은 세 호출 모두 `callvirt FarmAnimal::Speak`. 블로그의 "after" 결과(`Cow_Speak_m...(L_16, NULL)`)는 6000.3에서 재현되지 않았다. 플레이어 로그에 `The cow says 'Moo'`가 찍혀 코드가 실제로 실행됐음도 확인.
  - 결론의 범위: **Unity 6000.3.25f1, Windows x64, IL2CPP Development 빌드, 코드 생성 옵션 기본값**에서 확인한 사실이다. Release 빌드, 다른 플랫폼, 다른 버전은 확인하지 않았다. "IL 호출 대상이 final일 때만 직접 호출"은 E·F·G와 맞아떨어지는 해석이지만 IL2CPP 소스로 확인한 것은 아니다.
  - 실무 함의: Unity 6에서 클래스 계층의 override 호출을 직접 호출로 만들고 싶다면 `sealed`로는 부족하다. 비가상 메서드로 두거나, 인터페이스를 암시적으로 구현한 메서드를 구체 타입으로 호출하는 경우처럼 IL 대상 자체가 final인 메서드여야 한다.
- 한 단계 더:
  - devirtualization의 진짜 이득은 간접 호출 하나를 없애는 것보다, 그 뒤에 **인라인**이 가능해진다는 데 있다. `Speak()`처럼 상수를 반환하는 메서드라면 루프 전체가 접힐 수도 있다. 가상 호출로 남으면 C++ 컴파일러는 vtable 너머를 볼 수 없다.
  - .NET의 RyuJIT은 sealed 타입이나 final 메서드에 대한 호출을 JIT 단계에서 devirtualize한다(.NET Core 이후). "같은 C# 코드라도 백엔드마다 최적화가 다르다"는 01-L07의 경계 검사 제거와 같은 맥락이다.
  - 실무 판단: Unity 6 IL2CPP에서는 `sealed`만 붙인다고 이 패턴의 성능이 바뀌지 않았다. `sealed`는 설계 의도(상속 금지)로 쓰고, 핫 패스에서 직접 호출이 필요하면 비가상 메서드를 쓰거나 Burst로 옮긴다. struct + 제네릭 제약(constrained call)이 직접 호출이 되는지는 확인 필요.
- 근거: 이 프로젝트의 IL2CPP 생성 코드(`Labs.cpp`), 에디터에서 `Labs.dll` IL 바이트를 리플렉션으로 해석한 결과, Player DAG의 il2cpp 인자

## 코드 발췌
```cpp
// (A)~(D) 네 메서드 모두 같은 모양 — 슬롯 4번을 vtable에서 꺼내 간접 호출
SealedDog_t...* L_0 = ___0_d;
NullCheck(L_0);
L_1 = VirtualFuncInvoker0< int32_t >::Invoke(4, L_0);

// (E) IL 대상이 final인 Fox::Speak — 직접 호출
L_1 = Fox_Speak_m6DDA1BC4A88FF6A118AD46022700690A7D9288C7(L_0, NULL);

// (F) IL 대상이 final이 아닌 OpenFox::Speak — 가상 호출 (슬롯 5)
L_1 = VirtualFuncInvoker0< int32_t >::Invoke(5, L_0);

// (G) 블로그 예제 Farm.Start — sealed Cow도 가상 호출
Cow_t...* L_17 = (Cow_t...*)il2cpp_codegen_object_new(Cow_t..._il2cpp_TypeInfo_var);
Cow__ctor_m...(L_17, NULL);
V_1 = L_17;
...
L_21 = VirtualFuncInvoker0< String_t* >::Invoke(4, L_20);   // 블로그(5.3.5)에서는 Cow_Speak_m1607867742(L_16, NULL)
```
