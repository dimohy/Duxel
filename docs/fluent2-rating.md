# Fluent Rating — interactive scores

`Fluent.Rating` shares a `UiBinding<float>` score. Defaults are maximum5,step1,size28. Set `Step = .5f` for half steps. Zero means unselected. External values remain unchanged and display rounded to the nearest half.

```csharp
var score = new UiState<float>(2.5f);
var rating = Fluent.Rating("Service rating", score.Binding,
    new UiRatingOptions { Step = .5f });
```

`UiRatingOptions` provides `Maximum`, `Step`, `Size` and `RightToLeft`. Sizes are Small12/Medium16/Large20/ExtraLarge28. Values must be finite within0..Maximum; steps are1 or0.5. Invalid options fail before replacing current options. Insufficient parent width wraps fixed-size icons onto subsequent lines. Common inherited Margin,Transform and clipping remain available.

Hover previews without writing the binding. Press/release within the same segment selects once. Arrow keys cycle by the selected step; RTL reverses horizontal direction. Tab leaves the group. The common disabled scope rejects pointer and accessibility selection.

## Shared implementation and accessibility

`UiRating<TBehavior, TPresentation>` inherits `UiControlWidget` with separate static behavior/presentation contracts. Generated `UiFluentRating` and `Fluent.Rating` bind concrete implementations directly. Numeric identities are prepared once when options are configured; selection maps those identities to scores without per-frame numeric parsing.

Rating and RadioGroup share radio input, focus and accessibility publication. Rating permits the empty zero state in a single-selection group; each score/half-step is a RadioButton with SelectionItem support. The group owns one core Tab stop while individual items accept accessibility Focus/Select requests. Input-only steps retain behavior/layout and generate presentation only on the final step. Stars use existing compiled regular/filled Fluent font glyphs with common half clipping.

## Running and verification

When package assets already match the current project dependencies, run from the repository root:

```powershell
./run-gallery.ps1 -NoRestore
```

Open **Fluent controls → Input and selection → Rating** to try shared whole- and half-point values, reset, RTL, and wrapping at a width of 56. `-NoRestore` explicitly reuses existing NuGet assets. Missing assets fail without switching to an implicit restore. If dependencies changed, restore them before using this mode. When restoration is required,prepare a writable cache and use `./run-gallery.ps1`. Global L: cache exhaustion remains a separate environment issue.

Managed .NET11 passes14 input checks and32 GPU pairs,Radio accessibility18,compatibility91,composite contracts500,ordered input595 and Observer356. GPU references independently position glyphs for0/.5/2.5/5,LTR/RTL,width140/56 and slots1/3. They do not establish browser SVG pixel equivalence. The normal Observer acknowledges45 QueuePresent pages including the rating page; Linux is a host build only.

## Official reference and remaining scope

Reviewed [official Rating guidance](https://fluent2.microsoft.design/components/web/react/core/rating/usage) and the [pinned React implementation](https://github.com/microsoft/fluentui/tree/64f3279c84b67f800d383244b2d73782c9913843/packages/react-components/react-rating/library/src). The interactive implementation selects extra-large28 despite the guidance's general layout wording; this implementation's12/16/20/28 size scale is used.

Forced colors,browser pixel comparison and platform-service acceptance remain open. Catalog connection is not complete Rating conformance. [Interactive evidence and source hashes](../artifacts/fluent-rating-2026-11-16/verification.json).

## Localized interactive item names

Default option names use numeric formatting from `CultureInfo.CurrentCulture`. Set `UiRatingOptions.ItemLabel` for application wording exposed to screen readers.

```csharp
var rating = Fluent.Rating("Service rating", score.Binding, new UiRatingOptions
{
    Step = .5f,
    ItemLabel = static value => $"{value:0.#} out of 5"
});
```

Input identities remain culture-independent. Relabeling preserves selection,accessibility runtime identities and the pressed option. Names are prepared on creation and when the culture instance or formatter changes. Call `rating.RefreshItemLabels()` when only external translation data changes. Formatters must be pure and return nonempty,non-whitespace text. Failures never publish partially replaced labels. Disposal releases captured formatter references.

Observer **Rating** includes Korean half-step names. The official browser example confirms a committed score4 survives hover preview2,right-arrow selects5,and default icons occupy28 units. Forced-colors browser captures establish a reference only; engine acceptance remains open.

The official browser SVG uses a20-unit viewBox. Rating therefore scales the20-unit glyph instead of switching to a24-unit glyph. Icons originate from compiled Fluent fonts and require `IPlatformVectorTextBackend`. The shared path cancels interior mesh edges once and renders outline coverage through the same GPU vector-fill engine as SVG. Ordinary raster text remains unchanged.

Coverage images align to the em-canvas pixel grid rather than the native paragraph baseline,avoiding double filtering. Equal outlines,sizes and cameras reuse GPU results. SVG and glyphs share visible-window/camera calculations instead of allocating the full virtual image. Glyph entry capacity follows the existing text-cache limit while retaining the64MiB per-cache pixel budget.

Checks cover neutral fractional edges at MSAA1 and exact warm-cache pixel stability. In a fixed150×40 browser-reference region,differing pixels decrease from383 for opaque triangles to137,but maximum channel error107 remains. This is not exact pixel equivalence. [Comparison and verification](../artifacts/fluent-rating-2026-11-16/coverage-verification.json).

## Retained native curves

Font icons retain the original DirectWrite or HarfBuzz line/cubic contours in `PlatformTextGlyphMesh.Outline`. `PlatformTextGlyphOutline` records the fill rule; `UiVectorCurve` records each line or cubic segment. Providers close every contour and keep the buffers immutable. Existing triangle-only providers remain an explicit supported representation. SVG and glyph compilation share cubic monotone-interval math and the GPU fill renderer.

Zoom and rotation reuse the prepared native outline and GPU scene. The Windows fixture verifies two native mesh requests initially, no extra mesh requests during four transforms, and zero CPU glyph raster requests. This is a work-count result, not an FPS measurement. Ordinary text rendering remains unchanged.

The independent font-outline browser reference first materializes transparent RGBA8, then composites on RGB41. Its fixed 150×40 region matches the Windows GPU output exactly. This additional baseline does **not** replace the original official SVG rendered directly on an opaque background: that comparison still differs at 125 pixels (maximum channel error54). Font geometry and intermediate RGBA8 quantization explain different comparison scopes; no background-specific alpha correction or relaxed pixel tolerance is applied. [Current evidence](../artifacts/fluent-rating-2026-11-16/curves-verification.json).

## Read-only rating display

`Fluent.RatingDisplay` exposes one named accessibility Image without input,Tab stops or selectable children. Its default size is Medium16.

```csharp
var summary = Fluent.RatingDisplay("Average rating", new UiRatingDisplayValue(3.5f, 1234));
var compact = Fluent.RatingDisplay("Average rating", () => new UiRatingDisplayValue(score.Value, 1234),
    new UiRatingDisplayOptions { Compact = true });
```

`Compact` shows one filled icon alongside the rating and review count. Full display uses half values and filled background glyphs for the unselected portion. Both widgets support `Shape` (Star/Circle/Square) and `Color` (Neutral/Brand/Marigold) through shared icon painting. `ShowValue`/`ShowCount` hide visual text while preserving values in the accessible name. Override `ValueText`/`CountText`/`AccessibleLabel` for explicit text.

Numbers use the thread's `CultureInfo.CurrentCulture`. Replacing its instance changes the shared measurement key and recomputes parent geometry,including custom numeric formats with equal culture names. Replace the instance instead of mutating a culture already in use. Static values retain measurements; getter sources read current values. Scores must be finite within 0..Maximum,counts nonnegative,and display Maximum at least 2.

Observer **Rating** includes full/compact displays,review counts,shapes,colors and RTL. GPU glyph comparisons are separate from browser rendering equivalence.

Managed validation passes 132 Rating checks (previous 46,display CPU 14 and GPU 72 pairs),110 retention checks,500 composite contracts,356 Observer checks and595 ordered-input checks. Display GPU coverage uses the current default color scheme across three shapes,three colors,full/compact,LTR/RTL and slots1/3. It does not establish visual conformance for every theme or text size. [Display evidence](../artifacts/fluent-rating-2026-11-16/display-verification.json).

Subsequent validation passes219 Rating checks including localization,vector output and light/dark themes,17 standalone-icon checks,Radio accessibility18/compatibility91 and Observer356. Display GPU comparisons now cover144 pairs. The normal Observer acknowledges45 page submissions; Linux is a host build only. [Browser,naming and vector evidence with remaining defects](../artifacts/fluent-rating-2026-11-16/labels-vector-verification.json).

The later external `atspi-gallery-rating` probe passed three checks on each Linux x64 WSLg runtime (.NET 11.0.0 managed and the previously published NativeAOT build). It selected 3.5 through the native Selection interface, confirmed that compact RatingDisplay exposed the value and review count as a single nonfocusable image, and confirmed that navigation preserved the selection. See `W:/Cache/Duxel/gallery-source-20261117/rating-gallery-atspi-2034/verification.json`.

Eight checks in the normal Windows Gallery used external UI Automation and mouse input. Hovering over 3.5 kept the selected score at 2.5. Clicking committed 3.5 and updated both displays. UI Automation exposed each display as one nonfocusable image named with the score and review count, and both names updated to 1,235 after the review-count action. Live captures showed five star positions in the full display and one star in the compact display, with the same score and count. The `rating-only` suite, which also includes forced-color unit checks, passed 1,387 checks on each of Windows .NET 10.0.15 and .NET 11.0.0. See `L:/Cache/Duxel/fluent-rating-live-20261120/verification.json`. Browser pixel comparison and the Windows system forced-colors mode remain open.
