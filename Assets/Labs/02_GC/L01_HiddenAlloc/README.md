# 02-L01 숨은 할당 측정

## 목표
코드에 `new`가 보이지 않는데 GC 힙 할당이 생기는 패턴을 호출당 바이트로 실측한다. 컴파일러(Roslyn, C# 9)가 무엇을 만들어 내는지와 연결해 설명한다.

## 예측
- 질문: `AllocTargets.cs`의 A~K를 워밍업 후 한 번 호출할 때, 각각 GC 할당이 생기는가 (O/X)? 생긴다면 대략 몇 바이트인가? (64비트 기준)
- 학습자 답 (실행 전, 원문 그대로):
  ```
  A - O (박싱)
  B - O (string.Format("HP: {0}", _hp)에서 2번의 할당 발생 - string, 박싱)
  C - O (x => x == t 에서 클로저로 인한 할당)
  D - X
  E - X
  F - X
  G - O (IEnumerable<int> 변환 할당)
  H - X
  I - O (새 WaitForSeconds 인스턴스 할당)
  J - O (params가 새 배열 할당)
  K - O (params가 크기 0인 배열 할당)
  ```
- 추가 질문 (2026-10-31, 메서드 그룹 대처법 심화): 인스턴스 메서드 `bool IsMatch(int x) => x == _target;`를 람다로 감싼 `_list.FindIndex(x => IsMatch(x))`(M)는 워밍업 후 호출마다 할당되는가? 무엇이 몇 개?
- 학습자 답 (실행 전, 원문 그대로): "_target때문에 호출 할 때마다 1번씩 할당될거같은데"
- 비교용으로 L(인스턴스 메서드 그룹), N(필드를 안 읽는 인스턴스 메서드를 람다로 감쌈), O(static 필드에 캐시한 델리게이트), P(struct 제네릭 predicate)를 함께 잰다

## 실습 구성
- 코드: `AllocTargets.cs` — 케이스 A~K를 메서드 하나씩으로 둔다
- 코드: `HiddenAllocLab.cs` — 케이스마다 워밍업 3프레임 + 측정 10프레임, 프레임당 100회 호출. 0a(빈 호출)·0b(`new byte[16]`)로 측정 경로를 보정하고, 첫 워밍업 프레임의 할당을 따로 남긴다
- 실행: 에디터 메뉴 `Labs/02/L01 Hidden Alloc` 또는 플레이어 `-lab 02-L01`

## 결과
호출당 바이트 / 호출당 할당 횟수 (10프레임 평균. 플레이어는 모든 케이스에서 min = max)

| 실행일 | 백엔드 | Incremental GC | 빌드 | 조건 | 측정값 |
|---|---|---|---|---|---|
| 2026-10-31 | Mono | On | Development | 0a 빈 호출 / 0b `new byte[16]` | 0 / 48 B·1 |
| 2026-10-31 | Mono | On | Development | A 박싱 · B 보간 · C 캡처 람다 | 20 B·1 · 56 B·2 · 148 B·2 |
| 2026-10-31 | Mono | On | Development | D 캡처 없는 람다 · E static 메서드 그룹 | 0 (첫 프레임 144 B·2) · 128 B·1 |
| 2026-10-31 | Mono | On | Development | F `foreach List` · G `foreach IEnumerable` · H `Count(pred)` | 0 · 40 B·1 · 40 B·1 |
| 2026-10-31 | Mono | On | Development | I `WaitForSeconds` · J `params` 3개 · K `params` 0개 | 20 B·1 · 44 B·1 · 0 (첫 프레임 32 B·1) |
| 2026-10-31 | IL2CPP | On | Development | 0a / 0b | 0 / 48 B·1 |
| 2026-10-31 | IL2CPP | On | Development | A · B · C | 20 B·1 · 60 B·2 · 152 B·2 |
| 2026-10-31 | IL2CPP | On | Development | D · E | 0 (첫 프레임 144 B·2) · 128 B·1 |
| 2026-10-31 | IL2CPP | On | Development | F · G · H | 0 · 40 B·1 · 40 B·1 |
| 2026-10-31 | IL2CPP | On | Development | I · J · K | 24 B·1 · 44 B·1 · 0 (첫 프레임 32 B·1) |
| 2026-10-31 | Mono | On | Development | (추가) L 인스턴스 메서드 그룹 · M 람다로 감싼 인스턴스 메서드 · N 같은데 필드 안 읽음 | 128 B·1 · 128 B·1 · 128 B·1 |
| 2026-10-31 | Mono | On | Development | (추가) O static 필드 캐시 델리게이트 · P struct 제네릭 predicate | 0 · 0 (첫 프레임도 0) |
| 2026-10-31 | IL2CPP | On | Development | (추가) L · M · N | 128 B·1 · 128 B·1 · 128 B·1 |
| 2026-10-31 | IL2CPP | On | Development | (추가) O · P | 0 · 0 (첫 프레임도 0) |
| 2026-10-31 | Mono (에디터) | On | 에디터 | 0a 빈 호출 | 93 B·2.2 — 에디터 자체 할당(프레임당 약 9 KB)이 섞여 판정 불가, 참고용 |

원본: `Notes/results/02-L01_Mono_2026-10-31.csv`, `02-L01_IL2CPP_2026-10-31.csv`, `02-L01_Editor_2026-10-31.csv`

## 해석
- 예측과 비교: 11개 중 8개 맞음 (A·B·C·D·F·G·I·J). B는 할당 횟수(string + 박싱 = 2)까지 맞음.
  - E ✗ (X로 예측, 실제 128 B): C# 9 Roslyn은 메서드 그룹 → 델리게이트 변환을 **매번 `new Predicate<int>(IsThree)`** 로 낮춘다. 캡처 없는 람다(D)는 컴파일러가 만든 `<>c` 클래스의 static 필드에 델리게이트를 캐시하므로 첫 호출에만 할당된다 (첫 프레임 144 B = `<>c` 싱글턴 16 B + 델리게이트 128 B). 메서드 그룹 캐싱은 C# 11부터이고 Unity 6은 C# 9.
  - H ✗ (X로 예측, 실제 40 B): 람다는 캐시되지만, `Enumerable.Count(source, pred)`가 `IEnumerable<T>`로 받아 `foreach`하므로 G와 똑같이 박싱된 열거자가 생긴다. G와 바이트 수가 같은 것이 그 증거.
  - K ✗ (O로 예측, 실제 0): 인자 없는 `params` 호출은 Roslyn이 `Array.Empty<int>()`로 낮춘다. 첫 프레임의 32 B는 그 정적 빈 배열(`int[0]`)을 한 번 만드는 비용.
  - C: 할당 이유를 클로저로만 답했지만, 실제로는 클로저(20~24 B) + 델리게이트(128 B) 2개. 바이트의 대부분은 델리게이트다.
  - G: 후속 질문에서 F가 0인 이유("`List<T>`에 자체 struct `Enumerator`가 있다")는 맞음. G의 40 B를 "yield 상태"로 답했으나, 실제로는 **같은 struct `Enumerator`가 박싱된 것**이다. `List<T>`는 `yield` 이터레이터를 쓰지 않는다. `IEnumerable<T>.GetEnumerator()` 명시적 구현이 `new Enumerator(this)`를 `IEnumerator<T>`로 반환하면서 박싱한다 (아래 발췌). `foreach`는 정적 타입의 `GetEnumerator()`를 고르므로 F는 struct를 반환하는 public 메서드를, G는 인터페이스 메서드를 탄다.
- 추가 질문(M) 비교: "호출마다 1번"은 맞음. 이유("`_target` 때문")는 어긋남 — `_target`을 안 읽는 N도 똑같이 128 B·1회다. 원인은 람다가 **인스턴스 메서드를 호출해 `this`를 캡처**한다는 것. 이때 컴파일러는 클로저 클래스(`<>c__DisplayClass`)를 만들지 않고 람다 본문을 `AllocTargets`의 인스턴스 메서드(`<LambdaWrapsInstance>b__18_0`)로 옮긴 뒤 **`new Predicate<int>(this, 그 메서드)`** 를 매번 만든다 (아래 발췌). 할당되는 객체는 클로저가 아니라 델리게이트 하나. `<>c`의 static 캐시는 대상(`this`)이 없는 람다만 쓸 수 있으므로 M은 캐시되지 않는다.
  - 대처법 정리: 메서드 그룹(E·L)과 `this`를 캡처한 람다(M·N)는 매번 128 B. 델리게이트를 필드에 한 번 만들어 두면(O) 0, 델리게이트 대신 struct 제네릭 predicate(P)도 0. 인스턴스 메서드면 인스턴스 필드에 캐시한다.
- 한 단계 더:
  - **델리게이트 하나가 128 B**다. Mono/IL2CPP의 `Delegate`는 `method_ptr`, `invoke_impl`, `m_target`, `method`, `method_info`, `extra_arg` 등 필드가 많은 큰 객체다. 그래서 람다 비용은 캡처 변수 수보다 델리게이트 생성 여부가 좌우한다. 줄이려면 델리게이트를 필드에 한 번 캐시하거나, C# 9의 `static` 람다로 캡처 실수를 컴파일 오류로 막는다.
  - 크기가 백엔드마다 다르다: B 56/60, C 148/152, I 20/24 (Mono/IL2CPP). 클래스 인스턴스(클로저, `WaitForSeconds`)는 IL2CPP에서 8바이트 정렬된 크기로 잡히는 것으로 보이고, 박싱된 `int`(20 B)와 배열은 같다. 원인은 확인 필요 (`Il2CppClass::instance_size` 계산과 `GC Allocated In Frame`이 요청 크기를 세는지, Boehm 크기 클래스로 반올림한 크기를 세는지).
- 근거: 이 프로젝트 실측, IL2CPP 생성 코드(`Library/Bee/artifacts/WinPlayerBuildProgram/il2cppOutput/cpp/Generics__68.cpp`), C# 11 메서드 그룹 캐싱([What's new in C# 11 — Improved method group conversion to delegate](https://learn.microsoft.com/dotnet/csharp/whats-new/csharp-11#improved-method-group-conversion-to-delegate)), Unity 6 C# 버전([Unity 매뉴얼: C# compiler](https://docs.unity3d.com/6000.0/Documentation/Manual/csharp-compiler.html))

## 코드 발췌
IL2CPP가 생성한 `List<T>.IEnumerable<T>.GetEnumerator` (full sharing 본문, 이름 해시 생략):
```cpp
RuntimeObject* List_1_System_Collections_Generic_IEnumerableU3CTU3E_GetEnumerator_..._gshared (List_1_t...* __this, const RuntimeMethod* method)
{
    Enumerator_t... L_0;
    memset((&L_0), 0, sizeof(L_0));
    Enumerator__ctor_...((&L_0), __this, il2cpp_rgctx_method(method->klass->rgctx_data, 39));
    Enumerator_t... L_1 = L_0;
    RuntimeObject* L_2 = Box(il2cpp_rgctx_data_no_init(method->klass->rgctx_data, 38), &L_1);
    return (RuntimeObject*)L_2;
}
```

M `_list.FindIndex(x => IsMatch(x))` (`Labs.cpp`, 이름 해시 생략) — 람다 본문은 `AllocTargets` 자신의 메서드이고, 델리게이트 대상은 `__this`:
```cpp
Predicate_1_t...* L_2 = (Predicate_1_t...*)il2cpp_codegen_object_new(Predicate_1_t..._il2cpp_TypeInfo_var);
Predicate_1__ctor_...(L_2, __this, (intptr_t)((void*)AllocTargets_U3CLambdaWrapsInstanceU3Eb__18_0_..._RuntimeMethod_var), NULL);
L_3 = List_1_FindIndex_...(L_1, L_2, ...);
```
