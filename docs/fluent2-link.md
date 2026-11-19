# Fluent 2 Link

Last synced: 2026-11-10

Link is navigable text with a destination and a mandatory application callback. Rendering never starts a browser or process. The application decides how to navigate relative routes or external URIs.

```csharp
var link = Fluent.Link("User guide", new Uri("/guide", UriKind.Relative), Navigate);
IUiView view = Dux.Link("Reference", new Uri("/reference", UriKind.Relative), Navigate);

[UiWidgetStyle(typeof(UiLink<,>), typeof(UiLinkNavigationBehavior),
    typeof(UiFluentLinkPresentation<UiFluentSubtleLinkAppearance, UiInlineLinkUnderline>))]
public partial class ProductLink;
```

Default and user widgets share `UiLink<TBehavior, TPresentation>` inheritance and source generation. Inherit the generated class to extend `OnNavigate(Uri)`. Properties are `Label`, `Destination`, `Navigate` and `Width`. Renaming display text preserves retained widget input identity. Disposal releases destination/callback/line references without disposing external objects.

The direct API uses the same input, measurement and painting. Retain `UiLinkContent<TPresentation>` to reuse measured lines.

```csharp
ui.Link<UiLinkNavigationBehavior,
    UiFluentLinkPresentation<UiFluentDefaultLinkAppearance, UiStandaloneLinkUnderline>>(
    "User guide", destination, Navigate, content, width : 160);
```

Pointer release and Enter navigate. Space does not activate navigation links. Each link uses one Tab stop and common focus, clipping, disabled state and capture cancellation on removal. Hover requests a hand cursor.

Select Default/Subtle/Inverted/OnBrand appearance implementations and Standalone/Inline underline implementations. Labels use Regular14/20. Standalone links underline hover/pressed/focus; inline styling also underlines rest. Keyboard focus uses double underlines on each line; disabled links remove underlines. Each line uses measured advance and the actual font baseline. Positive `Width` wraps; zero uses natural width with explicit newlines. Unbreakable words follow the common text minimum-width rules.

The layout key is shared with RadioGroup and includes context, fonts, size, scaling, text settings and backend generation. Context ownership is weak. Admission limits are1024 source characters,4096 additional line characters and1024 per font path. Additional line arrays are limited to256 slots, including empty lines. Replacement strings from custom presentations are counted by their actual retained length. Observers bypass even previously admitted layouts. Custom presentations default to no reuse and opt in only for deterministic measurement. Failed measurement clears partial layout and propagates the exception.

The [official Link usage](https://fluent2.microsoft.design/components/web/react/core/link/usage) is marked Preview at retrieval. Reference commit8830c10bfb2f4c1bdd540287b09713b9679cd1ad; originals/hashes are in `artifacts/fluent-link-2026-11-06/plan.json`. Existing actual brand/grey tokens are reused rather than stale color comments.

The `--link` gallery exposes eight presentations and actual internal-page navigation/back. Managed .NET10 and fixed .NET11 RC3 NativeAOT are separate. Current checks, pixel comparisons and source/executable hashes are in that directory's `verification.json`. Beyond generated/direct equivalence, tests inspect actual ink for every state and inline rest/hover/disabled/focus differences. No speedup is measured or claimed.

Official browser pixel parity and exact font underline metrics, UIA link lists/accessible names, forced colors/logical RTL, text selection, visited history/context menus, disabledFocusable and mixed inline paragraph composition remain incomplete. Inline underline styling does not establish those capabilities. Whole Fluent, common engine and Pi missions remain active.

## Measurement and parent allocation — 2026-11-10

`UiLink.MeasureContent` uses `UiControlContext.MeasureLink(label, content, width)`. Measurement uses the existing bounded `UiLinkContent<TPresentation>` and typography key without processing input or invoking navigation. A constrained parent supplies the real wrapping width, including when the natural Width is 0 or 220. Standalone rendering and the existing immediate overload retain their previous natural sizing and Width rules.

```csharp
using var link = Fluent.Link("Read the guide", new("/guide", UriKind.Relative), Navigate, width : 220);
using var box = Dux.SizedBox(link, 80, 64);
// The actual root and pointer/keyboard bounds are 80 x 64.
```

`IUiLinkPresentation.Arrange(context, natural, allocatedSize)` explicitly accepts changed sizes; its default throws. The built-in implementation changes the local layout copy's Size and preserves its text block, glyph size, baseline and underline metrics. The retained natural layout is never overwritten by an arranged copy. Same-width measure/render phases share it, height-only changes do not rebuild lines, and actual-width changes invalidate wrapping once. No widget/context fields or new caches are added.

Rendering advances by the actual allocation, passes that root to input and paint, and clips text, underlines and focus decoration to it. Clipped/zero roots cannot activate outside their visible region. Table/column routing is scoped and restored. An explicit immediate overload accepts `content, width, constrainLayout : true`. Invalid natural extents or an Arrange result with the wrong Size fail before input behavior runs.

Current fixed .NET11 RC3 managed validation passes 495 checks: CPU/input/lifetime 61, existing GPU 160, allocated pixels 232, paced retained pixels 40 and reuse budgets 2. Raster/vector, MSAA 1/4, Light/Dark and three frame slots are covered. The 673 readbacks include warmup. Pointer tests cover the allocated blank area, rename during capture, narrower/shorter roots and ancestor clipping; keyboard tests preserve Enter navigation, Space exclusion and zero-root focus removal. Existing cache admission, cross-context, observer and disposal checks remain in the run.

`layout_link_allocation_fba.cs` offers Width : 100, Height : 120, Short/Long and application-owned navigation status. Source-equivalent local-reference FBA compilation, first presentation and 15-second exit are recorded separately from manual input. Evidence and hashes: `artifacts/link-allocation-2026-11-10/verification.json`. Native and runtime image comparison for this revision remain unverified because standard VS C++ tool discovery is absent. Earlier Native evidence applies to its original revision. No FPS, memory or allocation improvement is claimed; whole-goal counts and resize deferral are unchanged.

2026-11-12 follow-up — [Content measurement contract](widget-cpu-retention.md): deterministic sources/presentations participate in parent retention; dynamic string/badge getters and custom traits default to fresh measurement. Declared getter Progress geometry and stable Spinner visibility are separate promises; rendering/getters/animation/navigation continue. Focused26 and exact GPU comparisons qualify this scope, not full NativeAOT/performance. Evidence: artifacts/content-layout-retention-2026-11-12/verification.json.
