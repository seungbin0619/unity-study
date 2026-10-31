# 02-L02 클로저 할당 시점

## 목표
람다가 들어 있는 분기를 실행하지 않아도 할당이 생기는지 확인한다. 컴파일러가 클로저 객체와 델리게이트를 각각 **어느 지점에서** 만드는지 실측과 IL2CPP 생성 코드로 확인한다.

## 예측
- 질문: `ClosureTargets.cs`의 A~D를 `use = false`로 워밍업 후 호출할 때, 각각 호출마다 GC 할당이 생기는가? 생긴다면 무엇(클로저 / 델리게이트)이 몇 개인가? (참고: 02-L01에서 클로저 20~24 B, 델리게이트 128 B)
- 학습자 답 (실행 전, 원문 그대로): "전혀 모르겠는데" → 힌트(캡처된 변수는 클로저 클래스의 필드가 되므로 변수에 대한 모든 읽기·쓰기가 클로저 객체를 거친다, 분기 없는 lowering 예시, D는 02-L01 M을 떠올리기) 후 "여전히 전혀 모르겠어 알려줘" → 풀이

## 실습 구성
- 코드: `ClosureTargets.cs` — 캡처 대상(매개변수 / 맨 위 지역 변수 / 분기 안 지역 변수 / `this`)만 다른 4개 메서드
- 코드: `ClosureTimingLab.cs` — A~D를 `use = false`와 `true`로 각각, 워밍업 3프레임 + 측정 10프레임, 프레임당 100회 호출 (02-L01과 같은 방식)
- 실행: 에디터 메뉴 `Labs/02/L02 Closure Timing` 또는 플레이어 `-lab 02-L02`

## 결과
호출당 바이트 · 할당 횟수 (10프레임 평균, 모든 케이스 min = max). 보정: 0a 빈 호출 0, 0b `new byte[16]` 48 B·1 (두 백엔드 공통)

| 실행일 | 백엔드 | Incremental GC | 빌드 | 조건 | 측정값 |
|---|---|---|---|---|---|
| 2026-10-31 | Mono | On | Development | `use=false`: A 매개변수 · B 맨 위 지역 · C 분기 안 지역 · D `this`만 | 20 B·1 · 20 B·1 · 0 · 0 |
| 2026-10-31 | Mono | On | Development | `use=true`: A · B · C · D | 148 B·2 · 148 B·2 · 148 B·2 · 128 B·1 |
| 2026-10-31 | IL2CPP | On | Development | `use=false`: A · B · C · D | 24 B·1 · 24 B·1 · 0 · 0 |
| 2026-10-31 | IL2CPP | On | Development | `use=true`: A · B · C · D | 152 B·2 · 152 B·2 · 152 B·2 · 128 B·1 |

원본: `Notes/results/02-L02_Mono_2026-10-31.csv`, `02-L02_IL2CPP_2026-10-31.csv`

## 해석
- 예측과 비교: 학습자가 힌트 후에도 답하지 못해 풀이함.
- 규칙: **클로저 객체는 캡처된 변수가 선언된 스코프(scope)에 들어갈 때 만들어지고, 델리게이트는 람다 식이 평가되는 줄에서 만들어진다.**
  - 캡처된 변수는 지역 변수가 아니라 클로저 클래스(`<>c__DisplayClass`)의 필드가 된다. 그 변수에 대한 첫 대입(매개변수라면 인자 복사)부터 클로저를 거쳐야 하므로, 클로저는 그 변수의 스코프 시작에서 생성된다.
  - A(매개변수): 매개변수의 스코프는 메서드 전체 → 메서드에 들어가자마자 클로저를 만들고 `c.t = t`를 복사한 뒤 분기를 검사한다 (아래 발췌). `use=false`여도 클로저 1개.
  - B(맨 위 지역 변수): 스코프가 메서드 본문 → A와 같다.
  - C(분기 안 지역 변수): 스코프가 `if` 블록 → 블록에 들어갈 때만 생성. `use=false`면 0.
  - D(`this`만): 클로저 클래스가 없다. 람다는 `ClosureTargets`의 인스턴스 메서드가 되고 델리게이트(대상 `this`)만 람다 줄에서 생성 (02-L01 M과 같은 구조). `use=false`면 0, `true`면 128 B·1.
  - `use=true`에서는 A·B·C 모두 클로저 + 델리게이트 2개(Mono 148 = 20 + 128, IL2CPP 152 = 24 + 128).
- 실무 해법: 람다가 필요한 경로에서만 캡처 변수가 생기게 한다 — 캡처할 값을 분기 안의 지역 변수에 복사하거나(C처럼), 람다를 쓰는 부분을 별도 메서드로 빼서 그 메서드의 매개변수로 넘긴다. 매개변수를 직접 캡처하면 메서드의 어떤 경로를 타든 클로저가 생긴다.
- 한 단계 더:
  - 같은 스코프의 람다들은 **클로저 객체 하나를 공유**한다 (Roslyn 동작, 이 실습에서는 미측정). 그래서 오래 사는 델리게이트(이벤트 구독 등) 하나가 같은 스코프의 다른 캡처 변수까지 살려 둘 수 있다 — 의도치 않은 수명 연장(메모리 누수의 원인).
  - 매개변수 캡처는 메서드 입구에서 생성되므로, 메서드 맨 앞의 `if (!use) return;` 같은 조기 반환으로도 피할 수 없을 것으로 예상된다 (확인 필요, 실측으로 검증 가능).
- 근거: 이 프로젝트 실측, IL2CPP 생성 코드(`Library/Bee/artifacts/WinPlayerBuildProgram/il2cppOutput/cpp/Labs.cpp`)

## 코드 발췌
A `CaptureParameter(int t, bool use)` — 분기 검사(`if (!L_4)`)보다 클로저 생성이 먼저:
```cpp
U3CU3Ec__DisplayClass3_0_t...* L_0 = (...)il2cpp_codegen_object_new(U3CU3Ec__DisplayClass3_0_t..._il2cpp_TypeInfo_var);
U3CU3Ec__DisplayClass3_0__ctor_...(L_0, NULL);
V_0 = L_0;
L_1->___t = ___0_t;          // 매개변수를 클로저 필드로 복사
bool L_4 = ___1_use;
if (!L_4) { goto IL_0038; }  // 여기서야 분기
```

C `CaptureBranchLocal(bool use)` — 분기 검사 뒤, 블록 안에서 생성:
```cpp
bool L_1 = ___0_use;
if (!L_1) { goto IL_003f; }
U3CU3Ec__DisplayClass5_0_t...* L_2 = (...)il2cpp_codegen_object_new(U3CU3Ec__DisplayClass5_0_t..._il2cpp_TypeInfo_var);
L_3->___t = __this->____target;
```
