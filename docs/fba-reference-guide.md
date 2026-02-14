# FBA 프로젝트/패키지 참조 전환 가이드

## 구조

- **FBA 파일 기본값**: `#:package Duxel.App@*-*` (외부 사용자가 바로 `dotnet run` 가능)
- **개발자 실행**: `./run-fba.ps1` (자동으로 `#:project`로 치환, 기본 NativeAOT 게시)

## 사용법

### 외부 사용자 (NuGet 패키지)

```powershell
dotnet run samples/fba/all_features.cs
```

FBA 파일에 `#:package Duxel.App@*-*`가 있으므로 자동으로 최신 NuGet 패키지를 가져옵니다.

### 개발자 (로컬 프로젝트 참조)

```powershell
./run-fba.ps1 samples/fba/all_features.cs                     # 기본: 프로젝트 참조 + NativeAOT 게시
./run-fba.ps1 samples/fba/all_features.cs -Managed            # Managed(dotnet run) 실행
./run-fba.ps1 samples/fba/all_features.cs -RuntimeIdentifier win-x64
./run-fba.ps1 samples/fba/all_features.cs -Launch             # 게시 후 실행 파일 자동 실행
./run-fba.ps1 samples/fba/all_features.cs -NoCache
```

### 신규 이미지 효과 샘플 실행

```powershell
# 기본 실행 (웹 PNG/JPG/GIF 자동 다운로드 + NativeAOT 게시)
./run-fba.ps1 samples/fba/image_widget_effects_fba.cs -NoCache

# 로컬 이미지 강제 지정(선택)
$env:DUXEL_IMAGE_PATH='C:\images\sample.png'
./run-fba.ps1 samples/fba/image_widget_effects_fba.cs -NoCache
Remove-Item Env:DUXEL_IMAGE_PATH
```

- 기본 모드는 `Web PNG / Web JPG / Web GIF`를 자동 준비합니다.
- `DUXEL_IMAGE_PATH`를 지정하면 `Custom` 옵션으로 로컬 파일을 우선 확인할 수 있습니다.
- GIF를 선택하면 프레임 지연값을 사용해 애니메이션이 재생됩니다.

스크립트가:
1. `#:package Duxel.App@*-*` → `#:project ../../src/Duxel.App/Duxel.App.csproj` 치환
2. 임시 파일로 기본 `dotnet publish -p:PublishAot=true` 실행
3. 필요 시 `-Managed`로 `dotnet run` 실행
4. 실행 후 임시 파일 자동 삭제
5. 원본 파일은 절대 변경하지 않음

## 기본 동작 프로필

- 기본값: `Display` 프로필
- 전환: `DUXEL_APP_PROFILE=render` 환경변수 지정

```powershell
$env:DUXEL_APP_PROFILE='render'
./run-fba.ps1 samples/fba/Duxel_perf_test_fba.cs -Managed
Remove-Item Env:DUXEL_APP_PROFILE
```

### NuGet 패키지 배포 시

CI가 `@*-*`를 구체적 버전(예: `@0.2.0-preview`)으로 치환하여 배포 가능.

## NuGet floating version

```csharp
#:package Duxel.App@*-*     // 최신 프리릴리즈 포함
#:package Duxel.App@0.*-*   // 0.x 대의 최신 프리릴리즈
#:package Duxel.App@*        // 최신 stable만
```
