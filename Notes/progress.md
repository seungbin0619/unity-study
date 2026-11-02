# 진행 기록

## 완료한 개념
- 1. 네이티브–매니지드 경계: 래퍼 구조, 가짜 null, 생성자 금지·Destroy 지연·연쇄 파괴, 마샬링 비용, IL2CPP 파이프라인·생성 코드·sealed·제네릭 공유·스트리핑
- 2. 메모리와 GC: 메모리 영역, Boehm GC 특성, 숨은 할당, 측정·감소 기법, 블록/크기 클래스, 정밀·보수적 마킹, pointer-free 객체, Used/Reserved, Incremental GC와 쓰기 배리어, 파이널라이저

## 퀴즈 기록
- [정답] Destroy 후 같은 프레임/다음 프레임/is null 예측
- [정답] 값 타입 3 + 참조 타입 공유 1 = List<T>.Add 4벌
- [오답→풀이] GetComponent 캐싱을 "경계 넘기" 관점으로 설명하기
- [부분 정답] 매 프레임 할당 줄 고르기 — 인터페이스 타입 foreach의 열거자 박싱(F)을 놓침
- [부분 정답] (2026-10-29) 10만 개 GC 마킹 순서 — 답: 2<1<3, 1≈2. 설계3이 참조 때문에 스캔된다는 이유는 맞음. 설계1도 객체 10만 개 방문 + 참조 배열 스캔이라 2와 비슷하지 않다는 점을 놓침 (정답 2 < 3 ≤ 1, 02-L05로 실측 예정)
- [부분 정답 7/12] (2026-10-29) 01-L01 null 연산자 6개 × 2프레임 — `==`/`is null`/`ReferenceEquals`는 맞음. `(bool)go`(A)와 `?.`/`??`(A·B)를 틀림: `?.`/`??`를 Unity 오버로드를 타는 쪽으로 분류함
- [정답 (에디터 기준)] (2026-10-29) 01-L02 `new` vs `AddComponent` — 4문항 모두 맞음. 01-L01 약점(연산자 그룹)을 바로 적용함. 경고가 플레이어에서 안 나온다는 점은 고려하지 않음
- [힌트 후 정답] (2026-10-29) 01-L06 Q1 에디터 예외 분기 조건 — 1차에 네이티브 포인터로 답하고 비교 대상도 `new` vs `AddComponent`로 잘못 잡음. 힌트 후 `m_InstanceID != 0` 정답
- [힌트 후 정답] (2026-10-29) 01-L06 Q2 플레이어에서 다른 이유 — "모르겠음" → 힌트(DLL이 다름, 함수 이름) 후 "플레이어 DLL에서 기능 제거" 정답
- [부분 정답] (2026-10-29) 01-L03 생성자 횟수·스레드·순서 — 횟수 4/4, 스레드 3/4(동기 `LoadScene`을 메인으로 예측), 순서 1/4(2\~4를 "Awake 이후"로 예측 — 생성자는 항상 Awake보다 먼저이고 사이에 역직렬화가 낀다)
- [정답] (2026-10-29) 01-L04 연쇄 파괴·구독 누수 — 4문항 모두 맞음. 에디터/플레이어 예외 타입 차이를 스스로 적용함 (01-L01·01-L06 약점 회복 신호)
- [부분 정답 5/7] (2026-10-29) 01-L05 경계 비용 할당·속도 — 1a를 "수십 B"로 예측(실제 0), 1c의 에디터 전용 할당을 놓침. 나머지 맞음
- [정답] (2026-10-29) 약점 재확인: `GetComponent` vs 캐싱을 경계 관점으로 설명 — "경계를 넘어 컴포넌트 목록에서 타입 비교 후 반환 vs 단순 필드 읽기". 타입 변환·래퍼 반환 단계는 보충함

- [정답] (2026-10-30) 01-L07 `NullCheck` 개수 (A 3 / B 2 / C 0) — 개수는 모두 맞음. (C)의 이유를 "참조 x"로 답했으나(`this`도 역참조), 후속 질문에서 "this가 null이면 호출 자체가 안 됨"으로 정답
- [정답] (2026-10-30) 01-L07 추가: 직접 `if (a == null)`을 넣어도 `NullCheck` 2개가 남는다 — IL2CPP는 명령어 단위로 변환하고 앞의 검사 결과를 전달하지 않음. 측정에서도 E ≈ B ≫ D
- [부분 정답 3/4] (2026-10-31) 01-L08 가상/직접 호출 — A·B·C를 가상, D를 직접으로 예측. 실제로는 IL2CPP가 넷 다 가상 호출을 생성함. D의 추론(메서드 sealed → 구현 확정)은 이론상 옳음. C는 "자식에서 상속 가능"이라는 틀린 이유로 답이 맞음
- [힌트 후 정답] (2026-10-31) 01-L08 `SealedDog d`에 들어올 수 있는 런타임 타입 — "모르겠음" → 힌트(대입하려면 상속해야 함, sealed는 상속 금지) 후 "SealedDog 하나"
- [정답] (2026-10-31) 01-L08 가설 검증 — (E) `callvirt Fox::Speak`(final) 직접 / (F) `callvirt OpenFox::Speak`(final 아님) 가상, 이유 포함 정답. 결론: IL2CPP 6000.3은 IL 호출 대상 메서드가 final인지만 보고, 수신 객체의 정적 타입은 보지 않음
- [오답] (2026-10-31) 01-L09 `Faster (smaller) builds`에서 `Add` 본문 수 — "여전히 값 타입 3개, 참조 타입1개"로 답함(이유 없음). 실제 1벌. full generic sharing이 크기를 런타임 메타데이터에서 읽어 값 타입까지 공유한다는 점을 몰랐음
- [힌트 후 정답] (2026-10-31) 01-L09 기본 설정에서 `List<string>.Add`가 `List<int>.Add`보다 4배 느린 이유 — 1차 "참조 타입은 타입마다 크기가 달라서"(오답: 배열에는 8바이트 포인터가 들어감) → 힌트(`SetAt`의 `Il2CppCodeGenWriteBarrier`, `gcIncremental: 1`) 후 "참조가 중간에 바뀌면 마킹을 다시 해야 해서 배리어" 정답. 배리어는 dirty 표시만 하고 마킹 중이 아니어도 항상 실행된다는 점은 보충함
- [부분 정답 1/2] (2026-10-31) 01-L10 Stripping High에서 리플렉션 대상 — "둘 다 null". B(런타임 조합)는 맞음. A(상수 문자열)는 UnityLinker가 패턴을 알아봐서 타입은 남고, 생성자가 지워져 `MissingMethodException`
- [정답] (2026-10-31) 01-L10 `[Preserve]` 붙인 클래스에서 `Ping`이 지워진 이유 — "각 메서드도 사용 안 되면 삭제해도 된다고 판단". `[Preserve]`는 붙인 대상(타입 + 기본 생성자)에만 적용된다는 점은 보충함
- [부분 정답 8/11] (2026-10-31) 02-L01 숨은 할당 A\~K — B의 할당 2회(string + 박싱)까지 맞음. E(static 메서드 그룹)를 X로(실제 매번 델리게이트 128 B, C# 9는 캐시 안 함), H(LINQ `Count(pred)`)를 X로(실제 박싱된 열거자 40 B), K(인자 없는 `params`)를 O로(실제 `Array.Empty` → 0) 틀림. C를 클로저만으로 설명함(델리게이트 128 B가 대부분)
- [부분 정답] (2026-10-31) 02-L01 G에서 할당되는 객체 — F가 0인 이유("`List<T>` 자체 struct `Enumerator`")는 맞음. G를 "yield 상태 할당"으로 답함. 실제로는 같은 struct `Enumerator`가 인터페이스 반환에서 박싱됨 (IL2CPP 생성 코드의 `Box(...)`로 확인)
- [부분 정답] (2026-10-31) 02-L01 추가: `x => IsMatch(x)`(인스턴스 메서드를 감싼 람다)의 호출당 할당 — "`_target` 때문에 1번". 횟수는 맞음. 원인은 필드가 아니라 `this` 캡처(필드를 안 읽는 N도 128 B), 객체는 클로저가 아니라 델리게이트(대상 = `this`) 하나
- [부분 정답 2/3] (2026-10-31) 02-L01 추가 2: struct predicate 받는 쪽 시그니처 — (a) 제네릭 제약 0, (b) 인터페이스 매개변수 1회 박싱 정답. (c) `List.Sort(new Desc())`는 "모르겠음" → 힌트((b)와 시그니처 비교) 후 "할당 없음"(오답). 실제 2회 145 B: 넘길 때 박싱 1 + `ArraySortHelper`가 `comparer.Compare`를 `Comparison<T>`로 감싸는 128 B. 힌트를 받고도 `IComparer<T>` 매개변수를 (b)와 같은 경우로 연결하지 못함
- [힌트 후 풀이] (2026-10-31) 02-L02 클로저 할당 시점 (A 매개변수 / B 맨 위 지역 / C 분기 안 지역 / D `this`만, 모두 람다 분기 미실행) — "전혀 모르겠는데" → 힌트(캡처 변수는 클로저 필드, lowering 예시, 02-L01 M) 후에도 "모르겠음" → 풀이. 실측: A·B 클로저 1개(20/24 B), C·D 0
- [유도 후 정답] (2026-10-31) 02-L02 확인 질문: `x => x == t && x > _target`의 클로저에 담기는 것 — 1차 "클로저가 들어가야지"(target 칸 = 클로저까지만), 유도 질문(람다 메서드의 `this`는 클로저) 후 "this가 있어야겠네" 정답. 풀이 직후 D(`this`만 캡처)를 "`_target`이 클래스 변수인지 감이 안 옴"이라고 해서 지역 변수(스택) vs 인스턴스 필드(힙 객체 안)와 델리게이트 (target, method) 구조를 설명함
- [부분 정답] (2026-10-31) 02-L03 `Instantiate`/`Destroy` vs 풀링 — "A는 프레임당 엄청 많이, B는 최초 프레임 제외 0, B가 훨씬 빠름"(배수는 추측 불가라고 함). B 0·B가 빠름(약 2.2배)은 맞음. A의 GC 할당은 객체당 64 B(프레임당 6.4 KB)로 작았음 — `Instantiate`의 대부분이 네이티브 메모리라는 점을 고려하지 않음
- [정답] (2026-11-01) 02-L04 `byte[50MB]` 할당 → 해제 + Collect → 10초 대기의 Used/Reserved — 세 단계 모두 맞음 (실행 전 (2)·(3)을 한 번 바꿔 적었다가 스스로 정정). "Reserved는 부족한 경우 증가"까지 짚음. 참조만 끊은 시점(2a)에 Used가 그대로라는 점(추적 GC)은 설명으로 보충
- [부분 정답] (2026-11-01) 02-L05 마킹 비용 순서 (0 기준선 / 1 `List<class>` / 2 `struct[]` / 3a 참조 필드 `struct[]` + 공유 문자열 / 3b 원소마다 문자열) — 답 "0, 2, 3a, 3b = 1"(GC Used 계산은 생략). 실측 0 ≈ 2 < 1 < 3a < 3b (두 백엔드, 10만·100만 개). 2가 기준선과 같다는 점, 3b가 비싸다는 점은 맞음. 3a가 1보다 비싸다는 점은 놓침 — "방문할 객체 수"만 세고 "훑어야 할 배열 바이트·스캔 방식"을 고려하지 않음 (개념 단계 정답 `2 < 3 ≤ 1`도 같은 이유로 틀렸음)
- [힌트 후 정답] (2026-11-01) 02-L05 약점 재확인: `EnemyClass[]`(100만 개 7.6 MB)가 `EnemyStruct[]`(15.3 MB)보다 작은 이유 — "진짜 모르겠음" → 힌트(칸당 8 B인데 객체는 16 B 이상, 3a의 공유 문자열) 후 "포인터" 정답. 객체 본체는 힙의 다른 곳에 따로 있어 합계는 배열 7.6 MB + 객체 30.5 MB(Mono)라는 점은 보충함
- [부분 정답] (2026-11-01) 02-L06 Incremental GC On/Off 프레임 그래프 — Off "한 번 크게 올랐다가 뚝 떨어짐" 정답(GC마다 약 10 ms 단일 스파이크). On "일정 / 천천히 오르다 약간 떨어짐" 대체로 맞음(GC 프레임 약 1 ms). 놓친 점: 약 20회마다 full 마킹 스파이크 3\~6 ms × 2\~3프레임, On이 총 시간도 적음(부분 수집이 장수 객체를 다시 마킹하지 않음, `GC_full_freq` 19)
- [정답] (2026-11-01) 02-L06 확인: `_live[123] = null` 때 쓰기 배리어가 dirty로 표시하는 곳 — 질문이 헷갈린다고 해서 그림으로 다시 냄(힌트 아님) → "`_live` 배열 쪽, 루트에서 나가는 트리가 바뀐 것" 정답. 끊긴 객체는 이전 마크가 남아 다음 full 수집까지 회수되지 않는다(floating garbage)는 점은 보충함. 배열 칸 = 포인터 약점과 연결해 맞힘
- [정답] (2026-11-02) 02-L06 확인 2: 부분 수집에서 새로 연결된 객체가 살아남는 이유 — 질문 전에 "dirty → 연결된 거라면 그 트리를 타고 새 마킹, 해제된 거라면 그대로"로 스스로 정리함. 그 전에 "null 대입 → dirty → 다음 GC에서 마킹 해제"로 오해(참조 카운팅식 사고) → 살아 있음은 길 하나, 죽음은 전체 추적이 필요하다는 비대칭을 설명함. incremental(마킹을 프레임에 나눔)과 부분 수집(수집 사이에 마크 유지)을 한 덩어리로 보고 있어 둘을 구분해 설명함
- [의견 검증] (2026-11-02) 02-L06 학습자 의견 "부분 수집은 생성 후 연결되지 않은 객체만 처리하니 실제 환경에서는 큰 이점이 없을 것" → 추가 측정(midlived·mutate). 부분적으로 맞음: 30프레임 사는 객체가 많으면 On 이점이 사라지고(총 시간 Off와 같음, Used +21\~27 MB), 장수 데이터를 넓게 고치면 On이 약 3배 손해. 단 "한 프레임 안에 죽는 쓰레기 + 안 바뀌는 장수 데이터"에서는 이점이 큼(기본 조건 250 vs 430 ms)
- [질문 오해 — 채점 보류] (2026-11-02) 02-L07 Manual + 매 프레임 `CollectIncremental(slice)`, slice 3 → 1 → 0.1 ms — 답 "(a) GC 프레임 시간 증가, (b) 3 ms는 Used 감소, 0.1 ms로 갈수록 결국 증가". 결과를 본 뒤 학습자가 slice를 "3 ms마다 실행"(호출 간격)으로 읽었다고 밝힘 — 질문 문구가 모호했음(출제 문제). 실측: 세 slice(시간 예산)의 총 시간·Used가 거의 같고, 호출마다 새 수집 시작(GC 600회), slice는 상한이 아님. 간격 해석의 "자주 부를수록 GC 시간 증가" 직관은 실측(수집 빈도 = 호출 빈도)과 같은 방향. 다음에 API 매개변수가 들어간 예측 질문은 단위·의미(간격인지 예산인지)를 명시하기
- [부분 정답 1.5/3] (2026-11-02) 02-L07 추가: 호출 간격 N = 1·5·20·60 (slice 3 ms) — 답 "(a) 감소, (b) 증가, (c) 이전과 동일". (a) 맞음(GC 합계 1/30, 단 프레임 합계는 N=20부터 포화). (b) 부분 수집 1회 비용은 맞게 늘었지만(IL2CPP 1 → 2\~5 ms) 프레임 최대는 오히려 줄었음 — 최대를 정하는 건 20회마다 오는 full이고 N이 클수록 드물어짐. (c) 틀림: N MB 진폭 톱니(최고 +80 MB). 앞 실험의 "평평한 Used"를 전제(1 MB마다 수집)가 바뀐 조건에 그대로 옮김
- [정답 4/4] (2026-11-02) 02-L08 Destroy + GC 후 남는 것 (Leaky 구독 해제 안 함 / Clean 해제) — GameObject 둘 다 X, `LeakHolder`·`byte[]` Leaky만 O(매니지드), `Texture2D` 둘 다 O(네이티브). 두 백엔드 실측과 일치. 텍스처를 네이티브로 분류함 — "엔진 API 비용을 매니지드 vs 네이티브로 나누기" 약점 회복 신호. 보충: `UnloadUnusedAssets`가 셸의 필드 참조 때문에 Leaky 텍스처를 못 푼다는 점, Clean 텍스처 래퍼가 네이티브가 살아 있는 동안 함께 산다는 점
- [부분 정답 1.5/2] (2026-11-02) 02-L09 참조를 끊은 뒤 GC Used가 돌아오는 `Collect` 횟수 (1프레임 간격, Wait 없음) — 답 "소멸자 없음 1회, 있음 2회 (1회 후 소멸자 대기, 2회차에 소멸)". 없음 1회 맞음. 있음은 원리(1회차에 살려서 대기열, 실행 후 다음 수집에서 회수)는 맞지만, 파이널라이저가 별도 스레드에서 비동기로 돈다는 점을 고려하지 않음 — 질문 조건에서 Mono 157\~184회, IL2CPP 14\~18회. 60fps면 IL2CPP 2회·Mono 3회, `WaitForPendingFinalizers`를 끼우면 2회
- [힌트 후 오답→풀이] (2026-11-02) 02-L09 확인: Finalizable 첫 `Collect`가 Plain보다 3\~4배 비싼 이유 — 1차 "파이널라이저가 있으면 왜 더 오래 걸리지"(사실상 모름) → 힌트(파이널라이저가 `this`·필드를 씀, 02-L05 비용 = 표시하는 객체 수 + 훑는 바이트) 후 "마킹되어 있던 this와 byte를 취소 처리". 방향이 반대: 추적 GC는 쓰레기를 표시한 적이 없고, 파이널라이저 객체는 오히려 **새로 마킹해 살려야** 함(객체 10만 + `byte[]` 10만, 산 객체와 같은 비용). Plain 쓰레기는 마킹 단계에서 한 번도 닿지 않아 비용 0. 02-L06의 "null 대입 → 마킹 해제" 오해와 같은 계열
- [설명 들음] (2026-10-30) 검사 개수(정적)와 실행 횟수(루프 안이면 원소마다) 구분, IL2CPP는 hoist·경계 검사 제거를 하지 않음 (RyuJIT과 대비)

## 약점 (관련 실습에서 재확인)
- 어떤 연산자가 `UnityEngine.Object` 오버로드를 타는지 구분 (`==`·`!=`·`bool`만 탐 / `is null`·`?.`·`??`·`ReferenceEquals`는 안 탐), `==`와 `bool`이 같은 검사라는 점
- 에디터와 플레이어의 차이를 "다른 DLL, 다른 코드"로 떠올리는 것 (Q2에서 힌트 필요) — 에디터 결과를 볼 때마다 "플레이어에서도 같은가?"를 묻기
- 객체 생성 파이프라인 순서: 생성자(필드 초기화) → 역직렬화(값 덮어쓰기) → Awake. "동기 API = 메인 스레드에서 실행"이라고 가정하는 것 (동기 LoadScene도 로딩 스레드에서 역직렬화)
- 성능 이슈를 네이티브 경계 비용 관점으로 설명하는 것 — 01-L05에서 핵심은 설명함(개선). 다음엔 "경계를 넘는 방향과 넘어오는 데이터 종류가 할당을 결정한다"까지 말하는지 확인
- 인터페이스를 거칠 때 struct가 박싱되는 지점 찾기 — 02-L01에서도 재발: G는 맞혔지만 원인을 "yield 상태"로 봤고, 같은 원인인 H(LINQ가 `IEnumerable<T>`로 받음)는 놓침. "API가 매개변수를 인터페이스로 받으면 내부 foreach에서 박싱"을 02-L02 이후 다시 묻기. 추가 2에서도 직접 만든 `FindIndexI(IPredicate)`는 맞혔지만 BCL `Sort(IComparer<T>)`는 같은 경우로 보지 못함 — 익숙한 BCL API의 시그니처를 읽는 습관 확인
- 델리게이트 생성 시점: 캐시되는 것(캡처 없는 람다)과 매번 생성되는 것(메서드 그룹(C# 9), 캡처 람다)을 구분, 델리게이트가 클로저보다 훨씬 크다는 점 (02-L01). 추가 질문에서도 "람다가 무엇을 캡처하는가"를 읽은 필드로 판단함 — 인스턴스 멤버를 호출하면 `this` 캡처. 02-L02에서 재확인 → 02-L02에서 컴파일러 lowering(캡처 변수 → 클로저 필드)을 머릿속으로 그리지 못해 힌트 후에도 풀이 필요. "클로저는 캡처 변수의 스코프 진입 시, 델리게이트는 람다 줄에서"를 다음 람다 관련 질문(02-L03 이후, 4장 이벤트)에서 다시 묻기
- 정적 타입(static type)으로부터 "들어올 수 있는 런타임 타입의 범위"를 추론하는 것 — `sealed class`면 런타임 타입이 하나로 확정된다는 점을 바로 떠올리지 못함 (01-L08, 힌트 필요). 01-L09·인터페이스 호출 관련 실습에서 다시 묻기
- 참조 타입 원소 컬렉션(List<class>)이 GC에게는 "객체 N개 + 참조 N개"라는 점 — 원소 클래스가 pointer-free여도 방문 비용은 남음 → 02-L05 실측: 비용은 "새로 표시하는 객체 수"와 "훑는 바이트 × 스캔 방식"의 합. 참조 필드 struct 배열(3a)이 객체 100만 개(1)보다 비쌌음. 다음 GC 관련 질문(02-L06·02-L09)에서 "이 배치에서 GC가 읽는 바이트는?"을 묻기
- 참조 타입 배열·컬렉션에 저장되는 것은 객체가 아니라 8바이트 포인터라는 점 (01-L09에서 "참조 타입은 크기가 달라서"라고 답함). 02-L05·02-L06에서 다시 묻기 → 02-L05에서도 힌트 필요(재발). 02-L06에서 다시 묻기
- 엔진 API 비용을 "매니지드 힙(GC) vs 네이티브 메모리·CPU"로 나눠 보는 것 — 02-L03에서 `Instantiate`가 GC 할당을 많이 할 거라고 봄. 1장 래퍼 구조와 연결해 "GC에 잡히는 건 래퍼뿐"을 02-L04·02-L08(Memory Profiler)에서 다시 묻기 → 02-L08에서 텍스처를 네이티브, 버퍼·셸을 매니지드로 바르게 나눔 (회복 중)
- 추적 GC를 "쓰레기를 표시·해제하는 것"으로 보는 사고 (참조 카운팅식) — 02-L06에서 "null 대입 → 다음 GC에서 마킹 해제", 02-L09에서 "마킹된 this·byte를 취소 처리"로 재발. 마킹 비용은 **산 것(살려야 하는 것)**에만 들고 쓰레기는 닿지도 않는다는 점을 3장 이후 GC 관련 질문에서 "이 수집에서 마킹되는 것은?"으로 다시 묻기
- 런타임에 타입 정보를 읽어 처리하는 방식(full generic sharing, rgctx)의 존재 — "값 타입 = 반드시 전용 코드"로 고정해서 생각함
- 빌드 도구(UnityLinker, IL2CPP)가 하는 분석의 수준을 "전부 아니면 전무"로 가정하는 것 — 01-L10에서 링커가 상수 문자열 리플렉션은 알아본다는 점, 01-L08에서 IL2CPP가 sealed를 쓰지 않는다는 점. 도구가 "무엇을 보고 무엇을 못 보는지"를 단계별로 묻기

## 미응답 질문 (다음 세션 첫 질문으로)
(없음)

## 보류한 작업
- 2026-10-31 `Docs/build-and-environment.md`에 `Labs/Build/IL2CPP (Release)` 메뉴(출력 `Builds/IL2CPP-Release`, 생성 코드 비교용, 프로파일러 카운터 없음)를 추가하기 — 학습자가 "나중에"로 보류. Docs 수정이라 할 때 다시 묻는다

## 실습 기록
(실습별 상세는 각 실습 README. 여기에는 날짜와 링크만)
- 2026-10-29 01-L01 에디터 + Mono 플레이어 실행 — [README](../Assets/Labs/01_NativeManaged/L01_FakeNull/README.md) (IL2CPP 미실행, 예외 타입 차이 원인은 01-L06에서 확인)
- 2026-10-29 01-L02 에디터 + Mono 플레이어 실행 — [README](../Assets/Labs/01_NativeManaged/L02_NewVsAddComponent/README.md)
- 2026-10-29 01-L06 소스 읽기 + 에디터·Mono 플레이어 CoreModule 비교 — [README](../Assets/Labs/01_NativeManaged/L06_ReadBindings/README.md)
- 2026-10-29 01-L03 에디터 + Mono 플레이어 실행 — [README](../Assets/Labs/01_NativeManaged/L03_CtorTiming/README.md)
- 2026-10-29 01-L04 에디터 + Mono 플레이어 실행 — [README](../Assets/Labs/01_NativeManaged/L04_CascadeDestroy/README.md)
- 2026-10-29 01-L05 에디터 + Mono 플레이어 측정 — [README](../Assets/Labs/01_NativeManaged/L05_BoundaryCost/README.md)
- 2026-10-30 01-L07 IL2CPP 플레이어 실행 + 생성 코드 읽기 — [README](../Assets/Labs/01_NativeManaged/L07_NullChecks/README.md)
- 2026-10-31 01-L08 IL2CPP 플레이어 실행 + 생성 코드·IL 읽기 — [README](../Assets/Labs/01_NativeManaged/L08_Sealed/README.md) (예상과 달리 devirtualization 없음 → 같은 날 가설 검증: IL 호출 대상이 final일 때만 직접 호출. 2016년 블로그 예제를 그대로 옮겨도 재현 안 됨. 범위: 6000.3 Development·Release 빌드, Windows x64)
- 2026-10-31 01-L09 IL2CPP 플레이어 실행 (Code Generation 두 설정) + 생성 코드 읽기 — [README](../Assets/Labs/01_NativeManaged/L09_GenericSharing/README.md) (같은 날 학습자 요청으로 Incremental GC On/Off 비교: string−int 차이 약 6 ns 중 약 90%가 쓰기 배리어. 배리어 포함 여부는 빌드 시점 define `IL2CPP_ENABLE_WRITE_BARRIERS`로 결정)
- 2026-10-31 01-L10 에디터 + IL2CPP 플레이어(Stripping High) 실행 + 생성 코드 확인 — [README](../Assets/Labs/01_NativeManaged/L10_Stripping/README.md) (1장 실습 모두 완료)
- 2026-10-31 02-L01 에디터(참고용) + Mono·IL2CPP 플레이어 측정 + 생성 코드 확인 — [README](../Assets/Labs/02_GC/L01_HiddenAlloc/README.md)
- 2026-10-31 02-L02 Mono·IL2CPP 플레이어 측정 + 생성 코드 확인 — [README](../Assets/Labs/02_GC/L02_ClosureTiming/README.md)
- 2026-10-31 02-L03 Mono·IL2CPP 플레이어 측정 — [README](../Assets/Labs/02_GC/L03_Pooling/README.md) (2026-11-01 C 분해 추가: 64 B = GameObject 래퍼 24 + 스크립트 인스턴스 40)
- 2026-11-01 02-L04 Mono·IL2CPP 플레이어 측정 — [README](../Assets/Labs/02_GC/L04_UsedVsReserved/README.md) (같은 날 학습자 요청으로 Collect 반복·단편화 추가: IL2CPP만 수집 6\~12회 뒤 Reserved 반납, Mono는 유지)
- 2026-11-01 02-L05 Mono·IL2CPP 플레이어 측정 (10만·100만 개) + libil2cpp 할당 경로 확인 — [README](../Assets/Labs/02_GC/L05_MarkCost/README.md)
- 2026-11-01 02-L06 Mono·IL2CPP 플레이어 측정 (Incremental On/Off 4빌드) + bdwgc `GC_maybe_gc` 확인 — [README](../Assets/Labs/02_GC/L06_IncrementalGC/README.md) (2026-11-02 학습자 요청으로 midlived·mutate 조건 추가: 이점이 조건을 탐)
- 2026-11-02 02-L07 Mono·IL2CPP 플레이어 측정 (Enabled 기준선 + Manual slice 3 ms·1 ms·0.1 ms, 각 2회) + bdwgc `GC_start_incremental_collection`·`GC_collect_a_little_inner` 확인 — [README](../Assets/Labs/02_GC/L07_ManualGC/README.md) (같은 날 학습자 해석을 실험으로: 호출 간격 N = 1·5·20·60, Mono·IL2CPP 각 2회)
- 2026-11-02 02-L08 Mono·IL2CPP 플레이어 실행 + 스냅샷 A/B/C 촬영(`Builds/Snapshots`) — [README](../Assets/Labs/02_GC/L08_SnapshotLeak/README.md) (판정은 코드로 함). 이후 Memory Profiler 1.2.0 설치, 학습자가 창에서 A→B·B→C 비교와 Leaked Managed Shell 참조 경로를 확인 — 코드 판정과 일치
- 2026-11-02 02-L09 Mono·IL2CPP 플레이어 측정 (프레임 제한 없음·60fps 각 1회, Mono는 drain·Wait 단계 추가 전 버전 1회 더) — [README](../Assets/Labs/02_GC/L09_Finalizer/README.md) (첫 Mono 결과가 예측과 달라 drain·Wait·`-frameRate` 추가. IL2CPP 첫 실행은 `WeakReference(o, true)` `NotSupportedException`으로 실패 → IL2CPP에서는 긴 약한 참조 생략 후 재빌드. 2장 실습 모두 완료)

## Player Settings 변경 이력
(변경 시 날짜, 항목, 원래 값 → 바꾼 값, 복원 여부)
- 2026-10-29 Run In Background: `false` → `true`, **복원 안 함 (학습자 요청으로 상시 유지)**. 에디터가 포커스를 잃으면 플레이 모드가 멈춰 실습이 매우 오래 걸리던 문제 해결용. 플레이어 빌드에도 적용되지만 측정 기준(Backend, Incremental GC, Stripping)과 무관하고, 플레이어는 원래 `-batchmode`로 실행함
- 2026-10-30 Scripting Backend: Mono → IL2CPP (01-L07 빌드 동안만), **복원됨** (`LabBuilder`가 빌드 후 Mono로 되돌림)
- 2026-10-30 Scripting Backend: Mono → IL2CPP (01-L07 (E) 추가 빌드 동안만), **복원됨** — 수정한 `LabBuilder`가 디스크에도 `Standalone: 0`으로 저장함을 확인
- 2026-10-31 Scripting Backend: Mono → IL2CPP (01-L08 빌드 동안만), **복원됨** (`Standalone: 0` 확인)
- 2026-10-31 Scripting Backend: Mono → IL2CPP (01-L08 가설 검증 빌드 동안만), **복원됨** (`Standalone: 0` 확인)
- 2026-10-31 Scripting Backend: Mono → IL2CPP (01-L08 블로그 조건 재현 빌드 동안만), **복원됨** (`Standalone: 0` 확인)
- 2026-10-31 Scripting Backend: Mono → IL2CPP (01-L08 Release 빌드 동안만), **복원됨** (`Standalone: 0` 확인)
- 2026-10-31 Scripting Backend: Mono → IL2CPP (01-L09 기본 설정 빌드 동안만), **복원됨** (`Standalone: 0` 확인)
- 2026-10-31 IL2CPP Code Generation: `OptimizeSpeed`(Faster runtime) → `OptimizeSize`(Faster (smaller) builds) (01-L09 비교 빌드 동안만), **복원됨** (`il2cppCodeGeneration: Standalone: 0`. 원래 `{}`였으나 값은 같은 OptimizeSpeed). 이 비교 빌드에서도 Backend는 `LabBuilder`가 복원함 (`Standalone: 0` 확인)
- 2026-10-31 Incremental GC: `On` → `Off` (01-L09 쓰기 배리어 비용 검증 빌드 동안만), **복원됨** (`gcIncremental: 1` 확인, 복원 후 On 빌드로 재측정)
- 2026-10-31 Managed Stripping Level: 기본값(`managedStrippingLevel: {}`, API로는 `Disabled`) → `High` (01-L10 빌드 동안만), **복원됨** (`Disabled`로 되돌림. 파일에는 `{}` 대신 `Standalone: 0`으로 남음, 값은 같음). Backend도 `Standalone: 0` 확인
- 2026-10-31 Scripting Backend: Mono → IL2CPP (02-L01 빌드 동안만), **복원됨** (`Standalone: 0` 확인)
- 2026-10-31 Scripting Backend: Mono → IL2CPP (02-L01 추가 케이스 L\~P 빌드 동안만), **복원됨** (`Standalone: 0` 확인)
- 2026-10-31 Scripting Backend: Mono → IL2CPP (02-L01 추가 케이스 Q\~V 빌드 3회, 1회는 생성 코드 폴더 잠금으로 실패 후 재빌드), **복원됨** (매번 `Standalone: 0` 확인)
- 2026-10-31 Scripting Backend: Mono → IL2CPP (02-L02 빌드 동안만), **복원됨** (`Standalone: 0` 확인)
- 2026-10-31 Scripting Backend: Mono → IL2CPP (02-L03 빌드 동안만), **복원됨** (`Standalone: 0` 확인)
- 2026-11-01 Scripting Backend: Mono → IL2CPP (02-L03 C 분해 빌드 동안만), **복원됨** (`Standalone: 0` 확인)
- 2026-11-01 Scripting Backend: Mono → IL2CPP (02-L04 빌드 동안만), **복원됨** (`Standalone: 0` 확인)
- 2026-11-01 Scripting Backend: Mono → IL2CPP (02-L04 unmap·단편화 추가 빌드 동안만), **복원됨** (`Standalone: 0` 확인)
- 2026-11-01 Scripting Backend: Mono → IL2CPP (02-L05 빌드 동안만), **복원됨** (`Standalone: 0` 확인)
- 2026-11-01 Scripting Backend: Mono → IL2CPP (02-L06 Incremental On 빌드 동안만), **복원됨** (`Standalone: 0` 확인)
- 2026-11-01 Incremental GC: `On` → `Off` (02-L06 Off 빌드 Mono·IL2CPP 동안만, IL2CPP 빌드의 Backend 전환도 복원됨), **복원됨** (`gcIncremental: 1` 확인)
- 2026-11-02 Scripting Backend: Mono → IL2CPP (02-L06 추가 측정 On·Off 빌드 2회 동안만), **복원됨** (`Standalone: 0` 확인)
- 2026-11-02 Incremental GC: `On` → `Off` (02-L06 추가 측정 Off 빌드 Mono·IL2CPP 동안만), **복원됨** (`gcIncremental: 1` 확인)
- 2026-11-02 Scripting Backend: Mono → IL2CPP (02-L07 빌드 동안만), **복원됨** (`Standalone: 0` 확인)
- 2026-11-02 Scripting Backend: Mono → IL2CPP (02-L07 호출 간격 빌드 동안만), **복원됨** (`Standalone: 0` 확인)
- 2026-11-02 Scripting Backend: Mono → IL2CPP (02-L08 빌드 동안만), **복원됨** (`Standalone: 0` 확인)
- 2026-11-02 Scripting Backend: Mono → IL2CPP (02-L09 빌드 2회 동안만 — 1회차 실행 실패 후 수정 빌드), **복원됨** (매번 `Standalone: 0` 확인)
