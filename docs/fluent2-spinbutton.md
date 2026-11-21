# SpinButton

The stepper palette is separate from geometry and pointer handling. With system contrast colors active, enabled arrows use ButtonText, disabled/limited/read-only arrows use GrayText, and hover/hold surfaces use ButtonFace. Rest and disabled surfaces remain transparent. This native adaptation retains the [official Fluent state/appearance structure](https://raw.githubusercontent.com/microsoft/fluentui/master/packages/react-components/react-spinbutton/library/src/components/SpinButton/useSpinButtonStyles.styles.ts) and [system color roles](https://www.w3.org/TR/css-color-adjust-1/#forced-colors-properties). Forty-eight GPU contracts cover 192 combinations of four appearances, two sizes, RTL/LTR, both arrows and six states, with exact pixel comparison against a separate literal-role presentation. Existing light/dark/MSAA captures remain identical across 293 images. Validation and OS/platform limits: [system contrast](system-forced-colors.md).

`Fluent.SpinButton` binds a nullable `double` to an editable number with two pointer steppers. The generated class is `UiFluentSpinButton`; custom presentations use `UiSpinButton<TPresentation>`.

```csharp
var quantity = new UiState<double?>(3);
var input = Fluent.SpinButton("Quantity", quantity.Binding,
    new(Minimum: 0, Maximum: 100, Step: 1, PageStep: 10,
        IncrementLabel: "Increase quantity", DecrementLabel: "Decrease quantity"));
var form = Dux.Column(new(Spacing: 8, MainAxisSize: UiMainAxisSize.Min),
    Fluent.Label("Quantity", input), input);
```

Editing keeps a draft until Enter or focus leaves. Escape restores the committed value. Up/Down step; PageUp/PageDown use `PageStep`; unshifted Home/End use declared bounds. The editor is the single Tab stop. Steppers also expose native Invoke actions without becoming extra Tab stops.

Pointer press steps immediately. Holding repeats after300ms, easing toward80ms intervals. Release, leaving the button, disabled/read-only state, bounds and rejected changes stop repetition. Reentering during the same cancelled hold does not restart it. A settled control schedules no periodic repeat.

The default parser accepts finite invariant numeric text, including exponent notation. Empty text means null. Commits clamp to bounds and round to `Precision` (0–15), inferred from `Step` when omitted. Midpoints round away from zero. External values must already be finite and within bounds; display preserves their fractions independently of step precision. A custom `Formatter` and `Parser` can implement units or culture-specific input; they must return valid single-line text and finite numbers. Successful custom parses still follow numeric bounds and precision. `Changed` reports requested and actual values plus acceptance; rejected bindings are read back.

Small/Medium use minimum heights24/32, with four `UiInputAppearance` values. `RightToLeft` mirrors stepper placement. `ReadOnly` disables editing and both actions. Field and standalone Label associations use the common input path. Native semantics use Spinner type50016, Value and optional RangeValue; empty values withdraw RangeValue.

Run `./run-gallery.ps1` → **SpinButton**. Try empty values, units, size/appearance/read-only toggles, typing, focus changes and holding the steppers.

References: pinned [behavior](https://github.com/microsoft/fluentui/blob/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-spinbutton/library/src/components/SpinButton/useSpinButton.tsx) and [styles](https://github.com/microsoft/fluentui/blob/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-spinbutton/library/src/components/SpinButton/useSpinButtonStyles.styles.ts).

The bounded commit and midpoint rules above are explicit Duxel contracts. Full React parity, forced colors, physical screen readers, complete bidirectional text and NativeAOT remain unqualified. No whole Fluent conformance claim.

Validation: artifacts/spinbutton-2026-11-13/verification.json. Managed65 suites/10,149 assertion executions; GPU899 checks including25 Observer pages; SpinButton frame-slot2/3 produces265 identical PPM files. Builds have zero warnings/errors. Counts include repeated cases. The owned Observer session passes4 checks; this does not measure idle power.


Pointer focus correction: clicking inside the same editor moves the caret without committing even an incomplete numeric draft. Entering a stepper commits only the stepped result; moving to another control or empty space commits once. Common focus dispatch publishes the final pointer target instead of its temporary empty state, and an aborted frame restores provisional owned focus.


Pointer correction evidence: artifacts/spinbutton-pointer-focus-2026-11-13/verification.json. The previous same-editor click failure is reproduced, then managed65 suites/10,171 assertions, GPU495 assertions and4 owned-session checks pass. Frame-slot2/3 has293 identical PPM pairs; all265 prior images remain identical. No performance or whole-goal completion claim.

