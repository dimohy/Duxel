# Widget natural measurement and Field allocation

2026-11-10 [Field parent allocation](fluent2-field.md#parent-allocation--2026-11-10): actual width precedes text measurement; static FieldWidth/Arrange and a local layout copy reserve description rows and allocate remaining height to the real editor. The 80 x 64 / 80 x 32 defect is fixed. Managed Field1018 and separate routing1563 checks pass; natural cache, editor/undo and Core UIA metadata persist. Panel measurement rejection/closed-root boundary checked. Native remains unverified; container stages remain3/6. Local sample:layout_field_allocation_fba.cs.

2026-11-10 [Allocated child routing](widget-containers.md#allocated-child-routing--2026-11-10): common child rendering preserves the incoming clip and isolates/restores legacy table/column positioning. Nested Padding now honors actual offsets across 14 leaf variants. Managed 1529 checks pass (1113 CPU and 416 exact raster/vector GPU comparisons). The routing checkpoint found the Field 80 x 64 / 80 x 32 defect; the later Field allocation checkpoint above fixes it. Current Native prerequisites remain unavailable in checked standard paths; alternative L: roots deny access.

2026-11-10 [Badge measurement and allocation](fluent2-badge.md#measurement-and-parent-allocation--2026-11-10): shared geometry and one common root, centered fixed glyph/icon metrics, root clipping and hidden-counter reservation. Read-only providers and the existing bounded number memo are shared across explicit phases. Managed 374 checks pass; Native unverified. No fields/new caches. Local sample: layout_badge_allocation_fba.cs.

2026-11-10 [Link measurement and parent allocation](fluent2-link.md#measurement-and-parent-allocation--2026-11-10): real width controls wrapping; static Arrange returns a local copy while the bounded natural cache stays reusable. Input, paint and clips share the actual root. Managed 495 checks pass across CPU/input, raster/vector, MSAA 1/4, Light/Dark and paced retained frames. No new fields/caches; Native remains unverified. Local sample: layout_link_allocation_fba.cs.

2026-11-10 [Divider measurement and parent allocation](fluent2-divider.md#measurement-and-parent-allocation--2026-11-10): static Measure and CanArrange use one common root; actual width controls caption wrapping and both axes control lines, centering and clipping. Content providers remain read-only and are read once per explicit measurement/render phase. Managed525 checks pass; Native remains unverified. No new widget/context fields or caches. Local sample:layout_divider_allocation_fba.cs.

2026-11-10 [Text parent allocation](fluent2-text.md): CanArrange opts into one shared text root; assigned/measurement width controls wrapping without changing fonts. Existing content-provider reads are preserved. Managed321 checks pass across raster/vector,MSAA1/4,Light/Dark and retained16/3-frame updates. No per-widget fields were added;one context counter is added. Native remains unverified. New local sample:layout_text_allocation_fba.cs.

2026-11-10 [Progress measurement/allocation](fluent2-progress.md): static source/presentation measurement avoids getter reads; declare a getter height or use a tight parent. Static Arrange and the shared render path honor actual roots. Managed306 checks pass; Native remains unverified. New local sample:layout_progress_allocation_fba.cs. Whole-goal counts and resize deferral are unchanged.

`widget.Measure(ui, availableSize)` obtains desired size in the current context. Default/user widgets implement the same `protected override UiVector2 MeasureContent(UiControlContext context)`. Available size is a finite,nonnegative constraint; desired size is not forcibly truncated to it. The allocated region supplies the final clip.

Button,Toggle families(Checkbox/Switch/ToggleButton),Input,Slider,RadioGroup,Spinner,Progress,Text,Divider,Link,Badge/CounterBadge and Field currently measure their actual presentations. Getter-driven Progress requires a declared height or tight parent height; custom sources can provide pure layout metadata. Unsupported widgets fail explicitly. This does not advertise automatic measurement for all legacy views or Dux containers. New user widgets must provide pure measurement rather than rendering to obtain size.

Input/value-control measurement does not read value bindings,invoke event callbacks or process keyboard/pointer input. Text measurement reads its read-only content provider because content determines intrinsic size; it does not render or invoke input/event handlers. Input adornments and RadioGroup lines use the same authoritative layout as rendering. Caller cursor,next-item width,widget identity and available-region scopes are restored. Invalid sizes,reentrancy,widget rendering/shared input processing during measurement fail explicitly; subsequent measurement can continue after failure. User overrides must also honor the contract against drawing,events and state mutation.

Build and measurement are separate phases. Measurement does not implicitly run Build or render arbitrary IUiView content. A pre-owned child can participate in both phases:

```csharp
public sealed class ProfileEditor : UiWidget
{
    private readonly UiFluentField _name;
    public ProfileEditor(UiBinding<string> value)
    {
        _name = Fluent.Field(Fluent.Input("Name", value, 256));
        _name.Options = new() { Label = "Display name", Required = true };
        OwnChild("name", _name);
    }
    protected override IUiView Build(UiBuildContext context)
        => context.Child("name", () => _name);
    protected override UiVector2 MeasureContent(UiControlContext context)
        => context.Measure(_name, context.AvailableSize);
}
```

`Fluent.Field(control)`,`Dux.Field(control)` and `new UiFluentField(control)` use natural measurement. Width0 consumes available width. A positive Width is the outer Field width; horizontal layout constrains the child after reserving the33percent label column. The child's actual desired height is allocated,then descriptions follow it. The child must provide positive height.

```csharp
var field = Fluent.Field(Fluent.Input("Email", email.Binding, 256));
field.Width = 220;
field.Options = new() { Label = "Work email", Hint = "Use your work address" };
```

Existing `Fluent.Field(control, new UiVector2(220,32), options)` retains explicit allocation. Setting `ControlSize` selects explicit mode; setting `Width` selects natural mode. In natural mode,ControlSize is the last explicit setting,not the current measured result. The immediate API is `ui.AutoField<TBehavior,TPresentation>(control,width,options,content)`. A same-name float overload was avoided to preserve existing target-typed new call sites.

Source generation emits the new one-argument constructor,default factory and closed behavior/presentation binding. There is no runtime registry or type-name lookup. Field rechecks child size so changing state/adornments cannot leave a stale parent height. Measurement adds CPU,identity-scope,text work and guard branches. Application CPU,allocation,Private and FPS improvements are not claimed; same-condition repeated performance qualification remains separate.

GPU tests first render three Input sizes,Checkbox,Switch,Slider,RadioGroup,a user widget and a tall Input child to obtain actual bounds. Natural Field pixels must match explicit Fields allocated from those rendered heights. Two widths,both orientations,light/dark,MSAA1/4 and raster/vector rendering are covered. Narrow horizontal labels clip to their own column; comparison with a no-label control detects painting over neighboring content. Complete official browser parity,UIA,arbitrary slots,motion,all-container layout and Pi hardware qualification remain open.

Evidence: `artifacts/widget-measurement-2026-11-06/`. The `--field` sample removes fixed heights and includes an available-width Field. See the [run guide](widget-gallery-run.md).

Final review first reproduced a navigable-edit case that mutated item/hover/focus before the inner editor rejected measurement. The outer entry now rejects before mutation;label/caret entries are checked too. Earlier passes and final guarded qualification remain in separate directories. Custom presentations opting into layout reuse must honor their declared cache dependencies;measurement that reads arbitrary external state must not be cached unchanged.

## Explicit owned composition — 2026-11-12

A user-composed UiWidget remains non-retained by default. A wrapper whose measurement delegates to one owned UiWidget may explicitly override CanRetainMeasurement with protected CanRetainOwnedMeasurement(child). The helper verifies live ownership, rejects an unowned/other-owner/disposed child, and queries that child's existing contract without rebuilding or measuring it. It does not infer arbitrary Build dependencies. Any additional measurement input in the wrapper must invalidate through RequestRebuild or watched state. Fixed dimensions alone do not grant consent. The example uses the base constrained composition measurement; call MeasureConstrained, and preserve the same child key in Build. Opaque IUiView roots still require explicit measurement.
```csharp
public sealed class OwnedContent : UiWidget
{
    private readonly UiWidget _content;
    public OwnedContent(UiWidget content)
    {
        OwnChild("content", content);
        _content = content;
    }
    protected override bool CanRetainMeasurement => CanRetainOwnedMeasurement(_content);
    protected override IUiView Build(UiBuildContext context) => context.Child("content", () => _content);
}
```

## Validated owned-child sizing — 2026-11-12

Natural Field widgets and Tooltip share the existing UiRetainedLayout with explicit child constraints. Each owner has one optional single-result size cache, created only for a consenting live child in a retainable text environment. The base UiWidget acquires no new storage. First sizing still executes the child's validation/measurement. Subsequent hits require unchanged resource/text keys and compatible constraints; the render-only tight allocation of an already validated natural size may reuse it. Ownership/disposal is checked before every lookup. Unknown children, glyph observation and legacy immediate AutoField do not use this cache.

Property/owner invalidation clears references and increments the existing revision. An invalidated or failed attempt cannot commit, and the sizing consumer never borrows stale same-frame prepared geometry. Existing container prepared-arrangement behavior is preserved separately. All actual rendering, input values, field metadata/disabled scopes, editing/undo, popup scheduling/painting/actions remain live. Disposal drops the single-result cache. The additional owner cache storage is bounded; full application Private/working-set/long-growth and exactRC3 NativeAOT performance remain open.

Observer ./run-gallery.ps1 → Measurement reuse includes `Measure owned children` for the actual natural Field input and Tooltip target. This counts widget measurement calls, not every internal text/presentation operation. Evidence: artifacts/owned-child-sizing-2026-11-12/verification.json.
