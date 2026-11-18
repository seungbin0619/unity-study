# 03-L10 편집 모드 루프와 `[ExecuteAlways]` (에디터)

## 목표
편집 모드에서 `[ExecuteAlways]` `Update`(PlayerLoop)와 `EditorApplication.update`(에디터 루프)가 언제, 얼마나 자주 불리는지 센다. 입력이 없을 때, 씬을 고칠 때, `QueuePlayerLoopUpdate`를 부를 때, 마우스로 입력할 때를 비교한다. 에디터 전용 현상이라 결론은 에디터 기준이다.

## 예측
- 질문: 편집 모드에서 씬에 `[ExecuteAlways]` 스크립트 하나를 두고, 에디터에 포커스만 준 채 10초 동안 아무 입력도 하지 않는다. (a) `[ExecuteAlways]` `Update` (b) `EditorApplication.update`는 10초 내내 계속 불리나, (거의) 멈추나? 각각 이유는?
- 학습자 답 (실행 전, 원문 그대로): "(a) X (마우스 입력같은게 안들어오면 멈추던데) (b) O 이건 명시적으로 걸어둔거라 계속 돌듯"

## 실습 구성
- 코드: `EditModeLoopLab.cs`
  - `EditModeTicker`: `[ExecuteAlways]` 컴포넌트. `Update`마다 정적 카운터를 올린다.
  - `EditModeLoopLab`: 편집 모드에서 `EditorApplication.update`로 단계를 진행한다. `HideFlags.HideAndDontSave` GameObject에 `EditModeTicker`를 붙이고, 단계별로 두 콜백의 호출 수, 초당 횟수, 최대 간격(ms), 에디터 포커스 여부를 기록한다. 끝나면 GameObject를 지우고 `Notes/results/03-L10_Editor_<날짜>.csv`에 쓴다.
- 자동 단계 (메뉴 `Labs/03/L10 Edit Mode Loop`, 약 27초. 실행 중에는 입력하지 않는다)
  - 0. 안정화 2초 (기록 안 함)
  - 1. idle 10초: 아무것도 안 함
  - 2. modify 5초: 에디터 틱마다 Ticker의 `transform.position`을 조금씩 바꿈 (Undo 없음)
  - 3. queue 5초: 에디터 틱마다 `EditorApplication.QueuePlayerLoopUpdate()`
  - 4. idle again 5초
- 수동 단계 (메뉴 `Labs/03/L10 Edit Mode Loop (Mouse)`, 10초): 시작하면 Scene 뷰 위에서 마우스를 계속 움직인다.
- 실행: 학습자가 에디터에서 메뉴를 직접 누른다. 터미널에서 CLI로 부르면 에디터가 포커스를 잃은 채로 측정되기 때문이다.

## 결과
2026-11-18, 에디터 6000.3.25f1, Main 씬. 모든 단계에서 에디터 포커스 비율은 1.00이었다. 단계마다 1회씩 실행했다.

| 단계 | `[ExecuteAlways]` `Update` (횟수 / 초당 / 최대 간격) | `EditorApplication.update` (초당 / 최대 간격) |
|---|---|---|
| 1 idle 10초 | 4회 / 0.4 / 7.5 s | 약 24,000 / 7.5 ms |
| 2 틱마다 `transform.position` 수정 5초 | 1회 / 0.2 / — | 약 19,000 / 4.0 ms |
| 3 틱마다 `QueuePlayerLoopUpdate` 5초 | 1,583회 / **317** / 32 ms | **317** / 33 ms |
| 4 idle again 5초 | 1회 / 0.2 / — | 약 23,500 / 0.8 ms |
| M Scene 뷰에서 마우스 이동 10초 | 82회 / 8.2 / 387 ms | 약 16,000 / 7.5 ms |

원본: `Notes/results/03-L10_Editor_2026-11-18.csv`. `Main.unity`는 바뀌지 않았다(`HideAndDontSave` GameObject 사용).

## 해석
- **예측과 비교:** (a) X, (b) O 모두 맞았다.
  - (a)의 이유("입력이 없으면 멈춤")도 맞다. 10초 동안 4회였고, 마우스를 움직이면 초당 8회로 늘었다.
  - (b)의 이유는 보충이 필요하다. 명시적으로 걸어서 도는 것이 아니다. 에디터 루프 자체가 입력과 관계없이 계속 돌고, `+=`는 그 루프에 올라탈 뿐이다. 거는 것만으로 루프를 깨우지는 않는다.
  - 호출 빈도는 예상보다 훨씬 높았다. 편집 모드 PlayerLoop가 사실상 멈춘 동안에도 초당 약 2만 회 불렸다.
- **PlayerLoop를 깨우는 것:** 편집 모드 PlayerLoop는 "무언가 다시 그려야 할 때" 돈다(입력, 씬 뷰 다시 그리기 등).
  - 코드로 `transform.position`을 바꾸는 것만으로는 깨우지 않았다(5초에 1회). 툴이 `[ExecuteAlways]` `Update`에 기대 씬을 갱신한다면, 값을 바꾼 뒤 `QueuePlayerLoopUpdate()`나 `SceneView.RepaintAll()`로 직접 깨워야 한다.
- **`QueuePlayerLoopUpdate`를 틱마다 부르면:**
  - PlayerLoop가 에디터 틱마다 한 번씩 돌고(317 vs 317), 에디터 루프가 약 2만 Hz에서 317 Hz로 떨어졌다. 한 번에 약 3 ms인 PlayerLoop(와 그에 따른 다시 그리기)가 매 에디터 틱에 끼면서, 에디터 틱이 그만큼 길어진 것으로 보인다.
  - 즉 이 API는 "다음 틱에 한 번 돌려 달라"는 요청이고, 계속 부르면 편집 모드가 플레이 모드처럼 쉬지 않고 돈다.
- **한 단계 더:**
  - `EditorApplication.update` 콜백은 초당 수만 번 불릴 수 있는 핫 패스다. 툴 코드에서 매 틱 할당하거나 `AssetDatabase`를 조회하면 바로 에디터가 느려진다. 시간 간격을 직접 확인하거나, 필요할 때만 구독하고 끝나면 `-=`로 해제한다. 일회성 작업은 `delayCall`을 쓴다.
  - 확인 필요: 에디터가 쉴 때 얼마나 자주 도는지는 Preferences > General > Interaction Mode(에디터 쉬는 시간 설정)에 따라 달라질 것으로 보인다. 이 실습에서는 설정값을 읽지 않았다.
  - 에디터와 플레이어의 차이: 플레이어에는 편집 모드도 `EditorApplication`도 없다. 이 동작은 모두 에디터 전용이다.
- **근거:** Unity 스크립팅 API의 `ExecuteAlways`(편집 모드에서는 씬의 무언가가 바뀔 때만 `Update`가 호출된다는 설명), `EditorApplication.QueuePlayerLoopUpdate`, `EditorApplication.update` 문서, 그리고 이 실습의 실측.
