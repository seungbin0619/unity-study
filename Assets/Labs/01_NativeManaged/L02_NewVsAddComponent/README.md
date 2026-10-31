# 01-L02 `new MonoBehaviour()` vs `AddComponent`

## 목표
`new`로 만든 MonoBehaviour는 네이티브 객체 없이 매니지드 래퍼만 있는 상태가 된다. 그 객체가 콘솔 경고, null 관련 연산자, 멤버 접근에서 `AddComponent`로 만든 정상 컴포넌트와 어떻게 다른지 에디터와 플레이어에서 확인한다.

## 예측
- 질문:

  ```csharp
  var go = new GameObject("Host");
  var a = new ProbeBehaviour();               // LAB: 의도적
  var b = go.AddComponent<ProbeBehaviour>();
  ```

  | # | 식 | `a` (new) | `b` (AddComponent) |
  |---|---|---|---|
  | 0 | 생성 줄에서 콘솔 출력 | | |
  | 1 | `x == null` | | |
  | 2 | `x is null` | | |
  | 3 | `x?.name` | | |

- 학습자 답 (실행 전, 2026-10-29):

  | # | `a` (new) | `b` (AddComponent) | 이유 (`a`) |
  |---|---|---|---|
  | 0 | Warning 로그 | 로그 없음 | |
  | 1 | `true` | `false` | 내부 네이티브 포인터가 null |
  | 2 | `false` | `false` | 매니지드 참조 자체는 null이 아님 |
  | 3 | 예외 | `"Host"` | 참조가 null이 아니므로 `name` 접근을 시도하다 실패 |

## 실습 구성
- 코드: `NewVsAddComponentLab.cs` — 생성 줄을 `Application.logMessageReceived`로 감싸 콘솔 출력을 잡고, 두 객체에 3개 식을 평가한다. 예외가 나면 예외 타입을 기록한다.
- 실행: 에디터 메뉴 `Labs/01/L02 New vs AddComponent` 또는 플레이어 `-lab 01-L02`

## 결과

| 실행일 | 백엔드 | 빌드 | # | 식 | `a` (new) | `b` (AddComponent) |
|---|---|---|---|---|---|---|
| 2026-10-29 | Editor (Mono) | 에디터 플레이 모드 | 0 | 콘솔 출력 | Warning: "You are trying to create a MonoBehaviour using the 'new' keyword. This is not allowed. …" | 없음 |
| | | | 1 | `x == null` | `true` | `false` |
| | | | 2 | `x is null` | `false` | `false` |
| | | | 3 | `x?.name` | `NullReferenceException` | `"Host"` |
| 2026-10-29 | Mono | Development 플레이어 (`-batchmode -nographics`) | 0 | 콘솔 출력 | **없음** (플레이어 로그에도 없음) | 없음 |
| | | | 1~3 | | 에디터와 동일 | 에디터와 동일 |

원본: `Notes/results/01-L02_Editor_2026-10-29.csv`, `Notes/results/01-L02_Mono_2026-10-29.csv`

## 해석
- 예측과 비교: 1~3은 모두 맞음. 0은 에디터 기준으로 맞았지만, 플레이어에서는 경고가 **나오지 않았다.**
- `new`로 만든 MonoBehaviour는 네이티브 객체가 한 번도 만들어지지 않은 래퍼다. 그래서 01-L01의 "파괴된 객체"와 같은 그룹으로 동작한다. `==`는 `true`, `is null`은 `false`, `?.`는 그대로 통과해서 예외가 난다.
- 한 단계 더:
  - **경고는 에디터 전용이다.** 빌드에서는 `new`로 만든 컴포넌트가 아무 경고 없이 만들어지고, 처음 멤버에 접근할 때 예외가 난다. 에디터 콘솔을 보고 "경고가 뜨니까 잡힌다"고 생각하면 빌드에서 놓친다.
  - **에디터의 예외 타입이 01-L01과 다르다.** 에디터에서 파괴된 객체는 `MissingReferenceException`, 한 번도 네이티브가 없던 객체는 `NullReferenceException`이었다. 에디터가 "있었다가 사라짐"과 "처음부터 없음"을 구분한다는 뜻이다. 구분 기준은 [01-L06](../L06_ReadBindings/README.md)에서 확인했다. `m_InstanceID != 0`이면 에디터가 `MissingReferenceException`을 던지고, `new`로 만든 래퍼는 ID가 0이라 이 분기를 타지 않는다. 플레이어는 두 경우 모두 `NullReferenceException`이었다.
  - `new`가 금지된 이유: 컴포넌트는 네이티브 쪽에서 생성되고 GameObject에 등록된 다음 매니지드 래퍼와 연결되어야 한다. C# 생성자는 이 과정 없이 래퍼만 만든다. 반대로 Unity가 `AddComponent`나 씬 로드 중에 래퍼를 만들 때는 생성자를 직접 호출한다(호출 시점과 스레드는 01-L03).
- 근거: 실측 CSV, 플레이어 로그 `Notes/results/player_01-L02.log`(로컬 전용)에 경고 문자열 없음
