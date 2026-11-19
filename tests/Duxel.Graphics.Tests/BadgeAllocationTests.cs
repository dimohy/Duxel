using Duxel.Core;

internal static class BadgeAllocationTests
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
        using var badge = new ObservedBadge("Hi");
        using var box = Dux.SizedBox(badge, 80, 64);
        Frame(ui =>
        {
            try
            {
                check(box.MeasureConstrained(ui, new()) == new UiVector2(80, 64), "Badge supports pure constrained measurement");
            }
            catch (NotSupportedException)
            {
                check(false, "Badge supports pure constrained measurement");
            }
            badge.Render(ui);
            check(badge.Bounds.Width < 80 && badge.Bounds.Height == 20, "standalone Badge preserves its existing natural root");
            ui.SetCursorScreenPos(default);
            try
            {
                box.Render(ui);
            }
            catch (NotSupportedException)
            {
                check(false, "Badge renders inside a sized parent");
                return;
            }
            check(badge.Bounds == new UiRect(0, 0, 80, 64), $"Badge actual root equals parent allocation; actual = {badge.Bounds}");
        });
        foreach (var size in new[] { new UiVector2(12, 8), new UiVector2(0, 0) })
        {
            box.Options = new(size.X, size.Y);
            Frame(ui =>
            {
                box.Render(ui);
                check(badge.Bounds == new UiRect(0, 0, size.X, size.Y), "Badge honors narrow/short/zero allocation : " + size);
            });
        }
        box.Options = new(80, 64);
        using var counter = new ObservedCounter();
        using var counterBox = Dux.SizedBox(counter, 80, 64);
        Frame(ui =>
        {
            check(counter.Measure(ui, new(200, 200)) == default, "hidden CounterBadge has zero natural size");
            check(counterBox.MeasureConstrained(ui, new()) == new UiVector2(80, 64), "hidden CounterBadge preserves a tight parent's requested root");
            counterBox.Render(ui);
            check(counter.Bounds == new UiRect(0, 0, 80, 64), "hidden CounterBadge reserves the actual root without painting");
        });
        counter.Count = 108;
        Frame(ui =>
        {
            var cursor = ui.GetCursorScreenPos();
            ui.PushFontSize(28);
            counter.Measure(ui, new(200, 200));
            var measuredText = counter.Content.Text;
            counter.Render(ui);
            check(ReferenceEquals(measuredText, counter.Content.Text) && counter.Content.Text == "99+",
                "counter measurement and rendering share the existing bounded numeric-string memo");
            check(ui.GetFontSize() == 28, "Badge measurement/rendering restore the caller's font scope");
            ui.PopFontSize();
            ui.SetCursorScreenPos(cursor);
        });
        counter.Dot = true;
        Frame(ui =>
        {
            check(counter.Measure(ui, new(200, 200)) == new UiVector2(6, 6), "dot CounterBadge retains its natural 6x6 geometry");
            counterBox.Render(ui);
            check(counter.Bounds == new UiRect(0, 0, 80, 64) && counter.Content.AccessibleLabel == "108",
                "allocated dot CounterBadge preserves source description and actual root");
        });
        using var hidden = Dux.SizedBox(new UiBadge<UiCounterBadgeValue, UiCounterBadgeBehavior, HiddenBadgePresentation>(default), 80, 64);
        Frame(ui =>
        {
            check(hidden.MeasureConstrained(ui, new()) == new UiVector2(80, 64), "hidden Badge measurement does not consult presentation metadata or measurement");
            hidden.Render(ui);
            check(ui.GetItemRectSize() == new UiVector2(80, 64), "hidden Badge reserves its parent root without consulting or rendering its presentation");
        });
        var reads = 0;
        using var dynamic = Fluent.Badge(() =>
        {
            reads++;
            return "Live";
        });
        Frame(ui =>
        {
            var cursor = ui.GetCursorScreenPos();
            dynamic.Measure(ui, new(200, 200));
            check(reads == 1 && ui.GetCursorScreenPos() == cursor, "explicit Badge measurement reads current content once and preserves the caller cursor");
            dynamic.Render(ui);
        });
        check(reads == 2, "standalone Badge rendering reads once without adding a separate source-read phase");

        using var icon = new BadgeValidationIconWidget("");
        Frame(ui => check(Reject<ArgumentException>(() => icon.Measure(ui, new(200, 200))), "icon-only Badge measurement enforces the same accessible-label requirement as rendering"));
        icon.AccessibleLabel = "Completed";
        Frame(ui => check(icon.Measure(ui, new(200, 200)) == new UiVector2(20, 20), "icon-only Badge measures fixed icon metrics without painting"));

        using var missing = Dux.SizedBox(new UiBadge<string, UiTextBadgeBehavior, UnmeasuredBadgePresentation>("Custom"), 80, 64);
        using var invalid = new UiBadge<string, UiTextBadgeBehavior, InvalidBadgePresentation>("Custom");
        using var multiple = Dux.SizedBox(new UiBadge<string, UiTextBadgeBehavior, MultipleBadgePresentation>("Custom"), 80, 64);
        Frame(ui =>
        {
            check(Reject<NotSupportedException>(() => missing.MeasureConstrained(ui, new())), "custom Badge requires explicit pure measurement");
            check(Reject<InvalidOperationException>(() => invalid.Measure(ui, new(200, 200))), "invalid Badge extents fail explicitly");
            check(Reject<InvalidOperationException>(() => multiple.Render(ui)), "opted-in Badge must place exactly one common root");
            ui.SetCursorScreenPos(default);
            box.Render(ui);
            check(badge.Bounds == new UiRect(0, 0, 80, 64), "Badge allocation scopes recover after rejected custom presentations");
        });
        Frame(ui =>
        {
            if (!ui.BeginTable("allocated-badge-table", 1))
                throw new InvalidOperationException("Table fixture did not open.");
            ui.TableSetupColumn("Column", 200, .5f);
            ui.TableNextRow();
            box.Render(ui);
            var p = ui.GetItemRectMin();
            var s = ui.GetItemRectSize();
            check(badge.Bounds == new UiRect(p.X, p.Y, s.X, s.Y), "Badge uses the actual parent table root without duplicate legacy positioning");
            ui.EndTable();
        });
        BadgeWidgetTests.Run(check);
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

    private sealed class ObservedCounter() : UiFluentCounterBadge()
    {
        internal UiRect Bounds;
        internal UiBadgeContent Content;
        protected override UiBadgeContent ReadContent() => Content = base.ReadContent();
        protected override void RenderControl(UiImmediateContext ui)
        {
            base.RenderControl(ui);
            var p = ui.GetItemRectMin();
            var s = ui.GetItemRectSize();
            Bounds = new(p.X, p.Y, s.X, s.Y);
        }
    }

    private sealed class ObservedBadge(string text) : UiFluentBadge(text)
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

internal readonly struct UnmeasuredBadgePresentation : IUiBadgePresentation
{
    public static void Render(UiControlContext context, UiBadgeContent content, UiFluentBadgeOptions options) => context.PlaceText(new(24, 12));
}
internal readonly struct InvalidBadgePresentation : IUiBadgePresentation
{
    public static UiVector2 Measure(UiControlContext context, UiBadgeContent content, UiFluentBadgeOptions options) => new(float.NaN, 12);
    public static void Render(UiControlContext context, UiBadgeContent content, UiFluentBadgeOptions options) => context.PlaceText(new(24, 12));
}
internal readonly struct MultipleBadgePresentation : IUiBadgePresentation
{
    public static bool CanArrange => true;
    public static UiVector2 Measure(UiControlContext context, UiBadgeContent content, UiFluentBadgeOptions options) => new(24, 12);
    public static void Render(UiControlContext context, UiBadgeContent content, UiFluentBadgeOptions options)
    {
        context.PlaceText(new(24, 12));
        context.PlaceText(new(24, 12));
    }
}
internal readonly struct HiddenBadgePresentation : IUiBadgePresentation
{
    public static bool CanArrange => throw new InvalidOperationException("Hidden presentation metadata must not be read.");
    public static UiVector2 Measure(UiControlContext context, UiBadgeContent content, UiFluentBadgeOptions options) =>
        throw new InvalidOperationException("Hidden presentation must not be measured.");
    public static void Render(UiControlContext context, UiBadgeContent content, UiFluentBadgeOptions options) =>
        throw new InvalidOperationException("Hidden presentation must not be rendered.");
}
