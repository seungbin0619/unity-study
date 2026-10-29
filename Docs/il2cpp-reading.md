# IL2CPP 생성 코드 읽기

IL2CPP 빌드에는 Windows Build Support (IL2CPP) 모듈이 필요하다. 설치 여부는 [build-and-environment.md](build-and-environment.md#로컬-환경-2026-10-29-확인)를 본다.

- IL2CPP 빌드 후 `Library/Bee` 아래에서 `il2cppOutput` 디렉터리를 찾는다. 경로는 버전·플랫폼에 따라 다르므로 하드코딩하지 말고 검색한다.
- 실습 대상 메서드 이름으로 검색해서 **관련 함수만 발췌**해 보여주고, 다음 패턴을 짚어 설명한다.
  - `NullCheck(...)`, `IL2CPP_ARRAY_BOUNDS_CHECK(...)`
  - `Il2CppCodeGenWriteBarrier(...)` (Incremental GC 쓰기 배리어)
  - `VirtualFuncInvoker` / `InterfaceFuncInvoker` 계열 vs 직접 호출 (sealed devirtualization)
  - 클래스 정적 초기화 검사, 박싱 호출
- `[Il2CppSetOption]` 실습 시, Unity 설치 폴더의 il2cpp 디렉터리에 있는 `Il2CppSetOptionAttribute.cs`를 프로젝트에 복사해야 한다. 설치 폴더는 [build-and-environment.md](build-and-environment.md)의 에디터 경로 기준으로 검색한다.
