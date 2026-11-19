# Input event batches

## Input and presentation phases

`NewInputBatch` processes logical events in order by default. Intermediate events use `UiRenderPhase.Input`. The last event and ordinary `NewFrame` rendering use `Presentation`. Screen callbacks, bindings, measurement, hit testing, editing, IME and accessibility run on each processed step. Draw-list builders and text painters suppress intermediate drawing and the context builds one final `UiDrawData`. Pending texture and pixel commands keep their order and ownership through the final consumer. Damage history compares final presentations.

`UiSliderOptions.CoalescePointerMoves` lets a slider use the latest position in a run of consecutive moves while it holds left pointer capture. The host processes the first input step of each batch before allowing coalescing and retains all press, release, keyboard, wheel, character and dispatch steps. Enable this option for preview bindings that only need the latest position. Gallery SVG transform sliders enable it, while other sliders keep the default behavior. `InputProcessingMetrics` records the original input count in `RequestedSteps` and the number actually processed in `CompletedSteps`.

During a read-only `UiCodeView` selection drag, consecutive moves within the visible text can also use the latest position. Moves crossing the clip edge are processed in order so edge scrolling and the selection anchor still work. Transformed controls and popup-blocked surfaces keep ordered traversal. Code selection and line numbers are drawn only on the presentation pass.

When native packets arrive before the UI worker drains them, the app appends their events to a pending list. The worker takes ownership of the list at drain time, so subsequent packets cannot modify it. The app drops its pending list reference at each drain. Ordered movement that returns to the last presented position still wakes the event-driven frame loop. [Input handoff measurements](optimization-session-2026-11-19.md#app-input-handoff-during-rapid-pointer-movement)

`ui.RenderPhase`, `ui.IsPresentationPass` and the corresponding control/builder properties expose the common phase. Custom code can condition pure painting on this value while keeping layout and interactive behavior outside that condition. A standalone builder remains a presentation producer. Pure `PaintGroup` callbacks capture only final content; layers do not commit empty intermediate caches. Failed intermediate callbacks report partial input work with zero built drawings.

The Observer counts FPS samples only during presentation and can show input steps versus built drawings in **Repaint observation → CPU stage measurement → Inspect measurements**. The Windows App already sends ordered native packets through this API. The Linux X11 backend also sends ordered native input. Runtime latency needs a separate measurement.

The focused cohort covers native/editing contracts, raw drawing categories, parameter failures, retained layers, paint groups and intermediate image resources. GPU comparisons cover final pixels against sequential full repaint with frame slots 1 and 3. This verifies the recorded scenarios, not every transformed interactive widget or real IME service. [Verification](../artifacts/goal-audit-2026-11-16/presentation-phase-verification.json).

```powershell
& W:/Cache/Duxel/presentation-phase-20261116/publish-265/Duxel.Graphics.Tests.exe W:/Cache/Duxel/presentation-phase-manual ordered-input-batch-only
```

## Unpublished drawing ownership

`UiDrawListBuilder.Build()` transfers and consumes pending output. Release each returned owned draw list with `ReleasePooled()`, then return the `UiPooledList` container with `Return()`; the container is not `IDisposable`. A second Build contains only newly generated output. Internal presentation/deferred appends use the same transfer contract. `CloneOutput()` copies the current writable chunk; it does not replay an already consumed drawing.

Reset, free-memory and deferred-discard paths now return unpublished owned output from every active/inactive channel while preserving borrowed retained buffers. Primitive command metadata is committed before channel storage is cleared. A consumer's previously transferred frame remains valid across builder reset and context disposal, with scene leases preserving retained content.

If a screen callback fails, the owning context discards unpublished base/overlay/unfinished-layer geometry, clears layer builder ownership and releases pending scene leases. It preserves the exception and does not replay the callback or discard queued texture/pixel commands through this geometry cleanup. The next frame can reuse the layer id. Context disposal uses the same ownership path. This discard path is now used by intermediate input passes.

The ownership fixture adds 23 checks and passes the final 168-check Core cohort. The original 163-check baseline had seven failures; five later guards cover pending primitive metadata in three discard paths, mixed channel ownership and a claimed frame surviving context disposal. Related pending-frame resource and raster lifetime checks pass; retained text/object GPU comparisons also complete. Run the focused cohort with a fresh directory:

```powershell
& W:/Cache/Duxel/input-phase-20261116/publish-254/Duxel.Graphics.Tests.exe W:/Cache/Duxel/unpublished-drawing-manual ordered-input-batch-only
```

[Ownership evidence](../artifacts/goal-audit-2026-11-16/unpublished-drawing-verification.json). These checks qualify lifetime cleanup, not completed input-only rendering or CPU speed improvement.

## CPU processing measurements

`UiContext.InputProfilingEnabled` enables opt-in timing/allocation measurements of each `Render`. `InputProcessingMetrics` reports requested/completed logical steps, built drawings, success, preparation time, UI composition time, drawing finalization time, total elapsed time and allocated bytes on the rendering thread. Counts remain available when profiling is disabled; timing/allocation fields become zero. Preparation includes native dispatch and immediate-context reset; composition includes callbacks, layout and paint generation. These measurements do not separate layout from paint, and exclude `NewFrame`, native delivery, GPU work and other threads. Total time includes profiling overhead and work between stages. Failed calls expose partial completed counts; stage durations cover only stages that finished.

From a screen, `ui.SetInputProfilingEnabled(enabled)` changes the next `Render`, while `ui.GetInputProcessingMetrics()` reads the last finished call. Both use the owning context's state and request no frame. This avoids a diagnostic feedback loop and does not change callback/event ordering. In the Observer, open **Repaint observation**, enable **CPU stage measurement**, then press **Inspect measurements**. The snapshot changes only when requested; the diagnostic is off by default.

The repeatable managed diagnostic uses 1/8/32 input events and 0/64 unchanged buttons, eight warmup frames and 24 measured frames per case. It checks exact edited text and callback counts. The synthetic glyph atlas and Core-only workload identify scaling costs; they do not establish real OS-font/native/Linux latency or the fixed RC3 NativeAOT performance acceptance. The plan caps execution at three independent diagnostic processes. [Input plan](../artifacts/goal-audit-2026-11-16/input-batch-cpu-plan.json).

```powershell
& W:/Cache/Duxel/input-cpu-20261116/publish-247/Duxel.Graphics.Tests.exe W:/Cache/Duxel/input-cpu-manual input-batch-cpu
```

## Windows focus loss and held keys

The Windows input backend owns the unique keys it has delivered as down, independently of transient poll events. `WM_KILLFOCUS` appends one ordinary key-up for each owned key and retires that ownership. Existing queued down/repeat events remain in order; a later down starts a new lifetime. An ordinary key-up removes ownership immediately. Repeated focus loss emits no phantom key-ups. IME focus dispatch remains separate and uses its existing owner contract.

The ordered App/Core path consumes these releases, clearing held state and engine repeat deadlines. The native aggregate snapshot also contains the releases. The hidden-window fixture adds 20 checks across Engine and Platform repeat sources, including cross-poll ownership, coalesced downs, same-packet down/loss/down and normal key-up. Before the fix 373/383 checks passed; the final cohort passes 383/383. This is controlled HWND-message evidence, not actual keyboard/TSF or complete focus lifecycle acceptance. No CPU/FPS improvement is claimed. [Evidence](../artifacts/goal-audit-2026-11-16/focus-key-lifetime-verification.json).

```powershell
& W:/Cache/Duxel/focus-keys-20261116/publish-243/Duxel.Graphics.Tests.exe W:/Cache/Duxel/focus-keys-manual native-ordered-input-only
```

For interactive observation, run `./run-gallery.ps1`, open **Input·IME**, hold an arrow key, switch to another application, release the key there, then return. The key should remain released until a new native down arrives. This manual path still requires user observation; the automated fixture sends messages only to its own unshown HWND.

`UiContext.AddKeyEvent` and `AddInputCharacter` append to the next `SetInput`/`NewFrame` input. Platform events precede injected events within each key or character list. Reusable storage exposes the current event count independently of capacity, so a smaller batch cannot replay a previous batch's tail. Without queued injection the platform list is used directly. Reusing the previous `GetIO()` list preserves its active prefix and appends once. Lists are borrowed frame data, not immutable snapshots.

Input, Textarea and MarkdownEditor share this corrected path. All 18 checks pass: shrinking batches, list aliasing, empty frames, key ordering, actual bound text and warmed allocation. The 1,024-call `SetInput` diagnostic reports 32,768 allocated bytes before and zero after the merge/keyboard traversal changes. This is not an FPS, full-frame allocation or fixed-RC3 NativeAOT performance result. Regression checks also pass: Input allocation 202, multiline 4,114, Observer 316, controlled native IME 313 and input-kernel GPU 400. [Evidence](../artifacts/goal-audit-2026-11-16/input-merge-verification.json).

Run the published fixture with a fresh output directory:

```powershell
& W:/Cache/Duxel/input-merge-20261116/publish-186/Duxel.Graphics.Tests.exe W:/Cache/Duxel/input-merge-manual queued-input-only
```

## Unpublished drawing resource lifetime

Replacing an unclaimed drawing through `Render`, `NewFrame` or `EndFrame` now transfers its texture commands to the next drawing in order. Texture creation stays before later updates, destruction and recreation. Pixel storage remains owned until the final consumer disposes the frame, explicit renderer cleanup, or context disposal. Once `GetDrawData` transfers a frame to its consumer, its commands are never replayed by the context.

The focused fixture passes 17 checks. Its original 13-check baseline failed eight checks because required texture creation was discarded. Four additional guards cover recreation ordering, both pixel owners, explicit cleanup and a visible-image reference. A 320×240 GPU readback after replacing an Image frame matches the directly rendered reference byte for byte, with SHA256 values recorded. This proves the tested resource path, not complete mixed-input ordering or cumulative damage correctness. [Evidence](../artifacts/goal-audit-2026-11-16/pending-frame-resource-verification.json).

```powershell
& W:/Cache/Duxel/input-order-20261116/publish-191/Duxel.Graphics.Tests.exe W:/Cache/Duxel/pending-frame-manual pending-frame-resources-only
```

## Ordered Core input consumption

`UiContext.NewInputBatch(frameInfo, inputs, clipRect, textSettings)` accepts a nonempty ordered list of complete `UiInputState` values. Call `Render(screen)` once, then claim and submit its final `GetDrawData()` result. Keep the list and its key/character lists unchanged until rendering returns. An ordinary `NewFrame` replaces a pending batch. Empty batches fail before host time advances.

Each state carries its own pointer/button/modifier state. Every step executes screen traversal, layout and real behavior; only the final presentation generates visible output and draw data. Host time advances once, queued resources remain ordered, and damage history compares final presentations. Failed callbacks are surfaced without replay. This does not skip arbitrary screen code or layout work.

```csharp
context.NewInputBatch(frameInfo, [textStep, pointerStep], clipRect, textSettings);
context.Render(screen);
using var drawData = context.GetDrawData();
renderer.RenderDrawData(drawData);
```

The original fixture passed 71 checks: 63 Core checks cover three real editors, both traversal orders, cross-editor focus, same-editor caret moves, Ctrl+A/text order, host time, multiple button clicks, empty input and callback failure; eight GPU checks cover four source/pixel pairs with frame slots 1 and 3. A candidate with reuse enabled and one final GPU readback matches sequential full-repaint reference pixels. The fixture does not assert that GPU reuse actually occurred or that reported damage is minimal. [Evidence](../artifacts/goal-audit-2026-11-16/ordered-input-core-verification.json).

```powershell
& W:/Cache/Duxel/ordered-input-20261116/publish-197/Duxel.Graphics.Tests.exe W:/Cache/Duxel/ordered-input-manual ordered-input-batch-only
```

App snapshot transport consumes this API for ordered producers. The Windows App facade and Windows Observer now enable native ordered capture. The low-level Windows backend retains explicit legacy compatibility by default. The Linux X11 backend publishes ordered steps. Owned input removal now retires IME targets through `ReleaseInput`; custom immediate-id hosts must call it when removing an input. Keyboard focus/lifecycle acceptance and input-only CPU optimization remain work. Already-lost order cannot be reconstructed from legacy aggregates.

The Linux X11 backend now captures one ordered step per event that changes input. Each step carries its own key transitions, committed characters, button edges, wheel delta, and capture-time pixel scale. Verify with `Duxel.Linux.App.Tests x11-ordered-input` on an available X11 display. The fixture sends events only to its own hidden window.

With the Linux Gallery running, the external test command `x11-gallery-search-input <gallery-pid> <fresh-result.json> <x11-window-id>` verifies a burst of X11 keys in SearchBox. It reads the text and caret back through AT-SPI and checks that the edit survives leaving the page and returning. Supply the X11 window ID of the test Gallery instance as a decimal number.

Use `x11-gallery-pixels` with the same arguments to save two 1280 × 900 RGB snapshots each of SearchBox, Overview, and RadioGroup. The probe requires the whole gallery client rectangle to fit on the X11 screen. Compare both snapshots from each build to identify pixels that can change between captures.

## App snapshot transport

`InputSnapshot.OrderedInputs` describes complete input states in native client pixels. Null selects legacy capture; an empty list selects ordered capture with no events; a nonempty list is the ordered packet. Capture mode remains fixed for an App session. Empty ordered packets cannot publish aggregate transient events: contradictory input fails before dispatch. Header pointer/button/modifier values continue to describe the current native state.

The App owns copies of header events, every ordered state and its nested event lists before the producer can reuse them. It appends pending packets in capture order, clears consumed events without mutating the worker's frame, converts every pointer by content scale, and calls `NewInputBatch` for nonempty packets. Ordered idle input uses the ordinary idle frame path. Glyph discovery reads the authoritative ordered states with the existing atlas discovery policy.

The focused fixture now passes 33 checks: 17 snapshot/contract checks and 16 checks across four real `DuxelAppSession.RunCore` sessions (scales 1/2 and both character/click orders). An injected producer feeds actual worker threads, widgets and Vulkan; completion is checked with a final draw callback followed by an increased successful QueuePresent counter. Sessions own independent Win32 message queues, stay hidden and send no global input. This is actual App pipeline evidence with synthetic capture, not real keyboard/IME or pixel acceptance. [Current evidence](../artifacts/goal-audit-2026-11-16/native-ordered-input-verification.json), [initial App evidence](../artifacts/goal-audit-2026-11-16/app-ordered-input-verification.json).

```powershell
& W:/Cache/Duxel/native-ordered-20261116/publish-212/Duxel.Graphics.Tests.exe W:/Cache/Duxel/app-ordered-manual app-ordered-input-only
```

## Repeat ownership

`UiKeyRepeatSettings.Source` selects who generates held-key repetition. `UiKeyRepeatSource.Engine` keeps the existing timer-based behavior for hosts that deliver physical edges; it is the default of the existing two-argument settings constructor. `Platform` consumes delivered key events without adding synthetic key timers. Windows system settings select `Platform`. Both modes consume actual delivered occurrences, including coalesced repeats; the source controls additional synthesis rather than filtering input.

`UiState.KeyRepeatSource` exposes the current choice. Switching it through input settings retires old key-repeat deadlines. `SetRepeatTime` rejects competing engine timers under platform ownership. The complete input batch validates the source before advancing host time. Engine-mode `IsKeyPressed(repeat: true)` now arms its initial delay on the first press, matching repeat-count queries and editor behavior. Mouse-held spin repetition and animation clocks keep their independent ownership.

```csharp
var native = new UiKeyRepeatSettings(.25, .04) { Source = UiKeyRepeatSource.Platform };
var manual = new UiKeyRepeatSettings(.25, .04); // Engine owns additional repetition.
```

Core checks expanded from 88 to 129. Before consumption was connected, 19 of 129 checks failed: platform-owned editors gained extra idle movement, query timers had the wrong owner, old deadlines survived source changes, and invalid choices were accepted. All 129 now pass across Input/Textarea/Markdown, both sources, queries, switching, key-up and Windows defaults. This proves the tested repeat policy, not complete keyboard focus/lifecycle acceptance or general input-only CPU processing. Linux native selection/runtime qualification remains separate. [Evidence](../artifacts/goal-audit-2026-11-16/key-repeat-source-verification.json).

```powershell
& W:/Cache/Duxel/repeat-source-20261116/publish-236/Duxel.Graphics.Tests.exe W:/Cache/Duxel/repeat-source-manual ordered-input-batch-only
```

## Native repeat counts

Windows preserves the low WORD repetition count of `WM_KEYDOWN`/`WM_SYSKEYDOWN` and `WM_CHAR`. Down-event records expose `UiKeyEvent.IsRepeat`: bit 30 marks an already-held key, and additional occurrences in one message are marked repeated. `IsKeyPressed(key, repeat: false)` and nonrepeating chords exclude these records; `GetKeyPressedAmount` still reports all occurrences. The existing three-argument key-event constructor remains valid. Direct synthetic messages with zero count retain their established single-event behavior. `WM_IME_CHAR` follows its native single-result count rather than multiplying composition commits. [Key flags](https://learn.microsoft.com/en-us/windows/win32/inputdev/wm-keydown), [character count](https://learn.microsoft.com/en-us/windows/win32/inputdev/wm-char), [IME result contract](https://learn.microsoft.com/en-us/windows/win32/intl/wm-ime-char).

The first UTF-16 unit retains both its input owner and multiplicity through later frames/focus changes. The decoder emits repeated complete scalars, never repeated unpaired surrogate units. Ordered capture copies the complete newly appended event range instead of retaining only its last element. IME expands repeated text into one buffer after duplicate checks; both a single-unit duplicate and a repeated string identical to the IMM result are suppressed. Lifecycle cancellation and removed/ownerless targets retain their existing semantics.

One native message remains one ordered logical step, with its occurrences in the existing event list. Checks cover zero/one/three/65535 occurrences, previous-key-state flags, Core queries and a constant composition count for a coalesced message. The 339-check baseline failed 37 cases; the corrected cohort passed all 339. Another 24 boundary checks bring the native cohort to 363. These verify message multiplicity and Unicode/IME routing, not complete keyboard-service acceptance, input-only CPU processing or measured FPS improvement. Broader focus/lifecycle and Linux repeat-source qualification remain separate work. [Evidence](../artifacts/goal-audit-2026-11-16/native-input-repetition-verification.json).

```powershell
& W:/Cache/Duxel/input-repeat-20261116/publish-232/Duxel.Graphics.Tests.exe W:/Cache/Duxel/native-repeat-manual native-ordered-input-only
& W:/Cache/Duxel/input-repeat-20261116/publish-232/Duxel.Graphics.Tests.exe W:/Cache/Duxel/ime-repeat-manual ime-owner-only
```

## Event time and frame time

`UiInputState.EventTimeSeconds` carries an optional, finite nonnegative event time on a stable input clock. Core validates the complete batch before advancing host time, consumes each timestamp once when rendering its logical step, and exposes `UiState.InputTimeSeconds` to input behavior. Click chains, held-key repetition, mouse hold duration, text dragging and spin-button repetition use this clock. Animations and scheduled frame deadlines still use `TimeSeconds`; spin deadlines are translated between the clocks. Without timestamps, the existing frame clock applies. After a timestamped event, idle frames project the input clock by their frame delta.

Delivery order can contain older timestamps. A negative click interval starts a new chain rather than a double click. The first key-down now arms repeat timing for single-line editing and repeat-count queries as well as multiline/sliders. Clearing a consumed timestamp prevents later frames from replaying its old time.

The normal Windows message pump scopes capture to `MSG.time`, the posting time, and unwraps its DWORD against native `GetTickCount64`. Sent messages outside this queued dispatch use native capture uptime. This preserves queue age and the native clock across DWORD wrap. It deliberately uses the native clock rather than `Environment.TickCount64`, whose Windows sleep accounting changes in .NET 11. [MSG contract](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-msg), [native uptime](https://learn.microsoft.com/en-us/windows/win32/api/sysinfoapi/nf-sysinfoapi-gettickcount64), [.NET clock remarks](https://learn.microsoft.com/en-us/dotnet/api/system.environment.tickcount64).

Core timing checks expanded from 71 to 88. The first 84-check baseline failed eight click/repeat/validation assertions; after those passed, four added editor/query/idle checks exposed two first-press repeat failures. All 88 now pass. The earlier timing cohort passed 224 native checks (218 prior plus six queue/clock/transport cases), and native App integration remains 37. These are input correctness checks, not CPU-throughput or full keyboard-service acceptance. Each logical step still composes UI. Complete keyboard focus/lifecycle, general input-only processing remain work. [Evidence](../artifacts/goal-audit-2026-11-16/input-event-time-verification.json).

```powershell
& W:/Cache/Duxel/input-time-20261116/publish-224/Duxel.Graphics.Tests.exe W:/Cache/Duxel/input-time-manual ordered-input-batch-only
& W:/Cache/Duxel/input-time-20261116/publish-224/Duxel.Graphics.Tests.exe W:/Cache/Duxel/native-time-manual native-ordered-input-only
```

## Windows native ordered capture

`DuxelWindowsApp` enables ordered input for its App consumer, including the Windows Observer. Restart the Observer with `./run-gallery.ps1` to use the rebuilt runtime. Existing running generations retain their loaded binaries. A custom low-level host can set `WindowsPlatformBackendOptions.EnableOrderedInput = true`, bind that backend's IME handler, and consume `OrderedInputs` through App or a correctly scaled Core input batch. The low-level option defaults to legacy mode for existing aggregate-only hosts; it does not switch modes after errors.

Capture reads flagged IMM strings once on the HWND owner thread. Immutable `IUiInputDispatch` payloads apply on the UI thread before their logical step is rendered. The first UTF-16 unit keeps its logical input owner across a later pointer/focus change; an ownerless start never attaches later. Logical decoder resets do not reset the newer native capture stream. Native service session state remains separate, so an already-ended or replaced session cannot receive an old completion or caret request. A payload requires its originating IME handler and rejects a missing/foreign consumer before committing. [WM_CHAR UTF-16 contract](https://learn.microsoft.com/en-us/windows/win32/inputdev/wm-char), [composition flags](https://learn.microsoft.com/en-us/windows/win32/intl/wm-ime-composition).

Each native step carries its capture-time `CoordinateScale`; App converts and clears it instead of applying only the latest host scale. App also consumes recent committed metadata for glyph discovery after rendering, so that channel is released each host frame. Per-input commits remain independently consumed by their widgets. Standalone handler clients still own their recent-text consumption. The existing BMP atlas-discovery policy is retained; surrogate halves and controls are not added as glyphs.

The earlier native capture/lifetime cohort passed 218 checks: 90 editor/order/scenario cases with source/session assertions and removal-queue checks, plus foreign-consumer rejection and hidden-window checks. Scenarios cover text/click order, split UTF-16 across frames, ownerless/canceled sequences, click-before-START, combined flags, capture-time string snapshots, owner-thread completion, disposal and live hidden restoration. Owned disposal invokes the required `IUiImeHandler.ReleaseInput` contract. A native-producer App fixture passes 37 checks, including four actual worker/Vulkan sessions and recent-channel consumption. IMM extraction/completion/caret responses and pointer capture are controlled fixtures; actual keyboard/TSF/candidate-window pixels, broad timestamp/repeat semantics and CPU throughput are not certified. [Lifetime contract](windows-ime-routing.md#removed-and-hidden-inputs), [latest evidence](../artifacts/goal-audit-2026-11-16/ime-input-lifetime-verification.json), [initial capture evidence](../artifacts/goal-audit-2026-11-16/native-ordered-input-verification.json).

```powershell
& W:/Cache/Duxel/ime-lifetime-20261116/publish-220/Duxel.Graphics.Tests.exe W:/Cache/Duxel/native-order-manual native-ordered-input-only
& W:/Cache/Duxel/ime-lifetime-20261116/publish-220/Duxel.Graphics.Tests.exe W:/Cache/Duxel/native-app-manual native-ordered-app-only
```

## Legacy aggregate comparison

The snapshot has separate key/character lists and aggregate pointer state, without cross-channel event order. A controlled probe sends real `WM_CHAR` to an owned hidden window and stages a synthetic Core pointer press in both orders. It covers three editors and both widget render orders, without showing a window or changing global input/focus.

Of 49 checks, 24 raw-scalar checks and one hidden-window check pass; 12 of 24 routing expectations fail. Without the native IME handler, pre-click text goes to the new input; with the handler, post-click text goes to the former input. The probe exits nonzero and records expected/actual text:

```powershell
& W:/Cache/Duxel/input-merge-20261116/publish-186/Duxel.Graphics.Tests.exe W:/Cache/Duxel/input-order-manual input-order-probe
```

This retained negative comparison describes legacy aggregate capture, not the Windows App facade's new default. It is not actual keyboard/TSF/native-mouse acceptance. New Windows capture uses the real widget hit path with ordered App/Core consumption; it does not predict hits from accessibility bounds or retarget all text to final focus. Input-only CPU optimization and broader damage/service acceptance remain open.

## Event-to-event widget geometry

Real widgets verify that the common input phase does not reuse stale event geometry. Within one batch, a `Move` button changes the target button's margin, layout rotation, visual rotation about the top-left, scale, parent rotation or ScrollView offset. Subsequent clicks use fixed coordinates independent of accessibility bounds. Where the old point lies outside the new shape, it is rejected and the new point activates exactly once. Scaling keeps the old center inside the enlarged shape, so that case does not require rejection. Scrolling reveals an initially clipped button before clicking it.

Input, Textarea and MarkdownEditor verify pointer focus and character insertion after margin or rotation changes. Every case runs with both target/controller traversal orders. Buttons consume seven logical inputs and editors eight, each producing one final drawing. Button cases also check final accessibility bounds and stable identity.

On2026-11-16 the managed .NET11 cohort passes345 checks, including84 new geometry assertions. These new cases verify behavior, coordinates and final drawing, without claiming new GPU pixel comparisons. The existing four ordered-editor and six common-phase GPU pixel pairs remain in the passing cohort. Full widget-cache lifetime, actual OS IME service, Linux execution and fixed RC3 NativeAOT acceptance remain separate. [Evidence](../artifacts/goal-audit-2026-11-16/interactive-phase-verification.json).

```powershell
& W:/Cache/Duxel/interactive-phase-20261116/publish-270/Duxel.Graphics.Tests.exe W:/Cache/Duxel/interactive-phase-manual ordered-input-batch-only
```

## Input-only cache replay and presentation preparation

`ReplayLayerCache` retains frame deadlines, animation bounds and parent-layer text dependencies during input-only steps. It then leaves `EnsureReplay` command clip/opacity/translation adjustment and frame/scene leases to the final presentation. Input behavior is not skipped and empty caches are not installed. `BeginLayer`/`EndLayer` and `TryReplayLayer` share this replay route.

The extended reference disables layer caching and sequentially repaints every state in full. The candidate submits one final drawing per three inputs. Content revision, parent clip, opacity, translation,3D transform, nested content revision and texture updates with unchanged geometry run through three sequences with slots1/3. All42 changed-frame pairs and42 subsequent ordinary-frame cache replays match exactly. Each changed reference must also differ visibly from its preceding frame.

Before the fix,30 of42 ownership checks fail:70 unnecessary presentation owners are observed across input-only steps. After the fix all corresponding observations are zero. This proves removed preparation work in the fixture, not a time, allocation-byte or FPS improvement. Final cohorts pass input555,Observer355,pending-resource17,raster-lifetime10,paint-group30 and transform53 checks. The normal Observer acknowledges44 QueuePresent pages, without claiming pixel comparison. Linux verification is a host build only. [Evidence and limits](../artifacts/goal-audit-2026-11-16/cache-phase-verification.json).

```powershell
& W:/Cache/Duxel/interactive-phase-20261116/publish-274/Duxel.Graphics.Tests.exe W:/Cache/Duxel/cache-phase-manual ordered-input-batch-only
```

## Nested metadata and delayed frame submission

Input-only replay republishes cached-child deadlines at every logical step. Both `BeginLayer`/`EndLayer` and `TryReplayLayer` independently verify scheduling; an expired deadline rebuilds the child and schedules its next deadline. Parent translation, opacity and clip suppress hidden bounded animation and its frame request. A hidden final state does not inherit animation activity from an earlier input step.

A new parent independently adopts a replayed child's real raster-text dependency. Removing the child and adding unretained text beyond the ordinary cache limit preserves the parent's key. Removing the last parent releases retention without changing geometry, commands or pixel data already owned by held consumer frames. Final consumer disposal drains frame leases and retired geometry.

The GPU fixture receives the original frame without submitting it. An input batch then removes the layer and replaces its image. Original and replacement frames are submitted afterward, in order. With frame slots1/3, both match independent direct repaints exactly; an input-created texture survives to final submission. These are four pixel pairs.

On2026-11-16 managed .NET11 regression passes595 checks, including40 new metadata, visibility and lifetime assertions. All20 product-source hashes match the preceding verification, so Windows Observer and Linux host builds were not repeated. This does not certify all cache bounds, actual OS services, Linux execution or fixed RC3 NativeAOT acceptance. [Evidence](../artifacts/goal-audit-2026-11-16/layer-metadata-verification.json).

```powershell
& W:/Cache/Duxel/interactive-phase-20261116/publish-280/Duxel.Graphics.Tests.exe W:/Cache/Duxel/layer-metadata-manual ordered-input-batch-only
```

## Restoring default locks after validation

The `input-cache-gpu-only` profile isolates GPU input-cache checks. Test frames use the owned window's actual framebuffer extent. A 320×240 window request yielding 400×300 at 125% DPI therefore no longer submits a stale fixed-size frame. Pixel comparisons and cache expectations remain unchanged. Unavailable targets report requested,native and swapchain extents.

The final project operations after managed validation restore default Linux and SVG locks. Invoke `scripts/RestoreManagedValidationDefaults.ps1` with an explicit HTTP cache and fresh output directory. It validates absolute paths, available space, writability and pinned SDK11.0.100 before restoring Linux then SVG, checking exit codes and expected lock hashes. Cache placement applies only to child processes; user/machine settings, vulnerability auditing and warnings-as-errors remain unchanged. The HTTP-cache selection follows [official NuGet guidance](https://learn.microsoft.com/en-us/nuget/consume-packages/managing-the-global-packages-and-cache-folders).

On2026-11-16,L: had zero available bytes and audit-data writes failed with NU1900. The package-oriented `--no-http-cache` option still produced that failure. Auditing and warnings were not suppressed. The explicit W: validation cache completes both restores. Full disks, relative paths and reused output directories are rejected before restoration. Global L: space exhaustion remains unresolved. [Execution checks](../artifacts/goal-audit-2026-11-16/restore-cache-validation.json).

```powershell
./scripts/RestoreManagedValidationDefaults.ps1 -HttpCacheDirectory W:/Cache/Duxel/nuget-http -OutputDirectory W:/Cache/Duxel/restore-defaults-new
```

Choose a new output path for each run. This finishes managed validation; it does not replace ordinary Observer launch or fixed RC3 NativeAOT performance acceptance.
