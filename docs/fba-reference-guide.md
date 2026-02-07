# FBA 프로젝트/패키지 참조 전환 가이드

## 구조

- **FBA 파일 기본값**: `#:package Duxel.App@*-*` (외부 사용자가 바로 `dotnet run` 가능)
- **개발자 실행**: `./run-fba.ps1` (자동으로 `#:project`로 치환하여 로컬 소스 사용)

## 사용법

### 외부 사용자 (NuGet 패키지)

```powershell
dotnet run samples/fba/all_features.cs
```

FBA 파일에 `#:package Duxel.App@*-*`가 있으므로 자동으로 최신 NuGet 패키지를 가져옵니다.

### 개발자 (로컬 프로젝트 참조)

```powershell
./run-fba.ps1 samples/fba/all_features.cs           # 프로젝트 참조로 실행
./run-fba.ps1 samples/fba/all_features.cs -NoCache   # 캐시 없이 프로젝트 참조로 실행
```

스크립트가:
1. `#:package Duxel.App@*-*` → `#:project ../../src/Duxel.App/Duxel.App.csproj` 치환
2. 임시 파일로 `dotnet run` 실행
3. 실행 후 임시 파일 자동 삭제
4. 원본 파일은 절대 변경하지 않음

### NuGet 패키지 배포 시

CI가 `@*-*`를 구체적 버전(예: `@0.2.0-preview`)으로 치환하여 배포 가능.

## NuGet floating version

```csharp
#:package Duxel.App@*-*     // 최신 프리릴리즈 포함
#:package Duxel.App@0.*-*   // 0.x 대의 최신 프리릴리즈
#:package Duxel.App@*        // 최신 stable만
```
