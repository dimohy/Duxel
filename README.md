# Duxel

<p align="center">
  <img src="logo.svg" alt="Duxel logo" width="615" />
</p>

Immediate-mode GUI framework for .NET 10, using a Vulkan renderer with a Windows-native platform backend.

**Current package version:** `0.1.13-preview`

[![NuGet](https://img.shields.io/nuget/vpre/Duxel.App)](https://www.nuget.org/packages/Duxel.App)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

- 한국어 문서: [README.ko.md](README.ko.md)
- Version history: [docs/version-history.en.md](docs/version-history.en.md) · [한국어](docs/version-history.md)

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

## Samples

- Project sample: `samples/Duxel.Sample`
  - `dotnet run --project samples/Duxel.Sample/`
- FBA samples: `samples/fba/*.cs`
  - `dotnet run samples/fba/all_features.cs`
  - `./run-fba.ps1 samples/fba/all_features.cs` (local project reference; NativeAOT by default)

## DSL

Duxel supports declarative `.ui` files (indent-based tree) and runtime/state bindings.

- DSL reference: [docs/ui-dsl.en.md](docs/ui-dsl.en.md) · [한국어](docs/ui-dsl.md)
- FBA getting started: [docs/getting-started-fba.en.md](docs/getting-started-fba.en.md) · [한국어](docs/getting-started-fba.md)
- FBA reference guide: [docs/fba-reference-guide.en.md](docs/fba-reference-guide.en.md) · [한국어](docs/fba-reference-guide.md)

## Build

```powershell
dotnet build Duxel.slnx -c Release
```

## License

MIT
