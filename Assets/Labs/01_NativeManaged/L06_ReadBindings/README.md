# 01-L06 UnityCsReference 바인딩 코드 읽기 — null 예외는 어디서 갈리나

## 목표
01-L01과 01-L02에서 관찰한 예외 타입 차이를 소스와 실측으로 설명한다.

| 상태 | 에디터 | Mono 플레이어 |
|---|---|---|
| `Destroy`된 객체 (01-L01) | `MissingReferenceException` | `NullReferenceException` |
| `new`로 만든 객체 (01-L02) | `NullReferenceException` | `NullReferenceException` |

1. UnityCsReference(`6000.3.25f1` 태그)에서 예외를 고르는 경로를 찾아 에디터의 두 경우가 갈리는 조건을 확인한다.
2. 같은 소스인데 플레이어에서는 왜 결과가 다른지 실행 중인 `UnityEngine.CoreModule`을 직접 조사해 확인한다.

## 읽은 소스 (요약, 원문은 링크)
- [`Runtime/Export/Scripting/BindingsHelpers.cs`](https://github.com/Unity-Technologies/UnityCsReference/blob/6000.3.25f1/Runtime/Export/Scripting/BindingsHelpers.cs) — `ThrowHelper.ThrowNullReferenceException(obj)`: 바인딩 코드가 네이티브 포인터를 얻지 못했을 때 호출된다. `obj`가 `UnityEngine.Object`면 먼저 `TryThrowEditorNullExceptionObject`를 부르고, 거기서 던지지 않으면 `NullReferenceException`을 던진다.
- [`Runtime/Export/Scripting/UnityEngineObject.bindings.cs`](https://github.com/Unity-Technologies/UnityCsReference/blob/6000.3.25f1/Runtime/Export/Scripting/UnityEngineObject.bindings.cs) — `MarshalledUnityObject.TryThrowEditorNullExceptionObject(obj, parameterName)` (988행 부근), 흐름을 요약하면:

  ```
  error = obj.m_UnityRuntimeErrorString ?? ""
  if (obj.m_InstanceID != 0  &&  error가 "MissingReferenceException:"으로 시작하지 않음)
      throw MissingReferenceException("… has been destroyed but you are still trying to access it …")
  if (error가 "<예외타입>:<메시지>" 형태)
      그 타입의 예외를 만들어 throw
  // 아무것도 안 던지면 호출자가 NullReferenceException을 던짐
  ```

## 예측
- 질문 1 (소스 읽기): 에디터에서 01-L01은 `MissingReferenceException`, 01-L02는 `NullReferenceException`이었다. 위 흐름의 어느 조건이 두 경우를 가르는가? 각 객체의 그 값은 무엇인가?
- 학습자 답 (실행 전, 2026-10-29):
  - 1차: 내부 네이티브 포인터가 null인지 여부 (`new`는 null, `AddComponent`는 null 아님) → 비교 대상을 잘못 잡음. 두 객체 모두 포인터가 null이라 `ThrowHelper`까지 온 것이므로 포인터로는 갈리지 않음. 힌트 1회 제공
  - 2차: `Destroy`된 객체는 instance ID가 0이 아니라서 `MissingReferenceException`으로 분기하고, `new`로 만든 객체는 instance ID가 0이다 → 정답

- 질문 2 (실측 예측): 같은 소스인데 Mono 플레이어에서는 `Destroy`된 객체(instance ID ≠ 0)도 `NullReferenceException`이었다. 그 이유는?
- 학습자 답 (실행 전, 2026-10-29): 처음엔 "모르겠음" → 힌트 1회(에디터와 플레이어는 서로 다른 `UnityEngine.CoreModule.dll`을 쓴다, 함수 이름의 `Editor`) → "에디터에서는 instance ID 기반으로 `MissingReferenceException`이 나도록 하지만, 플레이어용 DLL에서는 그 기능을 제거한 것 같다"

## 실습 구성
- 코드: `ReadBindingsLab.cs` — 실행 중인 `UnityEngine.CoreModule`에서 리플렉션으로 `MarshalledUnityObject.TryThrowEditorNullExceptionObject`를 찾는다. IL 바이트 수와 IL 안의 `ldstr` 문자열(0x72 + user string 토큰)을 기록해 에디터와 플레이어를 비교한다.
- 보조 측정: DLL 파일 자체에 경고 메시지 문자열(UTF-16)이 들어 있는지 바이트 검색 (에디터 설치 폴더 / 플레이어 원본 Variations / 빌드 결과)
- 실행: 에디터 메뉴 `Labs/01/L06 Read Bindings` 또는 플레이어 `-lab 01-L06`

## 결과

`TryThrowEditorNullExceptionObject(Object, string)` 리플렉션 결과

| 실행일 | 백엔드 | 빌드 | 메서드 존재 | IL 바이트 | `ldstr` 문자열 |
|---|---|---|---|---|---|
| 2026-10-29 | Editor (Mono) | 에디터 플레이 모드 | 있음 | **234** | 7개 (아래) |
| 2026-10-29 | Mono | Development 플레이어 | 있음 | **2** | **0개** |
| 2026-10-29 | Editor (Mono) | 에디터 플레이 모드, Run In Background 켠 뒤 동작 확인용 재실행 | 있음 | 234 | 7개 (동일) |

에디터의 `ldstr` 문자열 7개: `""`, `"MissingReferenceException:"`, `"The object of type '"`, `"' has been destroyed but you are still trying to access it."`, `" Parameter name: "` ×2, `"UnityEngine."`

DLL 파일 바이트 검색 (2026-10-29, 경고 메시지 UTF-16 / 메서드 이름 ASCII)

| DLL | 메시지 문자열 | 메서드 이름 | 크기 |
|---|---|---|---|
| 에디터 `Editor/Data/Managed/UnityEngine/UnityEngine.CoreModule.dll` | 있음 | 있음 | 2,175,488 B |
| 플레이어 원본 `PlaybackEngines/windowsstandalonesupport/Variations/win64_player_development_mono/.../UnityEngine.CoreModule.dll` | **없음** | 있음 | 1,985,968 B |
| 빌드 결과 `Builds/Mono/Labs_Data/Managed/UnityEngine.CoreModule.dll` | **없음** | 있음 | 1,985,960 B |

원본: `Notes/results/01-L06_Editor_2026-10-29.csv`, `Notes/results/01-L06_Mono_2026-10-29.csv`

## 해석
- 예측과 비교: 질문 1은 힌트 후 정답. 질문 2는 힌트 후 "플레이어용 DLL에서 기능이 제거됐다"고 예측했고, 실측이 이를 뒷받침한다.
- **에디터의 두 경우를 가르는 것은 `m_InstanceID != 0`이다.** 네이티브 포인터는 두 경우 모두 없다(그래서 `ThrowHelper`까지 왔다). 래퍼는 `Destroy` 후에도 instance ID를 기억하므로 "있었다가 사라짐"을 알 수 있다. `new`로 만든 래퍼는 ID가 0이다.
- **플레이어용 `UnityEngine.CoreModule`에서는 이 메서드가 빈 껍데기다.** 메서드와 시그니처는 남아 있지만 IL이 2바이트(사실상 반환만)이고 문자열이 하나도 없다. 그래서 `ThrowHelper`는 아무 예외도 받지 못하고 항상 `NullReferenceException`을 던진다. 이 차이는 빌드 과정의 스트리핑 때문이 아니다. 에디터 설치 폴더의 **플레이어 원본 DLL에서부터** 메시지가 없다. 즉 Unity가 플레이어용 DLL을 애초에 다르게 컴파일해서 배포한다.
- 한 단계 더:
  - **UnityCsReference는 에디터 구성의 소스로 보인다.** 저장소의 이 함수에는 조건부 컴파일 흔적이 없는데, 플레이어 DLL에서는 본문이 비어 있다. 따라서 이 저장소만 읽고 "플레이어에서도 이렇게 동작한다"고 결론 내리면 틀릴 수 있다. 에디터 전용 분기가 저장소에서 보이지 않는다는 점은 이 함수로 확인했고, 저장소 전체가 그런지는 확인 필요.
  - 메서드를 지우지 않고 빈 본문으로 남기는 이유(추정): 호출부(`ThrowHelper`, 각 모듈의 바인딩 코드)를 에디터와 플레이어에서 똑같이 유지할 수 있다. 진단용 문자열 조합, `Type.GetType`, `Activator.CreateInstance` 비용은 에디터에서만 낸다.
  - 실무 결론: `MissingReferenceException`은 **에디터 전용 진단**이다. 빌드에서 파괴된 객체 접근은 일반 `NullReferenceException`으로 보이므로, 크래시 리포트에서 둘을 구분할 수 없다.
- 남은 확인: IL2CPP 플레이어(모듈 미설치). IL 2바이트의 실제 opcode는 기록하지 않았다.
- 근거:
  - UnityCsReference `6000.3.25f1` 태그: [`BindingsHelpers.cs`](https://github.com/Unity-Technologies/UnityCsReference/blob/6000.3.25f1/Runtime/Export/Scripting/BindingsHelpers.cs) `ThrowHelper`, [`UnityEngineObject.bindings.cs`](https://github.com/Unity-Technologies/UnityCsReference/blob/6000.3.25f1/Runtime/Export/Scripting/UnityEngineObject.bindings.cs) `TryThrowEditorNullExceptionObject`, `CompareBaseObjects`, `IsNativeObjectAlive`
  - 실측 CSV와 DLL 바이트 검색 (위)
