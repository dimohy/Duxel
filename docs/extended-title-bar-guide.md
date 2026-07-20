# Duxel Extended Title Bar Guide

> Last synced: 2026-07-20
>
> Audience: Duxel application developers, framework contributors, and coding AI agents
>
> Platform: Windows 11 / Windows-native backend
>
> Korean: [extended-title-bar-guide.ko.md](extended-title-bar-guide.ko.md)

## Purpose

`DuxelTitleBarMode.ExtendedContent` lets an application render tabs, search boxes, menus, and other UI from the top of the window while Windows continues to own the native Minimize, Maximize, and Close buttons and caption behavior.

This guide covers:

- enabling the extended title bar in an application
- laying out content without overlapping native caption buttons
- separating draggable space from interactive controls
- coordinate rules for DPI, multiple monitors, and maximized windows
- validation required before developers or coding agents report completion
- Win32/DWM contracts that framework contributors must preserve

The complete executable example is [`samples/fba/extended_title_bar_fba.cs`](../samples/fba/extended_title_bar_fba.cs).

## Choose a title-bar mode

| Mode | Title-bar content | Caption buttons | Application content origin |
|---|---|---|---|
| `Default` | Resolved from `UseDuxelTitleBar` | Follows the resolved mode | Follows the resolved mode |
| `System` | Windows | Windows | Native client area |
| `Duxel` | Duxel | Duxel | Below `DuxelTitleBarHeight` |
| `ExtendedContent` | Application | Windows/DWM | `(0, 0)` |

Set `TitleBarMode` explicitly in new code so the intent is unambiguous.

```csharp
Window = new DuxelWindowOptions
{
    Title = "Tabbed Duxel App",
    Width = 1100,
    Height = 700,
    TitleBarMode = DuxelTitleBarMode.ExtendedContent,
    IntegrateSystemChrome = true,
};
```

For source compatibility, `Default` resolves as follows:

- `UseDuxelTitleBar = true` → `Duxel`
- `UseDuxelTitleBar = false` → `System`
- an explicit `TitleBarMode` takes precedence over `UseDuxelTitleBar`

`IntegrateSystemChrome` integrates DWM colors, dark mode, borders, and corners. It does not extend application content into the title bar and does not replace `ExtendedContent`.

## Public API contract

### `TryGetCaptionButtonBounds`

```csharp
bool available = ui.TryGetCaptionButtonBounds(out UiRect bounds);
```

`bounds` encloses the complete native Minimize/Maximize/Close button cluster.

- unit: Duxel logical pixels
- origin: the current Duxel client-area origin `(0, 0)`
- valid mode: `ExtendedContent`
- returns `false` while hidden, minimized, or when DWM cannot provide a valid bound
- `Y` may be negative while maximized; do not assume `Y == 0`

Never hardcode a single button width or the cluster width. Windows theme, DPI, accessibility settings, and OS versions can change the actual bounds.

### `SetTitleBarDragRegions`

```csharp
ui.SetTitleBarDragRegions([
    new UiRect(x, y, width, height),
]);
```

Each rectangle produces `HTCAPTION` during Windows non-client hit testing.

- coordinates use the same Duxel logical client space as `TryGetCaptionButtonBounds`
- each call atomically replaces the complete previous region set
- `ui.SetTitleBarDragRegions([])` clears all regions
- every coordinate must be finite, and width and height must be positive
- calling it outside `ExtendedContent` throws `InvalidOperationException`
- the last region set remains active until replaced, so recompute dynamic layouts every frame

There is no separate exclusion list. Define drag regions only from the empty space left after tabs, buttons, menus, search boxes, and other interactive controls have been laid out.

## Recommended render pattern

The following code places a tab and New Tab button on the left, then makes the empty space before the native caption-button cluster draggable.

```csharp
public sealed class MainScreen : UiScreen
{
    private const float TitleBarHeight = 48f;

    public override void Render(UiImmediateContext ui)
    {
        var viewport = ui.GetMainViewport();
        var width = viewport.Size.X;

        // The background may span the top, but interactive controls must not overlap caption bounds.
        var drawList = ui.GetBackgroundDrawList();
        drawList.AddRectFilled(
            new UiRect(0f, 0f, width, TitleBarHeight),
            ui.GetColorU32(UiStyleColor.TitleBgActive));

        const float top = 8f;
        const float left = 12f;
        const float tabWidth = 112f;
        const float tabHeight = 32f;
        const float gap = 6f;

        ui.SetCursorScreenPos(new UiVector2(left, top));
        _ = ui.Button("Home##title-tab", new UiVector2(tabWidth, tabHeight));

        var newTabX = left + tabWidth + gap;
        ui.SetCursorScreenPos(new UiVector2(newTabX, top));
        _ = ui.Button("+##new-tab", new UiVector2(36f, tabHeight));

        var dragLeft = newTabX + 36f + 14f;
        if (ui.TryGetCaptionButtonBounds(out var captionButtons))
        {
            var dragRight = captionButtons.X - 8f;
            if (dragRight > dragLeft)
            {
                ui.SetTitleBarDragRegions([
                    new UiRect(dragLeft, 0f, dragRight - dragLeft, TitleBarHeight),
                ]);
            }
            else
            {
                ui.SetTitleBarDragRegions([]);
            }
        }
        else
        {
            // Do not cover native buttons with a guessed region.
            ui.SetTitleBarDragRegions([]);
        }

        DrawApplicationContent(ui, viewport);
    }

    private static void DrawApplicationContent(UiImmediateContext ui, UiViewport viewport)
    {
        const float margin = 24f;
        ui.SetNextWindowPos(new UiVector2(margin, TitleBarHeight + margin));
        ui.SetNextWindowSize(new UiVector2(
            MathF.Max(1f, viewport.Size.X - margin * 2f),
            MathF.Max(1f, viewport.Size.Y - TitleBarHeight - margin * 2f)));
        ui.BeginWindow("Content");
        ui.Text("Application content");
        ui.EndWindow();
    }
}
```

When interactive controls occupy the middle of the title bar, pass several non-interactive gaps instead of one large rectangle.

```csharp
ui.SetTitleBarDragRegions([
    leftEmptyRegion,
    centerEmptyRegion,
    rightEmptyRegion,
]);
```

Prefer non-overlapping drag rectangles. Overlap produces the same drag result but makes the layout intent harder to review.

## Layout rules

### 1. Reserve the complete caption-button cluster

Do not put application buttons, tabs, text input, tooltip triggers, or drag regions inside `captionButtonBounds`. A visual background may continue behind the native buttons, but pointer interaction must remain owned by Windows.

### 2. Derive drag regions from actual control geometry

Use the current frame's final control edge instead of assumptions such as “tabs use 300 px.” Added tabs, expanding search boxes, localized labels, or font scaling must update the drag regions in the same frame.

### 3. Replace or clear regions every frame

In an immediate-mode UI, a stale region can remain over a moved control or at the old position of a removed control. Ensure every render path calls either `SetTitleBarDragRegions(...)` or `SetTitleBarDragRegions([])`.

### 4. Do not convert to physical pixels

Duxel converts native DWM coordinates through the current window DPI into logical coordinates. Applying `GetDpiForWindow` again in application code causes double scaling.

### 5. Minimize assumptions about button placement

The cluster normally appears on the right in Windows, but treat the returned rectangle itself as the reserved area. Complex full-width layouts should compute free spans that do not intersect it.

## Preserved Windows behavior

When application layout follows this contract, the Duxel Windows backend preserves:

| Behavior | Underlying contract |
|---|---|
| Window dragging | Registered regions return `HTCAPTION` |
| Double-click maximize/restore | Native `HTCAPTION` behavior |
| Resize borders | Per-DPI Windows resize hit testing |
| Windows 11 Snap Layout | Maximize button returns `HTMAXBUTTON` |
| Alt+Space system menu | `WS_SYSMENU` and the default window procedure remain active |
| Native Minimize/Maximize/Close | DWM-first hit testing and native window styles |
| DPI transitions | Frame and coordinates refresh on `WM_DPICHANGED` |
| Multi-monitor maximize | Uses the work area of the monitor nearest the window |
| Taskbar protection | Maximized client area is constrained to monitor `rcWork` |

These guarantees can be broken if the app overlaps a registered drag rectangle with an interactive control or places a separate native/overlay window over the caption buttons.

## DPI and multi-monitor checks

Recompute layout across these state changes:

- moving the window between monitors with different DPI values
- switching between restored and maximized states
- changing display scaling
- resizing below the preferred width of the tab strip
- changing Windows theme or DWM composition
- changing taskbar placement or the monitor work area

Do not retain a `TryGetCaptionButtonBounds` result as long-lived state. Use the current frame's result for the current frame's layout.

## Run and diagnose

### Normal local run

Use the repository helper so local Duxel source changes are included:

```powershell
./run-fba.ps1 samples/fba/extended_title_bar_fba.cs -NoCache
```

`dotnet run samples/fba/extended_title_bar_fba.cs` resolves the file's NuGet package reference and therefore does not validate unpublished local source changes.

### NativeAOT diagnostics against a real HWND

The sample contains a diagnostic mode that targets the real Win32 window.

```powershell
./run-fba.ps1 samples/fba/extended_title_bar_fba.cs -NoCache -NoLaunch

$artifactDir = Join-Path (Get-Location) "samples/fba/artifacts/extended_title_bar_fba"
$exePath = Join-Path $artifactDir "extended_title_bar_fba.exe"
$diagnosticPath = Join-Path $artifactDir "diagnostics.txt"
$env:DUXEL_EXTENDED_TITLEBAR_DIAG_OUT = $diagnosticPath

$diagnosticProcess = Start-Process -FilePath $exePath -PassThru
if (-not $diagnosticProcess.WaitForExit(30000))
{
    Stop-Process -Id $diagnosticProcess.Id
    throw "Extended title-bar diagnostic timed out."
}

if ($diagnosticProcess.ExitCode -ne 0)
{
    throw "Diagnostic process failed with exit code $($diagnosticProcess.ExitCode)."
}

Get-Content -LiteralPath $diagnosticPath
if (Select-String -LiteralPath $diagnosticPath -Pattern '^FAIL')
{
    throw "One or more extended title-bar checks failed."
}
```

The current diagnostic sample verifies:

- HWND and caption/system-menu/resize/minimize/maximize styles
- equality between DWM caption-button bounds and the public API
- Minimize `HTMINBUTTON`, Maximize `HTMAXBUTTON`, and Close `HTCLOSE`
- tab `HTCLIENT` and empty drag area `HTCAPTION`
- top-left resize `HTTOPLEFT`
- the `WM_GETMINMAXINFO` work-area contract
- double-click maximize on a drag region
- public caption bounds while maximized
- equality between the maximized client area and monitor work area
- restore after maximize

A framework change is complete only when the diagnostic contains no `FAIL` lines and the full Release build succeeds without warnings or errors.

```powershell
dotnet build Duxel.slnx -c Release --no-restore
```

## Manual validation matrix

Validate the following combinations where available before distributing an application.

| Area | Recommended combinations |
|---|---|
| DPI | 100%, 125%, 150%, 200% |
| Monitors | Primary, secondary, and movement between different DPI values |
| Window states | Restored, maximized, minimized then restored |
| Taskbar | Bottom/side placement and auto-hide setting |
| Input | Mouse, touchpad, Alt+Space, double-click |
| Snap | Hover Maximize, click it, and select a Windows 11 Snap Layout |
| Theme | Light, dark, and live system-theme changes |

The automated diagnostic proves Win32/DWM contracts. It cannot replace visual review of application-specific tab widths, search boxes, menus, or localized text.

## Common mistakes

- setting only `UseDuxelTitleBar = true` and assuming `ExtendedContent` is active
- treating `IntegrateSystemChrome` as the content-extension option
- hardcoding caption-button dimensions
- registering the entire top strip and making tabs or buttons unclickable
- including the caption-button rectangle in a drag region
- multiplying logical coordinates by DPI again
- retaining an old drag region when `TryGetCaptionButtonBounds` returns `false`
- failing to replace regions after window or tab-layout changes
- treating a negative caption-bound `Y` while maximized as invalid
- validating local implementation changes only through NuGet-based `dotnet run`

## Framework contributor contract

When modifying the extended-title-bar path in `Duxel.Platform.Windows`, preserve these invariants:

1. Keep `WS_CAPTION`, `WS_SYSMENU`, `WS_THICKFRAME`, `WS_MINIMIZEBOX`, and `WS_MAXIMIZEBOX`.
2. Give `DwmDefWindowProc` the first opportunity to handle caption hit tests and preserve native caption-button codes.
3. Ensure the Maximize button is identified as `HTMAXBUTTON`.
4. Convert `DWMWA_CAPTION_BUTTON_BOUNDS` from window-relative physical coordinates into client-relative logical coordinates.
5. Resolve resize borders before application drag regions.
6. Use the current monitor's `rcWork` for maximized sizing.
7. Reapply the extended frame on `WM_DPICHANGED` and `WM_DWMCOMPOSITIONCHANGED`.
8. Preserve the `Default`/legacy `UseDuxelTitleBar` resolution contract.
9. Do not regress existing `System` or `Duxel` behavior.
10. Update the public API, sample, this guide, and both `duxel-agent-reference` documents in the same change set.

## Coding AI workflow

A coding agent handling an extended-title-bar request should:

1. read this guide and `samples/fba/extended_title_bar_fba.cs` first
2. distinguish application layout work from platform-backend work
3. use only public APIs for app work rather than duplicating Win32 hit testing
4. query native caption bounds every frame instead of hardcoding them
5. lay out every interactive control first, then register only remaining empty spans
6. call `SetTitleBarDragRegions([])` on unavailable and empty-region paths
7. use `run-fba.ps1` to test local source
8. update paired Korean/English docs and samples when public behavior changes
9. run the full Release build and NativeAOT real-HWND diagnostics
10. report evidence for every requirement and remaining environment limits before declaring completion

Copyable task instruction for an AI agent:

```text
Use Duxel ExtendedContent to render application content from y=0.
Treat docs/extended-title-bar-guide.md and samples/fba/extended_title_bar_fba.cs as authoritative.
Query native caption-button bounds every frame and exclude that rectangle from layout and drag regions.
Keep tabs, buttons, menus, and input controls as HTCLIENT by registering only empty space as drag regions.
Do not hardcode caption widths or DPI.
Before completion, run the Release build and NativeAOT HWND diagnostics and report PASS evidence per requirement.
```

## Completion checklist

- [ ] `TitleBarMode = DuxelTitleBarMode.ExtendedContent` is explicit.
- [ ] Application title-bar background and content render from `(0, 0)`.
- [ ] Caption-button cluster bounds are queried every frame.
- [ ] Caption bounds do not overlap application interaction controls.
- [ ] Drag regions exclude all interactive controls.
- [ ] Drag regions are cleared when bounds are unavailable or no gap remains.
- [ ] Dragging and double-click maximize/restore work in restored/maximized states.
- [ ] All resize borders work.
- [ ] Windows 11 Snap Layout appears for the native Maximize button.
- [ ] Alt+Space opens the system menu.
- [ ] Bounds remain correct after DPI changes and monitor movement.
- [ ] Maximized content does not cover the taskbar work area.
- [ ] Release build finishes with zero warnings and zero errors.
- [ ] NativeAOT diagnostics contain no `FAIL` line.

## Related documentation

- [Duxel Agent Reference](duxel-agent-reference.md)
- [FBA Getting Started](getting-started-fba.md)
- [FBA Reference Guide](fba-reference-guide.md)
- [FBA Sample Catalog](fba-run-samples.md)
- [Korean Extended Title Bar Guide](extended-title-bar-guide.ko.md)
