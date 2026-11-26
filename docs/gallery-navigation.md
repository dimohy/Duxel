# Use Duxel Gallery

[Launch Gallery on Windows or Linux](../README.md#try-duxel-gallery-040). The Gallery runs actual Duxel widgets and shows their C# source.

1. Choose a component from the navigation. Try variants and states directly in the example.
2. Select code in CodeView and copy it. You can also open the context menu to select all.
3. Paste the widget into a [file-based app](getting-started-fba.md) and keep the platform host from the app.

| Explore | Look for |
| --- | --- |
| Controls | Form fields, buttons, toggles, dialogs, and the MenuBar example. |
| Layout | Align, SizedBox, UniformGrid, scroll views and virtual lists. |
| Graphics and documents | Image file formats and fit, SVG, Markdown, and rendering diagnostics. |

Use Settings to choose English, Korean, Japanese, or Simplified Chinese.

## Select a .NET runtime

The source Gallery supports .NET 8, 9, 10, and 11. Build from the repository using the SDK in `global.json` and install the runtime you select below. Replace the `8` in either command with `9`, `10`, or `11`.

~~~powershell
dotnet run --project samples/Duxel.Gallery -f net8.0-windows
~~~

~~~bash
dotnet run --project samples/Duxel.Gallery.Linux -f net8.0
~~~

## Try the desktop menu bar

Open **Fluent controls → Actions and navigation → MenuBar** in the source Gallery. Open File, then move the pointer to Edit. With the menu bar focused, use Left and Right to switch menus, Down to open one, and Escape to return to its trigger. The example also includes a cascading submenu, a divider, a disabled action, and a checkbox. Copy its C# or try the [Notepad app](fba-run-samples.md#notepad).

[Runnable examples](fba-run-samples.md) · [한국어](gallery-navigation.ko.md)
