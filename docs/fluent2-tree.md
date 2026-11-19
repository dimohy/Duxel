# Fluent Tree — widget integration

Open **Content and lists → Tree** in the Gallery to use the two examples. In **Row buttons**, focus the Report row, press F2 to focus its Open button, Enter to open it, and Escape to return to the row. The opening count changes only on Enter.

The external Linux test client accepts `x11-gallery-tree-actions <gallery-pid> <result-json> <x11-window-id>`. It runs the original hierarchy and selection checks, then sends F2, Enter, and Escape to the owned Gallery window. It checks the AT-SPI focus state, the opening count, and captures the focused button and the restored row. This checks X11 keyboard delivery and the published AT-SPI state.

On WSLg, the .NET 11.0.0 managed and NativeAOT Gallery runs each passed 12 checks and exited normally. Four cross-runtime screenshot pairs match below the frame metrics, and four same-runtime repeat pairs match across the entire viewport. Evidence is in `W:/Cache/Duxel/gallery-source-20261117/tree-actions-2117/verification.json`. Physical keyboard and screen-reader speech were outside this check.

The [official guidance](https://fluent2.microsoft.design/components/web/react/core/tree/usage) distinguishes informational expansion, navigation and multiselect. Parent checking cascades to descendants; partial descendants produce mixed parent checks. [WAI-ARIA keyboard guidance](https://www.w3.org/WAI/ARIA/apg/patterns/treeview/) keeps focus independent from selection and defines inward/outward, visible-order and boundary navigation.

## Internal model contracts

- UiTreeHierarchy snapshots flat stable-key/parent/label input. Parents may appear after children. Duplicate keys, missing/self parents, control characters and disconnected cycles are rejected. Iterative indexing retains preorder, parent/children, level, sibling position/count, subtree ranges and terminal counts.
- UiTreeModel reads the caller's expanded branch values. It detects in-place edits, retains hidden descendant expansion, projects visible rows only on changes and restores hidden focus to the nearest visible ancestor. Inward opens or enters; outward closes or returns to a parent. A rejecting setter does not invent an expanded state or poll.
- UiTreeSelection uses terminal item values as its internal multiselect authority. Branch checks derive from descendant counts; changing a branch writes its terminal descendants once. Hidden selections persist. Mixed checks, duplicate/unknown/branch-value rejection, controlled writes and unchanged-state reuse are qualified. This is not a finalized public binding API.

## Evidence and remaining work

[Focused model evidence](../artifacts/fluent-tree-model-2026-11-18/result.json) covers48 checks, a10000-level hierarchy and zero allocations across128 unchanged expansion/selection synchronizations. These are scoped model checks, not whole-widget rendering, FPS or cross-platform speed measurements.

The internal UiTreeWidget/UiTreeItem path now owns arbitrary noninteractive row content and exposes real Tree/TreeItem ancestry. Focused input24 and GPU7 checks cover pointer/keyboard expansion, hierarchy focus, controlled external collapse, single navigation, cascading checks/mixed state, idle frame requests, rich two-line rows, mirrored RTL content and selector hit targets. [Widget evidence](../artifacts/fluent-tree-widget-2026-11-18/result.json). Windows and Linux role values and published hierarchical selection queries are connected. See the scoped evidence below; collapsed selection exposure is connected in the section below; external assistive-technology acceptance remains open.

At this early stage, typeahead, treegrid actions, public presentation, and per-example Gallery source were still open. The later sections record their implementation and focused checks. Full Fluent conformance and independent screen-reader speech remain open.

Unconditional keyboard refocusing initially cleared pointer activation and requested idle frames; only actual navigation now calls that path. Mixed checks use explicit Indeterminate rather than the legacy nullable-toggle capability conversion. A hidden-child layout-cache hypothesis was disproved (both policies arranged0times across8stable frames); the temporary hook/probe were removed. No layout/FPS speedup is claimed.

## Hierarchical selection queries

`UiAccessibilityTree.CaptureSelection` copies a container, its owned members and selected members under one read lock. `ResolveSelectionContainer` follows a nested row's explicit `SelectionContainerId` only to an actual ancestor and never bypasses an independent intervening selection container. Stale runtime IDs are rejected. A retained `UiAccessibilitySnapshot` exposes the same queries and builds its immutable ID index on first use.

Tree capture retains the real hierarchy, collects members in preorder and excludes independent nested ListBox selections. Iterative collection does not repeatedly walk ancestors for each member. Ordinary direct-child indices and ComboBox ownership through its actual popup ListBox are preserved.

Windows UIA GetSelection/GetSelectionContainer/AddToSelection and Linux AT-SPI selection queries, bulk changes and notifications consume this projection. Removing a selected descendant invalidates its actual Tree root; required selection protects the last selected member. Qualified checks: common projection13, actual Tree input29, Windows COM ABI7 and existing selection-control regression742. [Evidence](../artifacts/fluent-tree-selection-2026-11-18/result.json).

The earlier selection-projection stage still omitted collapsed descendants. The following section describes the newly connected virtual-item exposure/realization scope.

## Collapsed selections and demand-driven exposure

A selected descendant now remains available as a virtual TreeItem when its ancestors collapse. Only the hidden selected path and the latest ItemContainer-demanded path are published; unrelated hidden rows are not created. Windows keeps placeholders in raw view and excludes them from control/content views. Realization preserves RuntimeId and selection without stealing focus.

ItemContainer key/name/next lookup and VirtualizedItem.Realize use the existing request queue. `UiAccessibilityVirtualItem.ContainerId` identifies an actual ancestor container; the existing three-argument constructor and deconstruction remain intact. Realize writes the expanded ancestry once and completes after real row geometry is ready. Rejected controlled writes report failure without inventing visible state. Leaves expose the native ExpandCollapse LeafNode state.

ScrollIntoView expands ancestry and uses the actual enclosing ScrollView. An assigned child can still use its parent's retained arrangement on the expansion frame, leaving new row geometry empty. The common reveal path therefore preserves the request until the next layout generation. It does not remove arrangement reuse or substitute a timed retry. The same nested-scroll fixture reproduced the failure before the fix and succeeded afterward with the expected offset.

Focused73 checks comprise24 virtual-selection/Windows COM cases,29 Tree input cases,7 native-selection cases and13 common-projection cases. Existing virtual-list75 checks also pass. A10000-level ancestry is opened with one binding write; this is functional coverage, not a measured FPS/speedup claim. [Current evidence](../artifacts/fluent-tree-virtual-selection-2026-11-18/result.json).

Actual Linux AT-SPI execution, external screen readers, public Tree presentation/API, row actions, keyboard typeahead and Gallery registration remain incomplete. These query/realization checks do not establish whole-Tree conformance.

TreeChildrenChanged notifies each actual parent once for child membership or virtual-to-real view transitions. Windows reuses the existing structure-event transport; unchanged frames emit no repeat notification. Reparenting a Tree rebuilds virtual IDs in the new identity scope and retires the old providers.


## Public presentation and incremental name search

`UiTree<TPresentation>` and `UiTreeItem` are public. `IUiTreePresentation` supplies row metrics, row/chevron painting, deferred selector painting and focus painting to the same input/selection/accessibility engine. Measurement retention requires an explicit presentation promise; invalid metrics fail at the shared layout boundary.

Constructors distinguish informational expansion, nullable single selection and terminal-key multiple selection. The public API does not accept two optional competing selection authorities; a null single-selection binding is rejected. `UiTreeOptions` supplies Medium/Small row size, Subtle/SubtleAlpha/Transparent appearance, direction, width and navigation behavior. Option changes invalidate row/parent measurement while preserving identities, expansion and selection. Caller-owned content widgets retain their own typography and composition.

Committed text searches only visible rows. Repeated characters cycle matching rows, including Korean and multi-character names with spaces. Search moves focus without changing selection or expansion; a search-space does not toggle a selector. Navigation/focus exit resets the prefix; after one second, the next committed input starts a new prefix without a timer-driven render. Option controls share only the prefix buffer, preserving their own navigation/selection semantics.

The [standalone sample](../samples/fba/tree_presentation_fba.cs) demonstrates single navigation, cascading selection and live size/appearance/direction options. Run from the managed SDK validation directory:

```powershell
Set-Location W:\MyWorks\Duxel\tests\managed-validation
& ..\..\run-fba.ps1 ..\..\samples\fba\tree_presentation_fba.cs -Managed -NoCache -TargetFramework net11.0-windows7.0
```

The managed sample window was executed in explicit verification mode. Four frames changed size, appearance and direction, then checked two Tree roots, twelve items, retained bindings and normal exit. An outer ScrollView owns the full document so shorter windows can scroll vertically. This does not qualify every user interaction combination. Qualified checks: public API/search19, GPU9, existing Tree53 and option controls742. All six theme/appearance GPU captures were inspected. [Evidence](../artifacts/fluent-tree-presentation-2026-11-18/result.json).



## Plain string labels

`new UiTreeItem(value, parentValue, label)` creates a presentation-owned literal label. The Fluent presentation uses Body1 for Medium and Caption1 for Small. Explicit fourth-argument widgets retain their authored typography and composition. Custom presentations implement `IUiTreePresentation.CreateLabel` or use explicit content. Labels are created and validated before layout.

`UiTreeOptions.TextOverflow` defaults to Wrap, with actual multiline row allocation. Ellipsis measures through the existing paragraph engine and preserves combining-character and emoji ZWJ graphemes. Zero content width paints no text. Only truncated rows use the shared hover/focus full-name Tooltip, dismissible with Escape. Accessible names always retain the original text. Size/direction/width changes remeasure; unchanged frames reuse shaping.

Qualified: label16, GPU4 across themes/sizes/RTL with four inspected captures, public API19 and existing row input29. The executable sample combines literal labels with explicit two-line content and an overflow toggle. Full Tree, actual Linux and external assistive-technology acceptance remain incomplete. [Evidence](../artifacts/fluent-tree-labels-2026-11-18/result.json).


## Controls inside a row

Set `UiTreeOptions.RowActions` to opt into interactive row content. F2 focuses the first available control. Logical horizontal arrows move among controls that do not use the arrows themselves. Escape returns to the row after any active composition or blocking surface is handled. The containing tree keeps one Tab stop and repairs focus when a branch collapses. A click claimed by a child control does not trigger the row. The sample includes an Open button inside the Duxel.App row. Accessibility publishes a TreeGrid root with hierarchical rows and one content cell per expanded row. The cell contains the row controls. Windows UI Automation exposes the native Grid and GridItem patterns. Linux maps the root, rows and cells to AT-SPI tree table, table row and table cell roles. The Linux bridge exports Table and TableCell for loaded grids, including dimensions, cell lookup, spans, and existing row selection. Table indices refer to the logical row-major coordinate space. Accessible children retain their physical hierarchy. The live Linux D-Bus and screen-reader checks are pending. Up and Down move to the corresponding action in the adjacent row or focus the row when that action is absent. Home and End move to the first and last actions.

## Disclosure motion and multiline rows

Disclosure arrows use a shared 160 ms transition. `ReducedMotion = true` makes the Tree chevron change immediately. `AlignSelectorToTop = true` places the selection indicator and its hit area at the top of a multiline row. Each Gallery example shows code with its current configuration.

## Inspecting accessibility on Linux

Run `dotnet Duxel.Linux.App.Tests.dll atspi-gallery-tree <gallery-pid> <fresh-result.json>` in the Gallery's Linux D-Bus user session. The independent libatspi client verifies the folder's mixed state, collapse and reopen, and group and child toggles. It queries the second example through the AT-SPI Table interface, resolves the Report cell, and invokes its Open button. All seven checks passed against the .NET 11 x64 NativeAOT Gallery in WSLg.

Run the Gallery and Orca or Accerciser in the same Linux desktop user session. On GNOME, use `Super+Alt+S` to enable Orca. Focus the Report row in the Tree example and press F2 to enter its Open button. Orca can read the element name and state. Accerciser can inspect the row, cell and button hierarchy. Automated clients can use this information to verify actions and their results. Live Orca behavior in the current Gallery has not been verified.
