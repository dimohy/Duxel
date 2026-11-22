# Complete Fluent 2 component migration

2026-11-20. The catalog includes `InlineDrawer` and `OverlayDrawer` under Drawer. The Android circular ProgressIndicator variant brings the catalog to 55 top-level components and 36 registered variants. [Circular progress](fluent2-progress-indicator.md#circular-progress) The source scope remains draft while the review of component parts, states and responsive behavior continues. [Official scope and Drawer types](fluent2-scope.md)

Earlier on 2026-11-20, the Microsoft React, iOS, and Android component indexes matched the 64 catalog links. `CompoundButton`, `MenuButton`, and `SplitButton` brought the registered variant count to 33. The Windows .NET 11 generator suite passed 131 checks with each of package Roslyn and SDK Roslyn. The strict build found no missing factories and rejected the draft reference scope. [Official scope and button types](fluent2-scope.md)

2026-11-18. [FluentProvider layout direction](fluent2-provider.md#local-layout-direction) scopes measurement, navigation geometry and popup placement to a subtree. Windows Gallery and targeted regressions pass. The complete direction, brand and typography contracts remain in scope.

2026-11-18. [NavigationBar](fluent2-navigation-bar.md) adds a contextual title, screen actions and an optional secondary control. The generated inventory connects all 55 currently tracked top-level component names. Qualification of the full design, behavior and platform scope remains open.

2026-11-18. [Carousel](fluent2-carousel.md) adds controlled slides, step and image navigation, and optional autoplay.

2026-11-18. [CardNudge](fluent2-card.md#cardnudge) adds an inline suggestion with a controlled dismissal, an optional action and a live Gallery example.

2026-11-17: [HeadsUpDisplay](fluent2-heads-up-display.md) connects centered blocking/nonblocking presentation, dismissal, transitions and Gallery. Top-level linkage47/55,8 missing; full conformance remains open.

2026-11-16: interactive Rating connects the generated factory. There are68 exposed factories,39 connected of55 official top-level requirements and16 missing; complete conformance remains open. Input14,independent glyph GPU32 pairs,Observer356 and45 normal pages pass. Display/compact and browser visual comparison remain open. [Rating guide](fluent2-rating.md), `artifacts/fluent-rating-2026-11-16/verification.json`.

2026-11-15 [Skeleton](fluent2-skeleton.md): added loading scopes, Wave/Pulse, inheritance, static motion, native busy semantics and the Observer Skeleton tab. The compiled catalog has62 factories,37/55 connected parents and18 missing; connection counts do not establish full conformance.

2026-11-13 [List](fluent2-list.md):59 generated factories,35/55 top-level names connected,20 missing. Lifetime/editor propagation and external UIA Grid/selection/edit/disabled/omission are qualified; multi-DPI, physical screen readers and full Fluent conformance remain open.


2026-11-13 [Popover triggers](fluent2-popover.md): one Click/Hover/Context mode, 500ms close scheduling/cancellation and rejection quiescence, virtual point placement, Menu/Shift+F10. Observer → Popover → Change popover trigger.


2026-11-13 [Popover arrows and surface cache](fluent2-popover.md): WithArrow, 6/8px geometry, combined shadows, integral-movement/scale caching and pointer occlusion reuse the existing Tooltip raster. Full conformance remains incomplete.


2026-11-13 [Popover](fluent2-popover.md): owned arbitrary content, controlled/rejected opening, nonmodal/trapped focus, Escape restoration, natural height/scrolling, nesting, hiding and disposal. Observer → Popover. Catalog: 56 factories, 32/55 top-level connected, 23 missing; full conformance remains incomplete.


2026-11-13 Nonmodal popup input foundation added. At that earlier checkpoint the inventory was 55 factories, 31/55 connected, 24 missing; the Popover entry above supersedes it. [Foundation](popup-input-modes.md).

2026-11-13 Toolbar integration:55 factories,31/55 top-level names connected,24 missing. Connectivity does not mean full conformance. [API](fluent2-toolbar.md).

2026-11-13 InteractionTag adds independent primary/dismiss actions and group integration. 52 factories and 28/55 connected top-level names are connectivity counts, not full state/visual/accessibility acceptance. [API](fluent2-interaction-tag.md).

2026-11-13 Tag: 51 factories, 28/55 connected top-level components, 27 missing. Basic Tag and shared selection dismissal groups are covered; InteractionTag and full conformance remain open. [API](fluent2-tag.md).

2026-11-13 Combobox behavior: added dismissal/blur reconciliation, authoritative Open acceptance/rejection and external updates, actual OpenChanged notifications and localized static empty-result text. Baseline CPU59/8940,GPU6/804,native3/47 plus latest affected Combobox67,Observer246 and real application614frames/13checks passed. [API](fluent2-combobox.md), `artifacts/combobox-behavior-2026-11-13/verification.json`. Original scope, selected tags, physical IME and complete conformance remain open.

2026-11-13 Combobox: actual editor/IME and popup support substring search, single/multiple selection and freeform input. Catalog50 exposed/27of55 top-level connected/28 missing. CPU59/8908, GPU5/692, external UIA3/40, three real app sessions(each614frames) and579 exact RGB comparisons passed. Connectivity is not complete conformance. [API and limits](fluent2-combobox.md), `artifacts/combobox-2026-11-13/verification.json`.

Additional qualification: fixed stale popup publication stamps causing an accessibility relationship crash across UI contexts. Latest CPU58/8869, affected GPU3/296 and native3/39, plus two real app sessions passed. Earlier broad GPU9/1646 and native7/109 remain separately preserved; they were not all rerun on this binary.

2026-11-13 Dropdown: qualified single/multiple selection, persistent options, actual Windows Selection/Value, shared popup scrolling and name search. Catalog49 exposed/26of55 connected/29 missing. CPU58/8844, GPU9/1646, native7/109 and two real rendering sessions(each614frames/4checks) passed. [API and open scope](fluent2-dropdown.md), `artifacts/dropdown-2026-11-13/verification.json`. Editable Combobox, complete conformance, fixed RC3 NativeAOT and performance remain open.

2026-11-13 AvatarGroup: Spread/Stack/Pie, exact overflow counts, member menus, RTL and image/Korean/SVG content reuse existing Avatar, clip and Menu machinery. Run `./run-gallery.ps1` → **AvatarGroup**. [API and limits](fluent2-avatar-group.md). Catalog48 exposed/25of55 top-level connected/30 missing; complete conformance remains open.

2026-11-12 Breadcrumb qualification:47 exposed factories,24of55 official top-level groups connected,31 missing. Connectivity does not establish complete behavior, appearance or accessibility conformance. [Breadcrumb API and open scope](fluent2-breadcrumb.md), `artifacts/breadcrumb-2026-11-12/verification.json`. Default icons use the Fluent font; optional SVG icons reuse the existing cache path.

2026-11-12 current Card checkpoint:43 exposed factories,23 of55 top-level names connected,32 missing, ScopeComplete=false. The default passive Card container now owns actual content and exposes three sizes/four appearances through the real Observer page. Selection, managed focus modes and specialized slots remain required; this is a factory connection and baseline qualification, not full Card conformance. CPU55/8309, GPU1557, native Card9+Avatar23,883 prior images exact. [Card](fluent2-card.md), `artifacts/fluent-card-2026-11-12/verification.json`; earlier observations below retain their original counts.

2026-11-12 current: Avatar activity ring, shadow, presence cutout and inactive group composition are qualified in their documented scope. CPU54/8283, GPU1374 and native Avatar23 pass;635 prior scene images match. Current catalog query still exposes42 factories, connects22 of55 top-level names and leaves33 unconnected; these connection counts do not establish full conformance. ScopeComplete=false. Motion, forced colors, full official conformance and original deployment/performance gates remain open. Evidence: `artifacts/inactive-avatar-2026-11-12/verification.json` and `catalog-current/result.json`; [Avatar](fluent2-avatar.md).

Current 2026-11-12:42 exposed factories,55 required top-level names,33 missing; PresenceBadge is a reviewed Badge variant and does not increase the top-level denominator. Full visual/behavior conformance remains open. [PresenceBadge](fluent2-presence-badge.md). Counts below retain their historical observation times.

2026-11-12 Avatar base is now exposed through the generated factory and Avatar Observer page; catalog41exposed names/55required top-level/33missing. Exposure is not full feature completion: activity,shadow,presence andconformance remain open. See [Avatar](fluent2-avatar.md). Earlier counts are historical.

Last synced: 2026-11-12

Current compiled discovery exposes40 names (21 top-level and19 parent-linked variants), including [Accordion](fluent2-accordion.md). The [official-site scope](fluent2-scope.md) has55 top-level names, with34 unconnected. Evidence: `artifacts/fluent-accordion-2026-11-12/catalog-surface/result.json`; ScopeComplete=false. Full variant/state/visual/accessibility conformance still has no confirmed denominator. Earlier39/35,35/39 and325/299 figures are historical snapshots, not current completion counts.

Microsoft Fluent 2 is the default theme. The updated objective requires every official component, matching look and feel, and attribute-driven exclusion of nonstandard components. Its verbatim copy is `artifacts/fluent-components-2026-11-05/goal-objective.md`. Existing optimization and widget-architecture ledgers remain active.

## Official reference scope

Official sources were checked on 2026-11-05; originals and hashes are preserved in `source-manifest.json`.

| Reference | Observation | Interpretation |
|---|---:|---|
| [Fluent React catalog](https://fluent2.microsoft.design/components/web/react) | 47 cards | Not the complete cross-platform/subcomponent denominator |
| [Fluent iOS](https://fluent2.microsoft.design/components/ios) | 12 cards | Shared names and platform-specific behavior require mapping |
| [Fluent Android](https://fluent2.microsoft.design/components/android) | 5 cards | Platform references are retained rather than silently omitted |
| [Windows controls](https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/) | 62 linked table rows | Rows include controls, groups and patterns; this is not 62 distinct components |
| Official React umbrella entry point | 57 exported packages | Includes infrastructure; table, color picker and teaching popover also need review beyond the web cards |

The repository snapshot is `microsoft/fluentui` commit `8830c10bfb2f4c1bdd540287b09713b9679cd1ad`. A master snapshot is not treated as a stable release. Preview in card image descriptions does not establish release-channel status. The official Web Components link was found, but detailed reference extraction remains open. Duplicates, subcomponents, variants, states and release channels still require normalization, so complete component coverage and overall progress remain unknown.

## Actual consumer path

The generator consumes `src/Duxel.Core/FluentComponents.tsv`. Its 64 platform reference rows deduplicate to 55 top-level names; 19 variants link to parents. The 2026-11-11 managed .NET11 catalog run confirms 35 exposed names, 39 missing top-level names and ScopeComplete=false. All 64 current official reference URLs match; see `artifacts/fluent-tooltip-2026-11-11/catalog-drift.json`. Historical managed/Native agreement at the Link stage does not qualify current source for Native.

The following describes research before the scope correction. That draft had419 reference rows and289 names; it is no longer the generated requirements input. It includes 217 React UI component declarations and seven infrastructure entries plus Windows/mobile mappings. All 42 Web Component Definitions were compared:37 share names and five have provisional name mappings, which do not establish behavioral or visual equivalence. Deprecated Alert, the old InfoButton/InfoLabel package and three Virtualizer declarations are retained separately with their official replacement/migration guidance. Current react-infolabel components remain distinct from the deprecated package. Native subcomponents, variants and states still require review. Per-source mappings are preserved in `artifacts/fluent-catalog-2026-11-05/source-mapping.json` and `scope-review.json`.

Unreferenced standard names produce DUXWGT005; missing or malformed reference data produces DUXWGT006. `Fluent.RequiredComponentNames` and `MissingComponentNames` expose the current requirements and missing factories. `IsCatalogScopeComplete` is currently false. Audit arrays live in a separate nested type; ordinary widget construction performs no catalog parsing or lookup.

`-p:DuxelRequireCompleteFluentCatalog=true` produces DUXWGT007 for a draft or missing factory. The pre-correction Core build detected286 missing names and draft scope; those are historical counts. This verifies **factory declarations**, separately from visual/state/behavioral conformance. Default development builds permit continued implementation. An explicit audit static constructor prevents early initialization; the emitted assembly was checked for absence of BeforeFieldInit. .NET10 generator checks64, focused widget checks78 and net8/9/10 builds passed with zero warnings/errors. Those are historical checks. Current RadioGroup records separately cover managed GPU1513, fixed RC3 Native1513 each and catalog comparison. The existing eight cross-run image differences and unfinished official visual verification remain open.

`Dux.Button()` now calls the generated `Fluent.Button()` factory and returns an actual `UiFluentButton`, retaining its existing `IUiView` return contract. The source generator binds `UiFluentButton` directly to `UiButton<UiReleaseButtonBehavior, UiFluentButtonPresentation>`, which supports user inheritance.

Core's standard catalog generator consumes Fluent/Infrastructure/Legacy classifications from `UiComponentAttribute`. `Fluent.ComponentNames` and typed factories include only Fluent declarations. The old `UiButton` remains intact with Legacy and EditorBrowsable(Never). User extension uses `UiWidget`, concrete Fluent widgets and `UiButtonStyle`; consumer assemblies do not replace Core's standard catalog.

The generator now analyzes closed generic UiWidget implementations and public constructors. Invalid types, constraints and duplicate factories produce compile errors. Classification and exclusion across all existing APIs, DSLs, galleries and documentation remain incomplete. Presence in this catalog does not establish every Fluent variant, accessibility behavior, animation or pixel match. Colors still integrate with the existing design; visual-reference comparison remains open.

Existing `ui.Button()` and `UiButton` preserve numeric style compatibility. Default `Dux.Button()` selects the compiled Fluent implementation, so legacy numeric ControlStyle is not the replacement mechanism for its presentation. Choose concrete implementation types to replace presentation.

## Leaf lifetime and input identity

Leaves share `UiWidget` identity, reentrancy, ownership and disposal through `UiControlWidget`, without constructing an unused Build context, subscription collections or render delegate. Composite widgets create their context on first Build.

`UiWidgetIdentity.Instance` distinguishes retained instances. `Parent` preserves label scope for legacy factories recreated every frame; owned keyed children use their parent child keys. Tests cover recreated press/hold/release activation and distinct stable keys with identical labels.

After warmup, the .NET10 construction-only check allocated 131,072B for 1,024 old delegate factories versus 98,304B for new factories: 25% less, or 128→96B each. This compares the preserved previous factory body in one process. It does not establish application Private memory, FPS or repeated NativeAOT performance. No forced GC was used.

## Remaining completion criteria

The project still needs a complete inventory of official components, parts, and states. It also needs to verify the implementations, generated inheritance bindings, separation of nonstandard components, visuals, input, accessibility, and motion. Current engine acceptance covers Linux x64 and includes CPU and GPU costs, private memory, damage tracking, and equivalent-condition performance comparisons. Earlier ARM64 and Raspberry Pi results are preserved as historical evidence. Sources, plans, and logs are under `artifacts/fluent-components-2026-11-05/`.


## Badge/CounterBadge follow-up

Default Badge factories and generated discovery now use common inherited widgets.64 appearance/color combinations,six sizes,three shapes,counter visibility/overflow/dot are implemented. Official visual parity,UIA,RTL/forced colors remain open. See [API,generated code,lifetime and validation](fluent2-badge.md).


## Cross-platform reference follow-up

Historical research connected46 Android Compose-v2 sources/54 classified rows to the former generator input:289 to325 names,26 factories and299 unconnected. These are archived research counts, superseded by the official-site scope above.81 generator/72 widget-tree checks and managed/RC3 Native catalog parity passed. See [sources,mappings,regression and remaining scope](fluent2-platform-inventory.md).

Added the [common Divider implementation](fluent2-divider.md). This does not complete official visual/input/accessibility conformance.

Current Input connection/validation is recorded in artifacts/fluent-input-2026-11-06/verification.json. Connection counts are separate from full official conformance.

Earlier Tooltip factory checkpoint evidence: `artifacts/fluent-tooltip-factory-2026-11-11/final-catalog-only/result.json`; factory qualification: `artifacts/fluent-tooltip-factory-2026-11-11/verification.json`.

Current TabList catalog: `artifacts/fluent-tablist-2026-11-11/final-catalog-only/result.json`. See the [TabList foundation](fluent2-tablist.md).

2026-11-13 [Popover entry motion](fluent2-popover.md): default400ms group fade, directional10px slide, immediate exit, same-time caching, live input/IME/accessibility geometry and finite scheduling. Observer → Popover → Toggle popover animation. Full conformance remains incomplete.

2026-11-13 [Popover paint extent](fluent2-popover.md): custom PaintBounds, viewport default, bounds/budget checks and wide-decoration sample preserve Fluent pixels. A native Popover failure remains under diagnosis; full conformance is incomplete.

2026-11-17 [FluentProvider](fluent2-provider.md) adds native palette/geometry scoping. Canonical linkage is43/55 with12 unlinked; linkage is not complete conformance. Full brand tokens, Provider-local direction and full typography tokens and remaining official scope are open.


2026-11-17 [Shimmer](fluent2-shimmer.md) raises canonical linkage to44/55 with11 unlinked. Loading/content crossfade, reversal and reduced motion reuse common engine paths. Linkage is not full conformance.


2026-11-17 ProgressIndicator raises canonical linkage to45/55 with10 unlinked. [API and qualification](fluent2-progress-indicator.md). Linkage is not complete conformance.


2026-11-17 ActivityIndicator raises canonical linkage to46/55 with9 unlinked. [API and qualification](fluent2-activity-indicator.md). Linkage is not full conformance.

As of2026-11-18, linkage through HeadsUpDisplay is47/55 with8 pending. The [SegmentedControl body preview](fluent2-segmented-control.md) provides input and visual examples but is excluded from linkage until official styles and scrolling are completed.
