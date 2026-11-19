using Duxel.Core;
using Duxel.Platform.Linux;

internal static class TextBoundedRangeTests
{
    internal static void Run(string primary, string secondary, Action<bool, string> check)
    {
        using var tree = new UiAccessibilityTree();
        using var backend = new LinuxPlatformTextBackend();
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        context.SetPlatformTextBackend(backend);
        context.SetDirectTextEnabled(true);
        context.SetDirectTextPrimaryFontPath(primary);
        context.SetDirectTextSecondaryFontPath(secondary);
        context.SetTypographyFonts(new(primary, primary, primary, secondary, secondary, secondary) { Monospace = primary });
        context.SetAccessibilityTree(tree);
        var source = "# 정😀\n\n본문 **bold** and `code`";
        using var viewer = new UiMarkdownView("Document", new(() => source, value => source = value), new(Width: 240, Height: 180));
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        var screen = new Screen(ui => viewer.Render(ui));
        var viewportHeight = 300f;
        UiAccessibilityTextLayout Frame()
        {
            context.NewFrame(new(.1f, new(400, viewportHeight), new(1, 1)), idle, new(0, 0, 400, viewportHeight), UiTextSettings.Default);
            context.Render(screen);
            context.GetDrawData().ReleasePooled();
            return tree.CaptureSnapshot().Elements.Single(e => e.Node.Name == "Document").Node.TextLayout!;
        }
        var layout = Frame();
        var ranges = layout.GetBoundedRanges(layout.ClipBounds);
        check(ranges[0] == new UiAccessibilityTextRange(2, 4), "Bounded styled heading preserves Korean and emoji scalar offsets without hidden heading syntax");
        check(ranges.All(range => range.End > range.Start && Enumerable.Range(range.Start, range.End - range.Start)
            .All(offset => layout.GetCharacterExtents(offset).Width > 0)), "Bounded source runs exclude unpainted syntax and zero-width line separators");
        check(ranges.Length >= 4 && ranges.All(range => !layout.Content.GetText(range.Start, range.End).Contains('*')),
            "Hidden emphasis delimiters split source runs instead of leaking into a merged bounded range");
        var box = layout.GetCharacterExtents(2);
        var middle = new UiRect(box.X + box.Width / 4, box.Y, box.Width / 2, box.Height);
        foreach (var clip in Enum.GetValues<UiAccessibilityTextClip>())
            check(layout.GetBoundedRanges(middle, clip).Length == (clip == UiAccessibilityTextClip.None ? 1 : 0),
                "Horizontal glyph clipping obeys the requested source-cell edge policy, " + clip);
        middle = new(box.X, box.Y + box.Height / 4, box.Width, box.Height / 2);
        foreach (var clip in Enum.GetValues<UiAccessibilityTextClip>())
            check(layout.GetBoundedRanges(middle, yClip: clip).Length == (clip == UiAccessibilityTextClip.None ? 1 : 0),
                "Vertical glyph clipping obeys the requested source-cell edge policy, " + clip);
        foreach (var clip in Enum.GetValues<UiAccessibilityTextClip>())
        {
            var minimum = layout.GetBoundedRanges(new(box.X + box.Width / 2, box.Y, box.Width / 2, box.Height), clip);
            var maximum = layout.GetBoundedRanges(new(box.X, box.Y, box.Width / 2, box.Height), clip);
            check(minimum.Length == (((int)clip & 1) == 0 ? 1 : 0)
                && maximum.Length == (((int)clip & 2) == 0 ? 1 : 0),
                "Independent minimum and maximum edges distinguish asymmetric clipping policies, " + clip);
        }
        check(layout.GetBoundedRanges(box, UiAccessibilityTextClip.Both, UiAccessibilityTextClip.Both)
            .SequenceEqual([new UiAccessibilityTextRange(2, 3)]), "Exact cell edges are inclusive for full containment and exclude merely touching neighbors");
        check(layout.GetBoundedRanges(new(0, 0, 0, 50)).Length == 0
            && layout.GetBoundedRanges(new(-100, -100, 10, 10)).Length == 0, "Empty or disjoint rectangles return an empty bounded range array");
        foreach (var invalid in new Action[]
        {
            () => layout.GetBoundedRanges(new(0, 0, -1, 1)),
            () => layout.GetBoundedRanges(new(float.NaN, 0, 1, 1)),
            () => layout.GetBoundedRanges(box, (UiAccessibilityTextClip)4),
            () => layout.GetBoundedRanges(box, yClip: (UiAccessibilityTextClip)(-1))
        })
        {
            var refused = false;
            try { invalid(); }
            catch (ArgumentOutOfRangeException) { refused = true; }
            check(refused, "Bounded queries reject invalid rectangles and clipping policies explicitly");
        }
        check(ReferenceEquals(layout, Frame()) && source == layout.Content.Text,
            "Bounded queries preserve the immutable layout, exact source and event-idle frame reuse");
        source = "e\u0301A";
        layout = Frame();
        check(layout.GetBoundedRanges(layout.GetCharacterExtents(0)).SequenceEqual([new UiAccessibilityTextRange(0, 2)]),
            "A combining grapheme returns all committed scalars sharing its painted source cell");
        source = "| A | B |\n| --- | --- |\n| 정 | 😀 |";
        layout = Frame();
        var table = layout.GetBoundedRanges(layout.ClipBounds);
        check(table.Length >= 4 && table.All(range => !layout.Content.GetText(range.Start, range.End).Contains('|')),
            "Bounded table queries retain separate painted cells and omit source-only table separators");
        source = string.Join('\n', Enumerable.Repeat("정😀 sample", 30));
        layout = Frame();
        var visible = layout.GetBoundedRanges(new(-1000, -1000, 10000, 10000));
        check(visible.Length > 0 && visible.Max(range => range.End) < layout.Content.CharacterCount,
            "A document-wide bounded query excludes source cells outside the current painting viewport");
        var firstCell = layout.GetCharacterExtents(0);
        viewportHeight = firstCell.Y + firstCell.Height / 2;
        layout = Frame();
        var clipBottom = layout.ClipBounds.Y + layout.ClipBounds.Height;
        check(Enumerable.Range(0, layout.Content.CharacterCount).Select(layout.GetCharacterExtents)
            .Any(cell => cell.Width > 0 && cell.Y < clipBottom && cell.Y + cell.Height > clipBottom),
            "The real bounded query fixture contains a source cell partially clipped by the viewport bottom");
        check(layout.GetBoundedRanges(new(layout.ClipBounds.X, clipBottom, layout.ClipBounds.Width, 1000)).Length == 0,
            "A query wholly outside the viewport cannot retrieve the invisible portion of a partially visible source cell");
    }

    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
