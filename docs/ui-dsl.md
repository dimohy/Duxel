# UI DSL Reference

This document describes the DSL grammar based on the current code (`UiDslParser`, `UiDslWidgetDispatcher`, `UiDslPipeline`).

## Summary

- File format: `.ui` (indent-based tree)
- Parser: `Duxel.Core.Dsl.UiDslParser`
- Runtime rendering: `UiDslScreen` (managed hot-reload + NativeAOT source-generated)
- State store: `UiDslState` or `IUiDslValueSource`
- Event wiring: `IUiDslEventSink`

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
- Convenience binder: `UiDslEventBinder` (per-id callback registration)

For runtime binding, implement `IUiDslEventSink` and/or `IUiDslValueSource` and pass them to `UiDslScreen`.

### UiDslEventBinder

Instead of implementing `IUiDslEventSink` directly, use `UiDslEventBinder` for fluent per-id binding:

```csharp
var events = new UiDslEventBinder()
    .Bind("save", () => Console.WriteLine("Save clicked"))
    .Bind("file.new", () => Console.WriteLine("New file"))
    .BindCheckbox("dark_mode", value => Console.WriteLine($"Dark mode: {value}"))
    .OnAnyButton(id => Console.WriteLine($"Unhandled button: {id}"))
    .OnAnyCheckbox((id, v) => Console.WriteLine($"Unhandled checkbox: {id}={v}"));

var screen = new UiDslScreen("Ui/Main.ui", eventSink: events);
```

### Text Binding

The `Text` widget supports dynamic content via `Bind`:

```text
Text Bind="status" Default="Ready"
```

When `Bind` is specified, the text is read from `IUiDslValueSource`/`UiDslState` instead of a static string.

## Control Flow

The DSL supports control flow constructs inspired by Kotlin DSL and SwiftUI ViewBuilder patterns.

### If / ElseIf / Else

Conditional rendering based on state values.

```text
# Bool condition
If Bind="dark_mode"
  Text "Dark mode is ON"
ElseIf Bind="high_contrast"
  Text "High contrast mode"
Else
  Text "Default theme"

# Negated bool
If Bind="show_advanced" Not=true
  Text "Advanced options are hidden"

# String equality
If "user_role" = "admin"
  Button "admin_panel" "Admin Panel"

# String inequality
If "status" != "ready"
  Text "System is not ready"
```

**Rules:**
- `If Bind="key"` — true when bool state `key` is true
- `If Bind="key" Not=true` — negated condition
- `If "key" = "value"` — string equality check
- `If "key" != "value"` — string inequality check
- `ElseIf` follows the same syntax; skipped if a prior `If`/`ElseIf` was satisfied
- `Else` renders only when all preceding `If`/`ElseIf` were false

### Visible

Shorthand for `If Bind=...` without `Else` support.

```text
Visible Bind="show_advanced"
  SliderFloat "gamma" "Gamma" 0.5 0.0 3.0
  SliderFloat "contrast" "Contrast" 1.0 0.0 2.0
```

### ForEach

Repeat child widgets for a numeric range. Children are buffered and replayed for each iteration.

```text
# Range: start,end (inclusive)
ForEach Range=1,5
  Button "item_{_index}" "Item {_index}"

# Count-based (0 to N-1)
ForEach Count=3
  Text "Row {_index}"

# Custom variable name
ForEach Range=0,9 Var=i
  Text "Entry {i}"
```

**Template substitution:** Use `{_index}` (or `{varName}`) in any string argument. The placeholder is replaced with the current iteration value.

**Parameters:**
| Parameter | Description | Example |
|---|---|---|
| `Range=start,end` | Inclusive range [start, end] | `Range=1,5` → 1,2,3,4,5 |
| `Range=N` | Range [0, N] | `Range=4` → 0,1,2,3,4 |
| `Count=N` | Range [0, N-1] | `Count=5` → 0,1,2,3,4 |
| `Var=name` | Name of the iteration variable (default: `_index`) | `Var=i` |

### Switch / Case / Default

Multi-branch rendering based on a string state value.

```text
Switch Bind="theme"
  Case "Dark"
    Text "Using dark theme"
  Case "Light"
    Text "Using light theme"
  Default
    Text "Unknown theme"
```

**Rules:**
- `Switch Bind="key"` reads a string from state
- `Case "value"` matches when the switch value equals the case value (ordinal comparison)
- Only the first matching `Case` renders; subsequent cases are skipped
- `Default` renders when no `Case` matched

### Set

Set a state value during rendering (useful for initializing defaults).

```text
Set "greeting" "Hello World"
```

## Minimal Runtime Example

```csharp
#:property TargetFramework=net10.0
#:property platform=windows
#:package Duxel.$(platform).App@*-*

using Duxel.App;
using Duxel.Core.Dsl;
using Duxel.Windows.App;

var screen = new UiDslScreen("Ui/Main.ui", "Ui/theme.duxel-theme");

DuxelWindowsApp.Run(new DuxelAppOptions
{
    Window = new DuxelWindowOptions { Title = "DSL Demo" },
    Screen = screen
});
```

## Hot Reload / Generator

- Runtime hot reload: `UiDslPipeline.CreateHotReloadRenderer(...)`
- Generated renderer: `UiDslPipeline.CreateGeneratedRenderer(...)`
- In NativeAOT builds (`DUX_NATIVEAOT`), runtime hot reload is disabled.

## How to Confirm Supported Nodes

The final source of truth for supported nodes/widgets is the `switch` in `Duxel.Core.Dsl.UiDslWidgetDispatcher.BeginOrInvoke(...)`.

Runnable examples: `samples/Duxel.ThemeDemo`, `samples/Duxel.Sample`.
