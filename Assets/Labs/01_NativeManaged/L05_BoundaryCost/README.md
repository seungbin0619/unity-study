# 01-L05 경계 비용: `GetComponent` 캐싱, 배열 반환 API, `CompareTag`

## 목표
C#에서 네이티브로 넘어가는 호출 중 자주 쓰는 세 쌍을 비교해, 어떤 호출이 매번 GC 할당을 만들고 얼마나 느린지 측정한다. 결론은 Development 플레이어 기준으로 내고, 에디터 결과와의 차이도 기록한다.

## 예측
- 질문: 아래 각 줄을 한 번 호출할 때 **GC 할당이 생기는가? 생긴다면 대략 몇 바이트인가?** 그리고 각 쌍에서 **어느 쪽이 빠른가?**
  (`go`에는 `BoxCollider`가 붙어 있고 `Rigidbody`는 없다. `mesh`는 정점 1,000개, `list`는 미리 만들어 재사용하는 `List<Vector3>`, `go.tag`는 `"Player"`)

  | # | 호출 | 할당? (바이트) | 쌍에서 빠른 쪽 |
  |---|---|---|---|
  | 1a | `go.GetComponent<BoxCollider>()` (매번 조회) | | |
  | 1b | `cachedCollider` (필드에 캐싱한 참조 읽기) | | |
  | 1c | `go.GetComponent<Rigidbody>()` (없는 컴포넌트, 에디터 / 플레이어) | | (1a와 비교) |
  | 2a | `mesh.vertices` | | |
  | 2b | `mesh.GetVertices(list)` | | |
  | 3a | `go.tag == "Player"` | | |
  | 3b | `go.CompareTag("Player")` | | |

- 학습자 답 (실행 전, 2026-10-29):

  | # | 할당 | 빠른 쪽 |
  |---|---|---|
  | 1a | 수십 B | |
  | 1b | 0 | 1a·1b 중 가장 빠름 |
  | 1c | 0 (에디터·플레이어 구분 없음) | |
  | 2a | 정점 수에 비례 | |
  | 2b | 0 | 가장 빠름 |
  | 3a | `go`의 태그 길이에 비례 + (`"Player"` 첫 참조 시 그 길이만큼) | |
  | 3b | (`"Player"` 첫 참조 시 그 길이만큼) | 가장 빠름 |

## 실습 구성
- 코드:
  - `Common/LabMetrics.cs` — `ProfilerRecorder` 래퍼 (`GC Allocated In Frame`, `GC Allocation In Frame Count`, `GC Used/Reserved Memory`)
  - `BoundaryCostLab.cs` — 프레임 F에서 한 케이스를 N번(1,000 또는 100) 실행하며 `Stopwatch`로 시간을 재고, 프레임 F+1에서 F의 할당 카운터를 읽는다. 워밍업 3프레임 후 10프레임 측정. `0a`(빈 루프)는 프레임 자체의 잡음, `0b`(`new byte[1024]`)는 측정 경로 보정용
- 실행: 에디터 메뉴 `Labs/01/L05 Boundary Cost` 또는 플레이어 `-lab 01-L05`

## 결과
호출당 값. 할당은 (프레임 할당 − 기준선 `0a`) ÷ N. 에디터는 기준선이 프레임당 9,301 B / 218회(에디터 자체 할당), 플레이어는 0 B / 0회.

| 실행일 | 백엔드 | 빌드 | # | 호출 | 할당 B/호출 (할당 횟수/호출) | 시간 ns/호출 (평균) |
|---|---|---|---|---|---|---|
| 2026-10-29 | Mono | Development 플레이어 | 0b | `new byte[1024]` (보정) | 1,056 (1) | 1,400 |
| | | | 1a | `GetComponent<BoxCollider>()` | 0 | 20.3 |
| | | | 1b | 캐싱한 필드 | 0 | 0.8 |
| | | | 1c | `GetComponent<Rigidbody>()` (없음) | 0 | 19.7 |
| | | | 2a | `mesh.vertices` (정점 1,000) | 12,032 (1) | 14,615 |
| | | | 2b | `mesh.GetVertices(list)` | 0 | 1,080 |
| | | | 3a | `go.tag == "Player"` | 34 (1) | 231 |
| | | | 3b | `go.CompareTag("Player")` | 0 | 44 |
| 2026-10-29 | Editor (Mono) | 에디터 플레이 모드 | 0b | `new byte[1024]` (보정) | 1,056 (1) | 425 |
| | | | 1a | `GetComponent<BoxCollider>()` | 0 | 38.2 |
| | | | 1b | 캐싱한 필드 | 0 | 1.3 |
| | | | 1c | `GetComponent<Rigidbody>()` (없음) | **594 (2)** | **1,562** |
| | | | 2a | `mesh.vertices` | 12,032 (1) | 2,864 |
| | | | 2b | `mesh.GetVertices(list)` | 0 | 1,242 |
| | | | 3a | `go.tag == "Player"` | 34 (1) | 189 |
| | | | 3b | `go.CompareTag("Player")` | 0 | 88 |

원본: `Notes/results/01-L05_Editor_2026-10-29.csv`, `Notes/results/01-L05_Mono_2026-10-29.csv` (최소·최대값 포함)

재실행 (2026-10-29, 반환값 확인 추가): 할당 수치는 모두 같았고 시간은 ±15% 범위. 플레이어 1a 19.8 / 1b 0.8 / 2a 12,097 / 2b 923 / 3a 188 / 3b 42 ns.

없는 컴포넌트 조회(1c)가 돌려주는 값

| 식 | 에디터 | Mono 플레이어 |
|---|---|---|
| `GetComponent<Rigidbody>() is null` | **`false`** | `true` |
| `GetComponent<Rigidbody>() == null` | `true` | `true` |
| `GetComponent<Rigidbody>()?.mass` | **`MissingComponentException`** | `null` |

## 해석
- 예측과 비교: 1b·2a·2b·3a·3b 맞음. 1a는 "수십 B"로 예측했지만 존재하는 컴포넌트 조회는 할당이 없다. 1c는 플레이어 기준으로 맞았지만, 에디터에서는 호출마다 약 600 B / 2회 할당이 있었다.
- **1a vs 1b (약 25배)**: 학습자 설명(경계를 넘어 컴포넌트 목록에서 타입이 같은 것을 찾아 반환 vs 단순 필드 읽기)이 맞다. 한 번의 `GetComponent<T>()`에는 다음이 들어 있다.
  - 매니지드 → 네이티브 호출: `this`(GameObject 래퍼)에서 네이티브 포인터를 꺼낸다(01-L06의 `MarshalledUnityObject.Marshal`)
  - 타입 변환: 제네릭 `T` → `System.Type` → 네이티브 타입 정보
  - 네이티브 쪽 컴포넌트 목록 순회와 타입(상속 포함) 비교
  - 찾은 네이티브 컴포넌트에 연결된 매니지드 래퍼를 찾아 반환하고 매니지드로 복귀

  캐싱한 필드는 이 중 아무것도 하지 않는 메모리 읽기 1번이다.
- **2a vs 2b**: 둘 다 네이티브 정점 버퍼를 매니지드 메모리로 복사한다. 차이는 **복사 대상**이다. `vertices`는 호출마다 `Vector3[1000]`(12,000 B + 헤더 32 B)을 새로 만들고, `GetVertices(list)`는 이미 있는 리스트의 버퍼에 채운다. 그래서 2b도 1 µs가량 걸리지만 할당은 0이다.
- **3a vs 3b**: `tag` getter는 네이티브 문자열을 매니지드 `string`으로 새로 만들어 돌려준다(`"Player"` = 34 B, 호출마다 1개). `CompareTag`는 반대로 매니지드 문자열을 네이티브로 넘겨 네이티브 쪽에서 비교하므로 결과로 `bool`만 돌아온다. **경계를 넘는 방향과 넘어오는 데이터의 종류가 할당을 결정한다.**
- **1c (에디터 전용 가짜 null)**: 에디터의 `GetComponent`는 없는 컴포넌트에 대해 진짜 `null` 대신 **오류 메시지를 품은 가짜 null 객체**를 돌려준다. 할당 2회는 래퍼 객체와 메시지 문자열이다. 이 객체에 접근하면 01-L06에서 본 `m_UnityRuntimeErrorString` 경로로 `MissingComponentException`("어느 오브젝트에 어떤 컴포넌트가 없다")이 난다. 플레이어는 비용을 아끼려고 진짜 `null`을 돌려준다.
  - 그 결과 **`GetComponent<T>() is null`의 결과가 에디터(`false`)와 플레이어(`true`)에서 다르다.** `?.`도 에디터에서는 예외, 플레이어에서는 `null`이다. 01-L01에서 본 "연산자 그룹" 규칙이 환경 차이와 겹쳐서, 같은 코드가 에디터와 빌드에서 다르게 동작하는 사례다.
- 한 단계 더:
  - 에디터 프로파일러에서 "`GetComponent`가 GC 할당을 만든다"는 결론을 내기 쉽다. 이번 측정처럼 **없는 컴포넌트 조회만**, **에디터에서만** 할당이 생긴다. `TryGetComponent`는 Unity 2019.2에 이 에디터 할당을 피하려고 추가된 API다(Unity 문서 `Component.TryGetComponent` 설명).
  - 2a 시간은 플레이어(12~15 µs)가 에디터(2.9 µs)보다 길었다. 프레임당 1.2 MB를 할당하는 케이스라 측정 구간 안에서 GC가 돌았을 가능성이 있다(확인 필요, 02-L04·02-L06에서 GC 이벤트와 함께 다시 측정).
- 남은 확인: IL2CPP 플레이어. `TryGetComponent`의 실제 할당·시간 비교(확장 후보).
- 근거: 실측 CSV, UnityCsReference `UnityEngineObject.bindings.cs`(01-L06), Unity 블로그 "Custom == operator, should we keep it?"(2014, 에디터 가짜 null의 목적).
