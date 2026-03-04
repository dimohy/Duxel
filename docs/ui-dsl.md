# UI DSL Reference

This document describes the DSL grammar based on the current code (`UiDslParser`, `UiDslWidgetDispatcher`, `UiDslPipeline`).

## Summary

- File format: `.ui` (indent-based tree)
- Parser: `Duxel.Core.Dsl.UiDslParser`
- Runtime execution: `UiDslDocument.Emit(...)` → `UiDslImmediateEmitter`
- State store: `UiDslState` or `IUiDslValueSource`
- Event wiring: `IUiDslEventSink` / `UiDslBindings`

## Grammar Rules

### 1) Indentation

- Default indent size is 2 spaces.
- Tabs are not allowed by default (`AllowTabs=false`).
- If indentation is not a multiple of 2 spaces, parsing throws an exception.

```text
Window "Main"
  Row
    Button Id="ok" Text="OK"
```

### 2) Lines and Tokens

- A line is composed of `NodeName` + arguments.
- Strings use double quotes (`"`).
- Tokens are split by whitespace, while spaces inside strings are preserved.
- Comments use `#` or `//` (outside string literals only).

### 3) Argument Forms

- Named arguments: `Key=Value`
- Positional arguments: order-based
- Mixed usage is allowed

```text
Button Id="play" Text="Play" Size="120,32"
Button "play" "Play"
```

For nodes that consume `Id`/`Text` (such as `Button`), these fallback rules apply:

1. If both `Id` and `Text` are missing, one positional value becomes `(Id=Text=value)`
2. With two positional values, they map to `(Id, Text)`
3. If only `Id` exists, `Text=Id`
4. If only `Text` exists, `Id=Text`

## Begin Alias Normalization

After parsing, these names are automatically normalized to `Begin*` forms:

- `Group` → `BeginGroup`
- `Child` → `BeginChild`
- `MenuBar` → `BeginMenuBar`
- `MainMenuBar` → `BeginMainMenuBar`
- `Menu` → `BeginMenu`
- `Popup` → `BeginPopup`
- `PopupModal` → `BeginPopupModal`
- `PopupContextItem` → `BeginPopupContextItem`
- `PopupContextWindow` → `BeginPopupContextWindow`
- `PopupContextVoid` → `BeginPopupContextVoid`
- `TabBar` → `BeginTabBar`
- `TabItem` → `BeginTabItem`
- `Table` → `BeginTable`
- `Tooltip` → `BeginTooltip`
- `ItemTooltip` → `BeginItemTooltip`
- `DragDropSource` → `BeginDragDropSource`
- `DragDropTarget` → `BeginDragDropTarget`
- `Disabled` → `BeginDisabled`
- `MultiSelect` → `BeginMultiSelect`

Additional rules:

- `Combo`/`ListBox` are normalized to `BeginCombo`/`BeginListBox` **only when they have child blocks**.
- Without children, they are treated as single widget calls (`Combo`/`ListBox`).

## Value Parsing Rules

Based on `UiDslArgReader`:

- `bool`: `true/false` or `0/1`
- `int/uint/float/double`: invariant-culture numeric parsing
- `UiVector2/UiVector4`: `x,y` or `x;y` or `x|y`
- `UiColor`: `#RRGGBB`, `#RRGGBBAA`, `0x...`, integer values
- list forms (`Items`, arrays): separators `|`, `,`, `;`
- enum: name strings (`Enum.TryParse`, case-insensitive)

## State and Event Bindings

Stateful widgets (`Checkbox`, `Slider*`, `Input*`, `Combo`, `ListBox`, etc.) read/write values by `Id`.

- Default store: `UiDslState`
- External value source: `IUiDslValueSource`
- Events: `IUiDslEventSink`

When using `Duxel.App`, `UiDslBindings` provides an easy binding path.

```csharp
var bindings = new UiDslBindings()
    .BindButton("apply", () => Console.WriteLine("apply"))
    .BindBool("vsync", () => vsync, v => vsync = v)
    .BindFloat("volume", () => volume, v => volume = v)
    .BindString("name", () => name, v => name = v);
```

## Minimal Runtime Example

```csharp
#:property TargetFramework=net10.0
#:property platform=windows
#:package Duxel.$(platform).App@*-*

using Duxel.App;
using Duxel.Core.Dsl;
using Duxel.Windows.App;

var text = """
Window "DSL"
  Text "Hello"
  Checkbox Id="dark" Text="Dark" Default=true
  SliderFloat Id="volume" Text="Volume" Min=0 Max=1
""";

var doc = UiDslParser.Parse(text);

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

## Hot Reload / Generator

- Runtime hot reload: `UiDslPipeline.CreateHotReloadRenderer(...)`
- Generated renderer: `UiDslPipeline.CreateGeneratedRenderer(...)`
- In NativeAOT builds (`DUX_NATIVEAOT`), runtime hot reload is disabled.

## How to Confirm Supported Nodes

The final source of truth for supported nodes/widgets is the `switch` in `Duxel.Core.Dsl.UiDslWidgetDispatcher.BeginOrInvoke(...)`.

Runnable examples: `samples/fba/dsl_showcase.cs`, `samples/fba/dsl_interaction.cs`, `samples/Duxel.Sample`.
