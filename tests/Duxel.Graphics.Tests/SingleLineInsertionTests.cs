using Duxel.Core;

internal static class SingleLineInsertionTests
{
    internal sealed record Case(string Source, int Start, int End, string Insert, string Accepted, int Limit = 256, float Width = 280)
    {
        internal string Expected => Source.Remove(Start, End - Start).Insert(Start, Accepted);
        internal int Caret => Start + Accepted.Length;
        internal void Configure(nint layout)
        {
            if (Accepted == "한글")
                DirectWriteLayoutOracle.SetFontFamily(layout, Start, Accepted.Length + 1, "Malgun Gothic");
        }
    }
    internal static readonly Case[] Cases = SingleLineImeTests.Cases.Select(item =>
        new Case(item.Source, item.Start, item.Start, item.Preedit, item.Preedit, Width: item.Width))
        .Concat([new("abc X123", 4, 5, "אב", "אב"), new("abc 123", 4, 4, "אבג", "אב", 9)]).ToArray();

    internal static void Run(Action<bool, string> check)
    {
        foreach (var item in Cases)
            foreach (var paste in new[] { false, true })
            {
                using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
                ControlFontFixture.Configure(context);
                using var tree = new UiAccessibilityTree();
                context.SetAccessibilityTree(tree);
                context.SetClipboard(new Clipboard(item.Insert));
                var value = item.Source;
                var writes = 0;
                using var input = new UiInput<UiStandardTextInputBehavior, Presentation>("Insertion context",
                    new(() => value, next => { value = next; writes++; }), item.Limit, item.Width);
                var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
                void Frame(UiInputState? state = null)
                {
                    context.NewFrame(new(1f / 60, new(400, 300), new(1, 1)), state ?? idle, new(0, 0, 400, 300), UiTextSettings.Default);
                    context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding: 0); input.Render(ui); }));
                    using var data = context.GetDrawData();
                }
                Frame();
                var id = Presentation.Last.Id;
                context.State.ActiveId = context.State.FocusedId = id;
                context.State.SetCursor(id, item.End);
                context.State.SetSelection(id, new(item.Start, item.End));
                Frame(paste ? idle with
                {
                    KeyEvents = [new(UiKey.V, true, KeyModifiers.Ctrl)]
                }
                    : idle with
                    {
                        CharEvents = item.Insert.EnumerateRunes().Select(rune => new UiCharEvent((uint)rune.Value)).ToArray()
                    });
                void Verify(bool condition, string name) => check(condition, $"Actual single-line insertion : paste={paste}, {item.Insert}/{item.End - item.Start}/{item.Limit} / {name}");
                var expectedX = DirectWriteLayoutOracle.TrailingCaretPosition(item.Expected, item.Caret, item.Configure);
                Verify(value == item.Expected && writes == 1, "accepted text honors selection replacement and length limit with one binding write");
                Verify(Presentation.Last.CaretIndex == item.Caret && Presentation.Last.Selection == new UiTextSelection(item.Caret, item.Caret),
                    "caret follows the accepted insertion and collapses selection");
                Verify(MathF.Abs(Presentation.Last.LineLayout!.X(item.Caret) - expectedX) < .002f, "caret retains the native trailing edge");
                Verify(tree.TryGet(id, out var node) && node.Node.TextLayout is { } accessible
                    && MathF.Abs(accessible.GetRangeExtents(item.Caret, item.Caret).X - (expectedX - Presentation.Last.ScrollX)) < .002f,
                    "accessibility caret shares the accepted insertion edge");
                var geometry = Presentation.Last.LineLayout!.Geometry;
                Frame();
                Verify(ReferenceEquals(geometry, Presentation.Last.LineLayout!.Geometry)
                    && MathF.Abs(Presentation.Last.LineLayout.X(item.Caret) - expectedX) < .002f,
                    "unchanged frame retains native geometry and insertion affinity");
            }
    }
    internal sealed class Clipboard(string text) : IUiClipboard
    {
        public string GetText() => text; public void SetText(string value) => text = value;
    }
    private sealed class Screen(Action<UiImmediateContext> draw) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => draw(ui);
    }
    private readonly struct Presentation : IUiInputPresentation
    {
        internal static UiInputEditResult Last;
        public static UiControlTypographyScope BeginText(UiControlContext context) => context.BeginTypography<UiRegularTypeface, UiFluentType300>();
        public static UiInputLayout Measure(UiControlContext context, string label, float width) => new(new(width, 32), new(0, 0, width, 32), new(0, 0, width, 20), default, 20);
        public static void Paint(UiControlContext context, string label, in UiInputLayout layout, in UiInputEditResult edit) => Last = edit;
    }
}
