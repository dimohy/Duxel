using Duxel.Core;

internal static class InputEventTimingTests
{
    private static readonly UiInputState Idle = new(new(20, 12), false, false, false, false, false, false, 0, 0, [], [], new(.2, .1), default);
    private static readonly UiFrameInfo Frame = new(.016f, new(400, 300), new(1, 1));
    internal static void Run(Action<bool, string> check)
    {
        foreach (var interval in new[] { .1, 1.0, -.1 })
        {
            using var context = Context();
            var global = new List<int>();
            var widget = new List<int>();
            var screen = new Screen(ui =>
            {
                if (ui.IsMouseClicked(0))
                {
                    global.Add(context.State.LastClickCount);
                    widget.Add(context.State.RegisterClick("timed-widget", new(20, 12)));
                }
            });
            context.NewInputBatch(Frame, [Press(10), Idle with { EventTimeSeconds = 10.01 }, Press(10 + interval)], new(0, 0, 400, 300), UiTextSettings.Default);
            context.Render(screen);
            using var data = context.GetDrawData();
            var expected = interval is >= 0 and <= .3 ? 2 : 1;
            check(global.SequenceEqual(new[] { 1, expected }), $"Input time : gap={interval} / global click count uses event time");
            check(widget.SequenceEqual(new[] { 1, expected }), $"Input time : gap={interval} / widget click chain uses event time including backward delivery");
            check(Math.Abs(context.State.TimeSeconds - Frame.DeltaTime) < .000001, $"Input time : gap={interval} / animation time advances only once");
        }
        foreach (var kind in new[] { "Input", "Textarea", "Markdown" })
        {
            using var context = Context();
            using var tree = new UiAccessibilityTree();
            context.SetAccessibilityTree(tree);
            var value = "abcd";
            var binding = new UiBinding<string>(() => value, text => value = text);
            using UiWidget editor = kind switch
            {
                "Input" => Fluent.Input("Timed editor", binding, 256, 240),
                "Textarea" => Fluent.Textarea("Timed editor", binding, 256, 240, 90),
                "Markdown" => new UiMarkdownEditor("Timed editor", binding, new() { Width = 240, Height = 90 }),
                _ => throw new InvalidOperationException("Unknown timed editor.")
            };
            var screen = new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding: 0); editor.Render(ui); });
            context.NewFrame(Frame, Idle with
            {
                MousePosition = new(-100, -100)
            }, new(0, 0, 400, 300), UiTextSettings.Default);
            context.Render(screen);
            using (context.GetDrawData())
            {
            }
            var id = FindEditor(tree);
            context.State.ActiveId = context.State.FocusedId = id;
            context.State.SetCursor(id, 0);
            context.State.SetSelection(id, new(0, 0));
            context.NewInputBatch(Frame, [Idle with { EventTimeSeconds = 20, KeyEvents = [new(UiKey.RightArrow, true, default)] }, Idle with { EventTimeSeconds = 20.5 }], new(0, 0, 400, 300), UiTextSettings.Default);
            context.Render(screen);
            using (context.GetDrawData())
            {
            }
            check(context.State.GetCursor(id, 0) == 2, $"Input time : {kind} held-key repeat matures between logical steps");
        }
        using (var context = Context())
        {
            var counts = new List<int>();
            var screen = new Screen(ui => counts.Add(ui.GetKeyPressedAmount(UiKey.A, .2f, .1f)));
            context.NewInputBatch(Frame, [Idle with { EventTimeSeconds = 20, KeyEvents = [new(UiKey.A, true, default)] },
                Idle with { EventTimeSeconds = 20.45 }, Idle with { EventTimeSeconds = 20.46, KeyEvents = [new(UiKey.A, false, default)] }],
                new(0, 0, 400, 300), UiTextSettings.Default);
            context.Render(screen);
            using var data = context.GetDrawData();
            check(counts.SequenceEqual(new[] { 1, 3, 0 }) && !context.State.HasRepeatTime(UiKey.A),
                "Input time : repeat query arms on the first press, counts elapsed intervals, and retires on key-up");
        }
        using (var context = Context())
        {
            var screen = new Screen(_ => { });
            context.NewFrame(Frame, Idle with
            {
                EventTimeSeconds = 5
            }, new(0, 0, 400, 300), UiTextSettings.Default);
            context.Render(screen);
            using (context.GetDrawData())
            {
            }
            context.NewFrame(Frame);
            context.Render(screen);
            using var data = context.GetDrawData();
            check(Math.Abs(context.State.InputTimeSeconds - (5 + Frame.DeltaTime)) < .000001,
                "Input time : an idle frame projects the input clock without replaying a consumed event timestamp");
        }
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, -1d })
        {
            using var context = Context();
            var rejected = false;
            try
            {
                context.NewInputBatch(Frame, [Idle, Idle with { EventTimeSeconds = invalid }], new(0, 0, 400, 300), UiTextSettings.Default);
            }
            catch (ArgumentException) { rejected = true; }
            check(rejected && context.State.TimeSeconds == 0, $"Input time : invalid={invalid} / reject the complete batch before advancing frame time");
        }
    }
    private static UiInputState Press(double time) => Idle with { LeftMouseDown = true, LeftMousePressed = true, EventTimeSeconds = time };
    private static UiContext Context()
    {
        var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        return context;
    }
    private static string FindEditor(UiAccessibilityTree tree)
    {
        string? found = null;
        void Visit(string? id)
        {
            while (id is not null && tree.TryGet(id, out var item))
            {
                if (item.Node.Role == UiAccessibilityRole.Edit && item.Node.Name == "Timed editor")
                    found = item.Node.Id;
                Visit(item.FirstChild);
                id = item.Next;
            }
        }
        Visit(tree.Root.First);
        return found ?? throw new InvalidOperationException("Timed editor not found.");
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
