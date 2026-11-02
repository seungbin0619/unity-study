# 02-L07 `GarbageCollector.Mode.Manual`과 `CollectIncremental`

## 목표
자동 수집을 끄고(`GCMode = Manual`) 매 프레임 `CollectIncremental(slice)`로 GC를 직접 진행할 때, slice 크기에 따라 GC가 프레임에 주는 시간과 힙 사용량이 어떻게 달라지는지 Development 플레이어에서 확인한다.

## 예측
- 질문: 02-L06 조건(살아 있는 `EnemyClass` 400만 개, 매 프레임 1 MB 쓰레기, Incremental GC On)에서 `Manual` 모드로 매 프레임 끝에 `CollectIncremental(slice)`를 한 번 부르고 slice를 3 ms → 1 ms → 0.1 ms로 줄일 때, (a) GC 때문에 생기는 프레임 시간과 (b) GC Used는 어떻게 달라지나? 0.1 ms의 그래프 모양은?
- 학습자 답 (실행 전, 원문 그대로):
  ```
  slice를 줄여나갈때 (a) 증가, (b) 3ms에서는 점차 감소, 0.1로 갈수록 감소폭이 줄어들다가 결국엔 그래프가 증가하는쪽으로
  ```
- 결과를 본 뒤 확인: 학습자는 slice를 "한 번에 쓰는 시간 예산"이 아니라 **"3 ms마다 실행한다"(호출 간격)**로 읽고 답했다. 질문이 "매 프레임 한 번 부르고"와 "slice"를 함께 써서 모호했다 (출제 문제)
- 추가 질문 (2026-11-02, 학습자 해석을 실험으로): slice 3 ms 고정, **호출 간격 N = 1 → 5 → 20 → 60프레임** (N프레임마다 `CollectIncremental(3 ms)`, 반환값이 `true`면 다음 프레임에도 이어 부름). (a) 600프레임 동안 GC 총 시간, (b) 수집이 일어나는 프레임 하나의 GC 시간, (c) GC Used 그래프 모양은?
- 학습자 답 (실행 전, 원문 그대로):
  ```
  (a) 감소, (b) 증가, (c) 이전과동일
  ```

## 실습 구성
- 코드: `ManualGcLab.cs` — 02-L06의 살아 있는 힙(`EnemyClass` 400만 개)과 매 프레임 `byte[1024]` × 1024 할당을 그대로 쓰고, 살아 있는 힙을 만든 뒤 `GCMode = Manual`로 바꿔 매 프레임 할당 직후 `CollectIncremental(slice)`를 한 번 부른다. 워밍업 60프레임 뒤 600프레임 동안 프레임 시간, 할당 루프 시간, `CollectIncremental` 시간과 반환값, `GC.CollectionCount(0)` 증가분, GC Used를 기록
- 실행: 에디터 메뉴 `Labs/02/L07 Manual GC` 또는 플레이어 `-lab 02-L07 -slice <나노초>` (기본 3,000,000. `-slice 0`이면 Manual로 바꾸지 않은 Enabled 기준선)
- 살아 있는 힙을 만든 뒤에 모드를 바꾼다. 같은 조건을 두 번씩 실행했다
- 호출 간격: `-interval N` (기본 1). N프레임마다 호출을 시작하고, 반환값이 `true`인 동안은 매 프레임 이어 부른다

## 결과
600프레임, 살아 있는 `EnemyClass` 400만 개, 매 프레임 1 MB 쓰레기, Incremental GC On, Development 빌드. 시간 단위 ms. 값은 2회 실행 범위 (Enabled 기준선은 02-L06과 같은 조건)

| 실행일 | 백엔드 | 조건 | GC 횟수 | `CollectIncremental` 중앙값 | full 시작 프레임의 `CollectIncremental` | 다음 프레임 할당 루프 | 프레임 최대 | 600프레임 합계 | 그중 `CollectIncremental` | GC Used (MB) |
|---|---|---|---|---|---|---|---|---|---|---|
| 2026-11-02 | Mono | Enabled (기준선) | 51 | - | - | - | 10.1~10.8 | 367~426 | - | 155~172 |
| 2026-11-02 | Mono | Manual 3 ms | **600** | 0.60~0.66 | 6.4~7.0 | 5.6~7.7 | 10.8 | 1,001~1,059 | 592~624 | **153.6~155.0** |
| 2026-11-02 | Mono | Manual 1 ms | **600** | 0.62 | 4.3~4.4 | 5.6~9.3 | 10.8~12.3 | 1,005 | 525~534 | **153.6~155.0** |
| 2026-11-02 | Mono | Manual 0.1 ms | **600** | 0.62~0.63 | 3.4~3.6 | 5.5~8.1 | 11.5~13.1 | 1,023~1,093 | 473~501 | **153.6~156.3** |
| 2026-11-02 | IL2CPP | Enabled (기준선) | 43 | - | - | - | 9.1~10.8 | 357 | - | 216~235 |
| 2026-11-02 | IL2CPP | Manual 3 ms | **600** | 1.05 | 6.4~6.6 | 5.7~7.0 | 12.1~12.4 | 1,304~1,309 | 856~857 | **215.6~216.9** |
| 2026-11-02 | IL2CPP | Manual 1 ms | **600~601** | 1.04~1.10 | 4.5~4.7 | 6.1~10.0 | 13.2~14.8 | 1,317~1,329 | 784~803 | **215.6~218.3** |
| 2026-11-02 | IL2CPP | Manual 0.1 ms | **600** | 1.00~1.14 | 3.6~3.9 | 4.6~7.8 | 11.6~12.7 | 1,317~1,371 | 720~765 | **215.6~218.3** |

- 모든 Manual 조건에서 `GC.CollectionCount`가 **프레임마다 1씩** 올랐다 — `CollectIncremental` 호출 하나가 수집 하나를 시작하고, 대부분 그 호출 안에서 끝낸다 (`true`를 돌려준 프레임은 600 중 30~60개).
- 20프레임마다 full 수집이 온다. 그 프레임의 `CollectIncremental`은 slice와 관계없이 3.5~7 ms였고 `true`(남은 작업)를 돌려준다. 남은 마킹은 **다음 프레임의 할당 루프 안에서** 진행된다(평소 0.2 ms인 할당 루프가 5~10 ms). 프레임 최대가 slice를 줄여도 줄지 않는 이유다.
- Mono Enabled 첫 실행 1회는 GC 48회가 전부 17~21 ms 스파이크였다(합계 925 ms). 같은 빌드로 다시 돌린 두 번과 02-L06 재실행은 정상(GC 51회, 대부분 1 ms 안팎)이라 표에서 뺐다. 원인은 확인 못 함.

원본: `Notes/results/02-L07_Mono_2026-11-02.csv`, `02-L07_IL2CPP_2026-11-02.csv` (`slice ns` 행으로 조건 구분, 0 = Enabled. 프레임 행 값은 `frame ms|work ms|collect ms|more|gc|used MB`)

### 추가 측정 — 호출 간격 N (2026-11-02)
slice 3 ms 고정, N프레임마다 `CollectIncremental` (반환값 `true`면 이어 부름). 같은 600프레임 조건, 2회 실행 범위. Enabled 기준선은 위 표 (Mono 367~426, IL2CPP 357 ms)

| 실행일 | 백엔드 | N | GC 횟수 | 부분 수집 1회 `CollectIncremental` | 프레임 최대 | 600프레임 합계 | 그중 `CollectIncremental` | GC Used 최소~최대 (MB) |
|---|---|---|---|---|---|---|---|---|
| 2026-11-02 | Mono | 1 | 600 | 0.5~0.8 | 10.2~12.3 | 970~981 | 551~558 | 153.6~155.0 (평평) |
| 2026-11-02 | Mono | 5 | 126~129 | 0.6~1.2 | 11.5~16.9 | 430~475 | 142~156 | 153.7~160.3 |
| 2026-11-02 | Mono | 20 | 31 | 0.9~2.2 | 8.7~10.1 | 352~370 | 42~43 | 153.6~180.3 |
| 2026-11-02 | Mono | 60 | 10 | 1.4~2.4 | **3.4~3.7** | 354~355 | 17~19 | 153.6~**232.3** |
| 2026-11-02 | IL2CPP | 1 | 600 | 0.8~1.7 | 14.9~16.2 | 1,244~1,327 | 812~877 | 215.6~216.9 (평평) |
| 2026-11-02 | IL2CPP | 5 | 126 | 1.0~2.6 | 10.9~12.2 | 424~486 | 191~226 | 215.6~222.3 |
| 2026-11-02 | IL2CPP | 20 | 31~32 | 1.2~3.2 | 9.8~15.4 | 325~328 | 58~62 | 215.6~242.3 |
| 2026-11-02 | IL2CPP | 60 | 10 | 1.9~5.3 | **4.1~6.1** | **288~290** | 27~29 | 215.6~**294.2** |

- full 수집(약 6.5 ms + 다음 프레임 할당 루프)은 여전히 수집 20회마다 온다. N = 1·5·20에서는 600프레임 안에 1~30번 보였지만, N = 60은 수집이 10번뿐이라 **측정 구간 안에 full이 없었다** (1,200프레임마다 올 것으로 보임, 미측정).
- GC Used는 N이 커질수록 톱니 모양이 된다. 진폭이 N × 1 MB와 거의 같다 (N=5 약 6 MB, 20 약 26 MB, 60 약 79 MB).

원본: 같은 CSV. `interval frames` 행으로 구분 (이 행이 없는 앞의 실행은 N = 1)

## 해석
- 예측과 비교:
  - (a) "slice를 줄이면 GC 프레임 시간 증가" — 시간 예산으로 보면 **해당 없음(변화 없음).** 600프레임 합계는 세 slice가 거의 같았고(Mono 약 1,000~1,090, IL2CPP 약 1,300~1,370 ms), `CollectIncremental` 합계는 오히려 조금 줄었다. 평소 프레임의 GC 비용(Mono 0.6, IL2CPP 1.0 ms)은 slice와 무관했다. 대신 **Manual 자체가 Enabled보다 약 2.5~3.7배 비쌌다.**
  - (b) "3 ms는 Used 감소, 0.1 ms로 갈수록 결국 증가" — 시간 예산으로 보면 **해당 없음(변화 없음).** 세 slice 모두 Used가 살아 있는 힙 크기에 붙어 평평했고(Mono 154~156, IL2CPP 216~218 MB), Enabled(155~172 / 216~235 MB)보다 낮았다.
  - 단, 학습자는 slice를 호출 간격으로 읽었으므로 위 채점은 질문의 의도와 다른 변수에 대한 것이다. 간격 해석에서 나온 "자주 부를수록 GC 시간이 늘어난다"는 직관은 실측의 핵심(수집 빈도 = 호출 빈도)과 같은 방향이다. 호출 간격 자체는 아래 추가 측정에서 바꿨다.
  - slice를 시간 예산으로 읽을 때 빠지기 쉬운 모델은 "수집 한 번의 일정량을 slice로 쪼개 나눠 갚는다"이다. 실제로는 (1) 호출할 때마다 **새 수집을 시작**해서 수집 빈도가 할당량이 아니라 호출 빈도로 정해졌고, (2) slice는 상한이 아니었으며, (3) 마무리 못 한 마킹은 다음 할당이 이어받았다. 그래서 쓰레기가 쌓일 틈이 없었다.
- 왜 매번 새 수집인가 — libil2cpp `BoehmGC.cpp`에 `StartIncrementalCollection()` → `GC_start_incremental_collection()`(`alloc.c` 377행)이 있다. 이 함수는 `GC_should_start_incremental_collection = TRUE`로 "할당량 임계치를 무시하고 수집을 시작"하게 만든다(`GC_should_collect`). `CollectIncremental`이 진행 중인 수집이 없을 때 이 경로를 타는 것으로 보인다 — Unity 네이티브 쪽 구현은 비공개라 **확인 필요**지만, GC 횟수가 호출 수와 정확히 같다는 측정과 맞는다.
- 왜 slice가 상한이 아닌가 (`alloc.c` `GC_collect_a_little_inner`):
  - 마킹 단계는 `GC_mark_some`을 `GC_rate`(기본 10)번 돌리는 단위로 진행한다. 이 실험의 부분 수집은 dirty 페이지가 적어 한 번의 호출에서 마킹이 끝난다.
  - 마킹이 끝나면 세상을 멈추고 마무리 마킹(`GC_stopped_mark`)을 한다. 시간 제한은 **첫 시도에만** 걸리고(`MAX_PRIOR_ATTEMPTS` = 1), 한 번 실패하면 다음에는 `GC_never_stop_func`로 끝까지 한다. 즉 마무리에는 바닥 비용(Mono 0.6, IL2CPP 1.0 ms)이 있다.
- 왜 할당이 마킹을 이어받는가 — `GC_allocobj`(`alloc.c` 1564행)는 프리 리스트가 비면 `GC_incremental && !GC_dont_gc`일 때 `GC_collect_a_little_inner(1)`을 부른다. Manual 모드에서도 이 경로가 살아 있다는 것이 측정(full 다음 프레임의 할당 루프 5~10 ms)으로 보인다. "Manual = GC가 내가 부를 때만 돈다"는 정확하지 않다 — **자동으로 시작하지 않을 뿐, 시작된 수집은 할당 경로에서도 진행된다.**
- 한 단계 더:
  - **조건 없이 매 프레임 `CollectIncremental`을 부르는 것은 안티패턴이다.** 1 MB마다 수집을 하니 Enabled(약 15 MB마다 수집)보다 GC가 12배 자주 돌고, 수집마다 바닥 비용(루트·dirty 페이지 재스캔과 마무리)을 낸다. 메모리는 아끼지만(Used 최고치 Mono −17, IL2CPP −17 MB) CPU를 2.5~3.7배 쓴다. 메모리 vs CPU의 교환이다.
  - 실무 패턴은 "**언제 시작할지**"를 코드가 정하는 것이다: 전투 중에는 Manual로 두고 로딩·일시정지·컷신에서 `GC.Collect()`나 `CollectIncremental`을 몰아서 부르거나, GC Used가 마지막 수집 이후 일정량 늘었을 때만 시작하고 반환값이 `true`인 동안만 이어 부른다.
  - `Disabled`는 `GC.Collect()`까지 막는다. 짧은 구간(예: 레벨 하나) 동안 힙을 키워도 되는 경우에만 쓴다 (Unity 문서 `GarbageCollector.Mode`).
- 추가 측정 해석 — 호출 간격 N:
  - 예측과 비교:
    - (a) "N이 커지면 총 GC 시간 감소" — **맞음.** `CollectIncremental` 합계가 N=1 → 60에서 Mono 555 → 18 ms, IL2CPP 845 → 28 ms로 줄었다. 다만 **선형이 아니라 포화한다.** 프레임 합계는 N=20과 60이 거의 같았다(Mono 약 355 ms). 남은 시간은 할당 자체(약 220~290 ms)라서 GC를 더 줄여도 프레임 합계가 많이 줄지 않는다.
    - (b) "수집 프레임 하나의 GC 시간 증가" — **부분 수집에 대해서는 맞음.** 부분 수집 1회 비용이 N에 따라 늘었다(IL2CPP 약 1.0 → 2~5 ms). 그러나 **프레임 최대는 오히려 줄었다**(IL2CPP 15 → 4~6 ms). 프레임 최대를 정하는 것은 부분 수집이 아니라 20회마다 오는 full 수집인데, N이 클수록 full이 드물어지고 N=60은 측정 구간에 아예 없었다.
    - (c) "Used는 이전과 동일" — **틀림.** N=1만 평평하고, N이 커지면 N MB 진폭의 톱니가 된다(Mono 최고 155 → 232 MB, IL2CPP 217 → 294 MB). Manual 모드에서는 부르지 않는 동안 아무것도 회수되지 않으니, 그 사이의 쓰레기가 전부 쌓인다.
    - 어긋난 지점: (c)에서 앞 실험의 "평평한 Used"를 그대로 가져왔다. 앞 실험에서 Used가 평평했던 이유는 "1 MB마다 수집"이었으므로, 호출 간격을 바꾸면 이 전제가 바뀐다.
  - 쓰레기는 마킹하지 않는데 왜 부분 수집이 비싸지는가: 마킹은 살아 있는 객체만 따라가므로 쓰레기 양과 무관하다. 그런데도 비용이 늘었다. 수집을 마칠 때 회수 준비(`GC_finish_collection` → 블록별 reclaim 목록 구성)가 힙의 **블록 수**에 비례하고, 쓰레기 1 MB = `byte[1024]` 블록 약 256개(4 KB 블록 기준)가 N배로 늘기 때문으로 보인다 (비용 분해는 확인 필요).
  - Enabled와 비교: Enabled는 약 12~14 MB마다 수집한다. N=20(20 MB마다)은 프레임 합계가 Enabled와 비슷하거나 약간 낮고(IL2CPP 326 vs 357 ms), N=60은 더 낮지만 Used가 80 MB 더 크다. 결국 **수집 간격 = 메모리 진폭 vs GC 빈도의 교환**이고, Enabled의 할당량 임계치(02-L06 `min_bytes_allocd`)는 이 교환점을 살아 있는 힙 크기로 자동으로 정하는 것이다. Manual로 얻는 것은 총량이 아니라 **수집을 놓을 프레임을 고르는 권한**이다.
- 근거: 이 프로젝트 실측. `Editor/Data/il2cpp/external/bdwgc/alloc.c`(`GC_start_incremental_collection` 377행, `GC_should_collect`, `GC_collect_a_little_inner` 659행, `MAX_PRIOR_ATTEMPTS`, `GC_allocobj` 1564행), `libil2cpp/gc/BoehmGC.cpp`(`CollectALittle`, `StartIncrementalCollection`). Mono 런타임 소스는 확인하지 않음 (같은 패턴이라 같은 Boehm 로직으로 보임, 확인 필요)

## 코드 발췌
```c
/* il2cpp/external/bdwgc/alloc.c */
GC_API void GC_start_incremental_collection()
{
  if (GC_incremental)
  {
    GC_should_start_incremental_collection = TRUE;   /* 할당량 임계치를 무시하고 시작 */
    GC_collect_a_little();
  }
}

/* GC_collect_a_little_inner: 마무리 마킹 */
if (GC_n_attempts < max_prior_attempts             /* MAX_PRIOR_ATTEMPTS = 1 */
    && GC_time_limit != GC_TIME_UNLIMITED) {
  if (!GC_stopped_mark(GC_timeout_stop_func)) {    /* 첫 시도만 시간 제한 */
    GC_n_attempts++;
    break;
  }
} else {
  (void)GC_stopped_mark(GC_never_stop_func);       /* 두 번째는 끝까지 */
}
```
