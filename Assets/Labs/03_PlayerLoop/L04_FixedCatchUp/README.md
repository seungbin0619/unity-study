# 03-L04 `FixedUpdate` 따라잡기와 `maximumDeltaTime`

## 목표
프레임이 길어질 때 `FixedUpdate`가 한 프레임에 몇 번 몰아서 도는지, `Time.maximumDeltaTime`이 그 횟수와 게임 시간을 어떻게 제한하는지, 물리 스텝 자체가 비쌀 때 무슨 일이 생기는지 잰다.

## 예측
- 질문: `fixedDeltaTime` 0.02 s(20 ms), `maximumDeltaTime` 기본값 약 0.333 s("한 프레임에 게임 시간을 최대 이만큼만 진행시킨다")일 때,
  - (1) 프레임이 100 ms로 일정하면 프레임당 `FixedUpdate`는 몇 번?
  - (2) 프레임이 500 ms로 일정하면 프레임당 `FixedUpdate`는 몇 번이고, `Update`에서 읽는 `Time.deltaTime`은 얼마?
- 학습자 답 (실행 전, 원문 그대로): "1. 5회 2. 16.x회 (Time.deltaTime = 0.333)"

## 실습 구성
- 코드
  - `FixedCounter.cs` — `FixedUpdate`마다 횟수를 세고, `Update`에서 그 프레임의 횟수·`Time.deltaTime`·실제 프레임 시간(Stopwatch)을 기록. `Update`/`FixedUpdate`에서 지정한 ms만큼 `Thread.Sleep`
  - `FixedCatchUpLab.cs` — 조건 7개(a 0 ms, b 10 ms, c 50 ms, d 100 ms, e 500 ms, f 500 ms + `maximumDeltaTime` 1.0, g `FixedUpdate`마다 30 ms)를 워밍업 5프레임 뒤 10\~60프레임씩 기록. vSync 끔, `targetFrameRate` -1, 끝나면 `maximumDeltaTime` 복원
- 실행: 에디터 메뉴 `Labs/03/L04 Fixed Catch Up` 또는 플레이어 `-lab 03-L04`

## 결과
배치모드 Development 플레이어 (Mono / IL2CPP). `fixedDeltaTime` 0.02, `maximumDeltaTime` 기본 0.3333. 에디터는 실행하지 않음.

| 조건 | 실제 프레임 (ms) | 프레임당 `FixedUpdate` (Mono / IL2CPP) | 수열 일부 (Mono) | `Time.deltaTime` | 게임/실제 시간 |
|---|---|---|---|---|---|
| a 0 ms | 0.03\~0.04 | 0 / 0 | 0 0 0 … | ≈0 | 1.00 |
| b 10 ms | 10.5 / 10.9 | 평균 0.53 / 0.55 | 0 1 0 1 1 0 … | 0.0105 / 0.0109 | 1.00 |
| c 50 ms | 50.7 / 51.1 | 평균 2.53 / 2.57 (2\~3) | 3 2 3 2 … | 0.051 | 1.00 |
| d 100 ms | 100.7 / 101.0 | **5** / 5.05 (한 번 6) | 5 5 5 … | 0.101 | 1.00 |
| e 500 ms | 500.6 / 501.2 | **평균 16.7 / 16.6 (16\~17)** | 17 16 17 17 16 … | **0.3333** | **0.67** |
| f 500 ms + `maximumDeltaTime` 1.0 | 500.6 / 501.3 | **25** / 25 | 25 25 25 … | 0.501 | 1.00 |
| g `FixedUpdate`마다 30 ms | 510 / 511 | 평균 16.7 / 16.65 (16\~17) | 17 17 16 … | 0.3333 | 0.65 |

- e·g의 `Time.unscaledDeltaTime`은 0.49\~0.52 (실제 시간, 상한 없음).
- 여러 번 돈 프레임에서 첫·마지막 `FixedUpdate`의 `Time.time` 차: d 0.08 (5회), e·g 0.32 (17회), f 0.48 (25회) = (횟수 − 1) × 0.02.

원본: `Notes/results/03-L04_Mono_2026-11-04.csv`, `03-L04_IL2CPP_2026-11-04.csv` (프레임별 횟수 수열 포함)

## 해석
- **예측과 비교: 둘 다 맞음.** (1) 100 ms → 5회. (2) 500 ms → 16.x회(16·17 교대, 평균 16.7), `Time.deltaTime` 0.333. 누적 규칙(쌓인 시간이 20 ms를 넘을 때마다 1회, 남은 시간은 다음 프레임으로)과 상한(한 프레임에 최대 0.333 s만 쌓음)을 그대로 적용한 계산이었다. 16/17이 섞이는 것도 333.3 ÷ 20 = 16.67의 나머지가 쌓인 결과다.
- **`maximumDeltaTime`의 대가는 슬로모션이다.** 프레임이 0.333 s보다 길면 게임 시간은 프레임마다 0.333 s만 간다 → 500 ms 프레임에서 게임/실제 = 0.67. 상한을 1.0으로 올리면(f) 25회 돌고 게임 시간이 실제와 같아지지만, 한 프레임에 물리를 25번 돌린다.
- **비싼 물리 스텝(g) = spiral of death와 그 브레이크.** 스텝 하나가 30 ms인데 게임 시간으로는 20 ms만 진행한다. 따라잡으려고 많이 돌수록 프레임이 길어지고, 길어진 만큼 또 따라잡아야 하는 악순환이다. 상한이 없다면 프레임 시간이 계속 늘어난다. 실측에서는 `maximumDeltaTime`이 프레임당 16\~17스텝에서 끊어 **프레임 약 510 ms, 게임 시간 0.65배로 안정**됐다. 프레임은 느려져도 무한히 나빠지지는 않는다.
- **`FixedUpdate`가 "고정 간격"인 건 게임 시간 기준이다.** 한 프레임에 17번 몰아 돌 때 실제로는 거의 연달아 실행되지만, 그 안의 `Time.time`(= `fixedTime`)은 매번 정확히 0.02씩 늘었다.
- **한 단계 더**
  - `Time.unscaledDeltaTime`은 상한을 받지 않는다(e에서 0.50). 그래서 히치(hitch, 순간 멈춤) 동안 게임 로직은 느려지고 `unscaled`로 움직이는 UI는 실제 속도로 움직인다. 둘을 섞어 쓰면 히치 때 어긋난다.
  - 모바일·저사양에서 프레임이 자주 33 ms를 넘으면 `FixedUpdate`가 프레임당 2\~3회가 되어 CPU를 더 쓰고, 그래서 프레임이 더 길어진다. `fixedDeltaTime`을 0.02보다 키우거나, 상한을 낮춰 슬로모션을 감수하는 것이 대응이다. 트레이드오프는 물리 정확도 vs 실시간성이다.
  - b의 0/1 교대(10 ms 프레임 → 2프레임에 1회)와 a의 0회(0.03 ms 프레임)는 03-L02 배치모드에서 `FixedUpdate`가 안 돌았던 이유를 수치로 보여 준다.
- **근거**: 이 프로젝트의 실측. [Time.maximumDeltaTime](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Time-maximumDeltaTime.html), [Time and frame rate management](https://docs.unity3d.com/6000.3/Documentation/Manual/TimeFrameManagement.html)
