using Duxel.Core;

internal static class SpinnerAllocationTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var accessibility = new UiAccessibilityTree();
        context.SetAccessibilityTree(accessibility);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(Action<UiImmediateContext> draw)
        {
            context.NewFrame(new(1f / 60, new(320, 240), new(1, 1)), idle, new(0, 0, 320, 240), UiTextSettings.Default);
            context.Render(new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding : 0);
                ui.SetCursorScreenPos(default);
                draw(ui);
            }));
            using var data = context.GetDrawData();
        }
        var spinner = new ObservedSpinner("Working", new()
        {
            Width = 260
        });
        using var box = Dux.SizedBox(spinner, 80, 96);
        Frame(ui =>
        {
            check(box.MeasureConstrained(ui, new()) == new UiVector2(80, 96), "SizedBox measures the allocated 80x96 Spinner root");
            box.Render(ui);
        });
        check(spinner.Bounds == new UiRect(0, 0, 80, 96), $"Spinner root honors parent 80x96 allocation; actual = {spinner.Bounds}");
        var accessibleId = accessibility.Root.First;
        check(accessibleId is not null && accessibility.TryGet(accessibleId, out var element) && element.Node.Bounds == new UiRect(0, 0, 80, 96),
            "Spinner accessibility publishes the actual allocated root");
        box.Options = new(120, 16);
        Frame(box.Render);
        check(spinner.Bounds == new UiRect(0, 0, 120, 16), "a short parent changes the actual Spinner root without keeping its natural height");
        foreach (var size in Enum.GetValues<UiSpinnerSize>())
        {
            using var ring = Dux.SizedBox(new UiSpinner<UiFluentSpinnerBehavior, ObservedSpinnerPresentation>(options : new(size)
            {
                Width = 260
            }), 80, 96);
            Frame(ring.Render);
            var layout = ObservedSpinnerPresentation.Last;
            var diameter = UiFluentSpinnerPresentation.Diameter(size);
            check(layout.Size == new UiVector2(80, 96) && layout.Ring.Width == diameter && layout.Ring.Height == diameter &&
                layout.Ring.X + diameter * .5f == 40 && layout.Ring.Y + diameter * .5f == 48,
                $"allocated {size} Spinner preserves its ring diameter and centers it at40,48");
        }
        foreach (var position in Enum.GetValues<UiSpinnerLabelPosition>())
        foreach (var rtl in new[] { false, true })
        {
            using var labeled = Dux.SizedBox(new UiSpinner<UiFluentSpinnerBehavior, ObservedSpinnerPresentation>("Two\nlines", new(LabelPosition : position, RightToLeft : rtl)
            {
                Width = 260
            }), 100, 120);
            Frame(labeled.Render);
            var layout = ObservedSpinnerPresentation.Last;
            var vertical = position is UiSpinnerLabelPosition.Above or UiSpinnerLabelPosition.Below;
            var first = position is UiSpinnerLabelPosition.Above or UiSpinnerLabelPosition.Before;
            if (!vertical && rtl)
                first = !first;
            var gap = vertical ? (first ? layout.Ring.Y - layout.Label.Y - layout.Label.Height : layout.Label.Y - layout.Ring.Y - layout.Ring.Height)
                : (first ? layout.Ring.X - layout.Label.X - layout.Label.Width : layout.Label.X - layout.Ring.X - layout.Ring.Width);
            var left = MathF.Min(layout.Ring.X, layout.Label.X);
            var right = MathF.Max(layout.Ring.X + layout.Ring.Width, layout.Label.X + layout.Label.Width);
            var top = MathF.Min(layout.Ring.Y, layout.Label.Y);
            var bottom = MathF.Max(layout.Ring.Y + layout.Ring.Height, layout.Label.Y + layout.Label.Height);
            check(MathF.Abs(gap - 8) < .001f && MathF.Abs(left + right - 100) < .001f && MathF.Abs(top + bottom - 120) < .001f && layout.Text.LineCount == 2,
                $"{position}/RTL{rtl} centers natural multiline content with an8px gap inside the allocated root");
        }
        using var delayed = new ObservedSpinner("Pending", new(DelaySeconds : 1000)
        {
            Width = 260
        });
        using var delayedBox = Dux.SizedBox(delayed, 80, 96);
        Frame(ui =>
        {
            check(delayedBox.MeasureConstrained(ui, new()) == new UiVector2(80, 96), "a pending Spinner measures its assigned root without starting its behavior");
            delayedBox.Render(ui);
        });
        check(delayed.Bounds == new UiRect(0, 0, 80, 96), "a pending Spinner keeps the parent root while its content remains absent");
        accessibleId = accessibility.Root.First;
        check(accessibleId is not null && accessibility.TryGet(accessibleId, out var pending) && pending.Node.Offscreen && pending.Node.Bounds.Height == 0,
            "a delayed Spinner keeps hidden accessibility semantics despite its allocated layout footprint");
        using var unsupported = Dux.SizedBox(new UiSpinner<UiFluentSpinnerBehavior, MissingSpinnerArrange>(), 80, 96);
        var rejected = false;
        Frame(ui =>
        {
            try
            {
                unsupported.MeasureConstrained(ui, new());
            }
            catch (NotSupportedException)
            {
                rejected = true;
            }
        });
        check(rejected, "a custom Spinner must implement changed parent allocation explicitly during pure measurement");
        using var incorrect = Dux.SizedBox(new UiSpinner<UiFluentSpinnerBehavior, IncorrectSpinnerArrange>(), 80, 96);
        rejected = false;
        Frame(ui =>
        {
            try
            {
                incorrect.MeasureConstrained(ui, new());
            }
            catch (InvalidOperationException)
            {
                rejected = true;
            }
        });
        check(rejected, "a Spinner Arrange that ignores the assigned root is rejected");
        using var generated = Dux.SizedBox(new GeneratedAllocatedSpinner(), 80, 96);
        AllocatedSpinnerPresentation.Paints = 0;
        Frame(generated.Render);
        check(AllocatedSpinnerPresentation.Paints == 1 && AllocatedSpinnerPresentation.Last == new UiVector2(80, 96),
            "a generated user Spinner consumes its statically selected allocation-dependent presentation");
        using var explicitControl = Dux.SizedBox(new ImmediateSpinnerControl(true), 80, 96);
        Frame(explicitControl.Render);
        check(ImmediateSpinnerControl.Last == new UiVector2(80, 96), "a user control shares public Spinner measurement and explicit allocation rendering");
        using var legacyControl = Dux.SizedBox(new ImmediateSpinnerControl(false), 80, 96);
        Frame(legacyControl.Render);
        check(ImmediateSpinnerControl.Last == new UiVector2(260, 32), "existing immediate Spinner overload preserves natural geometry inside a parent");
        var zeroSpinner = new ObservedSpinner("Clipped", default);
        using var zero = Dux.SizedBox(zeroSpinner, 0, 0);
        Frame(zero.Render);
        check(zeroSpinner.Bounds == new UiRect(0, 0, 0, 0) && !context.State.HasActiveAnimations,
            "a zero-sized Spinner has a zero root and requests no visible animation");
        SpinnerTests.Run(check);
        FrameSchedulingTests.Run(check);
    }
    private sealed class ObservedSpinner(string label, UiSpinnerOptions options) : UiFluentSpinner(label, options)
    {
        internal UiRect Bounds;
        protected override void RenderControl(UiImmediateContext ui)
        {
            base.RenderControl(ui);
            var p = ui.GetItemRectMin();
            var s = ui.GetItemRectSize();
            Bounds = new(p.X, p.Y, s.X, s.Y);
        }
    }
    private sealed class Screen(Action<UiImmediateContext> draw) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => draw(ui);
    }
    private sealed class ImmediateSpinnerControl(bool constrain) : UiControlWidget
    {
        private UiFluentSpinnerBehavior _behavior;
        internal static UiVector2 Last;
        protected override UiVector2 MeasureContent(UiControlContext context) => context.MeasureSpinner<UiFluentSpinnerPresentation>(options : new() { Width = 260 });
        protected override void RenderControl(UiImmediateContext ui)
        {
            ui.Spinner<UiFluentSpinnerBehavior, UiFluentSpinnerPresentation>(PrimaryInputId("spinner"), ref _behavior, null, new()
            {
                Width = 260
            }, constrain);
            Last = ui.GetItemRectSize();
        }
    }
}

internal readonly struct ObservedSpinnerPresentation : IUiSpinnerPresentation
{
    internal static UiSpinnerLayout Last;
    public static UiControlTypographyScope BeginLabel(UiControlContext context, UiSpinnerOptions options, bool hasLabel) => UiFluentSpinnerPresentation.BeginLabel(context, options, hasLabel);
    public static UiSpinnerLayout Measure(UiControlContext context, string? label, UiSpinnerOptions options) => UiFluentSpinnerPresentation.Measure(context, label, options);
    public static UiSpinnerLayout Arrange(UiControlContext context, UiSpinnerOptions options, in UiSpinnerLayout natural, UiVector2 size) => UiFluentSpinnerPresentation.Arrange(context, options, natural, size);
    public static void Paint(UiControlContext context, UiSpinnerOptions options, in UiSpinnerFrame frame, in UiSpinnerLayout layout, UiVector2 origin)
    {
        Last = layout;
        UiFluentSpinnerPresentation.Paint(context, options, frame, layout, origin);
    }
}
internal readonly struct MissingSpinnerArrange : IUiSpinnerPresentation
{
    public static UiControlTypographyScope BeginLabel(UiControlContext context, UiSpinnerOptions options, bool hasLabel) => default;
    public static UiSpinnerLayout Measure(UiControlContext context, string? label, UiSpinnerOptions options) => new(new(24, 12), default, default, default);
    public static void Paint(UiControlContext context, UiSpinnerOptions options, in UiSpinnerFrame frame, in UiSpinnerLayout layout, UiVector2 origin)
    {
    }
}
internal readonly struct IncorrectSpinnerArrange : IUiSpinnerPresentation
{
    public static UiControlTypographyScope BeginLabel(UiControlContext context, UiSpinnerOptions options, bool hasLabel) => default;
    public static UiSpinnerLayout Measure(UiControlContext context, string? label, UiSpinnerOptions options) => new(new(24, 12), default, default, default);
    public static UiSpinnerLayout Arrange(UiControlContext context, UiSpinnerOptions options, in UiSpinnerLayout natural, UiVector2 size) => natural;
    public static void Paint(UiControlContext context, UiSpinnerOptions options, in UiSpinnerFrame frame, in UiSpinnerLayout layout, UiVector2 origin)
    {
    }
}
internal readonly struct AllocatedSpinnerPresentation : IUiSpinnerPresentation
{
    internal static int Paints;
    internal static UiVector2 Last;
    public static UiControlTypographyScope BeginLabel(UiControlContext context, UiSpinnerOptions options, bool hasLabel) => default;
    public static UiSpinnerLayout Measure(UiControlContext context, string? label, UiSpinnerOptions options) => new(new(24, 12), default, default, default);
    public static UiSpinnerLayout Arrange(UiControlContext context, UiSpinnerOptions options, in UiSpinnerLayout natural, UiVector2 size) => natural with { Size = size, Ring = new(0, 0, size.X, size.Y) };
    public static void Paint(UiControlContext context, UiSpinnerOptions options, in UiSpinnerFrame frame, in UiSpinnerLayout layout, UiVector2 origin)
    {
        Paints++;
        Last = layout.Size;
        context.Frame(new(origin.X, origin.Y, layout.Ring.Width, layout.Ring.Height), context.Theme.Button, context.Theme.Border, 0, 1);
    }
}
[UiWidgetStyle(typeof(UiSpinner<,>), typeof(UiFluentSpinnerBehavior), typeof(AllocatedSpinnerPresentation))]
internal partial class GeneratedAllocatedSpinner;
