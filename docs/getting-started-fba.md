# Build a C# desktop app with one file

Duxel shares widgets and application state between Windows and Linux. Use Fluent 2 for the controls, `Dux.Column` or `Dux.Row` for layout, and `UiState<T>` for controls that read and change the same state.

1. Install the [.NET 11 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/11.0) and start a graphical session with Vulkan. On Linux follow [the Linux setup guide](linux-platform.md).
2. [Run Duxel Gallery](../README.md#try-duxel-gallery-040). Find a control, exercise it, and copy the C# shown below it.
3. Open [`duxel_calculator.cs`](../samples/fba/duxel_calculator.cs). The file selects the appropriate host package at build time. Its widget tree and arithmetic are shared.
4. From the checkout root, run `dotnet run --file samples/fba/duxel_calculator.cs`. The Host folder beside the sample provides platform startup. Change a widget or layout. When working with a coding assistant, point it to the exact Gallery page and the [API and source map](duxel-agent-reference.md).

## Package choice

| Package | When to use it |
| --- | --- |
| [Duxel.Windows.App](https://www.nuget.org/packages/Duxel.Windows.App/0.4.0) | Build a Windows desktop app. |
| [Duxel.Linux.App](https://www.nuget.org/packages/Duxel.Linux.App/0.4.0) | Build a Linux X11 desktop app. Configure installed font files for the host. |
| [Duxel.App](https://www.nuget.org/packages/Duxel.App/0.4.0) | Shared runtime, included through either host. |
| [Duxel.Gallery](https://www.nuget.org/packages/Duxel.Gallery/0.4.0) / [Duxel.Gallery.Linux](https://www.nuget.org/packages/Duxel.Gallery.Linux/0.4.0) | Launch the corresponding platform's examples. |

[One-line example commands](fba-run-samples.md) · [한국어](getting-started-fba.ko.md)
