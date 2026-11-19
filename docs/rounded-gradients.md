# Shared rounded gradients

`UiDrawListBuilder.AddRectFilledGradient(rect, gradient, texture, clip, radius)` draws a three-stop linear field inside a rectangle or rounded rectangle. UiLinearGradient.Start/End are local coordinates; MiddlePosition is strictly between zero and one. Endpoint colors extend beyond the line. Nonfinite/zero-length lines and invalid stops/radii are rejected.

One shape uses two existing32B rectangle slots: bounds/radius/endpoint colors, then origin/direction/middle stop/color. The LinearGradient command interprets both as one instance. Ordinary primitives do not grow; odd slot prefixes need no padding. Existing static/dynamic streams, compiled layers, transforms and opacity are reused.

Generated Fluent ProgressBar and typed immediate indeterminate progress use this common primitive. One color field combines background and moving color under the existing analytic rounded mask, replacing a separate background and two mesh quads. Determinate/reduced-motion behavior is preserved.

| Same256 gradients | Previous mesh | Shared primitive | Reduction |
| --- | ---: | ---: | ---: |
| Actual draw calls | 512 | 1 | 99.80% |
| Dynamic geometry upload | 61,440B | 16,384B | 73.33% |

Pixels match exactly. Full .NET10 validation passes954 checks; shared managed/fixed-RC3 GPU tests pass45 and input tests62. Three independent Native processes alternate comparison order. Tests cover MSAA1x/4x, odd slots, rounded masks, static compilation, depth order, opacity, color/stop/translation/clip changes and acquired-image retention. Unchanged gradients issue zero draws/uploads. The net8/9/10 Release solution builds with zero warnings/errors.

Historical323 images are400x300; current images are320x240, so direct comparison is invalid. A fresh preserved-original-shader reference at the same size matches all323 existing images exactly. Only new gradient/Progress GPU tests are excluded from that reference run. An initial test counted empty commands; it now counts actual draws.

Original SPIR-V is preserved. Separate candidate compilation, spirv-val, disassembly, correctness and geometry-cost checks precede promotion. Vulkan1.2/SPIR-V1.5 remains unchanged. Native runs requesting Khronos validation have empty stderr. Actual hardware is NVIDIA RTX5070 Ti Laptop; AMD hardware, occupancy and vendor timing are unverified.

Evidence: `artifacts/rounded-gradient-2026-11-05/verification.json`. Draw/upload reductions do not establish whole-app FPS, frame distributions, Private/working-set or Pi gains. Additional shader interpolation cost, previous residency costs, the full RC3 physics failure and complete Fluent fidelity remain open.

Run `dotnet run --project tests/Duxel.Rendering.Tests -c Release -- <output-directory>`. `tests/Duxel.Graphics.Tests` shares GPU bodies across managed/Native; append `packed-first` to reverse order. Rendering.Tests `--legacy-rendering-reference` excludes only new gradient/Progress GPU tests for preserved-original-shader comparison.
