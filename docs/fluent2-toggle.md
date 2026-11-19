# Compiled checkbox/switch and input identity

2026-11-11 [Common Toggle accessibility](widget-accessibility.md): actual Checkbox/Switch/ToggleButton roles and Off/On/Indeterminate states use one canonical ToggleState, existing action/binding policy and Windows Toggle property events. Checked remains a two-state projection. Core21/Observer134/generators115, external Toggle11/host4 and checkbox-menu6/host3 pass;47GPU checks and73 unchanged bodies preserve rendering. Earlier shared peers update on the requested next frame. Full narration/forced colors/NativeAOT remain open. Evidence: artifacts/toggle-accessibility-2026-11-11/verification.json.

Last synced: 2026-11-11

## Persistent selection button follow-up

`Fluent.ToggleButton(label,binding)` and `Dux.ToggleButton(label,binding)` use generated `UiFluentToggleButton`. It inherits the same `UiToggle<bool,...>` value/input kernel as checkbox and switch with a separate button-shaped presentation. Immediate `ui.ToggleButton(label,ref value)` uses the same implementation. Pointer-held state remains separate from persistent checked state. Focused .NET10 checks cover inherited OnChanged, disabled input, label changes during activation, callback counts and checked/unchecked primitive colors.

The current default presentation uses the existing palette and medium-button measurement. Official secondary checked styles were reviewed, but complete color tokens, icons, sizes, appearances, keyboard, accessibility, focus, motion and visual comparison remain unfinished. A new catalog name does not certify full Fluent conformance. The pinned official reference is `react-button/library/src/components/ToggleButton/useToggleButtonStyles.styles.ts`; its source is preserved in `artifacts/fluent-toggle-button-2026-11-05/`.

The user confirmed a final common architecture that handles10,000 buttons effectively. Static generated binding alone does not prove this. Retained, scrolling, partial/full-change cases must measure CPU time, allocations, Private memory, uploads/draws, input correctness and code size. Those scale measurements have not run yet.

Fluent 2 migration remains in progress. Button, checkbox and switch implementation binding and input paths exist; complete components, official visual fidelity, keyboard behavior, accessibility and motion do not. The user observed no visible style change. Existing windows, fonts, palette and multiple presentations remain, and startup confirmation is not visual-conformance evidence.

## Shared contract

`UiControlContext` generalizes the earlier button-only context. `UiToggle<TValue,TValuePolicy,TBehavior,TPresentation>` separates value transitions, input policy and presentation. Compile-time bool and bool? policies support mixed(null)→true→false transitions. Bindings update once per activation; user subclasses can override `OnChanged`.

`Dux.Checkbox()` uses generated bool/bool? `Fluent.Checkbox()` overloads; `Dux.Toggle()` uses `Fluent.Switch()`. Direct `UiFluentCheckbox`, `UiFluentMixedCheckbox` and `UiFluentSwitch` instances share the same foundation and input path. Legacy immediate `Checkbox` and `Toggle` preserve their previous appearance/numeric styles through presentations connected to the same generic kernel.

The standard catalog contains Button, Checkbox, Switch and ToggleButton. Mixed checkbox has its own typed factory overload without duplicating the discovery name. These names describe implementation binding, not full official state conformance.

## General widget generation

```csharp
[UiWidgetStyle(typeof(UiToggle<,,,>), typeof(bool?),
    typeof(UiNullableToggleValue), typeof(UiReleaseButtonBehavior),
    typeof(UiFluentCheckboxPresentation))]
public partial class MixedChoice;
```

`UiWidgetStyle` generates an inheritable class from a concrete UiWidget implementation and closed generic arguments. It analyzes public constructor types, defaults and reference passing; standard factories consume the same contract instead of duplicating button-specific strings. Incompatible policies, behavior, presentation, open types and generic constraints produce DUXWGT004. Duplicate factory signatures and invalid exposure produce DUXWGT005. Legacy `UiButtonStyle` uses the same validation/generation contract. No runtime implementation lookup or reflection is introduced.

## Display text and input identity

A focused test reproduced lost activation when Label changed during a retained control press. Within instance or owned child-key scope, primary input identity is now independent of display text. Immediate calls and unowned Parent factories retain their previous label-based identity. No new ID string is concatenated per frame. Tests cover button/checkbox label changes during presses and keyed-child rebuild identity.

## Official references and remaining scope

[Official checkbox guidance](https://fluent2.microsoft.design/components/web/react/core/checkbox/usage) and Checkbox/Switch reference style/behavior sources are pinned at commit `8830c10bfb2f4c1bdd540287b09713b9679cd1ad`. Originals, URLs and hashes are in `artifacts/fluent-toggle-2026-11-05/sources.json`. Medium checkbox16 and switch40×20 dimensions were checked; icons, all tokens/states, fonts, animation and actual visual matching remain incomplete. Keyboard, focus and accessibility need separate implementation/verification.

## Verification and observed failure

.NET10 full regression passed787 checks; generator checks passed50; the net8/net9/net10 solution Release build had zero warnings/errors. New fixed RC3 NativeAOT and managed FBA consumers presented their first GPU frame and exited0. Native SHA256: `7A84F1A70C4AC282E11E6B1176B0522A3593EB082FBAFD3EAC7AD582713520CD`. Startup is distinct from performance and visual conformance.

The RC3 full regression failed the existing60-step GPU particle CPU/GPU comparison: direction error0.022302791 and angular error0.06961107 against the unchanged0.005 tolerance. Two subsequent independent runs of existing `--gpu-particles-only` passed; this does not resolve the failure or establish a full pass. The existing test now preserves exact prior input, CPU expectation and GPU result on the first recurrence. Narrow reproduction and cause isolation are next; do not blindly repeat the full suite. Original failure, focused outcomes and budget remain in the artifact directory.

2026-11-12 — Known deterministic leaf presentations now participate in the existing parent's measurement cache while rendering/input continue. Custom traits remain opt-in; label/options/size/items and environment changes invalidate layout. See [common leaf measurement contract](widget-cpu-retention.md) and artifacts/leaf-layout-retention-2026-11-11/verification.json. Full NativeAOT/performance gates remain open.
