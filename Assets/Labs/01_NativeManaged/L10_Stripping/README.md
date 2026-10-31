# 01-L10 코드 스트리핑과 `[Preserve]` / `link.xml`

## 목표
리플렉션으로만 쓰는 클래스가 Managed Stripping Level `High`에서 빌드에서만 사라지는 것을 확인한다.
리플렉션에 쓰는 문자열이 상수일 때와 런타임에 만들어질 때 UnityLinker의 판단이 달라지는지 보고, `[Preserve]`와 `link.xml`로 고친다.

## 예측
- 질문 1: Stripping `High` IL2CPP 빌드에서 (A) 상수 문자열로 찾는 클래스, (B) 런타임에 조합한 문자열로 찾는 클래스는 각각 어떻게 될까? (에디터에서는 둘 다 성공)
- 학습자 답 (실행 전, 원문 그대로): "둘 다 실패할거같은데, High로 설정하면 적극적으로 지워서 아무리 상수 문자열로 존재하더라도 타입을 찾지 못해서 null로 반환될 것 같음"

## 실습 구성
- 코드: `HiddenTargets.cs` — 어디서도 직접 생성·호출하지 않는 클래스 5개, 각자 `Ping()`만 가짐
- 코드: `StrippingLab.cs` — `Type.GetType` → `Activator.CreateInstance` → `GetMethod("Ping")` → `Invoke`를 단계별로 기록
  - (A) 상수 문자열, (B) 런타임 조합 문자열, (C) B + `[Preserve]`, (D) B + `link.xml`, (E) `typeof`로 타입만 정적 참조. 메서드 이름도 A만 상수, 나머지는 런타임 조합
- `link.xml` — `HiddenLinkXml`을 `preserve="all"`로 보존
- 실행: 에디터 메뉴 `Labs/01/L10 Stripping` 또는 플레이어 `-lab 01-L10`
- 빌드: Managed Stripping Level을 `High`로 바꾼 뒤 `Labs/Build/IL2CPP (Development)`, 빌드 후 원래 값으로 복원

## 결과

| 케이스 | 에디터 | IL2CPP, Stripping High | 생성 C++(`Labs.cpp`)에 남은 메서드 |
|---|---|---|---|
| A 상수 문자열 | 성공 (1) | 타입 found → `CreateInstance`에서 **`MissingMethodException`** | 없음 |
| B 런타임 조합 | 성공 (2) | 타입 **null** | 없음 |
| C B + `[Preserve]` | 성공 (3) | 타입 found, 생성 ok → `GetMethod("Ping")` **null** | `.ctor`만 |
| D B + `link.xml` | 성공 (4) | 성공 (4) | `.ctor`, `Ping` |
| E `typeof`만 | 성공 (5) | 타입 found → `CreateInstance`에서 **`MissingMethodException`** | 없음 |

- 실행일 2026-10-31, IL2CPP Development, Windows x64, 6000.3.25f1
- 원본: `Notes/results/01-L10_Editor_2026-10-31.csv`, `Notes/results/01-L10_IL2CPP_2026-10-31.csv`
- `GameAssembly.dll`: 31.2 MB (같은 날 기본 스트리핑 IL2CPP Development 빌드는 42.6 MB, 참고용)

## 해석
- 예측과 비교
  - B(런타임 조합 → 타입 null)는 맞음.
  - A는 틀림. 실제로는 타입이 남았다. UnityLinker는 `Type.GetType("상수")` 같은 리플렉션 패턴을 IL에서 알아보고, 이름이 상수이면 그 타입을 보존한다. B는 이름이 실행 중에 만들어져서 알아볼 수 없었다.
  - 다만 A에서 남은 것은 **타입뿐**이다. 생성자는 지워져서 `Activator.CreateInstance`가 `MissingMethodException`을 냈다. 리플렉션으로 무엇을 할지(생성, 메서드 호출)는 링커가 모르기 때문이다. E(`typeof`)도 같은 결과다.
- C에서 `[Preserve]`를 붙였는데 `Ping`이 지워진 이유 (학습자: "클래스의 각 메소드 또한 사용이 안되는 경우 삭제해도 된다고 판단해서" — 정답)
  - `[Preserve]`는 붙인 대상에만 적용된다. 타입에 붙이면 타입과 기본 생성자까지만 보존하고(실측: `.ctor`만 남음), 나머지 멤버는 일반 도달 가능성 분석을 따른다.
  - `Ping`까지 남기려면 메서드에 `[Preserve]`를 붙이거나 `link.xml`의 `preserve="all"`(D)을 쓴다.
  - `link.xml`은 소스를 고칠 수 없는 서드파티·.NET 어셈블리, 네임스페이스·어셈블리 단위 지정에도 쓸 수 있다.
- 한 단계 더
  - A는 null보다 찾기 어려운 실패다. "타입은 있는데 생성자가 없는" 상태라서, 문제가 타입 조회가 아니라 생성 단계에서 드러난다.
  - Unity는 `PreserveAttribute`를 이름으로 인식한다. 자체 어셈블리에 같은 이름의 attribute를 정의해도 동작한다 (Unity Manual 기준, 실측 아님). 그래서 UnityEngine을 참조하지 않는 라이브러리도 보존 지시를 넣을 수 있다.
  - 실무 사례: JSON 직렬화, DI 컨테이너처럼 멤버를 리플렉션으로 다루는 라이브러리. 에디터는 스트리핑을 하지 않으므로, 출시와 같은 Stripping Level의 플레이어 빌드로 테스트해야 한다.
  - 크기: `GameAssembly.dll` 42.6 → 31.2 MB. 크기 이득과 보존 지시를 관리하는 비용을 함께 따져야 한다.
- 범위: 6000.3.25f1, Windows x64, IL2CPP Development, Stripping High만 확인 (Low·Medium은 확인 안 함)
- 근거
  - 이 프로젝트의 실측 CSV와 il2cppOutput `Labs.cpp` (남은 메서드 검색)
  - Unity Manual "Managed code stripping" — `[Preserve]` 적용 범위와 `link.xml` 형식 (문서 근거)
  - 리플렉션 패턴 인식은 UnityLinker가 기반으로 하는 ILLink의 동작으로 보임 (구현 세부는 확인 필요, 결과는 실측)
