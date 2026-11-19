using Duxel.Core;

internal static class SingleLineImeTests
{
    internal readonly record struct Case(string Source, int Start, string Preedit, float Width = 280)
    {
        internal string Display => Source.Insert(Start, Preedit);
        internal void Configure(nint layout)
        {
            if (Preedit == "한글")
                DirectWriteLayoutOracle.SetFontFamily(layout, Start, Preedit.Length + 1, "Malgun Gothic");
        }
    }
    internal static readonly Case[] Cases = [new("A tail", 1, "V"), new("abc 123", 4, "אב"), new("ب 123", 1, "\u0650ت"), new("a  z", 2, "한글", 24)];

    internal static void Run(Action<bool, string> check)
    {
        foreach (var item in Cases)
        {
            using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
            ControlFontFixture.Configure(context);
            var tree = new UiAccessibilityTree();
            context.SetAccessibilityTree(tree);
            var ime = new Handler { Preedit = item.Preedit };
            context.SetImeHandler(ime);
            var text = item.Source;
            var writes = 0;
            using var input = new UiInput<UiStandardTextInputBehavior, Presentation>("IME context", new(() => text, next => { text = next; writes++; }), 256, item.Width);
            var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
            void Frame(UiInputState? state = null, float delta = 1f / 60)
            {
                context.NewFrame(new(delta, new(400, 300), new(1, 1)), state ?? idle, new(0, 0, 400, 300), UiTextSettings.Default);
                context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding: 0); input.Render(ui); }));
                using var draw = context.GetDrawData();
            }
            Frame();
            var id = Presentation.Last.Id;
            context.State.ActiveId = context.State.FocusedId = id;
            context.State.SetCursor(id, item.Start);
            context.State.SetSelection(id, new(item.Start, item.Start));
            Frame();
            var composed = Presentation.Last.CompositionLayout!;
            void Verify(bool condition, string name) => check(condition, "Actual single-line IME : " + item.Preedit + " / " + name);
            Verify(composed.Text == item.Display && text == item.Source && writes == 0,
                "full display projection preserves committed source without binding writes");
            var expectedCaret = DirectWriteLayoutOracle.TrailingCaretPosition(item.Display, item.Start + item.Preedit.Length, item.Configure);
            Verify(MathF.Abs(composed.CaretX - expectedCaret) < .002f,
                "composition caret uses the native trailing edge in its whole sentence");
            var probes = DirectWriteLayoutOracle.GeometryProbes(item.Display, item.Configure);
            var range = probes.Ranges.Single(probe => probe.Start == item.Start && probe.End == item.Start + item.Preedit.Length);
            var fragments = composed.Layout.Geometry!.SelectionFragments(item.Start, item.Start + item.Preedit.Length);
            Verify(Coverage(fragments, range.Fragments), "underline range follows independent contextual native fragments");
            var maximum = MathF.Max(0, probes.Ranges.Single(probe => probe.Start == 0 && probe.End == item.Display.Length).Fragments.Max(fragment => fragment.Left + fragment.Width) - item.Width);
            var expectedScroll = expectedCaret > item.Width ? MathF.Min(maximum, expectedCaret - item.Width + 4) : 0;
            Verify(MathF.Abs(Presentation.Last.ScrollX - expectedScroll) < .002f,
                "horizontal scrolling uses the complete composed width and caret");
            Verify(ime.Owner == id && MathF.Abs(ime.Caret.X - MathF.Round(expectedCaret - expectedScroll)) < .002f,
                "host IME caret rectangle follows the drawn composition cursor");
            Verify(tree.TryGet(id, out var node) && node.Node.TextLayout is { } accessible
                && MathF.Abs(accessible.GetRangeExtents(item.Start, item.Start).X - (expectedCaret - expectedScroll)) < .002f,
                "committed accessibility caret maps to the composed trailing edge");
            var snapshot = composed.Layout.Geometry;
            var display = composed.Text;
            Frame();
            Verify(ReferenceEquals(composed, Presentation.Last.CompositionLayout) && ReferenceEquals(snapshot, composed.Layout.Geometry)
                && ReferenceEquals(display, composed.Text), "unchanged composition retains its display string and native snapshot");
            ime.Pending = item.Preedit;
            ime.Preedit = null;
            Frame(idle with
            {
                CharEvents = [new('Z')]
            });
            Verify(text == item.Display && writes == 1 && Presentation.Last.CompositionLayout is null,
                "commit writes once, suppresses duplicate characters and retires composition geometry");
            var committedCaret = item.Start + item.Preedit.Length;
            Verify(Presentation.Last.CaretIndex == committedCaret
                && MathF.Abs(Presentation.Last.LineLayout!.X(committedCaret) - expectedCaret) < .002f,
                "commit retains the composed native trailing caret edge");
            Verify(MathF.Abs(ime.Caret.X - MathF.Round(expectedCaret - expectedScroll)) < .002f,
                "host candidate rectangle does not jump after commit");
            Verify(tree.TryGet(id, out var committedNode) && committedNode.Node.TextLayout is { } committedAccessible
                && MathF.Abs(committedAccessible.GetRangeExtents(committedCaret, committedCaret).X - (expectedCaret - expectedScroll)) < .002f,
                "accessibility caret stays on the committed native trailing edge");
            Frame();
            Verify(MathF.Abs(Presentation.Last.LineLayout!.X(committedCaret) - expectedCaret) < .002f,
                "committed trailing caret affinity survives an unchanged frame");
            int SourcePosition(int displayPosition) => displayPosition <= item.Start ? displayPosition
                : displayPosition <= committedCaret ? item.Start : displayPosition - item.Preedit.Length;
            void ResetPointer()
            {
                text = item.Source;
                ime.Preedit = item.Preedit;
                context.State.ActiveId = null;
                Frame(delta: 1);
                context.State.ActiveId = context.State.FocusedId = id;
                context.State.SetCursor(id, item.Start);
                context.State.SetSelection(id, new(item.Start, item.Start));
                context.State.SetScrollX(id, 0);
                Frame();
            }
            var pointerProbes = probes.Hits.Where(probe => probe.X - expectedScroll > 1 && probe.X - expectedScroll < item.Width - 1)
                .GroupBy(probe => SourcePosition(probe.Position)).Select(group => group.First()).Take(3).ToArray();
            Verify(pointerProbes.Length > 0, "independent native hit probes cover the visible composed field");
            foreach (var shift in new[] { false, true })
            {
                var matching = true;
                foreach (var probe in pointerProbes)
                {
                    ResetPointer();
                    var target = SourcePosition(probe.Position);
                    Frame(idle with
                    {
                        MousePosition = new(probe.X - expectedScroll, 10),
                        LeftMouseDown = true,
                        LeftMousePressed = true,
                        Modifiers = shift ? KeyModifiers.Shift : default
                    });
                    matching &= Presentation.Last.CaretIndex == target
                        && Presentation.Last.Selection == new UiTextSelection(shift ? item.Start : target, target)
                        && text == item.Source && writes == 1;
                    Frame(idle with
                    {
                        LeftMouseReleased = true
                    }, delta: 1);
                }
                Verify(matching, shift ? "Shift-click maps native composed hits to source selection anchors"
                    : "click maps native composed hits without committing preedit");
            }
            ResetPointer();
            var anchorProbe = probes.Hits.First(probe => SourcePosition(probe.Position) == item.Start
                && probe.X - expectedScroll > 1 && probe.X - expectedScroll < item.Width - 1);
            var dragProbe = pointerProbes.First(probe => SourcePosition(probe.Position) != item.Start);
            Frame(idle with
            {
                MousePosition = new(anchorProbe.X - expectedScroll, 10),
                LeftMouseDown = true,
                LeftMousePressed = true
            });
            Verify(Presentation.Last.CaretIndex == item.Start && !Presentation.Last.Selection.HasSelection,
                "drag anchors inside preedit map to the original source insertion point");
            Frame(idle with
            {
                MousePosition = new(dragProbe.X - expectedScroll, 10),
                LeftMouseDown = true
            });
            Verify(Presentation.Last.Selection == new UiTextSelection(item.Start, SourcePosition(dragProbe.Position))
                && text == item.Source && writes == 1,
                "held drag follows native composed geometry without modifying committed source");
            Frame(idle with
            {
                LeftMouseReleased = true
            });
            input.Dispose();
            Presentation.Last = default;
            Verify(context.State.TextInputStorage == (0, 0), "owner disposal releases input and composition storage");
        }
    }

    private static bool Coverage(PlatformTextSelectionFragment[] actual, (float Left, float Width)[] expected)
    {
        var edges = actual.SelectMany(fragment => new[] { fragment.Left, fragment.Left + fragment.Width })
            .Concat(expected.SelectMany(fragment => new[] { fragment.Left, fragment.Left + fragment.Width })).Distinct().Order().ToArray();
        for (var index = 1; index < edges.Length; index++)
        {
            if (edges[index] - edges[index - 1] < .002f)
                continue;
            var x = (edges[index] + edges[index - 1]) / 2;
            if (actual.Any(fragment => x > fragment.Left && x < fragment.Left + fragment.Width)
                != expected.Any(fragment => x > fragment.Left && x < fragment.Left + fragment.Width))
                return false;
        }
        return true;
    }

    internal sealed class Handler : IUiImeHandler
    {
        internal string? Owner, Preedit, Pending;
        internal UiRect Caret;
        public void SetCaretRect(UiRect caretRect, UiRect inputRect, float height, float width) => Caret = caretRect;
        public string? GetCompositionText() => Preedit;
        public void SetCompositionOwner(string? id) => Owner = id;
        public string? ConsumeCommittedText(string id)
        {
            if (Owner != id)
                return null;
            var result = Pending;
            Pending = null;
            return result;
        }
        public void ReleaseInput(string inputId) => _ = ConsumeCommittedText(inputId);
        public string? ConsumeRecentCommittedText() => null;
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
        public static void Paint(UiControlContext context, string label, in UiInputLayout layout, in UiInputEditResult edit)
        {
            Last = edit;
            context.PaintEditableText(layout, edit, new(new(255, 255, 255), new(40, 100, 180), new(255, 170, 0))
            {
                Typography = true
            });
        }
    }
}
