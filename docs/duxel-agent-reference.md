# Build with Duxel and a coding assistant

[한국어](duxel-agent-reference.ko.md) · [Duxel documentation](README.md)

Give your assistant the target platform, the screen you want, and the reference version. Use the installed NuGet version for an application that consumes packages. Use this checkout and its local-source runner when you need current branch APIs.

## Pick the starting point

| Task | Reference |
| --- | --- |
| Retained mode for forms, tools, dashboards and dialogs | [Declarative C# dashboard](../samples/fba/declarative_dashboard_fba.cs), [Gallery](gallery-navigation.md) |
| Immediate mode for custom drawing and direct input | [Immediate-mode examples](../samples/fba/all_features.cs), [Architecture](design.md) |
| Layout, theming and custom widgets | [Containers](widget-containers.md), [Theme provider](fluent2-provider.md), [Custom widgets](custom-widgets.md) |
| Load or generate a declarative UI | [`.ui` syntax and source generation](ui-dsl.md) |

## Starting prompt

> Build a Windows desktop screen in a single C# file using this Duxel checkout. Use `Dux` for layout, `Fluent` for controls, and `UiState<T>` with bindings for user-editable values. Use retained mode and keep the widget tree alive across frames. Use the built-in light design and show how to switch to dark. Use an immediate-mode view if the screen needs custom drawing. Check the public signatures or copy the matching Gallery sample before writing code. Compile and run the result. Verify the main click or keyboard interaction and tell me which command you ran.

## Check the result

- Start with the nearest Gallery example. Its copied C# often includes the state and options needed to use the control.
- When diagnosing resident geometry memory, distinguish cached draw-list bytes from Vulkan page allocations. See [retained rendering](retained-rendering.md).
- Review keyboard focus, editing and content overflow for the actual screen. Use [keyboard guidance](widget-keyboard.md) and [accessibility guidance](widget-accessibility.md).
- For an immediate-mode document viewport that scrolls vertically, pass `horizontalScroll: false` to `BeginChild`. The viewport reserves the scrollbar gutter during layout. A nested code view can still scroll horizontally when a line exceeds its own width.
- Run the local source example with `./run-fba.ps1 samples/fba/declarative_dashboard_fba.cs -Managed -NoCache`. For published packages, follow the [file-based app quick start](getting-started-fba.md).
