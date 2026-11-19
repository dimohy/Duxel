# Widget accessibility foundation

2026-11-11 — [RadioGroup](fluent2-radio-group.md) now exposes its actual exclusive selection owner and RadioButton children through common Selection/SelectionItem. Options never expose Toggle/Invoke. Native Select, live validation, stable value-based identity, descriptions and roving Focus are connected to the real binding/input path; parent allocation is unchanged. Focused18, prior input/allocation91 and actual Observer137 pass. See artifacts/radio-accessibility-2026-11-11/verification.json for external/native scope and remaining limits.

2026-11-11 — Checkbox, Switch and ToggleButton now publish their actual state and use the existing typed Toggle action. `UiAccessibilityNode.ToggleState` is the canonical nullable enum: null means unsupported; Off0, On1 and Indeterminate2 are supported states. `Checked` remains a two-state source-compatible projection of that same value, so its null must not be used to infer capability. `UiAccessibilityToggleChange.PreviousState` preserves all three previous states while the existing bool? constructor, Previous projection and deconstruction remain available.

The shared UiToggle path applies live enabled/clip/popup checks and the original value policy/binding callback; nullable Checkbox activation remains mixed→on, then ordinary on/off. Painting and semantic publication share one policy read. `IUiTogglePresentation.AccessibilityRole` defaults to Checkbox; built-in Switch and ToggleButton select their own roles statically. Windows maps Checkbox to50002 and Switch/ToggleButton to50000 with Toggle10015; stateful controls do not duplicate Invoke. Integer state property30086 changes independently of capability30041, so mixed is not disabled or unsupported. This follows [Microsoft CheckBox/Toggle guidance](https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-supportcheckboxcontroltype) and the [WinUI Switch Button/Toggle mapping](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.automation.peers.toggleswitchautomationpeer).

Each node describes its rendered snapshot. A shared sibling drawn before a changed control catches up on the requested next frame; publication does not claim a transactional repaint of earlier siblings. Names, Field/Tooltip rules, focus support and source identity use the existing common contracts. Core21, Observer134, generators115, external Toggle client11/host4 and old checkbox-menu client6/host3 pass. ObserverGPU47 and all73 comparable bodies are unchanged. See artifacts/toggle-accessibility-2026-11-11/verification.json. Full localized narration, forced colors, complete component conformance and exactRC3 NativeAOT remain open.

2026-11-11 [Tooltip surface events](tooltip-events.md): actual ToolTip children, control/content views and close-before-removal/open-after-publication now work. Source/ancestry snapshots preserve asynchronous UIA event caches. CPU520 and external client10/host3 pass; official surfaces, property-change/window-shutdown events and current NativeAOT remain open.

2026-11-11 [Tooltip relationships](tooltip-accessibility.md): Description extends Windows HelpText while preserving hint/validation; Label supplies Name below explicit child/Field naming; Inaccessible publishes no relationship. Logical SourceId is not a separate provider node. Visible tooltip role/events still require implementation.

The common semantic tree feeds Windows UI Automation for action/edit/range/toggle controls, tab/menu/panel relationships, scrolling and virtual items. Qualification is tracked per feature; full Fluent component/state coverage, text patterns, localized narration and cross-runtime validation remain open.

## Common contract

`UiContext.SetAccessibilityTree(tree)` attaches a tree. App sessions attach the tree supplied by `IPlatformAccessibilityBackend`; Windows supplies it automatically. The platform owns that tree. Detach it before disposing an independently owned tree and stop the producer before disposal.

One render producer stages controls and publishes a completed frame. Provider threads read the published state under a reader lock. Unchanged entries reuse storage and preserve the revision. Removed IDs disappear; a later recreation receives a new runtime ID. A failed render preserves the previous publication. This is logical render completion, not proof of GPU presentation.

Requests enter a bounded queue of 1,024 actions and wake the existing frame scheduler. The render producer applies Focus, Invoke and SetValue through the existing control/binding/callback paths. Requests for one element retain arrival order. Synchronous requests on the render producer return Busy; foreign synchronous requests have a two-second timeout. Invoke queues asynchronously. Disabled, clipped, stale, read-only and invalid single-line requests are rejected explicitly. All control characters are rejected in SetValue. Disposal completes remaining requests and releases burst storage.

## Names, children and capabilities

`UiWidget.AccessibleName` supplies an explicit name, including icon-only buttons. Otherwise the primary Field control uses the Field label; plain controls use their display label. A `##` identifier does not create a spoken name. Field required/invalid/hint/validation metadata belongs to the primary control. Input adornment widgets are actual semantic children with independent names. Published bounds are clipped logical bounds, converted to physical screen bounds by the Windows provider.

`IUiButtonBehavior.AccessibilityFocus` and the corresponding input behavior capabilities describe the actual selected behavior. A custom button behavior opts into focus only when it consumes the common navigation path. Capability selection is static; it does not require a runtime theme registry.

AccessibleName uses existing lazy identity storage. Unnamed, unrendered Parent factories retain their previous object size. Scoped identity caches carry an additional optional name reference; the semantic tree and providers also add storage. No application memory/FPS improvement is claimed.

## Windows bridge and validation

Windows responds to `WM_GETOBJECT` for `UiaRootObjectId`. `GeneratedComInterface`, `GeneratedComClass` and `LibraryImport` generate native bridges compatible with NativeAOT. There is no runtime COM reflection registry. ABI inputs are pinned in `artifacts/uia-foundation-2026-11-06/source-manifest.json`.

Button exposes Invoke; Input exposes Value, read-only state and the selected focus capability. Field information is available through native UIA properties. The root implements fragment navigation and descendant hit testing. Disposal stops returning the root during disconnect, drains retained child providers, disconnects a registered root while semantic state is readable, then disposes the tree. An already unavailable connection is recorded as that terminal HRESULT; other disconnect failures propagate. Accessibility events and comprehensive long-duration lifetime qualification remain incomplete.

Use the existing test runners:

```powershell
dotnet run --project tests/Duxel.Graphics.Tests -c Release -- artifacts/uia-local accessibility-only
dotnet run --project tests/Duxel.Graphics.Tests -c Release -- artifacts/uia-client uia-client-pid <owned-test-process-id>
./run-fba.ps1 samples/fba/widget_tree_theme_fba.cs -NoCache -ExtraArgs @('--input')
```

The client mode is for the Input gallery. It reads actual Windows UIA controls and tests shared SetValue, read-only rejection and Clear Invoke. `uia-field-client-pid` targets the Field gallery and verifies required/invalid/HelpText, shared validation changes, stable identity and physical bounding rectangles. The test locates a Duxel window class within the exact owned process, rather than selecting an IME/helper window. It does not inspect the desktop image or establish visual fidelity. Managed .NET10 and fixed .NET11 RC3 NativeAOT results, failed initial attempts and executable hashes are recorded separately in the artifact directory.

## Removed provider lifetime

`UiAccessibilityTree.ElementsRemoved` runs after completed removal publication, outside the tree lock. Windows removes stale weak-map keys immediately and shrinks former burst capacity, even if no later client query occurs. Only removal frames trigger that scan. Registered removed providers enter a retirement queue; a posted private Windows message drains at most32 per dispatch. COM disconnect runs outside cache/publication locks and off the render producer. Shutdown drains remaining child providers before root cleanup. Unregistered ABI-only providers have no UIA connection to disconnect.

The focused cache case holds4,096 real provider instances, removes4,095 and checks one surviving entry/capacity at most256 without forced GC. Four concurrent readers verify complete entries over1,000 publications and disposal. Separate managed and NativeAOT host/client processes remove/recreate an actual widget and retain its old client interface. The old element remains unavailable and the replacement has a distinct runtime identity. Both runs recorded one already-unavailable native disconnect result and zero queued providers; this is not an S_OK result or a long-duration process-memory measurement. Native UIA events, screen-reader announcements and application cost qualification remain open.

References: [server-side providers](https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-serversideprovider), [source-generated COM](https://learn.microsoft.com/en-us/dotnet/standard/native-interop/comwrappers-source-generation), [provider disconnection](https://learn.microsoft.com/en-us/windows/win32/api/uiautomationcoreapi/nf-uiautomationcoreapi-uiadisconnectprovider).

Qualified results: .NET10 full3,024/focused34; fixed .NET11 RC3 NativeAOT focused34; external NativeAOT client4 and target normal exit0. The final net8/9/10 solution build has no warnings/errors. See `artifacts/uia-foundation-2026-11-06/verification.json`. The original factory allocation check caught the initial base-field regression and passes again; zero unchanged-node allocation is a focused check, not a three-run application performance claim.

Captured event-graph Toggle ancestors expose immutable read-only state getters and reject actions after their source tree is disposed. The native host verifies mixed2 through the actual COM vtable after disposal; ordinary no-ACK TooltipClosed shutdown also passes. Other frozen patterns, localized narration and full process-memory qualification remain separate gates.
