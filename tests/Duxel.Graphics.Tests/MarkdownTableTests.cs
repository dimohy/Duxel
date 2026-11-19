using Duxel.Core;

internal static class MarkdownTableTests
{
    internal static void Run(Action<bool, string> check)
    {
        MarkdownTableParagraphTests.Run(check);
        MarkdownMixedParagraphTests.Run(check);
        const string text = "Before\n\n| Left | Middle | Right |\n| :--- | :---: | ---: |\n| a\\|b | **한글** | 12 |\n| | | |\n\nAfter";
        var source = new UiMarkdownTextSource(new(text));
        source.Prepare(text, -1);
        check(source.Tables.Count == 1 && source.Tables[0].Rows.Length == 3
            && source.Tables[0].Columns.SequenceEqual(new[] { UiStyledTableAlignment.Left, UiStyledTableAlignment.Center, UiStyledTableAlignment.Right }),
            "Markdown table model retains parsed rows, columns and independent alignment");
        var table = source.Tables[0];
        check(text[table.Source.Start..table.Source.End].StartsWith("| Left", StringComparison.Ordinal)
            && text[table.Source.Start..table.Source.End].EndsWith("| | | |", StringComparison.Ordinal),
            "Markdown table source range includes outer pipes, the separator row and empty trailing cells");
        check(source.Projection.Text == "Before\n\nLeftMiddleRight\na|b한글12\n\n\nAfter",
            "Markdown table projection hides structural pipes and alignment syntax while preserving escaped cell pipes: " + source.Projection.Text.Replace("\n", "\\n"));
        check(table.Rows[2].Cells.All(cell => cell.Length == 0 && cell.Start >= table.Rows[2].Source.Start && cell.End <= table.Rows[2].Source.End),
            "Markdown empty table cells have valid source insertion positions");
        var map = source.Projection.SourceToDisplay;
        check(Enumerable.Range(1, map.Length - 1).All(i => map[i] >= map[i - 1]) && map[^1] == source.Projection.Text.Length,
            "Markdown table projection preserves total monotonic source boundary mapping");
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        var lines = new UiVisualTextLines();
        context.NewFrame(new(1f / 60, new(400, 300), new(1, 1)), idle, new(0, 0, 400, 300), UiTextSettings.Default);
        context.Render(new Screen(ui =>
        {
            using var typography = new UiControlContext(ui).BeginTypography<UiRegularTypeface, UiFluentType300>();
            void Prepare(float width, int active = -1) => lines.Prepare(ui, text, width, 180, 20, 12,
                styled: source, activeSourceIndex: active, horizontalGutter: 12);
            Prepare(300);
            check(lines.Count == 7 && lines.StyledTableRows.Count == 3 && !lines.HasUnwrappedOverflow,
                "Markdown table layout replaces the raw separator line with three structured visual rows");
            var rendered = lines.StyledTableRows.OrderBy(pair => pair.Key).Select(pair => pair.Value).ToArray();
            check(rendered.All(row => row.Height == 32 && row.ColumnEdges[^1] == 300)
                && ReferenceEquals(rendered[0].ColumnEdges, rendered[1].ColumnEdges),
                "Markdown table cells share column edges, row padding and the full available width");
            var korean = text.IndexOf("한글", StringComparison.Ordinal) + 1;
            var rowIndex = lines.FindLine(korean);
            check(lines.Hit(lines.X(korean, rowIndex) + .1f, lines.Y(rowIndex) + 16, 20) == korean,
                "Markdown table hit testing and caret positions map to the same original Korean cell character");
            var emptyLine = lines.StyledTableRows.Single(pair => pair.Value.Source.Cells.All(cell => cell.Length == 0));
            for (var column = 0; column < 3; column++)
            {
                var empty = emptyLine.Value;
                var x = empty.ColumnEdges[column + 1] - 2;
                check(lines.Hit(x, lines.Y(emptyLine.Key) + 16, 20) == empty.Source.Cells[column].Start,
                    "Markdown empty-cell padding maps to that cell rather than a neighboring column: " + column);
            }
            var builds = lines.Builds;
            Prepare(300);
            check(lines.Builds == builds && source.Document.ParseCount == 1,
                "Markdown table layout and parsed cell structure are reused unchanged");
            Prepare(300, korean);
            check(lines.StyledTableRows.Count == 0 && source.Projection.Text == text && lines.Count == 8,
                "Markdown active table reveals the complete original table as editable raw lines");
            Prepare(80);
            check(lines.StyledTableRows.Count == 3 && lines.HasUnwrappedOverflow && lines.Width > 80
                && lines.EffectiveWidth < 80 && lines.EffectiveHeight < 180,
                "Markdown wide tables retain complete cell content and reserve both scrollbar gutters when needed");
            var intrinsicWidth = lines.Width;
            lines.Prepare(ui, text, intrinsicWidth + 6, 100, 20, 12, styled: source, horizontalGutter: 12);
            check(lines.HasUnwrappedOverflow && lines.EffectiveHeight == 88 && lines.EffectiveWidth < intrinsicWidth,
                "Markdown table scrollbar sizing converges when a vertical gutter introduces horizontal overflow");
            foreach (var newline in new[] { "\n", "\r\n", "\r" })
            {
                var headerOnly = "| A |" + newline + "| --- |";
                var headerSource = new UiMarkdownTextSource(new(headerOnly));
                var headerLines = new UiVisualTextLines();
                headerLines.Prepare(ui, headerOnly, 300, 180, 20, 0, styled: headerSource);
                check(headerLines.Count == 1 && headerLines.Height == 32 && headerLines.StyledTableRows.Count == 1,
                    "Markdown header-only table consumes its hidden separator with LF, CRLF and CR source lines");
            }
        }));
        using var data = context.GetDrawData();
        foreach (var sample in new[]
        {
            "| A | B |\r\n| --- | --- |\r\n| one |\r\n",
            "A | B\n--- | ---\none | two",
            "| A |\n| --- |",
            "| A | B |\r| --- | --- |\r| one | two |"
        })
        {
            var parsed = new UiMarkdownTextSource(new(sample));
            parsed.Prepare(sample, -1);
            check(parsed.Tables.Count == 1 && parsed.Tables[0].Rows.All(row => row.Cells.Length == parsed.Tables[0].Columns.Length)
                && parsed.Tables[0].Source.End <= sample.Length,
                "Markdown table source metadata handles CRLF, missing cells, optional outer pipes and header-only tables");
        }
        var crList = new UiMarkdownTextSource(new("- one\r- two"));
        crList.Prepare(crList.Document.Source, -1);
        check(crList.Projection.Text == "• one\r• two", "Markdown list prefixes follow CR-only source line boundaries");
        lines.Clear();
        source.Release();
        check(lines.StyledTableRows.Count == 0 && source.Tables.Count == 0,
            "Markdown table source and layout ownership release retained cell metadata");
        foreach (var (escaped, rendered) in new[]
        {
            (@"a\|b", "a|b"), (@"a\\b", @"a\b"), (@"\*a\*", "*a*"), (@"\\\\", @"\\"), (@"\。", @"\。")
        })
        {
            foreach (var inTable in new[] { false, true })
            {
                var input = inTable ? "| H |\n| --- |\n| " + escaped + " |" : escaped;
                var parsed = new UiMarkdownTextSource(new(input));
                parsed.Prepare(input, -1);
                check(parsed.Projection.Text == (inTable ? "H\n" : "") + rendered,
                    "Markdown escaped literal source mapping preserves adjacent pairs and non-ASCII punctuation: " + escaped + "/" + inTable
                    + " actual=" + parsed.Projection.Text.Replace("\n", "\\n"));
            }
        }
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
