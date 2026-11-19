using Duxel.Core;

internal static class LinkAllocationTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
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
        using var link = new ObservedLink("Read the guide");
        using var box = Dux.SizedBox(link, 80, 64);
        Frame(ui =>
        {
            try
            {
                check(box.MeasureConstrained(ui, new()) == new UiVector2(80, 64), "Link supports pure constrained measurement");
            }
            catch (NotSupportedException)
            {
                check(false, "Link supports pure constrained measurement");
            }
            link.Render(ui);
            check(link.Bounds.Width > 80 && link.Bounds.Height == 20, "standalone Link preserves its existing natural root");
            ui.SetCursorScreenPos(default);
            try
            {
                box.Render(ui);
            }
            catch (NotSupportedException)
            {
                check(false, "Link renders inside a sized parent");
                return;
            }
            check(link.Bounds == new UiRect(0, 0, 80, 64), $"Link actual root equals parent allocation; actual = {link.Bounds}");
        });

        var navigations = 0;
        Uri? visited = null;
        link.Navigate = destination =>
        {
            navigations++;
            visited = destination;
        };
        Frame(box.Render, idle with { MousePosition = new(70, 50), LeftMouseDown = true, LeftMousePressed = true });
        check(navigations == 0, "allocated Link pointer press does not navigate early");
        link.Label = "Renamed";
        Frame(box.Render, idle with { MousePosition = new(70, 50), LeftMouseReleased = true });
        check(navigations == 1 && visited == link.Destination, "pointer release in the allocated blank area activates once and preserves identity across label rename");
        Frame(box.Render, idle with { KeyEvents = [new(UiKey.Space, true, default)] });
        Frame(box.Render, idle with { KeyEvents = [new(UiKey.Space, false, default)] });
        check(navigations == 1, "allocated navigation Link still ignores Space");
        link.Destination = new("/current", UriKind.Relative);
        Frame(box.Render, idle with { KeyEvents = [new(UiKey.Enter, true, default)] });
        Frame(box.Render, idle with { KeyEvents = [new(UiKey.Enter, false, default)] });
        check(navigations == 2 && visited == link.Destination, "focused allocated Link navigates once on Enter to its current destination");
        box.Options = new(20, 64);
        Frame(box.Render, idle with { MousePosition = new(70, 10), LeftMouseDown = true, LeftMousePressed = true });
        Frame(box.Render, idle with { MousePosition = new(70, 10), LeftMouseReleased = true });
        check(navigations == 2, "clipped glyphs outside the narrower actual root are not clickable");
        box.Options = new(80, 8);
        Frame(box.Render, idle with { MousePosition = new(10, 12), LeftMouseDown = true, LeftMousePressed = true });
        Frame(box.Render, idle with { MousePosition = new(10, 12), LeftMouseReleased = true });
        check(navigations == 2, "clipped text below the short allocated height is not clickable");
        box.Options = new(0, 0);
        Frame(box.Render, idle with { KeyEvents = [new(UiKey.Enter, true, default)] });
        check(navigations == 2 && link.Bounds == new UiRect(0, 0, 0, 0), "zero root clears keyboard participation and cannot navigate");

        box.Options = new(80, 64);
        Frame(ui =>
        {
            using var clip = new UiControlContext(ui).Clip(new(0, 0, 20, 64));
            box.Render(ui);
        }, idle with { MousePosition = new(70, 50), LeftMouseDown = true, LeftMousePressed = true });
        Frame(ui =>
        {
            using var clip = new UiControlContext(ui).Clip(new(0, 0, 20, 64));
            box.Render(ui);
        }, idle with { MousePosition = new(70, 50), LeftMouseReleased = true });
        check(navigations == 2, "ancestor clipping also limits the actual Link pointer region");

        using var probeContent = new UiLinkContent<UiFluentLinkPresentation<UiFluentDefaultLinkAppearance, UiStandaloneLinkUnderline>>();
        using var probe = new LinkControlProbe(probeContent);
        using var probeBox = Dux.SizedBox(probe, 80, 64);
        LinkAllocationBehavior.Calls = 0;
        Frame(ui =>
        {
            var cursor = ui.GetCursorScreenPos();
            var focused = context.State.FocusedId;
            var size = probeBox.MeasureConstrained(ui, new());
            check(size == new UiVector2(80, 64) && ui.GetCursorScreenPos() == cursor && context.State.FocusedId == focused,
                "Link measurement preserves caller cursor and focus while reporting assigned geometry");
            check(LinkAllocationBehavior.Calls == 0, "Link measurement never evaluates input behavior");
            probeBox.Render(ui);
        });
        check(probe.Bounds == new UiRect(0, 0, 80, 64) && LinkAllocationBehavior.Bounds == probe.Bounds,
            "measurement, input behavior and last-item state share the same Link root");
        check(probe.Content.LayoutBuildCount == 1, "same-width measure and render share one retained natural Link layout");
        probeBox.Options = new(80, 8);
        Frame(probeBox.Render);
        check(probe.Content.LayoutBuildCount == 1 && probe.Bounds.Height == 8,
            "height-only allocation changes arrange a local copy without corrupting retained natural layout");
        probeBox.Options = new(160, 64);
        Frame(probeBox.Render);
        check(probe.Content.LayoutBuildCount == 2 && probe.Bounds.Width == 160,
            "actual parent width changes invalidate retained Link wrapping exactly once");
        probe.Label = "Several words wrap at the actual parent width.\nSecond line";
        Frame(probeBox.Render);
        check(probe.Content.LayoutBuildCount == 3, "changed Link content invalidates the same bounded layout cache");
        probe.Constrain = false;
        Frame(probeBox.Render);
        check(new UiVector2(probe.Bounds.Width, probe.Bounds.Height) != new UiVector2(160, 64), "the existing immediate Link overload preserves natural sizing inside a parent");

        using var unsupported = Dux.SizedBox(new UiLink<LinkAllocationBehavior, FixedLinkPresentation>("Custom", new("/custom", UriKind.Relative), static _ => { }), 80, 64);
        using var invalidArrange = Dux.SizedBox(new UiLink<LinkAllocationBehavior, InvalidArrangeLinkPresentation>("Custom", new("/custom", UriKind.Relative), static _ => { }), 80, 64);
        using var invalidSize = new UiLink<LinkAllocationBehavior, InvalidSizeLinkPresentation>("Custom", new("/custom", UriKind.Relative), static _ => { });
        Frame(ui =>
        {
            var calls = LinkAllocationBehavior.Calls;
            check(Reject<NotSupportedException>(() => unsupported.MeasureConstrained(ui, new())), "custom Link must explicitly implement changed parent arrangement");
            check(Reject<InvalidOperationException>(() => invalidArrange.Render(ui)), "a custom Link cannot return the wrong assigned size");
            check(Reject<InvalidOperationException>(() => invalidSize.Measure(ui, new(200, 200))), "nonfinite Link natural extents fail explicitly");
            check(LinkAllocationBehavior.Calls == calls, "rejected Link geometry never reaches input behavior");
            ui.SetCursorScreenPos(default);
            box.Render(ui);
            check(link.Bounds == new UiRect(0, 0, 80, 64), "the next Link recovers after failed measurement/arrangement");
        });
        Frame(ui =>
        {
            if (!ui.BeginTable("allocated-link-table", 1))
                throw new InvalidOperationException("Table fixture did not open.");
            ui.TableSetupColumn("Column", 200, .5f);
            ui.TableNextRow();
            box.Render(ui);
            var p = ui.GetItemRectMin();
            var s = ui.GetItemRectSize();
            check(link.Bounds == new UiRect(p.X, p.Y, s.X, s.Y), "allocated Link uses the parent's table root without applying legacy positioning twice");
            ui.EndTable();
        });
        LinkWidgetTests.Run(check);
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

    private sealed class ObservedLink(string label) : UiFluentLink(label, new("/details", UriKind.Relative), static _ => { })
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

internal readonly struct LinkAllocationBehavior : IUiButtonBehavior
{
    internal static int Calls;
    internal static UiRect Bounds;
    public static UiButtonInteraction Process(UiControlContext context, string id, UiRect bounds)
    {
        Calls++;
        Bounds = bounds;
        return new(false, false, false);
    }
}
internal sealed class LinkControlProbe(UiLinkContent<UiFluentLinkPresentation<UiFluentDefaultLinkAppearance, UiStandaloneLinkUnderline>> content) : UiControlWidget
{
    internal readonly UiLinkContent<UiFluentLinkPresentation<UiFluentDefaultLinkAppearance, UiStandaloneLinkUnderline>> Content = content;
    internal string Label = "Read the guide";
    internal bool Constrain = true;
    internal UiRect Bounds;
    protected override UiVector2 MeasureContent(UiControlContext context) => context.MeasureLink(Label, Content, 220);
    protected override void RenderControl(UiImmediateContext ui)
    {
        ui.Link<LinkAllocationBehavior, UiFluentLinkPresentation<UiFluentDefaultLinkAppearance, UiStandaloneLinkUnderline>>(
            Label, new("/probe", UriKind.Relative), static _ => { }, Content, 220, Constrain);
        var p = ui.GetItemRectMin();
        var s = ui.GetItemRectSize();
        Bounds = new(p.X, p.Y, s.X, s.Y);
    }
}
internal readonly struct FixedLinkPresentation : IUiLinkPresentation
{
    public static UiControlTypographyScope BeginText(UiControlContext context) => default;
    public static UiLinkLayout Measure(UiControlContext context, string text, float width) => new(new(24, 12), default);
    public static void Paint(UiControlContext context, in UiLinkLayout layout, UiRect bounds, UiButtonInteraction interaction)
    {
    }
}
internal readonly struct InvalidArrangeLinkPresentation : IUiLinkPresentation
{
    public static UiControlTypographyScope BeginText(UiControlContext context) => default;
    public static UiLinkLayout Measure(UiControlContext context, string text, float width) => new(new(24, 12), default);
    public static UiLinkLayout Arrange(UiControlContext context, in UiLinkLayout natural, UiVector2 allocatedSize) => natural;
    public static void Paint(UiControlContext context, in UiLinkLayout layout, UiRect bounds, UiButtonInteraction interaction)
    {
    }
}
internal readonly struct InvalidSizeLinkPresentation : IUiLinkPresentation
{
    public static UiControlTypographyScope BeginText(UiControlContext context) => default;
    public static UiLinkLayout Measure(UiControlContext context, string text, float width) => new(new(float.NaN, 12), default);
    public static void Paint(UiControlContext context, in UiLinkLayout layout, UiRect bounds, UiButtonInteraction interaction)
    {
    }
}
