# Fluent Card

[한국어](fluent2-card.ko.md)

Last synced: 2026-11-12

`Fluent.Card` currently provides the default passive content container. It owns one persistent widget tree; compose ordinary Row/Column content and real inputs, buttons or links inside it. Four focus modes and root-only disabled state are available. Bound Card selection is available; `Fluent.CardFooter(children, action)` provides structured footer content and a far-end action. `Fluent.CardHeader(header, description, image, action)` supplies optional named header slots. An immutable floatingAction slot is available; CardPreview is available through CardContent. Factory exposure does not establish complete official Card conformance.

```csharp
using var card = Fluent.Card("Work item",
    Dux.Column(new(Spacing: 8, MainAxisSize: UiMainAxisSize.Min),
        Fluent.Body1("Review the document"),
        Fluent.Button("Open", OpenDocument)),
    new(Width: 320));
card.Render(ui);
```

`UiCardSize` Small/Medium/Large selects padding8/12/16 and corner radius2/4/6. `UiCardAppearance` Filled/FilledAlternative uses neutral background1/2 and Shadow4; Outline keeps the backdrop visible with neutral stroke1; Subtle leaves the backdrop visible without a border or shadow. These default tokens follow the pinned source. The existing analytic rounded primitive and shared `UiBoxShadow` render the surface. No separate tessellation, shader or renderer is added.

`UiCardOptions.Width/Height` specify the body dimensions, including content padding. Without them, content determines the body size. The native layout reserves complete shadow support outside the body so neighboring allocations and clipping are explicit. That reserved outer space differs from CSS visual overflow; the accessibility group bounds identify the body. Actual tight parent constraints remain authoritative and may reduce the available body/content. Primary content stays within its padded allocation; floatingAction uses its independent top-right slot. Custom metrics may use zero padding; child painting and pointer acquisition respect the actual rounded body.

The root exposes one named native UIA Group, ClassName `Duxel.Card`, without editable Value. A passive root has no Invoke or keyboard stop. Children keep their normal providers, focus, pointer and editing behavior. `Label` supplies the group name; inherited `AccessibleName` overrides it. Label/options changes preserve owned instances and text state. Disposal releases the complete owned tree and shadow leases. A child cannot be owned by another container simultaneously.

`UiCard<TPresentation>` separates the compiled presentation from common ownership and box layout. `IUiCardPresentation.Measure` returns metrics; `Paint` receives interaction and paints the body. Measurement retention defaults false for a custom presentation; opt in only for metrics derived from options. The common single-child cache still depends on child measurement retention and configured layout resources. Ordinary content rendering/input continues on requested frames. Unchanged shadow pixels are reused; the Card schedules no periodic frames.

Run `./run-gallery.ps1` → **Card**. Change appearance, size and width; type into **Card note** and invoke **Action inside the card**, then change pages and return. The gallery shows all twelve size/appearance combinations. Current regression validation is under `artifacts/card-rounded-content-2026-11-12/verification.json`; the original passive baseline remains under `artifacts/fluent-card-2026-11-12/verification.json`. Original fixed RC3 NativeAOT/runtime-image/performance, full official interaction/visual/accessibility conformance and cross-device gates remain open.

References: [official Card usage](https://fluent2.microsoft.design/components/web/react/core/card/usage), [pinned Card props](https://github.com/microsoft/fluentui/blob/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-card/library/src/components/Card/Card.types.ts), [pinned styling](https://github.com/microsoft/fluentui/blob/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-card/library/src/components/Card/useCardStyles.styles.ts). Exact sources, token fixture provenance and MIT license are preserved in the evidence directory.


## Focus and root-only disabled state

`UiCardOptions.FocusMode` uses `UiFocusGroupMode`. Off skips the root and visits children. NoTab skips children when Tab leaves the root; Enter or a child pointer click enters a group whose internal Tab navigation wraps. TabExit enters the same way but Tab leaves after the last child. TabOnly visits children with Tab after the root. All three enabled modes support Enter into the first child and Escape back to the root. The entering Enter cannot also invoke the child button. Keyboard focus paints the common focus outline.

`Disabled = true` disables only the root's Enabled/Focusable semantics. Actual child buttons and inputs remain usable. The default presentation inherits disabled background, stroke and foreground colors. Filled cards paint Shadow2 while reserving Shadow4 space to keep the body position stable. Independently styled children keep their own colors. Custom presentations have `ContentForeground` and `UiCardMetrics.ReservedShadow`; reservation cannot be smaller than the painted shadow.

Run `./run-gallery.ps1` → **Card** → **Change card focus mode**, **Toggle the card surface**. Current evidence is `artifacts/card-focus-2026-11-12/verification.json`. Group boundaries resolve actual IDs after a TabPanel inserts its late navigation stop. Temporary ID maps release references after each frame and sparse metadata capacity shrinks. Nested NoTab forward/reverse navigation, staged Escape, actual input-owned IME composition/Korean commit and internal menu Escape/Enter priority are verified. All combinations and full parity with the original web implementation remain unverified.


## Root actions and interactive painting

Use `Fluent.Card(label, content, options, activated: Open)` or `card.Activated = Open`. Pressing and releasing the background invokes the root once. Owned buttons, inputs and menus handle their own input without also invoking the root. Dragging from background to a child or from a child to the background cancels root activation. The native root remains a Group and exposes real UIA Invoke. Disabled roots reject Invoke. `Activated = null` removes the action capability.

`FocusMode` now defaults to null (automatic): Off without a callback, NoTab with one. Explicit Off/NoTab/TabExit/TabOnly overrides that choice. Enter on a nonselectable action Card enters its first child without invoking root or child. Callback changes invalidate the derived layout/semantics.

Enabled action Cards use pinned official hover/pressed backgrounds, strokes and foregrounds. Filled cards paint Shadow8 on hover and reserve Shadow8 space in rest, pressed and disabled states to keep body/child positions stable. A pointer dragged in from outside cannot claim pressed ownership. Unchanged hover frames do not upload another shadow texture. The native reserved-space adaptation still differs from CSS overflow.

Custom `Paint(context, bounds, options, metrics, interaction)` receives `UiButtonInteraction`. `options.IsInteractive` is the read-only state computed by the widget from its callback. `Shadow` and `ContentForeground` also receive interaction. Dynamic shadow elevation must stay within its reserved maximum or fail explicitly. Derived single-child `LayoutOptions` must only depend on state invalidated through rebuilding.

Run `./run-gallery.ps1` → **Card** → **Toggle the card's own action**; compare background and child button execution counts. Current evidence: `artifacts/card-action-2026-11-12/verification.json`. Full conformance and original fixed RC3 deployment gates remain required work.


## Bound selection

```csharp
var selected = new UiState<bool>(false);
using var card = Fluent.Card("Document", Fluent.Body1("Document to review"),
    new(Width: 280), selection: selected.Binding);
```

`Selection` is a `UiBinding<bool>`; null disables selection. The caller's model survives mode changes. Read its current value with `card.Selection?.Value`. Presentation Options carry resolved `IsInteractive`/`IsSelectable`/`IsSelected` snapshots; the widget's own `Options` still stores caller configuration.

Selectable Cards do not install a focus group, regardless of explicit FocusMode. A real child Checkbox is the first Tab stop; Enter and Space toggle it, with Space committing on release. The default checkbox is visually hidden, matching the pinned source. Native accessibility uses body bounds for its selection/focus target and paints the body focus outline. No invented visible checkmark is added. Owned inputs, buttons and menus keep independent later Tab stops and actions.

Background release or Group Invoke writes selection once and then invokes optional Activated. Checkbox keys/Toggle change only selection. Child clicks, typing, Enter and menu actions do not also select or invoke the root. Background-to-child drag release cancels. Disabled blocks only root/selection, leaving children usable. External Windows clients use actual Checkbox Toggle and committed integer state-change events. Removed checkbox elements and retained Toggle providers return Unavailable.

Selected fills, strokes and hover/pressed precedence follow pinned tokens. Deferred pointer selection paints its new surface on the requested followup frame. No periodic timer is added. External values that affect custom metrics preserve the current prepared allocation, invalidate ancestors and request relayout; the new arrangement becomes retained again.

Run `./run-gallery.ps1` → **Card** → **Toggle card selection mode**; compare background/child actions and model/change counters, then switch pages and return. Selection baseline qualification: CPU55/8485(Card184,Observer210), twelve GPU profiles2433(Card448), native Card20/Avatar23;1108 prior images exact. Evidence: `artifacts/card-selection-2026-11-12/verification.json`. Full conformance remains incomplete.

## Footer slots

`Fluent.CardFooter([Fluent.Button("Open", OpenDocument)], Fluent.Caption1("Ready"))` owns both slots. Content keeps a 12px gap; the optional action uses the far edge of the available width. Children stretch to the footer height. Unbounded width uses the natural sum; insufficient width fails explicitly. Measurement is retained through the shared layout cache; child invalidation propagates to Card. The footer adds no accessibility group or keyboard stop. It schedules no animation. RTL/forced-colors and full Card conformance remain open.

Footer validation: CPU55/8506 (Card205,including21 Footer checks),GPU Card464/Observer63,nativeCard24.325 priorCardPPMs exact. See `artifacts/card-slots-2026-11-12/verification.json`.

## Header slots

`Fluent.CardHeader(Fluent.Body1Strong("Work"), Fluent.Caption1("Details"), Fluent.Avatar("Ada"), Fluent.Button("Info", ShowInfo))` owns all supplied slots. Every slot is optional. Paint, keyboard and accessibility order follows image/title/description/action. With description, two auto rows share the middle column; image/action span both rows and center vertically. Without description, the title grows in a centered flex row. Image has a 12px right margin and action a 12px left margin, including when the middle slots are absent. Header fills bounded width; an unbounded width uses intrinsic content plus margins, respecting explicit minima. Insufficient image/action space or combined text height fails explicitly. Existing child measurement retention, pointer routing and native control providers are reused. No timer or extra semantic group is introduced.

The row allocation follows the pinned [Header styles](https://github.com/microsoft/fluentui/blob/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-card/library/src/components/CardHeader/useCardHeaderStyles.styles.ts) and [CSS Grid auto-track distribution](https://www.w3.org/TR/css-grid-2/#algo-stretch). This does not establish full browser min-content, RTL, forced-colors or Card conformance. Run `./run-gallery.ps1` → Card; the Korean avatar, title, description and Card information button use these real slots. Header actions remain independent of root selection/invocation.

Header qualification: CPU55/8550(Card247 including42Header,Observer212),GPU Card492+Observer63,nativeCard27(host6/client21),337 protected prior Card images exact. See `artifacts/card-header-2026-11-12/verification.json`.


## Floating action

Append `floatingAction: widget` to `Fluent.Card` or `UiCard<TPresentation>`. The immutable slot owns a distinct widget, validated together with primary content before either is adopted. `Child` remains the original content; `FloatingAction` returns the auxiliary child. The slot does not enlarge intrinsic Card size. It measures against the body minus 8px and sits 4px from the top and right, independently of content padding. Ordinary content paints first and the floating control paints above it; keyboard and accessibility order put the floating control first. Pointer input covered by the floating allocation cannot activate obscured content. Higher popup tiers remain interactive.

For selectable Cards, provide a real checkbox bound to the same model. Any floating action suppresses the default hidden selection proxy; an arbitrary button does not acquire Toggle semantics automatically. Caller controls the floating widget's disabled state. Card.Disabled disables the root, while the supplied control and primary content keep their own state. Background selection uses the latest value from the binding captured for that frame; a child selection change does not cause a second model write. Deferred surface repaint requests a followup frame. The protected override is now `OnSelectionChanged(UiBinding<bool> selection, bool selected)` so implementations commit the captured binding even when a child changes `Selection` during the frame.

```csharp
var state = new UiState<bool>(false);
var action = Fluent.Checkbox("##select-document", state.Binding);
action.AccessibleName = "Select document";
using var card = Fluent.Card("Document", Fluent.Body1("Document to review"),
    new(Width: 280), selection: state.Binding, floatingAction: action);
```

Standard Fluent radii are supported. Custom radii use the [common rounded paint clip](rounded-paint-clip.md), including the fixed4px floating slot and zero-padding primary content. Full RTL/forced-colors/conformance and fixed RC3 deployment remain open.

Run `./run-gallery.ps1` → **Card** → **Floating selection action**. Toggle the right checkbox or Card background; the internal button changes only its execution counter. CPU55/8669(Card349,Observer214), GPU12/2509(Card524), native Card27/CardFloating30/Avatar23 and two independent614-frame rendering sessions pass. Evidence: `artifacts/card-floating-action-2026-11-12/verification.json`. These are managed runtime results, not NativeAOT qualification or measured performance improvement.


## Rounded content and pointer boundary

Card clips both primary content and floatingAction to its body with the common analytic rounded paint scope. Root background, border, shadow and root focus outline paint outside this child scope. Default Fluent padding/radii stay unchanged. Custom `UiCardMetrics` may now use padding smaller than radius, including zero; no artificial padding or radius reduction is applied. Radius clamps to half the actual body extent through the common shape policy. The former floating custom-radius rejection is removed.

Pointer acquisition follows the same rounded body, including nested cards and real floating controls. A clipped corner cannot activate/focus the root, child button or selection checkbox. A hidden selection proxy also cannot claim its ancestor's visible background click. The tier-scoped pointer guard permits higher popup builders to paint and receive input outside the Card. Root/button release beyond the curve cancels their pointer activation. Keyboard navigation and native Focus/Invoke/Toggle remain independent; accessibility bounding rectangles remain rectangular.

A fixed Card body does not automatically stretch every content widget. Use `Dux.Expand(content)` when zero-padding content should fill the body. The sample uses an explicit custom presentation with padding0 and radius32; its toggle changes radius to0 without changing allocation.

Run `./run-gallery.ps1` → **Card**, scroll to **Rounded clipping and input**. Click the real **Action inside the rounded card** button, try its clipped corners and toggle **rounded corners**. The counter and choice survive page changes. The sample remains reachable through the shared ScrollItem provider. Evidence: `artifacts/card-rounded-content-2026-11-12/verification.json`. Independent GPU reference comparisons allow one channel unit for centered/absolute SDF interpolation at AA boundaries; unchanged replay remains byte exact. Full official conformance and original NativeAOT/runtime-image/performance/device gates remain open.


`Fluent.CardPreview(content, logo)` owns media and an absolute32px logo placed12px from the left/bottom. The logo does not contribute to intrinsic size. Put direct slots in `Dux.CardContent([...])` as the Card's primary child (composition-only wrappers are supported). Preview bleeds through the Card padding: vertical left/right and first top/last bottom; horizontal top/bottom and first left/last right. `UiCardOptions.Orientation` defaults to Vertical. Horizontal final Header/Footer grows into remaining bounded width. Other custom child trees keep their own layout. Main-axis overflow fails explicitly; CSS flex-shrink/min-content conformance remains open. Preview content and logo retain their actual input/accessibility providers; Card border paints above Preview. Replays keep measured allocations without scheduling periodic frames.

Run `./run-gallery.ps1` → **Card** → **Media preview**. Compare vertical/horizontal SVG cards, toggle preview width/size, and inspect the fixed logo and full-bleed edges. SVG is loaded once per content lifetime. Evidence: `artifacts/card-preview-2026-11-12/verification.json`. The46 exposed factories still connect23 of55 top-level names; this variant is not another top-level completion.


CardPreview qualification: full managed CPU55/8754(Card407), affected Observer followup223 after test-only additions, twelve GPU profiles2673(Card608), native Card27/CardFloating30/Avatar23, hidden/visible application sessions614frames each. Of2316 unchanged-baseline images,2300 are exact and16 differ only in the dynamic HUD above y32; changed gallery and new images are separately qualified. Ordinary preview was not launched. Evidence: `artifacts/card-preview-2026-11-12/verification.json`. Full official conformance and original RC3 deployment/performance gates remain open.


2026-11-12 minimum allocation correction: CardContent and direct vertical CardPreview preserve the Card body's minimum after shadow/padding deflation. Vertical Header/Footer first resolve their intrinsic width before the body stretches; a horizontal final Header/Footer receives remaining minimum width even when the maximum is loose. Preview keeps its natural main-axis extent; horizontal media centers within the minimum height. The prepared parent-input snapshot keeps replay stable. Run `./run-gallery.ps1` → **Card** → **Minimum size of the parent**; change parent minimum width and orientation. This uses an actual constrained parent without a fixed Card width. Full CSS min-content/flex-shrink conformance remains open.

Evidence: `artifacts/card-minimum-allocation-2026-11-12/verification.json`. Nine initial contract failures were reproduced; after both minimum propagation and intrinsic-slot correction, full CPU55/8772(Card419,Observer226), GPU12profiles2725(Card656), native80 and hidden/visible614-frame sessions pass. Of2332 existing RGB image baselines,2312 are exact and20 differ only in the dynamic HUD above y32;32 new captures and the changed Observer gallery have independent geometry/pixel/replay checks. Timing improvement was not measured. Factory/top-level counts remain46 exposed,23 connected of55. The original deployment/performance/full conformance gates remain open.

Additional in-flight regression: the Card GPU profile also passes656checks with requested MaxFramesInFlight3, giving13profiles/3381checks in total. All517 matching captures between requested2/3 slots are RGB byte exact. This does not measure actual simultaneous frame count.

## CardNudge

`Fluent.CardNudge(title, description, open, action, options)` places a suggestion above the next content in an ordinary column. The card owns the optional action widget. The caller controls visibility through a `UiBinding<bool>`. Closing the card or swiping its surface at least 48 logical units horizontally writes `false`; a caller that rejects the write keeps the card visible. The action widget runs its own callback.

The close button is available to keyboard and assistive technology. `UiCardNudgeOptions.CloseLabel` localizes it. Supply `null` only when the surrounding interface provides another accessible dismissal action. With standard motion, the outgoing paint fades as its layout space collapses. `ReducedMotion` collapses it immediately.

Run the Gallery and select **CardNudge** under **Content and lists**. Use the reset button to try both swipe directions and the close button. The source below the example reflects the bound values. The interaction follows the [Fluent iOS Card nudge guidance](https://fluent2.microsoft.design/components/ios/core/cardnudge/usage).
