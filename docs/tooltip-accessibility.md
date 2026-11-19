# Tooltip target accessibility

Last synced: 2026-11-11

`UiTooltipOptions.Relationship` associates tooltip text with the primary semantic control owned by the target. The default is `Description`. `UiAccessibilityNode.Tooltip` publishes immutable `UiAccessibilityTooltip` metadata with `SourceId`, `Text` and `Relationship`, independently of `IsOpen`. `SourceId` identifies the logical tooltip source; a visible surface also uses this ID, while hidden help has no surface provider.

| Relationship | Target semantics | Windows mapping |
| --- | --- | --- |
| `Description` | Preserve the target's name and attach supplemental text | Append to target `HelpText`, preserving Field hint and validation text |
| `Label` | Use tooltip text as the name unless the child has an explicit `AccessibleName` or Field label | Target `Name`; label text is not appended as help |
| `Inaccessible` | Publish no tooltip relationship | Preserve the target's original name/help |

This follows the [official Fluent guidance](https://fluent2.microsoft.design/components/web/react/core/tooltip/usage) for descriptive text and the [Windows ToolTip contract](https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-supporttooltipcontroltype), which makes ordinary tooltip information available through the referring control's HelpText. [UIA property identifiers](https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-automation-element-propids) define HelpText as a BSTR and Name separately. The platform mapping here uses those target properties. [Visible ToolTip nodes and open/close events](tooltip-events.md) are now qualified. Full property-change/shutdown notifications and platform conformance remain open.

The primary edit claims its relationship before before/after adornments render, so adornment buttons retain independent names and help. Either Field/Tooltip nesting order preserves Field label, hint, validation, required and invalid state. The nearest nested Tooltip owns the relationship and wins equivalent visual requests. Completed or throwing scopes restore the previous context. A target composition with several independent primary controls still needs an explicit group/target contract.

Hidden, clipped and disabled controls retain semantic descriptions. Omission removes the target and invalidates old provider identities; returning recreates the provider while retaining the logical source identity. Text is captured when the target renders so deferred painting and published semantics agree within a frame; a later text assignment appears on the next target frame. Neither relationship mode mutates the child's `AccessibleName` property.

## Validation

Evidence: `artifacts/tooltip-accessibility-2026-11-11`. One original hidden-HelpText failure and a later frame-text mismatch in both CPU and GPU were reproduced before correction. Final fixed .NET11 RC3 checks include CPU 502 (29 new semantic checks), Tooltip GPU 288 (nested and frame-snapshot cases added), existing accessibility 40/lifetime 101 and the full retained input/container regressions.

The sample Footer button declares `Description` explicitly. A separate Windows UIA client discovers the running sample, reads the actual target's HelpText and Name, checks the neighboring Inset button and focusability, and confirms that description text is not a fabricated standalone control. This is a focused native-property qualification; full screen-reader behavior and tooltip event delivery remain open. Run the sample with:

```powershell
./run-fba.ps1 samples/fba/layout_custom_contract_fba.cs -NoCache
```

This extends the [generic target contract](tooltip-targets.md). `Fluent.Tooltip`, official positioning/arrow/shadows, controlled visibility, focus-origin/document visibility, current NativeAOT/runtime comparison and new-path performance remain unfinished.
