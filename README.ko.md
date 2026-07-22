# Duxel

<p align="center">
  <img src="logo.svg" alt="Duxel logo" width="615" />
</p>

.NET 8, .NET 9 및 .NET 10 기반 즉시 모드 GUI 프레임워크로, Vulkan 렌더러와 Windows 네이티브 플랫폼 백엔드를 사용합니다.

**현재 공개 패키지 버전:** `0.2.11-preview`

**지원 패키지 타깃:** `net8.0`, `net9.0`, `net10.0`. 파일 기반 앱(FBA) 워크플로에는 계속 .NET 10 SDK가 필요합니다.

[![NuGet](https://img.shields.io/nuget/vpre/Duxel.App)](https://www.nuget.org/packages/Duxel.App)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

- English: [README.md](README.md)
- 버전 이력: [docs/version-history.ko.md](docs/version-history.ko.md) · [English](docs/version-history.md)
- Duxel 에이전트 참조 문서: [docs/duxel-agent-reference.ko.md](docs/duxel-agent-reference.ko.md) · [English](docs/duxel-agent-reference.md)
- 확장 타이틀바 가이드: [docs/extended-title-bar-guide.ko.md](docs/extended-title-bar-guide.ko.md) · [English](docs/extended-title-bar-guide.md)

## 제공 기능

- `UiScreen.Render(...)` 기반 즉시 모드 위젯 API (`UiImmediateContext`).
- 프로필 기반 렌더 설정(`Display` / `Render`)과 MSAA 설정.
- Windows 네이티브 입력/IME/클립보드 지원.
- 애플리케이션 탭, 네이티브 캡션 동작, Snap Layout, 시스템 메뉴, DPI, 다중 모니터를 지원하는 Windows 11 스타일 확장 타이틀바.
- NativeAOT 친화 런타임 구성.
- UI DSL(`.ui`) 파서/런타임 및 소스 생성기 경로.

## 패키지

| 패키지 | 용도 |
|---|---|
| `Duxel.App` | 앱 파사드 및 공용 런타임 파이프라인 |
| `Duxel.Windows.App` | Windows 플랫폼 러너 (`DuxelWindowsApp.Run`) |

일반 .NET 8 Windows 애플리케이션은 `net8.0-windows7.0` 또는 그 이후 Windows TFM을 사용하세요. Duxel 패키지 어셈블리는 이식 가능한 `net8.0` 자산으로 제공하고, 소비자 프로젝트의 Windows TFM이 플랫폼 요구사항을 분석기와 빌드에 명확히 전달합니다. 파일 기반 앱에는 .NET 10 SDK가 필요하므로 FBA 샘플은 계속 `net10.0`을 사용합니다.

## 빠른 시작 (FBA, Windows)

`hello.cs`:

```csharp
#:property TargetFramework=net10.0
#:property platform=windows
#:package Duxel.$(platform).App@*-*

using Duxel.App;
using Duxel.Core;
using Duxel.Windows.App;

DuxelWindowsApp.Run(new DuxelAppOptions
{
    Window = new DuxelWindowOptions { Title = "Hello Duxel" },
    Screen = new HelloScreen()
});

public sealed class HelloScreen : UiScreen
{
    public override void Render(UiImmediateContext ui)
    {
        ui.BeginWindow("Hello");
        ui.Text("Hello, Duxel!");
        ui.EndWindow();
    }
}
```

실행:

```powershell
dotnet run hello.cs
```

## 대표 FBA 쇼케이스

헬로 월드보다 한 단계 더 나아간 Duxel 화면을 빠르게 보고 싶다면, 아래 대표 FBA 샘플부터 실행하면 됩니다.

- `samples/fba/all_features.cs` — 타이포그래피, 레이아웃, 팝업/컨텍스트, 입력 진단, 아이템 상태, 멀티셀렉트, 레이어, 드로잉, 이미지, 마크다운 쇼케이스 창까지 포함한 전체 위젯 갤러리이며, 첫 실행 시 빈 화면 대신 `Markdown Studio`를 바로 엽니다.
- `samples/fba/ui_mixed_stress.cs` — 컨트롤, 폼, 긴 리스트, 데이터 테이블, 텍스트 렌더링, 조밀한 캔버스 영역을 한 화면에 배치한 균형형 멀티윈도우 쇼케이스입니다.
- `samples/fba/Duxel_perf_test_fba.cs` — VSync, MSAA, 캐시 토글, 폴리곤 설정, 렌더러 프로파일을 즉시 조절해 볼 수 있는 폴리곤 스트레스 테스트 샘플입니다.

## 샘플

- 프로젝트 샘플: `samples/Duxel.Sample`
  - `dotnet run --project samples/Duxel.Sample/`
- FBA 샘플: `samples/fba/*.cs`
  - `dotnet run samples/fba/all_features.cs`
  - `./run-fba.ps1 samples/fba/all_features.cs` (로컬 프로젝트 참조, 기본 NativeAOT)
  - `all_features.cs`는 기본으로 `Markdown Studio`를 열고, 전용 타이포그래피, 레이아웃, 팝업/컨텍스트, 입력 질의, 아이템 상태, 멀티셀렉트, 레이어/애니메이션 쇼케이스 창을 함께 제공합니다.

## DSL

`.ui` 선언형 문법과 상태 바인딩을 지원합니다.

- DSL 문서: [docs/ui-dsl.ko.md](docs/ui-dsl.ko.md) · [English](docs/ui-dsl.md)
- 에이전트 참조 문서: [docs/duxel-agent-reference.ko.md](docs/duxel-agent-reference.ko.md) · [English](docs/duxel-agent-reference.md)
- 확장 타이틀바: [docs/extended-title-bar-guide.ko.md](docs/extended-title-bar-guide.ko.md) · [English](docs/extended-title-bar-guide.md)
- FBA 시작 가이드: [docs/getting-started-fba.ko.md](docs/getting-started-fba.ko.md) · [English](docs/getting-started-fba.md)
- FBA 참조 가이드: [docs/fba-reference-guide.ko.md](docs/fba-reference-guide.ko.md) · [English](docs/fba-reference-guide.md)
- FBA 샘플 카탈로그: [docs/fba-run-samples.ko.md](docs/fba-run-samples.ko.md) · [English](docs/fba-run-samples.md)
- 커스텀 위젯 문서: [docs/custom-widgets.ko.md](docs/custom-widgets.ko.md) · [English](docs/custom-widgets.md)
- 설계 문서: [docs/design.ko.md](docs/design.ko.md)
- 최적화 정책: [docs/optimization-policy.ko.md](docs/optimization-policy.ko.md)

## 빌드

```powershell
dotnet build Duxel.slnx -c Release
```

## 라이선스

MIT
