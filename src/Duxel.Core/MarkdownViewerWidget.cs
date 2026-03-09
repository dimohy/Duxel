using System.Text;

namespace Duxel.Core;

public sealed class MarkdownViewerWidget(string id) : IUiCustomWidget
{
    private static readonly UiColor HeadingColor = new(0xFF7A5228);
    private static readonly UiColor LinkColor = new(0xFFB45A1B);
    private static readonly UiColor CodeColor = new(0xFF1D5B8F);
    private static readonly UiColor StrongColor = new(0xFF7C1F1F);
    private static readonly UiColor EmphasisColor = new(0xFF5A5A5A);

    private string _cachedMarkdown = string.Empty;
    private MarkdownDocument _document = MarkdownDocument.Empty;
    private readonly Dictionary<string, bool> _checklistOverrides = new(StringComparer.Ordinal);

    public string Id { get; } = ValidateId(id);

    public string Markdown { get; set; } = string.Empty;

    public float Height { get; set; } = 320f;

    public bool ShowBorder { get; set; } = true;

    public bool ShowStats { get; set; } = true;

    public void Render(UiImmediateContext ui)
    {
        ArgumentNullException.ThrowIfNull(ui);

        EnsureParsed();

        var childId = $"{Id}##markdown-viewer";
        if (ui.BeginChild(childId, new UiVector2(0f, Height), ShowBorder))
        {
            for (var i = 0; i < _document.Blocks.Count; i++)
            {
                RenderBlock(ui, _document.Blocks[i], i);
            }
        }
        ui.EndChild();

        if (ShowStats)
        {
            ui.TextDisabled($"Blocks: {_document.Blocks.Count}  Lines: {CountLines(Markdown)}  Chars: {Markdown.Length}");
        }
    }

    private void EnsureParsed()
    {
        if (string.Equals(_cachedMarkdown, Markdown, StringComparison.Ordinal))
        {
            return;
        }

        _cachedMarkdown = Markdown ?? string.Empty;
        _checklistOverrides.Clear();
        _document = Parse(_cachedMarkdown);
    }

    private void RenderBlock(UiImmediateContext ui, MarkdownBlock block, int blockIndex)
    {
        switch (block.Kind)
        {
            case MarkdownBlockKind.Heading:
                RenderHeading(ui, block);
                break;
            case MarkdownBlockKind.Paragraph:
                RenderInlineParagraph(ui, block.Inlines);
                break;
            case MarkdownBlockKind.BulletList:
                RenderBulletList(ui, block, blockIndex);
                break;
            case MarkdownBlockKind.OrderedList:
                RenderOrderedList(ui, block);
                break;
            case MarkdownBlockKind.Table:
                RenderTable(ui, block, blockIndex);
                break;
            case MarkdownBlockKind.Quote:
                RenderQuote(ui, block);
                break;
            case MarkdownBlockKind.CodeBlock:
                RenderCodeBlock(ui, block, blockIndex);
                break;
        }

        ui.Spacing();
    }

    private static void RenderHeading(UiImmediateContext ui, MarkdownBlock block)
    {
        var text = ExtractPlainText(block.Inlines);
        if (block.Level is 1)
        {
            ui.PushFontSize(ui.GetFontSize() * 1.35f);
            ui.SeparatorText(text);
            ui.PopFontSize();
            return;
        }

        if (block.Level is 2)
        {
            ui.PushFontSize(ui.GetFontSize() * 1.18f);
            ui.SeparatorText(text);
            ui.PopFontSize();
            return;
        }

        if (block.Level is 3)
        {
            ui.PushFontSize(ui.GetFontSize() * 1.08f);
            ui.TextColored(HeadingColor, text);
            ui.PopFontSize();
            return;
        }

        ui.TextColored(HeadingColor, text);
    }

    private static readonly UiColor QuoteBarColor = new(0xFF886644);
    private static readonly UiColor QuoteBgColor = new(0x18886644);

    private static void RenderQuote(UiImmediateContext ui, MarkdownBlock block)
    {
        var barWidth = 3f;
        var indent = barWidth + 8f;
        var lineHeight = ui.GetTextLineHeight();
        var paddingY = lineHeight * 0.3f;

        // Measure text height from content width
        var plainText = ExtractPlainText(block.Inlines);
        var availWidth = ui.GetContentRegionAvail().X;
        var textWidth = ui.CalcTextSize(plainText).X;
        var wrapWidth = MathF.Max(1f, availWidth - indent);
        var lineCount = MathF.Max(1f, MathF.Ceiling(textWidth / wrapWidth));
        var textHeight = lineCount * lineHeight;
        var totalHeight = textHeight + (paddingY * 2f);

        var cursorBefore = ui.GetCursorScreenPos();

        // Render text centered within the quote block
        ui.SetCursorPosY(cursorBefore.Y + paddingY);
        ui.Indent(indent);
        RenderInlineParagraph(ui, block.Inlines);
        ui.Unindent(indent);

        // Force cursor to end of quote block (skip internal ItemSpacing)
        ui.SetCursorPosY(cursorBefore.Y + totalHeight);

        // Draw background and bar
        var drawList = ui.GetWindowDrawList();
        drawList.AddRectFilled(new UiRect(cursorBefore.X, cursorBefore.Y, availWidth, totalHeight), QuoteBgColor);
        drawList.AddRectFilled(new UiRect(cursorBefore.X, cursorBefore.Y, barWidth, totalHeight), QuoteBarColor);
    }

    private void RenderCodeBlock(UiImmediateContext ui, MarkdownBlock block, int blockIndex)
    {
        var lineCount = CountLines(block.Text);
        var codeHeight = Math.Clamp((lineCount * ui.GetTextLineHeightWithSpacing()) + 12f, 48f, 240f);
        var codeId = $"{Id}##code-{blockIndex}";

        if (!string.IsNullOrWhiteSpace(block.Info))
        {
            ui.TextDisabled(block.Info);
            ui.SameLine();
        }

        if (ui.SmallButton($"Copy##{codeId}"))
        {
            ui.SetClipboardText(block.Text);
        }

        if (ui.IsItemHovered())
        {
            ui.SetTooltip("Copy code block to clipboard");
        }

        if (ui.BeginChild(codeId, new UiVector2(0f, codeHeight), true))
        {
            ui.TextUnformatted(block.Text);
        }
        ui.EndChild();
    }

    private void RenderBulletList(UiImmediateContext ui, MarkdownBlock block, int blockIndex)
    {
        for (var itemIndex = 0; itemIndex < block.Items.Count; itemIndex++)
        {
            var item = block.Items[itemIndex];
            var indent = item.Depth * 18f;
            if (indent > 0f)
            {
                ui.Indent(indent);
            }

            if (item.IsChecklist)
            {
                var key = $"{blockIndex}:{itemIndex}:{ExtractPlainText(item.Inlines)}";
                var value = _checklistOverrides.TryGetValue(key, out var overridden) ? overridden : item.IsChecked;
                if (ui.Checkbox($"##{Id}-check-{blockIndex}-{itemIndex}", ref value))
                {
                    _checklistOverrides[key] = value;
                }

                ui.SameLine();
                ui.AlignTextToFramePadding();
            }
            else
            {
                ui.Bullet();
                ui.SameLine();
            }

            RenderInlineParagraph(ui, item.Inlines);

            if (indent > 0f)
            {
                ui.Unindent(indent);
            }
        }
    }

    private static void RenderOrderedList(UiImmediateContext ui, MarkdownBlock block)
    {
        for (var itemIndex = 0; itemIndex < block.Items.Count; itemIndex++)
        {
            var item = block.Items[itemIndex];
            var number = item.Order > 0 ? item.Order : itemIndex + 1;
            var indent = item.Depth * 18f;
            if (indent > 0f)
            {
                ui.Indent(indent);
            }

            ui.TextDisabled($"{number}.");
            ui.SameLine();
            RenderInlineParagraph(ui, item.Inlines);

            if (indent > 0f)
            {
                ui.Unindent(indent);
            }
        }
    }

    private static void RenderTable(UiImmediateContext ui, MarkdownBlock block, int blockIndex)
    {
        if (block.TableRows.Count is 0 || block.TableHeader.Cells.Count is 0)
        {
            return;
        }

        var columnCount = block.TableHeader.Cells.Count;

        // Measure max content width per column for proportional sizing
        Span<float> maxWidths = columnCount <= 16 ? stackalloc float[columnCount] : new float[columnCount];
        for (var col = 0; col < columnCount; col++)
        {
            maxWidths[col] = MathF.Max(1f, ui.CalcTextSize(ExtractPlainText(block.TableHeader.Cells[col])).X);
            for (var row = 0; row < block.TableRows.Count; row++)
            {
                var cells = block.TableRows[row].Cells;
                if (col < cells.Count)
                {
                    maxWidths[col] = MathF.Max(maxWidths[col], ui.CalcTextSize(ExtractPlainText(cells[col])).X);
                }
            }
        }

        var totalMeasured = 0f;
        for (var i = 0; i < columnCount; i++)
        {
            totalMeasured += maxWidths[i];
        }

        var available = ui.GetContentRegionAvail().X;
        const float columnSpacingEstimate = 8f;
        var usable = MathF.Max(columnCount, available - (columnSpacingEstimate * (columnCount - 1)));

        if (!ui.BeginTable($"md-table-{blockIndex}", columnCount, UiTableFlags.Borders | UiTableFlags.RowBg))
        {
            return;
        }

        var cellPaddingY = ui.GetTextLineHeight() * 0.15f;
        ui.TableSetCellPaddingY(cellPaddingY);

        for (var col = 0; col < columnCount; col++)
        {
            var ratio = totalMeasured > 0f ? maxWidths[col] / totalMeasured : 1f / columnCount;
            ui.TableSetupColumn(ExtractPlainText(block.TableHeader.Cells[col]), ratio * usable, 0f);
        }
        ui.TableHeadersRow();

        var lineHeight = ui.GetTextLineHeight();

        for (var rowIndex = 0; rowIndex < block.TableRows.Count; rowIndex++)
        {
            var row = block.TableRows[rowIndex];

            // Pass 1: measure max cell height for this row
            var maxCellHeight = lineHeight;
            for (var columnIndex = 0; columnIndex < columnCount; columnIndex++)
            {
                var colWidth = ui.TableGetColumnWidth(columnIndex);
                if (colWidth <= 0f)
                {
                    continue;
                }

                var cell = columnIndex < row.Cells.Count ? row.Cells[columnIndex] : [];
                var textWidth = ui.CalcTextSize(ExtractPlainText(cell)).X;
                var lines = MathF.Max(1f, MathF.Ceiling(textWidth / colWidth));
                var cellHeight = lines * lineHeight;
                maxCellHeight = MathF.Max(maxCellHeight, cellHeight);
            }

            var rowMinHeight = maxCellHeight + (cellPaddingY * 2f);
            ui.TableNextRow(default, rowMinHeight);

            // Pass 2: render cells with vertical centering
            for (var columnIndex = 0; columnIndex < columnCount; columnIndex++)
            {
                if (columnIndex > 0)
                {
                    ui.TableNextColumn();
                }

                var cell = columnIndex < row.Cells.Count ? row.Cells[columnIndex] : [];
                var textWidth = ui.CalcTextSize(ExtractPlainText(cell)).X;
                var colWidth = MathF.Max(1f, ui.TableGetColumnWidth(columnIndex));
                var lines = MathF.Max(1f, MathF.Ceiling(textWidth / colWidth));
                var cellHeight = lines * lineHeight;
                var offsetY = MathF.Max(0f, (maxCellHeight - cellHeight) * 0.5f);

                ui.TableSetCellContentOffsetY(offsetY);
                RenderInlineFlat(ui, cell);
                ui.TableSetCellContentOffsetY(0f);
            }
        }

        ui.EndTable();
    }

    private static void RenderInlineFlat(UiImmediateContext ui, IReadOnlyList<MarkdownInline> inlines)
    {
        if (inlines.Count is 0)
        {
            ui.TextUnformatted(string.Empty);
            return;
        }

        var first = true;
        for (var i = 0; i < inlines.Count; i++)
        {
            var inline = inlines[i];
            if (string.IsNullOrEmpty(inline.Text))
            {
                continue;
            }

            if (!first)
            {
                ui.SameLine(0f);
            }

            RenderInlineToken(ui, inline);
            first = false;
        }

        if (first)
        {
            ui.TextUnformatted(string.Empty);
        }
    }

    private static void RenderInlineToken(UiImmediateContext ui, MarkdownInline inline)
    {
        switch (inline.Kind)
        {
            case MarkdownInlineKind.Link:
                if (Uri.TryCreate(inline.Url, UriKind.Absolute, out _))
                {
                    _ = ui.TextLinkOpenURL(inline.Text, inline.Url);
                    if (ui.IsItemHovered())
                    {
                        ui.SetTooltip(inline.Url);
                    }
                    return;
                }

                ui.TextColored(LinkColor, inline.Text);
                return;
            case MarkdownInlineKind.InlineCode:
                ui.TextColored(CodeColor, inline.Text);
                return;
            case MarkdownInlineKind.Strong:
                ui.TextColored(StrongColor, inline.Text);
                return;
            case MarkdownInlineKind.Emphasis:
                ui.TextColored(EmphasisColor, inline.Text);
                return;
            default:
                ui.TextUnformatted(inline.Text);
                return;
        }
    }

    private static void RenderInlineParagraph(UiImmediateContext ui, IReadOnlyList<MarkdownInline> inlines)
    {
        var tokens = Tokenize(inlines);
        var wrapWidth = MathF.Max(120f, ui.GetContentRegionAvail().X);
        var lineWidth = 0f;
        var firstOnLine = true;

        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            if (token.IsWhitespace && firstOnLine)
            {
                continue;
            }

            var tokenWidth = Math.Max(0f, ui.CalcTextSize(token.Text).X);
            var shouldWrap = !firstOnLine && !token.IsWhitespace && (lineWidth + tokenWidth) > wrapWidth;
            if (shouldWrap)
            {
                firstOnLine = true;
                lineWidth = 0f;
                if (token.IsWhitespace)
                {
                    continue;
                }
            }

            if (!firstOnLine)
            {
                ui.SameLine(0f);
            }

            RenderToken(ui, token);
            lineWidth += tokenWidth;
            firstOnLine = false;
        }
    }

    private static void RenderToken(UiImmediateContext ui, MarkdownToken token)
    {
        switch (token.Kind)
        {
            case MarkdownInlineKind.Link:
                if (Uri.TryCreate(token.Url, UriKind.Absolute, out _))
                {
                    _ = ui.TextLinkOpenURL(token.Text, token.Url);
                    if (ui.IsItemHovered())
                    {
                        ui.SetTooltip(token.Url);
                    }
                    return;
                }

                ui.TextColored(LinkColor, token.Text);
                return;
            case MarkdownInlineKind.InlineCode:
                ui.TextColored(CodeColor, token.Text);
                return;
            case MarkdownInlineKind.Strong:
                ui.TextColored(StrongColor, token.Text);
                return;
            case MarkdownInlineKind.Emphasis:
                ui.TextColored(EmphasisColor, token.Text);
                return;
            default:
                ui.TextUnformatted(token.Text);
                return;
        }
    }

    private static List<MarkdownToken> Tokenize(IReadOnlyList<MarkdownInline> inlines)
    {
        var tokens = new List<MarkdownToken>();
        for (var i = 0; i < inlines.Count; i++)
        {
            var inline = inlines[i];
            if (inline.Kind is MarkdownInlineKind.Link or MarkdownInlineKind.InlineCode)
            {
                tokens.Add(new MarkdownToken(inline.Kind, inline.Text, inline.Url, false));
                continue;
            }

            var span = inline.Text.AsSpan();
            var start = 0;
            while (start < span.Length)
            {
                var isWhitespace = char.IsWhiteSpace(span[start]);
                var end = start + 1;
                while (end < span.Length && char.IsWhiteSpace(span[end]) == isWhitespace)
                {
                    end++;
                }

                tokens.Add(new MarkdownToken(inline.Kind, span[start..end].ToString(), inline.Url, isWhitespace));
                start = end;
            }
        }

        return tokens;
    }

    private static string ExtractPlainText(IReadOnlyList<MarkdownInline> inlines)
    {
        if (inlines.Count is 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        for (var i = 0; i < inlines.Count; i++)
        {
            builder.Append(inlines[i].Text);
        }

        return builder.ToString();
    }

    private static MarkdownDocument Parse(string markdown)
    {
        var normalized = NormalizeNewlines(markdown);
        if (normalized.Length is 0)
        {
            return MarkdownDocument.Empty;
        }

        var lines = normalized.Split('\n');
        var blocks = new List<MarkdownBlock>();

        for (var index = 0; index < lines.Length;)
        {
            var line = lines[index];
            if (string.IsNullOrWhiteSpace(line))
            {
                index++;
                continue;
            }

            if (TryParseCodeBlock(lines, ref index, out var codeBlock))
            {
                blocks.Add(codeBlock);
                continue;
            }

            if (TryParseHeading(line, out var headingBlock))
            {
                blocks.Add(headingBlock);
                index++;
                continue;
            }

            if (TryParseQuote(lines, ref index, out var quoteBlock))
            {
                blocks.Add(quoteBlock);
                continue;
            }

            if (TryParseTable(lines, ref index, out var tableBlock))
            {
                blocks.Add(tableBlock);
                continue;
            }

            if (TryParseList(lines, ref index, out var listBlock))
            {
                blocks.Add(listBlock);
                continue;
            }

            blocks.Add(ParseParagraph(lines, ref index));
        }

        return new MarkdownDocument(blocks);
    }

    private static MarkdownBlock ParseParagraph(string[] lines, ref int index)
    {
        var parts = new List<string>();
        while (index < lines.Length)
        {
            var line = lines[index];
            if (string.IsNullOrWhiteSpace(line)
                || IsFenceStart(line)
                || IsHeading(line)
                || IsQuote(line)
                || IsTableStart(lines, index)
                || TryParseListItem(line, out _))
            {
                break;
            }

            parts.Add(line.Trim());
            index++;
        }

        return new MarkdownBlock(MarkdownBlockKind.Paragraph, 0, ParseInlines(string.Join(" ", parts)), [], string.Empty, string.Empty, new([]), []);
    }

    private static bool TryParseCodeBlock(string[] lines, ref int index, out MarkdownBlock block)
    {
        block = default;
        if (!IsFenceStart(lines[index]))
        {
            return false;
        }

        var info = lines[index].Trim()[3..].Trim();
        index++;

        var builder = new StringBuilder();
        while (index < lines.Length && !IsFenceStart(lines[index]))
        {
            if (builder.Length > 0)
            {
                builder.Append('\n');
            }

            builder.Append(lines[index]);
            index++;
        }

        if (index < lines.Length && IsFenceStart(lines[index]))
        {
            index++;
        }

        block = new MarkdownBlock(MarkdownBlockKind.CodeBlock, 0, [], [], builder.ToString(), info, new([]), []);
        return true;
    }

    private static bool TryParseHeading(string line, out MarkdownBlock block)
    {
        block = default;
        var trimmed = line.TrimStart();
        var level = 0;
        while (level < trimmed.Length && level < 6 && trimmed[level] == '#')
        {
            level++;
        }

        if (level is 0 || level >= trimmed.Length || trimmed[level] != ' ')
        {
            return false;
        }

        var text = trimmed[(level + 1)..].Trim();
        block = new MarkdownBlock(MarkdownBlockKind.Heading, level, ParseInlines(text), [], string.Empty, string.Empty, new([]), []);
        return true;
    }

    private static bool TryParseQuote(string[] lines, ref int index, out MarkdownBlock block)
    {
        block = default;
        if (!IsQuote(lines[index]))
        {
            return false;
        }

        var parts = new List<string>();
        while (index < lines.Length && IsQuote(lines[index]))
        {
            var trimmed = lines[index].TrimStart();
            var content = trimmed[1..].TrimStart();
            if (content.Length > 0)
            {
                parts.Add(content);
            }

            index++;
        }

        block = new MarkdownBlock(MarkdownBlockKind.Quote, 0, ParseInlines(string.Join(" ", parts)), [], string.Empty, string.Empty, new([]), []);
        return true;
    }

    private static bool TryParseTable(string[] lines, ref int index, out MarkdownBlock block)
    {
        block = default;
        if (!IsTableStart(lines, index))
        {
            return false;
        }

        var header = ParseTableRow(lines[index]);
        index += 2;

        var rows = new List<MarkdownTableRow>();
        while (index < lines.Length && IsTableContent(lines[index]))
        {
            rows.Add(ParseTableRow(lines[index]));
            index++;
        }

        block = new MarkdownBlock(MarkdownBlockKind.Table, 0, [], [], string.Empty, string.Empty, header, rows);
        return true;
    }

    private static bool TryParseList(string[] lines, ref int index, out MarkdownBlock block)
    {
        block = default;
        if (!TryParseListItem(lines[index], out var firstItem))
        {
            return false;
        }

        var items = new List<MarkdownListItem> { firstItem };
        index++;
        while (index < lines.Length && TryParseListItem(lines[index], out var item))
        {
            if (item.IsOrdered != firstItem.IsOrdered)
            {
                break;
            }

            items.Add(item);
            index++;
        }

        var kind = firstItem.IsOrdered ? MarkdownBlockKind.OrderedList : MarkdownBlockKind.BulletList;
        block = new MarkdownBlock(kind, 0, [], items, string.Empty, string.Empty, new([]), []);
        return true;
    }

    private static bool TryParseListItem(string line, out MarkdownListItem item)
    {
        item = default;
        var depth = CountLeadingSpaces(line) / 2;
        var trimmed = line.TrimStart();
        if (trimmed.Length < 2)
        {
            return false;
        }

        if (trimmed.StartsWith("- [ ] ", StringComparison.Ordinal)
            || trimmed.StartsWith("* [ ] ", StringComparison.Ordinal)
            || trimmed.StartsWith("+ [ ] ", StringComparison.Ordinal))
        {
            item = new MarkdownListItem(ParseInlines(trimmed[6..]), true, false, false, 0, depth);
            return true;
        }

        if (trimmed.StartsWith("- [x] ", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("* [x] ", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("+ [x] ", StringComparison.OrdinalIgnoreCase))
        {
            item = new MarkdownListItem(ParseInlines(trimmed[6..]), true, true, false, 0, depth);
            return true;
        }

        if (trimmed.StartsWith("- ", StringComparison.Ordinal)
            || trimmed.StartsWith("* ", StringComparison.Ordinal)
            || trimmed.StartsWith("+ ", StringComparison.Ordinal))
        {
            item = new MarkdownListItem(ParseInlines(trimmed[2..]), false, false, false, 0, depth);
            return true;
        }

        var dotIndex = trimmed.IndexOf('.');
        if (dotIndex > 0 && int.TryParse(trimmed[..dotIndex], out var order) && dotIndex + 1 < trimmed.Length && trimmed[dotIndex + 1] == ' ')
        {
            item = new MarkdownListItem(ParseInlines(trimmed[(dotIndex + 2)..]), false, false, true, order, depth);
            return true;
        }

        return false;
    }

    private static MarkdownTableRow ParseTableRow(string line)
    {
        var trimmed = line.Trim();
        if (trimmed.StartsWith("|", StringComparison.Ordinal))
        {
            trimmed = trimmed[1..];
        }

        if (trimmed.EndsWith("|", StringComparison.Ordinal))
        {
            trimmed = trimmed[..^1];
        }

        var parts = trimmed.Split('|');
        var cells = new List<IReadOnlyList<MarkdownInline>>(parts.Length);
        for (var i = 0; i < parts.Length; i++)
        {
            cells.Add(ParseInlines(parts[i].Trim()));
        }

        return new MarkdownTableRow(cells);
    }

    private static List<MarkdownInline> ParseInlines(string text)
    {
        var inlines = new List<MarkdownInline>();
        if (string.IsNullOrEmpty(text))
        {
            return inlines;
        }

        var buffer = new StringBuilder();
        for (var index = 0; index < text.Length;)
        {
            if (TryReadInlineCode(text, ref index, out var inlineCode))
            {
                FlushText(buffer, inlines);
                inlines.Add(new MarkdownInline(MarkdownInlineKind.InlineCode, inlineCode, string.Empty));
                continue;
            }

            if (TryReadDelimited(text, ref index, "**", out var strong))
            {
                FlushText(buffer, inlines);
                inlines.Add(new MarkdownInline(MarkdownInlineKind.Strong, strong, string.Empty));
                continue;
            }

            if (TryReadDelimited(text, ref index, "*", out var emphasis))
            {
                FlushText(buffer, inlines);
                inlines.Add(new MarkdownInline(MarkdownInlineKind.Emphasis, emphasis, string.Empty));
                continue;
            }

            if (TryReadLink(text, ref index, out var label, out var url))
            {
                FlushText(buffer, inlines);
                inlines.Add(new MarkdownInline(MarkdownInlineKind.Link, label, url));
                continue;
            }

            buffer.Append(text[index]);
            index++;
        }

        FlushText(buffer, inlines);
        return inlines;
    }

    private static bool TryReadInlineCode(string text, ref int index, out string inlineCode)
    {
        inlineCode = string.Empty;
        if (text[index] != '`')
        {
            return false;
        }

        var end = text.IndexOf('`', index + 1);
        if (end < 0)
        {
            return false;
        }

        inlineCode = text[(index + 1)..end];
        index = end + 1;
        return true;
    }

    private static bool TryReadDelimited(string text, ref int index, string delimiter, out string value)
    {
        value = string.Empty;
        if (!text.AsSpan(index).StartsWith(delimiter, StringComparison.Ordinal))
        {
            return false;
        }

        var start = index + delimiter.Length;
        var end = text.IndexOf(delimiter, start, StringComparison.Ordinal);
        if (end < 0)
        {
            return false;
        }

        value = text[start..end];
        if (value.Length is 0)
        {
            return false;
        }

        index = end + delimiter.Length;
        return true;
    }

    private static bool TryReadLink(string text, ref int index, out string label, out string url)
    {
        label = string.Empty;
        url = string.Empty;
        if (text[index] != '[')
        {
            return false;
        }

        var closeBracket = text.IndexOf(']', index + 1);
        if (closeBracket < 0 || closeBracket + 1 >= text.Length || text[closeBracket + 1] != '(')
        {
            return false;
        }

        var closeParen = text.IndexOf(')', closeBracket + 2);
        if (closeParen < 0)
        {
            return false;
        }

        label = text[(index + 1)..closeBracket];
        url = text[(closeBracket + 2)..closeParen];
        index = closeParen + 1;
        return true;
    }

    private static void FlushText(StringBuilder buffer, List<MarkdownInline> inlines)
    {
        if (buffer.Length is 0)
        {
            return;
        }

        inlines.Add(new MarkdownInline(MarkdownInlineKind.Text, buffer.ToString(), string.Empty));
        buffer.Clear();
    }

    private static bool IsFenceStart(string line)
    {
        return line.TrimStart().StartsWith("```", StringComparison.Ordinal);
    }

    private static bool IsHeading(string line)
    {
        return TryParseHeading(line, out _);
    }

    private static bool IsQuote(string line)
    {
        return line.TrimStart().StartsWith(">", StringComparison.Ordinal);
    }

    private static bool IsTableStart(string[] lines, int index)
    {
        if (index + 1 >= lines.Length)
        {
            return false;
        }

        return IsTableContent(lines[index]) && IsTableSeparator(lines[index + 1]);
    }

    private static bool IsTableContent(string line)
    {
        return !string.IsNullOrWhiteSpace(line) && line.Contains('|', StringComparison.Ordinal);
    }

    private static bool IsTableSeparator(string line)
    {
        var trimmed = line.Trim();
        if (!trimmed.Contains('|', StringComparison.Ordinal))
        {
            return false;
        }

        for (var i = 0; i < trimmed.Length; i++)
        {
            var ch = trimmed[i];
            if (ch is '|' or '-' or ':' or ' ')
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private static int CountLeadingSpaces(string line)
    {
        var count = 0;
        while (count < line.Length && line[count] == ' ')
        {
            count++;
        }

        return count;
    }

    private static string NormalizeNewlines(string text)
    {
        return (text ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
    }

    private static int CountLines(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 1;
        }

        var lines = 1;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                lines++;
            }
        }

        return lines;
    }

    private static string ValidateId(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return id;
    }

    private enum MarkdownBlockKind
    {
        Heading,
        Paragraph,
        BulletList,
        OrderedList,
        Table,
        Quote,
        CodeBlock,
    }

    private enum MarkdownInlineKind
    {
        Text,
        Emphasis,
        Strong,
        InlineCode,
        Link,
    }

    private sealed record MarkdownDocument(IReadOnlyList<MarkdownBlock> Blocks)
    {
        public static readonly MarkdownDocument Empty = new([]);
    }

    private readonly record struct MarkdownBlock(
        MarkdownBlockKind Kind,
        int Level,
        IReadOnlyList<MarkdownInline> Inlines,
        IReadOnlyList<MarkdownListItem> Items,
        string Text,
        string Info,
        MarkdownTableRow TableHeader,
        IReadOnlyList<MarkdownTableRow> TableRows
    );

    private readonly record struct MarkdownInline(MarkdownInlineKind Kind, string Text, string Url);

    private readonly record struct MarkdownListItem(
        IReadOnlyList<MarkdownInline> Inlines,
        bool IsChecklist,
        bool IsChecked,
        bool IsOrdered,
        int Order,
        int Depth
    );

    private readonly record struct MarkdownTableRow(IReadOnlyList<IReadOnlyList<MarkdownInline>> Cells);

    private readonly record struct MarkdownToken(MarkdownInlineKind Kind, string Text, string Url, bool IsWhitespace);
}