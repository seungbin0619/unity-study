# 03-L05 `Update` 1만 개 vs 매니저 vs 커스텀 시스템

## 목표
같은 일을 하는 객체 1만 개를 매 프레임 갱신할 때, 엔진이 스크립트마다 `Update`를 부르는 방식과 매니지드 루프 하나로 부르는 방식(매니저 `MonoBehaviour`, 커스텀 PlayerLoop 시스템)의 비용 차이를 잰다. 빈 `Update`와 `Update`가 없는 컴포넌트도 비용이 드는지 함께 본다.

## 예측
- 질문: A(`MonoBehaviour` 1만 개 각자 `Update`) / B(매니저 하나가 일반 C# 객체 1만 개를 `for`로 `Tick`) / C(커스텀 PlayerLoop 시스템 하나가 1만 개를 `for`로 `Tick`)를 빠른 순으로, 이유와 함께. (배수 질문은 학습자 요청으로 철회)
- 학습자 답 (실행 전, 원문 그대로): "B=C<A일 듯. A는 c++ 네이티브에서 C# 경계로 인해 시간이 오래 걸릴 것 같음. 몇 배일지는 잘 모르겠음."

## 실습 구성
- 코드
  - `Tickers.cs` — 객체마다 하는 일은 모두 `_value += UpdateCostLab.Dt` (프레임마다 한 번 정해 둔 정적 값. `Time.deltaTime`을 객체마다 읽으면 그 자체가 경계 호출이라 조건이 달라진다)
    - `UpdateTicker`(`Update`에서 일함) / `EmptyUpdateTicker`(빈 `Update`) / `NoUpdateTicker`(`Update` 없음) / `ManualTicker`(`Update` 없이 `Tick()`만 있는 `MonoBehaviour`) / `PlainTicker`(일반 `sealed class`) / `TickManager`(배열을 `for`로 돌며 `Tick()`)
  - `UpdateCostLab.cs` — PlayerLoop `Update` 단계의 `ScriptRunBehaviourUpdate` 바로 앞·뒤에 타이머 시스템을 끼우고, 그 사이에 (C에서만) 커스텀 시스템을 넣는다. 측정 구간 = `ScriptRunBehaviourUpdate` + 커스텀 시스템. 조건마다 GameObject 1만 개(컴포넌트 하나씩)를 만들고 워밍업 30프레임 뒤 300프레임 기록, vSync 끔
  - 조건: `0 baseline`(아무것도 없음) / `A Update x10000` / `A0 empty Update x10000` / `N no Update x10000` / `B manager -> plain x10000` / `B2 manager -> MonoBehaviour.Tick x10000` / `C PlayerLoop system -> plain x10000`
- 실행: 에디터 메뉴 `Labs/03/L05 Update Cost` 또는 플레이어 `-lab 03-L05`

## 결과
2026-11-12, 배치모드 Development 플레이어 (Incremental GC On), 조건마다 300프레임. 측정 구간 = `ScriptRunBehaviourUpdate` + 커스텀 시스템. 에디터는 실행하지 않음.

| 조건 | Mono 중앙값 (ms) | Mono 객체당 (ns) | IL2CPP 중앙값 (ms) | IL2CPP 객체당 (ns) |
|---|---|---|---|---|
| 0 기준선 | 0.0015 | – | 0.0013 | – |
| **A** `Update` × 1만 | **1.10** (재측정 0.95) | **약 95\~110** | **0.69** (재측정 0.70) | **약 69** |
| A0 빈 `Update` × 1만 | 1.01 | 약 100 | 0.68 | 약 68 |
| N `Update` 없음 × 1만 | 0.0015 | 0 | 0.0013 | 0 |
| **B** 매니저 → 일반 객체 | **0.012** | **1.2** | **0.010** | **1.0** |
| B2 매니저 → `MonoBehaviour.Tick` | 0.012 | 1.2 | 0.011 | 1.1 |
| **C** PlayerLoop 시스템 → 일반 객체 | **0.012** | **1.2** | **0.011** | **1.1** |

- 객체당 = (중앙값 − 기준선) ÷ 1만. 평균도 중앙값과 거의 같음.
- 최대(300프레임 중): A Mono 2.4 ms·IL2CPP 1.7 ms, B·B2·C는 모두 0.1 ms 이하.
- GC 할당은 모든 조건에서 프레임당 0 B.
- 순서 효과 확인용으로 마지막에 다시 잰 기준선·A도 처음과 같은 수준.

원본: `Notes/results/03-L05_Mono_2026-11-12.csv`, `03-L05_IL2CPP_2026-11-12.csv`

## 해석
- **예측과 비교: 맞음.** 순서 B ≈ C < A, 이유(A는 객체마다 네이티브 → 매니지드 경계를 넘음)도 맞다. B와 C는 측정 오차 안에서 같다. 차이는 Mono 약 90배, IL2CPP 약 65배였다.
- **A의 비용은 거의 전부 "호출하는 방식"에서 나온다.** 빈 `Update`(A0)가 일하는 `Update`(A)와 거의 같다. 객체당 일 자체는 약 1 ns(B)이고, 나머지 약 70\~100 ns는 엔진이 스크립트 하나를 부를 때마다 드는 고정비다. 고정비에 들어가는 것:
  - 네이티브 → 매니지드 호출 (Mono는 runtime invoke 경로, IL2CPP는 메서드 포인터 호출 + 예외 처리 틀)
  - 객체마다 하는 검사 (파괴됐는지, 컴포넌트가 enabled인지, GameObject가 active인지)
  - 스크립트마다 예외를 잡아 다음 스크립트를 계속 부르는 것
  - (확인 필요) 흩어진 네이티브 객체를 차례로 따라가며 생기는 캐시 미스. 정확한 분해는 이 실습으로는 못 한다.
- **`Update`가 없으면 비용이 0이다(N).** 엔진은 스크립트를 로드할 때 `Update`가 정의되어 있는지 보고, 정의된 컴포넌트만 호출 목록에 넣는다. 그래서 빈 `Update`를 남겨 두면 그대로 비용이 된다. Unity 템플릿이 만드는 빈 `Update()`를 지워야 하는 이유다.
- **B2 ≈ B: 비싼 건 `MonoBehaviour`라서가 아니라 엔진이 불러서다.** 같은 `MonoBehaviour` 1만 개라도 매니지드 매니저가 직접 `Tick()`을 부르면 일반 메서드 호출(약 1 ns)이다. 경계를 넘는 횟수가 1만 번에서 1번으로 줄어드는 것이 핵심이다.
- **C ≈ B: 커스텀 PlayerLoop 시스템의 장점은 속도가 아니라 위치다.** 둘 다 경계를 프레임당 한 번 넘는다. C는 `MonoBehaviour`·GameObject 없이 돌고, `PreLateUpdate` 같은 원하는 단계에 끼울 수 있다는 점이 다르다.
- **한 단계 더**
  - **매니저 패턴의 대가.** A에서는 엔진이 enabled·active·파괴 여부를 확인해 주고, 한 스크립트가 예외를 던져도 다음 스크립트는 계속 불린다. B·C에서는 이것을 직접 책임져야 한다. 루프 안에서 예외가 하나 나면 나머지 객체가 그 프레임에 갱신되지 않는다. 등록·해제(`OnEnable`/`OnDisable`)를 잊으면 파괴된 객체를 계속 부르게 된다(1장 가짜 null). 순서도 `Script Execution Order`가 아니라 배열 순서로 정해진다.
  - **IL2CPP가 Mono보다 약 30% 싸다**(69 vs 100 ns). 매니지드 코드 자체(B)는 둘이 비슷하므로, 차이는 엔진이 스크립트를 부르는 경로에 있다. 1장 01-L05에서 본 "백엔드마다 경계 비용이 다르다"와 같은 축이다.
  - **실무 감각.** 1만 개면 Mono에서 프레임당 약 1 ms(60fps 예산 16.7 ms의 6%)다. 수백 개 수준이면 무시해도 되지만, 총알·군중·UI 요소처럼 수천 개가 되는 것은 매니저로 모으는 것이 효과적이다. ECS/DOTS가 노리는 것도 이 "객체마다 호출" 구조를 없애는 것이다.
- **근거**: 이 프로젝트의 실측. [Unity Blog: 10000 Update() calls (2015)](https://unity.com/blog/engine-platform/10000-update-calls) — 엔진이 매 호출마다 하는 일(경계 넘기, 객체 검사, 예외 처리)을 설명한다. 오래된 글이라 Unity 6에서의 세부는 확인 필요지만, 이번 실측의 방향(빈 `Update`도 비용, 매니저가 훨씬 빠름)과 같다.
