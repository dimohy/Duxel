using Duxel.Core;

internal static class SingleLineNavigationInputTests
{
    internal static void Run(Action<bool, string> check)
    {
        SingleLineImeTests.Run(check);
        SingleLineInsertionTests.Run(check);
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        var text = "אבגד";
        using var input = new UiInput<UiStandardTextInputBehavior, Presentation>("Native single line",
            new(() => text, next => text = next), 256, 280);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(UiInputState? state = null, float delta = 1f / 60)
        {
            context.NewFrame(new(delta, new(400, 300), new(1, 1)), state ?? idle, new(0, 0, 400, 300), UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding: 0); input.Render(ui); }));
            using var draw = context.GetDrawData();
        }
        Frame();
        var id = Presentation.Last.Id;
        void Reset(int start, int? end = null, string source = "אבגד")
        {
            text = source;
            context.State.ActiveId = null;
            Frame();
            context.State.ActiveId = context.State.FocusedId = id;
            context.State.SetCursor(id, end ?? start);
            context.State.SetSelection(id, new(start, end ?? start));
        }
        void Key(UiKey key, KeyModifiers modifiers = default) => Frame(idle with
        {
            KeyEvents = [new(key, true, modifiers), new(key, false, modifiers)]
        });
        Reset(2);
        Key(UiKey.RightArrow);
        check(Presentation.Last.CaretIndex == 1, "Single-line Right follows the physical Hebrew direction");
        Key(UiKey.LeftArrow, KeyModifiers.Shift);
        check(Presentation.Last.Selection == new UiTextSelection(1, 2), "Single-line Shift Left retains the source anchor");
        Reset(0, 4);
        Key(UiKey.LeftArrow);
        check(Presentation.Last.CaretIndex == 4 && !Presentation.Last.Selection.HasSelection,
            "Single-line Left collapses selection to the physical left edge");
        Reset(0, 4);
        Key(UiKey.RightArrow);
        check(Presentation.Last.CaretIndex == 0 && !Presentation.Last.Selection.HasSelection,
            "Single-line Right collapses selection to the physical right edge");
        Reset(2);
        Frame(idle with
        {
            KeyEvents = [new(UiKey.RightArrow, true, default)]
        });
        context.State.SetRepeatTime(UiKey.RightArrow, context.State.TimeSeconds - 1);
        Frame();
        check(Presentation.Last.CaretIndex == 0, "Single-line held arrows share native movement");
        Frame(idle with
        {
            KeyEvents = [new(UiKey.RightArrow, false, default)]
        });
        Reset(2);
        Key(UiKey.Delete);
        check(text == "אבד", "Single-line Delete retains logical source order");
        Reset(2);
        Key(UiKey.Backspace);
        check(text == "אגד", "Single-line Backspace retains logical source order");
        foreach (var source in new[] { "abc אב 123", "ABC 123 אב" })
        {
            var trailing = DirectWriteLayoutOracle.TrailingCaretPosition(source, source.Length);
            bool AtEnd() => Presentation.Last.CaretIndex == source.Length
                && MathF.Abs(Presentation.Last.LineLayout!.X(source.Length) - trailing) < .002f;

            Reset(0, source: source);
            Key(UiKey.End);
            check(AtEnd(), "Single-line End uses the trailing edge of the last logical character : " + source);
            Key(UiKey.LeftArrow);
            check(Presentation.Last.LineLayout!.X(Presentation.Last.CaretIndex) < trailing,
                "Single-line Left resumes visual navigation from the logical end : " + source);
            Key(UiKey.End, KeyModifiers.Ctrl);
            check(AtEnd(), "Single-line Ctrl+End reaches the same trailing edge : " + source);
            Reset(0, source: source);
            Key(UiKey.End, KeyModifiers.Shift);
            check(AtEnd() && Presentation.Last.Selection == new UiTextSelection(0, source.Length),
                "Single-line Shift+End retains the source anchor and trailing edge : " + source);

            Reset(0, source: source);
            Frame(idle with { KeyEvents = [new(UiKey.End, true, default)] });
            // Force cursor navigation to exercise the held End path independently of its press.
            context.State.SetCursor(id, 0);
            context.State.SetSelection(id, new(0, 0));
            context.State.SetRepeatTime(UiKey.End, context.State.TimeSeconds - 1);
            Frame();
            check(AtEnd(), "Single-line held End uses the same trailing edge : " + source);
            Frame(idle with { KeyEvents = [new(UiKey.End, false, default)] });
        }
        foreach (var source in new[] { "אבגד", "abc אבגד 123", "AV e\u0301 office" })
        {
            Reset(0, source: source);
            Frame();
            var original = Presentation.Last.LineLayout!.Geometry;
            Frame();
            check(ReferenceEquals(original, Presentation.Last.LineLayout!.Geometry),
                "Unchanged single-line input reuses its native geometry : " + source);
            var probes = DirectWriteLayoutOracle.GeometryProbes(source);
            var matching = true;
            foreach (var probe in probes.Hits)
            {
                Frame(idle with
                {
                    MousePosition = new(probe.X, 10),
                    LeftMouseDown = true,
                    LeftMousePressed = true
                });
                matching &= Presentation.Last.CaretIndex == probe.Position
                    && MathF.Abs(Presentation.Last.LineLayout!.X(probe.Position) - probe.CaretX) < .002f;
                Frame(idle with
                {
                    MousePosition = new(probe.X, 10),
                    LeftMouseReleased = true
                }, delta: 1);
                // Repeated independent click probes must not become a double/triple click gesture.
            }
            check(matching, "Actual single-line click and caret affinity match independent native hit tests : " + source);
            var rangesMatch = true;
            var boundaries = System.Globalization.StringInfo.ParseCombiningCharacters(source).Append(source.Length).ToHashSet();
            foreach (var probe in probes.Ranges)
            {
                if (!boundaries.Contains(probe.Start) || !boundaries.Contains(probe.End))
                    continue;
                var actual = Presentation.Last.LineLayout!.Geometry!.SelectionFragments(probe.Start, probe.End);
                var edges = actual.SelectMany(fragment => new[] { fragment.Left, fragment.Left + fragment.Width })
                    .Concat(probe.Fragments.SelectMany(fragment => new[] { fragment.Left, fragment.Left + fragment.Width }))
                    .Distinct().Order().ToArray();
                for (var index = 1; index < edges.Length; index++)
                {
                    if (edges[index] - edges[index - 1] < .002f)
                        continue;
                    var x = (edges[index] + edges[index - 1]) / 2;
                    rangesMatch &= actual.Any(fragment => x > fragment.Left && x < fragment.Left + fragment.Width)
                        == probe.Fragments.Any(fragment => x > fragment.Left && x < fragment.Left + fragment.Width);
                }
            }
            check(rangesMatch, "Single-line selection fragments match independent native ranges : " + source);
            check(tree.TryGet(id, out var node) && node.Node.TextLayout is { } accessibility
                && MathF.Abs(accessibility.GetCharacterExtents(0).X - probes.Ranges.First(range => range.Start == 0 && range.End == 1).Fragments.Min(fragment => fragment.Left)) < .002f,
                "Single-line accessibility character bounds share native visual geometry : " + source);
        }
    }

    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
    private readonly struct Presentation : IUiInputPresentation
    {
        internal static UiInputEditResult Last;
        public static UiControlTypographyScope BeginText(UiControlContext context) => context.BeginTypography<UiRegularTypeface, UiFluentType300>();
        public static UiInputLayout Measure(UiControlContext context, string label, float width) =>
            new(new(width, 32), new(0, 0, width, 32), new(0, 0, width, 20), default, 20);
        public static void Paint(UiControlContext context, string label, in UiInputLayout layout, in UiInputEditResult edit) => Last = edit;
    }
}
