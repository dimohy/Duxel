<img src="samples/Duxel.Gallery/Assets/Brand/duxel-mark.svg" width="72" height="72" alt="Duxel logo">

# Duxel · Cross-platform C# desktop UI for Windows and Linux

Build fast native desktop apps for **Windows and Linux** with a shared **C# / .NET** UI, **Fluent 2** controls, and a **Vulkan** renderer. Duxel is designed for **AI-assisted development and vibe coding**. Pick a working control in the Gallery, share its C# example with your coding assistant, and iterate in a file-based app (FBA). The same widget and state code runs with either platform host.

[한국어](README.ko.md) · [Documentation](docs/README.md) · [Examples](docs/fba-run-samples.md) · [Release history](docs/version-history.md)

## Try Duxel Gallery 0.4.0

Install the [.NET 11 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/11.0) and use a graphical desktop with Vulkan. The Gallery shows working controls, layout, images, SVG, and Markdown with C# examples you can copy.

### Start a Windows app

Open PowerShell 7 and run the Gallery as a file-based app.

~~~powershell
curl.exe -fsSLO https://raw.githubusercontent.com/dimohy/Duxel/v0.4.0/samples/fba/duxel_gallery.cs && dotnet run --file duxel_gallery.cs
~~~

### Start a Linux app

Use Bash on Linux or WSLg with X11/Xwayland and Vulkan. [Linux setup](docs/linux-platform.md)

~~~bash
curl -fsSLO https://raw.githubusercontent.com/dimohy/Duxel/v0.4.0/samples/fba/duxel_gallery_linux.cs && dotnet run --file duxel_gallery_linux.cs
~~~

![Duxel Gallery showing the SizedBox layout demo and its C# source](docs/images/duxel-0.4.0-gallery.png)

## Run a Fluent 2 desktop app

Download and run any of the following as a single C# file. The Calculator, Notepad, Large List, and Motion Canvas run with the same source on Windows and Linux. [One-line commands for every example](docs/fba-run-samples.md)

| Example | What to try |
| --- | --- |
| [Calculator](samples/fba/duxel_calculator.cs) | Chain decimal operations and try the divide-by-zero case. |
| [Notepad](samples/fba/duxel_notepad.cs) | Edit a local note and save it as plain text. |
| [Large List](samples/fba/duxel_large_list.cs) | Scroll or jump through 100,000 rows with a virtualized viewport. |
| [Motion Canvas](samples/fba/duxel_render_motion.cs) | Adjust the number of animated shapes while the Fluent 2 controls stay interactive. |

![The inventory sample showing 12 row widgets for 100,000 items](docs/images/duxel-0.4.0-inventory.png)

## Build with Duxel

- Compose retained-mode widgets with `Dux`, `Fluent`, and `UiState<T>`. Use immediate-mode drawing for custom graphics inside the same UI.
- Reference the Windows or Linux app host in a project or via `#:package` in a single C# file. The hosts and shared runtime ship assets for .NET 8, 9, and 10. The 0.4.0 Gallery launchers use the .NET 11 SDK.
- Check rendering behavior in the Gallery, Large List, or Motion Canvas. For measurements from earlier builds, see [historical benchmarks](docs/archive/README.md).

[Start a C# app](docs/getting-started-fba.md) · [API and source map](docs/duxel-agent-reference.md) · [Repository instructions](AGENTS.md) · [LLM-friendly documentation index](llms.txt)
