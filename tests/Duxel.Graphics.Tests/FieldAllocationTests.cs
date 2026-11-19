using Duxel.Core;
using Presentation = Duxel.Core.UiFluentFieldPresentation<Duxel.Core.UiFluentMediumFieldSize, Duxel.Core.UiVerticalFieldLayout>;

internal static class FieldAllocationTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var accessibility = new UiAccessibilityTree();
        context.SetAccessibilityTree(accessibility);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(Action<UiImmediateContext> draw, UiInputState? input = null)
        {
            context.NewFrame(new(1f / 60, new(320, 240), new(1, 1)), input ?? idle, new(0, 0, 320, 240), UiTextSettings.Default);
            context.Render(new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding : 0);
                ui.SetCursorScreenPos(default);
                draw(ui);
            }));
            using var data = context.GetDrawData();
        }
        var value = "a";
        var reads = 0;
        var writes = 0;
        var input = new ObservedInput(new(
            () =>
            {
                reads++;
                return value;
            },
            text =>
            {
                value = text;
                writes++;
            }));
        var field = Fluent.Field(input);
        var observed = new AllocationRoutingTests.Probe(field);
        using var box = Dux.SizedBox(observed, 80, 64);
        Frame(ui =>
        {
            var cursor = ui.GetCursorScreenPos();
            check(box.MeasureConstrained(ui, new()) == new UiVector2(80, 64), "Field reports the actual 80x64 parent allocation");
            check(reads == 0 && writes == 0 && ui.GetCursorScreenPos() == cursor && ui.CurrentField is null,
                "Field measurement preserves cursor/metadata and never reads or writes the input binding");
            var intrinsic = default(UiVector2);
            var unbounded = true;
            try
            {
                intrinsic = field.MeasureConstrained(ui, new(80, 80, 0, float.PositiveInfinity));
            }
            catch (ArgumentOutOfRangeException)
            {
                unbounded = false;
            }
            check(unbounded && intrinsic == new UiVector2(80, 32), "Field measures finite intrinsic control height under an unbounded parent axis");
            box.Render(ui);
        });
        check(observed.Bounds == new UiRect(0, 0, 80, 64) && input.Bounds == observed.Bounds,
            "Field root and actual owned input consume the same assigned 80x64 geometry");
        var id = accessibility.Root.First;
        check(id is not null && accessibility.TryGet(id, out var accessible) && accessible.Node.Bounds == input.Bounds,
            "Field publishes the actual allocated input bounds through the existing accessibility tree");
        Frame(box.Render, idle with { MousePosition = new(40, 54), LeftMouseDown = true, LeftMousePressed = true });
        check(context.State.ActiveId is not null, "the lower allocated Field area focuses its real editor");
        Frame(box.Render, idle with { MousePosition = new(40, 54), LeftMouseReleased = true });
        Frame(box.Render, idle with { KeyEvents = [new(UiKey.End, true, default)], CharEvents = [new('x')] });
        check(value == "ax" && writes == 1, "allocated Field preserves actual text input and one binding update");
        var active = context.State.ActiveId;
        box.Options = new(160, 48);
        Frame(box.Render);
        check(observed.Bounds == new UiRect(0, 0, 160, 48) && input.Bounds == observed.Bounds && context.State.ActiveId == active,
            "live Field parent changes retain the actual input owner");
        Frame(box.Render, idle with { KeyEvents = [new(UiKey.Z, true, KeyModifiers.Ctrl)], Modifiers = KeyModifiers.Ctrl });
        check(value == "a", "Field arrangement preserves the owned editor's undo history");

        field.Options = new() { Label = "Email", Required = true, Hint = "Hint", ValidationMessage = "Err" };
        box.Options = new(80, 120);
        Frame(box.Render);
        check(input.Bounds == new UiRect(0, 26, 80, 58) && observed.Bounds == new UiRect(0, 0, 80, 120),
            "vertical Field reserves label and description rows before allocating remaining height to its input");
        check(input.Field == new UiFieldContext("Email", "Hint", "Err", true, UiFieldValidationState.Error),
            "allocated Field preserves required/invalid metadata on its actual child");
        Frame(box.Render, idle with { MousePosition = new(10, 10), LeftMouseDown = true, LeftMousePressed = true });
        check(context.State.FocusedId is not null && writes == 2, "allocated Field label focuses the existing editor without editing its value");
        field.Options = field.Options with { Disabled = true };
        Frame(box.Render, idle with { CharEvents = [new('z')] });
        check(value == "a", "disabled allocated Field does not accept editor input");
        id = accessibility.Root.First;
        check(id is not null && accessibility.TryGet(id, out var disabled) && !disabled.Node.Enabled && disabled.Node.Required
            && disabled.Node.Invalid && disabled.Node.Name == "Email" && disabled.Node.Hint == "Hint",
            "allocated Field accessibility keeps actual disabled/required/invalid/name/description metadata");
        field.Options = field.Options with { Disabled = false };
        box.Options = new(0, 0);
        Frame(box.Render);
        check(observed.Bounds == new UiRect(0, 0, 0, 0) && input.Bounds.Width == 0 && input.Bounds.Height == 0,
            "zero Field allocation remains valid and gives its owned editor zero geometry");
        id = accessibility.Root.First;
        check(id is not null && accessibility.TryGet(id, out var hidden) && hidden.Node.Offscreen,
            "zero allocated Field input is offscreen in accessibility publication");

        using var content = new UiFieldContent<CountedFieldPresentation>();
        var cacheChild = Fluent.Input("Cached", new UiState<string>("Value").Binding, 32);
        using var cacheProbe = new FieldControlProbe(cacheChild, content);
        using var cacheBox = Dux.SizedBox(cacheProbe, 80, 120);
        CountedFieldPresentation.Measurements = 0;
        Frame(ui =>
        {
            cacheBox.MeasureConstrained(ui, new());
            cacheBox.Render(ui);
        });
        check(CountedFieldPresentation.Measurements == 1 && content.Layout.Size == new UiVector2(80, 94) && content.Layout.Control.Height == 32,
            "Field measure/render share one bounded natural layout and never overwrite it with the arranged copy");
        cacheBox.Options = new(80, 80);
        Frame(cacheBox.Render);
        check(CountedFieldPresentation.Measurements == 1 && CountedFieldPresentation.Last.Control.Height == 18,
            "height-only allocation changes reuse natural text while rearranging the real control");
        cacheBox.Options = new(160, 80);
        Frame(cacheBox.Render);
        check(CountedFieldPresentation.Measurements == 2 && CountedFieldPresentation.Last.Control.Width == 160,
            "actual Field width changes invalidate wrapped layout once");

        using var horizontal = new UiField<UiLabelFieldBehavior, UiFluentFieldPresentation<UiFluentMediumFieldSize, UiHorizontalFieldLayout>>(
            Fluent.Input("Horizontal", new UiState<string>("Value").Binding, 32), new(220, 32), new() { Label = "Label", Hint = "Hint" });
        var horizontalProbe = new AllocationRoutingTests.Probe(horizontal);
        using var horizontalBox = Dux.SizedBox(horizontalProbe, 80, 120);
        Frame(horizontalBox.Render);
        check(horizontalProbe.Bounds == new UiRect(0, 0, 80, 120) && horizontal.ControlSize == new UiVector2(220, 32),
            "explicit-size horizontal Field honors the parent's root without mutating its declared control size");

        var nestedInput = new ObservedInput(new UiState<string>("Nested").Binding);
        var inner = Fluent.Field(nestedInput);
        inner.Options = new() { Label = "Inner" };
        var outer = Fluent.Field(inner, new(220, 32), new() { Label = "Outer" });
        using var nestedBox = Dux.SizedBox(outer, 80, 120);
        Frame(ui =>
        {
            nestedBox.Render(ui);
            check(nestedInput.Bounds == new UiRect(0, 52, 80, 68) && nestedInput.Field?.Label == "Inner",
                "nested allocated Fields reserve both labels and keep the nearest child metadata");
            check(ui.CurrentField is null, "nested allocated Fields restore metadata after their actual child render");
        });

        using var legacy = new UiFieldContent<Presentation>();
        Frame(ui =>
        {
            ui.Field<UiLabelFieldBehavior, Presentation>(cacheChild, new(220, 32), default, legacy);
            check(ui.GetItemRectSize() == new UiVector2(220, 32), "existing immediate Field overload preserves its natural geometry");
        });
        using var column = new UiColumn([Fluent.Field(Fluent.Input("Column input", new UiState<string>("Value").Binding, 64))],
            new(MainAxisSize : UiMainAxisSize.Min));
        Frame(ui =>
        {
            var desired = default(UiVector2);
            var supported = true;
            try
            {
                desired = column.MeasureConstrained(ui, new(80, 80, 0, float.PositiveInfinity));
            }
            catch (ArgumentOutOfRangeException)
            {
                supported = false;
            }
            check(supported && desired == new UiVector2(80, 32), "a shrink-wrapped Column can measure a natural Field with an unbounded main axis");
        });
        using var missingWidth = Dux.SizedBox(new UiField<FieldAllocationBehavior, MissingFieldWidth>(Fluent.Input("Missing", new UiState<string>("").Binding, 32), new(220, 32)), 80, 64);
        using var missingArrange = Dux.SizedBox(new UiField<FieldAllocationBehavior, MissingFieldArrange>(Fluent.Input("Missing", new UiState<string>("").Binding, 32)), 80, 64);
        using var wrongArrange = Dux.SizedBox(new UiField<FieldAllocationBehavior, WrongFieldArrange>(Fluent.Input("Wrong", new UiState<string>("").Binding, 32)), 80, 64);
        FieldAllocationBehavior.Calls = 0;
        Frame(ui =>
        {
            check(Reject<NotSupportedException>(() => missingWidth.MeasureConstrained(ui, new())), "constrained explicit custom Field requires declared outer-width metadata");
            check(Reject<NotSupportedException>(() => missingArrange.MeasureConstrained(ui, new())), "custom Field must explicitly arrange changed parent allocation");
            check(Reject<InvalidOperationException>(() => wrongArrange.Render(ui)), "Field rejects Arrange that returns the wrong root before input behavior");
            check(FieldAllocationBehavior.Calls == 0 && ui.CurrentField is null, "rejected Field geometry never invokes label behavior or leaks metadata");
            check(Reject<ArgumentOutOfRangeException>(() => ui.Field<UiLabelFieldBehavior, Presentation>(cacheChild, default)),
                "legacy explicit Field still rejects invalid source control sizes");
        });
        box.Options = new(80, 120);
        Frame(ui =>
        {
            box.Render(ui);
            check(observed.Bounds == new UiRect(0, 0, 80, 120) && ui.CurrentField is null,
                "valid Field recovers after custom geometry failures without leaking scopes");
        });
        var panelReads = 0;
        using var panel = new UiPanel<IUiView>("Independent panel", new EmptyPanelContent(), new()
        {
            Open = new(() =>
            {
                panelReads++;
                return false;
            }, static _ => { })
        });
        Frame(ui =>
        {
            check(Reject<NotSupportedException>(() => panel.MeasureConstrained(ui, UiBoxConstraints.Tight(new(80, 64)))) && panelReads == 0,
                "independent Panel explicitly rejects generic leaf measurement without reading its open binding");
            panel.Render(ui);
            check(panelReads == 1, "closed independent Panel retains its direct root behavior after rejected measurement");
        });
        FieldWidgetTests.Run(check);
    }

    private static bool Reject<T>(Action action) where T : Exception
    {
        try
        {
            action();
            return false;
        }
        catch (T)
        {
            return true;
        }
    }

    private sealed class ObservedInput(UiBinding<string> value) : UiFluentInput("Input", value, 64)
    {
        internal UiRect Bounds;
        internal UiFieldContext? Field;
        protected override void RenderControl(UiImmediateContext ui)
        {
            base.RenderControl(ui);
            Field = ui.CurrentField;
            var p = ui.GetItemRectMin();
            var s = ui.GetItemRectSize();
            Bounds = new(p.X, p.Y, s.X, s.Y);
        }
    }
    private sealed class Screen(Action<UiImmediateContext> draw) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => draw(ui);
    }
    private sealed class EmptyPanelContent : IUiView
    {
        public void Render(UiImmediateContext ui) => throw new InvalidOperationException("Closed panel must not render content.");
    }
}

internal readonly struct FieldAllocationBehavior : IUiFieldBehavior
{
    internal static int Calls;
    public static bool FocusRequested(UiControlContext context, string id, UiRect label)
    {
        Calls++;
        return false;
    }
}
internal readonly struct MissingFieldWidth : IUiFieldPresentation
{
    public static UiFieldLayout Measure(UiControlContext context, UiFieldOptions options, UiVector2 size) => Presentation.Measure(context, options, size);
    public static UiFieldLayout Arrange(UiControlContext context, UiFieldOptions options, in UiFieldLayout natural, UiVector2 size) => Presentation.Arrange(context, options, natural, size);
    public static void Paint(UiControlContext context, UiFieldOptions options, in UiFieldLayout layout, UiVector2 origin) => Presentation.Paint(context, options, layout, origin);
}
internal readonly struct MissingFieldArrange : IUiFieldPresentation
{
    public static UiFieldLayout Measure(UiControlContext context, UiFieldOptions options, UiVector2 size) => Presentation.Measure(context, options, size);
    public static void Paint(UiControlContext context, UiFieldOptions options, in UiFieldLayout layout, UiVector2 origin) => Presentation.Paint(context, options, layout, origin);
}
internal readonly struct WrongFieldArrange : IUiFieldPresentation
{
    public static UiFieldLayout Measure(UiControlContext context, UiFieldOptions options, UiVector2 size) => Presentation.Measure(context, options, size);
    public static UiFieldLayout Arrange(UiControlContext context, UiFieldOptions options, in UiFieldLayout natural, UiVector2 size) => natural;
    public static void Paint(UiControlContext context, UiFieldOptions options, in UiFieldLayout layout, UiVector2 origin) => Presentation.Paint(context, options, layout, origin);
}

internal readonly struct CountedFieldPresentation : IUiFieldPresentation
{
    internal static int Measurements;
    internal static UiFieldLayout Last;
    public static bool ReuseLayout => true;
    public static float FieldWidth(float width) => width;
    public static UiFieldLayout Measure(UiControlContext context, UiFieldOptions options, UiVector2 size)
    {
        Measurements++;
        return Presentation.Measure(context, options, size);
    }
    public static UiFieldLayout Arrange(UiControlContext context, UiFieldOptions options, in UiFieldLayout natural, UiVector2 allocated) =>
        Presentation.Arrange(context, options, natural, allocated);
    public static void Paint(UiControlContext context, UiFieldOptions options, in UiFieldLayout layout, UiVector2 origin)
    {
        Last = layout;
        Presentation.Paint(context, options, layout, origin);
    }
}
internal sealed class FieldControlProbe : UiControlWidget
{
    private readonly UiWidget _child;
    private readonly UiFieldContent<CountedFieldPresentation> _content;
    private static readonly UiFieldOptions Options = new() { Label = "Label", Hint = "Hint", ValidationMessage = "Err" };
    internal FieldControlProbe(UiWidget child, UiFieldContent<CountedFieldPresentation> content)
    {
        _child = child;
        _content = content;
        OwnChild("input", child);
    }
    protected override UiVector2 MeasureContent(UiControlContext context) => context.MeasureField(_child, new(220, 32), 0, Options, _content);
    protected override void RenderControl(UiImmediateContext ui) =>
        ui.Field<UiLabelFieldBehavior, CountedFieldPresentation>(_child, new(220, 32), Options, _content, constrainLayout : true);
}
