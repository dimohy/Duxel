using Duxel.Core;

internal static class InlineAlignmentTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(Action<UiImmediateContext> draw, UiInputState? input = null)
        {
            context.NewFrame(new(.1f, new(640, 480), new(1, 1)), input ?? idle, new(0, 0, 640, 480), UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding: 0); draw(ui); }));
            using var data = context.GetDrawData();
        }
        UiAccessibilityElement Find(string name) => tree.CaptureSnapshot().Elements.Single(x => x.Node.Name == name);
        static bool Near(float x, float y) => MathF.Abs(x - y) < .001f;
        using var span = new UiInlineTextSpan("wide line\nx");
        using var flow = new UiInlineFlowWidget([span]);
        using var sized = Dux.SizedBox(flow, 200, 80);
        Frame(sized.Render);
        var left = Find(span.Text);
        var builds = flow.VisualLines.Builds;
        check(left.Node.Bounds.X == 0 && flow.VisualLines.Count == 2,
            "Default inline alignment preserves left origin and both prepared hard lines");
        flow.HorizontalAlignment = UiItemHorizontalAlign.Center;
        Frame(sized.Render);
        var center = Find(span.Text);
        check(Near(center.Node.Bounds.X, (200 - center.Node.Bounds.Width) / 2)
            && center.RuntimeId == left.RuntimeId && center.Node.Bounds.Height == left.Node.Bounds.Height,
            "Centered multiline inline geometry preserves source identity and balanced horizontal margins");
        flow.HorizontalAlignment = UiItemHorizontalAlign.Right;
        Frame(sized.Render);
        var right = Find(span.Text);
        check(Near(right.Node.Bounds.X + right.Node.Bounds.Width, 200) && right.RuntimeId == left.RuntimeId,
            "Right-aligned multiline inline geometry reaches the assigned right edge without reflecting glyphs");
        Frame(sized.Render);
        check(flow.VisualLines.Builds == builds, "Alignment changes and stable replay reuse unchanged prepared text lines");
        Frame(ui => check(float.IsFinite(flow.MeasureConstrained(ui, new(0, float.PositiveInfinity, 0, 200)).X),
            "Unbounded right-aligned measurement retains finite natural width"));
        var rejected = false;
        try
        {
            flow.HorizontalAlignment = (UiItemHorizontalAlign)(-1);
        }
        catch (ArgumentOutOfRangeException) { rejected = true; }
        check(rejected && flow.HorizontalAlignment == UiItemHorizontalAlign.Right,
            "Invalid physical alignment rejects atomically without replacing the active option");

        var calls = 0;
        using var prefix = new UiInlineTextSpan("prefix ");
        using var button = Fluent.Button("Inline action", () => calls++);
        using var objects = new UiInlineFlowWidget([prefix, button]);
        using var objectBox = Dux.SizedBox(objects, 300, 80);
        Frame(objectBox.Render);
        var oldButton = Find("Inline action");
        var oldPrefix = Find("prefix ");
        objects.HorizontalAlignment = UiItemHorizontalAlign.Right;
        Frame(objectBox.Render);
        var movedButton = Find("Inline action");
        var movedPrefix = Find("prefix ");
        check(Near(movedButton.Node.Bounds.X + movedButton.Node.Bounds.Width, 300)
            && Near(movedButton.Node.Bounds.X - oldButton.Node.Bounds.X, movedPrefix.Node.Bounds.X - oldPrefix.Node.Bounds.X)
            && movedButton.RuntimeId == oldButton.RuntimeId,
            "Actual atomic widget and literal span receive the same line translation and retain their identities");
        var mouse = new UiVector2(movedButton.Node.Bounds.X + movedButton.Node.Bounds.Width / 2,
            movedButton.Node.Bounds.Y + movedButton.Node.Bounds.Height / 2);
        Frame(objectBox.Render, idle with
        {
            MousePosition = mouse,
            LeftMouseDown = true,
            LeftMousePressed = true
        });
        Frame(objectBox.Render, idle with
        {
            MousePosition = mouse,
            LeftMouseReleased = true
        });
        check(calls == 1, "Actual pointer hit testing follows the aligned atomic widget instead of its previous position");
        tree.RequestAction(movedButton.Node.Id, movedButton.RuntimeId, UiAccessibilityAction.Invoke, wait: false);
        Frame(objectBox.Render);
        check(calls == 2, "The aligned atomic widget retains its actual accessibility Invoke behavior");

        using var body = Fluent.MessageBarBody("body", Fluent.MessageBarTitle("Heading"));
        using var message = Fluent.MessageBar("Aligned message", body, options: new(Width: 500));
        var announced = 0;
        tree.Announcement += _ => announced++;
        Frame(message.Render);
        var initialTitle = Find("Heading");
        message.Options = message.Options with
        {
            RightToLeft = true
        };
        Frame(message.Render);
        var root = Find("Aligned message");
        var caption = Find("body");
        check(body.HorizontalAlignment == UiItemHorizontalAlign.Right
            && Near(caption.Node.Bounds.X + caption.Node.Bounds.Width, root.Node.Bounds.X + root.Node.Bounds.Width - 41)
            && Find("Heading").RuntimeId == initialTitle.RuntimeId && announced == 1,
            "RTL MessageBar aligns its actual owned body at the mirrored content edge without changing title identity or reannouncing");
        message.Options = message.Options with
        {
            RightToLeft = false
        };
        Frame(message.Render);
        check(body.HorizontalAlignment == UiItemHorizontalAlign.Left && Near(Find("Heading").Node.Bounds.X, 41),
            "Returning MessageBar direction restores physical left alignment of the same owned body");
    }
    private sealed class Screen(Action<UiImmediateContext> draw) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => draw(ui);
    }
}
