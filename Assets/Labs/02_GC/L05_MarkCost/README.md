# 02-L05 데이터 배치에 따른 GC 마킹 비용

## 목표
같은 원소 10만 개를 `List<class>`, `struct[]`, 참조 필드가 있는 `struct[]`로 살려 둘 때 `GC.Collect()` 한 번의 시간이 어떻게 달라지는지 Development 플레이어에서 확인한다.

## 예측
- 질문: 0 기준선, 1 `List<EnemyClass>`, 2 `EnemyStruct[]`, 3a `EnemyStructRef[]`(모든 `Name`이 같은 문자열), 3b `EnemyStructRef[]`(원소마다 다른 문자열)을 `GC.Collect()` 시간이 짧은 순서로. (GC Used 증가량 계산은 학습자가 생략)
- 학습자 답 (실행 전, 원문 그대로):
  ```
  0, 2, 3a, 3b = 1인거같음
  ```
  (2026-10-29 개념 단계 답: "2<1<3, 1≈2")

## 실습 구성
- 코드: `DataLayouts.cs` — 세 원소 타입. `MarkCostLab.cs` — 경우마다 데이터를 만들고 워밍업 수집 3회 뒤 `GC.Collect()` 31회를 `Stopwatch`로 재서 최소·중앙값·최대와 GC Used 증가량을 기록. 순서 효과 확인용으로 마지막에 기준선을 다시 잰다
- 실행: 에디터 메뉴 `Labs/02/L05 Mark Cost` 또는 플레이어 `-lab 02-L05` (원소 수 변경: `-count N`)

## 결과
`GC.Collect()` 31회 중앙값(ms) / GC Used 증가량(MB). Incremental GC On, Development 플레이어

| 실행일 | 백엔드 | Incremental GC | 빌드 | 조건 | 0 기준선 | 1 `List<class>` | 2 `struct[]` | 3a 공유 문자열 | 3b 원소마다 문자열 | 0' 기준선 다시 |
|---|---|---|---|---|---|---|---|---|---|---|
| 2026-11-01 | Mono | On | Development | 10만 개 | 0.270 | 0.483 / 3.82 | 0.278 / 1.53 | 0.580 / 2.29 | 0.631 / 6.89 | 0.287 |
| 2026-11-01 | Mono | On | Development | 100만 개 (`-count 1000000`) | 0.291 | 2.391 / 38.15 | 0.312 / 15.26 | **4.251** / 22.89 | 4.956 / 75.48 | 0.325 |
| 2026-11-01 | IL2CPP | On | Development | 10만 개 | 0.318 | 0.555 / 5.36 | 0.311 / 1.53 | 0.616 / 2.29 | 0.652 / 8.70 | 0.302 |
| 2026-11-01 | IL2CPP | On | Development | 100만 개 | 0.306 | 2.470 / 53.59 | 0.331 / 15.26 | **4.100** / 22.89 | 4.915 / 84.23 | 0.552 |

원본: `Notes/results/02-L05_Mono_2026-11-01.csv`, `02-L05_IL2CPP_2026-11-01.csv` (각각 10만 개 실행 뒤에 100만 개 실행을 이어 붙임. `count` 행으로 구분)

## 해석
- 예측과 비교: 실측 순서는 **0 ≈ 2 < 1 < 3a < 3b** (두 백엔드, 두 크기 모두 같음).
  - 맞음: 참조 필드가 없는 `struct[]`(2)는 기준선과 구별되지 않는다. 100만 개(15 MB)여도 +0.02 ms.
  - 맞음: 3b는 1과 비슷한 쪽이 아니라 가장 비싸다. 다만 예측(3b = 1)과 달리 **3a조차 1보다 비싸다**(100만 개에서 4.1\~4.3 ms vs 2.4\~2.5 ms).
  - 어긋난 점: "참조를 따라가서 방문하는 객체 수"만 세면 3a(문자열 1개)는 1(객체 100만 개)보다 싸야 한다. 실제로는 **스캔해야 하는 배열의 크기와 스캔 방식**이 더 컸다. 개념 단계 정답(`2 < 3 ≤ 1`, "공유 문자열이면 3이 훨씬 싸짐")도 같은 이유로 틀렸다 — 측정이 맞다.
- 왜 2는 공짜인가 (libil2cpp `vm/Array.cpp` `NewSpecific`): 원소에 참조가 없으면(`!klass->has_references`) `AllocatePtrFree` → `GC_MALLOC_ATOMIC`. Boehm은 atomic 블록에 마크 비트만 세우고 내용은 읽지 않는다. 15 MB든 1.5 MB든 비용이 같다.
- 왜 3a가 1보다 비싼가 (IL2CPP):
  - 3의 배열은 원소 struct에 비트맵 디스크립터가 있어 `GC_gcj_vector_malloc`으로 할당되고, 마킹 때 `BoehmGC.cpp`의 벡터 마크 프로시저가 원소마다(3워드 = 24 B) 비트맵을 보며 `Name` 칸을 골라 읽는다. 100만 개면 **24 MB를 원소 단위 루프로** 훑는다.
  - 1의 `EnemyClass[]`(List 내부 배열)은 `MakeDescriptorForArray`가 `GC_NO_DESCRIPTOR`라 일반 할당 → 8 B 포인터만 촘촘히 든 **8 MB를 Boehm 기본 루프로** 훑는다. 가리키는 `EnemyClass`도 참조 필드가 없어 `NewPtrFree`(atomic)이므로 마크 비트만 세우고 내용은 읽지 않는다.
  - 즉 마킹 비용 ≈ "훑는 바이트 × 스캔 방식의 단가" + "새로 표시하는 객체 수". 3a는 앞항이 크고, 1은 뒤항이 크다. 3b는 둘 다 크다(3a 대비 +0.8 ms = 문자열 100만 개 표시).
  - Mono도 같은 순서지만 Mono Boehm이 struct 배열을 정밀(precise) 스캔하는지 보수적(conservative)으로 전부 훑는지는 확인 필요.
- 한 단계 더:
  - **참조 필드 하나가 배열 전체를 스캔 대상으로 만든다.** 데이터 지향 설계에서 "핫 데이터 struct에는 참조를 넣지 않는다"(이름·에셋은 인덱스/ID로, 별도 배열에)가 GC 측면에서도 이득인 이유. Burst/NativeArray가 요구하는 blittable 제약과 같은 방향이다.
  - **pointer-free 객체는 많아도 내용을 읽지 않는다.** 1에서 비싼 것은 객체 100만 개의 마크 비트 + 포인터 배열 스캔이지 객체 필드가 아니다. 원소 클래스에 참조 필드를 하나 넣으면 객체마다 내용 스캔이 추가된다(미측정).
  - **GC Used 크기 차이**: 1의 객체는 Mono 32 B, IL2CPP 48 B로 보인다(100만 개 기준 (38.15−7.63)/1M, (53.59−7.63)/1M). 문자열도 IL2CPP가 더 크다. IL2CPP Boehm의 `all_interior_pointers`가 할당마다 1바이트를 더해 크기 등급이 올라가는 것으로 추정 (확인 필요).
  - 0' 기준선은 10만 개에서 0과 같았다. IL2CPP 100만 개에서 0.55 ms로 늘어난 것은 커진 힙(Reserved)의 블록 수가 늘어난 영향으로 보인다 (확인 필요).
- 근거: 이 프로젝트 실측. `Editor/Data/il2cpp/libil2cpp/vm/Array.cpp`(`NewSpecific`), `vm/Object.cpp`(`NewAllocSpecific` → `NewPtrFree`), `gc/BoehmGC.cpp`(벡터 마크 프로시저, `MakeDescriptorForArray`)

## 코드 발췌
```cpp
// libil2cpp/vm/Array.cpp — NewSpecific
if (!klass->has_references)
    o = Object::AllocatePtrFree(byte_len, klass);              // 2: atomic, 내용 안 읽음
else if (klass->element_class->byval_arg.valuetype &&
         ((GC_descr)klass->element_class->gc_desc & GC_DS_TAGS) == GC_DS_BITMAP)
    o = (Il2CppObject*)GC_gcj_vector_malloc(byte_len, klass);  // 3: 원소 비트맵으로 훑음
...
else
    o = Object::Allocate(byte_len, klass);                     // 1: 참조 배열, 기본 스캔
```
