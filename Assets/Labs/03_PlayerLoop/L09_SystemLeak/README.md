# 03-L09 커스텀 시스템의 수명 (에디터)

## 목표
`[RuntimeInitializeOnLoadMethod]`에서 커스텀 PlayerLoop 시스템을 끼우고 정리하지 않을 때, 플레이 모드에 다시 들어갈 때마다 시스템이 쌓이는지 센다. Reload Domain을 켰을 때와 껐을 때를 비교하고, 정리 방법 두 가지(`SubsystemRegistration`, `playModeStateChanged`)의 효과도 본다. 에디터 전용 현상이라 결론은 에디터 기준이다.

## 예측
- 질문: 플레이에 들어갈 때마다 `GetCurrentPlayerLoop()` → `Update` 끝에 `MySystem` 추가 → `SetPlayerLoop()`를 하고 정리는 하지 않는다. Play에 3번 연속 들어갔다 나오면, 세 번째로 들어갔을 때 트리의 `MySystem`은 (1) Reload Domain 켬 (2) 끔일 때 각각 몇 개인가?
- 학습자 답 (실행 전, 원문 그대로): "1, 3 (껐을 때 CurrentPlayerLoop는 ㅇ이전 실행에 설정된 PlayerLoop가 그대로 남으므로)"

## 실습 구성
- 코드: `SystemLeakLab.cs`
  - `SystemLeak`: 실습이 켜져 있을 때만(`SessionState`) 동작한다. `SubsystemRegistration` 시점에 남아 있는 노드 수를 먼저 센 뒤(조건 C는 이어서 제거), `BeforeSceneLoad`에서 `Update` 끝에 `LeakySystem`을 추가한다(조건 D는 `ExitingPlayMode` 제거 핸들러도 등록).
  - `SystemLeakLab`(`03-L09`): 진입마다 `LeakySystem` 노드 수(타입 비교 / 이름 비교), `Update` 단계 자식 수(현재 vs 기본), 10프레임 동안의 프레임당 `Tick` 호출 수를 기록한다.
  - `SystemLeakDriver`: 조건 4개 × 진입 3회를 자동으로 돈다. 조건마다 남은 노드를 지우고 Enter Play Mode Options를 설정하며, 끝나면 원래 값으로 되돌린다.
- 조건: A Reload Domain 켬, 정리 없음 / B 끔, 정리 없음 / C 끔, `SubsystemRegistration`에서 제거 / D 끔, `ExitingPlayMode`에서 제거
- 실행: 에디터 메뉴 `Labs/03/L09 System Leak` (플레이어에서는 실행하지 않음)

## 결과
2026-11-18, 에디터 6000.3.25f1. 조건마다 3번 진입했다. 기본 트리의 `Update` 단계 자식은 4개다.

| 조건 | 진입 시 남아 있던 노드 (`SubsystemRegistration`, 1·2·3회차) | 설치 뒤 `LeakySystem` (1·2·3회차) | 프레임당 `Tick` (1·2·3회차) | 마지막으로 나온 뒤 편집 모드 |
|---|---|---|---|---|
| A 리로드 켬, 정리 없음 | 0 · 0 · 0 | 1 · 1 · 1 | 0.9 · 0.9 · 0.9 | **1개 남음** |
| B 리로드 끔, 정리 없음 | 0 · 1 · 2 | **1 · 2 · 3** | 0.9 · 1.8 · 2.7 | 3개 남음 |
| C 끔, `SubsystemRegistration`에서 제거 | 0 · 1 · 1 | 1 · 1 · 1 | 0.9 · 0.9 · 0.9 | 1개 남음 |
| D 끔, `ExitingPlayMode`에서 제거 | 0 · 0 · 0 | 1 · 1 · 1 | 0.9 · 0.9 · 0.9 | 0개 |

- 타입으로 센 값과 이름으로 센 값이 모든 행에서 같았다. 도메인 리로드 뒤 옛 `Type`을 가진 노드가 남는 경우는 없었다.
- `Tick`이 1이 아니라 0.9인 것은 측정 창 경계 때문이다. 10프레임 동안 9번 불렸고, 모든 조건에서 같다.
- 원본: `Notes/results/03-L09_Editor_2026-11-18.csv`
- Enter Play Mode Options는 실행이 끝난 뒤 원래 값으로 돌아왔다. `EditorSettings.asset`은 바뀐 줄 없음, `m_EnterPlayModeOptions: 0`.
  - 확인 필요: 실행 전 API `enterPlayModeOptionsEnabled`는 `False`였는데, 파일의 `m_EnterPlayModeOptionsEnabled`는 `1`이다. 6000.3에서 이 속성의 의미가 바뀐 것으로 보이지만 아직 확인하지 않았다.

## 해석
- **예측과 비교:** (1) 1개, (2) 3개 모두 맞았다. (2)의 이유("이전 실행의 PlayerLoop가 그대로 남음")도 맞다. 진입할 때 이전 세션의 노드가 1개, 2개 남아 있었고, 그 위에 하나씩 더 쌓였다.
  - 트리에만 쌓이는 것이 아니다. **실제 호출도 N배**가 된다(프레임당 `Tick` 0.9 → 1.8 → 2.7).
- **A에서 1개인 이유:** "플레이를 나갈 때 엔진이 트리를 되돌리기 때문"이 아니다. A도 나온 직후 편집 모드에는 노드가 그대로 남아 있었다.
  - 다음 진입의 도메인 리로드가 트리를 초기화한다. 그래서 `SubsystemRegistration` 시점에는 이미 `Update` 자식 4개(기본 트리)로 돌아와 있었다.
  - 도메인을 버리면 노드의 `updateDelegate`는 사라진 어셈블리의 코드를 가리키게 된다. 그런 노드를 남겨 둘 수 없으니, 리로드 때 엔진이 트리를 되돌리는 것이 자연스럽다. 다만 이 부분은 실측에서 끌어낸 추론이고 소스로 확인하지는 않았다.
- **정리 방법 비교:**
  - C `SubsystemRegistration`: 플레이 세션 사이(편집 모드)에는 노드가 남아 있다가, 다음 진입의 첫 시점에 지워진다.
  - D `ExitingPlayMode`: 나갈 때 바로 지워서, 편집 모드에도 남지 않는다.
  - C는 "다음 진입"이 있어야 정리가 일어나고, D는 에디터 API라 `#if UNITY_EDITOR`가 필요하다. 둘 다 쓰는 것이 안전하다.
  - 정리 핸들러를 등록할 때도 `-=` 뒤에 `+=`를 해야 한다. 리로드가 꺼져 있으면 static 이벤트 구독 자체가 쌓이기 때문이다.
- **한 단계 더:**
  - 남은 노드를 지울 때는 `SetPlayerLoop(GetDefaultPlayerLoop())`로 통째로 되돌리면 안 된다. Input System처럼 다른 패키지가 끼운 시스템까지 사라진다. 이 실습처럼 **자기 `type`만 골라서 빼야** 한다(03-L01: current ≠ default).
  - 같은 원리로 static 필드, static 이벤트, `[RuntimeInitializeOnLoadMethod]`로 만든 싱글턴도 리로드가 꺼지면 모두 남는다. Unity 문서가 `SubsystemRegistration`에서 static을 초기화하라고 권하는 이유다.
- **근거:**
  - Unity 매뉴얼 "Configurable Enter Play Mode"(Domain Reload를 끄면 static 필드와 이벤트 핸들러가 남는다는 경고)
  - `RuntimeInitializeLoadType.SubsystemRegistration` 문서
  - 이 실습의 실측
