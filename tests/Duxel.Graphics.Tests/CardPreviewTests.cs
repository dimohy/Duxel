using Duxel.Core;

internal static class CardPreviewTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(Action<UiImmediateContext> draw)
        {
            context.NewFrame(new(1f / 60, new(600, 500), new(1, 1)), idle, new(0, 0, 600, 500), UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding: 0); draw(ui); }));
            using var data = context.GetDrawData();
        }
        static bool Throws<T>(Action action) where T : Exception
        {
            try
            {
                action();
                return false;
            }
            catch (T) { return true; }
        }
        var body = new Probe(new(100, 80));
        var logo = new Probe(new(90, 90));
        using var standalone = Fluent.CardPreview(body, logo);
        Frame(ui => check(standalone.MeasureConstrained(ui, new(0, 500, 0, 400)) == new UiVector2(100, 80),
            "Card Preview logo does not contribute to intrinsic size"));
        check(logo.Measures == 0, "Card Preview measurement never executes absolute logo measurement");
        Frame(standalone.Render);
        check(body.Bounds == new UiRect(0, 0, 100, 80) && logo.Bounds == new UiRect(12, 36, 32, 32),
            "standalone Card Preview fills content and places its 32px logo at left/bottom12px");
        var measures = body.Measures + logo.Measures;
        Frame(standalone.Render);
        check(body.Measures + logo.Measures == measures, "Card Preview stable replay retains both actual slot measurements");
        using var shared = new Probe(new(1, 1));
        check(Throws<ArgumentException>(() => _ = Fluent.CardPreview(shared, shared)), "Card Preview rejects duplicate slot ownership atomically");
        using var owner = Fluent.CardPreview(shared);
        using var free = new Probe(new(1, 1));
        check(Throws<InvalidOperationException>(() => _ = Fluent.CardPreview(free, shared)), "Card Preview preflights all slots before adopting any child");
        using var freeOwner = Fluent.CardPreview(free);
        using var invalidContent = Dux.CardContent([]);
        Frame(ui => check(Throws<InvalidOperationException>(() => invalidContent.Measure(ui, new(600, 500))), "CardContent fails outside its primary Card slot"));

        foreach (var size in Enum.GetValues<UiCardSize>())
        {
            var p = size == UiCardSize.Small ? 8 : size == UiCardSize.Large ? 16 : 12;
            foreach (var orientation in Enum.GetValues<UiCardOrientation>())
            {
                var first = new Probe(new(80, 60));
                var middle = new Probe(new(80, 60));
                var last = new Probe(new(80, 60));
                using var card = new UiCard<Flat>("Preview slots", Dux.CardContent([
                    Fluent.CardPreview(first), Fluent.CardPreview(middle), Fluent.CardPreview(last)]), new(size, Width: orientation == UiCardOrientation.Horizontal ? 300 : 240, Orientation: orientation));
                Frame(card.Render);
                if (orientation == UiCardOrientation.Vertical)
                {
                    check(first.Bounds == new UiRect(0, 0, 240, 60) && middle.Bounds == new UiRect(0, 60 + p, 240, 60)
                        && last.Bounds == new UiRect(0, 120 + 2 * p, 240, 60), "vertical Preview first/middle/last full bleed uses parent size : " + size);
                }
                else
                {
                    check(first.Bounds == new UiRect(0, 0, 80, 60) && middle.Bounds == new UiRect(80 + p, 0, 80, 60)
                        && last.Bounds == new UiRect(160 + 2 * p, 0, 80, 60), "horizontal Preview first/middle/last uses top/bottom bleed : " + size);
                }
            }
        }
        var directBody = new Probe(new(80, 60));
        var directLogo = new Probe(new(20, 20));
        using var direct = new UiCard<Flat>("Direct Preview", Fluent.CardPreview(directBody, directLogo), new(Width: 200, Height: 120));
        Frame(direct.Render);
        check(directBody.Bounds == new UiRect(0, 0, 200, 60) && directLogo.Bounds == new UiRect(12, 16, 32, 32),
            "direct Preview bleeds all four parent margins while keeping its natural main-axis content");
        direct.Options = direct.Options with
        {
            Orientation = UiCardOrientation.Horizontal
        };
        Frame(direct.Render);
        Frame(direct.Render);
        check(directBody.Bounds == new UiRect(0, 30, 80, 60), "horizontal direct Preview centers in the Card cross axis after option invalidation");
        var thumb = new Probe(new(80, 60));
        var heading = new Probe(new(40, 20));
        using var horizontalCard = new UiCard<Flat>("Horizontal Header", Dux.CardContent([
            Fluent.CardPreview(thumb), Fluent.CardHeader(heading)]), new(Width: 240, Orientation: UiCardOrientation.Horizontal));
        Frame(horizontalCard.Render);
        check(thumb.Bounds == new UiRect(0, 0, 80, 60) && heading.Bounds == new UiRect(92, 20, 136, 20),
            "horizontal Card grows its final Header into remaining width and centers the cross axis");
        check(context.State.NextScheduledFrameTimeSeconds is null, "Card Preview and CardContent never schedule periodic frames");
        check(Fluent.ComponentNames.Contains("CardPreview") && Fluent.RequiredComponentNames.Length == 55,
            "Card Preview is an exposed variant without increasing top-level requirements");
        using var logoOnly = Fluent.CardPreview(logo: new Probe(new(200, 200)));
        Frame(ui => check(logoOnly.Measure(ui, new(500, 400)) == default, "absolute logo-only Preview has zero intrinsic extent"));
        var tiny = new Probe(new(1, 1));
        using var tinyCard = new UiCard<Flat>("Tiny Preview", Fluent.CardPreview(tiny));
        Frame(tinyCard.Render);
        check(tiny.Bounds == new UiRect(0, 0, 1, 1), "Preview smaller than its negative margins preserves its visual size at zero logical extent");
        using var auto = new UiCard<Flat>("Natural Preview", Dux.CardContent([Fluent.CardPreview(new Probe(new(100, 80)))]));
        Frame(ui => check(auto.Measure(ui, new(500, 400)) == new UiVector2(100, 80), "auto CardContent full bleed removes padding from intrinsic Preview size"));
        RunInput(check);
        RunMinimumAllocation(check);
    }

    private static void RunMinimumAllocation(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(UiWidget root)
        {
            context.NewFrame(new(1f / 60, new(600, 500), new(1, 1)), idle, new(0, 0, 600, 500), UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding: 0); root.Render(ui); }));
            using var data = context.GetDrawData();
        }
        foreach (var orientation in Enum.GetValues<UiCardOrientation>())
        {
            var media = new Probe(new(80, 60));
            var heading = new Probe(new(40, 20));
            using var card = new UiCard<Flat>("Minimum Preview slots", Dux.CardContent([
                Fluent.CardPreview(media), Fluent.CardHeader(heading)]), new(Orientation: orientation));
            using var root = Dux.ConstrainedBox(card, new(240, float.PositiveInfinity, 200, float.PositiveInfinity));
            Frame(root);
            check(media.Bounds == (orientation == UiCardOrientation.Vertical ? new UiRect(0, 0, 240, 60) : new(0, 70, 80, 60)),
                "Card minimum allocation reaches Preview stretch and cross-axis centering : " + orientation);
            check(heading.Bounds == (orientation == UiCardOrientation.Vertical ? new UiRect(12, 72, 216, 20) : new(92, 90, 136, 20)),
                "Card minimum allocation reaches Header stretch or final-slot growth : " + orientation);
            var measurements = media.Measures + heading.Measures;
            Frame(root);
            check(media.Measures + heading.Measures == measurements,
                "minimum-sized Card retains unchanged child measurements : " + orientation);
            root.Options = new(300, 360, 220, 260);
            Frame(root);
            Frame(root);
            check(media.Bounds == (orientation == UiCardOrientation.Vertical ? new UiRect(0, 0, 300, 60) : new(0, 80, 80, 60)),
                "Card minimum constraint changes update actual Preview allocation without filling its loose maximum : " + orientation);
            check(heading.Bounds == (orientation == UiCardOrientation.Vertical ? new UiRect(12, 72, 276, 20) : new(92, 100, 196, 20)),
                "Card minimum constraint changes update Header geometry : " + orientation);
        }
        var direct = new Probe(new(80, 60));
        using var directCard = new UiCard<Flat>("Minimum direct Preview", Fluent.CardPreview(direct));
        using var directRoot = Dux.ConstrainedBox(directCard, new(240, 320, 200, 300));
        Frame(directRoot);
        check(direct.Bounds == new UiRect(0, 0, 240, 60),
            "direct vertical Preview uses Card minimum width while preserving its natural main-axis extent");
        check(context.State.NextScheduledFrameTimeSeconds is null, "minimum Card allocation never creates periodic frames");
    }

    private static void RunInput(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        var bodyCalls = 0;
        var logoCalls = 0;
        using var card = new UiCard<Flat>("Preview input card", Dux.CardContent([
            Fluent.CardPreview(Dux.SizedBox(Fluent.Button("Preview body", () => bodyCalls++), 200, 80),
                Fluent.Button("Preview logo", () => logoCalls++))]), new(Width: 200));
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(UiInputState? input = null)
        {
            context.NewFrame(new(1f / 60, new(400, 300), new(1, 1)), input ?? idle, new(0, 0, 400, 300), UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding: 0); card.Render(ui); }));
            using var data = context.GetDrawData();
        }
        UiAccessibilityElement Find(string name)
        {
            UiAccessibilityElement? found = null;
            void Visit(string? id)
            {
                while (id is not null && tree.TryGet(id, out var item))
                {
                    if (item.Node.Name == name)
                        found = item;
                    Visit(item.FirstChild);
                    id = item.Next;
                }
            }
            Visit(tree.Root.First);
            return found ?? throw new InvalidOperationException(name);
        }
        void Click(UiVector2 point)
        {
            Frame(idle with
            {
                MousePosition = point,
                LeftMouseDown = true,
                LeftMousePressed = true
            });
            Frame(idle with
            {
                MousePosition = point,
                LeftMouseReleased = true
            });
        }
        Frame();
        var logo = Find("Preview logo");
        var body = Find("Preview body");
        var root = Find("Preview input card");
        check(logo.Node.Bounds == new UiRect(12, 36, 32, 32) && body.Node.Bounds == new UiRect(0, 0, 200, 80),
            "Preview accessibility exposes actual full-bleed body and absolute logo bounds");
        check(logo.Node.ParentId == root.Node.Id && body.Node.ParentId == root.Node.Id,
            "Preview and CardContent add no phantom accessibility groups");
        Click(new(28, 52));
        check(logoCalls == 1 && bodyCalls == 0, "overlapping Preview logo owns pointer input above content");
        Click(new(5, 30));
        check(logoCalls == 1 && bodyCalls == 1, "Preview content accepts pointer input inside its full-bleed margin");
        tree.RequestAction(body.Node.Id, body.RuntimeId, UiAccessibilityAction.Focus, wait: false);
        Frame();
        Frame(idle with
        {
            KeyEvents = [new(UiKey.Tab, true, default)]
        });
        check(context.State.FocusedId == logo.Node.Id, "Preview navigation follows actual content then logo source order");
        Frame(idle with
        {
            KeyEvents = [new(UiKey.Space, true, default)]
        });
        Frame(idle with
        {
            KeyEvents = [new(UiKey.Space, false, default)]
        });
        check(logoCalls == 2 && bodyCalls == 1, "Preview logo retains independent keyboard activation");
    }
    private readonly struct Flat : IUiCardPresentation
    {
        public static bool CanRetainMeasurement => true;
        public static UiCardMetrics Measure(UiCardOptions options) => new(options.Size == UiCardSize.Small ? 8 : options.Size == UiCardSize.Large ? 16 : 12, 0);
        public static void Paint(UiControlContext context, UiRect bounds, UiCardOptions options, UiCardMetrics metrics, UiButtonInteraction interaction)
        {
        }
    }
    private sealed class Probe(UiVector2 size) : UiControlWidget
    {
        internal UiRect Bounds;
        internal int Measures;
        protected override bool CanRetainMeasurement => true;
        protected override UiVector2 MeasureContent(UiControlContext context)
        {
            Measures++;
            return context.LayoutConstraints.Constrain(size);
        }
        protected override void RenderControl(UiImmediateContext ui)
        {
            var actual = new UiControlContext(ui).ConstrainToLayout(size);
            var origin = ui.GetCursorScreenPos();
            Bounds = new(origin.X, origin.Y, actual.X, actual.Y);
            ui.Dummy(actual);
        }
    }
    private sealed class Screen(Action<UiImmediateContext> draw) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => draw(ui);
    }
}
