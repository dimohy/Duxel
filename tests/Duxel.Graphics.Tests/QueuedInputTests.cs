using Duxel.Core;

internal static class QueuedInputTests
{
    private static readonly UiInputState Idle = new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);

    internal static void Run(Action<bool, string> check)
    {
        using (var context = CreateContext())
        {
            void Begin(UiInputState input)
                => context.NewFrame(new(1f / 60, new(400, 300), new(1, 1)), input, new(0, 0, 400, 300), UiTextSettings.Default);

            foreach (var c in "abcd")
                context.AddInputCharacter(c);
            context.AddKeyEvent(UiKey.LeftArrow, true);
            context.AddKeyEvent(UiKey.LeftArrow, false);
            context.AddKeyEvent(UiKey.RightArrow, true);
            context.AddKeyEvent(UiKey.RightArrow, false);
            Begin(Idle);
            var large = context.GetIO();
            check(large.CharEvents.Count == 4 && large.KeyEvents.Count == 4, "large injected batch exposes all four characters and keys");
            context.EndFrame();

            context.AddInputCharacter('x');
            context.AddKeyEvent(UiKey.Home, true);
            Begin(Idle with
            {
                CharEvents = [new('p')],
                KeyEvents = [new(UiKey.End, true, default)]
            });
            var small = context.GetIO();
            check(small.CharEvents.Count == 2, "shrinking merged character batches expose only the current two events");
            check(small.KeyEvents.Count == 2, "shrinking merged key batches expose only the current two events");
            check(small.CharEvents[0].CodePoint == 'p' && small.CharEvents[1].CodePoint == 'x', "platform characters precede injected characters");
            check(small.KeyEvents[0].Key == UiKey.End && small.KeyEvents[1].Key == UiKey.Home, "platform keys precede injected keys");
            context.EndFrame();

            context.AddInputCharacter('z');
            context.AddKeyEvent(UiKey.Home, false);
            Begin(Idle with
            {
                CharEvents = small.CharEvents,
                KeyEvents = small.KeyEvents
            });
            var aliased = context.GetIO();
            check(aliased.CharEvents.Count == 3 && aliased.CharEvents[0].CodePoint == 'p' && aliased.CharEvents[1].CodePoint == 'x' && aliased.CharEvents[2].CodePoint == 'z', "reusing the previous character list preserves its active prefix once");
            check(aliased.KeyEvents.Count == 3 && aliased.KeyEvents[2].Key == UiKey.Home && !aliased.KeyEvents[2].IsDown, "reusing the previous key list appends the new release once");
            context.EndFrame();

            Begin(Idle);
            check(context.GetIO().CharEvents.Count == 0 && context.GetIO().KeyEvents.Count == 0, "empty next frame does not replay either queue");
            context.EndFrame();

            for (var i = 0; i < 256; i++)
            {
                context.AddInputCharacter('a');
                context.AddKeyEvent(UiKey.Home, false);
                context.SetInput(Idle);
            }
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 1024; i++)
            {
                context.AddInputCharacter('a');
                context.AddKeyEvent(UiKey.Home, false);
                context.SetInput(Idle);
            }
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Console.WriteLine("Warmed SetInput allocated bytes : " + allocated);
            check(allocated == 0, "warmed SetInput merge of shrinking character and key queues allocates zero bytes");
        }

        foreach (var kind in new[] { "Input", "Textarea", "Markdown" })
        {
            using var context = CreateContext();
            ControlFontFixture.Configure(context);
            var value = string.Empty;
            var binding = new UiBinding<string>(() => value, text => value = text);
            using UiWidget widget = kind switch
            {
                "Input" => Fluent.Input(string.Empty, binding, 256, 240),
                "Textarea" => Fluent.Textarea(string.Empty, binding, 256, 240, 90),
                "Markdown" => new UiMarkdownEditor("Queued Markdown", binding, new() { Width = 240, Height = 90 }),
                _ => throw new InvalidOperationException("Unknown editor.")
            };
            void Frame(UiInputState? input = null)
            {
                context.NewFrame(new(1f / 60, new(400, 300), new(1, 1)), input ?? Idle, new(0, 0, 400, 300), UiTextSettings.Default);
                context.Render(new Screen(ui =>
                {
                    ui.EnableRootViewportContentLayout(contentPadding: 0);
                    ui.SetCursorScreenPos(default);
                    widget.Render(ui);
                }));
                using var data = context.GetDrawData();
            }
            Frame();
            Frame(Idle with
            {
                MousePosition = new(20, 15),
                LeftMouseDown = true,
                LeftMousePressed = true
            });
            Frame(Idle with
            {
                MousePosition = new(20, 15),
                LeftMouseReleased = true
            });
            foreach (var c in "abcd")
                context.AddInputCharacter(c);
            Frame();
            check(value == "abcd", kind + " consumes a four-character injected batch once");
            context.AddInputCharacter('x');
            Frame();
            check(value == "abcdx", kind + " consumes a shorter next batch without stale characters");
            Frame();
            check(value == "abcdx", kind + " leaves text unchanged on an empty frame");
        }
    }

    private static UiContext CreateContext()
        => new(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
}
