using Duxel.Core;

internal static class MarkdownTableParagraphTests
{
    internal static void Run(Action<bool, string> check)
    {
        const string text = "| Header | Other |\n| :--- | ---: |\n| **אב** abc 123 | AV e\u0301 |";
        const string displayed = "אב abc 123";
        var source = new UiMarkdownTextSource(new(text));
        var visual = new UiVisualTextLines();
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        void Frame(int active = -1)
        {
            context.NewFrame(new(1f / 60, new(500, 300), new(1, 1)),
                new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default),
                new(0, 0, 500, 300), UiTextSettings.Default);
            context.Render(new Screen(ui => visual.Prepare(ui, text, 400, 200, 20, 0, styled: source, activeSourceIndex: active)));
            using var data = context.GetDrawData();
        }
        Frame();
        check(visual.StyledTableRows.Count == 2 && visual.StyledTableRows.Values.All(row =>
            row.CellLayouts.All(layout => layout.NativeParagraph is not null && layout.StyledSource is UiStyledTextSliceSource)),
            "Every rendered Markdown table cell consumes a native paragraph from an immutable source slice");
        var entry = visual.StyledTableRows.OrderBy(pair => pair.Key).Last();
        var line = entry.Key;
        var row = entry.Value;
        var cell = row.Source.Cells[0];
        var child = row.CellLayouts[0];
        check(child.StyledSource!.Projection.Text == displayed,
            "Cell slices preserve mixed style and hidden markers without reparsing a second Markdown document");
        void Configure(nint layout) => DirectWriteLayoutOracle.SetFontWeight(layout, 0, 2, 700);
        var expected = DirectWriteLayoutOracle.CaretPositions("Segoe UI", displayed, 14, Configure);
        var offsets = new[] { 2, 3, 6, 7, 8, 9, 10, 11, 12, 13, 14 };
        check(offsets.Select((offset, index) => MathF.Abs(visual.X(cell.Start + offset, line) - row.CellX[0] - expected[index]) < .002f).All(value => value),
            "Table carets use independent whole-cell native shaping across bold and RTL text");
        var probes = DirectWriteLayoutOracle.GeometryProbes(displayed, Configure);
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
            var hit = visual.Hit(row.CellX[0] + probe.X, visual.Y(line) + row.Height / 2, 20);
            hits &= hit == cell.Start + Raw(probe.Position, probe.Trailing)
                && MathF.Abs(visual.X(hit, line) - row.CellX[0] - probe.CaretX) < .002f;
        }
        check(hits, "Table pointer geometry preserves independent native source edges and bidi affinity");
        var reference = probes.Ranges.Single(range => range.Start == 0 && range.End == 2).Fragments;
        var actual = visual.SelectionFragments(cell.Start + 2, cell.Start + 4, line);
        check(actual.Length == reference.Length && actual.Select((fragment, index) =>
            MathF.Abs(fragment.Left - row.CellX[0] - reference[index].Left) < .002f
            && MathF.Abs(fragment.Width - reference[index].Width) < .002f).All(value => value),
            "Table selections share independent native visual fragments instead of subtracting RTL prefix widths");
        var index = visual.Hit(row.CellX[0] - 1, visual.Y(line) + row.Height / 2, 20);
        var edges = probes.Hits.Select(probe => probe.CaretX).Distinct().Order().ToArray();
        var steps = true;
        foreach (var edge in edges.Skip(1))
        {
            var previousX = visual.X(index, line) - row.CellX[0];
            var target = probes.Hits.Where(probe => MathF.Abs(probe.CaretX - edge) < .002f
                && probe.X < edge && probe.X > previousX).OrderBy(probe => MathF.Abs(probe.X - edge)).First();
            index = visual.MoveHorizontal(index, right: true);
            steps &= index == cell.Start + Raw(target.Position, target.Trailing)
                && MathF.Abs(visual.X(index, line) - row.CellX[0] - edge) < .002f;
        }
        check(steps, "Physical arrows traverse a complete native table cell with original hidden-marker source boundaries");
        var nextCell = visual.MoveHorizontal(index, right: true);
        check(nextCell == row.Source.Cells[1].Start && visual.MoveHorizontal(nextCell, right: false) == cell.End,
            "Table arrows cross column boundaries without stopping on hidden pipe syntax");
        var snapshot = child.NativeParagraph;
        Frame();
        check(ReferenceEquals(child, visual.StyledTableRows[line].CellLayouts[0]) && ReferenceEquals(snapshot, child.NativeParagraph)
            && source.Document.ParseCount == 1,
            "Unchanged table frames retain source slices and their complete native paragraph snapshots");
        Frame(cell.Start + 3);
        check(visual.StyledTableRows.Count == 0 && visual.NativeParagraph is not null && source.Projection.Text == text
            && source.Document.ParseCount == 1,
            "An active table exposes original syntax through native whole-document layout without reparsing");
        check(child.NativeParagraph is null && child.StyledSource is null,
            "Retiring structured table rows releases their cell snapshots and borrowed source slices");
        visual.Clear();
        source.Release();
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
