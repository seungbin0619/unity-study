# 04-L01 인라인 직렬화: 공유 참조·다형성·null

## 목표
일반 `[Serializable]` 클래스 필드가 직렬화 → 역직렬화(`Instantiate` 복제, 도메인 리로드)를 거친 뒤 공유 참조·런타임 타입·null이 어떻게 되는지 확인한다. 같은 필드를 `[SerializeReference]`로 선언했을 때와 비교한다.

## 예측
- 질문: `Item a, b, c, d` 필드를 가진 `Holder`에서 `a = b = shared`, `c = new Special { value = 2, bonus = 5 }`(선언 타입 `Item`), `d = null`로 두고 `Instantiate(h)`하면 복제본에서 (1) `ReferenceEquals(copy.a, copy.b)`는? (2) `copy.c`의 런타임 타입과 `bonus`는? (3) `copy.d`는 null인가?
- 학습자 답 (실행 전, 원문 그대로):
  1. "false (직렬화 -> 역직렬화하며 shared 또한 새롭게 역직렬화됨)"
  2. "Special, 남음"
  3. "null"

## 실습 구성
- 코드: `InlineSerializationLab.cs` — `InlineHolder`(일반 필드)와 `RefHolder`(`[SerializeReference]`)에 같은 값을 넣고, `Instantiate` 전후의 원본과 복제본에서 a·b·c·d, `ReferenceEquals(a, b)`, `a.value = 99` 뒤 `b.value`를 기록한다. `JsonUtility.ToJson`으로 직렬화 결과 텍스트도 남긴다
- 실행: 에디터 메뉴 `Labs/04/L01 Inline Serialization` 또는 플레이어 `-lab 04-L01`
- 04-L01b (에디터 편집 모드): 숨긴 GameObject에 두 Holder를 만들고 `[NonSerialized] int`에 7을 넣은 뒤 `EditorUtility.RequestScriptReload()`. 리로드 뒤 같은 항목을 기록한다. 메뉴 `Labs/04/L01b Domain Reload`

## 결과
2026-11-18, 에디터 + 배치모드 Development 플레이어(Mono). 에디터와 Mono의 기록이 한 줄도 다르지 않다. IL2CPP는 실행하지 않았다.

| 항목 | 원본 (전) | 일반 필드: 복제본 | 일반 필드: `Instantiate` 뒤 **원본** | `[SerializeReference]`: 복제본 |
|---|---|---|---|---|
| `ReferenceEquals(a, b)` | true | **false** | true | true |
| `a.value = 99` 뒤 `b.value` | — | 1 | — | 99 |
| `c` | `Special(2, 5)` | **`Item(2)`**, `bonus` 사라짐 | `Special(2, 5)` | `Special(2, 5)` |
| `d` | null | **`Item(0)`** | **`Item(0)`** (원본도 바뀜) | null |

- `ReferenceEquals(original.a, copy.a)`는 두 방식 모두 false (복제본은 새 객체).
- `JsonUtility.ToJson(원본)`:
  - 일반 필드: `{"a":{"value":1},"b":{"value":1},"c":{"value":2},"d":{"value":0}}` — 타입 이름도, 공유 표시도, null 표시도 없다.
  - `[SerializeReference]`: `{"a":{"rid":1000},"b":{"rid":1000},"c":{"rid":1001},"d":{"rid":-2},"references":{"version":2,"RefIds":[…{"rid":1000,"type":{"class":"Item",…},"data":{"value":1}},{"rid":1001,"type":{"class":"Special",…},"data":{"value":2,"bonus":5}}]}}` — 필드에는 id만, 객체는 타입 이름과 함께 `references` 표에 한 번씩. null은 `rid -2`.

### 04-L01b 도메인 리로드 (에디터 편집 모드)
`RequestScriptReload()` 뒤 같은 GameObject의 컴포넌트를 다시 읽음. 결과는 `Instantiate` 복제본과 같다.

| 항목 | 일반 필드 | `[SerializeReference]` |
|---|---|---|
| `ReferenceEquals(a, b)` / `a.value = 99` 뒤 `b.value` | false / 1 | true / 99 |
| `c` | `Item(2)` | `Special(2, 5)` |
| `d` | `Item(0)` | null |
| `[NonSerialized] int` (리로드 전 7) | 0 | 0 |

원본: `Notes/results/04-L01_Editor_2026-11-18.csv`, `Notes/results/04-L01_Mono_2026-11-18.csv`, `Notes/results/04-L01b_Editor_2026-11-18.csv`

## 해석
- 예측과 비교: (1) false는 이유까지 맞음. (2) `Special`·`bonus` 남음, (3) null은 틀림. 세 결과의 원인은 하나다. 일반 `[Serializable]` 필드는 **값 타입(struct)처럼** 저장된다. 데이터에는 선언 타입(`Item`) 레이아웃의 필드 값만 있다(JSON 참고). 객체가 누구인지(identity), 실제 타입이 무엇인지, null인지를 적을 칸이 없다. 그래서 역직렬화는 "선언 타입 `Item`을 새로 만들고 값을 채우는 것"밖에 할 수 없다. (1)을 설명할 때 쓴 "새로 역직렬화된다"는 추론을 (2)·(3)에도 적용해 "데이터에 무엇이 남아 있나?"를 물었다면 같은 결론이 나온다.
- 한 단계 더:
  - **원본도 바뀐다.** `Instantiate`가 원본을 직렬화(쓰기)하는 동안 원본의 null 필드 `d`가 `Item(0)`으로 채워졌다. 읽기 전용 작업처럼 보여도 직렬화기가 매니지드 객체를 건드린다. 원인 추론(확인 필요): Unity 직렬화는 같은 `Transfer` 코드로 읽기·쓰기를 모두 하고, 인라인 필드는 쓸 때도 객체가 있어야 필드를 읽을 수 있어서 null이면 만들어 넣는다. 결과적으로 "null로 표시한 선택적 데이터"는 직렬화를 한 번 거치면 사라지고 기본값 객체가 된다. 이 객체도 GC 할당이다.
  - **도메인 리로드도 같은 경로.** 에디터에서 스크립트가 컴파일될 때마다 모든 컴포넌트가 직렬화 → 역직렬화되므로 공유 참조가 조용히 갈라지고, `[NonSerialized]`·`Dictionary` 같은 직렬화되지 않는 상태는 기본값으로 돌아간다. 에디터 툴에서 "컴파일 한 번 뒤 캐시가 비거나 같은 객체가 둘로 나뉜다"는 버그의 원인이다.
  - **`[SerializeReference]`의 대가.** 객체를 `references` 표에 한 번만 쓰고 필드에는 id만 둔다. 그래서 공유·다형성·null이 보존된다. 대신 데이터에 **타입 이름(클래스·네임스페이스·어셈블리)**이 들어간다. 클래스 이름을 바꾸면 기존 데이터를 읽지 못해 `[MovedFrom]`이 필요하다. 코드에서 그 타입을 안 쓰고 데이터에서만 참조하면 Stripping이 타입을 지울 수 있다는 점(01-L10과 같은 구조)도 추론되지만 확인이 필요하다.
- 근거: Unity Manual "Script serialization"(사용자 정의 클래스는 struct처럼 인라인 직렬화, 다형성·null 미지원), Scripting API `SerializeReference`. 원본 변경·도메인 리로드는 이 실습의 실측.
