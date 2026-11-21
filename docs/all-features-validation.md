# Comprehensive sample validation (2026-11-04)

The sample is `samples/fba/all_features.cs`. Component Gallery and Applied Samples expose 37 feature windows; this is not an exhaustive inventory of every current API.

Run `./run-fba.ps1 samples/fba/all_features.cs -NoCache`. When older outputs are protected, specify `-ArtifactRootDirectory 'W:/MyWorks/Duxel/artifacts/all-features-2026-11-04/native'`. The dedicated directory must be below repository `artifacts`; the root itself, external paths and Managed mode are rejected before execution. Ownership checks and retention limits remain active. Protected older outputs were preserved.

SDK 10.0.301/.NET10 NativeAOT publication and the real Windows window were verified. All 37 windows rendered through the compiled sample and Vulkan; six state transitions cover button, checkbox, slider, input, combo and popup. Text input uses UiInputState and the Windows IME committed-input boundary, not physical keyboard IME automation. Evidence is in `artifacts/all-features-2026-11-04/`, including `views-latest/views.json`, PPM images and `inputs-latest/interactions.json`.

The final .NET10 regression suite passed 715 checks and the Release solution build had zero warnings/errors. Separate NativeAOT publication retains ILCompiler NU1903 warnings. Progress label clipping and overlapping column text were corrected; common-engine `TextWrapped` now respects the current column's available width. Existing uncommitted work was preserved. Final window and interaction results are in `views-completed` and `inputs-completed`.

FPS measurement previously dropped frames sharing one millisecond. High-resolution Stopwatch timing now counts them. This fixes measurement accuracy, not renderer throughput. Render-call FPS includes idle/input waits. Default MSAA4 and serial rendering differ from earlier RC3 speed measurement settings. This .NET10 functional validation does not replace pinned .NET11 RC3 performance comparisons.

Retained damage rendering reads reacquired swapchain pixels, so it now creates the swapchain with `clipped=false`; clipped pixels otherwise have no preservation guarantee. Present-completion semaphores are now reused by swapchain image rather than frame order, following the [Vulkan guide](https://docs.vulkan.org/guide/latest/swapchain_semaphore_reuse.html). These correctness changes alone did not resolve the symptom.

The user observed missing FPS text, while the NF line remained visible, when holding the left mouse button above 3000 FPS. Both full and partial redraw, and both managed and NativeAOT execution, reproduced it. Updating both lines every frame passed 1024 synchronous and 1024 in-flight frames (3074 checks). Holding FPS content for 16 frames while changing NF content every three frames reproduced stale pixels on asynchronous frame 1.

The common retained layer batcher cache compared geometry fingerprints but omitted draw-command state. Its source identity now also includes command state (texture, clip, opacity and offsets). Caching, partial redraw and quality remain enabled. The same 128 synchronous and 128 in-flight frames passed all 386 checks after the fix. A focused regression also checks command-only invalidation with identical geometry. The user confirmed that the final NativeAOT window no longer flickers while holding the button. Before/after evidence: `overlay-promotions`, `overlay-key-final`, `regression-complete-fixed` and `completion-audit.json`.

Final executable: `artifacts/all-features-2026-11-04/native/all_features.exe`, SHA256 `37FEB8C1D4BE032F7F5D1EDD5C14420ABA6A4147C859E218F5B688DDC0E8A3E2`. Idle waiting can stop or lower displayed FPS; holding the button keeps the input-driven render loop active.

No throughput or Private-memory improvement percentage is claimed. Diagnostic profiles are single runs, not warmed three-run median comparisons. Earlier rejected cleanup targets and manual instructions remain in cleanup guidance; validation proceeded without deleting them.
