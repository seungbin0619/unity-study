# 02-L06 Incremental GC On/Off 프레임 타임

## 목표
살아 있는 힙이 큰 상태에서 매 프레임 1 MB를 쓰레기로 만들 때, Incremental GC On/Off에 따라 프레임 시간 그래프가 어떻게 달라지는지 Development 플레이어에서 확인한다.

## 예측
- 질문: On과 Off 각각 600프레임의 프레임 시간 그래프 모양 (평소 프레임, GC가 일어나는 프레임·구간)
- 학습자 답 (실행 전, 원문 그대로):
  ```
  on일 땐 일정하게 유지 or 천천히 오르다가 Collect 후 약간 떨어지는 그래프
  off일 땐 한번 크게 올랐다가 다시 정상 속도까지 뚝 떨어지는 그래프,
  ```

## 실습 구성
- 코드: `IncrementalGcLab.cs` — `EnemyClass`(02-L05) 400만 개를 살려 두고, 매 프레임 `byte[1024]` × 1024를 할당. 워밍업 60프레임 뒤 600프레임 동안 프레임 시간(`Stopwatch`, 프레임 경계 간격), 할당 루프 시간, `GC.CollectionCount(0)` 증가분, GC Used를 기록. vSync 끔, 프레임 제한 없음
- 실행: 에디터 메뉴 `Labs/02/L06 Incremental GC` 또는 플레이어 `-lab 02-L06` (살아 있는 객체 수 변경: `-count N`)
- On/Off는 Player Settings `Use Incremental GC`를 바꾼 빌드로 비교한다 (CSV의 `GarbageCollector.isIncremental` 행으로 확인)

## 결과
600프레임, 살아 있는 `EnemyClass` 400만 개 (GC Used: Mono 154 MB, IL2CPP 216 MB). 시간 단위 ms. 타임 슬라이스 3 ms(`incrementalTimeSliceNanoseconds` 3,000,000)

| 실행일 | 백엔드 | Incremental GC | 빌드 | GC 횟수 | 평소 프레임 평균 | GC 프레임 | 큰 스파이크 | 600프레임 합계 |
|---|---|---|---|---|---|---|---|---|
| 2026-11-01 | Mono | **Off** | Development | 25 (24프레임마다) | 0.326 | **9.3~10.7** (전부) | GC마다 | 430 |
| 2026-11-01 | Mono | **On** | Development | 51 (11~12프레임마다) | 0.343 | 0.9~1.5 | 2회: 3.7 → 6.3, 4.6 → 5.5 (2프레임에 나뉨, 약 237프레임 간격) | 250 |
| 2026-11-01 | IL2CPP | **Off** | Development | 16 (37~38프레임마다) | 0.290 | **10.1~11.5** (전부) | GC마다 | 336 |
| 2026-11-01 | IL2CPP | **On** | Development | 43 (13~14프레임마다) | 0.298 | 1.1~1.3 | 2회: 3.8 → 6.5 → 1.9 (3프레임에 나뉨, 약 276프레임 간격) | 220 |

- Off의 GC 프레임은 거의 전부가 할당 루프 안(work ms ≈ frame ms) — 할당이 수집을 유발해 그 자리에서 멈춘다.
- On의 큰 스파이크 구간에서는 GC 횟수가 늘기 전에 GC Used가 평소 최고치(Mono 154 → 170 MB, IL2CPP 233 → 235 MB)를 넘는다 — 마킹이 끝날 때까지 할당이 계속 쌓인다.

원본: `Notes/results/02-L06_Mono_2026-11-01.csv`, `02-L06_IL2CPP_2026-11-01.csv` (Off 실행 뒤에 On 실행을 이어 붙임. `GarbageCollector.isIncremental` 행으로 구분, 프레임 행 값은 `frame ms|work ms|gc|used MB`)

## 해석
- 예측과 비교:
  - Off "한 번 크게 올랐다가 정상으로 뚝 떨어짐" — **맞음.** GC마다 10 ms 안팎의 단일 프레임 스파이크(평소의 약 30배), 바로 다음 프레임은 정상.
  - On "일정하게 유지 / 천천히 오르다 Collect 후 약간 떨어짐" — **대체로 맞음.** GC 프레임은 약 1 ms로 작은 혹 수준. 다만 두 가지는 예측에 없었다: (1) 약 20번에 한 번은 3~6 ms 스파이크가 2~3프레임 이어진다, (2) **On이 전체 시간도 더 적다**(250 vs 430 ms). "비용을 여러 프레임에 나눌 뿐 총량은 같거나 더 크다"는 일반적인 설명과 다르다.
- 왜 On이 총량까지 적은가 — Boehm의 incremental 모드는 **세대별(generational) 부분 수집**을 겸한다 (`il2cpp/external/bdwgc/alloc.c` `GC_maybe_gc`):
  - incremental이 아니면 수집마다 `GC_try_to_collect_inner` = 전체 마킹(world stop).
  - incremental이면 대부분이 **부분 수집(partial)**: 이전 수집의 마크 비트를 지우지 않고(`GC_clear_marks`는 full일 때만), 쓰기 배리어가 dirty로 표시한 페이지만 다시 훑는다. 400만 개 장수 객체는 한 번도 안 바뀌므로 다시 마킹하지 않는다 → GC 프레임 약 1 ms.
  - `n_partial_gcs >= GC_full_freq`(기본 19)이면 **전체 마킹(full)**. 측정의 스파이크 간격(Mono 237프레임 ≈ 12프레임 × 20회, IL2CPP 276 ≈ 14 × 20)과 맞는다. 이 full 마킹이 시간 제한(`GC_set_time_limit`, `BoehmGC.cpp`)에 걸려 여러 프레임으로 나뉜 것이 3~6 ms × 2~3프레임 구간이다 (합계 ≈ Off의 10 ms).
  - 01-L09에서 잰 쓰기 배리어 비용이 바로 이 dirty 표시의 값이다. 배리어가 없으면 부분 수집이 "바뀐 곳"을 알 수 없다.
- 한 단계 더:
  - **슬라이스 3 ms는 상한이 아니다.** On 스파이크 프레임이 6.3~6.8 ms였다. 할당 경로 안에서 하는 마킹(work ms 3.4~4.5)과 프레임 끝 슬라이스가 합쳐진 것으로 보인다 (나눠지는 지점은 확인 필요). 60 fps(16.7 ms) 예산에서 이 정도는 숨지만 120 fps(8.3 ms)에서는 보인다.
  - **On이면 GC가 더 자주 돈다**(Mono 25 → 51회). 부분 수집이 덜 회수하므로 다음 트리거가 빨리 온다. 장수 객체가 많고 쓰레기가 꾸준한 이 실험은 incremental에 유리한 조건이다. 장수 객체의 참조를 매 프레임 바꾸는 코드라면 dirty 페이지가 많아져 부분 수집 이득이 줄어든다 (미측정).
  - **full 마킹 중 힙이 자란다.** 마킹이 여러 프레임에 걸치면 그동안의 할당이 회수되지 못해 Used가 평소 최고치를 넘는다. 메모리 예산이 빠듯하면 이것도 고려해야 한다.
  - Mono도 같은 패턴(약 20회 주기의 스파이크)이므로 같은 Boehm 로직으로 보이지만, Mono 런타임 소스로는 확인하지 않음 (확인 필요).
- 추가 (학습자 질문, 2026-11-01) — GC는 언제 도는가: 주기가 아니라 **할당량 임계치**. `alloc.c` `min_bytes_allocd()`:
  - 임계치 = (2 × 참조 있는 블록 사용량 + 참조 없는(atomic) 블록 사용량 / 4 + 루트) / `GC_free_space_divisor`(기본 3). incremental이면 다시 ÷ 2. 마지막 수집 이후 할당이 이것을 넘으면 다음 할당에서 수집.
  - 이 실험 대입 (Mono Off): `_live` 배열 30.5 MB(참조 있음) × 2 + `EnemyClass` 122 MB(atomic) / 4 ≈ 92 MB → ÷ 3 ≈ 31 MB. 실측 24프레임 × 1.33 MB ≈ 32 MB. On이면 ÷ 2 ≈ 15 MB, 실측 11.5프레임 × 1.33 ≈ 15 MB. 둘 다 맞음.
  - IL2CPP는 같은 계산(≈ 36 MB / 18 MB)과 실측(약 50 MB / 18 MB)의 Off 쪽이 어긋남. IL2CPP 빌드의 `GC_FREE_SPACE_DIVISOR` 정의나 루트 크기 차이일 수 있음 (확인 필요).
  - 함의: 살아 있는 힙이 클수록 GC가 덜 자주 돌지만 한 번이 비싸다. 참조 있는 데이터는 atomic 데이터보다 임계치 계산에서 8배 무겁게 친다 — 02-L05의 "GC가 읽어야 하는 바이트"와 같은 기준.
- 근거: 이 프로젝트 실측. `Editor/Data/il2cpp/external/bdwgc/alloc.c`(`GC_full_freq = 19`, `GC_maybe_gc`, `min_bytes_allocd`), `libil2cpp/gc/BoehmGC.cpp`(`GC_enable_incremental`, `GC_set_time_limit`)

## 코드 발췌
```c
/* il2cpp/external/bdwgc/alloc.c — GC_maybe_gc */
if (!GC_incremental) {
    GC_try_to_collect_inner(GC_never_stop_func);   /* Off: 매번 전체 마킹 */
    ...
} else {
    if (GC_need_full_gc || n_partial_gcs >= GC_full_freq) {   /* 기본 19 */
        ...
        GC_clear_marks();                          /* full: 마크를 지우고 처음부터 */
        n_partial_gcs = 0;
    } else {
        n_partial_gcs++;                           /* partial: 이전 마크 유지, dirty 페이지만 */
    }
}
```
