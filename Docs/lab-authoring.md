# 실습 작성 규칙

## 코드

- **씬 수작업 의존을 최소화한다.** 실습은 `LabRunner`에 등록된 클래스로 만들고, 에디터 메뉴 또는 커맨드라인 인자로 실행되게 한다. 필요한 GameObject는 코드로 생성한다.
  - 모든 실습은 공용 `Main` 씬에서 돈다. 실습을 위해 `Main.unity`를 수정하지 않는다.
  - 실행할 실습 선택: 플레이어는 `-lab <ID>` 인자, 에디터 메뉴는 `SessionState`에 저장한 ID를 `LabRunner`가 읽는다.
  - 씬 로드 자체를 관찰해야 할 때만 실습 폴더에 전용 씬을 둔다 ([project-structure.md](project-structure.md#씬-운영)).
- 측정은 다음을 사용한다.
  - 할당량: `ProfilerRecorder`의 `"GC Allocated In Frame"` (Memory 카테고리)
  - 힙 크기: `"GC Used Memory"`, `"GC Reserved Memory"`
  - 구간 시간: `ProfilerMarker` + `Stopwatch`
  - 에디터에서 빠른 할당 여부 확인: Unity Test Framework(`com.unity.test-framework` 1.6.0)의 `Is.Not.AllocatingGCMemory()` 제약. 테스트는 `Assets/Labs/Tests`에 두고, 결과를 쓸 때는 에디터 기준임을 명시한다. 실행 명령은 [build-and-environment.md](build-and-environment.md).
- 측정 전 **워밍업 프레임**을 두고(JIT, 최초 초기화 할당 배제), 여러 번 반복해 평균·최대를 낸다.
- 결과는 `Notes/results/<실습ID>_<백엔드>_<날짜>.csv`로 저장하고, 콘솔에는 한 줄 요약만 출력한다. 플레이어 빌드에서는 `Application.persistentDataPath` 또는 `-logFile`로 지정한 경로에 쓰고 경로를 출력한다.
- 실습 코드 자체가 측정을 오염시키지 않게 한다 (측정 구간 안에서 문자열 포맷, LINQ, 로그 출력 금지).
- 의도적으로 할당·누수·크래시를 일으키는 코드에는 주석으로 `// LAB: 의도적` 표시를 한다.

## 실습 README

실습을 만들 때 README를 먼저 만들고(목표·예측 질문까지), 학습자가 예측을 답하면 그대로 기록한 뒤 실행한다. 실행이 끝나면 결과·해석을 채운다.

```markdown
# 02-L05 struct 배열과 GC 마킹 비용

## 목표
이 실습으로 확인하려는 것 한두 줄.

## 예측
- 질문:
- 학습자 답 (실행 전, 원문 그대로):

## 실습 구성
- 코드: `StructArrayLab.cs` — 무엇을 어떻게 비교하는지 한 줄
- 실행: 에디터 메뉴 `Labs/02/L05` 또는 플레이어 `-lab 02-L05`

## 결과
| 실행일 | 백엔드 | Incremental GC | 빌드 | 조건 | 측정값 |
|---|---|---|---|---|---|
| 2026-10-29 | IL2CPP | On | Development | 설계1 | ... |

원본: `Notes/results/02-L05_IL2CPP_2026-10-29.csv`

## 해석
- 예측과 비교: 맞은 점 / 어긋난 점과 그 이유
- 한 단계 더: 심화 포인트
- 근거: 참고한 문서·소스 링크

## 코드 발췌 (필요할 때만)
IL2CPP 생성 코드 등 해석에 필요한 부분만 짧게.
```

작성 규칙:
- 결과 표에는 CSV를 **요약한 값**(평균·최대 등)만 넣고, 원본 숫자 전체는 붙이지 않는다.
- 다시 실행하면 기존 행을 덮어쓰지 말고 **행을 추가**한다. 설정을 바꿔 비교하는 것 자체가 학습 내용이기 때문이다.
- 예측란은 결과를 본 뒤에 고치지 않는다.
- 해석을 채운 직후 최상위 README의 상태·핵심 결론을 갱신한다 ([project-structure.md](project-structure.md#최상위-readme)).
