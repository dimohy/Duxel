# Duxel Extended Title Bar Guide

> Last synced: 2026-07-20
>
> Audience: Duxel application developers, framework contributors, and coding AI agents
>
> Platform: Windows 11 / Windows-native backend
>
> Korean: [extended-title-bar-guide.ko.md](extended-title-bar-guide.ko.md)

## Purpose

`DuxelTitleBarMode.ExtendedContent` lets an application render tabs, search boxes, menus, and other UI from the top of the window. Duxel keeps the Minimize, Maximize/Restore, and Close glyphs visible over the Vulkan surface while Windows continues to own their native caption behavior.

This guide covers:

- enabling the extended title bar in an application
- laying out content without overlapping the caption-button cluster
- separating draggable space from interactive controls
- coordinate rules for DPI, multiple monitors, and maximized windows
- validation required before developers or coding agents report completion
- Win32/DWM contracts that framework contributors must preserve

The complete executable example is [`samples/fba/extended_title_bar_fba.cs`](../samples/fba/extended_title_bar_fba.cs).

## Library support versus sample code

This feature is implemented by the Duxel libraries. The sample does not implement a private title-bar backend; it demonstrates how application code composes UI with the public library contract.

These APIs are available in the published `0.2.9-preview` or later packages. Use a project/source reference or `run-fba.ps1` only when validating newer unpublished local changes.

| Concern | Provided by the Duxel library | Application or sample responsibility |
|---|---|---|
| Duxel-owned title bar | In `Duxel` mode the runtime draws the effective icon, left-aligned title, caption buttons, system-menu path, and Maximize/Restore glyph from current window state | Select `Duxel`; do not duplicate or synchronize caption UI in the application screen |
| Extended client area | `DuxelTitleBarMode.ExtendedContent` and the app runtime expose content from `(0, 0)` | Select the mode explicitly |
| Effective window icon | The Windows backend resolves `IconData`, `IconPath`, or the Windows default icon; `TryGetWindowIcon` exposes the resolved texture | Choose its size and position in the application-owned title bar |
| Extended caption buttons | `Duxel.App` uses the same shared 48-pixel Duxel renderer as `Duxel` mode, while `Duxel.Platform.Windows` preserves native hit codes, commands, hover state, and Snap Layout behavior | Reserve the rectangle returned by `TryGetCaptionButtonBounds`; do not draw replacement buttons there |
| Caption-button bounds | The backend returns a stable logical cluster anchored at `(clientWidth - 48 * buttonCount, 0)` with `DuxelTitleBarHeight`; raw DWM `Y`/height never controls visual placement | Query the current value every frame and use it for layout |
| Window dragging | The backend converts registered rectangles into native `HTCAPTION` hit tests | Register only non-interactive empty regions with `SetTitleBarDragRegions` |
| DPI, resize, maximize, taskbar work area | The Windows backend owns the Win32/DWM behavior | Recompute application layout from current logical coordinates |
| Tabs, search, menus, title text | No fixed Duxel layout is imposed in `ExtendedContent` | Draw and arrange the desired application UI |

The `ExtendedTitleBarScreen` class in the sample is reusable application-side layout guidance. The `ExtendedTitleBarDiagnostics` class and its Win32 P/Invoke declarations are test instrumentation only. Production applications should use the public APIs above and must not copy the diagnostic hit-testing implementation.

## Choose a title-bar mode

| Mode | Title-bar content | Caption buttons | Application content origin |
|---|---|---|---|
| `Default` | Resolved from `UseDuxelTitleBar` | Follows the resolved mode | Follows the resolved mode |
| `System` | Windows | Windows | Native client area |
| `Duxel` | Duxel | Duxel | Below `DuxelTitleBarHeight` |
| `ExtendedContent` | Application | Duxel visuals over Windows/DWM behavior | `(0, 0)` |

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

## Duxel-owned title bar quick start

Use `Duxel` when the application needs conventional chrome and does not need tabs or other controls at `y = 0`:

```csharp
Window = new DuxelWindowOptions
{
    Title = "Duxel App",
    TitleBarMode = DuxelTitleBarMode.Duxel,
};
```

The library draws the resolved window icon, left-aligned title, Minimize, Maximize/Restore, and Close buttons. It also owns dragging, title-bar double-click maximize/restore, right-click system menu, Alt+Space, and resize behavior. Application code must not draw replacement caption buttons. The Maximize glyph is one square while restored and automatically becomes the overlapping-squares Restore glyph while maximized.

## ExtendedContent developer quick start

Reference `Duxel.Windows.App`, select `ExtendedContent`, and provide an ordinary `UiScreen`:

```csharp
using Duxel.App;
using Duxel.Core;
using Duxel.Windows.App;

DuxelWindowsApp.Run(new DuxelAppOptions
{
    Window = new DuxelWindowOptions
    {
        Title = "Tabbed Duxel App",
        Width = 1100,
        Height = 700,
        TitleBarMode = DuxelTitleBarMode.ExtendedContent,
        IntegrateSystemChrome = true,
    },
    Screen = new MainScreen(),
});
```

Inside `MainScreen.Render`, perform these steps every frame:

1. Draw the title-bar background from `y = 0`.
2. Obtain the already resolved taskbar/window icon with `TryGetWindowIcon` and draw it if the layout includes an icon.
3. Lay out tabs, search, menus, and other interactive controls first.
4. Query `TryGetCaptionButtonBounds` and keep every application control outside that rectangle.
5. Register only the remaining non-interactive spans with `SetTitleBarDragRegions`.
6. Do not draw Minimize, Maximize, or Close replacements; the Duxel library owns their visuals and Windows owns their behavior.

The complete version of this pattern is the [`extended_title_bar_fba.cs`](../samples/fba/extended_title_bar_fba.cs) sample. Copy or adapt `ExtendedTitleBarScreen`; keep `ExtendedTitleBarDiagnostics` only in tests.

## Public API contract

### `TryGetWindowIcon`

```csharp
if (ui.TryGetWindowIcon(out var windowIcon))
{
    windowIcon.Prepare(ui, UiImageEffects.Default);
    // Draw windowIcon.TextureId in the application-owned title bar.
}
```

This returns the effective icon already assigned to the native window. Use it when the extended title bar includes an app icon so the title-bar, taskbar, and Alt+Tab representations remain consistent. The texture is prepared and drawn like any other `UiImageTexture`.

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

The following code places the effective window icon, a tab, and a New Tab button on the left, then makes the empty space before the native caption-button cluster draggable.

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
        const float left = 48f;
        const float tabWidth = 112f;
        const float tabHeight = 32f;
        const float gap = 6f;

        if (ui.TryGetWindowIcon(out var windowIcon))
        {
            windowIcon.Prepare(ui, UiImageEffects.Default);
            drawList.AddImage(
                windowIcon.TextureId,
                new UiVector2(12f, 12f),
                new UiVector2(36f, 36f),
                new UiVector2(0f, 0f),
                new UiVector2(1f, 1f),
                new UiColor(0xFFFFFFFF));
        }

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
                    new UiRect(0f, 0f, 48f, TitleBarHeight),
                    new UiRect(dragLeft, 0f, dragRight - dragLeft, TitleBarHeight),
                ]);
            }
            else
            {
                ui.SetTitleBarDragRegions([new UiRect(0f, 0f, 48f, TitleBarHeight)]);
            }
        }
        else
        {
            // Do not cover native buttons with a guessed region.
            ui.SetTitleBarDragRegions([new UiRect(0f, 0f, 48f, TitleBarHeight)]);
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

Do not put application buttons, tabs, text input, tooltip triggers, or drag regions inside `captionButtonBounds`. A visual background may continue behind the button cluster, but pointer interaction must remain owned by Windows. Duxel automatically draws the caption glyph overlay in this rectangle; application code must not draw another set.

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
| Minimize/Maximize/Close | Duxel-rendered glyph overlay plus DWM-first hit testing and native window styles |
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

- availability of the effective window icon through `TryGetWindowIcon`
- HWND and caption/system-menu/resize/minimize/maximize styles
- availability of the native DWM caption metadata and equality between the public API and Duxel's stable `Y = 0`, 48-pixel logical cluster
- Minimize `HTMINBUTTON`, Maximize `HTMAXBUTTON`, and Close `HTCLOSE`
- `HTMAXBUTTON` across the full Duxel caption-button height in restored and maximized states
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
| Duxel chrome glyph | One-square Maximize while restored; overlapping-squares Restore while maximized; one-square Maximize again after restore |
| Taskbar | Bottom/side placement and auto-hide setting |
| Input | Mouse, touchpad, Alt+Space, double-click |
| Snap | Hover Maximize, click it, and select a Windows 11 Snap Layout |
| Theme | Light, dark, and live system-theme changes |

The automated diagnostic proves Win32/DWM contracts. It does not prove that pixels are visible on an opaque Vulkan surface. Visual review must confirm all three caption glyphs, hover/pressed feedback, Maximize/Restore switching, the icon right-click system menu, and the intended tab styling in addition to application-specific layout.

## Common mistakes

- setting only `UseDuxelTitleBar = true` and assuming `ExtendedContent` is active
- drawing application caption buttons in `Duxel` mode or manually caching the Maximize/Restore glyph instead of letting library state drive it
- treating `IntegrateSystemChrome` as the content-extension option
- hardcoding caption-button dimensions
- registering the entire top strip and making tabs or buttons unclickable
- including the caption-button rectangle in a drag region
- multiplying logical coordinates by DPI again
- retaining an old drag region when `TryGetCaptionButtonBounds` returns `false`
- failing to replace regions after window or tab-layout changes
- treating a negative caption-bound `Y` while maximized as invalid
- validating local implementation changes only through NuGet-based `dotnet run`
- treating successful `HTMINBUTTON`/`HTMAXBUTTON`/`HTCLOSE` diagnostics as proof that caption-button pixels are visible

## Framework contributor contract

When modifying the Windows title-bar paths in `Duxel.App` or `Duxel.Platform.Windows`, preserve these invariants:

1. Keep `WS_CAPTION`, `WS_SYSMENU`, `WS_THICKFRAME`, `WS_MINIMIZEBOX`, and `WS_MAXIMIZEBOX`.
2. Resolve the Duxel caption cluster in `WM_NCHITTEST` before `DwmDefWindowProc`, return native caption-button codes, and continue using DWM for the remaining non-client behavior.
3. Ensure the Maximize button is identified as `HTMAXBUTTON`.
4. Keep public caption bounds anchored at logical `Y = 0`, use `DuxelTitleBarHeight`, and never copy maximized DWM negative `Y` or native 30-pixel height into Duxel visual placement.
5. Resolve resize borders before application drag regions.
6. Use the current monitor's `rcWork` for maximized sizing.
7. Reapply the extended frame on `WM_DPICHANGED` and `WM_DWMCOMPOSITIONCHANGED`.
8. Preserve the `Default`/legacy `UseDuxelTitleBar` resolution contract.
9. Use `DuxelCaptionButtonRenderer` in both `Duxel` and `ExtendedContent`, including the same position, stroke, and state-aware Maximize/Restore glyph.
10. In `ExtendedContent`, draw from the stable public Duxel bounds and non-client hover/pressed state; keep click commands, `HTMAXBUTTON`, and Snap Layout in the Windows path.
11. Route `WM_NCRBUTTONUP` on registered `HTCAPTION`/`HTSYSMENU` regions to the native system menu so an application-drawn window icon retains standard title-bar behavior.
12. Do not regress existing `System` or `Duxel` behavior.
13. Update the public API, sample, this guide, and both `duxel-agent-reference` documents in the same change set.

## Coding AI workflow

A coding agent handling an extended-title-bar request should:

1. read this guide and `samples/fba/extended_title_bar_fba.cs` first
2. choose `Duxel` for library-owned conventional chrome and `ExtendedContent` only for application UI at `y = 0`
3. distinguish application layout work from platform-backend work
4. use only public APIs for app work rather than duplicating Win32 hit testing
5. query the public Duxel caption bounds every frame instead of reading raw DWM coordinates or hardcoding them
6. lay out every interactive control first, then register only remaining empty spans
7. remove stale calculated gaps on unavailable and empty-region paths; independently valid regions such as a fixed left icon area may remain
8. use `run-fba.ps1` to test local source
9. visually verify the `ExtendedContent` Minimize/Maximize/Close glyphs, hover feedback, icon right-click system menu, and tab styling; hit-test diagnostics alone are insufficient
10. verify the `Duxel` and `ExtendedContent` Maximize glyph changes to Restore while maximized and changes back after restore
11. update paired Korean/English docs and samples when public behavior changes
12. run the full Release build and NativeAOT real-HWND diagnostics
12. report evidence for every requirement and remaining environment limits before declaring completion

Copyable task instruction for an AI agent:

```text
Choose Duxel mode for conventional library-owned chrome, or ExtendedContent when application content must render from y=0.
Treat docs/extended-title-bar-guide.md and samples/fba/extended_title_bar_fba.cs as authoritative.
In Duxel mode, do not recreate library-owned caption buttons or cache their Maximize/Restore state in application code.
Query Duxel caption-button bounds every frame and exclude that rectangle from layout and drag regions.
Keep tabs, buttons, menus, and input controls as HTCLIENT by registering only empty space as drag regions.
Do not hardcode caption widths or DPI.
Before completion, run the Release build and NativeAOT HWND diagnostics and report PASS evidence per requirement.
```

## Completion checklist

- [ ] If application content must begin at `y = 0`, `TitleBarMode = DuxelTitleBarMode.ExtendedContent` is explicit.
- [ ] `Duxel` and `ExtendedContent` use the same caption-button renderer; Maximize changes to overlapping-squares Restore while maximized and changes back after restore.
- [ ] Application title-bar background and content render from `(0, 0)`.
- [ ] Caption-button cluster bounds are queried every frame.
- [ ] Caption bounds do not overlap application interaction controls.
- [ ] Drag regions exclude all interactive controls.
- [ ] Stale calculated drag gaps are removed when bounds are unavailable or no gap remains; only independently valid regions remain.
- [ ] Dragging and double-click maximize/restore work in restored/maximized states.
- [ ] All resize borders work.
- [ ] Windows 11 Snap Layout appears for the Duxel-drawn Maximize button through `HTMAXBUTTON`.
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
