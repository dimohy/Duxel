using Duxel.Core;

internal static class MenuRenderingTests
{
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<UiInputState> setInput,
        Action<bool, string> check, int width, UiFluentColorScheme scheme)
    {
        Run<UiThemeMenuPresentation>((name, draw) => capture("theme-" + name, draw), setInput, check, width, scheme);
        Run<UiFluentMenuPresentation>((name, draw) => capture("fluent-" + name, draw), setInput, check, width, scheme);
    }
    private static void Run<TPresentation>(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<UiInputState> setInput,
        Action<bool, string> check, int width, UiFluentColorScheme scheme) where TPresentation : IUiMenuPresentation
    {
        var calls = new List<string>();
        using var menu = new UiMenu<TPresentation>("Actions", [new("one", "One"), new("disabled", "Disabled", false), new("three", "Three")], calls.Add);
        void Render(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding : 0);
            var paint = new UiControlContext(ui);
            paint.Frame(paint.ClipRect, new(124, 124, 124), default, 0, 0);
            menu.Render(ui);
        }
        UiInputState Input(UiVector2? point = null, bool down = false, bool pressed = false, bool released = false) =>
            new(point ?? new(-100, -100), down, false, pressed, released, false, false, 0, 0, [], [], default, default);
        setInput(Input());
        var closed = capture("closed", Render);
        setInput(Input(new(30, 16), down : true, pressed : true));
        capture("trigger-down", Render);
        setInput(Input(new(30, 16), released : true));
        capture("trigger-up", Render);
        setInput(Input());
        var open = capture("open", Render);
        check(menu.IsOpen && !open.AsSpan(width * 40 * 4).SequenceEqual(closed.AsSpan(width * 40 * 4)),
            "actual GPU menu opens a popup below its allocated trigger");
        var background = (55 * width + 120) * 4;
        var ink = 0;
        for (var y = 44; y < 68; y++)
            for (var x = 16; x < 100; x++)
            {
                var pixel = (y * width + x) * 4;
                if (Math.Abs(open[pixel] - open[background]) > 40
                    || Math.Abs(open[pixel + 1] - open[background + 1]) > 40
                    || Math.Abs(open[pixel + 2] - open[background + 2]) > 40) ink++;
            }
        check(ink > 20, "actual GPU menu item paints readable font glyphs rather than legacy atlas placeholder pixels");
        for (var i = 0; i < 3; i++)
            check(capture("open-replay-" + i, Render).AsSpan().SequenceEqual(open),
                "actual menu popup survives acquired-image reuse : " + i);
        if (typeof(TPresentation) == typeof(UiFluentMenuPresentation))
        {
            var beyondSurface = (58 * width + 141) * 4;
            check(open[beyondSurface] + 2 < closed[beyondSurface],
                "Fluent Menu casts a visible shadow beyond the popup surface");
            using var fixture = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "MenuPalette.json")));
            void Background(byte[] pixels, string state, int y)
            {
                var entry = fixture.RootElement.EnumerateArray().Single(value => value.GetProperty("scheme").GetString() == scheme.ToString()
                    && value.GetProperty("state").GetString() == state);
                var expected = entry.GetProperty("background").EnumerateArray().Select(value => value.GetByte()).ToArray();
                var pixel = (y * width + 120) * 4;
                check(Enumerable.Range(0, 3).All(channel => Math.Abs(pixels[pixel + channel] - expected[channel]) <= 1),
                    "actual Fluent menu GPU state matches independent official background : " + scheme + " / " + state);
            }
            setInput(Input(new(120, 56)));
            Background(capture("hover", Render), "Hover", 56);
            setInput(Input(new(120, 56), down : true, pressed : true));
            Background(capture("pressed", Render), "Pressed", 56);
            setInput(Input(released : true));
            capture("cancel-press", Render);
            setInput(Input(new(120, 90)));
            Background(capture("disabled-hover", Render), "Disabled", 90);
            setInput(Input());
            capture("restored-idle", Render);
            menu.Options = menu.Options with { RightToLeft = true };
            var rtl = capture("right-to-left", Render);
            var blank = (55 * width + 70) * 4;
            int Ink(int start, int end)
            {
                var count = 0;
                for (var y = 44; y < 68; y++)
                    for (var x = start; x < end; x++)
                    {
                        var pixel = (y * width + x) * 4;
                        if (Enumerable.Range(0, 3).Any(channel => Math.Abs(rtl[pixel + channel] - rtl[blank + channel]) > 40)) count++;
                    }
                return count;
            }
            check(Ink(98, 126) > 20 && Ink(13, 42) == 0,
                "actual Fluent RTL menu aligns its real label glyphs against the logical content start");
        }
        setInput(Input(new(30, 56), down : true, pressed : true));
        capture("item-down", Render);
        setInput(Input(new(30, 56), released : true));
        capture("item-up", Render);
        setInput(Input());
        var dismissed = capture("dismissed", Render);
        check(!menu.IsOpen && calls.SequenceEqual(["one"]), "actual GPU popup pointer input invokes its item once");
        check(dismissed.AsSpan(width * 40 * 4).SequenceEqual(closed.AsSpan(width * 40 * 4)),
            "actual GPU popup removal clears its full previous surface");
        for (var i = 0; i < 3; i++)
            check(capture("dismissed-replay-" + i, Render).AsSpan(width * 40 * 4).SequenceEqual(closed.AsSpan(width * 40 * 4)),
                "dismissed menu surface remains cleared on acquired-image reuse : " + i);
    }
}
