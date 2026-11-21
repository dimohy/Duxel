# Standalone labels

Native qualification: `artifacts/label-relationships-2026-11-12/verification.json`. An owned Win32 fixture and separate UIA client verify Label pointer focus, Name change events, Required metadata, Value editing, detach and disabled/reenabled targets, plus the actual `LabeledBy` provider identity and removal event (16 checks). CPU Label40, all46 managed contracts/7844 assertion executions, GPU28 and33 unchanged baseline images pass separately. Earlier text-event evidence remains under `artifacts/label-native-2026-11-12/`.

[한국어](label-widgets.ko.md)

`Fluent.Label(text, target, options)` creates an independently laid-out label. Render the target separately; the label borrows it and never renders or disposes it.

```csharp
var editor = Fluent.Input("Original name", value.Binding, 256, 320);
var label = Fluent.Label("User name", editor, new(Required : true));
// In the existing render callback, or as separate children in a Column:
label.Render(ui);
editor.Render(ui);
// Dispose label before editor.
```

`UiLabelOptions` selects `Required`, `Disabled`, `Size` (Small/Medium/Large) and `Semibold`. Labels wrap to their available width without truncating source text. The required marker follows the last line with4logical units of spacing. Large uses semibold16/22typography; Small12/16 andMedium14/20 can use regular or semibold. Disabled text and marker use the neutral disabled foreground. See the [official usage guidance](https://fluent2.microsoft.design/components/web/react/core/label/usage) and [pinned8830c10b styles](https://raw.githubusercontent.com/microsoft/fluentui/8830c10b/packages/react-components/react-label/library/src/components/Label/useLabelStyles.styles.ts).

The target must implement `IUiLabelTarget` and have instance identity. Input,Textarea,Button,Link,Slider,Toggle-based Checkbox/Switch andRadioGroup implement the contract. One live standalone label per target is allowed; unsupported targets fail explicitly. Custom controls can opt in while preserving the standard primary-control identity and keyboard focus registration. This is a direct control association, not a label for arbitrary composed containers. Passing no target creates a plain categorizing label.

A pointer press focuses the target. If the label renders first, focus is claimed in that frame; if the target already rendered, one requested frame delivers focus. Disabled/clipped/blocked targets do not acquire deferred focus later. Detaching,retargeting,disabling or disposing the label cancels its pending request. This API transfers focus; it does not invoke buttons or toggle values. Label.Disabled changes the label only; disable the target separately if needed. The label adds no Tab stop, timer or hover animation.

The association supplies the target's accessibility name and required metadata. Explicit AccessibleName and enclosing Field label take precedence. Group child names remain independent. Every rendered Label publishes a nonfocusable `UiAccessibilityRole.Label` node (Windows UIA Text type50020), without Invoke/Value patterns or a duplicate content-view entry. When both widgets are captured in a frame, the target's `LabeledBy` points to that node, regardless of render order. Omission, detach and disposal clear the relation; clipping/disabled state preserve semantics. Relationship events use the existing post-publication snapshot contract. An association may still supply Name/Required while its label is omitted; no missing provider is referenced. Forced-color mode and full Fluent conformance remain unverified. A borrowed target must outlive its label; dispose/detach labels first. See [Microsoft Text guidance](https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-supporttextcontroltype).

The label keeps one bounded text-layout result (source<=1024characters,derived strings<=4096characters,lines<=256) under the existing resource/constraint key. Glyph observation disables retention. Invalidating text/options after a parent has measured it rebuilds the glyph content for that same frame; prepared parent geometry cannot substitute stale label text.

Run `./run-gallery.ps1` from the repository root and choose **Input and IME · combined**. Click **Notes** to focus the textarea. Buttons below it switch the label's required marker,size,weight,disabled state and long wrapped text. A disabled label leaves the separately rendered textarea enabled. GPU/contract evidence is under `artifacts/fluent-label-2026-11-12/`; see [validation commands](widget-contract-validation.md).
