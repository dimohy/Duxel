# Duxel FBA Quick Start Guide

> Last synced: 2026-07-22
> Korean original: [getting-started-fba.ko.md](getting-started-fba.ko.md)

This guide covers the .NET 10 FBA (File-Based App) flow to run Duxel with a single `.cs` file. Duxel `0.2.11-preview` packages support regular `net8.0`, `net9.0`, and `net10.0` project consumers.

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
| `declarative_dashboard_fba.cs` | Declarative C# dashboard using `UiState<T>`, reusable `UiComponent` classes, compiled Windows 11 design tokens, and product-shell helpers |
| `hello_duxel_fba.cs` | Ultra-minimal hello sample with a small `Hello` and a large `Duxel!` |
| `extended_title_bar_fba.cs` | Windows 11-style extended title bar using the effective window icon, tab-shaped application controls, caption buttons visually identical to Duxel chrome in restored/maximized states, native Windows behavior, public drag/bounds APIs, and test-only Win32/DWM diagnostics |
| `windows_calculator_fba.cs` | Calculator UI demo using library-owned Duxel chrome, including state-aware Maximize/Restore glyphs |
| `text_render_validation_fba.cs` | Text rendering validation |
| `font_style_validation_fba.cs` | Font style/size rendering validation |
| `scrolling_static_layer_bench_fba.cs` | Scrolling/clip static layer benchmark for visual regression and cache invalidation validation |
| `Duxel_perf_test_fba.cs` | High-polygon benchmark |
| `pipeline_ordering_bench_fba.cs` | Dynamic solid/text pipeline ordering benchmark |
| `dynamic_widget_ordering_bench_fba.cs` | Widget-like dynamic ordering and row clip churn benchmark |
| `static_cache_rebuild_bench_fba.cs` | Static cache rebuild, reuse, and allocation pressure benchmark |
| `static_layer_moving_order_bench_fba.cs` | Moving static-layer replay ordering benchmark |
| `texture_upload_barrier_bench_fba.cs` | Texture upload copy/barrier policy benchmark |
| `directtext_page_upload_bench_fba.cs` | DirectText page-style partial texture upload benchmark |
| `directtext_dynamic_text_bench_fba.cs` | DirectText stable-cache versus changing-string frame-tail, text-work, and allocation benchmark |
| `vector_primitives_bench_fba.cs` | Primitive-heavy vector workload benchmark |
| `analytic_rounded_primitives_bench_fba.cs` | 1x-MSAA visual and performance gate for analytic rounded fills, outlines, combined fill/border panels, and circles |

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
