# Mixed-weight inline text

`UiInlineFlowWidget` and text-only `MessageBarBody` also consume the shared native paragraph. Mixed-weight/font bidi context and wrapping cross child boundaries while child ownership and accessibility identities remain distinct. Transformed/margined children and atomic input/link widgets occupy native object allocations while retaining their normal rendering, input and focus lifetimes. `UiInlineFlowWidget.RightToLeft` selects base direction and defaults to false. Equal assignments retain layout; changed values invalidate it. Explicit RTL requires native paragraphs and direct text. MessageBar forwards its direction option to the standard body. The Typography tab exposes **자식을 소유한 문단**. Linux qualification of this new path requires Ubuntu recovery.

`UiInlineText.RightToLeft` selects explicit paragraph base direction and defaults to `false`. Set it in an initializer or change it on the existing widget; source text and owned runs are preserved. A changed value invalidates the native paragraph, while equal assignment retains it. Physical alignment remains the parent's responsibility. Explicit RTL requires a native paragraph backend with direct text enabled; unsupported requests fail instead of silently changing semantics.

```csharp
using var paragraph = new UiInlineText([
    new("abc אב ", UiInlineTextWeight.Bold), new("גד regular 123")
]) { RightToLeft = true };
paragraph.RightToLeft = false;
```

The Typography Observer exposes **오른쪽에서 왼쪽 문단**. Current Windows qualification passes70 native/request/Core checks,128 exact native GPU pairs and40 actual Observer scenarios including revealing and toggling that switch. Linux source is connected; its new NativeAOT qualification is pending because Ubuntu currently reports filesystem I/O errors. The previously qualified `e529777d` delivery remains selected; it does not contain this new switch. See [current execution state](../artifacts/fluent-messagebar-2026-11-15/literal-direction-verification.json).

Windows and Linux now use the [native styled-paragraph snapshot](styled-paragraphs.md) for literal layout and painting. Windows qualification covers64 exact whole-native GPU pairs and45 request/native/Core checks. Linux NativeAOT passes351 styled,334 original geometry and114 text-baseline checks; delivery is `e529777d`. Both Typography Observer samples are available through their normal launchers. Other consumers remain open; the older counts below describe their original scope.

`UiInlineText` is a shared infrastructure widget for regular (400), semibold (600), and bold (700) spans in one Body1 text root. It supports continuous sentences such as a MessageBar title and body. It does not add a new official Fluent component or increase catalog completion counts.

```csharp
using var content = new UiInlineText([
    new("Connected. ", UiInlineTextWeight.Semibold),
    new("You can continue your work.")
]);

// Use inside UiScreen.Render.
content.Render(ui);
```

Replace `Runs` to update content. The widget copies the input list and exposes a read-only snapshot. `Text` is the concatenated literal source. It does not interpret `#`, `**`, or link syntax as markup. It is a passive text widget, not a selectable editor; accessibility publishes the complete source as one static label.

Wrapping is enabled by default and uses the actual parent allocation. Explicit LF and CRLF line breaks are preserved. `Wrap = false` measures full natural advances without inserting ellipsis; painting outside the assigned root follows the parent clip. Common widget Margin, Transform, disabled state, and accessible names remain available.

Adjacent spans with the same face shape together, preserving combining characters and kerning across caller string boundaries. A face change must occur at a Unicode text-element boundary. Changing face inside an emoji or combining sequence throws explicitly. An invalid replacement leaves the previous content intact.

Source/style projection is built on content changes. Visual lines are retained while width, fonts, scale, and resource identity remain unchanged. Foreground-only changes do not remeasure layout. The widget requests no animation or periodic frames. These cache checks are not a universal measured speedup claim.

Focused evidence is recorded in `artifacts/fluent-messagebar-2026-11-15/inline-verification.json`:32 Windows managed model checks,64 existing text regression checks, and32 raw GPU pixel pairs against independently positioned regular/semibold/bold text. Cases include Korean, automatic wrapping, explicit newlines, Light/Dark, MSAA1/4, and retained repetitions. Linux native execution, Windows NativeAOT, complete paragraph bidi conformance, and the full MessageBar are outside this qualification. The protected Linux Observer delivery has not been replaced with this foundation change.
