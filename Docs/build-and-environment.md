# 빌드·측정 환경

## 로컬 환경 (2026-10-29 확인)

- OS: Windows 11
- Unity: 6000.3.25f1 (`ProjectSettings/ProjectVersion.txt`)
- 에디터 경로: `D:\Program Files\Unity\Hub\Editor\6000.3.25f1\Editor\Unity.exe` (Hub 설치 경로가 D: 드라이브)
- 빌드 타깃: Windows Standalone (x64)
- 플레이어 모듈: Windows **Mono만** 설치됨. IL2CPP 빌드를 하려면 학습자가 Unity Hub에서 "Windows Build Support (IL2CPP)" 모듈을 설치해야 한다.
- C++ 툴체인: Visual Studio Community 2022(`D:\Program Files\Microsoft Visual Studio\2022\Community`)에 "C++를 사용한 데스크톱 개발" 워크로드 설치됨, Windows SDK 10.0.22621.0 / 10.0.26100.0(`D:\Windows Kits\10`) 있음.

환경이 바뀌면(모듈 설치, 에디터 업그레이드) 이 절을 갱신하고 최상위 README의 "환경"도 맞춘다.

## 측정 환경 규칙

- **에디터 측정은 참고용**이다. 에디터는 가짜 null 할당, Mono JIT, 에디터 오버헤드 등 노이즈가 있다. 할당·시간에 대한 결론은 **Development Build 플레이어**에서 낸다.
- 다음 Player Settings를 바꾸기 전에는 학습자에게 알리고, 원래 값과 바꾼 값을 `Notes/progress.md`에 기록하며, 실습이 끝나면 복원한다.
  - Scripting Backend (Mono / IL2CPP)
  - Use Incremental GC
  - Managed Stripping Level
  - IL2CPP Code Generation (런타임 속도 / 코드 크기·빌드 시간)
- `LabBuilder`는 빌드 전에 위 설정을 백업하고 빌드 후 복원한다. Unity 6 API(`NamedBuildTarget`, `PlayerSettings.SetScriptingBackend` 등)를 사용한다.

## 배치모드 빌드·실행

```bash
UNITY_EDITOR="/d/Program Files/Unity/Hub/Editor/6000.3.25f1/Editor/Unity.exe"

"$UNITY_EDITOR" -batchmode -quit -projectPath . \
  -executeMethod Labs.Editor.LabBuilder.BuildIL2CPP \
  -logFile Notes/results/build.log

# 실행 예
Builds/IL2CPP/Labs.exe -lab 02-L05 -logFile Notes/results/player_02-L05.log
```

- **에디터가 같은 프로젝트를 열고 있으면 배치모드가 실패한다.** 이 경우 학습자에게 에디터를 닫아달라고 하거나, 에디터 메뉴 `Labs/Build ...`로 빌드하도록 안내한다.
- 빌드 실패 시 `build.log`에서 에러 부분만 발췌해 보여준다.

## 테스트 실행 (EditMode)

```bash
"$UNITY_EDITOR" -batchmode -projectPath . -runTests -testPlatform EditMode \
  -testResults Notes/results/tests.xml -logFile Notes/results/tests.log
```

- `-quit`는 붙이지 않는다. `-runTests`가 끝나면 스스로 종료한다.
- 단일 테스트: `-testFilter <전체 이름 또는 정규식>` 추가.
