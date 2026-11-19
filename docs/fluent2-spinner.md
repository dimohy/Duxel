# Fluent 2 Spinner

## Parent allocation — 2026-11-10

Spinner now uses the same pure measurement and allocated rendering path. `IUiSpinnerPresentation.Arrange` receives the natural layout and the actual root size. Fluent centers its unchanged ring and label in that root, preserving diameter, typography and the 8px gap; clipping handles small or zero allocations. This follows the pinned [Fluent root centering and ring flex-shrink rules](https://raw.githubusercontent.com/microsoft/fluentui/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-spinner/library/src/components/Spinner/useSpinnerStyles.styles.ts). It does not claim full browser fidelity.

```csharp
var spinner = Fluent.Spinner("Working", new() { Width = 260 });
var allocated = Dux.SizedBox(spinner, 80, 96);
```

Custom presentations must implement static `Arrange` when their root size changes. The default throws, and an incorrect returned size fails during measurement. Custom controls can use `UiControlContext.MeasureSpinner<TPresentation>` and the explicit `Spinner(..., constrainLayout : true)` overload. Existing immediate overloads retain natural sizing. Delayed content remains absent while its assigned root footprint is preserved; natural delayed height and hidden accessibility semantics are preserved. Measurement does not evaluate or start animation behavior. No new widget fields or arrays were added.

Original source reproduced an 80x96 measured parent with an actual 260x32 root. Current .NET11 RC3 managed validation passes 520 checks: 180 CPU/semantic/scheduling checks and 340 exact pixel comparisons across sizes, label positions, RTL, Light/Dark and MSAA1/4 with three frame slots. There are 617 readbacks including warmup. Live parent changes, acquired-image replays, generated custom presentations, unsupported/wrong Arrange, zero roots and delayed roots are covered. Build warnings/errors are zero. Native remains unverified because C++ Build Tools are absent. Evidence: `artifacts/spinner-allocation-2026-11-10/verification.json`. Whole-goal counts and resize deferral are unchanged; no performance improvement is claimed.


Spinner is a documented **Preview** component on the [official Fluent 2 site](https://fluent2.microsoft.design/components/web/react/core/spinner/usage). Its implementation is pinned to Microsoft Fluent UI commit `8830c10bfb2f4c1bdd540287b09713b9679cd1ad` (MIT). The queried current Spinner styles match that pinned file. Source URLs and hashes are in `artifacts/fluent-spinner-2026-11-06/source-manifest.json`.

## Composition and static implementation selection

`Fluent.Spinner(label, options)` and `Dux.Spinner(label, options)` create the generated, inheritable `UiFluentSpinner`. It derives from `UiSpinner<UiFluentSpinnerBehavior, UiFluentSpinnerPresentation>`. A user widget follows the same inheritance and `[UiWidgetStyle]` source-generation contract. Behavior owns delay/animation lifetime; presentation owns typography, layout and ring composition. No runtime theme registry or reflection selects these implementations.

Retain the instance in Build. Recreating it every frame restarts its delay and animation.

```csharp
protected override IUiView Build(UiBuildContext context)
{
    var spinner = context.Child("connection", () =>
        Fluent.Spinner("Connecting to data …"));
    spinner.Options = new(UiSpinnerSize.Large,
        ReducedMotion: true) { Width = 260 };
    return spinner;
}
```

The immediate API uses caller-owned behavior state:

```csharp
private UiFluentSpinnerBehavior _spinner;
// In Render:
ui.Spinner<UiFluentSpinnerBehavior, UiFluentSpinnerPresentation>(
    "connection", ref _spinner, "Connecting to data …");
```

## Variants and lifetime

Sizes are ExtraTiny16, Tiny20, ExtraSmall24, Small28, Medium32, Large36, ExtraLarge40 and Huge44 logical pixels. Stroke widths are2/3/4. Primary and Inverted appearances use the pinned brandWeb and neutral tokens; Inverted belongs on a strong surface. Labels support Before/After/Above/Below with an8px gap. Small sizes use Body1, medium through ExtraLarge use Subtitle2 and Huge uses Subtitle1. RTL mirrors horizontal order and ring direction.

`Width=0` fills available width, preserving minimum content width; a positive width specifies the root width with the same content minimum. Pure measurement and rendering use the same static presentation. Label-free instances can set `AccessibleName`. Explicit Parent identity uses the label; retain keyed instances or select Instance identity when labels are absent or duplicated.

Delay starts when the instance first renders. Changing a pending delay restarts its deadline; a shown instance stays shown. The natural delayed root has zero height; a parent can still allocate its footprint. The1500ms normal motion follows the pinned two135-degree tails,105-degree mask and EasyEase curve. Reduced motion retains the official1800ms rotation and conic fade. Removing/disposal stops this instance from requesting animation. Pending delay now uses the common [scheduled-frame path](frame-scheduling.md); visible motion continues requesting animation.

The common `UiDrawListBuilder.AddAnnularSector` uses angular vertex color, one-pixel radial coverage ramps and a maximum0.01 logical-pixel chord error. It splits large meshes without reducing precision. This is a general drawing primitive, not a sample-specific optimization. Animated geometry still incurs CPU construction/upload cost; no application FPS/Private/Pi improvement is claimed.

## Accessibility and qualification

The common semantic tree exposes an indeterminate UIA ProgressBar, its label/explicit name and clipped bounds. It has no keyboard focus or fabricated RangeValue. The real gallery's16 examples pass33 external native UIA checks. Events, announcements and full accessibility qualification remain open.

NET10 full3,140 and focused GPU1,312 pass. Fixed NET11 RC3 NativeAOT EXT2/core3 each pass1,312. All1,137 generated images match NET10 in each Native mode. Those checks cover eight sizes, appearances, positions, schemes, MSAA1/4, raster/vector text and RTL/reduced-motion phases, plus independent interior color/hole samples. They compare Duxel paths and native runtimes, **not official browser pixels**. Full official fidelity, forced colors, arbitrary slot replacement and remaining accessibility work are unqualified. The original full goal remains active.

Run the gallery with:

```powershell
./run-fba.ps1 samples/fba/widget_tree_theme_fba.cs -NoCache -DotnetPath 'C:/Users/dimohy/.cache/duxel-dotnet-10.0.301/dotnet.exe' -ExtraArgs @('--spinner')
```

## Pause — 2026-11-11

`UiSpinnerOptions.Paused` defaults to `false`. When true it freezes the current phase and stops continuous frame requests. Resuming excludes paused time from the animation clock. `ReducedMotion` retains slower rotation and is independent of pause. Delayed reveal still schedules its one-shot deadline while paused.

```csharp
var spinner = Fluent.Spinner("Waiting", new() { Paused = true });
spinner.Options = spinner.Options with { Paused = false };
```

The observer starts paused and provides an explicit playback toggle. Fixed determinate progress does not cause continuous rendering. Evidence: Spinner, actual Fluent page and scheduling tests under `artifacts/observer-idle-panel-2026-11-11/`. This change does not qualify NativeAOT or claim measured throughput gains.

2026-11-12 follow-up — [Content measurement contract](widget-cpu-retention.md): deterministic sources/presentations participate in parent retention; dynamic string/badge getters and custom traits default to fresh measurement. Declared getter Progress geometry and stable Spinner visibility are separate promises; rendering/getters/animation/navigation continue. Focused26 and exact GPU comparisons qualify this scope, not full NativeAOT/performance. Evidence: artifacts/content-layout-retention-2026-11-12/verification.json.
