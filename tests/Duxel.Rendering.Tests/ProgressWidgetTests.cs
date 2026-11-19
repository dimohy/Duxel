using Duxel.Core;

internal static class ProgressWidgetTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        var requests = 0;
        context.SetRequestFrameCallback(() => requests++);
        UiDrawData Frame(Action<UiImmediateContext> render)
        {
            context.NewFrame(new(.25f, new(400, 240), new(1, 1)), idle, new(0, 0, 400, 240), UiTextSettings.Default);
            context.Render(new Screen(render));
            return context.GetDrawData();
        }
        using var progress = Fluent.ProgressBar(1, 4);
        progress.Size = new(200, 0);
        progress.Shape = UiProgressShape.Square;
        var size = default(UiVector2);
        UiRectFilledPrimitive[] widget;
        static IEnumerable<UiRectFilledPrimitive> Rects(UiDrawData data) => data.DrawLists.SelectMany(list =>
            list.RectFilledPrimitives is { } rectangles ? rectangles.ToArray() : Array.Empty<UiRectFilledPrimitive>());
        using (var data = Frame(ui => { progress.Render(ui); size = ui.GetItemRectSize(); }))
            widget = Rects(data).ToArray();
        check(size == new UiVector2(200, 2), "generated ProgressBar uses the official medium2px line and requested width");
        var fill = widget.Where(vertex => vertex.Color == context.GetStyle().ProgressBarFill).ToArray();
        check(fill.Length == 1 && MathF.Abs(fill[0].Rect.Width - 50) < .001f,
            "ProgressBar uses value/maximum for actual fill primitive geometry");
        using (var data = Frame(ui => ui.ProgressBar<UiFluentProgressPresentation>(new(1, 4, UiProgressShape.Square), new(200, 0))))
            check(widget.SequenceEqual(Rects(data)), "typed immediate and generated progress consume the same geometry kernel");
        progress.Thickness = UiProgressThickness.Large;
        using (var data = Frame(ui => { progress.Render(ui); size = ui.GetItemRectSize(); }))
        {
        }
        check(size.Y == 4, "large ProgressBar reserves the official4px thickness");
        check(new UiProgressValue(-1).Fraction == 0 && new UiProgressValue(20, 4).Fraction == 1,
            "finite out-of-range progress values clamp at both ends");
        foreach (var invalid in new[]{new UiProgressValue(float.NaN),new UiProgressValue(float.PositiveInfinity),
            new UiProgressValue(0,0),new UiProgressValue(0,float.NaN),new UiProgressValue(0,Shape:(UiProgressShape)99)})
        {
            var rejected = false;
            try
            {
                invalid.Validate();
            }
            catch (ArgumentOutOfRangeException) { rejected = true; }
            check(rejected, "invalid progress input is rejected rather than drawing an invalid width: " + invalid);
        }
        var reads = 0;
        using var dynamic = Fluent.ProgressBar(() => { reads++; return new(.5f); });
        using (var data = Frame(dynamic.Render))
        {
        }
        using (var data = Frame(dynamic.Render))
        {
        }
        check(reads == 2, "generated progress getter executes exactly once per rendered frame");
        var invalidGetter = false;
        using var bad = Fluent.ProgressBar(static () => new UiProgressValue(float.NaN));
        try
        {
            using var data = Frame(bad.Render);
        }
        catch (ArgumentOutOfRangeException) { invalidGetter = true; }
        check(invalidGetter, "dynamic progress validates the callback result before layout");
        using var indeterminate = Fluent.ProgressBar((float?)null);
        indeterminate.Size = new(200, 0);
        requests = 0;
        using (var data = Frame(indeterminate.Render))
        {
            var rectangles = Rects(data).ToArray();
            var commands = data.DrawLists.SelectMany(list => list.Commands).Where(command => command.ElementCount > 0).ToArray();
            check(rectangles.Length == 2 && rectangles[0].CornerRadius == 1 &&
                rectangles[1].Color == context.GetStyle().ProgressBarFill && commands.Length == 1 &&
                commands[0].IsLinearGradientPrimitive && commands[0].PrimitiveInstanceCount == 1 &&
                commands[0].TextureId == new UiTextureId(2),
                "indeterminate progress uses one rounded gradient instance and the white texture: " +
                $"rectangles={rectangles.Length}, radius={(rectangles.Length > 0 ? rectangles[0].CornerRadius : 0)}, commands={commands.Length}, " +
                $"details={string.Join(';', commands)}");
        }
        check(context.State.HasActiveAnimations && requests == 0 && context.State.NextScheduledFrameTimeSeconds > context.State.TimeSeconds, "visible indeterminate progress schedules animation frames");
        requests = 0;
        using (var data = Frame(ui => { ui.SetCursorScreenPos(new(1000, 1000)); indeterminate.Render(ui); }))
        {
        }
        check(!context.State.HasActiveAnimations && requests == 0, "clipped indeterminate progress does not keep the render loop active");
        indeterminate.ReducedMotion = true;
        using (var data = Frame(indeterminate.Render))
            check(Rects(data).Any(primitive => primitive.Color != context.GetStyle().ProgressBarBg && (primitive.Color.Rgba >> 24) < 255),
                "reduced-motion progress pulses opacity instead of translating the bar");
        requests = 0;
        using (var data = Frame(progress.Render))
        {
        }
        check(!context.State.HasActiveAnimations && requests == 0, "unchanged determinate progress does not request animation frames");
        foreach (var color in Enum.GetValues<UiProgressColor>())
        {
            progress.Color = color;
            using var data = Frame(progress.Render);
            check(Rects(data).Any(), "progress color variant produces actual geometry: " + color);
        }
        using var custom = new UiProgress<CustomPresentation>(.5f);
        using (var data = Frame(ui => { custom.Render(ui); size = ui.GetItemRectSize(); }))
            check(size == new UiVector2(40, 40) && Rects(data).Any(primitive => primitive.CornerRadius < 0),
                "user progress presentation replaces bar layout with actual analytic circle geometry");
    }
    private readonly struct CustomPresentation : IUiProgressPresentation
    {
        public static UiVector2 Measure(UiControlContext context, UiProgressValue value, UiVector2 requestedSize) => new(40, 40);
        public static void Paint(UiControlContext context, UiProgressValue value, UiRect bounds) =>
            context.Circle(new(bounds.X + 20, bounds.Y + 20), 20, context.Theme.ProgressBarFill);
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
