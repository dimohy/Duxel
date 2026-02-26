# Duxel FBA Quick Start

> Synced: 2026-02-26  
> Korean original: [getting-started-fba.md](getting-started-fba.md)

## Requirements

| Item | Requirement |
|---|---|
| .NET SDK | 10.0+ |
| OS | Windows 10/11 |
| GPU | Vulkan 1.0+ |

## 30-Second Run

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

## Sample Execution

```powershell
dotnet run samples/fba/all_features.cs
./run-fba.ps1 samples/fba/all_features.cs
./run-fba.ps1 samples/fba/all_features.cs -Managed
```

- `dotnet run`: NuGet package execution path.
- `run-fba.ps1`: local project-reference path (NativeAOT by default).

## DSL Example

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

## More Docs

- [fba-reference-guide.en.md](fba-reference-guide.en.md)
- [fba-run-samples.md](fba-run-samples.md)
- [ui-dsl.en.md](ui-dsl.en.md)
