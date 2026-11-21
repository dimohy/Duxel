# Implementation acceptance review

On 2026-11-16, the original widget-retention, widget-tree, container, and Observer ledgers were reread: 50 requirements across four files. The [review record](../artifacts/goal-audit-2026-11-16/acceptance-review.json) preserves each criterion and its existing completion flag. Passing a focused regression does not complete the product.

On 2026-11-19, the same 50 criterion IDs were present across the four current ledgers. Their completion flags total 33 of 50: widget retention 11 of 20, widget tree and theme 6 of 14, containers 6 of 6, and Gallery 10 of 10. This tally covers those four ledgers.

## Defect found during this review

Synthetic pointer input on the actual Observer controls, **Popover → Open profile editor → Close profile editor**, crashed while connecting accessibility relationships. Closing the surface retired its staged accessibility nodes in the same frame, but left deferred relationships between standalone Labels and their input widgets.

The shared overlay retirement path now retires nodes and deferred Label relationships together. Relationships outside that surface remain live; reopening creates relationships to current nodes. Strict relationship validation remains enabled. The Observer regression additionally checks the actual Label/input connection after reopening.

The broader input suite also exposed a test-resource issue: its successful supplementary-character case selected a font pair without `U+1F600`. That case now explicitly selects Segoe UI Emoji and fails before execution if it is unavailable. The test character and production unsupported-glyph failure remain unchanged.

Final-source checks passed: Observer 316, Label 63, composite contracts 500, text editing 4,114, Tooltip GPU 792, and Popover GPU 452. These scopes may overlap. All 44 ordinary Observer pages acknowledged presentation; this is not pixel comparison or actual keyboard acceptance. Windows and Linux host builds had no warnings/errors. Actual Linux execution was not performed.

## Remaining acceptance scope

| Scope | Current assessment | Required evidence |
| --- | --- | --- |
| Common CPU construction, geometry, cache lifetime | Partially verified | All consumers including legacy APIs, queued frames, and cache bounds |
| Custom container external consumer | Partially verified | Linux x64 managed and NativeAOT matched the initial X11 image and all four held exercise screens. Physical input, container performance and current Windows NativeAOT remain open. [Custom container runtime comparison](custom-container-contract.md#linux-runtime-comparison--2026-11-19) |
| Container acceptance | 6 of 6 requirements | The Windows .NET 11 build passed scroll, precision, virtualization, lifetime, and Vulkan checks. All 185 GPU images match the archived Windows NativeAOT run. The independent source-generated Footer passed 15 checks. The Gallery presented the container and virtual pages and verified source placement. The Linux NativeAOT comparison measured identical sample workloads on both builds. [Container ledger](../artifacts/widget-containers-2026-11-06/plan.json) · [Performance scope](archive/performance/optimization-session-2026-11-19.md#external-custom-container-nativeaot-build-comparison) |
| Matched performance and memory | Partially verified | Stable .NET 11 NativeAOT has two earlier matched ERD motion runs, one idle run, and one current motion run after the startup fixes. Vulkan payload stayed constant. The current run's Private Bytes rose 7.41 MiB. The cause of the residual growth remains open. |
| Windows NativeAOT | Partially verified | Windows .NET 11.0.0 managed and NativeAOT each passed 104 Gallery presentation scenarios. Image comparisons matched 4,038 common widget, 185 container, 245 RadioGroup, 148 Gallery integration, and 59 calendar and date/time picker PNG pairs across their respective runs. The full workload and remaining widget families still need comparison. |
| Gallery acceptance | 10 of 10 requirements | The Windows .NET 11 managed build passed 105 Vulkan presentation scenarios and six external UI Automation checks in the same runtime. The desktop check covered page navigation, SearchBox and Textarea editing and retention, clearing the SearchBox, and normal window closure. Startup validates all page routes and factory mappings. [Current ledger](../artifacts/implementation-observer-2026-11-11/plan.json) · [Tooltip event tests](tooltip-events.md#qualification) |
| Linux and ARM64 | Partially verified | The x64 .NET 11 NativeAOT Gallery passed 109 Vulkan presentation scenarios on Ubuntu/WSLg. The recorded presentation sequences match accepted Vulkan submissions. Source launch resolves x64 and ARM64 publish targets. Broad cross-host pixel and ARM64 hardware verification remain open. |
| Visible Linux SVG browser comparison | Partially verified | A Linux x64 managed build and the verified NativeAOT build match six X11 captures below the diagnostic header. The scrolled state contains the actual 2×, 37° Duxel vector. The matching captures include the scrollbar in each sampled phase. [Verification scope](archive/performance/optimization-session-2026-11-19.md#visible-linux-svg-browser-comparison) |
| Linux Carousel | Partially verified | External AT-SPI passed nine checks each against the managed and NativeAOT x64 Gallery, including preview selection, wrapping, autoplay and pause. Screen-reader speech remains open. [Carousel verification](fluent2-carousel.md) |
| Linux TreeGrid row actions | Partially verified | X11 F2, Enter and Escape worked on the live Gallery in .NET 11 managed and NativeAOT. Four cross-runtime captures match below the frame metrics. Physical input and screen-reader speech remain open. [Tree verification](fluent2-tree.md) |
| Fluent, SVG, Markdown, accessibility, transforms | Full denominator undefined | Original requirements and state/presentation/platform acceptance |
| Resize | Deferred by the user | Excluded from current work |

The managed Windows and WSLg Linux presentation reuse suite now has a shared-name baseline. Each host passed local pixel checks. The hosts match 214 of 574 raw RGBA cases, including all 160 sparse-update cases. The remaining pixel differences and the full Gallery comparison are open. [Vulkan raw-pixel scope](../tests/Duxel.Vulkan.Linux.Tests/README.md).

Projected fractional borders now evaluate edge coverage before applying the local clip. The independent 1× MSAA checks verify both edge colours and the clear pixels outside the clip. They pass on Windows managed and WSLg x64 NativeAOT with .NET 11.0.0 and reject the previous shader on WSLg. Other cross-host pixel differences remain open.

On WSLg with Cairo 1.18.0, the Linux styled paragraph fixture matches independent native run-mask composition in all 36 cases. Comparing against Pango painting directly into a shared surface still yields seven exact-alpha differences. A single-run native A8 probe reproduced the difference with no Duxel or Vulkan calls.

The historical six-axis aggregate snapshot does not record individual requirement membership. This review does not invent a mapping from the 50 source requirements to those aggregates. Existing counts describe the historical ledger, not current whole-product verification. Final regressions, executable hashes, and source fingerprints are recorded in the sibling `verification.json`.

An external Windows UI Automation client used the ordinary .NET 11 Gallery to set a Korean SearchBox query and a multiline Textarea value. Both values and their displayed C# initializers survived navigation away and back. Invoking the SearchBox clear button emptied both the field and the initializer, and the empty value persisted after navigation. The owned Gallery exited normally. This checks native automation actions and leaves physical keyboard, IME, and screen-reader use open.

## Additional common input-phase evidence

The common Input/Presentation phase processes layout and behavior per event while generating only the final presentation. Real button callbacks change margin, layout/visual rotation, scale, parent rotation and scroll; three editors verify subsequent pointer focus and editing, across both traversal orders. The cohort passes345 checks including84 new assertions. All20 product-source hashes match the preceding common-phase verification. [Evidence](../artifacts/goal-audit-2026-11-16/interactive-phase-verification.json).

This was partial evidence for `cpu-behavior` at the time of the audit. The historical snapshot remains 24 done and 26 open across 50 criteria. The current widget-retention ledger now records the Windows basic-widget behavior criterion as verified on .NET 11. The managed run passed 4,646 checks, matched all 4,038 shared GPU images to archived NativeAOT, and separately checked eight newly captured Switch direction images. The native window and resize groups remain deferred. Current NativeAOT, memory bounds, and cross-platform criteria have their own evidence. [Current widget-retention ledger](../artifacts/widget-retention-2026-11-04/plan.json)
