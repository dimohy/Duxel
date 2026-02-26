# FBA 프로젝트/패키지 참조 전환 가이드

> 마지막 동기화: 2026-02-26

## 기본 원칙

- FBA 샘플은 현재 `#:package Duxel.$(platform).App@*-*` 패턴을 사용합니다.
- 일반 사용자: `dotnet run <file>.cs`로 NuGet 패키지 실행
- 개발자: `./run-fba.ps1`로 로컬 프로젝트 참조 실행

## 가장 많이 쓰는 실행

```powershell
# NuGet 패키지 방식
dotnet run samples/fba/all_features.cs

# 로컬 프로젝트 참조 (기본 NativeAOT)
./run-fba.ps1 samples/fba/all_features.cs

# 로컬 프로젝트 참조 (Managed)
./run-fba.ps1 samples/fba/all_features.cs -Managed
```

## `run-fba.ps1` 동작

스크립트는 원본 FBA 파일을 수정하지 않고 임시 파일을 만들어 실행합니다.

1. `#:package`를 감지해 `#:project`로 치환
	- `Duxel.App` → `src/Duxel.App`
	- `Duxel.Windows.App` 또는 `Duxel.$(platform).App`(windows) → `src/Duxel.Windows.App`
2. Windows 경로에서는 `DuxelWindowsApp.Run(...)` 호출 형태로 맞춤 변환
3. 기본은 `dotnet publish -p:PublishAot=true`
4. `-Managed` 사용 시 `dotnet run`
5. 실행 후 임시 파일 삭제

## 주요 옵션

| 옵션 | 설명 |
|---|---|
| `-Managed` | NativeAOT 대신 managed 실행 |
| `-RuntimeIdentifier win-x64` | NativeAOT RID 지정 |
| `-NoCache` | `dotnet` 캐시 비활성화 인수 전달 |
| `-NoBuild` | 빌드 생략 인수 전달 |
| `-Platform windows` 또는 `--platform windows` | 템플릿 패키지 플랫폼 값 지정 |

## 프로필 전환

```powershell
$env:DUXEL_APP_PROFILE='render'
./run-fba.ps1 samples/fba/Duxel_perf_test_fba.cs -Managed
Remove-Item Env:DUXEL_APP_PROFILE
```

## 버전 표기 예시

```csharp
#:package Duxel.$(platform).App@*-*   // 최신 프리릴리즈 포함
#:package Duxel.$(platform).App@0.*-* // 0.x 최신 프리릴리즈
#:package Duxel.$(platform).App@*     // 최신 stable만
```
