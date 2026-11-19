using Duxel.Core;

internal static class MarkdownEntityTests
{
    internal static void Run(Action<bool, string> check)
    {
        foreach (var (text, decoded) in new[] { ("&amp;", "&"), ("&eacute;", "é"), ("&fjlig;", "fj"), ("&#x10400;", "\U00010400") })
        {
            var source = new UiMarkdownTextSource(new(text));
            var visual = new UiVisualTextLines();
            using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
            ControlFontFixture.Configure(context);
            if (decoded.Length == 2 && char.IsSurrogate(decoded[0]))
            {
                var symbol = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "seguisym.ttf");
                var regular = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "segoeui.ttf");
                context.SetDirectTextPrimaryFontPath(symbol);
                context.SetDirectTextSecondaryFontPath(regular);
                context.SetTypographyFonts(new(symbol, symbol, symbol, regular, regular, regular));
            }
            void Frame(float width = 160, int active = -1)
            {
                context.NewFrame(new(1f / 60, new(400, 300), new(1, 1)),
                    new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default),
                    new(0, 0, 400, 300), UiTextSettings.Default);
                context.Render(new Screen(ui => visual.Prepare(ui, text, width, 300, 20, 0, styled: source, activeSourceIndex: active)));
                using var draw = context.GetDrawData();
            }
            Frame();
            check(source.Projection.Text == decoded && source.Projection.Atoms.Length == 1,
                "Markdown entity retains one original source atom : " + text);
            check(visual.NativeParagraph is not null && visual.Count == 1,
                "Decoded entity keeps full native paragraph shaping : " + text);
            var width = visual.Width;
            check(visual.Hit(width * .25f, 10, 20) == 0 && visual.Hit(width * .75f, 10, 20) == text.Length,
                "Entity pointer halves skip hidden source characters : " + text);
            visual.Hit(0, 10, 20);
            check(visual.MoveHorizontal(0, true) == text.Length && visual.MoveHorizontal(text.Length, false) == 0
                && visual.Next(0) == text.Length && visual.Previous(text.Length) == 0,
                "Entity physical and logical arrows traverse one source unit : " + text);
            check(visual.SnapCaret(2, false) == 0 && visual.SnapCaret(2, true) == text.Length,
                "Entity caret normalization excludes hidden syntax : " + text);
            var selection = visual.AlignSelection(new(1, text.Length - 1));
            check(selection.Start == 0 && selection.End == text.Length,
                "Entity partial source selection expands to both atom edges : " + text);
            var snapshot = visual.NativeParagraph;
            Frame();
            check(ReferenceEquals(snapshot, visual.NativeParagraph) && source.Document.ParseCount == 1,
                "Unchanged entity preserves its native snapshot and syntax parse : " + text);
            Frame(1);
            check(visual.Count == 1 && visual[0].End == text.Length,
                "Narrow entity wrapping cannot create a hidden-syntax tail row : " + text);
            Frame(active: 2);
            check(source.Projection.Text == text && source.Projection.Atoms.Length == 0 && source.Document.ParseCount == 1,
                "Active entity exposes editable source syntax without reparsing : " + text);
            visual.Clear();
            source.Release();
        }
    }

    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
