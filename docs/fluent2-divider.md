# Fluent 2 Divider

2026-11-05. Follow the [official-site scope](fluent2-scope.md). Divider belongs to the existing55 top-level names; it does not expand the goal.

The official [usage guidance](https://fluent2.microsoft.design/components/web/react/core/divider/usage) and Divider types/styles from Microsoft Fluent UI `8830c10bfb2f4c1bdd540287b09713b9679cd1ad` are pinned in `artifacts/fluent-divider-2026-11-05/source-manifest.json`.

## API and implementation

```csharp
using var plain = Fluent.Divider();
using var caption = Fluent.Divider("Section", new(
    Alignment : UiDividerAlignment.Start,
    Appearance : UiDividerAppearance.Brand,
    Inset : true));
using var vertical = Fluent.Divider("Details", new(Vertical : true, Length : 120));
using var live = Fluent.Divider(() => currentCaption);
caption.Text = "Updated";
```

Supports horizontal/vertical orientation, start/center/end alignment, default/subtle/strong/brand appearances, inset, childless lines and actual Regular12px type with16px line boxes. Content spacing/inset is12px; short end segments are8px. Vertical minimum height is20px without content and84px with content. Length specifies layout length;0 uses available horizontal width or vertical minimum height. Horizontal RTL reverses logical start/end. Multiline content wraps without truncation. Strokes respect table-cell and ancestor clips.

`UiDivider<TSource,TSourcePolicy,TPresentation>` is the shared default/user base. Content sourcing and presentation are statically separated. Generated `UiFluentDivider`/`UiFluentDynamicDivider`, `Fluent.Divider`, `Dux.Divider` and `ui.Divider` consume the same authoritative presentation. Subclasses extend ReadText; IUiDividerPresentation can replace composition/measurement/painting. Source/Options publish changes before waking an idle frame; Dispose releases the source.

Legacy Separator implementations/APIs remain preserved; complete exclusion of all legacy discovery paths remains unfinished. `UiTheme.FluentColorScheme` selects shared color data; BadgeColorScheme is a compatibility alias over the same value. Source generation fixes widget implementation types through closed generics without a runtime theme registry. Color-scheme changes invalidate static layers.

## Validation and limits

.NET10 Divider43 and full1170 checks passed. Pinned .NET11 RC3 NativeAOT EXT/core with2 frame resources passed317 each. All30 managed Divider images match both Native paths byte-for-byte (60 comparisons), covering MSAA1/4, light/dark, orientation/alignment/appearance. All179 previous Badge qualification images match each corresponding Native path (358 comparisons).

Initial comparisons exposed an omitted root-MSAA argument and fixtures restoring a previous frame's MSAA instead of the current requested setting. A test-only observation now preserves the actual requested setting; comparisons were repeated under matching conditions. Production Vulkan behavior was not changed to force a match. One existing managed Badge allocation assertion failed; an instrumented pass does not establish its cause as resolved.

Official browser visual parity, actual Windows UIA, forced colors and a standard arbitrary-child content slot remain unfinished. Custom presentation replacement is not a substitute for that slot. No new whole-app timing/Private/Pi improvement is claimed. The explicit static-layer ContentVersion contract remains. Execution records, source/binary hashes and unresolved issues: `artifacts/fluent-divider-2026-11-05/verification.json`.

Use `--divider` in the [run guide](widget-gallery-run.md). Startup/first-frame/clean auto-exit are separate from manual visual/input acceptance.

## Measurement and parent allocation — 2026-11-10

`UiDivider` now implements pure `MeasureContent` through `UiControlContext.MeasureDivider<TPresentation>`. `IUiDividerPresentation.Measure` defaults to an explicit unsupported exception. Custom presentations implement it and opt into `CanArrange` to accept changed allocations. An opted-in render must place exactly one common `PlaceText` root at the assigned bounds and clip its paint to that root. The shared wrapper checks the root and restores table/column routing after success or failure. `MeasureFluentDivider` exposes the built-in geometry without placing or painting it.

```csharp
using var divider = Fluent.Divider("Section", new(Length : 220));
using var box = Dux.SizedBox(divider, 80, 64);
// The actual root is 80 x 64. Length describes the natural layout.
```

The real parent width controls horizontal caption wrapping. Both axes control line extent, cross-axis centering and root clipping, including short and zero roots. Typography remains Regular 12px with 16px line boxes. The existing immediate overload and standalone widgets preserve natural sizing; immediate callers opt in with `ui.Divider<TPresentation>(text, options, constrainLayout : true)`. Source providers remain read-only and are read once per explicit measurement/render phase; parent layout can invoke separate measurement phases. Measurement neither paints nor processes input. No widget/context fields or caches were added; the existing common text placement scope is reused.

The original failure was reproduced: SizedBox measurement/rendering threw, while standalone geometry was 220 x 16. Fixed .NET11 RC3 managed validation passes 525 checks: 64 CPU/lifetime checks, 19 existing GPU checks, 400 allocation pixel comparisons, 40 paced retained-frame comparisons and 2 reuse budgets. Coverage includes raster/vector, MSAA 1/4, Light/Dark, three frame slots, alignment, RTL, inset, empty/multiline/literal captions, short/zero bounds and generated custom presentations. All 911 readbacks, including warmup, are retained under `artifacts/divider-allocation-2026-11-10/` with logs, source hashes and `verification.json`.

The new local `layout_divider_allocation_fba.cs` sample offers Width : 100, Height : 120, Orientation and Short/Empty/Reset controls. Local-reference compilation and first presentation/15-second exit are checked separately from manual interaction. Native validation for this revision remains unverified because standard VS C++ tool discovery is absent; the earlier Native evidence above applies to the earlier revision. No FPS, memory or allocation improvement is claimed. Whole-goal counts and the resize deferral are unchanged.


## Parent measurement retention — 2026-11-12

Static text sources and deterministic Divider presentations participate in existing parent measurement retention. Getter sources and unknown presentations default to dynamic measurement. Source/options changes invalidate the owning parent; actual rendering still runs each frame. A derived class that introduces mutable measurement state must opt out or invalidate with RequestRebuild.

Evidence: artifacts/form-layout-retention-2026-11-12/verification.json. Observer: ./run-gallery.ps1 → Measurement reuse.
