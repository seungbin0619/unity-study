# 02-L08 Memory Profiler 스냅샷으로 누수 찾기

## 목표
GameObject를 `Destroy`한 뒤 무엇이 메모리에 남는지를 스냅샷 비교와 코드 판정(`WeakReference`, 객체 검색)으로 확인한다. 특히 정적 이벤트 구독 누수(Leaked Managed Shell)와 런타임에 만든 `Texture2D`가 각각 매니지드·네이티브 중 어디에 남는지 구분한다.

## 예측
- 질문: `LeakHolder`(256 KB `byte[]` 필드, Awake에서 `new Texture2D(512, 512, RGBA32)`, Awake에서 정적 이벤트 구독)를 붙인 GameObject를 Leaky 50개(구독 해제 안 함) / Clean 50개(`OnDestroy`에서 해제) 만들고 모두 `Destroy` → 몇 프레임 뒤 `GC.Collect()` → 스냅샷 B. 그룹마다 (1) GameObject(네이티브), (2) `LeakHolder` 객체(C#), (3) `_buffer`, (4) `_tex`가 남는가, 남으면 매니지드/네이티브 중 어디인가?
- 학습자 답 (실행 전, 원문 그대로):
  ```
  1. 둘다 x
  2. Leaky - o(매니지드), Clean - x
  3. _buffer - o (매니지드), Clean - x
  4. _tex - o, Clean - o (둘 다 네이티브)
  ```

## 실습 구성
- 코드: `LeakHolder.cs`(정적 이벤트 `LeakEvents.Tick`, 구독하는 컴포넌트), `SnapshotLeakLab.cs` — 스냅샷 A(생성 전) → 100개 생성 → `Destroy` + GC → 스냅샷 B → `Resources.UnloadUnusedAssets()` + GC → 스냅샷 C. B·C에서 그룹마다 남은 GameObject·`Texture2D`(이름으로 `Resources.FindObjectsOfTypeAll`), `LeakHolder`·`byte[]`·`Texture2D` 래퍼(`WeakReference`)를 세어 CSV에 기록
- 실행: 에디터 메뉴 `Labs/02/L08 Snapshot Leak` 또는 플레이어 `-lab 02-L08 -snapshotDir <폴더>` (스냅샷 `.snap`은 크기가 커서 git 제외 폴더 `Builds/Snapshots`에 둔다)
- 스냅샷 비교(Memory Profiler 창)는 학습자가 GUI로 한다. 창을 쓰려면 `com.unity.memoryprofiler` 패키지가 필요하다 (1.2.0 설치됨)

## 결과
그룹당 50개, Development 빌드, Incremental GC On. 두 백엔드의 판정 결과가 같았다 (수치는 Mono / IL2CPP)

| 실행일 | 백엔드 | 단계 | 그룹 | (1) GameObject | (2) `LeakHolder` 래퍼 (살아 있음 / `== null`) | (3) `byte[]` | (4) `Texture2D` 네이티브 | (4b) `Texture2D` 래퍼 |
|---|---|---|---|---|---|---|---|---|
| 2026-11-02 | Mono·IL2CPP | B (Destroy + GC) | Leaky | 0 | 50 / 50 | 50 | 50 (50.0 MB) | 50 |
| 2026-11-02 | Mono·IL2CPP | B | Clean | 0 | 0 | 0 | **50 (50.0 MB)** | **50** |
| 2026-11-02 | Mono·IL2CPP | C (+ `UnloadUnusedAssets` + GC) | Leaky | 0 | 50 / 50 | 50 | **50 (50.0 MB)** | 50 |
| 2026-11-02 | Mono·IL2CPP | C | Clean | 0 | 0 | 0 | **0** | 0 |

| 단계 | GC Used (MB) | 네이티브 할당 `GetTotalAllocatedMemoryLong` (MB) | 이벤트 구독자 |
|---|---|---|---|
| A (생성 전) | 0.96 / 1.18 | 42.5 / 65.5 | 0 |
| 생성 직후 | 26.4 / 26.7 | 145.8 / 172.7 | 100 |
| B | **13.7 / 14.0** | **145.7 / 172.6** | 50 |
| C | 13.7 / 14.0 | **93.9 / 122.4** | 50 |

- B의 GC Used 증가분 약 12.8 MB = Leaky `byte[]` 50 × 256 KB(12.5 MB) + 래퍼들. 네이티브는 B에서 거의 줄지 않았다 — `Texture2D` 100개(100 MB)가 그대로 남았다.
- 스냅샷: `Builds/Snapshots/02-L08_<백엔드>_{A,B,C}.snap` (Mono 21 / 58 / 58 MB, git 제외)
- Memory Profiler 1.2.0 창 확인 (2026-11-02, 학습자, Mono 스냅샷): 코드 판정과 같은 결과
  - A → B 비교: Summary에서 Managed 약 13 MB·Graphics 약 100 MB 증가, Unity Objects에 `L08_Leaky_NN_tex`·`L08_Clean_NN_tex` 각 50개
  - B 단독: `LeakHolder`가 Leaked Managed Shell로 표시. References 경로 `LeakEvents.Tick` → `Action` → `LeakHolder`
  - B → C 비교: Clean 텍스처만 사라짐

원본: `Notes/results/02-L08_Mono_2026-11-02.csv`, `02-L08_IL2CPP_2026-11-02.csv`

## 해석
- 예측과 비교: **4문항 모두 맞음.**
  - (1) GameObject는 두 그룹 모두 네이티브에서 사라졌다.
  - (2)·(3) Leaky만 `LeakHolder`와 `byte[]`가 매니지드에 남았다. 남은 `LeakHolder` 50개는 전부 `== null`이 `true`인 파괴된 래퍼다 — Memory Profiler가 "Leaked Managed Shell"로 분류하는 객체. 붙잡는 경로: 정적 필드 `LeakEvents.Tick` → 델리게이트(멀티캐스트 목록) → target `LeakHolder` → `_buffer`, `_texture`.
  - (4) `new Texture2D`는 두 그룹 모두 네이티브에 남았다. GameObject를 파괴해도 그 컴포넌트가 만든 에셋은 파괴되지 않는다.
- 한 단계 더:
  - **Clean 그룹의 누수가 더 위험하다.** 구독을 제대로 해제한 Clean도 텍스처 50 MB를 남겼다. 이 누수는 네이티브에만 있어서 GC Used(13.7 MB)만 보면 보이지 않는다. 런타임에 만든 `Texture2D`·`Mesh`·`Material`·`RenderTexture`는 만든 쪽이 `OnDestroy`에서 `Destroy`해야 한다. 그렇지 않으면 다음 씬 전환(내부적으로 `UnloadUnusedAssets`)이나 직접 호출할 때까지 남는다.
  - **`UnloadUnusedAssets`는 매니지드 참조를 루트로 본다.** C에서 Clean 텍스처는 회수됐지만(네이티브 −51.8 / −50.2 MB) Leaky 텍스처는 남았다. 파괴된 래퍼(Leaked Managed Shell)의 `_texture` 필드가 여전히 매니지드 참조이기 때문이다. 그래서 이벤트 구독 누수는 매니지드 몇 바이트로 끝나지 않고, **그 셸이 가리키는 네이티브 에셋까지 언로드를 막는다.**
  - **Clean 그룹의 `Texture2D` 래퍼도 B까지 살아 있었다.** `LeakHolder`가 수거되어 매니지드 쪽에서 래퍼를 가리키는 곳이 없는데도, 네이티브 텍스처가 언로드된 C에서야 사라졌다. 살아 있는 네이티브 객체가 자기 래퍼를 강한 GC 핸들(GCHandle)로 붙잡는 것으로 보인다 (Unity 네이티브 구현이라 확인 필요). 1장의 "GC에 잡히는 것은 래퍼뿐"과 짝을 이루는 사실: 래퍼의 수명은 네이티브 객체의 수명에 묶여 있다.
- 근거: 이 프로젝트 실측 (Mono·IL2CPP Development 플레이어). Unity 문서 `Resources.UnloadUnusedAssets`, `Unity.Profiling.Memory.MemoryProfiler.TakeSnapshot`
