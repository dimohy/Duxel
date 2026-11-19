# Keyboard input and focus for generated controls

Last synced: 2026-11-05

Generated Fluent buttons, toggle buttons, Checkbox, Switch and inherited user widgets share the control keyboard path. IUiButtonBehavior selects behavior independently of IUiButtonPresentation/IUiTogglePresentation. Generated Button/ToggleButton types select Enter/Space behavior; Checkbox/Switch select the Space-only type. No runtime theme registry is searched.

## Behavior and lifetime

Tab/ShiftTab traverse enabled, visible controls from the preceding successful frame. Disabled/clipped controls are not registered, and only the highest registered popup tier remains in the order. Ctrl/Alt/Super+Tab are not consumed as ordinary Tab. Enter activates on key-down; Space maintains held state until key-up. Repeated Space down events do not duplicate callbacks. Repeated Enter events coalesce to at most one activation per frame under the existing bool result contract.

Pointer press transfers focus and hides its keyboard visual; Tab shows it again. Removal, disabling, Escape, clicking another control and UiContext.AddFocusEvent(false) cancel armed Space activation. Clicking an editor releases button focus; tabbing to a button releases the previous editor's active state. Existing IsItemActive/Activated/DeactivatedAfterEdit queries include Space state.

UiButtonInteraction adds init properties Focused and FocusVisible while preserving its three positional constructor arguments. User presentations can draw arbitrary focus indicators through these properties. UiControlContext.FocusOutline is a shared drawing helper, not certification of all official Fluent focus visuals, contrast or states.

Two tab-order buffers swap after successful frames and clear previous references. Capacity above four times current registration or64 slots shrinks after120 consecutive small frames; Dispose releases both buffers. There is no arbitrary registration cap that loses input. Reference/array residency scales with the screen and still requires whole-app memory/performance measurement.

## Official basis and validation

Checked on2026-11-05, Microsoft's [Button specification](https://github.com/microsoft/fluentui/blob/master/specs/Button.md) describes Tab focus and Enter/Space activation; its [Checkbox specification](https://github.com/microsoft/fluentui/blob/master/specs/Checkbox.md) covers Tab, Space and keyboard-only focus visuals. [Windows keyboard guidance](https://learn.microsoft.com/en-us/windows/apps/design/input/keyboard-interactions) distinguishes command Enter from selection Space. These are behavioral references, not substitutes for complete visual/accessibility validation.

.NET10 passed871 full checks,159 focused checks and66 generator checks; the net8/9/10 solution built with zero warnings/errors. All323 preceding images are identical. The new tests/Duxel.Control.Tests consumer links the same ControlKeyboardTests.cs and runs24 input/lifetime checks without a window or GPU. It does not duplicate the test body; JSON output also uses source generation.

Fixed .NET11 RC3 NativeAOT Speed(win-x64) performed actual native code generation and passed the same24 checks. SDK11.0.100-rc.3.26522.148/runtime11.0.0-rc.3.26522.148; executable SHA256 E16D9913A5374321302FD8117D75F204675F1F0C22312A6E91897CDD860798F5. Managed/native check names and order are compared. This validates actual common keyboard code in NativeAOT, not Windows event delivery, GPU visuals, UIA or performance.

An initial null-focus comparison incorrectly requested another frame on every idle render. The idle-before check failed before the fix and passed after requiring a real owned focus before release. This regression preserves event-driven static screens.

```powershell
& 'C:/Users/dimohy/.cache/duxel-dotnet-10.0.301/dotnet.exe' run --project tests/Duxel.Control.Tests -c Release -- './artifacts/control-check.json'
& './artifacts/widget-keyboard-2026-11-05/native-controls-rc3/Duxel.Control.Tests.exe' './artifacts/control-native-check.json'
```

## Remaining scope

Not all legacy inputs, sliders or composite controls participate in Tab order. New controls join after a successful frame; a newly disabled/removed previous target loses focus. Arbitrary same-frame key order, automatic scrolling, nested focus scopes/restoration, native focus-event delivery and full UIA require further implementation/validation. Default palette, all widgets and official state/visual migration remain incomplete.

The user-requested1,200-entity ERD window was published from the earlier keyboard code. Its executable was not overwritten and the live window was not closed after the idle fix. That startup is not keyboard or final performance evidence. The old RC3 GPU comparison failure, Private residency cost and unverified Pi hardware remain open.

Evidence: artifacts/widget-keyboard-2026-11-05/verification.json, controls-net10.json, controls-native-rc3.json, image-comparison.json, idle-before.stderr.log, source-hashes.json and erd-interactive-run.json. The full goal remains active.
