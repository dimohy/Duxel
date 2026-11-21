# HeadsUpDisplay

Run `./run-gallery.ps1` and select **Alerts and progress → HeadsUpDisplay**. Examples cover centered presentation, input blocking, automatic dismissal and four visual bodies, each with adjacent source following actual settings. NEW moves to this route. Catalog linkage is 47/55 with 8 missing, not full Fluent conformance.

```csharp
var hud = Fluent.HeadsUpDisplay(new(Label : "Saving"));
hud.Content.Tapped = () => hud.Hide();
var show = Fluent.Button("Show", hud.Show);
var example = Dux.Column(new(Spacing : 8, MainAxisSize : UiMainAxisSize.Min), show, hud);
```

UiHeadsUpDisplayWidget owns the centered portal; UiHeadsUpDisplayContent owns its visual body. Blocking and persistence default to true. Show/Hide and controlled Open/OpenChanged share the common overlay lifecycle. ShowSuccess/ShowFailure and nonpersistent presentations dismiss after one second. Rejected controlled dismissal is requested once, never retried every frame. Escape/background clicks do not implicitly cancel work.

Nonblocking mode preserves background interaction without stealing focus. Blocking mode uses common focus/return rules. Entry scales 1.3→1 and fades in; exit scales 1→.8 and fades out, both linear over 200ms. Input/accessibility retire immediately at dismissal; only frozen paint remains. Shared frame deadlines require no new timer/thread.

KeyboardHeight is explicit host-provided occlusion. A nonzero value reserves an additional 50-pixel gap and centers within the remaining region. The Gallery simulation does not open an actual keyboard; automatic OS keyboard detection is not implemented. Full occlusion creates no empty GPU capture or transition requests. Portal ReducedMotion also freezes the activity body.

Natural square sides range from 100 to 192 logical pixels. Icon edge: 36; horizontal padding: 12; icon/label gap: 20. With a label, top/bottom padding is 20/16; without one, 20/20. Corner radius is 4. Smaller parent allocations clip the natural square; zero allocation produces no paint or animation requests.

Labels are centered and limited to two lines. Font and line height shrink together to a minimum scale of .6, then overflow receives an ellipsis at a text-element boundary. Accessibility retains the complete original label. Prepared text/layout uses the shared measurement key rather than rebuilding every frame. `UiControlContext.BeginTypography<TTypeface, TMetrics>(float scale, bool hasText = true)` is an additive overload preserving the existing bool signature. Disposal restores outer font and line metrics.

Activity reuses the existing ActivityIndicator arc and timeline. Success/failure reuse Fluent vector icons. Custom accepts a `UiMenuIcon` and supplies the same 36-pixel slot and gray tint; the provided painter must honor those bounds/color arguments. System contrast colors take precedence. ReducedMotion keeps busy semantics while freezing rotation and stopping frame requests.

Without Tapped, the body exposes a read-only Image; with Tapped, it exposes an invokable, keyboard-focusable Button. Tapping only invokes the callback and never implicitly cancels work. Busy is true for Activity and false otherwise. Common AccessibleName overrides naming. Common portal focus/blocking lifetime has focused execution coverage; real assistive-technology announcements and all OS combinations remain open.

Official iOS light/dark backgrounds use grey14/grey24, icon colors grey56/grey72 and white labels. The desktop mapping covers these two palettes, not iOS darkElevated. Actual SwiftUI pixel comparison, full platform accessibility and keyboard-avoidance positioning remain open.

Validation: portal CPU20/Vulkan22 at MSAA1/4, actual Gallery5 and body/source17 cases pass. The shared exit-code change is covered by Dialog107; catalog4 qualifies registration. One Gallery predicate confused Content with ContentOptions constructors; after correction only its two source cases reran. Identical Core hashes permit reuse of prior portal results. Generic suites, close A/B and unchanged ActivityIndicator tests were not repeated. Latest build/restoration proof is in artifacts/fluent-heads-up-display-portal-2026-11-17/result.json. Actual Linux and pinned RC3 NativeAOT remain unrun. Earlier body emoji input stopped for a missing fixture glyph, not a qualified emoji implementation.

```text
Duxel.Graphics.Tests.exe <fresh-output> hud-portal-only
Duxel.Graphics.Tests.exe <fresh-output> hud-portal-gpu
Duxel.Graphics.Tests.exe <fresh-output> hud-gallery-only
Duxel.Graphics.Tests.exe <fresh-output> hud-gallery-source-only
```

Official sources: [body](https://github.com/microsoft/fluentui-apple/blob/main/Sources/FluentUI_iOS/Components/HUD/HeadsUpDisplay.swift), [tokens](https://github.com/microsoft/fluentui-apple/blob/main/Sources/FluentUI_iOS/Components/HUD/HeadsUpDisplayTokenSet.swift), [presentation lifetime](https://github.com/microsoft/fluentui-apple/blob/main/Sources/FluentUI_iOS/Components/HUD/HUD.swift), [colors](https://github.com/microsoft/fluentui-apple/blob/main/Sources/FluentUI_common/Core/Theme/Extensions/iOS/FluentTheme+iOS.swift).
