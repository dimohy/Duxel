<p align="center">
  <img src="samples/Duxel.Gallery/Assets/Brand/duxel-mark.svg" width="96" height="96" alt="" />
</p>

<h1 align="center">Duxel</h1>

<p align="center"><strong>A C# UI library for native desktop apps</strong></p>

Build application windows, tools, and custom visualizations with **retained-mode** C# widgets and an **immediate-mode** drawing API. Use either mode or combine them in the same Vulkan-rendered window. Start with the built-in Fluent 2 light and dark themes, then tailor the design to your app.

[![NuGet](https://img.shields.io/nuget/vpre/Duxel.App)](https://www.nuget.org/packages/Duxel.App)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://licenses.nuget.org/MIT)

[한국어](README.ko.md) · [Documentation](docs/README.md) · [Release history](docs/version-history.md)

## Build an interface

- **Retained mode** keeps a widget tree across frames. Use `Dux` for layout and `Fluent` for controls. Connect application state through `UiState<T>` and bindings. Use C# or the declarative `.ui` format.
- **Immediate mode** uses `UiScreen.Render` and `UiImmediateContext` for drawing, custom interaction, and diagnostics. An application can host widgets and immediate content together.
- **Fluent 2 themes** include light and dark Windows 11 designs. Customize colors, typography, and control geometry through `UiTheme`, `UiDesignTokens`, and `IUiDesign`. Use a provider to theme one region independently.

Duxel also includes virtualized lists and data grids, SVG and Markdown widgets, keyboard navigation, and platform accessibility. See the documentation for platform and component coverage. [Fluent 2 coverage](docs/fluent2-coverage.md) · [Theme provider](docs/fluent2-provider.md)

## Try the Gallery

On Windows with the .NET 11 SDK and a Vulkan-capable GPU, run this PowerShell line. It downloads a single C# file and launches the Gallery from NuGet.

```powershell
$file = Join-Path ([IO.Path]::GetTempPath()) 'duxel_gallery_0_3_preview.cs'; Invoke-WebRequest 'https://raw.githubusercontent.com/dimohy/Duxel/v0.3.0-preview/samples/fba/duxel_gallery.cs' -OutFile $file -UseBasicParsing; dotnet run --file $file
```

To build the Gallery from this checkout, run:

```powershell
./run-gallery.ps1
```

Choose an example, operate the actual control, and copy its C# code. Gallery settings offer English, Korean, Japanese, and Simplified Chinese. [Gallery guide](docs/gallery-navigation.md) · [Linux setup](docs/linux-platform.md)

## Start a Windows app

With the .NET 10 SDK or later, save this example as `hello.cs`. The file uses the published Windows package.

```csharp
#:property TargetFramework=net10.0
#:property PublishAot=false
#:property SelfContained=false
#:package Duxel.Windows.App@0.3.0-preview

using Duxel.App;
using Duxel.Core;
using Duxel.Windows.App;

var clicks = new UiState<int>(0);
var root = Dux.Column(12,
    Fluent.Title2("Hello, Duxel"),
    Fluent.Button("Add one", () => clicks.Value++),
    Fluent.Body1(() => $"Count: {clicks.Value}"));

DuxelWindowsApp.Run(DuxelApp.Options<LightDesign>(Dux.App(root), title: "Hello Duxel"));

readonly struct LightDesign : IUiDesign
{
    public static UiCompiledDesign Create() => UiCompiledDesign.Windows11;
}
```

```powershell
dotnet run hello.cs
```

For an existing Windows project, install `Duxel.Windows.App` with `dotnet add package Duxel.Windows.App --version 0.3.0-preview`. The packages target .NET 8, 9, and 10. [File-based app guide](docs/getting-started-fba.md) · [Declarative C# sample](samples/fba/declarative_dashboard_fba.cs) · [Immediate-mode sample](samples/fba/all_features.cs)

## Build from source

```powershell
dotnet build Duxel.slnx -c Release
```
