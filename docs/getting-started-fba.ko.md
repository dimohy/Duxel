# Duxel FBA 빠른 시작 가이드

> 마지막 동기화: 2026-03-05

단일 `.cs` 파일로 Duxel 앱을 실행하는 FBA(File-Based App) 기준 가이드입니다.

## 필수 환경

| 항목 | 요구 사항 |
|---|---|
| .NET SDK | 10.0 이상 |
| OS | Windows 10/11 |
| GPU | Vulkan 1.0+ 지원 |

## 30초 실행

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

```powershell
dotnet run hello.cs
```

## 샘플 실행

레포 샘플은 `samples/fba/`에 있습니다.

```powershell
dotnet run samples/fba/all_features.cs
./run-fba.ps1 samples/fba/all_features.cs
./run-fba.ps1 samples/fba/all_features.cs -Managed
```

- `dotnet run ...`: NuGet 패키지 기준 실행
- `run-fba.ps1`: 로컬 프로젝트 참조로 치환해 실행(기본 NativeAOT)

### 자주 쓰는 샘플

| 파일 | 설명 |
|---|---|
| `all_features.cs` | 전체 위젯 종합 데모 |
| `dsl_showcase.cs` | DSL 레이아웃 데모 |
| `dsl_interaction.cs` | DSL 상태/이벤트 데모 |
| `windows_calculator_fba.cs` | 계산기 UI 데모 |
| `text_render_validation_fba.cs` | 텍스트 렌더 검증 |
| `font_style_validation_fba.cs` | 폰트 스타일/크기 렌더링 검증 |
| `Duxel_perf_test_fba.cs` | 대량 폴리곤 벤치 |

## 프로필/환경 변수

```powershell
$env:DUXEL_APP_PROFILE='render'
./run-fba.ps1 samples/fba/Duxel_perf_test_fba.cs -Managed
Remove-Item Env:DUXEL_APP_PROFILE
```

## DSL 방식

```csharp
#:property TargetFramework=net10.0
#:property platform=windows
#:package Duxel.$(platform).App@*-*

using Duxel.App;
using Duxel.Core.Dsl;
using Duxel.Windows.App;

var dslText = """
Window "My App"
  Text "Hello DSL"
  Checkbox Id="vsync" Text="VSync" Default=true
""";

var doc = UiDslParser.Parse(dslText);

DuxelWindowsApp.Run(new DuxelAppOptions
{
    Window = new DuxelWindowOptions { Title = "DSL Demo" },
    Dsl = new DuxelDslOptions
    {
        State = new UiDslState(),
        Render = emitter => doc.Emit(emitter)
    }
});
```

## 참고 문서

- [docs/fba-reference-guide.ko.md](docs/fba-reference-guide.ko.md)
- [docs/fba-run-samples.ko.md](docs/fba-run-samples.ko.md) · [English](docs/fba-run-samples.md)
- [docs/ui-dsl.ko.md](docs/ui-dsl.ko.md)
