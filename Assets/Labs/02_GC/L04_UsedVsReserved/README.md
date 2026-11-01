# 02-L04 GC Used vs Reserved

## 목표
매니지드 힙의 "쓰는 양(GC Used)"과 "OS에서 받아 둔 양(GC Reserved)"이 할당·해제·수집에 따라 어떻게 따로 움직이는지 Development 플레이어에서 확인한다.

## 예측
- 질문: `byte[50MB]` 하나를 (1) 할당한 직후, (2) 참조를 끊고 `GC.Collect()`한 직후, (3) 그 뒤 10초 동안 아무것도 안 했을 때 — GC Used와 GC Reserved는 각각 어떻게 변하는가?
- 학습자 답 (실행 전, 원문 그대로):
  ```
  (1) GC Used +50MB (GC Reserved는 부족한 경우 증가)
  (2) GC Used 감소, Reserved는 그대로
  (3) 그대로
  ```
  (실행 전에 한 번 고친 답. 처음 답은 "(2) 그대로 / (3) GC Used 감소, Reserved는 그대로"였고 학습자가 바로 정정함)

## 실습 구성
- 코드: `UsedVsReservedLab.cs` — 단계마다 5프레임을 넘긴 뒤 Used/Reserved를 세 경로(`ProfilerRecorder`, `Profiler.GetMonoUsed/HeapSizeLong`, `GC.GetTotalMemory`)로 기록. 비교용으로 (4) 같은 크기 재할당, (5) 다시 해제 + Collect 단계도 둔다
- 실행: 에디터 메뉴 `Labs/02/L04 Used vs Reserved` 또는 플레이어 `-lab 02-L04`

## 결과
Used / Reserved (MB, `ProfilerRecorder`). `Profiler.GetMonoUsed/HeapSizeLong`, `GC.GetTotalMemory(false)`도 모든 단계에서 같은 값(±0.01). 괄호는 단계 직후의 `GC.CollectionCount(0)` 증가분

| 실행일 | 백엔드 | Incremental GC | 빌드 | 조건 | 측정값 |
|---|---|---|---|---|---|
| 2026-11-01 | Mono | On | Development | 0 기준선 (Collect 후) | 0.97 / 1.34 |
| 2026-11-01 | Mono | On | Development | 1 `new byte[50MB]` | 50.97 / 51.84 (GC +1) |
| 2026-11-01 | Mono | On | Development | 2a 참조만 끊음 · 2b `GC.Collect()` | 50.97 / 51.84 · 0.97 / 51.84 (GC +1) |
| 2026-11-01 | Mono | On | Development | 3 10초 대기 | 0.97 / 51.84 (GC 0) |
| 2026-11-01 | Mono | On | Development | 4 같은 크기 재할당 · 5 다시 해제 + Collect | 50.97 / 51.84 (GC +1) · 0.97 / 51.84 (GC +1) |
| 2026-11-01 | IL2CPP | On | Development | 0 기준선 | 1.18 / 1.34 |
| 2026-11-01 | IL2CPP | On | Development | 1 `new byte[50MB]` | 51.18 / 51.84 (GC +1) |
| 2026-11-01 | IL2CPP | On | Development | 2a · 2b | 51.19 / 51.84 · 1.19 / 51.84 (GC +1) |
| 2026-11-01 | IL2CPP | On | Development | 3 10초 대기 | 1.19 / 51.84 (GC 0) |
| 2026-11-01 | IL2CPP | On | Development | 4 · 5 | 51.19 / 51.84 (GC +1) · 1.19 / 51.84 (GC +1) |

원본: `Notes/results/02-L04_Mono_2026-11-01.csv`, `02-L04_IL2CPP_2026-11-01.csv`

## 해석
- 예측과 비교: (1)·(2)·(3) 모두 맞음.
  - (1) Used +50 MB, Reserved 1.34 → 51.84 MB (기존 빈 공간이 부족해서 힙을 키움).
  - (2) 참조를 끊기만 해서는 Used가 그대로다(2a). 매니지드 힙은 참조 카운팅이 아니라 **추적(tracing) GC**라서, 다음 수집이 돌아야 비로소 "안 쓰는 것"으로 판정된다. `Collect` 후(2b) Used만 내려가고 Reserved는 51.84 MB 유지.
  - (3) 10초 동안, 그리고 이후 수집 2번을 더 거쳐도 Reserved는 줄지 않았다. 이 빌드의 Boehm 힙은 한 번 키운 공간을 OS에 돌려주지 않는다.
- 한 단계 더:
  - **Reserved는 최고 수위(high-water mark)로 남는다.** 로딩 중 한 번 생긴 일시적 할당 피크가 세션 끝까지 프로세스 메모리로 남는다. 메모리 예산이 빠듯한 모바일·콘솔에서 이게 문제가 된다. 대처는 피크 자체를 줄이는 것 — 큰 임시 버퍼를 재사용하거나(`ArrayPool`), 네이티브 메모리(`NativeArray`)로 옮겨 명시적으로 해제한다.
  - **남은 공간은 재사용된다.** (4)에서 같은 크기를 다시 할당해도 Reserved가 늘지 않았다. 다만 이번에는 연속된 50 MB 빈 블록이 그대로 있었기 때문이다. Boehm은 객체를 옮기지 않는(non-moving, non-compacting) GC라서, 빈 공간이 잘게 쪼개져 있으면(단편화, fragmentation) 합계가 충분해도 큰 배열이 안 들어가 힙이 더 커질 수 있다 (확인 필요, 실측 가능).
  - **큰 할당은 GC를 부른다.** (1)과 (4)에서 `new byte[50MB]` 직후 GC 횟수가 1씩 늘었다. 이 할당 자체가 수집을 유발한 것으로 보인다(정확한 트리거 조건은 확인 필요).
- 근거: 이 프로젝트 실측 (세 가지 API가 같은 값을 보고)
