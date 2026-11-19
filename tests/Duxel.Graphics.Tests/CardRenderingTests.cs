using System.Text.Json;
using Duxel.Core;

internal static class CardRenderingTests
{
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check,
        int stride, UiFluentColorScheme scheme, UiContext context, Action<UiVector2, bool, bool> pointer)
    {
        using var reference = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "CardPalette.json")));
        using var interactionReference = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "CardInteractionPalette.json")));
        var colors = reference.RootElement.GetProperty(scheme.ToString());
        static byte[] Color(JsonElement value) => Convert.FromHexString(value.GetString()!.AsSpan(1));
        byte[] InteractionColor(UiCardAppearance appearance, string state)
        {
            var value = interactionReference.RootElement.GetProperty(scheme.ToString()).GetProperty(appearance.ToString()).GetProperty(state);
            return value.GetString() == "transparent" ? [30, 70, 90] : Color(value);
        }
        var probe = new Probe();
        UiRasterTextureStatistics shadows = default;
        using var card = Fluent.Card("Pixel card", probe, new(Width: 96, Height: 64));
        void Draw(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            var context = new UiControlContext(ui);
            context.Frame(context.ClipRect, new(30, 70, 90), default, 0, 0);
            ui.SetCursorScreenPos(new(16, 16));
            card.Render(ui);
            shadows = ui.ShadowTextureStatistics;
        }
        int At(int x, int y) => (y * stride + x) * 4;
        foreach (var size in Enum.GetValues<UiCardSize>())
        foreach (var appearance in Enum.GetValues<UiCardAppearance>())
        {
            card.Options = new(size, appearance, Width: 96, Height: 64);
            var pixels = capture($"{size}-{appearance}", Draw);
            var metrics = UiFluentCardPresentation.Measure(card.Options);
            var margin = metrics.ShadowInsets;
            var x = (int)(16 + margin.Left);
            var y = (int)(16 + margin.Top);
            var center = At(x + 48, y + 32);
            var expected = appearance switch
            {
                UiCardAppearance.Filled => Color(colors.GetProperty("filled")),
                UiCardAppearance.FilledAlternative => Color(colors.GetProperty("filledAlternative")),
                _ => new byte[] { 30, 70, 90 }
            };
            check(pixels.AsSpan(center, 3).SequenceEqual(expected),
                "GPU Card body matches the independent pinned palette or transparent backdrop : " + size + " " + appearance);
            var padding = reference.RootElement.GetProperty("padding").GetProperty(size.ToString()).GetInt32();
            var child = At(x + padding + 3, y + padding + 3);
            check(pixels[child] == 255 && pixels[child + 1] == 0 && pixels[child + 2] == 0,
                "GPU Card paints its actual owned child at the official size padding : " + size + " " + appearance);
            if (appearance == UiCardAppearance.Outline)
                check(pixels.AsSpan(At(x + 48, y), 3).SequenceEqual(Color(colors.GetProperty("outline"))),
                    "GPU outline Card has the pinned one-pixel stroke : " + size);
            if (metrics.Shadow is not null)
                check(pixels[At(x - 2, y + 32) + 1] < 70,
                    "GPU filled Card paints its common soft shadow outside the rounded body : " + size + " " + appearance);
            check(pixels.AsSpan().SequenceEqual(capture($"replay-{size}-{appearance}", Draw)),
                "GPU unchanged Card replays exact body shadow and content pixels : " + size + " " + appearance);
        }
        foreach (var appearance in new[] { UiCardAppearance.Filled, UiCardAppearance.FilledAlternative })
        {
            card.Options = new(Appearance: appearance, Width: 96, Height: 64);
            var enabledMetrics = UiFluentCardPresentation.Measure(card.Options);
            card.Options = card.Options with { Disabled = true };
            var pixels = capture("disabled-" + appearance, Draw);
            var disabledMetrics = UiFluentCardPresentation.Measure(card.Options);
            check(enabledMetrics.ShadowInsets == disabledMetrics.ShadowInsets,
                "GPU disabled filled Card preserves its reserved body position : " + appearance);
            var x = (int)(16 + disabledMetrics.ShadowInsets.Left);
            var y = (int)(16 + disabledMetrics.ShadowInsets.Top);
            var expected = scheme == UiFluentColorScheme.Light ? (byte)240 : (byte)20;
            check(pixels.AsSpan(At(x + 48, y + 32), 3).SequenceEqual(new byte[] { expected, expected, expected }),
                "GPU disabled Card paints the pinned disabled background : " + appearance);
            var child = At(x + 15, y + 15);
            check(pixels[child] == 255 && pixels[child + 1] == 0 && pixels[child + 2] == 0,
                "GPU disabled Card preserves its independently painted owned child : " + appearance);
        }
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        try
        {
            card.Options = new(Appearance: UiCardAppearance.Outline, Width: 96, Height: 64, FocusMode: UiFocusGroupMode.NoTab);
            var unfocused = capture("focus-ready", Draw);
            if (!tree.TryGet(tree.Root.First!, out var root)) throw new InvalidOperationException("Card GPU focus root is missing.");
            check(tree.RequestAction(root.Node.Id, root.RuntimeId, UiAccessibilityAction.Focus, wait: false) == UiAccessibilityResult.Success,
                "GPU Card accepts focus through its actual accessibility group");
            context.State.NavCursorVisible = true;
            var focused = capture("focused", Draw);
            check(!unfocused.AsSpan().SequenceEqual(focused) && focused.AsSpan(At(64, 48), 3).SequenceEqual(unfocused.AsSpan(At(64, 48), 3)),
                "GPU Card keyboard focus paints an outline while preserving its interior");
            check(focused.AsSpan().SequenceEqual(capture("focused-replay", Draw)),
                "GPU unchanged focused Card replays exact outline pixels");
            context.State.NavCursorVisible = false;
            card.Activated = static () => { };
            foreach (var appearance in Enum.GetValues<UiCardAppearance>())
            {
                card.Options = new(Appearance: appearance, Width: 96, Height: 64);
                pointer(new(-100, -100), false, false);
                var resting = capture("interactive-rest-" + appearance, Draw);
                if (!tree.TryGet(tree.Root.First!, out root)) throw new InvalidOperationException("Interactive Card GPU root is missing.");
                var body = root.Node.Bounds;
                var x = (int)body.X;
                var y = (int)body.Y;
                var point = new UiVector2(body.X + body.Width - 5, body.Y + body.Height - 5);
                pointer(point, false, false);
                var hover = capture("interactive-hover-" + appearance, Draw);
                var uploads = shadows.Uploads;
                check(hover.AsSpan(At(x + 48, y + 32), 3).SequenceEqual(InteractionColor(appearance, "hover")),
                    "GPU interactive Card hover matches the pinned source background : " + appearance);
                check(tree.TryGet(root.Node.Id, out var current) && current.Node.Bounds == body,
                    "GPU interactive Card hover does not move its body or children : " + appearance);
                if (appearance is UiCardAppearance.Filled or UiCardAppearance.FilledAlternative)
                    check(Enumerable.Range(x - 8, 5).Sum(column => hover[At(column, y + 32) + 1])
                        < Enumerable.Range(x - 8, 5).Sum(column => resting[At(column, y + 32) + 1]),
                        "GPU interactive Card hover expands Shadow4 to Shadow8 within reserved layout : " + appearance);
                check(hover.AsSpan().SequenceEqual(capture("interactive-hover-replay-" + appearance, Draw)) && shadows.Uploads == uploads,
                    "GPU interactive Card unchanged hover reuses its actual shadow texture and pixels : " + appearance);
                pointer(new(-100, -100), true, true);
                capture("interactive-outside-press-" + appearance, Draw);
                pointer(point, true, false);
                var outsideDrag = capture("interactive-outside-drag-" + appearance, Draw);
                check(outsideDrag.AsSpan().SequenceEqual(hover),
                    "GPU dragging into Card from outside preserves hover rather than claiming a pressed state : " + appearance);
                pointer(point, true, true);
                var pressed = capture("interactive-pressed-" + appearance, Draw);
                check(pressed.AsSpan(At(x + 48, y + 32), 3).SequenceEqual(InteractionColor(appearance, "pressed")),
                    "GPU interactive Card pressed background matches its pinned source : " + appearance);
            }
        }
        finally { pointer(new(-100, -100), false, false); context.SetAccessibilityTree(null); }
        using var footerCard = Fluent.Card("Footer pixels", Fluent.CardFooter(
            [new FooterProbe(new(255, 0, 0)), new FooterProbe(new(0, 255, 0))], new FooterProbe(new(0, 0, 255))),
            new(Appearance: UiCardAppearance.Outline, Width: 96, Height: 64));
        void DrawFooter(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            var paint = new UiControlContext(ui);
            paint.Frame(paint.ClipRect, new(30, 70, 90), default, 0, 0);
            ui.SetCursorScreenPos(new(16, 16));
            footerCard.Render(ui);
        }
        var footerPixels = capture("footer-slots", DrawFooter);
        check(footerPixels.AsSpan(At(30, 30), 3).SequenceEqual(new byte[] { 255, 0, 0 })
            && footerPixels.AsSpan(At(54, 30), 3).SequenceEqual(new byte[] { 0, 255, 0 }),
            "GPU Card Footer paints its owned content at the pinned 12px gap");
        check(footerPixels.AsSpan(At(90, 30), 3).SequenceEqual(new byte[] { 0, 0, 255 })
            && footerPixels.AsSpan(At(43, 30), 3).SequenceEqual(new byte[] { 30, 70, 90 }),
            "GPU Card Footer action reaches the padded far edge without painting its gap");
        check(footerPixels.AsSpan().SequenceEqual(capture("footer-slots-replay", DrawFooter)),
            "GPU Card Footer unchanged replay preserves exact pixels");
        footerCard.Options = footerCard.Options with { Width = 128 };
        var widerFooter = capture("footer-slots-wider", DrawFooter);
        check(widerFooter.AsSpan(At(122, 30), 3).SequenceEqual(new byte[] { 0, 0, 255 })
            && widerFooter.AsSpan(At(90, 30), 3).SequenceEqual(new byte[] { 30, 70, 90 })
            && widerFooter.AsSpan(At(30, 30), 3).SequenceEqual(new byte[] { 255, 0, 0 }),
            "GPU Card Footer parent width change moves only the trailing action");
    }
    private sealed class FooterProbe(UiColor color) : UiControlWidget
    {
        protected override bool CanRetainMeasurement => true;
        protected override UiVector2 MeasureContent(UiControlContext context) => context.LayoutConstraints.Constrain(new(12, 12));
        protected override void RenderControl(UiImmediateContext ui)
        {
            var paint = new UiControlContext(ui);
            var size = paint.ConstrainToLayout(new(12, 12));
            var origin = ui.GetCursorScreenPos();
            paint.Frame(new(origin.X, origin.Y, size.X, size.Y), color, default, 0, 0);
            ui.Dummy(size);
        }
    }
    private sealed class Probe : UiControlWidget
    {
        protected override bool CanRetainMeasurement => true;
        protected override UiVector2 MeasureContent(UiControlContext context) => context.LayoutConstraints.Constrain(new(12, 12));
        protected override void RenderControl(UiImmediateContext ui)
        {
            var context = new UiControlContext(ui);
            var size = context.ConstrainToLayout(new(12, 12));
            var point = ui.GetCursorScreenPos();
            context.Frame(new(point.X, point.Y, size.X, size.Y), new(255, 0, 0), default, 0, 0);
            ui.Dummy(size);
        }
    }
}
