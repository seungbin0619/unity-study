# 02-L09 파이널라이저와 수거 지연

## 목표
파이널라이저(`~C()`)가 있는 객체가 참조를 잃은 뒤 몇 번째 `GC.Collect()`에서 메모리가 실제로 돌아오는지를, 파이널라이저 없는 같은 객체와 비교해 확인한다.

## 예측
- 질문: 필드로 `byte[1024]` 하나를 가진 클래스 10만 개(증가분 약 110 MB)를 만든다. 한 그룹은 빈 파이널라이저 `~C() { }`가 있고 다른 그룹은 없다. 그룹마다 참조를 모두 끊은 뒤 `GC.Collect()`를 한 번씩 부르고(사이에 1프레임 대기, `WaitForPendingFinalizers`는 부르지 않음) 매번 GC Used를 잰다. 각 그룹의 GC Used가 생성 전 수준으로 돌아오는 것은 몇 번째 `Collect` 직후인가?
- 학습자 답 (실행 전, 원문 그대로):
  ```
  소멸자 없음 -> GC.Collect 1회 후 (바로 소멸)
  소멸자 있음 -> GC.Collect 2회 후 (1회 후 소멸자 대기, 2회차에 소멸)
  ```

## 실습 구성
- 코드: `FinalizerLab.cs` — 그룹(Plain / Finalizable)마다 10만 개 생성 → 참조 해제 → `GC.Collect()` 5회. 매 `Collect` 직후 `GC.GetTotalMemory(false)`와 소요 시간, 1프레임 뒤 GC Used·실행된 파이널라이저 수·표본 1,000개의 짧은/긴 `WeakReference`(`trackResurrection` false/true) 생존 수를 기록. 그룹 순서를 바꿔 3회 반복
  - 파이널라이저 본문은 실행 횟수를 세는 `Interlocked.Increment` 하나뿐이다 (예측 질문의 "빈 파이널라이저"와 같은 조건으로 본다)
  - 끝까지(drain): c5 이후에도 같은 방식(Collect → 1프레임)을 파이널라이저가 모두 돌고 객체가 회수될 때까지 이어 가며 총 `Collect` 횟수·시간을 잰다
  - Finalizable+Wait: `Collect` → `WaitForPendingFinalizers` → `Collect` → `Collect`, 대기 시간으로 파이널라이저 스레드 처리량을 잰다
  - `-frameRate N`: `Application.targetFrameRate`. 기본(-1, 제한 없음)과 60을 비교한다 — `Collect` 사이 간격이 결과를 바꾸는지 보려고 추가
- 실행: 에디터 메뉴 `Labs/02/L09 Finalizer` 또는 플레이어 `-lab 02-L09 [-frameRate 60]`

## 결과
그룹당 10만 개, Development 빌드, Incremental GC On, `-batchmode -nographics`. 시행 3회의 범위. GC Used 기준선은 Mono 약 2.0 MB, IL2CPP 약 2.3 MB

| 실행일 | 백엔드 | 프레임 | 그룹 | 생성 후 | c1 | c2 | c5 | 기준선 회복까지 `Collect` | 소요 |
|---|---|---|---|---|---|---|---|---|---|
| 2026-11-02 | Mono | 제한 없음 | Plain | 136.2 | **2.1** | 2.1 | 2.1 | **1** | — |
| 2026-11-02 | Mono | 제한 없음 | Finalizable | 140.8 | 140.1 | 137.4\~137.9 | 128.6\~132.3 | **157\~184** | 380\~440 ms |
| 2026-11-02 | Mono | 60 | Finalizable | 140.8 | 140.1 | 29.0\~54.5 | 6.0 | **3** | — |
| 2026-11-02 | IL2CPP | 제한 없음 | Plain | 136.4 | **2.3** | 2.3 | 2.3 | **1** | — |
| 2026-11-02 | IL2CPP | 제한 없음 | Finalizable | 142.5 | 141.7 | 127.2\~129.0 | 86.8\~93.4 | **14\~18** | 32\~46 ms |
| 2026-11-02 | IL2CPP | 60 | Finalizable | 142.5 | 141.7 | **6.2** | 6.2 | **2** | — |

(GC Used, MB. "회복"은 Finalizable의 경우 아래의 약 4 MB 잔여를 뺀 수준)

| 항목 | Mono | IL2CPP |
|---|---|---|
| c1 직후까지 실행된 파이널라이저 (제한 없음 / 60fps) | 1,700\~2,000 / 64,000\~83,000 | 9,600\~10,800 / 100,000 |
| `Collect` 1회당 더 실행되는 파이널라이저 (제한 없음) | 약 1,500\~2,000 | 약 8,000\~11,000 |
| `WaitForPendingFinalizers` (10만 개) | 13.0\~16.0 ms | 2.6\~4.0 ms |
| Wait 조합: 두 번째 `Collect` 뒤 GC Used | 6.0\~6.5 (기준선 +4) | 6.2 (기준선 +3.9) |
| c1 `Collect` 시간: Plain / Finalizable | 1.5\~1.9 / 5.2\~6.7 ms | 2.1\~2.6 / 6.7\~9.7 ms |
| 생성 후 GC Used: Finalizable − Plain | +4.6 MB (객체당 약 48 B) | +6.1 MB (객체당 약 64 B) |
| 짧은 `WeakReference` (c1) | 0 / 1,000 (두 그룹 모두 c1에서 죽음) | 같음 |
| 긴 `WeakReference(o, true)` | Finalizable은 객체가 실제로 회수될 때까지 살아 있음 (c1 1,000 → 점차 감소) | **생성 시 `NotSupportedException`** ("IL2CPP does not support resurrection for weak references") |

- Finalizable 그룹은 다 회수된 뒤에도 GC Used가 기준선보다 약 4 MB 높았다 (추가 `Collect`에도 유지). 생성 시 늘어난 등록 비용(4.6 / 6.1 MB)과 크기가 비슷하다. 다음 시행의 `Settle`(Collect → Wait → Collect) 뒤에는 대부분 기준선으로 돌아왔다. 원인은 확인 필요
- 처음 Mono 실행(15:42)은 drain·Wait 단계가 없는 버전이다. 결과는 같은 경향(c5까지 128\~132 MB)

원본: `Notes/results/02-L09_Mono_2026-11-02.csv`, `02-L09_IL2CPP_2026-11-02.csv`

## 해석
- 예측과 비교:
  - **Plain "1회" — 맞음.** 두 백엔드 모두 c1에서 기준선으로 돌아왔다.
  - **Finalizable "2회" — 원리는 맞고 횟수는 조건에 따라 달랐다.** "1회차에 파이널라이저 대기, 다음 `Collect`에서 회수"는 그대로 관찰됐다. 단 그 사이에 파이널라이저가 **다 돌아야** 2회다. 파이널라이저는 별도 스레드에서 비동기로 돌기 때문에, 질문 조건(프레임 제한 없음, 1프레임 간격)에서는 Mono 157\~184회, IL2CPP 14\~18회가 걸렸다. 프레임 간격을 16.7 ms로 벌리면 IL2CPP는 정확히 2회, Mono는 3회였다. `WaitForPendingFinalizers`를 끼우면 두 백엔드 모두 2회다.
- 무엇이 횟수를 정하나: `GC.Collect()`는 세상을 멈추는(stop-the-world) 수집이라 그동안 파이널라이저 스레드도 멈춘다. 제한 없는 batchmode 프레임에서는 메인 스레드가 거의 계속 `Collect`(4\~9 ms) 안에 있고, 파이널라이저 스레드는 두 `Collect` 사이의 짧은 틈에만 돈다. 그래서 `Collect` 1회당 처리량(Mono 약 2천, IL2CPP 약 1만)만큼씩 회수됐다. 처리 속도 자체는 IL2CPP가 4\~5배 빠르다(Wait 3 ms vs 14 ms, 객체당 30 ns vs 140 ns).
- 한 단계 더:
  - **회수 안 될 객체인데 마킹 비용은 산 객체처럼 든다.** c1 `Collect`가 Plain보다 3\~4배 비쌌다. bdwgc는 수집 끝에 도달 불가능한 파이널라이저 객체를 찾으면, 파이널라이저가 그 객체와 필드를 써야 하므로 그 객체에서 닿는 것을 전부 다시 마킹해 살려 둔다 (`finalize.c`의 `GC_finalize`). 그래서 10만 개 + `byte[]` 10만 개를 한 번 더 훑는다.
  - **"죽음"이 세 단계로 나뉜다.** (1) 도달 불가 → 짧은 약한 참조가 끊김(c1), (2) 파이널라이저 실행, (3) 메모리 회수 → 긴 약한 참조가 끊김. 짧은 `WeakReference`가 null이라고 메모리가 돌아온 것은 아니다. IL2CPP는 (3)을 관찰하는 긴 약한 참조 자체를 지원하지 않는다.
  - **파이널라이저는 생성 비용도 있다.** 객체마다 GC에 등록 기록이 생겨 GC Used가 10만 개당 4.6\~6.1 MB 더 늘었다. 회수 후에도 약 4 MB가 한동안 남았다.
  - Unity 코드에서는 파이널라이저 대신 `IDisposable` + 명시적 `Dispose`를 쓴다. 파이널라이저는 "Dispose를 잊었을 때의 안전망"으로만 두고, `Dispose`에서 `GC.SuppressFinalize(this)`로 등록을 풀어 위 비용을 피한다.
- 근거: 이 프로젝트 실측 (Mono·IL2CPP Development 플레이어). bdwgc `finalize.c` `GC_finalize`·`GC_invoke_finalizers`. 파이널라이저 등록 방식(Mono `GC_REGISTER_FINALIZER_NO_ORDER`, IL2CPP `BoehmGC.cpp`)은 소스 구조 기준이며 버전별 세부는 확인 필요
