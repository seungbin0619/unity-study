# 01-L09 제네릭 인스턴스와 full generic sharing

## 목표
IL2CPP가 `List<int/float/Vector3/string/GameObject>.Add`에 대해 C++ 함수 본문을 몇 벌 만드는지 생성 코드에서 센다.
IL2CPP Code Generation을 `Faster runtime`(기본)에서 `Faster (smaller) builds`로 바꾸면 그 수와 호출 비용이 어떻게 달라지는지 비교한다.

## 예측
- (사전 개념 질문, 기본 설정) 값 타입 3 + 참조 타입 공유 1 = 4벌 — 이전 세션에서 정답
- 질문 1: `Faster (smaller) builds`(full generic sharing을 모든 인스턴스에 적용)로 바꾸면 5개 타입의 `Add` 본문은 몇 벌이 될까? 이유는?
- 학습자 답 (실행 전, 원문 그대로): "여전히 값 타입 3개, 참조 타입1개 아닌가"

## 실습 구성
- 코드: `GenericTargets.cs` — 타입 인자만 다른 `List<T>.Add` 루프를 `NoInlining` 메서드 5개(`FillInt` 등)에 하나씩 담음. il2cppOutput에서 이 메서드가 부르는 `Add`를 따라가 본문을 센다
- 코드: `GenericSharingLab.cs` — 용량을 미리 잡은 리스트에 `Clear` 후 100만 번 `Add`, 워밍업 3 + 표본 20, 표본마다 케이스 순서를 돌림
- 실행: 에디터 메뉴 `Labs/01/L09 Generic Sharing` 또는 플레이어 `-lab 01-L09`
- 빌드: `Labs/Build/IL2CPP (Development)` 두 번. 두 번째는 IL2CPP Code Generation을 `OptimizeSize`로 바꾼 뒤 빌드하고 원래 값(`OptimizeSpeed`)으로 복원

## 결과

### 생성 코드: `Add` 본문 수

호출 쪽(`Labs.cpp`)의 `List_1_Add_<해시>_inline` 래퍼는 두 설정 모두 타입마다 1개씩 5개다. 래퍼가 넘겨주는 실제 본문(`_gshared_inline`)이 달라진다.

| 타입 | Faster runtime (기본) | Faster (smaller) builds |
|---|---|---|
| `int` | `m0248…` (`int32_t`) | `mD4F3…` (`Il2CppFullySharedGenericAny`), `&item` 전달 |
| `float` | `m2F86…` (`float`) | `mD4F3…`, `&item` 전달 |
| `Vector3` | `m79E5…` (`Vector3_t`) | `mD4F3…`, `&item` 전달 |
| `string` | `mEBCF…` (`RuntimeObject*`) | `mD4F3…`, 포인터 값 그대로 전달 |
| `GameObject` | `mEBCF…` (`RuntimeObject*`) | `mD4F3…`, 포인터 값 그대로 전달 |
| **본문 수** | **4** | **1** |

### 빌드 크기

| 항목 | Faster runtime | Faster (smaller) builds |
|---|---|---|
| `GameAssembly.dll` | 42.6 MB | 24.1 MB |
| il2cppOutput `cpp/` 파일 수 | 454 | 272 |
| il2cppOutput `cpp/` 크기 | 598 MB | 275 MB |
| 빌드 시간 (LabBuilder 보고) | 225 s | 81 s (C++ 캐시 영향 가능, 참고용) |

### `Add` 1회 시간 (ns, 100만 회 × 20 표본)

| 실행일 | 백엔드 | Incremental GC | 빌드 | Code Generation | int | float | Vector3 | string | GameObject |
|---|---|---|---|---|---|---|---|---|---|
| 2026-10-31 | IL2CPP | On | Development | Faster runtime | 1.83 | 1.87 | 1.83 | 7.54 | 7.45 |
| 2026-10-31 | IL2CPP | On | Development | Faster (smaller) builds | 9.88 | 9.72 | 10.20 | 10.76 | 10.66 |

평균값. 최소는 모두 평균의 -7% 안이고, 최대는 일부 표본이 튐 (기본 설정 float 최대 3.18, int 최대 2.65).
원본: `Notes/results/01-L09_IL2CPP_2026-10-31.csv` (11:37 행 = Faster runtime, 11:39 행 = Faster (smaller) builds)

## 해석
- 예측과 비교
  - 기본 설정 4벌(값 타입 3 + 참조 공유 1)은 맞음.
  - `Faster (smaller) builds`에서도 4벌이라는 예측은 틀림. 실제로는 1벌.
  - 어긋난 지점: "값 타입은 크기가 달라서 따로 만들어야 한다"는 전제는 크기를 컴파일 시점에 정할 때만 성립한다. full generic sharing은 T의 크기·값 타입 여부를 런타임에 rgctx(runtime generic context) 메타데이터에서 읽는다(`il2cpp_codegen_sizeof`, `il2cpp_codegen_class_is_value_type`). 그래서 크기가 다른 타입도 한 본문을 쓸 수 있다.
- 공유의 대가
  - 공유 본문은 매 호출마다 다음을 한다: 크기 조회, `alloca` 2개, `memcpy` 2번, 값/참조 분기, `Il2CppCodeGenWriteBarrierForClass`(클래스를 보고 런타임에 판단).
  - 값 타입 `Add`가 1.8 → 10 ns(약 5.4배)로 느려졌다. 코드 크기는 `GameAssembly.dll` 기준 42.6 → 24.1 MB로 줄었다.
- 참조 타입이 기본 설정에서도 4배 느린 이유 (학습자: 1차 "참조 타입은 타입마다 크기가 달라서" → 힌트 후 정답)
  - 참조 배열에 저장되는 것은 포인터라서 크기는 항상 8바이트다. 크기 차이가 원인이라면 12바이트 `Vector3`가 가장 느려야 하는데, `int`와 같았다.
  - 차이는 참조 배열의 `SetAt`에만 붙는 쓰기 배리어(write barrier) `Il2CppCodeGenWriteBarrier`다. 이 프로젝트는 `gcIncremental: 1`이다.
  - Incremental GC가 켜져 있으면 배리어는 마킹 중이 아니어도 참조를 저장할 때마다 항상 실행된다. 값 타입 저장에는 GC가 추적할 포인터가 없어서 배리어가 없다.
  - **확인 필요**: 4배 차이가 전부 배리어 비용인지는 Incremental GC Off 빌드로 재 봐야 확정된다 (02-L06과 묶어서 검증 가능).
  - `Faster (smaller) builds`에서 참조 타입이 1.4배만 느려진 것도 이것으로 설명된다 (추정). 공유 본문에서는 값 타입도 `…ForClass` 배리어 판단을 거친다. 전용 본문에서 이미 배리어 비용을 내던 참조 타입은 상대적으로 덜 잃는다.
- 한 단계 더
  - full generic sharing은 `Faster runtime`에서도 빌드 시점에 알 수 없는 인스턴스에 쓰인다 (Unity 2022.1+). 예: `MakeGenericType`으로 만든 값 타입 인스턴스. 2021 이전에는 이런 경우 "no ahead of time (AOT) code was generated" `ExecutionEngineException`이 났다.
  - 즉 `OptimizeSpeed`는 "공유를 끈다"가 아니라 "빌드 시점에 아는 인스턴스는 전용 코드로 만든다"는 뜻이다. 이 실습에서 실측하지는 않았다.
  - 호출 쪽 `_inline` 래퍼는 두 설정 모두 타입마다 생기므로, 래퍼 수만 세면 차이가 보이지 않는다. 래퍼가 넘겨주는 `_gshared` 본문을 따라가야 한다.
- 범위: 6000.3.25f1, Windows x64, IL2CPP Development 빌드, Incremental GC On
- 근거
  - 이 프로젝트의 il2cppOutput (`Labs.cpp`, `GenericMethods.cpp`, `Generics__117.cpp`)
  - `il2cpp/libil2cpp/codegen/il2cpp-codegen.h` — `IL2CPP_ENABLE_WRITE_BARRIERS`일 때만 배리어 함수가 실제로 정의되고, 아니면 빈 inline 함수
  - "한 단계 더"의 2022.1 이전 동작은 Unity 블로그 "IL2CPP full generic sharing in Unity 2022.1 beta" 기준 (이번 세션에서 원문을 다시 읽지는 않음, 실측 아님)

## 코드 발췌

기본 설정의 배열 저장 (참조 배열에만 배리어가 붙음):
```cpp
// RuntimeObject[]의 SetAt
m_Items[index] = value;
Il2CppCodeGenWriteBarrier((void**)m_Items + index, (void*)value);
```

`Faster (smaller) builds`의 호출 쪽 래퍼 (int):
```cpp
inline void List_1_Add_m0248…_inline (List_1_t0591…* __this, int32_t ___0_item, const RuntimeMethod* method)
{
	((void (*) (List_1_tDBA8…*, Il2CppFullySharedGenericAny, const RuntimeMethod*))List_1_Add_mD4F3…_gshared_inline)
		((List_1_tDBA8…*)__this, (Il2CppFullySharedGenericAny)&___0_item, method);
}
```

공유 본문 (`List_1_Add_mD4F3…_gshared_inline`, 저장 부분):
```cpp
const uint32_t SizeOf_T = il2cpp_codegen_sizeof(il2cpp_rgctx_data_no_init(method->klass->rgctx_data, 9));
const Il2CppFullySharedGenericAny L_8 = alloca(SizeOf_T);
...
il2cpp_codegen_memcpy(L_8, (il2cpp_codegen_class_is_value_type(...) ? ___0_item : &___0_item), SizeOf_T);
il2cpp_codegen_memcpy((L_6)->GetAddressAt(L_7), L_8, SizeOf_T);
Il2CppCodeGenWriteBarrierForClass(il2cpp_rgctx_data(method->klass->rgctx_data, 9), (void**)(L_6)->GetAddressAt(L_7), (void*)L_8);
```
