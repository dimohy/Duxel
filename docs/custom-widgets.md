# Custom Widgets

Last synced: 2026-03-09

## Overview

Duxel built-in widgets are exposed as methods on `UiImmediateContext`.

Custom widgets are instance-based objects that implement `IUiCustomWidget` and render themselves by receiving the current `UiImmediateContext` in `Render(UiImmediateContext ui)`.

This keeps reusable widget behavior outside the built-in widget surface while still using the same immediate-mode primitives.

## Public API

- `IUiCustomWidget`
- `MarkdownEditorWidget`
- `MarkdownViewerWidget`

## Minimal Example

```csharp
using Duxel.Core;

public sealed class MarkdownScreen : UiScreen
{
    private readonly MarkdownEditorWidget _editor = new("editor", "Markdown")
    {
        Height = 320f,
        Text = "# Hello\n\nThis is **custom** markdown."
    };

    private readonly MarkdownViewerWidget _viewer = new("viewer")
    {
        Height = 320f,
    };

    public override void Render(UiImmediateContext ui)
    {
        ui.BeginWindow("Markdown Demo");

        ui.Columns(2, false);
        _editor.Render(ui);

        ui.NextColumn();
        _viewer.Markdown = _editor.Text;
        _viewer.Render(ui);

        ui.Columns(1, false);
        ui.EndWindow();
    }
}
```

## Markdown Viewer Features

- Headings, paragraphs, quotes, fenced code blocks
- Bullet lists, checklists, nested ordered lists
- Tables
- Inline links, inline code, emphasis, strong text
- Interactive checklist toggles in preview
- Link hover tooltip showing the target URL
- Code block copy button using clipboard integration

## Sample

The showcase sample is available in `Markdown Studio` inside `samples/fba/all_features.cs`, which now also includes dedicated typography/layout and advanced interaction showcase windows for cross-checking widget behavior side by side.

Run it with:

```powershell
./run-fba.ps1 samples/fba/all_features.cs -NoCache
```