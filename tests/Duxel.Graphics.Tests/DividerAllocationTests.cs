using Duxel.Core;

internal static class DividerAllocationTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
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

        using var divider = new ObservedDivider("Section", new(Length : 220));
        using var box = Dux.SizedBox(divider, 80, 64);
        Frame(ui =>
        {
            try
            {
                check(box.MeasureConstrained(ui, new()) == new UiVector2(80, 64), "Divider supports pure constrained measurement");
            }
            catch (NotSupportedException)
            {
                check(false, "Divider supports pure constrained measurement");
            }
            divider.Render(ui);
            check(divider.Bounds == new UiRect(0, 0, 220, 16), "standalone Divider preserves its existing natural root");
            ui.SetCursorScreenPos(default);
            try
            {
                box.Render(ui);
            }
            catch (NotSupportedException)
            {
                check(false, "Divider renders inside a sized parent");
                return;
            }
            check(divider.Bounds == new UiRect(0, 0, 80, 64), $"Divider actual root equals parent allocation; actual = {divider.Bounds}");
        });

        foreach (var vertical in new[] { false, true })
        foreach (var size in new[] { new UiVector2(80, 64), new UiVector2(12, 8), new UiVector2(0, 0) })
        {
            divider.Options = divider.Options with { Vertical = vertical, Inset = true };
            box.Options = new(size.X, size.Y);
            Frame(ui =>
            {
                box.Render(ui);
                check(divider.Bounds == new UiRect(0, 0, size.X, size.Y),
                    $"Divider honors both axes and zero allocation; vertical = {vertical}, size = {size}");
            });
        }
        divider.Options = new(Length : 220);
        box.Options = new(80, 64);
        divider.Text = "Several words wrap at the assigned width";
        Frame(ui =>
        {
            var cursor = ui.GetCursorScreenPos();
            ui.PushFontSize(28);
            var size = divider.MeasureConstrained(ui, new(80, 80, 0, 200));
            check(size.X == 80 && size.Y > 16 && size.Y % 16 == 0,
                "pure Divider measurement wraps 12px text at the constrained width with 16px line boxes");
            check(ui.GetCursorScreenPos() == cursor && ui.GetFontSize() == 28,
                "measurement restores the caller's cursor and font scope");
            ui.PopFontSize();
        });

        var reads = 0;
        using var dynamic = Fluent.Divider(() =>
        {
            reads++;
            return "Dynamic";
        }, new(Length : 220));
        Frame(ui =>
        {
            dynamic.Measure(ui, new(200, 200));
            check(reads == 1, "explicit Divider measurement reads its content provider exactly once");
            dynamic.Render(ui);
        });
        check(reads == 2, "standalone render reads current Divider content once without an extra measurement phase");

        using var missing = Dux.SizedBox(new UiDivider<UnmeasuredDividerPresentation>("Custom"), 80, 64);
        using var fixedSize = Dux.SizedBox(new UiDivider<FixedDividerPresentation>("Custom"), 80, 64);
        using var doubleRoot = Dux.SizedBox(new UiDivider<DoubleDividerPresentation>("Custom"), 80, 64);
        using var invalidSize = new UiDivider<InvalidDividerPresentation>("Custom");
        using var drawingMeasure = new UiDivider<DrawingMeasureDividerPresentation>("Custom");
        Frame(ui =>
        {
            check(Reject<NotSupportedException>(() => missing.MeasureConstrained(ui, new())), "missing custom Divider measurement fails explicitly");
            check(Reject<NotSupportedException>(() => fixedSize.MeasureConstrained(ui, new())), "custom Divider must opt into changed allocation");
            check(Reject<InvalidOperationException>(() => doubleRoot.Render(ui)), "an opted-in Divider cannot place multiple common roots");
            check(Reject<InvalidOperationException>(() => invalidSize.Measure(ui, new(200, 200))), "nonfinite custom Divider measurement fails explicitly");
            check(Reject<InvalidOperationException>(() => drawingMeasure.Measure(ui, new(200, 200))), "Divider measurement cannot call the paint entry point");
            ui.SetCursorScreenPos(default);
            box.Render(ui);
            check(divider.Bounds == new UiRect(0, 0, 80, 64), "Divider allocation and typography scopes recover after rejected custom presentations");
        });
        Frame(ui =>
        {
            if (!ui.BeginTable("allocated-divider-table", 1))
                throw new InvalidOperationException("Table fixture did not open.");
            ui.TableSetupColumn("Column", 200, .5f);
            ui.TableNextRow();
            box.Render(ui);
            var p = ui.GetItemRectMin();
            var s = ui.GetItemRectSize();
            check(divider.Bounds == new UiRect(p.X, p.Y, s.X, s.Y), "Divider uses the parent's actual table root without applying legacy alignment twice");
            ui.EndTable();
        });
        using var generated = Dux.SizedBox(new GeneratedAllocatedDivider("Custom"), 80, 64);
        Frame(generated.Render);
        check(CustomAllocatedDividerPresentation.Last == new UiRect(0, 0, 80, 64), "generated user Divider participates through its closed presentation contract");
        DividerWidgetTests.Run(check);
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

    private sealed class ObservedDivider(string text, UiDividerOptions options) : UiDivider<UiFluentDividerPresentation>(text, options)
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
}

internal readonly struct UnmeasuredDividerPresentation : IUiDividerPresentation
{
    public static void Render(UiControlContext context, string text, UiDividerOptions options) => context.PlaceText(new(24, 12));
}
internal readonly struct FixedDividerPresentation : IUiDividerPresentation
{
    public static UiVector2 Measure(UiControlContext context, string text, UiDividerOptions options) => new(24, 12);
    public static void Render(UiControlContext context, string text, UiDividerOptions options) => context.PlaceText(new(24, 12));
}
internal readonly struct DoubleDividerPresentation : IUiDividerPresentation
{
    public static bool CanArrange => true;
    public static UiVector2 Measure(UiControlContext context, string text, UiDividerOptions options) => new(24, 12);
    public static void Render(UiControlContext context, string text, UiDividerOptions options)
    {
        context.PlaceText(new(24, 12));
        context.PlaceText(new(24, 12));
    }
}
internal readonly struct InvalidDividerPresentation : IUiDividerPresentation
{
    public static UiVector2 Measure(UiControlContext context, string text, UiDividerOptions options) => new(float.NaN, 12);
    public static void Render(UiControlContext context, string text, UiDividerOptions options) => context.PlaceText(new(24, 12));
}
internal readonly struct DrawingMeasureDividerPresentation : IUiDividerPresentation
{
    public static UiVector2 Measure(UiControlContext context, string text, UiDividerOptions options)
    {
        context.FluentDivider(text, options);
        return new(24, 12);
    }
    public static void Render(UiControlContext context, string text, UiDividerOptions options) => context.FluentDivider(text, options);
}
internal readonly struct CustomAllocatedDividerPresentation : IUiDividerPresentation
{
    internal static UiRect Last;
    public static bool CanArrange => true;
    public static UiVector2 Measure(UiControlContext context, string text, UiDividerOptions options) => new(24, 12);
    public static void Render(UiControlContext context, string text, UiDividerOptions options)
    {
        var layout = context.PlaceText(new(24, 12));
        Last = new(layout.Position.X, layout.Position.Y, layout.Size.X, layout.Size.Y);
        context.Frame(Last, context.Theme.Button, context.Theme.Border, 0, 1);
    }
}
[UiWidgetStyle(typeof(UiDivider<>), typeof(CustomAllocatedDividerPresentation))]
internal partial class GeneratedAllocatedDivider;
