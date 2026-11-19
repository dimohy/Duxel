using Duxel.Core;

internal static class ProgressAllocationTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
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

        using var progress = Fluent.ProgressBar(.5f);
        progress.Thickness = UiProgressThickness.Large;
        var measured = default(UiVector2);
        var supported = true;
        Frame(ui =>
        {
            try
            {
                measured = progress.Measure(ui, new(220, 120));
            }
            catch (NotSupportedException)
            {
                supported = false;
            }
        });
        check(supported && measured == new UiVector2(220, 4),
            "static Progress exposes pure natural measurement using its real thickness");

        var probe = new MeasuredProbe();
        using var box = Dux.SizedBox(probe, 80, 64);
        Frame(box.Render);
        check(probe.Bounds == new UiRect(0, 0, 80, 64),
            $"Progress drawing honors the parent's 80x64 root; actual = {probe.Bounds}");

        using var animated = new UiProgress<ProgressCountingBehavior, UiFluentProgressPresentation>(.5f);
        ProgressCountingBehavior.Evaluations = 0;
        Frame(ui =>
        {
            var cursor = ui.GetCursorScreenPos();
            var item = ui.GetItemID();
            var first = animated.Measure(ui, new(200, 100));
            var second = animated.Measure(ui, new(120, 100));
            check(first == new UiVector2(200, 2) && second == new UiVector2(120, 2),
                "repeated pure measurement tracks available width without reading or advancing behavior");
            check(ProgressCountingBehavior.Evaluations == 0 && ui.GetCursorScreenPos() == cursor && ui.GetItemID() == item,
                "measurement preserves behavior, cursor and item state");
            animated.Render(ui);
        });
        check(ProgressCountingBehavior.Evaluations == 1, "Progress evaluates behavior once when rendered after measurement");

        var reads = 0;
        var thickness = UiProgressThickness.Large;
        var dynamic = new ObservedDynamic(() =>
        {
            reads++;
            return new(.25f, Thickness : thickness);
        });
        using var dynamicBox = Dux.SizedBox(dynamic, 80, 64);
        Frame(ui =>
        {
            check(dynamicBox.MeasureConstrained(ui, new()) == new UiVector2(80, 64) && reads == 0,
                "a getter-driven Progress uses a tight parent without invoking its getter during measurement");
            dynamicBox.Render(ui);
        });
        check(reads == 1 && dynamic.Bounds == new UiRect(0, 0, 80, 64),
            "the dynamic Progress reads once for rendering and paints the actual assigned root");
        thickness = UiProgressThickness.Medium;
        dynamicBox.Options = new(120, 16);
        Frame(dynamicBox.Render);
        check(reads == 2 && dynamic.Bounds == new UiRect(0, 0, 120, 16),
            "changing getter style and parent dimensions preserves one source read and live root geometry");

        using var generated = Fluent.ProgressBar(() =>
        {
            reads++;
            return new(.5f);
        });
        generated.Size = new(0, 4);
        Frame(ui =>
        {
            check(generated.Measure(ui, new(180, 100)) == new UiVector2(180, 4) && reads == 2,
                "a generated dynamic Progress has pure natural measurement when its height is declared");
            generated.Render(ui);
        });
        check(reads == 3, "the generated dynamic factory retains exactly one getter call per render");
        generated.Size = default;
        var rejected = false;
        Frame(ui =>
        {
            try
            {
                generated.Measure(ui, new(180, 100));
            }
            catch (NotSupportedException)
            {
                rejected = true;
            }
            check(rejected && reads == 3,
                "unknown getter-dependent thickness fails explicitly rather than reading a callback or inventing a medium height");
            generated.Size = new(0, 4);
            check(generated.Measure(ui, new(180, 100)) == new UiVector2(180, 4),
                "measurement recovers after an unsupported source geometry without corrupting the context");
        });

        using var metadata = new UiProgress<ProgressMetadata, ProgressMetadataSource, UiFluentProgressPresentation>(
            new(() =>
            {
                reads++;
                return new(.75f, Thickness : UiProgressThickness.Large);
            }, new(.75f, Thickness : UiProgressThickness.Large)));
        Frame(ui =>
        {
            check(metadata.Measure(ui, new(180, 100)) == new UiVector2(180, 4) && reads == 3,
                "a statically selected user source can supply pure layout metadata without executing its value callback");
            metadata.Render(ui);
        });
        check(reads == 4, "a metadata source still reads its current render value exactly once");

        using var missing = Dux.SizedBox(new UiProgress<MissingProgressArrange>(.5f), 80, 64);
        rejected = false;
        Frame(ui =>
        {
            try
            {
                missing.MeasureConstrained(ui, new());
            }
            catch (NotSupportedException)
            {
                rejected = true;
            }
        });
        check(rejected, "a value presentation must opt into changed parent allocation during measurement");
        using var incorrect = Dux.SizedBox(new UiProgress<IncorrectProgressArrange>(.5f), 80, 64);
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
        check(rejected, "an Arrange result that ignores the assigned Progress root is rejected");
        using var missingPure = new UiProgress<Func<UiProgressValue>, UiGetterProgressSource, MissingProgressArrange>(() =>
        {
            reads++;
            return new(.5f);
        });
        missingPure.Size = new(24, 12);
        rejected = false;
        Frame(ui =>
        {
            try
            {
                missingPure.Measure(ui, new(180, 100));
            }
            catch (NotSupportedException)
            {
                rejected = true;
            }
        });
        check(rejected && reads == 4, "a custom getter presentation must provide source-independent measurement explicitly");

        using var user = Dux.SizedBox(new GeneratedAllocatedProgress(.5f), 80, 64);
        AllocatedProgressPresentation.Paints = 0;
        Frame(user.Render);
        check(AllocatedProgressPresentation.Paints == 1 && AllocatedProgressPresentation.Bounds == new UiRect(0, 0, 80, 64),
            "a generated custom Progress uses the same allocation path and closed Paint implementation");
        using var explicitControl = Dux.SizedBox(new ImmediateProgressControl(true), 80, 64);
        Frame(explicitControl.Render);
        check(ImmediateProgressControl.Last == new UiVector2(80, 64),
            "a user control shares public pure measurement and explicit constrained Progress rendering");
        using var legacyControl = Dux.SizedBox(new ImmediateProgressControl(false), 80, 64);
        Frame(legacyControl.Render);
        check(ImmediateProgressControl.Last == new UiVector2(220, 4),
            "the existing immediate Progress overload preserves natural geometry inside a parent");

        using var zero = Dux.SizedBox(Fluent.ProgressBar((float?)null), 0, 0);
        Frame(zero.Render);
        check(!context.State.HasActiveAnimations, "a zero-sized indeterminate Progress requests no visible animation");
        var saved = generated.Size;
        rejected = false;
        try
        {
            generated.Size = new(float.NaN, 4);
        }
        catch (ArgumentOutOfRangeException)
        {
            rejected = true;
        }
        check(rejected && generated.Size == saved, "invalid requested Progress dimensions fail without mutating the previous size");

        ProgressWidgetTests.Run(check);
        ProgressMotionTests.Run(check);
    }

    private sealed class MeasuredProbe() : UiProgress<UiFluentProgressPresentation>(.5f)
    {
        internal UiRect Bounds;

        protected override UiVector2 MeasureContent(UiControlContext context) => context.ConstrainToLayout(new(220, 4));

        protected override void RenderControl(UiImmediateContext ui)
        {
            Size = new(220, 4);
            base.RenderControl(ui);
            var position = ui.GetItemRectMin();
            var size = ui.GetItemRectSize();
            Bounds = new(position.X, position.Y, size.X, size.Y);
        }
    }

    private sealed class Screen(Action<UiImmediateContext> draw) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => draw(ui);
    }

    private sealed class ObservedDynamic(Func<UiProgressValue> source)
        : UiProgress<Func<UiProgressValue>, UiGetterProgressSource, UiFluentProgressPresentation>(source)
    {
        internal UiRect Bounds;
        protected override void RenderControl(UiImmediateContext ui)
        {
            base.RenderControl(ui);
            var position = ui.GetItemRectMin();
            var size = ui.GetItemRectSize();
            Bounds = new(position.X, position.Y, size.X, size.Y);
        }
    }

    private sealed class ImmediateProgressControl(bool constrain) : UiControlWidget
    {
        internal static UiVector2 Last;
        protected override UiVector2 MeasureContent(UiControlContext context) =>
            context.MeasureProgress<UiFluentProgressPresentation>(new(220, 4));
        protected override void RenderControl(UiImmediateContext ui)
        {
            ui.ProgressBar<UiFluentProgressPresentation>(new(.5f), new(220, 4), constrain);
            Last = ui.GetItemRectSize();
        }
    }
}

internal struct ProgressCountingBehavior : IUiProgressBehavior<ProgressCountingBehavior>
{
    internal static int Evaluations;
    public static UiProgressValue Evaluate(ref ProgressCountingBehavior state, UiProgressValue target, double time, out bool animating)
    {
        Evaluations++;
        animating = false;
        return target;
    }
    public static void Release(ref ProgressCountingBehavior state) { }
}

internal readonly record struct ProgressMetadata(Func<UiProgressValue> Reader, UiProgressValue Layout);
internal readonly struct ProgressMetadataSource : IUiProgressSource<ProgressMetadata>
{
    public static ProgressMetadata Normalize(ProgressMetadata source) => source;
    public static UiProgressValue Read(ProgressMetadata source) => source.Reader().Validate();
    public static UiVector2 Measure<TPresentation>(UiControlContext context, ProgressMetadata source, UiVector2 size)
        where TPresentation : IUiProgressPresentation => context.MeasureProgress<TPresentation>(source.Layout, size);
}

internal readonly struct MissingProgressArrange : IUiProgressPresentation
{
    public static UiVector2 Measure(UiControlContext context, UiProgressValue value, UiVector2 size) => new(24, 12);
    public static void Paint(UiControlContext context, UiProgressValue value, UiRect bounds) { }
}
internal readonly struct IncorrectProgressArrange : IUiProgressPresentation
{
    public static UiVector2 Measure(UiControlContext context, UiProgressValue value, UiVector2 size) => new(24, 12);
    public static UiVector2 Arrange(UiControlContext context, UiVector2 natural, UiVector2 allocated) => natural;
    public static void Paint(UiControlContext context, UiProgressValue value, UiRect bounds) { }
}
internal readonly struct AllocatedProgressPresentation : IUiProgressPresentation
{
    internal static int Paints;
    internal static UiRect Bounds;
    public static UiVector2 Measure(UiControlContext context, UiProgressValue value, UiVector2 size) => new(24, 12);
    public static UiVector2 Arrange(UiControlContext context, UiVector2 natural, UiVector2 allocated) => allocated;
    public static void Paint(UiControlContext context, UiProgressValue value, UiRect bounds)
    {
        Paints++;
        Bounds = bounds;
        context.Frame(bounds, context.Theme.Button, context.Theme.Border, 0, 1);
    }
}
[UiWidgetStyle(typeof(UiProgress<>), typeof(AllocatedProgressPresentation))]
internal partial class GeneratedAllocatedProgress;
