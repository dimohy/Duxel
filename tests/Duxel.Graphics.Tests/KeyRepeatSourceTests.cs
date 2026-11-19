using Duxel.Core;
using Duxel.Platform.Windows;

internal static class KeyRepeatSourceTests
{
    private static readonly UiFrameInfo Frame = new(.016f, new(400, 300), new(1, 1));
    private static UiInputState Input(UiKeyRepeatSource source, double time, params UiKeyEvent[] keys) =>
        new(new(-100, -100), false, false, false, false, false, false, 0, 0, keys, [], new(.2, .1)
        {
            Source = source
        }, default)
        {
            EventTimeSeconds = time
        };
    internal static void Run(Action<bool, string> check)
    {
        foreach (var source in new[] { UiKeyRepeatSource.Engine, UiKeyRepeatSource.Platform })
            foreach (var kind in new[] { "Input", "Textarea", "Markdown" })
            {
                using var context = Context();
                using var tree = new UiAccessibilityTree();
                context.SetAccessibilityTree(tree);
                var value = "abcdefgh";
                var binding = new UiBinding<string>(() => value, text => value = text);
                using UiWidget editor = kind switch
                {
                    "Input" => Fluent.Input("Repeat source", binding, 256, 240),
                    "Textarea" => Fluent.Textarea("Repeat source", binding, 256, 240, 90),
                    "Markdown" => new UiMarkdownEditor("Repeat source", binding, new() { Width = 240, Height = 90 }),
                    _ => throw new InvalidOperationException("Unknown repeat editor.")
                };
                var screen = new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding: 0); editor.Render(ui); });
                void Render(UiInputState input)
                {
                    context.NewFrame(Frame, input, new(0, 0, 400, 300), UiTextSettings.Default);
                    context.Render(screen);
                    using var data = context.GetDrawData();
                }
                Render(Input(source, 99));
                var id = FindEditor(tree);
                context.State.ActiveId = context.State.FocusedId = id;
                context.State.SetCursor(id, 0);
                context.State.SetSelection(id, new(0, 0));
                Render(Input(source, 100, new UiKeyEvent(UiKey.RightArrow, true, default)));
                var prefix = $"Repeat source : {source}/{kind}";
                check(context.State.GetCursor(id, 0) == 1, prefix + " / initial key-down moves once");
                check(context.State.HasRepeatTime(UiKey.RightArrow) == (source == UiKeyRepeatSource.Engine), prefix + " / only the engine source owns a synthetic timer");
                Render(Input(source, 100.45));
                var expected = source == UiKeyRepeatSource.Engine ? 2 : 1;
                check(context.State.GetCursor(id, 0) == expected, prefix + " / an idle frame cannot add platform-owned repeats");
                Render(Input(source, 100.5, Repeat(UiKey.RightArrow), Repeat(UiKey.RightArrow), Repeat(UiKey.RightArrow)));
                check(context.State.GetCursor(id, 0) == expected + 3 && value == "abcdefgh", prefix + " / all delivered repeat events still reach the real editor");
                Render(Input(source, 100.6, new UiKeyEvent(UiKey.RightArrow, false, default)));
                check(!context.State.IsKeyDown(UiKey.RightArrow) && !context.State.HasRepeatTime(UiKey.RightArrow), prefix + " / release retires key and timer state");
            }
        foreach (var source in new[] { UiKeyRepeatSource.Engine, UiKeyRepeatSource.Platform })
        {
            using var context = Context();
            var presses = new List<bool>();
            var amounts = new List<int>();
            var screen = new Screen(ui => { presses.Add(ui.IsKeyPressed(UiKey.A, true)); amounts.Add(ui.GetKeyPressedAmount(UiKey.B, .2f, .1f)); });
            void Render(UiInputState input)
            {
                context.NewFrame(Frame, input, new(0, 0, 400, 300), UiTextSettings.Default);
                context.Render(screen);
                using var data = context.GetDrawData();
            }
            Render(Input(source, 100, new(UiKey.A, true, default), new(UiKey.B, true, default)));
            var timers = source == UiKeyRepeatSource.Engine
                ? context.State.HasRepeatTime(UiKey.A) && context.State.HasRepeatTime(UiKey.B)
                : !context.State.HasRepeatTime(UiKey.A) && !context.State.HasRepeatTime(UiKey.B);
            check(timers, $"Repeat source : {source}/queries / timers are armed once by their selected source");
            Render(Input(source, 100.45));
            Render(Input(source, 100.5, Repeat(UiKey.A), Repeat(UiKey.B), Repeat(UiKey.B), Repeat(UiKey.B)));
            Render(Input(source, 100.6, new(UiKey.A, false, default), new(UiKey.B, false, default)));
            check(presses.SequenceEqual(new[] { true, source == UiKeyRepeatSource.Engine, true, false })
                && amounts.SequenceEqual(new[] { 1, source == UiKeyRepeatSource.Engine ? 3 : 0, 3, 0 }),
                $"Repeat source : {source}/queries / native events are consumed while idle synthesis follows source ownership");
        }
        using (var context = Context())
        {
            context.State.SetRepeatTime(UiKey.C, 42);
            var screen = new Screen(_ => { });
            context.NewFrame(Frame, Input(UiKeyRepeatSource.Platform, 100), new(0, 0, 400, 300), UiTextSettings.Default);
            context.Render(screen);
            using (context.GetDrawData())
            {
            }
            check(!context.State.HasRepeatTime(UiKey.C), "Repeat source : switching to platform retires prior engine deadlines");
            var rejected = false;
            try
            {
                context.State.SetRepeatTime(UiKey.C, 101);
            }
            catch (InvalidOperationException) { rejected = true; }
            check(rejected, "Repeat source : platform ownership rejects competing synthetic key timers");
            context.NewFrame(Frame, Input(UiKeyRepeatSource.Engine, 101), new(0, 0, 400, 300), UiTextSettings.Default);
            context.Render(screen);
            using (context.GetDrawData())
            {
            }
            context.State.SetRepeatTime(UiKey.C, 102);
            check(context.State.HasRepeatTime(UiKey.C), "Repeat source : an explicit engine selection restores timer ownership");
        }
        foreach (var invalid in new[] { -1, 2, int.MaxValue })
        {
            using var context = Context();
            var rejected = false;
            try
            {
                context.NewInputBatch(Frame, [Input(UiKeyRepeatSource.Engine, 100), Input((UiKeyRepeatSource)invalid, 101)], new(0, 0, 400, 300), UiTextSettings.Default);
            }
            catch (ArgumentException) { rejected = true; }
            check(rejected && context.State.TimeSeconds == 0, $"Repeat source : invalid={invalid} / reject the complete batch before host time advances");
        }
        check(new WindowsKeyRepeatSettingsProvider().GetSettings().Source == UiKeyRepeatSource.Platform,
            "Repeat source : Windows system settings select native message repetition");
    }
    private static UiKeyEvent Repeat(UiKey key) => new(key, true, default) { IsRepeat = true };
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
                if (item.Node.Role == UiAccessibilityRole.Edit && item.Node.Name == "Repeat source")
                    found = item.Node.Id;
                Visit(item.FirstChild);
                id = item.Next;
            }
        }
        Visit(tree.Root.First);
        return found ?? throw new InvalidOperationException("Repeat source editor not found.");
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
