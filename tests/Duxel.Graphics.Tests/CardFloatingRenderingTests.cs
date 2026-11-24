using System.Text.Json;
using Duxel.Core;

internal static class CardFloatingRenderingTests
{
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check,
        int stride, UiFluentColorScheme scheme, UiContext context)
    {
        var floating = new Probe(new(48, 24), new(0, 255, 0));
        using var card = Fluent.Card("Floating geometry pixels", new Probe(new(160, 60), new(255, 0, 0)),
            new(Appearance: UiCardAppearance.Outline, Width: 144, Height: 96), floatingAction: floating);
        UiWidget current = card;
        void Draw(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            var paint = new UiControlContext(ui);
            paint.Frame(paint.ClipRect, new(30, 70, 90), default, 0, 0);
            ui.SetCursorScreenPos(new(16, 16));
            current.Render(ui);
        }
        bool Pixel(byte[] pixels, int x, int y, byte r, byte g, byte b)
            => pixels.AsSpan((y * stride + x) * 4, 3).SequenceEqual(new byte[] { r, g, b });
        var first = capture("floating-overlay", Draw);
        check(Pixel(first, 115, 32, 0, 255, 0) && Pixel(first, 40, 40, 255, 0, 0),
            "GPU floating action paints above actual overlapping primary content");
        check(Pixel(first, 110, 22, 0, 255, 0) && Pixel(first, 155, 24, 0, 255, 0)
            && Pixel(first, 157, 24, 30, 70, 90), "GPU floating action has the pinned4px inset independent of content padding");
        check(first.AsSpan().SequenceEqual(capture("floating-overlay-replay", Draw)), "GPU floating action unchanged replay is pixel exact");
        card.Options = card.Options with { Width = 176 };
        var wider = capture("floating-overlay-wider", Draw);
        check(Pixel(wider, 150, 32, 0, 255, 0) && Pixel(wider, 115, 32, 255, 0, 0) && Pixel(wider, 187, 22, 0, 255, 0),
            "GPU floating action follows the body right edge when Card width changes");
        floating.Resize(new(24, 24));
        var smaller = capture("floating-overlay-smaller", Draw);
        check(Pixel(smaller, 170, 32, 0, 255, 0) && Pixel(smaller, 150, 32, 255, 0, 0),
            "GPU floating child invalidation changes only its actual allocation");
        using var reference = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "CardInteractionPalette.json")));
        var selected = false;
        var writes = 0;
        var binding = new UiBinding<bool>(() => selected, value => { selected = value; writes++; });
        var checkbox = Fluent.Checkbox("##floating-pixel-selection", binding);
        checkbox.AccessibleName = "Floating pixel selection";
        using var selectionCard = Fluent.Card("Floating selected pixels", Fluent.Body1(""), new(Width: 144, Height: 96),
            selection: binding, floatingAction: checkbox);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        try
        {
            current = selectionCard;
            capture("floating-selection-ready", Draw);
            if (!tree.TryGet(tree.Root.First!, out var root) || !tree.TryGet(root.FirstChild!, out var item))
                throw new InvalidOperationException("Floating pixel selection is missing.");
            check(item.Node.Role == UiAccessibilityRole.Checkbox && item.Node.Name == "Floating pixel selection",
                "GPU fixture publishes only the actual floating checkbox as the first Card child");
            tree.RequestAction(item.Node.Id, item.RuntimeId, UiAccessibilityAction.Toggle, wait: false);
            capture("floating-selection-toggle", Draw);
            var committed = capture("floating-selection-committed", Draw);
            var expected = Convert.FromHexString(reference.RootElement.GetProperty(scheme.ToString()).GetProperty("Filled")
                .GetProperty("selected").GetString()!.AsSpan(1));
            var center = ((int)(root.Node.Bounds.Y + root.Node.Bounds.Height / 2) * stride
                + (int)(root.Node.Bounds.X + root.Node.Bounds.Width / 2)) * 4;
            check(selected && writes == 1 && committed.AsSpan(center, 3).SequenceEqual(expected),
                "GPU floating checkbox updates the shared selected body palette with one binding write on its followup frame");
            var deadline = context.State.TimeSeconds + UiDisclosureMotion.DurationSeconds;
            var settled = committed;
            for (var frame = 0; frame < 30 && context.State.TimeSeconds < deadline; frame++)
                settled = capture("floating-selection-settling", Draw);
            check(context.State.TimeSeconds >= deadline
                && settled.AsSpan().SequenceEqual(capture("floating-selection-replay", Draw)) && writes == 1,
                "GPU floating selection replay preserves settled pixels without rewriting the model");
        }
        finally { context.SetAccessibilityTree(null); }
    }
    private sealed class Probe(UiVector2 size, UiColor color) : UiControlWidget
    {
        private UiVector2 _size = size;
        internal void Resize(UiVector2 value) { _size = value; RequestRebuild(); }
        protected override bool CanRetainMeasurement => true;
        protected override UiVector2 MeasureContent(UiControlContext context) => context.LayoutConstraints.Constrain(_size);
        protected override void RenderControl(UiImmediateContext ui)
        {
            var paint = new UiControlContext(ui);
            var actual = paint.ConstrainToLayout(_size);
            var origin = ui.GetCursorScreenPos();
            paint.Frame(new(origin.X, origin.Y, actual.X, actual.Y), color, default, 0, 0);
            ui.Dummy(actual);
        }
    }
}
