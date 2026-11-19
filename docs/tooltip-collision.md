# Tooltip collision and arrow qualification

Last synced: 2026-11-11

Current [auto-size/overflow](tooltip-auto-size.md) provides stateful remeasurement and visible/full content extents.

Current [measured placement](tooltip-measured-layout.md) adds compiled per-candidate offsets and actual target-width content remeasurement.

Current [offset/cover qualification](tooltip-offset-cover.md) extends this baseline with signed offsets, logical padding and cover/shift limits while retaining these768 reference cases.

The shared resolver now follows the offset/flip/shift/arrow calculations from Floating UI core 1.6.8, the version selected by the [pinned Fluent lock](https://github.com/microsoft/fluentui/blob/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/yarn.lock). The [published core module](https://unpkg.com/@floating-ui/core@1.6.8/dist/floating-ui.core.esm.js) and utils 0.2.8 are retained with hashes and MIT attribution. The production implementation is compiled C#; JavaScript is used only to generate independent test expectations.

Changes include facing-side-first best-fit selection, the RTL centered alignment-side rule, immutable candidate sequences, separate flip/overflow rectangles, configurable arrow width, the one-unit arrow padding reserve, a small-target alignment reset followed by another edge shift, and residual `ArrowCenterOffset`. The existing triangle width defaults to twice its height. A future Fluent surface can supply the CSS arrow's unrotated square width; this checkpoint does not implement its rounded shape or shadow composition.

`reference-cases.mjs` executes the original core module with only its utils import redirected to the retained version. Its explicit platform supplies unit-scale target/body/boundary rectangles and arrow/client dimensions. It generates 768 deterministic LTR/RTL cases with 12 positions, oversized bodies, small targets, default/custom candidate lists (including duplicates), separate boundaries, pinning, multiple arrow dimensions and padding. C# compares positions, body bounds, arrow anchors and residual offset to tolerance 0.001. This is a rectangle-platform test, not a DOM ancestor/transform qualification.

The first comparison found five RTL centered-candidate priority differences. The signed alignment-side condition was corrected; all reference cases pass. Final CPU checks total1326. Tooltip GPU416 includes eight upstream-derived collision scenes per light/dark/MSAA1/4 configuration, in addition to the earlier actual-owner placement/snapshot scenes. The previous side-top/bottom literal fixtures move by one unit to match the arrow reset; unchanged images remain separately audited. The consumer sample uses an immutable custom placement sequence. Evidence: `artifacts/tooltip-collision-2026-11-11`.

Open work remains DOM-equivalent ancestor/scale discovery where applicable, official rounded arrow and combined shadows, controlled visibility and complete property/window-shutdown notifications, the Fluent factory, current NativeAOT/runtime comparison, and the unchanged full project goal. No full Fluent conformance or performance improvement is claimed.
