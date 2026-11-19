# Generated Fluent Slider

Last synced: 2026-11-11

## Numeric accessibility — implementation and remaining gate

Generated/generic Sliders publish `UiAccessibilityRole.Slider` and `UiAccessibilityNode.Range` (`UiAccessibilityRangeInfo`): logical value, minimum/maximum, small/page changes and vertical orientation. Label supplies the accessible name; AccessibleName and Field naming retain their existing precedence. The Windows provider exposes RangeValue10003, control type50015, numeric properties30047–30052 and orientation30023. It does not advertise Invoke or string Value for a Slider.

`UiAccessibilityTree.RequestRangeValue(id, runtimeId, doubleValue, wait)` queues a typed request through the existing UI wake path. Rendering rechecks current bounds, enabled state, clipping and runtime identity before applying the same Slider snapping and binding callback. Nonfinite/out-of-range input fails; unchanged input produces no callback/event. Omitted step retains the existing one-unit behavior; explicit steps remain relative to Minimum, including the existing last-valid-step rule. Vertical/reversed travel does not reverse logical numeric bounds. `RangeChanged` reports committed immutable node state after publication and outside the tree lock; unchanged frames emit no range event.

Managed/direct-provider28 checks and the strict external client13/host3 checks pass. External coverage includes all six numeric ABI getters, shared snapping and callback order, idempotence, invalid/disabled writes, property events, range/orientation changes and retained getter rejection after removal. Retired child providers retain immutable COM configuration, runtime identity, fragment owner and null HWND-host association until UIA disconnection; numeric data and actions still reject removed elements. HWND root metadata expires with its host, preserving the asynchronous TooltipClosed snapshot lifetime. The final no-acknowledgment Tooltip shutdown regression also passes. Earlier failed probes are preserved in artifacts/slider-range-2026-11-11; lifetime-verification.json records the correction and current qualification. Narration, legacy Slider migration and NativeAOT remain separate gates.

Run `./run-gallery.ps1` → Fluent or SVG to inspect the real controls. SVG movement publishes the1px range and accepts numeric accessibility movement without XML/raster work. Generated and legacy Slider numeric access is qualified below; narration, complete legacy tab keyboard/flag coverage and NativeAOT remain open.

`UiSliderOptions.ShowStepMarkers` defaults to true. Set it to false to hide visual step markers while preserving the same discrete pointer/keyboard values. Observer SVG uses this for1px/1°/1-percentage-point adjustments so dense markers do not cover the continuous rail. The opt-out is checked against the unmarked GPU reference for both themes and MSAA1/4.

`Fluent.Slider(label, binding, minimum=0, maximum=100, step=null)` creates the inheritable UiFluentSlider. UiSlider<TBehavior,TPresentation> uses the same UiControlWidget lifetime/identity/input path. Override OnChanged or independently replace IUiSliderBehavior/IUiSliderPresentation. Source generation closes concrete types directly without runtime registries, reflection or theme selection.

Typed immediate API: `ui.Slider<UiFluentSliderBehavior,UiFluentSliderPresentation>(id, ref value, options, size)`. Label identifies the control; compose explanatory Text/Field separately. Full migration of nongeneric SliderFloat/VSlider and existing Dux.Slider compatibility APIs remains open.

UiSliderOptions specifies finite min/max, positive optional step, size, vertical/reversed travel. Omitted step uses one-unit input without markers. Explicit steps align to min, resolving ties upward and ending at the last valid step below max. Out-of-range model values are clamped for display only; actual input changes invoke one callback. The binding getter runs once per rendered frame.

Input covers track clicks, dragging beyond root bounds, Tab/Shift+Tab, arrows, Home/End, PageUp/PageDown, disabled/removal cancellation. Space/Enter do not activate it as a button. Edited deactivation survives drag release; removal clears shared capture.

Small uses16px thumb/2px rail/24px root; medium20/4/32, minimum axis120px. Rendering covers vertical/reversed, steps and hover/pressed/disabled/focus. Marker emission is clipped while preserving phase; periods<=1px form a continuous covered surface. Existing analytic circles/rounded rectangles, layers, caches and damage are reused. Slider state colors now use the pinned Fluent tokens described below; complete material/browser-pixel fidelity, high contrast/OS settings and Field/UIA remain open.

Pinned official commit8830c10bfb2f4c1bdd540287b09713b9679cd1ad provides react-slider API and useSlider/useSliderState/useSliderStyles sources. Sources/URLs/hashes are under artifacts/fluent-slider-2026-11-05. CSS sizes/slots and native range state were inspected.24 discovery names,289 known requirements and265 missing names are not final overall coverage/conformance.

Validation: fullNET10 995, shared managed/fixedRC3 Native99 checks,423 identical preceding images and five new Slider images. Tests cover inherited callbacks, pointer/keyboard, removal while dragging, steps, reversal, vertical input, invalid values, custom presentation and generated/immediate GPU equality. An initial test incorrectly assumed a separate circle buffer; it now checks actual negative-radius rectangle slots. Whole-app timing/allocation/Private gains are unmeasured. See [execution guide](widget-gallery-run.md), mode `--slider`.

## Hover color correction — 2026-11-11

The neutral button hover-slot mapping has been removed. `UiFluentSliderPalette.Resolve` reuses existing compound-brand colors to distinguish rest, hover, pressed and disabled fills for both the filled rail and thumb. Fluent8830c10b `colorCompoundBrandBackground`, Hover and Pressed resolve against actual `brandWeb` values, not stale source comments. Dark rest/hover/pressed are #479ef5/#62abf5/#2886de; disabled is #5c5c5c.

Remaining rail, thumb border, inner surface and step markers use the Slider's neutral tokens. `UiSliderPaint` separates Rail/Fill/ThumbBorder/Surface. The fixed ProgressBar below does not change color when the Slider is hovered. Input/value/layout contracts and frame scheduling are preserved. Forced colors, complete OS accessibility and full browser equivalence remain pending.

Run `./run-gallery.ps1` → Fluent → hover the progress Slider. Rest, hover and pressed use blue shades; disabled uses grey. Evidence: artifacts/slider-hover-2026-11-11/verification.json. An independent token fixture checks actual thumb/rail pixels and invariant ProgressBar pixels across Light/Dark, MSAA1/4 and stepped/unstepped sliders.


Legacy SliderFloat/SliderInt, scalar/vector/array wrappers, SliderAngle and VSliderFloat/VSliderInt share the typed numeric queue and live enabled/clip/range validation. Float values remain continuous: horizontal step is a keyboard increment, not a numeric snapping grid. Continuous controls report SmallChange0; integers report1 and retain ties-to-even float rounding. Angle publishes degrees and writes radians to its caller. Integer publication reflects final wrapper conversion. Existing float-based integer/double precision and keyboard/focus behavior are unchanged; no full-width precision or new Focus capability is claimed. Legacy tab selection/panel relationships are qualified separately; full keyboard/flag coverage remains open. Focused legacy16 (combined44), Observer127, external legacy client6/host3 and Observer GPU47 including actual pointer-selected Immediate controls pass. Run ./run-gallery.ps1 → Existing API → Hello / Immediate. Evidence: artifacts/slider-range-2026-11-11/legacy-verification.json.

2026-11-12 — Known deterministic leaf presentations now participate in the existing parent's measurement cache while rendering/input continue. Custom traits remain opt-in; label/options/size/items and environment changes invalidate layout. See [common leaf measurement contract](widget-cpu-retention.md) and artifacts/leaf-layout-retention-2026-11-11/verification.json. Full NativeAOT/performance gates remain open.
