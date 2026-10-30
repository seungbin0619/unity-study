# 01-L07 IL2CPP null·경계 검사와 `Il2CppSetOption`

## 목표
C#에서는 보이지 않는 런타임 검사(null 검사(null check), 배열 경계 검사(array bounds check))가 IL2CPP 생성 C++에서 어디에, 몇 개 들어가는지 직접 센다. `[Il2CppSetOption]`으로 검사를 껐을 때 생성 코드와 원소당 시간이 어떻게 달라지는지 확인한다.

## 예측
- 질문: [CheckTargets.cs](CheckTargets.cs)의 세 메서드를 IL2CPP로 변환했을 때, 각 메서드의 생성 C++에 **`NullCheck(...)` 호출이 몇 개** 들어갈까?
  - (A) `ReadChain(ChainNode n) => n.Next.Next.Value;`
  - (B) `SumArray(int[] a)`: `for (i < a.Length) sum += a[i];`
  - (C) `ChainNode.DoubleValue() => Value + Value;`
- 학습자 답 (실행 전, 원문 그대로, 2026-10-30):
  ```
  (A) n, n.Next, n.Next.Next -> 3개
  (B) a.Length, a[i] -> 2개
  (C) 0개 (참조 x)
  ```

- 추가 질문 (2026-10-30, 1차 결과를 본 뒤): 개발자가 직접 `if (a == null) return 0;`을 넣은 (E) `SumArrayGuarded`의 생성 C++에는 `NullCheck(...)`가 몇 개 남을까? ((B)는 2개)
- 학습자 답 (실행 전, 원문 그대로, 2026-10-30):
  ```
  여전히 2개, 동일 이유.
  ```

## 실습 구성
- 코드: `CheckTargets.cs` — 검사 대상 메서드 A~E (D는 B에 `Il2CppSetOption(NullChecks/ArrayBoundsChecks, false)`를 붙인 버전, E는 B 앞에 직접 null 검사를 넣은 버전)
- 코드: `NullCheckLab.cs` — 대상 메서드를 호출해 스트리핑을 막고, null 체인 예외 타입과 B·D·E의 원소당 시간(100만 원소 × 10회, 워밍업 3, 표본 20)을 기록
- `Il2CppSetOptionAttribute.cs` — Unity 설치 폴더 `Editor/Data/il2cpp`에서 복사
- 실행: IL2CPP Development 빌드 후 `Builds/IL2CPP/Labs.exe -lab 01-L07 -batchmode -nographics -logFile Notes/results/player_01-L07.log`, 생성 코드는 `Library/Bee` 아래 `il2cppOutput`에서 메서드 이름으로 검색

## 결과
생성 코드: `Library/Bee/artifacts/WinPlayerBuildProgram/il2cppOutput/cpp/Labs.cpp`

| 메서드 | `NullCheck` 개수 (정적) | 경계 검사 |
|---|---|---|
| (A) `ReadChain` | 3 (`n`, `n.Next`, `n.Next.Next`) | — |
| (B) `SumArray` | 2 (루프 조건의 `a.Length`, 본문의 `a[i]`) — 둘 다 루프 안 | `GetAt()` 안의 `IL2CPP_ARRAY_BOUNDS_CHECK` |
| (C) `ChainNode.DoubleValue` | 0 (`__this->___Value`를 바로 읽음) | — |
| (D) `SumArrayUnchecked` | 0 | 없음 (`GetAtUnchecked()`) |
| (E) `SumArrayGuarded` | 2 (B와 같음, 직접 넣은 `a == null` 비교와 별개로 남음) | `GetAt()` |

| 실행일 | 백엔드 | Incremental GC | 빌드 | 조건 | 측정값 |
|---|---|---|---|---|---|
| 2026-10-30 | IL2CPP | On | Development | `ReadChain(new ChainNode())` | `NullReferenceException` |
| 2026-10-30 | IL2CPP | On | Development | (B) `SumArray` | 평균 0.482 ns/원소 (최소 0.353, 최대 0.759) |
| 2026-10-30 | IL2CPP | On | Development | (D) `SumArrayUnchecked` | 평균 0.168 ns/원소 (최소 0.150, 최대 0.211) |
| 2026-10-30 (2차, E 추가) | IL2CPP | On | Development | (B) `SumArray` | 평균 0.551 ns/원소 (최소 0.436, 최대 0.677) |
| 2026-10-30 (2차, E 추가) | IL2CPP | On | Development | (D) `SumArrayUnchecked` | 평균 0.302 ns/원소 (최소 0.268, 최대 0.368) |
| 2026-10-30 (2차, E 추가) | IL2CPP | On | Development | (E) `SumArrayGuarded` | 평균 0.724 ns/원소 (최소 0.626, 최대 0.878) |

원본: `Notes/results/01-L07_IL2CPP_2026-10-30.csv`

## 해석
- 예측과 비교: 개수 3/2/0 모두 맞음. (C)의 이유를 처음에 "참조 x"라고 답했으나 `Value`는 `this->Value`라서 역참조가 맞다. 후속 질문에서 "this가 null이면 DoubleValue가 호출될 일이 없다"로 정답. 실제로 호출하는 쪽(`NullCheckLab`)에서 `NullCheck(L_22)` 뒤에 `ChainNode_DoubleValue(L_22)`를 부른다. 검사가 사라진 게 아니라 **호출하는 쪽(callvirt 의미론)으로 옮겨간** 것이다.
- 정적 개수 vs 실행 횟수: (B)의 2개는 둘 다 루프 안에 있어서, 원소 하나마다 `NullCheck` 2번 + 경계 검사 1번이 실행된다. IL2CPP는 IL 명령어를 거의 1:1로 옮길 뿐이고, 루프 밖으로 끌어올리지(hoist) 않는다.
- 시간: 검사를 끈 (D)가 원소당 0.168 ns, (B)는 0.482 ns로 약 2.9배 빨랐다 (Development 빌드, C++ 컴파일러 설정은 기본값이며 확인하지 않음). 검사에는 예외를 던지는 분기가 있어서 C++ 컴파일러의 루프 최적화(벡터화 등)를 막는 것으로 보인다 — 확인 필요 (디스어셈블리 비교로 검증 가능).
- 한 단계 더:
  - .NET의 RyuJIT은 `for (i < a.Length) a[i]` 형태에서 경계 검사를 제거한다(bounds check elimination). IL2CPP 생성 코드에는 이런 단계가 없고, 남는 검사를 없앨지는 C++ 컴파일러에 맡긴다. 그래서 핫 루프에서는 `Il2CppSetOption`이 의미 있는 차이를 만든다.
  - 대가: 검사를 끈 메서드에 null이나 범위 밖 인덱스가 들어오면 예외 대신 액세스 위반(access violation)으로 크래시하거나, 범위 밖 메모리를 조용히 읽고 쓴다. 입력이 보장되는 핫 루프에만, 메서드나 클래스 단위로 좁게 쓴다.
  - 경계 검사는 생성 코드에 매크로로 바로 보이지 않고 `GetAt()` 안에 들어 있다 (`GetAt` = `IL2CPP_ARRAY_BOUNDS_CHECK` + 읽기, `GetAtUnchecked` = 읽기만).
- 추가 질문 (E): "여전히 2개, 동일 이유" — 정답. IL2CPP는 IL 명령어 하나씩 변환하면서 앞에서 확인한 사실(`a != null`)을 뒤로 전달하지 않는다(데이터 흐름 분석 없음). 직접 넣은 `a == null` 비교는 일반 C# 코드로 따로 변환되고, `ldlen`·`ldelem` 앞의 `NullCheck`는 그대로 남는다.
  - `NullCheck`와 `IL2CPP_ARRAY_BOUNDS_CHECK`는 `il2cpp-codegen.h`의 inline 함수·매크로라서, C++ 컴파일러가 중복을 증명하면 지울 *수는* 있다. 그러나 2차 측정에서 E(0.72 ns)는 B(0.55 ns)보다 빠르지 않았고 D(0.30 ns)와는 거리가 멀다. 직접 검사해도 성능상 이득은 없었다.
  - 측정 한계: 같은 빌드 안에서도 1차와 2차의 D가 0.17 → 0.30 ns로 흔들렸고, B → D → E 순서로 한 번씩만 쟀다. B와 E의 차이(E가 더 느림)는 순서·잡음일 수 있으니 해석하지 않는다. "E ≈ B ≫ D"까지만 결론으로 삼는다.
  - 제어 단위: `Il2CppSetOption`은 어셈블리·클래스·구조체·메서드·프로퍼티·델리게이트 단위다. 문장이나 블록 단위는 없으므로 핫 루프를 작은 메서드로 떼어서 붙인다. 람다·이터레이터·`async`처럼 컴파일러가 만든 메서드에 바깥 설정이 적용되는지는 확인 필요.
- `LabBuilder` 수정 확인: 2차 빌드 후 `ProjectSettings.asset`의 `Standalone`이 `0`(Mono)으로 저장되어 있었다.
- 근거: 이 프로젝트의 IL2CPP 생성 코드 (`Labs.cpp`, `GenericMethods__1.cpp`의 `GetAt`), Unity 설치 폴더 `Il2CppSetOptionAttribute.cs`의 주석, [Unity 매뉴얼: IL2CPP 런타임 검사](https://docs.unity3d.com/6000.3/Documentation/Manual/scripting-backends-il2cpp-checks.html) (링크 확인 필요)

## 코드 발췌
```cpp
// (A) ReadChain — 역참조마다 NullCheck
NullCheck(L_0);  L_1 = L_0->___Next;
NullCheck(L_1);  L_2 = L_1->___Next;
NullCheck(L_2);  L_3 = L_2->___Value;

// (C) DoubleValue — this는 검사 없이 바로 읽음
int32_t L_0 = __this->___Value;
int32_t L_1 = __this->___Value;

// (B) SumArray 루프 본문 / 조건
NullCheck(L_1);  L_4 = (L_1)->GetAt(L_3);                        // GetAt 안에 IL2CPP_ARRAY_BOUNDS_CHECK
NullCheck(L_7);  V_2 = L_6 < ((RuntimeArray*)L_7)->max_length;

// (D) SumArrayUnchecked — NullCheck 없음, GetAtUnchecked
L_4 = (L_1)->GetAtUnchecked(L_3);
```
