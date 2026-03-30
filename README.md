# Duxel

<p align="center">
  <img src="logo.svg" alt="Duxel logo" width="615" />
</p>

Immediate-mode GUI framework for .NET 10, using a Vulkan renderer with a Windows-native platform backend.

**Current package version:** `0.2.1-preview`

[![NuGet](https://img.shields.io/nuget/vpre/Duxel.App)](https://www.nuget.org/packages/Duxel.App)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

- 한국어 문서: [README.ko.md](README.ko.md)
- Version history: [docs/version-history.md](docs/version-history.md) · [한국어](docs/version-history.ko.md)
- Duxel agent reference: [docs/duxel-agent-reference.md](docs/duxel-agent-reference.md) · [한국어](docs/duxel-agent-reference.ko.md)

## What it provides

- Immediate-mode widget API (`UiImmediateContext`) with `UiScreen.Render(...)` lifecycle.
- Vulkan backend with profile-based defaults (`Display` / `Render`) and configurable MSAA.
- Windows-native window/input backend (keyboard, mouse, wheel, IME, clipboard).
- NativeAOT-friendly runtime patterns.
- UI DSL (`.ui`) parser/runtime and source-generator path.

## Packages

| Package | Purpose |
|---|---|
| `Duxel.App` | Core app facade and shared runtime pipeline |
| `Duxel.Windows.App` | Windows platform runner package (`DuxelWindowsApp.Run`) |

## Quick start (FBA, Windows)

Create `hello.cs`:

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

Run:

```powershell
dotnet run hello.cs
```

## Featured FBA showcase

If you want to see Duxel beyond a hello-world window, start with these representative FBA samples:

- `samples/fba/all_features.cs` — the broad widget gallery with typography, layout, popup/context, input diagnostics, item state, multi-select, layers, drawing, image, and markdown showcase windows, now opening `Markdown Studio` on first launch instead of a blank surface.
- `samples/fba/ui_mixed_stress.cs` — a balanced multi-window showcase that fills the screen with controls, forms, long lists, data tables, text rendering, and a dense canvas stress area.
- `samples/fba/Duxel_perf_test_fba.cs` — a polygon stress-test sample with live controls for VSync, MSAA, cache toggles, polygon settings, and profile-oriented renderer checks.

<p align="center">
  <img src="captures/all-features-showcase.png" alt="Duxel all features FBA sample" width="1100" />
</p>

The screenshot above was captured from `all_features.cs`, which now opens the `Markdown Studio` surface on first launch while the menu bar still exposes the wider component gallery.

<p align="center">
  <img src="captures/ui-mixed-stress-showcase.png" alt="Duxel Mixed UI Stress FBA sample" width="1100" />
</p>

The screenshot above was captured from `ui_mixed_stress.cs`, a representative FBA sample that shows how Duxel can compose several tool-style windows, high-volume lists and tables, and custom drawing in a single frame.

<p align="center">
  <img src="captures/duxel-perf-test-showcase.png" alt="Duxel performance test FBA sample" width="1100" />
</p>

The performance screenshot was captured from `Duxel_perf_test_fba.cs` with a higher initial polygon count so the stress scene is visible immediately.

## Samples

- Project sample: `samples/Duxel.Sample`
  - `dotnet run --project samples/Duxel.Sample/`
- FBA samples: `samples/fba/*.cs`
  - `dotnet run samples/fba/all_features.cs`
  - `./run-fba.ps1 samples/fba/all_features.cs` (local project reference; NativeAOT by default)
  - `all_features.cs` now opens `Markdown Studio` by default and still includes dedicated typography, layout, popup/context, input-query, item-status, multi-select, and layer/animation showcase windows.

## DSL

Duxel supports declarative `.ui` files (indent-based tree) and runtime/state bindings.

- DSL reference: [docs/ui-dsl.md](docs/ui-dsl.md) · [한국어](docs/ui-dsl.ko.md)
- Agent reference: [docs/duxel-agent-reference.md](docs/duxel-agent-reference.md) · [한국어](docs/duxel-agent-reference.ko.md)
- FBA getting started: [docs/getting-started-fba.md](docs/getting-started-fba.md) · [한국어](docs/getting-started-fba.ko.md)
- FBA reference guide: [docs/fba-reference-guide.md](docs/fba-reference-guide.md) · [한국어](docs/fba-reference-guide.ko.md)
- FBA sample catalog: [docs/fba-run-samples.md](docs/fba-run-samples.md) · [한국어](docs/fba-run-samples.ko.md)
- Custom widgets: [docs/custom-widgets.md](docs/custom-widgets.md) · [한국어](docs/custom-widgets.ko.md)
- Design notes (Korean): [docs/design.ko.md](docs/design.ko.md)
- Optimization policy (Korean): [docs/optimization-policy.ko.md](docs/optimization-policy.ko.md)

## Build

```powershell
dotnet build Duxel.slnx -c Release
```

## License

MIT
