# UI DSL Reference

> Synced: 2026-02-26  
> Korean original: [ui-dsl.md](ui-dsl.md)

This document mirrors the current behavior of `UiDslParser`, `UiDslWidgetDispatcher`, and `UiDslPipeline`.

## Syntax Rules

- File format: `.ui` (indent-based tree).
- Default indent size: 2 spaces.
- Tabs are not allowed by default (`AllowTabs=false`).
- Line comments: `#` and `//` (outside string literals).
- Each line is `NodeName` + optional arguments.

```text
Window "Main"
  Row
    Button Id="ok" Text="OK"
```

## Arguments

- Named arguments: `Key=Value`
- Positional arguments: ordered tokens
- Mixing is allowed.

```text
Button Id="play" Text="Play" Size="120,32"
Button "play" "Play"
```

For `Id`/`Text` nodes (e.g., `Button`), fallback is:

1. one positional token => `Id=Text=token`
2. two positional tokens => `(Id, Text)`
3. only `Id` => `Text=Id`
4. only `Text` => `Id=Text`

## Begin Alias Normalization

The parser normalizes these nodes to `Begin*` names:

- `Group`, `Child`, `MenuBar`, `MainMenuBar`, `Menu`
- `Popup`, `PopupModal`, `PopupContextItem`, `PopupContextWindow`, `PopupContextVoid`
- `TabBar`, `TabItem`, `Table`, `Tooltip`, `ItemTooltip`
- `DragDropSource`, `DragDropTarget`, `Disabled`, `MultiSelect`

Special case:

- `Combo` and `ListBox` become `BeginCombo` / `BeginListBox` only when they contain child blocks.

## Value Parsing

- `bool`: `true/false` or `0/1`
- numbers: invariant-culture numeric parsing
- vectors: `x,y` / `x;y` / `x|y`
- colors: `#RRGGBB`, `#RRGGBBAA`, `0x...`, integer
- string lists and numeric lists: separators `|`, `,`, `;`
- enums: case-insensitive `Enum.TryParse`

## State and Bindings

Stateful widgets use `Id` as key.

- State store: `UiDslState`
- External source: `IUiDslValueSource`
- Events: `IUiDslEventSink`
- App helper: `UiDslBindings` in `Duxel.App`

```csharp
var bindings = new UiDslBindings()
    .BindButton("apply", () => Console.WriteLine("apply"))
    .BindBool("vsync", () => vsync, v => vsync = v)
    .BindFloat("volume", () => volume, v => volume = v);
```

## Runtime vs Generated Renderer

- Hot reload: `UiDslPipeline.CreateHotReloadRenderer(...)`
- Generated path: `UiDslPipeline.CreateGeneratedRenderer(...)`
- In NativeAOT (`DUX_NATIVEAOT`), runtime hot reload is disabled.

## Source of Truth

Supported nodes are defined in `UiDslWidgetDispatcher.BeginOrInvoke(...)` (`switch` cases).

Practical examples:

- `samples/fba/dsl_showcase.cs`
- `samples/fba/dsl_interaction.cs`
- `samples/Duxel.Sample`
