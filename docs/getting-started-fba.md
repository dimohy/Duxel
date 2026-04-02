# Duxel FBA Quick Start Guide

> Last synced: 2026-03-25  
> Korean original: [getting-started-fba.ko.md](getting-started-fba.ko.md)

This guide covers the FBA (File-Based App) flow to run Duxel with a single `.cs` file.

## Requirements

| Item | Requirement |
|---|---|
| .NET SDK | 10.0 or newer |
| OS | Windows 10/11 |
| GPU | Vulkan 1.0+ support |

## 30-Second Run

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

## Running Samples

Repo samples are in `samples/fba/`.

```powershell
dotnet run samples/fba/all_features.cs
./run-fba.ps1 samples/fba/all_features.cs
./run-fba.ps1 samples/fba/all_features.cs -Managed
```

- `dotnet run ...`: NuGet package execution path.
- `run-fba.ps1`: rewrites to local project references (NativeAOT by default).

### Frequently Used Samples

| File | Description |
|---|---|
| `all_features.cs` | Full widget showcase demo with dedicated typography, layout, popup/context, input-query, item-status, multi-select, and layer/animation windows |
| `hello_duxel_fba.cs` | Ultra-minimal hello sample with a small `Hello` and a large `Duxel!` |
| `windows_calculator_fba.cs` | Calculator UI demo |
| `text_render_validation_fba.cs` | Text rendering validation |
| `font_style_validation_fba.cs` | Font style/size rendering validation |
| `Duxel_perf_test_fba.cs` | High-polygon benchmark |

## Profile / Environment Variable

```powershell
$env:DUXEL_APP_PROFILE='render'
./run-fba.ps1 samples/fba/Duxel_perf_test_fba.cs -Managed
Remove-Item Env:DUXEL_APP_PROFILE
```

## DSL Style

For DSL-based UI, use the `UiDslScreen` class with `.ui` and `.duxel-theme` files in a project. See `samples/Duxel.ThemeDemo` for a complete example.

## Related Docs

- [docs/fba-reference-guide.md](fba-reference-guide.md)
- [docs/fba-run-samples.md](fba-run-samples.md) · [한국어](fba-run-samples.ko.md)
- [docs/ui-dsl.md](ui-dsl.md)
