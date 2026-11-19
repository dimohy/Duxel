using Duxel.Core;

internal static class MarkdownLinkTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        var input = Idle;
        Run(draw =>
        {
            context.NewFrame(new(1f / 60, new(760, 480), new(1, 1)), input, new(0, 0, 760, 480), UiTextSettings.Default);
            context.Render(new Screen(draw));
            using var data = context.GetDrawData();
        }, next => input = next, context, check);
    }

    private static UiInputState Idle => new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);

    internal static void Run(Action<Action<UiImmediateContext>> frame, Action<UiInputState> setInput,
        UiContext context, Action<bool, string> check)
    {
        const string initial = "[한글 **링크**](https://example.com/path?q=1&x=2) and <https://example.org>\n\n[상대](../guide)\n\n| A | B |\n| - | - |\n| [표 링크](https://example.net/table) | value |";
        var source = new UiState<string>(initial);
        using var view = Dux.MarkdownView("Link preview", source.Binding, new(Width: 240, Height: 220, BaseUri: new("https://example.com/docs/current/")));
        var destinations = new List<Uri>();
        view.Navigate = destinations.Add;
        var mirror = new UiVisualTextLines();
        var styled = new UiMarkdownTextSource(new(initial));
        var points = new Dictionary<string, UiVector2>();
        var disabled = false;
        var shown = true;
        void Draw(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            ui.SetCursorScreenPos(new(10, 20));
            if (shown)
            {
                ui.BeginDisabled(disabled);
                try
                {
                    view.Render(ui);
                }
                finally { ui.EndDisabled(); }
            }
            mirror.Prepare(ui, source.Value, 216, 200, 20, 0, styled: styled);
            points.Clear();
            foreach (var word in new[] { "한글", "링크", "https://example.org", "상대", "표 링크" })
            {
                var start = source.Value.IndexOf(word, StringComparison.Ordinal);
                if (start < 0)
                    continue;
                var line = mirror.FindLine(start);
                points[word] = new(22 + (mirror.X(start, line) + mirror.X(start + 1, line)) / 2,
                    30 + mirror.Y(line) + mirror.LineHeight(line) / 2);
            }
        }
        void Step(UiInputState input)
        {
            setInput(input);
            frame(Draw);
        }
        void Press(UiVector2 point) => Step(Idle with
        {
            MousePosition = point,
            LeftMousePressed = true,
            LeftMouseDown = true
        });
        void Release(UiVector2 point) => Step(Idle with
        {
            MousePosition = point,
            LeftMouseReleased = true
        });
        void Click(string word)
        {
            context.State.AdvanceTime(.6f);
            var point = points[word];
            Press(point);
            Release(point);
        }
        Step(Idle);
        check(view.Document.Links.Count == 4 && view.Document.ParseCount == 1, "Markdown caches normal, autolink, relative and table destinations in one parse");
        var first = points["한글"];
        Step(Idle with
        {
            MousePosition = first
        });
        check(context.State.MouseCursor == UiMouseCursor.Hand, "Markdown viewer link hover uses the hand cursor");
        Press(first);
        Step(Idle with
        {
            MousePosition = first,
            LeftMouseDown = true
        });
        check(destinations.Count == 0, "Markdown link press and hold do not navigate");
        Release(first);
        check(destinations.Count == 1 && destinations[^1].AbsoluteUri == "https://example.com/path?q=1&x=2", "Markdown link release navigates once with its complete destination");
        Step(Idle);
        check(destinations.Count == 1 && view.Document.ParseCount == 1, "Markdown idle neither repeats navigation nor reparses");
        Click("링크");
        check(destinations.Count == 2, "Nested bold link label remains clickable");
        Click("https://example.org");
        check(destinations.Count == 3 && destinations[^1].Host == "example.org", "Wrapped autolink navigates from rendered text");
        Click("상대");
        check(destinations.Count == 4 && destinations[^1].AbsoluteUri == "https://example.com/docs/guide", "Relative links resolve against the explicit Markdown base URI");
        Click("표 링크");
        check(destinations.Count == 5 && destinations[^1].AbsolutePath == "/table", "Table cell links use aligned visual geometry");
        context.State.AdvanceTime(.6f);
        Press(first);
        Step(Idle with
        {
            MousePosition = first with
            {
                X = first.X + 12
            },
            LeftMouseDown = true
        });
        Step(Idle with
        {
            MousePosition = first,
            LeftMouseDown = true
        });
        Release(first);
        check(destinations.Count == 5, "Dragging away and returning to a link cancels activation");
        Press(first);
        Release(new(600, 400));
        check(destinations.Count == 5, "Releasing outside the link does not navigate");
        Press(first);
        source.Value = initial.Replace("example.com/path", "example.com/changed", StringComparison.Ordinal);
        Release(first);
        check(destinations.Count == 5, "Changing the source while pressed cancels stale navigation");
        Step(Idle with
        {
            MousePosition = first,
            LeftMousePressed = true,
            LeftMouseReleased = true
        });
        check(destinations.Count == 6 && destinations[^1].AbsolutePath == "/changed", "Coalesced press and release use the current source destination");
        disabled = true;
        Click("한글");
        check(destinations.Count == 6 && context.State.MouseCursor != UiMouseCursor.Hand, "Disabled Markdown links neither navigate nor advertise activation");
        disabled = false;
        Press(first);
        shown = false;
        Step(Idle with
        {
            MousePosition = first,
            LeftMouseDown = true
        });
        shown = true;
        Release(first);
        check(destinations.Count == 6, "Hiding a pressed Markdown view cancels activation when it returns");
        Press(first);
        Release(new(230, 225));
        check(destinations.Count == 6, "Blank viewer padding does not activate a nearby link");
        Step(Idle with
        {
            MousePosition = new(600, 400)
        });
        check(context.State.MouseCursor != UiMouseCursor.Hand, "Leaving a Markdown link restores the normal cursor");
        view.Dispose();
        var disposed = false;
        try
        {
            view.Navigate = destinations.Add;
        }
        catch (ObjectDisposedException) { disposed = true; }
        check(disposed, "Disposed Markdown releases and rejects navigation callbacks");
        var mail = new UiMarkdownDocument("<user@example.com> ![image](https://example.com/image.png) [ref][id]\n\n[id]: https://example.com/reference");
        check(mail.Links.Count == 2 && mail.Links[0].Destination == "mailto:user@example.com" && mail.Links[1].Destination.EndsWith("/reference", StringComparison.Ordinal),
            "Email and reference destinations are cached while images are excluded");
        var rejected = false;
        try
        {
            UiLinkNavigation.OpenExternal(new("javascript:alert(1)"));
        }
        catch (ArgumentException) { rejected = true; }
        check(rejected, "Default external navigation rejects unsupported schemes before shell execution");
        setInput(Idle);
    }

    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
