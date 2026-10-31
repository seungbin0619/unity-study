# 02-L03 `Instantiate`/`Destroy` vs 풀링

## 목표
객체를 매 프레임 만들고 파괴하는 방식과 풀링(`SetActive`)을 Development 플레이어에서 비교한다. 매니지드 GC 할당·GC 발생 주기·작업 시간을 함께 보고, `Instantiate` 비용 중 무엇이 GC 문제이고 무엇이 아닌지 구분한다.

## 예측
- 질문: 프레임마다 객체 100개(빈 GameObject + `BoxCollider` + 빈 MonoBehaviour)를 꺼내고 직전 프레임의 100개를 돌려놓는다. A(`Instantiate`/`Destroy`)와 B(풀에서 `SetActive(true/false)`)의 **프레임당 매니지드 GC 할당**은 각각 대략 얼마이고, **작업 시간**은 어느 쪽이 몇 배쯤 클까?
- 학습자 답 (실행 전, 원문 그대로): "A는 프레임당 엄청많이, B는 최초프레임 제외 안나겠지 작업시간도 B가 훨씬빠를거고.. 근데 이걸 인간적으로 어떻게 몇배쯤을 추측해"

## 실습 구성
- 코드: `PoolingLab.cs` — 방식마다 워밍업 30프레임 + 측정 600프레임. 프레임마다 작업 시간(`Stopwatch`), 직전 프레임의 "GC Allocated In Frame", `GC.CollectionCount(0)` 변화량을 기록
- 코드: `PooledProbe.cs` — 생성되는 객체에 붙는 빈 컴포넌트 (`OnEnable`만 있음)
- 실행: 에디터 메뉴 `Labs/02/L03 Pooling` 또는 플레이어 `-lab 02-L03`

## 결과
600프레임, 프레임당 100개. 작업 시간은 `Stopwatch`로 잰 step 구간만 (`Destroy`의 실제 파괴는 프레임 끝에 일어나므로 포함되지 않음)

| 실행일 | 백엔드 | Incremental GC | 빌드 | 조건 | 측정값 |
|---|---|---|---|---|---|
| 2026-10-31 | Mono | On | Development | A `Instantiate`/`Destroy` | 할당 6,400 B·200회/프레임 (객체당 64 B·2회), 작업 avg 0.912 / p50 0.786 / p99 2.300 / max 3.026 ms |
| 2026-10-31 | Mono | On | Development | A의 GC | 600프레임에 GC 23회 (약 26프레임 ≈ 166 KB마다). 작업 중 GC가 낀 19프레임 avg 1.630 ms vs 나머지 0.889 ms. GC Used 1.10 → 1.32 MB |
| 2026-10-31 | Mono | On | Development | B 풀 `SetActive` | 할당 0, GC 0회, 작업 avg 0.426 / p50 0.350 / p99 0.986 / max 1.369 ms |
| 2026-10-31 | IL2CPP | On | Development | A `Instantiate`/`Destroy` | 할당 6,400 B·200회/프레임, 작업 avg 0.851 / p50 0.758 / p99 1.867 / max 2.819 ms |
| 2026-10-31 | IL2CPP | On | Development | A의 GC | 600프레임에 GC 19회 (약 31프레임 ≈ 200 KB마다). 작업 중 GC 13프레임 avg 1.575 ms vs 나머지 0.835 ms. GC Used 1.44 → 1.58 MB |
| 2026-10-31 | IL2CPP | On | Development | B 풀 `SetActive` | 할당 0, GC 0회, 작업 avg 0.374 / p50 0.327 / p99 0.768 / max 1.011 ms |

원본: `Notes/results/02-L03_Mono_2026-10-31.csv`, `02-L03_IL2CPP_2026-10-31.csv` (프레임별 `할당|ms|GC횟수` 포함)

## 해석
- 예측과 비교:
  - B 할당 0, GC 0 ✅ (풀 생성은 측정 전에 함). B가 빠름 ✅ — A가 평균 약 2.1배(Mono)·2.3배(IL2CPP).
  - A "프레임당 엄청 많이" — 실제로는 **객체당 64 B, 프레임당 6.4 KB**로 02-L01의 람다 몇 개 수준이다. `Instantiate`가 만드는 것의 대부분(Transform, Collider, 컴포넌트 데이터)은 **네이티브 메모리**라서 GC 힙에 잡히지 않는다. 매니지드 쪽에는 C# 래퍼 같은 작은 객체만 생긴다(01장의 래퍼 구조).
- 그래서 풀링의 이득은 "GC 할당 제거"보다 **CPU 시간**(네이티브 생성·파괴, 생명주기 콜백, 물리 등록) 쪽이 크다. 이 실습에서 GC가 낀 프레임의 추가 비용은 약 0.7 ms인데, 그건 GC Used가 1~1.5 MB로 작은 테스트 힙이라서다.
- 한 단계 더:
  - **GC 한 번의 비용은 쓰레기 양이 아니라 살아 있는 힙 크기에 비례한다.** Boehm은 마크(mark)·스윕(sweep) 방식이라, 살아 있는 객체를 모두 훑는다. 실제 게임처럼 매니지드 힙이 수십~수백 MB면 같은 6.4 KB/프레임이 같은 주기로 GC를 일으켜도 한 번에 수 ms 이상이 될 수 있다 (02-L05에서 힙 크기별로 실측 예정).
  - A의 작업 시간은 **과소평가**다. `Destroy`는 프레임 끝에 실제로 파괴하므로 그 비용은 `Stopwatch` 밖에 있다. 프레임 전체 시간으로 비교하면 차이가 더 클 것으로 예상 (미측정).
  - 풀링도 공짜가 아니다: `SetActive` 200회에 약 0.4 ms. `OnEnable`/`OnDisable` 콜백, 콜라이더의 물리 씬 등록·해제가 매번 일어난다. 렌더러만 끄거나 위치만 옮기는 식의 더 가벼운 "반납"이 있는지 객체 종류에 따라 고른다.
  - 객체당 매니지드 할당 2회·64 B의 정체는 **확인 필요**. 후보는 `Instantiate`가 돌려주는 `GameObject` 래퍼(필드는 `m_CachedPtr` 하나 → 24 B)와, 스크립트 컴포넌트라 항상 매니지드 인스턴스가 필요한 `PooledProbe`(28 B). 합이 52 B라 64 B와 맞지 않는다. MonoBehaviour 없는 템플릿으로 한 케이스를 더 재면 가를 수 있다.
- 근거: 이 프로젝트 실측, IL2CPP 생성 코드의 `UnityEngine.Object` 필드(`intptr_t ___m_CachedPtr` 하나)와 `PooledProbe`(`int32_t ___EnableCount`)
