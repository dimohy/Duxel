# Widget inheritance and compiled implementation binding

[RadioGroup](fluent2-radio-group.md) separates grouped input from three arrangement algorithms through the same generated/inheritable path. `UiControlTextBlock` retains measured runs and paints explicit line baselines and actual ink. Radio layout reuse validates context/font/width invalidation and partial-failure cleanup.

[Checkbox implementation](fluent2-checkbox.md) provides eight compositions of size-specific marks, frame shape and label layout. The default `IUiTogglePresentation.BeginText` scope preserves custom presentations; Fluent explicitly selects14/20 typography. Built-in/user-generated widgets and generic immediate calls consume the same input/painting implementation.

Last synced: 2026-11-06

## Shared button invalidation and disposal

The follow-up audit connects Progress, Slider and Toggle to the same resource-release stage. Progress Source/Size, Slider Label/Options/Size and Toggle Label publish changes before waking an idle frame; equal assignments stay idle. Invalid values preserve current state and scheduling. Progress clears source/policy references even when policy cleanup fails.

Slider/Toggle release their binding references; subsequent Value binding access throws ObjectDisposedException. Caller-owned UiBinding/UiState objects are not disposed or mutated. External binding changes and arbitrary getter internals still use the existing Watch/explicit-frame-request contract; automatic tracking of arbitrary getters is not claimed.

The current27 generated standard names map to7 participating base families. This qualifies ownership participation of currently connected widgets, not complete official components/features or every legacy widget. .NET10 focused17/full1199 and pinned RC3 Native EXT/core346 each passed;418 previous images match exactly. Binding-graph collection while disposed widgets remain referenced is an isolated lifetime unit test; its GC calls are not Private-memory or performance measurements. Evidence: `artifacts/control-ownership-2026-11-05/verification.json`.

`UiButton<TBehavior,TPresentation>` publishes Label/Size changes before requesting an idle frame. Equal assignments do not request one. Replacing Activated affects the next input and needs no visual repaint. Replacing label/callback while held preserves retained input identity and invokes the new callback once on release. Null labels are rejected consistently with construction. Disposed buttons reject Label/Size/Activated mutations.

Framework-owned resources are released in a common stage separate from user `OnDispose`. Omitting a base call or throwing from that hook cannot bypass release of Button callback/label or Text/Badge/Divider sources. User-hook and resource-release errors remain observable through the existing AggregateException contract; ownership, frame callback and identity cleanup continue. User extensions remain responsible for their own resources.

An explicitly rebuilt original-button control fails the idle-notification assertion. Current tests cover normal input, equal assignments and disposal failures. After100 warmup assignments,10000 identical assignments allocate0B and request0 frames. This is not a new whole-app FPS/Private/Pi measurement. Initial managed allocation-combination failures are not considered resolved merely because instrumented/warmed runs pass. Evidence and exact scope: `artifacts/button-lifecycle-2026-11-05/verification.json`.

[Complete Fluent migration](fluent2-coverage.md) takes precedence for expanded scope, current default factory and discovery policy. `Dux.Button()` now uses the generated `UiFluentButton` factory; the initial numeric compatibility descriptions below apply to `ui.Button()` and legacy `UiButton`.

The first widget theme is **Microsoft Fluent 2**. Windows 11 and WinUI guidance informs its Windows presentation; this is not a separate first Windows theme or a Material-first implementation.

## Current implementation and use

`UiWidget` owns Build, keyed identity, state subscriptions and lifecycle. The leaf base `UiControlWidget` uses the same foundation. The inheritable built-in `UiButton<TBehavior, TPresentation>` binds input processing separately from measurement and painting.

```csharp
[UiButtonStyle(typeof(UiReleaseButtonBehavior), typeof(UiFluentButtonPresentation))]
public partial class ProductButton;

public sealed class SaveButton : ProductButton
{
    public SaveButton() : base("Save") { }
    protected override void OnActivated() { /* application action */ }
}
```

The generator emits `ProductButton : UiButton<UiReleaseButtonBehavior, UiFluentButtonPresentation>` and its constructor. No runtime theme-name lookup or type registry selects this pair. Invalid declarations produce DUXWGT003; invalid implementation pairs produce DUXWGT004. Concrete implementations implement the static contracts `IUiButtonBehavior` and `IUiButtonPresentation`. Interfaces, abstract classes, open generics and inaccessible types cannot be selected.

`ui.Button<TBehavior, TPresentation>()` and inherited buttons share the authoritative input, layout and presentation path. Existing `ui.Button()` and `Dux.Button()` also reach that path. The default `UiButton` preserves existing numeric style scopes through an explicit compatibility policy. The Fluent implementation selects its presentation directly and does not depend on legacy numeric skin selection.

`UiControlContext` exposes Frame, Circle and Text through the current layer, clip and text pipeline. Presentations can replace the drawing algorithm rather than merely changing a radius. Tests replace the frame with a circle and independently select press/release behavior. Instance inheritance exposes lifecycle/action hooks while hot-path input and presentation use static generic contracts. No implementation object or runtime registry is constructed per frame. This does not establish maximum performance or an application FPS improvement.

Owning a button with `UiBuildContext.Child()` preserves its instance, input identity and state across parent rebuilds and disposes it on removal. The runnable example is `samples/fba/widget_tree_theme_fba.cs`.

## Official guidance and implementation boundaries

Checked 2026-11-05: [Fluent Windows guidance](https://fluent2.microsoft.design/components/windows) connects Windows 11 Fluent experiences with WinUI. [Fluent button guidance](https://fluent2.microsoft.design/components/web/react/core/button/usage/) distinguishes standard, compound and menu buttons and toggle behavior. The current implementation covers basic medium standard-button measurement, frame and centered text with separate input policies. It does not establish complete Fluent controls, accessibility, keyboard behavior or animation support. Pinned button source and SHA-256 values are in `artifacts/widget-tree-theme-2026-11-05/sources/manifest.json`.

[Mica](https://learn.microsoft.com/en-us/windows/apps/design/style/mica) guides app backdrop/content layering; [Acrylic](https://learn.microsoft.com/en-us/windows/apps/design/style/acrylic) primarily guides transient surfaces. Actual Mica/Acrylic materials are not implemented by this new theme yet. Their implementation, GPU cost and platform policy support require separate verification. Duxel is not being replaced by WinUI or the Windows App SDK renderer.

## Verification and remaining scope

- `.NET10` solution Release build: zero warnings and errors. Core still compiles for net8/net9/net10.
- Generator checks: 27 (16 legacy numeric checks plus 11 implementation-binding checks). Numeric configuration success does not count as implementation substitution.
- Focused checks: 57; full regression including GPU: 769. Results and logs live under `artifacts/widget-implementation-2026-11-05/`.
- The fixed RC3 full regression also passed 769 checks. New NativeAOT and managed FBA first GPU presentation/exit0 are recorded in `native-run.json` and `managed-app.stdout.log`. The new NativeAOT SHA256 is `7D86831FD1C15DD4042721658C6730BDDA22C11EB0C9FCB66B88C0BD77EECFA4`. Startup is distinct from interactive, visual-completeness and performance verification. Success of the earlier 88D4714D binary does not validate the new source.
- Text inputs, checkboxes, toggles, sliders, lists, menus and popups still need the same inheritance/presentation migration. Their current numeric skins are compatibility code, not completion evidence.
- Theme-wide generation across all controls, broader composition/drawing extension APIs, official-rule mapping, same-condition CPU/memory/frame performance, Linux/ARM64 execution and physical Pi tests remain open.

The original optimization ledger remains active. Two numeric-generation completions were reopened, leaving 12 of 34 verification bundles at that stage. Full Fluent scope was subsequently added, so that snapshot is not current overall progress. The complete denominator remains unknown pending official component mapping.

## Actual Button fonts and sizes

The generated default Button uses actual Semibold14/20. Bind small Regular12/16 or large Semibold16/22 through `UiButtonStyle`. Surrounding-font restoration, bounded metrics and static-layer invalidation share the common path; the non-generic immediate Button retains its compatibility presentation. [States, validation and remaining scope](fluent2-button.md), [run guide](widget-gallery-run.md).

The current-source `--buttons` page also includes primary/outline/subtle/transparent. Bind them statically to shared behavior using `UiButtonStyle` and the size-specific `UiFluentButtonPresentation<TAppearance>` family. [Exact API and remaining scope](fluent2-button.md).

The `--buttons` square/circular size rows also use generated shared widgets. Compose a shape statically inside an appearance, such as `UiFluentPrimaryButtonAppearance<UiFluentCircularButtonShape>`. User frame algorithms share this extension point. See [scope and limits](fluent2-button.md).

Bind `UiFluentIconButtonPresentation<TSize,TAppearance,TIcon,TPosition>` through a generated style. The `--buttons` Add icon uses the pinned official font, including a user-declared generated icon type; icon-only UIA remains incomplete. [Composition, validation and limits](fluent2-button.md).

See [official icons](fluent2-icons.md) for the bundled fonts and `[UiFluentIcon]` generation path. Add regular/filled/interactive is provided; full symbol API, SVG/browser parity and UIA are not complete.

Equal selected icon metadata reuses source construction/output. Generator functional107 and all57 Core-generated files were checked. Reemission after shared-file declaration removal remains incomplete; see [tracking and limits](fluent2-icons.md).

[Switch static sizing, composition and state presentation](fluent2-switch.md)

[Link navigation, inheritance, static presentation and cache](fluent2-link.md)

[Single-line input behavior, presentation and lifetime foundation](input-kernel.md)
