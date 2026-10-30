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

리플렉션: `SealedDog.Speak` `IsFinal=False`, `Wolf.Speak` `IsFinal=True` (둘 다 `IsVirtual=True`)

| 실행일 | 백엔드 | Incremental GC | 빌드 | 조건 | 측정값 (ns/호출) |
|---|---|---|---|---|---|
| 2026-10-31 | IL2CPP | On | Development | (A) `Animal` 타입, `Dog` 인스턴스 | 평균 2.124 (최소 2.028, 최대 2.450) |
| 2026-10-31 | IL2CPP | On | Development | (B) `Dog` | 평균 2.124 (최소 2.015, 최대 2.418) |
| 2026-10-31 | IL2CPP | On | Development | (C) `SealedDog` | 평균 2.110 (최소 2.015, 최대 2.310) |
| 2026-10-31 | IL2CPP | On | Development | (D) `Wolf` | 평균 2.130 (최소 2.017, 최대 2.344) |

원본: `Notes/results/01-L08_IL2CPP_2026-10-31.csv`

## 해석
- 예측과 비교: 결과만 보면 A·B·C 맞음, D 틀림 (3/4). 그러나 이유로 보면 D의 추론("메서드가 sealed라 구현 확정")은 이론상 옳고, C는 답은 맞았지만 이유("자식에서 상속 가능")가 틀렸다. 후속 질문 "`SealedDog d`에 들어올 수 있는 런타임 타입은?"에 처음엔 "모르겠음", 힌트(상속해야 대입 가능 + sealed는 상속 금지) 후 "SealedDog 하나"로 정답.
- 이론상 직접 호출이 가능한 것은 C(타입이 봉인 → 런타임 타입이 하나)와 D(메서드가 봉인 → 어떤 파생 클래스든 `Wolf.Speak`가 최종 구현)다. A·B는 파생 클래스가 더 있을 수 있어서 불가능하다.
- 그런데 IL2CPP 6000.3.25f1은 네 경우 모두 `VirtualFuncInvoker`를 생성했고, 호출 시간도 2.11~2.13 ns로 같았다. C# 컴파일러가 네 경우 모두 `callvirt Animal::Speak`(처음 선언된 메서드)를 내고, IL2CPP는 이 패턴에서 수신 객체의 정적 타입을 보고 호출 대상을 좁히지 않았다.
  - "sealed면 IL2CPP가 직접 호출로 바꾼다"는 설명이 널리 알려져 있다(Unity의 IL2CPP 최적화 블로그로 기억하지만 출처는 확인 필요). 이 버전·이 패턴에서는 재현되지 않았다. 어떤 형태에서 devirtualization이 일어나는지는 후속 실험으로 좁혀야 한다 — 확인 필요.
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
```
