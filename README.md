# Duxel

[Rating](docs/fluent2-rating.md): try `Fluent.Rating` half steps, right-to-left layout, and wrapping in Gallery under **Fluent controls → Input and selection → Rating**. Run `./run-gallery.ps1 -NoRestore` when restored package assets match current dependencies.

[Widget contract validation](docs/widget-contract-validation.md): one isolated runner for the declared managed CPU suites; GPU, NativeAOT and full product acceptance remain separate.

<p align="center">
  <img src="logo.svg" alt="Duxel logo" width="615" />
</p>

Duxel is a .NET UI library with an immediate-mode API for drawing and interaction, composable Fluent 2 widgets for building application interfaces, and an extensible design system. Applications can use the two UI styles together within the same Vulkan-rendered window.

**Current published package version:** `0.2.12-preview`

**Supported package targets:** `net8.0`, `net9.0`, `net10.0`. The file-based app (FBA) workflow still requires the .NET 10 SDK.

[![NuGet](https://img.shields.io/nuget/vpre/Duxel.App)](https://www.nuget.org/packages/Duxel.App)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

- Korean documentation: [README.ko.md](README.ko.md)
- Version history: [docs/version-history.md](docs/version-history.md) · [Korean](docs/version-history.ko.md)
- Duxel agent reference: [docs/duxel-agent-reference.md](docs/duxel-agent-reference.md) · [Korean](docs/duxel-agent-reference.ko.md)
- Extended title bar guide: [docs/extended-title-bar-guide.md](docs/extended-title-bar-guide.md) · [Korean](docs/extended-title-bar-guide.ko.md)

## What it provides

- Immediate-mode widget API (`UiImmediateContext`) with `UiScreen.Render(...)` lifecycle.
- Composable Fluent 2 controls with reusable widget trees, state bindings, keyboard navigation, and accessibility.
- Extensible themes and design tokens. Customize colors with `UiTheme`, control geometry with `UiDesignTokens`, and package a design with `IUiDesign`.
- Vulkan backend with profile-based defaults (`Display` / `Render`) and configurable MSAA.
- Windows-native window/input backend (keyboard, mouse, wheel, IME, clipboard).
- Windows 11-style extended title bars with application tabs, native caption behavior, Snap Layout, system menus, DPI, and multi-monitor support.
- NativeAOT-friendly runtime patterns.
- UI DSL (`.ui`) parser/runtime and source-generator path.

## Packages

| Package | Purpose |
|---|---|
| `Duxel.App` | Core app facade and shared runtime pipeline |
| `Duxel.Windows.App` | Windows platform runner package (`DuxelWindowsApp.Run`) |

For a regular .NET 8 Windows application, target `net8.0-windows7.0` (or a later Windows TFM). Duxel package assemblies provide portable `net8.0` assets, while the Windows TFM makes the platform requirement explicit to analyzers and consumers. FBA samples continue to use `net10.0` because file-based apps require the .NET 10 SDK.

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
        ui.Text("Hello, Duxel!");
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

## Samples

- Gallery: run `./run-gallery.ps1` to try the widgets and inspect their C# examples. Open **Settings → Gallery language** to choose English, Korean, Japanese, or Simplified Chinese.
- Project sample: `samples/Duxel.Sample`
  - `dotnet run --project samples/Duxel.Sample/`
- FBA samples: `samples/fba/*.cs`
  - `dotnet run samples/fba/all_features.cs`
  - `./run-fba.ps1 samples/fba/all_features.cs` (local project reference; NativeAOT by default)
  - `all_features.cs` now opens `Markdown Studio` by default and still includes dedicated typography, layout, popup/context, input-query, item-status, multi-select, and layer/animation showcase windows.

## DSL

Duxel supports declarative `.ui` files (indent-based tree) and runtime/state bindings.

- DSL reference: [docs/ui-dsl.md](docs/ui-dsl.md) · [Korean](docs/ui-dsl.ko.md)
- Agent reference: [docs/duxel-agent-reference.md](docs/duxel-agent-reference.md) · [Korean](docs/duxel-agent-reference.ko.md)
- Extended title bar: [docs/extended-title-bar-guide.md](docs/extended-title-bar-guide.md) · [Korean](docs/extended-title-bar-guide.ko.md)
- FBA getting started: [docs/getting-started-fba.md](docs/getting-started-fba.md) · [Korean](docs/getting-started-fba.ko.md)
- FBA reference guide: [docs/fba-reference-guide.md](docs/fba-reference-guide.md) · [Korean](docs/fba-reference-guide.ko.md)
- FBA sample catalog: [docs/fba-run-samples.md](docs/fba-run-samples.md) · [Korean](docs/fba-run-samples.ko.md)
- Custom widgets: [docs/custom-widgets.md](docs/custom-widgets.md) · [Korean](docs/custom-widgets.ko.md)
- Design notes (Korean): [docs/design.ko.md](docs/design.ko.md)
- Optimization policy (Korean): [docs/optimization-policy.ko.md](docs/optimization-policy.ko.md)

## Build

```powershell
dotnet build Duxel.slnx -c Release
```

## License

MIT

