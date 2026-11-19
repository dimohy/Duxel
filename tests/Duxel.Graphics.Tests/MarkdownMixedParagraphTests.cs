using Duxel.Core;

internal static class MarkdownMixedParagraphTests
{
    internal static void Run(Action<bool, string> check)
    {
        const string text = "**אב** abc 123\n\n| Header |\n| --- |\n| value |\n\n**גד** tail";
        var source = new UiMarkdownTextSource(new(text));
        var visual = new UiVisualTextLines();
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        void Frame(int active = -1)
        {
            context.NewFrame(new(1f / 60, new(400, 300), new(1, 1)),
                new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default),
                new(0, 0, 400, 300), UiTextSettings.Default);
            context.Render(new Screen(ui => visual.Prepare(ui, text, 240, 300, 20, 0, styled: source, activeSourceIndex: active)));
            using var data = context.GetDrawData();
        }
        Frame();
        check(visual.Count == 6 && visual.StyledTableRows.Count == 2 && visual.StyledParagraphRows.Count == 4,
            "Mixed native paragraphs and table rows preserve all original blank lines without a duplicate table boundary row");
        check(visual.StyledParagraphRows.Values.All(row => row.Layout.NativeParagraph is not null),
            "Every non-table row in a mixed document shares a complete native paragraph snapshot");
        void Configure(nint layout) => DirectWriteLayoutOracle.SetFontWeight(layout, 0, 2, 700);
        var expected = DirectWriteLayoutOracle.CaretPositions("Segoe UI", "אב abc 123", 14, Configure);
        var offsets = new[] { 2, 3, 6, 7, 8, 9, 10, 11, 12, 13, 14 };
        check(offsets.Select((offset, index) => MathF.Abs(visual.X(offset, 0) - expected[index]) < .002f).All(value => value),
            "Mixed-document prefix carets match independent whole native shaping across font styles");
        var probes = DirectWriteLayoutOracle.GeometryProbes("אב abc 123", Configure);
        static int Raw(int position, bool trailing) => position switch
        {
            0 => 2,
            1 => 3,
            2 => trailing ? 4 : 6,
            _ => position + 4
        };
        var hits = true;
        foreach (var probe in probes.Hits)
        {
            var index = visual.Hit(probe.X, 1, 20);
            hits &= index == Raw(probe.Position, probe.Trailing) && MathF.Abs(visual.X(index, 0) - probe.CaretX) < .002f;
        }
        check(hits, "Mixed-document pointer geometry retains original markers and native bidi affinity");
        var fragments = visual.SelectionFragments(2, 4, 0);
        var reference = probes.Ranges.Single(range => range.Start == 0 && range.End == 2).Fragments;
        check(fragments.Length == reference.Length && fragments.Select((fragment, index) =>
            MathF.Abs(fragment.Left - reference[index].Left) < .002f && MathF.Abs(fragment.Width - reference[index].Width) < .002f).All(value => value),
            "Mixed-document selections use independent native visual fragments");
        var suffix = text.LastIndexOf("**גד**", StringComparison.Ordinal);
        var suffixLine = visual.FindLine(suffix);
        var suffixExpected = DirectWriteLayoutOracle.CaretPositions("Segoe UI", "גד tail", 14, Configure);
        check(MathF.Abs(visual.X(suffix + 2, suffixLine) - suffixExpected[0]) < .002f
            && visual.Y(suffixLine) == 124,
            "Native suffix coordinates translate past the table grid and blank line without losing original source offsets");
        var first = visual.StyledParagraphRows[0].Layout;
        var snapshot = first.NativeParagraph;
        Frame();
        check(ReferenceEquals(first, visual.StyledParagraphRows[0].Layout) && ReferenceEquals(snapshot, first.NativeParagraph),
            "Unchanged mixed documents retain native block slices and snapshots");
        Frame(2);
        check(source.Document.ParseCount == 1 && visual.StyledParagraphRows[0].Layout.StyledSource!.Projection.Text.StartsWith("**אב**", StringComparison.Ordinal),
            "A captured active block reveals original syntax inside its native slice without reparsing");
        check(first.NativeParagraph is null && first.StyledSource is null,
            "Rebuilding composed blocks releases their previous native snapshots and source references");
        Frame(text.IndexOf("value", StringComparison.Ordinal));
        check(visual.NativeParagraph is not null && visual.StyledParagraphRows.Count == 0 && visual.StyledTableRows.Count == 0,
            "Revealing every table returns the mixed source to one native whole-document layout");
        visual.Clear();
        source.Release();
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
