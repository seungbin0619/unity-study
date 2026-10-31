# 01-L01 가짜 null(fake null)과 null 관련 연산자

## 목표
`Destroy` 후 매니지드 래퍼(wrapper)는 남고 네이티브 객체만 사라진 상태에서, C#의 null 관련 연산자(`==`, `is null`, `ReferenceEquals`, 암묵적 bool, `?.`, `??`)가 각각 어떤 결과를 내는지 같은 프레임과 다음 프레임에서 확인한다. 어떤 연산자가 `UnityEngine.Object`의 오버로드를 타고 어떤 연산자가 타지 않는지 구분하는 것이 목적이다.

## 예측
- 질문: 아래 코드에서 `Destroy` 직후(같은 프레임)와 다음 프레임에 각 식의 결과는?

  ```csharp
  var go = new GameObject("Target");
  var fallback = new GameObject("Fallback");
  Object.Destroy(go);
  // (A) 같은 프레임, (B) 다음 프레임에서 평가
  ```

  | # | 식 | (A) 같은 프레임 | (B) 다음 프레임 |
  |---|---|---|---|
  | 1 | `go == null` | | |
  | 2 | `go is null` | | |
  | 3 | `ReferenceEquals(go, null)` | | |
  | 4 | `(bool)go` | | |
  | 5 | `go?.name` | | |
  | 6 | `(go ?? fallback).name` | | |

- 학습자 답 (실행 전, 2026-10-29):

  | # | (A) 같은 프레임 | (B) 다음 프레임 | 이유 |
  |---|---|---|---|
  | 1 | `false` | `true` | |
  | 2 | `false` | `false` | |
  | 3 | `false` | `false` | |
  | 4 | `false` | `false` | |
  | 5 | 예외 | `null` | (A) 내부 참조가 null이라 `name`을 가져올 수 없음 |
  | 6 | `"Fallback"` | `"Fallback"` | `(bool)go == false`라서 `fallback`의 이름을 참조 |

## 실습 구성
- 코드: `FakeNullLab.cs` — Destroy 직후와 `Awaitable.NextFrameAsync()` 뒤에 6개 식을 평가하고, 예외가 나면 예외 타입을 기록
- 실행: 에디터 메뉴 `Labs/01/L01` 또는 플레이어 `-lab 01-L01`

## 결과

| 실행일 | 백엔드 | 빌드 | # | 식 | (A) 같은 프레임 | (B) 다음 프레임 |
|---|---|---|---|---|---|---|
| 2026-10-29 | Editor (Mono) | 에디터 플레이 모드 | 1 | `go == null` | `false` | `true` |
| | | | 2 | `go is null` | `false` | `false` |
| | | | 3 | `ReferenceEquals(go, null)` | `false` | `false` |
| | | | 4 | `(bool)go` | `true` | `false` |
| | | | 5 | `go?.name` | `"Target"` | `MissingReferenceException` |
| | | | 6 | `(go ?? fallback).name` | `"Target"` | `MissingReferenceException` |
| 2026-10-29 | Editor (Mono) | 에디터 플레이 모드, 러너를 `Awaitable`로 교체 후 재실행 | 1~6 | (위와 동일) | 동일 | 동일 |
| 2026-10-29 | Mono | Development 플레이어 (`-batchmode -nographics`) | 1~4 | | 에디터와 동일 | 에디터와 동일 |
| | | | 5 | `go?.name` | `"Target"` | **`NullReferenceException`** |
| | | | 6 | `(go ?? fallback).name` | `"Target"` | **`NullReferenceException`** |

원본: `Notes/results/01-L01_Editor_2026-10-29.csv` (15:14 코루틴 러너, 15:18 Awaitable 러너), `Notes/results/01-L01_Mono_2026-10-29.csv`

## 해석
- **연산자는 두 그룹으로 나뉜다.**
  - Unity 오버로드를 타는 것: `==`, `!=`, 암묵적 `bool` (1, 4). 둘 다 `Object.CompareBaseObjects`를 거치며 "네이티브 객체가 살아 있는가"까지 확인한다. 그래서 1과 4는 항상 반대 값이다.
  - C# 언어가 직접 처리하는 것: `is null`, `ReferenceEquals`, `?.`, `??` (2, 3, 5, 6). 매니지드 참조가 실제로 null인지만 본다. C#은 `?.`와 `??`의 오버로드를 허용하지 않아서, 컴파일러가 단순 참조 비교 IL을 낸다.
- **같은 프레임 (A)**: `Destroy`는 지연 파괴라서 네이티브 객체가 아직 살아 있다. 모든 식이 "살아 있는 객체"처럼 동작한다.
- **다음 프레임 (B)**: 네이티브 객체만 사라지고 매니지드 래퍼는 남는다(가짜 null). 그래서 2, 3은 여전히 "null 아님"이고, 5와 6도 같은 그룹이라 `go`를 그대로 통과시킨다. 그 결과 `name` getter가 네이티브 쪽을 찾다가 `MissingReferenceException`을 던진다. 5가 `null`을 돌려주거나 6이 `fallback`으로 빠지는 일은 없다.
- 예측과 비교: 1~3은 맞음. 4(A), 5, 6에서 어긋남.
  - 4(A): `Destroy` 즉시 파괴된 것으로 봤지만, 같은 줄에서 1(A)는 `false`(살아 있음)로 답했다. 1과 4는 같은 검사의 부정이라 함께 움직여야 한다.
  - 5, 6: `?.`와 `??`를 오버로드를 타는 그룹(`bool`)으로 분류했다. 실제로는 2(`is null`)와 같은 그룹이다. 2를 두 프레임 모두 `false`로 맞혔으니, 그 결론을 5, 6에 그대로 적용하면 된다.
- 한 단계 더:
  - Rider와 Microsoft.Unity.Analyzers가 `?.`와 `??`에 경고(UNT0008, UNT0007)를 띄우는 이유가 이 결과다. 안전한 형태는 `if (go) go.DoX();` 또는 `go != null ? go : fallback`이다.
  - 같은 이유로 `TryGetComponent(out var c)`가 반환한 값이나 `?.Invoke()` 이벤트 패턴에서도, 대상이 `UnityEngine.Object`이면 파괴 여부를 따로 확인해야 한다.
  - **예외 타입이 환경마다 다르다.** 에디터에서는 `MissingReferenceException`, Mono Development 플레이어에서는 `NullReferenceException`이었다. 연산자 결과(1~4)는 같고 예외 타입만 다르다. 따라서 `catch (MissingReferenceException)`로 파괴된 객체 접근을 잡는 코드는 빌드에서 동작하지 않는다.
  - 예외 타입이 다른 원인은 [01-L06](../L06_ReadBindings/README.md)에서 확인했다. `TryThrowEditorNullExceptionObject`가 instance ID ≠ 0이면 `MissingReferenceException`을 던지는데, 플레이어용 CoreModule에서는 이 메서드 본문이 비어 있다. IL2CPP 플레이어 결과는 IL2CPP 모듈 설치 후 추가한다.
- 근거:
  - 실측 CSV (위)
  - UnityCsReference `Runtime/Export/Scripting/UnityEngineObject.bindings.cs` — `operator ==`, `implicit operator bool`, `CompareBaseObjects`, `IsNativeObjectAlive`
  - C# 언어 사양: `?.`, `??`는 사용자 정의 연산자 오버로드 대상이 아님
