# Run Fluent 2 desktop apps from one C# file

Each example is an independent file-based app. Install the [.NET 11 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/11.0) and a Vulkan driver. On Linux, follow the [setup guide](linux-platform.md). If you already have the file, run `dotnet run --file <filename.cs>`.

Run the commands in PowerShell 7 on Windows or Bash on Linux. Each command downloads the example into the working directory and runs it.

The four apps below use the Windows app color mode. On Linux they read the [desktop color preference](https://flatpak.github.io/xdg-desktop-portal/docs/doc-org.freedesktop.portal.Settings.html) at launch via `gdbus`. They use Fluent light when the session has no preference or no Settings portal, as can happen under WSLg. Restart a Linux sample after changing the desktop preference.

## Calculator

Try decimal arithmetic, chained operators and clear. [C# source](../samples/fba/duxel_calculator.cs)

~~~powershell
curl.exe -fsSLO https://raw.githubusercontent.com/dimohy/Duxel/main/samples/fba/duxel_calculator.cs && dotnet run --file duxel_calculator.cs
~~~

~~~bash
curl -fsSLO https://raw.githubusercontent.com/dimohy/Duxel/main/samples/fba/duxel_calculator.cs && dotnet run --file duxel_calculator.cs
~~~

## Notepad

Type, select text, and save to duxel-note.txt. Pass a text file after -- to open an existing note. [C# source](../samples/fba/duxel_notepad.cs)

~~~powershell
curl.exe -fsSLO https://raw.githubusercontent.com/dimohy/Duxel/main/samples/fba/duxel_notepad.cs && dotnet run --file duxel_notepad.cs
~~~

~~~bash
curl -fsSLO https://raw.githubusercontent.com/dimohy/Duxel/main/samples/fba/duxel_notepad.cs && dotnet run --file duxel_notepad.cs
~~~

## Large List

Scroll 100,000 rows. Drag the scrollbar and use Top and Bottom to jump across the list. [C# source](../samples/fba/duxel_large_list.cs)

~~~powershell
curl.exe -fsSLO https://raw.githubusercontent.com/dimohy/Duxel/main/samples/fba/duxel_large_list.cs && dotnet run --file duxel_large_list.cs
~~~

~~~bash
curl -fsSLO https://raw.githubusercontent.com/dimohy/Duxel/main/samples/fba/duxel_large_list.cs && dotnet run --file duxel_large_list.cs
~~~

## Motion Canvas

Change the load from 500 to 5,000 animated circles. Pause and resume while using the Fluent 2 controls. [C# source](../samples/fba/duxel_render_motion.cs)

~~~powershell
curl.exe -fsSLO https://raw.githubusercontent.com/dimohy/Duxel/main/samples/fba/duxel_render_motion.cs && dotnet run --file duxel_render_motion.cs
~~~

~~~bash
curl -fsSLO https://raw.githubusercontent.com/dimohy/Duxel/main/samples/fba/duxel_render_motion.cs && dotnet run --file duxel_render_motion.cs
~~~

## Smaller examples and the Gallery

- [Controls and state](../samples/fba/gallery_controls.cs) uses `UiState<T>`, `Fluent.Switch`, and `Fluent.Text`. On Linux specify a font with `dotnet run --file gallery_controls.cs -- "$(fc-match -f '%{file}' sans-serif)"`.
- The [layout example](../samples/fba/gallery_layout.cs) demonstrates the nine Align positions and SizedBox sizing options on Windows.
- The [Gallery guide](gallery-navigation.md) covers layout, controls, image fit, SVG, and Markdown.
- Large List and Motion Canvas show behavior on your machine. For older measurements and their test conditions, see the [performance archive](archive/README.md).

[한국어](fba-run-samples.ko.md)
