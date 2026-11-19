# Fluent 2 implementation scope — user correction, 2026-11-05

2026-11-16: the compiled55-name requirement snapshot has39 connected names including Rating and16 missing. The68 exposed factories include variants and are separate from full state/visual/platform conformance. Follow the [Rating scope](fluent2-rating.md); the full original goal and first-declaration-removal cache gate0/2 remain open.

2026-11-13 [List](fluent2-list.md): generated Fluent.List connects plain/single/multiple selection to Observer.59 factories,35/55 official top-level names connected,20 missing. Complete conformance remains open.


2026-11-13 [Popover triggers](fluent2-popover.md): one Click/Hover/Context mode, 500ms close scheduling/cancellation and rejection quiescence, virtual point placement, Menu/Shift+F10. Observer → Popover → 팝업 열기 방식.


2026-11-13 [Popover arrows and surface cache](fluent2-popover.md): WithArrow, 6/8px geometry, combined shadows, integral-movement/scale caching and pointer occlusion reuse the existing Tooltip raster. Full conformance remains incomplete.


2026-11-13 [Popover](fluent2-popover.md): owned arbitrary content, controlled/rejected opening, nonmodal/trapped focus, Escape restoration, natural height/scrolling, nesting, hiding and disposal. Observer → Popover. Catalog: 56 factories, 32/55 top-level connected, 23 missing; full conformance remains incomplete.


2026-11-13 Nonmodal popup input foundation added. At that earlier checkpoint the inventory was 55 factories, 31/55 connected, 24 missing; the Popover entry above supersedes it. [Foundation](popup-input-modes.md).

2026-11-13 Toolbar integration:55 factories,31/55 top-level names connected,24 missing. Connectivity does not mean full conformance. [API](fluent2-toolbar.md).

2026-11-13 InteractionTag: 52 factories, 28/55 connected top-level names, 27 missing. This is a Tag variant, preserving the top-level denominator; full conformance remains open. [API](fluent2-interaction-tag.md).

2026-11-13 Tag: 51 exposed factories, 28/55 connected top-level components and 27 missing. TagGroup is tracked under Tag, without inflating the top-level denominator. Full conformance remains open. [API](fluent2-tag.md).

2026-11-13 Combobox behavior: added dismissal/blur reconciliation, authoritative Open acceptance/rejection and external updates, actual OpenChanged notifications and localized static empty-result text. Baseline CPU59/8940,GPU6/804,native3/47 plus latest affected Combobox67,Observer246 and real application614frames/13checks passed. [API](fluent2-combobox.md), `artifacts/combobox-behavior-2026-11-13/verification.json`. Original scope, selected tags, physical IME and complete conformance remain open.

2026-11-13 Combobox: actual editor/IME and popup support substring search, single/multiple selection and freeform input. Catalog50 exposed/27of55 top-level connected/28 missing. CPU59/8908, GPU5/692, external UIA3/40, three real app sessions(each614frames) and579 exact RGB comparisons passed. Connectivity is not complete conformance. [API and limits](fluent2-combobox.md), `artifacts/combobox-2026-11-13/verification.json`.

Additional qualification: fixed stale popup publication stamps causing an accessibility relationship crash across UI contexts. Latest CPU58/8869, affected GPU3/296 and native3/39, plus two real app sessions passed. Earlier broad GPU9/1646 and native7/109 remain separately preserved; they were not all rerun on this binary.

2026-11-13 Dropdown: qualified single/multiple selection, persistent options, actual Windows Selection/Value, shared popup scrolling and name search. Catalog49 exposed/26of55 connected/29 missing. CPU58/8844, GPU9/1646, native7/109 and two real rendering sessions(each614frames/4checks) passed. [API and open scope](fluent2-dropdown.md), `artifacts/dropdown-2026-11-13/verification.json`. Editable Combobox, complete conformance, fixed RC3 NativeAOT and performance remain open.

2026-11-13 AvatarGroup: Spread/Stack/Pie, exact overflow counts, member menus, RTL and image/Korean/SVG content reuse existing Avatar, clip and Menu machinery. Run `./run-gallery.ps1` → **아바타 그룹**. [API and limits](fluent2-avatar-group.md). Catalog48 exposed/25of55 top-level connected/30 missing; complete conformance remains open.

2026-11-12 Breadcrumb qualification:47 exposed factories,24of55 official top-level groups connected,31 missing. Connectivity does not establish complete behavior, appearance or accessibility conformance. [Breadcrumb API and open scope](fluent2-breadcrumb.md), `artifacts/breadcrumb-2026-11-12/verification.json`. Default icons use the Fluent font; optional SVG icons reuse the existing cache path.

Latest 2026-11-11 Menu connection: exposed36=top-level17+variants19, missing38/55. Evidence artifacts/menu-composition-2026-11-11/catalog-counts.json. Earlier counts and the 64-URL match below are historical evidence, not a new full-platform revalidation. [Fluent Menu](fluent2-menu.md) still has shadow, full state/accessibility and tab-composition work remaining.

Earlier 2026-11-11 checkpoint: 35 exposed names comprise 16 of 55 official top-level names and 19 parent-linked variants, with 39 top-level names unconnected. All 64 live platform reference URLs match (Web React47/iOS12/Android5). See `artifacts/fluent-tooltip-2026-11-11/catalog-drift.json`. [Tooltip](fluent2-tooltip.md) now has a generated factory backed by the existing target wrapper. Complete state/visual/accessibility conformance has no confirmed denominator.

The scope authority is the public component catalog and component design, state and interaction guidance at https://fluent2.microsoft.design/. Track platform differences published on that site in the mapping.

Official repositories and Storybook are implementation and verification references for those components. Do not promote every public type, helper, older design generation or platform-specific API into an independent requirement. Track subcomponents as required by the official component contract.

The previous 325 names are repository research results, not the goal's component denominator. The 299 unconnected names are not a count of widgets that must be implemented. Preserve existing evidence and implementations. Generated requirements and related tests now follow the official-site classification described below.

Shared inheritance, behavior/presentation separation, source-generated static binding, exclusion of nonstandard default components and common engine optimization remain required.

## Production generation and verification

The official site's Web React47, iOS12 and Android5 entries match the independent saved link snapshots. Platform deduplication produces55 top-level names. The19 currently supported variants link to parent component guidance. Text presets, CounterBadge and ToggleButton do not inflate the top-level count. The Windows page's WinUI guidance remains a Windows presentation reference, not permission to import every repository API into scope.

Production FluentComponents.tsv now follows that boundary. Preserve the original research in artifacts/fluent-site-scope-2026-11-05/FluentComponents.before.tsv. The generator rejects component/pattern/variant requirements without official-site component guidance (DUXWGT006). Each variant must link to parent guidance present in the same catalog. Required top-level names and allowed factory names are separate; strict completeness also rejects missing variants (DUXWGT007).

.NET10 generator83 and widget-tree72 checks passed. Managed .NET10.0.9 and pinned RC3 Native .NET11.0.0-rc.3.26522.148 produce identical arrays. The26 factory names comprise7 top-level names and19 variants;48 top-level names are unconnected. This does not prove absence of legacy functionality or complete fidelity of the7 connected components. Full variant/state/visual/input/accessibility conformance remains incomplete. Evidence: artifacts/fluent-site-scope-2026-11-05/verification.json.

2026-11-06 RadioGroup connection:28 generated factory names comprise9 top-level names plus19 existing variants;46 top-level names remain unconnected. The official top-level denominator stays55. These are connections, not completed visual/input/accessibility conformance. Current evidence: artifacts/fluent-radio-2026-11-06/verification.json.

2026-11-06 Link connection:29 factories comprise10 top-level names plus19 variants;45 of55 top-level names remain unconnected. This is separate from full variant/state/visual/accessibility completion. Evidence: artifacts/fluent-link-2026-11-06/verification.json.

After Input connection on2026-11-06, discovery has30 names=11 top-level+19 variants, with44/55 top-level names unconnected. This does not count complete official states/visual/accessibility conformance. Evidence: artifacts/fluent-input-2026-11-06/catalog.json and verification.json.
After Field connection on2026-11-06,the generated catalog has31 names=12 top-level+19 variants,with43/55 top-level names unconnected. Default Fields own real controls and consume generated size/orientation presentations. UIA,full variants and official visual parity remain incomplete. Evidence: artifacts/fluent-field-2026-11-06/catalog/result.json,component-mapping.json and verification.json.


Earlier Tooltip factory checkpoint evidence: `artifacts/fluent-tooltip-factory-2026-11-11/final-catalog-only/result.json`; factory qualification: `artifacts/fluent-tooltip-factory-2026-11-11/verification.json`.

Current TabList catalog: `artifacts/fluent-tablist-2026-11-11/final-catalog-only/result.json`. See the [TabList foundation](fluent2-tablist.md).

2026-11-13 [Popover entry motion](fluent2-popover.md): default400ms group fade, directional10px slide, immediate exit, same-time caching, live input/IME/accessibility geometry and finite scheduling. Observer → Popover → 팝업 모션. Full conformance remains incomplete.

2026-11-13 [Popover paint extent](fluent2-popover.md): custom PaintBounds, viewport default, bounds/budget checks and wide-decoration sample preserve Fluent pixels. A native Popover failure remains under diagnosis; full conformance is incomplete.
