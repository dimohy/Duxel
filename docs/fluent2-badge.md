# Fluent 2 Badge and CounterBadge

Last synced: 2026-11-10

Badge/CounterBadge use the common inherited widget model and source generation. Full official visual parity,Windows UIA,RTL and forced colors remain open. Neither complete component conformance nor full Fluent coverage is claimed.

## Official basis

Current Microsoft Fluent UI master was checked and matches the existing pin `8830c10bfb2f4c1bdd540287b09713b9679cd1ad`. Sources include [official Badge guidance](https://fluent2.microsoft.design/components/web/react/core/badge/usage/), pinned Badge/CounterBadge types,behavior,styles,actual brandWeb and light/dark color tokens. URLs/SHA256 are retained in `artifacts/fluent-badge-2026-11-05/source-manifest.json`. Guidance labels Badge Preview while the React entrypoint exports it. Windows InfoBadge remains a separate requirement. Actual brandWeb values are used instead of obsolete comments in alias files.

## Usage and static binding

```csharp
using var ready = Fluent.Badge("Ready", new(Shape: UiBadgeShape.Rounded));
using var count = Fluent.CounterBadge(108); //99+
using var live = Fluent.CounterBadge(() => new UiCounterBadgeValue(unread));
using var dot = Fluent.CounterBadge(0, dot: true);
ui.Badge("Ready");
ui.CounterBadge(new UiCounterBadgeValue(unread), ref counterTextCache);
```

`UiBadge<TSource,TBehavior,TPresentation>` is a real UiControlWidget implementation. `IUiBadgeBehavior<TSource,TSelf>` owns data reading,counter/visibility rules and allowed variants; `IUiBadgePresentation` owns composition,layout and painting. Default/user-derived widgets share this path. User presentations can replace the whole layout and drawing algorithm. `IUiBadgeIcon` is also bound through a closed generic presentation,without runtime implementation registries or per-frame delegates.

```csharp
[UiWidgetStyle(typeof(UiBadge<,,>), typeof(string), typeof(UiTextBadgeBehavior),
    typeof(UiFluentBadgePresentation<MyVectorIcon>))]
public partial class MyIconBadge;
```

Generated UiFluentBadge directly inherits closed UiBadge<string,UiTextBadgeBehavior,UiFluentBadgePresentation>; generated Fluent.Badge directly constructs it. Actual generated code is retained under `generated`. Default Dux.Badge(text) and DuxelView.Display.Badge(text) now use this path. The original UiBadgeOptions overloads retain their implementation/signature and have Legacy/EditorBrowsable(Never) metadata. Explicit default/old-options arguments retain their previous path. Use Fluent.Badge(text,new UiFluentBadgeOptions(...)) for standard variants. Complete legacy exposure policy remains separate work.

## Presentation,behavior,lifetime

Badge supports six sizes,three shapes,four appearances and eight colors. Heights: tiny6,extra-small10,small16,medium20,large24,extra-large32. Actual Segoe UI Semibold4/6/10/12px and common DirectText/vector paths are used. Icons can precede/follow text or stand alone; icon-only content requires AccessibleLabel. Passive badges do not register keyboard tab stops. Labels have no automatic maximum width.

CounterBadge permits circular/rounded,filled/ghost and brand/danger/important/informative,matching its narrower contract. Zero is hidden unless showZero; values above the default99 overflow to99+. Dot remains visible at zero and is6px. Explicit nonempty content takes precedence. Counts use C# int; complete arbitrary fractional JavaScript-number equivalence is not claimed. Negative values and explicit overflow0 are preserved. Each widget retains one bounded numeric string and does not reformat unchanged counts each frame. Disposal releases the memo,source delegate and accessible label.

UiTheme.BadgeColorScheme selects only Light/Dark color data,not runtime widget implementations. Built-in designs explicitly select it. Metadata changes invalidate static layers even when all previous color fields remain unchanged. All64 appearance/color combinations are in expected-colors.json.

## Validation and remaining scope

.NET10 focused49 and final full1095 checks passed. GPU tests cover32 appearance/color combinations under Light/Dark and MSAA1/4,checking actual interior/transparent pixels and exact immediate/generated/inherited/retained equivalence. Count changes,overflow,dot and hiding previously visible content are checked on the GPU. See verification.json for pinned RC3 NativeAOT results and image comparisons. API/layout checks are distinct from matching official browser/WinUI screenshots.

AccessibleLabel/count descriptions currently provide a data contract,not completed Windows UIA integration. Official visual/icon comparison,RTL,forced colors and full cross-platform differences remain open. Prior DirectText benchmarks are not reinterpreted as current whole-app performance after adding Badge. Accepted resize behavior and the unexplained historical Native input timeout remain recorded.

The gallery is samples/fba/widget_tree_theme_fba.cs --badge. See [local execution](widget-gallery-run.md).


Behavior state is part of each closed generic type. Static-text behavior is an empty value type without a counter memo. Only counter behavior retains numeric text; changing counts still reuse the same99+ string while its displayed value is unchanged. Dot/vector-icon content without text requires no font measurement or text backend. No new object-size or whole-app memory percentage is claimed.


Final pinned RC3 NativeAOT passed243 checks each under EXT/core presentation lifetime. The179 pre-storage-change managed images match both Native configurations;22 current managed Badge images also match both.157 earlier common GPU images remain identical. All compare full320x240 PPM bytes. The latest Native --badge run confirmed first-frame submission and clean3-second auto-exit,not manual visual/input acceptance. verification.json retains source/executable hashes. Source/Options/AccessibleLabel changes publish before requesting a frame; identical assignments remain idle.

Light/Dark matrices from actual Native GPU readback were also inspected. Subtle ghost/outline use white foreground tokens,so background selection matters. This does not replace official reference-image comparison. After detecting incremental emission skipping,an unused audit-only define forced actual output of four adapters and the catalog. Normal build settings were restored with zero solution warnings/errors.

## Allocation probe boundary and invalidation control (2026-11-06)

The managed full GPU run following Input observed12336B during existing Badge reads. Unchanged focused49, JIT-focused49 and traced fullGPU2427 runs passed, so the precise cause is still unknown. Failure to reproduce does not establish resolution. Tier0/OSR listings exist but do not prove causation.

The product cache is unchanged. The exact measured loop now lives in a separate NoInlining method, warmed through the same method instead of a shorter surrounding loop. Cold counts remain recorded. The steady expectation stays0B, and a control invalidates the memo each iteration to detect actual formatting allocations. There is no tolerance increase, retry-until-success, runtime switch used to pass, or forced GC during measurement.

Three independent NET10 runs pass50 checks each with cold/steady0B and invalidated320000B. Updated managed fullGPU2428 and fixedRC3 NativeAOT focused50 pass; Native also reports0B/320000B. All2097 current managed images match both preserved Native EXT/core sets. Nine relevant product source hashes are unchanged, allowing unrelated managed2913/Native2427 per mode/generator115 results to be reused at their original scope. This is not a product allocation/FPS/Private improvement claim.

Evidence is in `artifacts/badge-allocation-2026-11-06/verification.json`, `plan.json`, preserved failure, pre-change source, JIT, trace and execution logs. Managed reproduction: `dotnet run --project tests/Duxel.Rendering.Tests -c Release -- artifacts/badge-check --badge-only`; the fixedRC3 published executable accepts `artifacts/badge-native badge-only`. Pin the existing dedicated SDK paths. Do not repeat full diagnosis without new distinguishing evidence. The original12336B cause remains open while Input slots/Field/UIA/motion and the full objective continue.

## Measurement and parent allocation — 2026-11-10

UiBadge and CounterBadge now implement pure MeasureContent through UiControlContext.MeasureBadge<TPresentation>. IUiBadgePresentation.Measure defaults to an explicit unsupported exception; CanArrange opts into one common PlaceText root and paint clipped to that root. The built-in MeasureFluentBadge<TIcon> shares natural geometry with rendering without placing or painting it. Invalid extents, missing measurement and multiple allocated roots fail explicitly. The shared wrapper scopes/restores table/column state.

```csharp
using var badge = Fluent.Badge("Status");
using var box = Dux.SizedBox(badge, 80, 64);
// Actual frame/root is 80 x 64; glyph and icon metrics remain unchanged.
```

An explicit parent changes the frame/root dimensions, centers natural text/icons in that root and clips all frame/text/icon paint. Font size and icon dimensions still come from Badge size tokens. This also applies to an explicitly allocated dot; its standalone natural size remains 6 x 6. Existing immediate overloads and standalone widgets preserve natural geometry. Custom immediate calls opt in with content, options, constrainLayout : true.

Hidden zero CounterBadge has zero natural desired size, and a tight parent still reserves its assigned root without invoking the presentation or painting. Legacy standalone hidden badges still consume no space. Read-only content providers and source ReadContent are read once per explicit measure/render phase; parent layout can request separate phases. The existing source behavior's bounded numeric-string memo can be prepared/reused by those phases. Measurement does not paint, process input or invoke event callbacks. No widget/context fields, new caches, runtime registries or source snapshots were added.

Fixed .NET11 RC3 managed validation passes 374 checks: CPU/source/lifetime 73, existing GPU 11, allocated pixels 248, paced retained pixels 40 and reuse budgets 2. Raster/vector, Light/Dark, MSAA 1/4 and three frame slots are covered; 599 readbacks include warmup. The existing unchanged/overflow numeric memo probes still report 0B for 10000 steady reads, with the invalidation control detecting 320000B; this is a narrow cache invariant, not a before/after whole-app improvement. Existing source disposal, icon-only label validation and palette invalidation checks remain.

New local layout_badge_allocation_fba.cs offers Width : 100, Height : 64, Increment/Zero/Overflow and Dot controls. Identical-body local-reference compilation and first presentation/15-second exit are recorded separately from manual interaction. Evidence: artifacts/badge-allocation-2026-11-10/verification.json and adjacent source hashes/logs. Current Native/cross-runtime image checks remain unverified because standard VS C++ discovery is absent; earlier Native evidence applies to its own revision. Whole-goal counts and resize deferral remain unchanged. New allocation/FPS/Private/native-code-size improvement is unmeasured.

2026-11-12 follow-up — [Content measurement contract](widget-cpu-retention.md): deterministic sources/presentations participate in parent retention; dynamic string/badge getters and custom traits default to fresh measurement. Declared getter Progress geometry and stable Spinner visibility are separate promises; rendering/getters/animation/navigation continue. Focused26 and exact GPU comparisons qualify this scope, not full NativeAOT/performance. Evidence: artifacts/content-layout-retention-2026-11-12/verification.json.

UiControlContext.PaintFluentBadge(bounds, content, options) paints a decorative badge in supplied bounds without placing another layout item or advancing the cursor. It shares the standalone badge typography, palette, clipping and forced-color logic. The containing control owns accessibility semantics; TabList exposes badge text as its item help text.
