using Markdig;
using Markdig.Extensions.EmphasisExtras;
using Markdig.Extensions.TaskLists;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace Duxel.Core;

/// <summary>One parsed Markdown source revision shared by its rendered and editable projections.</summary>
public sealed partial class UiMarkdownDocument
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UsePreciseSourceLocation().UsePipeTables().UseTaskLists()
        .UseEmphasisExtras(EmphasisExtraOptions.Strikethrough).Build();
    private string? _source;
    private UiStyledSourceRun[] _runs = [];
    private UiSourceTextRange[] _blocks = [];
    private UiStyledTextTable[] _tables = [];
    private UiMarkdownLink[] _links = [];
    internal IReadOnlyList<UiMarkdownLink> Links => _links;
    private UiMarkdownImageReference[] _images = [];
    internal IReadOnlyList<UiMarkdownImageReference> Images => _images;
    internal IReadOnlyList<UiStyledTextTable> Tables => _tables;
    public string Source => _source ?? string.Empty;
    public long Revision
    {
        get; private set;
    }
    public int ParseCount
    {
        get; private set;
    }
    public int BlockCount => _blocks.Length;
    internal IReadOnlyList<UiStyledSourceRun> Runs => _runs;

    public UiMarkdownDocument(string source = "") => Update(source);

    public bool Update(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (string.Equals(_source, source, StringComparison.Ordinal))
            return false;
        var syntax = Markdig.Markdown.Parse(source, Pipeline);
        var lineStarts = new List<int> { 0 };
        for (var i = 0; i < source.Length; i++)
            if (source[i] is '\r' or '\n')
            {
                if (source[i] == '\r' && i + 1 < source.Length && source[i + 1] == '\n')
                    i++;
                lineStarts.Add(i + 1);
            }
        var lines = lineStarts.ToArray();
        var compiler = new Compiler(source, lines);
        foreach (var block in syntax)
            compiler.Block(block, default);
        _runs = compiler.Finish();
        _tables = compiler.Tables.ToArray();
        _links = compiler.Links.ToArray();
        _images = compiler.Images.ToArray();
        _blocks = syntax.Select(block => BlockRange(block, source, lines)).Where(range => range.Length > 0).ToArray();
        _source = source;
        Revision = checked(Revision + 1);
        ParseCount++;
        return true;
    }

    internal UiSourceTextRange? BlockAt(int index)
    {
        if (index < 0)
            return null;
        if (Source.Length == 0)
            return new(0, 0);
        foreach (var block in _blocks)
            if (block.Contains(index) || index == Source.Length && block.End == index)
                return block;
        // Blank lines remain editable source blocks too.
        var start = SourceLineStart(Source, index);
        return new(start, SourceLineEnd(Source, index) - start);
    }
    internal void Release()
    {
        _source = string.Empty;
        _runs = [];
        _blocks = [];
        _tables = [];
        _links = [];
        _images = [];
        Revision = checked(Revision + 1);
    }

    private static UiSourceTextRange Range(SourceSpan span, int length)
    {
        var start = Math.Clamp(span.Start, 0, length);
        var end = Math.Clamp((long)span.End + 1, start, length);
        return new(start, (int)end - start);
    }

    private static int SourceLineEnd(string source, int index)
    {
        var end = source.AsSpan(index).IndexOfAny('\r', '\n');
        return end < 0 ? source.Length : index + end;
    }

    private static int SourceLineStart(string source, int index) => source.AsSpan(0, index).LastIndexOfAny('\r', '\n') + 1;

    private static int SourceNextLineStart(string source, int end)
    {
        if (end < source.Length && source[end] == '\r')
            end++;
        if (end < source.Length && source[end] == '\n')
            end++;
        return end;
    }

    private static UiSourceTextRange BlockRange(Block block, string source, int[] lines)
    {
        if (block is Table { Count: > 0 } table)
        {
            var start = lines[table.Line];
            var lastLine = table.Line + table.Count - 1 + (table[0] is TableRow { IsHeader: true } ? 1 : 0);
            return new(start, SourceLineEnd(source, lines[lastLine]) - start);
        }
        var range = Range(block.Span, source.Length);
        var end = range.End;
        if (block is FencedCodeBlock fenced)
        {
            // An unclosed fenced block's Span may end inside the opening info string.
            // Retained source-line positions include its actual content without rescanning syntax.
            end = SourceLineEnd(source, Math.Max(range.Start, end - 1));
            if (fenced.Lines.Count > 0)
                end = Math.Max(end, SourceLineEnd(source, fenced.Lines.Lines[fenced.Lines.Count - 1].Position));
        }
        if (block is ContainerBlock container)
            foreach (var child in container)
                end = Math.Max(end, BlockRange(child, source, lines).End);
        return new(range.Start, end - range.Start);
    }

    private sealed partial class Compiler(string source, int[] lineStarts)
    {
        private string SourceText => source;
        private int[] SourceLineStarts => lineStarts;
        private readonly UiStyledTextStyle[] _styles = new UiStyledTextStyle[source.Length];
        private readonly bool[] _syntax = new bool[source.Length];
        private readonly Dictionary<int, (int Length, string Display)> _replacements = [];
        internal List<UiMarkdownLink> Links { get; } = [];
        internal List<UiMarkdownImageReference> Images { get; } = [];

        private void Style(UiSourceTextRange range, UiStyledTextStyle style)
        {
            for (var i = range.Start; i < range.End; i++)
                _styles[i] = style;
        }
        private void Hide(int start, int end)
        {
            for (var i = Math.Max(0, start); i < Math.Min(source.Length, end); i++)
                _syntax[i] = true;
        }

        internal void Block(Block block, UiStyledTextStyle inherited)
        {
            var range = BlockRange(block, source, lineStarts);
            var style = inherited;
            if (block is TableRow { IsHeader: true })
                style = style with
                {
                    Flags = style.Flags | UiStyledTextFlags.Bold
                };
            if (block is HeadingBlock heading)
                style = style with
                {
                    Heading = heading.Level,
                    Flags = style.Flags | UiStyledTextFlags.Bold
                };
            if (block is QuoteBlock)
                style = style with
                {
                    Flags = style.Flags | UiStyledTextFlags.Quote
                };
            if (block is CodeBlock)
                style = style with
                {
                    Flags = style.Flags | UiStyledTextFlags.Code
                };
            Style(range, style);
            if (block is ThematicBreakBlock)
            {
                Style(range, style with
                {
                    Flags = style.Flags | UiStyledTextFlags.Rule
                });
                Hide(range.Start, range.End);
            }
            if (block is LeafBlock { Inline: { } inline })
            {
                if (block is HeadingBlock)
                {
                    if (inline.FirstChild is { } first)
                        Hide(range.Start, first.Span.Start);
                    if (inline.LastChild is { } last)
                        Hide(last.Span.End + 1, range.End);
                }
                Inline(inline, style);
            }
            if (block is FencedCodeBlock fenced)
            {
                var firstEnd = SourceLineEnd(source, range.Start);
                Hide(range.Start, Math.Min(SourceNextLineStart(source, firstEnd), range.End));
                // Only the parser can distinguish a closing fence from literal code content.
                if (fenced.ClosingFencedCharCount > 0)
                {
                    var closing = SourceLineStart(source, Math.Max(range.Start, range.End - 1));
                    // The separator before a closing fence is not a rendered empty code line.
                    if (closing > range.Start)
                    {
                        closing--;
                        if (closing > range.Start && source[closing - 1] == '\r')
                            closing--;
                    }
                    Hide(closing, range.End);
                }
            }
            if (block is ContainerBlock container)
                foreach (var child in container)
                    Block(child, style);
            if (block is Table table)
                CompileTable(table, range);
            if (block is QuoteBlock or ListItemBlock)
                CompilePrefix(range, block is QuoteBlock,
                    block is ListItemBlock { Count: > 0 } item && item[0] is ParagraphBlock { Inline.FirstChild: TaskList });
        }

        private void CompilePrefix(UiSourceTextRange range, bool quote, bool task)
        {
            var start = SourceLineStart(source, range.Start);
            for (var line = start; line < range.End;)
            {
                var cursor = line;
                while (cursor < range.End && source[cursor] is ' ' or '\t')
                    cursor++;
                if (quote && cursor < range.End && source[cursor] == '>')
                    _replacements[cursor] = (1, "│");
                else if (!quote && cursor + 1 < range.End && source[cursor] is '-' or '*' or '+' && source[cursor + 1] == ' ')
                {
                    if (task && line == start)
                        Hide(cursor, cursor + 2);
                    else
                        _replacements[cursor] = (1, "•");
                }
                var next = SourceLineEnd(source, line);
                if (next == source.Length)
                    break;
                line = SourceNextLineStart(source, next);
            }
        }

        private void Inline(Inline inline, UiStyledTextStyle inherited)
        {
            var range = Range(inline.Span, source.Length);
            var style = inherited;
            if (inline is LinkInline { IsImage: true } image)
            {
                var alt = new System.Text.StringBuilder();
                AppendAlternative(image, alt);
                Images.Add(new(range, image.Url ?? string.Empty, alt.ToString()));
                Style(range, style with
                {
                    ImageIndex = Images.Count
                });
                _replacements[range.Start] = (range.Length, "\uFFFC");
                return;
            }
            if (inline is TaskList task)
            {
                Style(range, style with
                {
                    Flags = style.Flags | UiStyledTextFlags.Task
                    | (task.Checked ? UiStyledTextFlags.Checked : 0)
                });
                _replacements[range.Start] = (range.Length, "\uFFFC");
            }
            if (inline is AutolinkInline autoLink)
            {
                Links.Add(new(new(range.Start + 1, Math.Max(0, range.Length - 2)),
                    autoLink.IsEmail ? "mailto:" + autoLink.Url : autoLink.Url, autoLink.Url));
                Style(range, style with
                {
                    Flags = style.Flags | UiStyledTextFlags.Link
                });
                Hide(range.Start, range.Start + 1);
                Hide(range.End - 1, range.End);
            }
            if (inline is EmphasisInline emphasis)
            {
                var flag = emphasis.DelimiterChar == '~' ? UiStyledTextFlags.Strike
                    : emphasis.DelimiterCount >= 2 ? UiStyledTextFlags.Bold : UiStyledTextFlags.Italic;
                style = style with
                {
                    Flags = style.Flags | flag
                };
                Style(range, style);
                if (emphasis.FirstChild is { } first)
                    Hide(range.Start, first.Span.Start);
                if (emphasis.LastChild is { } last)
                    Hide(last.Span.End + 1, range.End);
            }
            if (inline is LinkInline link)
            {
                if (!link.IsImage && link.FirstChild is { } firstLabel && link.LastChild is { } lastLabel && link.Url is { } url)
                {
                    var name = new System.Text.StringBuilder();
                    AppendAlternative(link, name);
                    Links.Add(new(Range(new(firstLabel.Span.Start, lastLabel.Span.End), source.Length), url, name.ToString()));
                }
                style = style with
                {
                    Flags = style.Flags | UiStyledTextFlags.Link
                };
                Style(range, style);
                if (link.FirstChild is { } first)
                    Hide(range.Start, first.Span.Start);
                if (link.LastChild is { } last)
                    Hide(last.Span.End + 1, range.End);
            }
            if (inline is CodeInline)
            {
                Style(range, style with
                {
                    Flags = style.Flags | UiStyledTextFlags.Code
                });
                var left = range.Start;
                var right = range.End;
                while (left < right && source[left] == '`')
                    _syntax[left++] = true;
                while (right > left && source[right - 1] == '`')
                    _syntax[--right] = true;
            }
            if (inline is HtmlEntityInline entity && range.Length > 0)
                _replacements[range.Start] = (range.Length, entity.Transcoded.ToString());
            if (inline is LiteralInline literal && range.Length > 0)
            {
                // Table post-processing can map an escaped literal to its decoded first character.
                // Its semantic content identifies that span without consuming a neighboring escaped pair.
                if (literal.IsFirstCharacterEscaped && range.Start > 0 && source[range.Start - 1] == '\\'
                    && source.AsSpan(range.Start, range.Length).TrimEnd().SequenceEqual(literal.Content.AsSpan()))
                    Hide(range.Start - 1, range.Start);
                for (var i = range.Start; i + 1 < range.End; i++)
                    if (source[i] == '\\' && source[i + 1] is >= '!' and <= '/' or >= ':' and <= '@' or >= '[' and <= '`' or >= '{' and <= '~')
                        _syntax[i++] = true;
            }
            if (inline is ContainerInline container)
                foreach (var child in container)
                    Inline(child, style);
        }

        internal UiStyledSourceRun[] Finish()
        {
            var runs = new List<UiStyledSourceRun>();
            for (var start = 0; start < source.Length;)
            {
                if (_replacements.TryGetValue(start, out var replacement))
                {
                    runs.Add(new(new(start, replacement.Length), replacement.Display, _styles[start], false));
                    start += replacement.Length;
                    continue;
                }
                var end = start + 1;
                while (end < source.Length && _styles[end] == _styles[start] && _syntax[end] == _syntax[start] && !_replacements.ContainsKey(end))
                    end++;
                runs.Add(new(new(start, end - start), _syntax[start] ? string.Empty : source[start..end], _styles[start], _syntax[start]));
                start = end;
            }
            return runs.ToArray();
        }

        private static void AppendAlternative(ContainerInline container, System.Text.StringBuilder text)
        {
            foreach (var child in container)
                if (child is LiteralInline literal)
                    text.Append(literal.Content);
                else if (child is CodeInline code)
                    text.Append(code.Content);
                else if (child is HtmlEntityInline entity)
                    text.Append(entity.Transcoded);
                else if (child is LineBreakInline)
                    text.Append(' ');
                else if (child is ContainerInline nested)
                    AppendAlternative(nested, text);
        }
    }
}

internal sealed class UiMarkdownTextSource(UiMarkdownDocument document) : UiStyledTextSource
{
    private readonly UiMarkdownImageCache _images = new();
    private Func<Uri, UiImageSource?>? _imageResolver;
    private Uri? _baseUri;
    internal override IReadOnlyList<UiStyledTextImage> Images => _images.Images;
    internal void ConfigureImages(Func<Uri, UiImageSource?>? resolver, Uri? baseUri)
    {
        _imageResolver = resolver;
        _baseUri = baseUri;
        if (_images.Configure(resolver, baseUri))
            _revision = checked(_revision + 1);
        _composition?.ConfigureImages(resolver, baseUri);
    }
    internal void InvalidateImages()
    {
        _images.Invalidate();
        _revision = checked(_revision + 1);
        _composition?.InvalidateImages();
    }
    private UiMarkdownTextSource? _composition;
    private UiSourceTextRange? _activeBlock;
    private long _documentRevision;
    private long _revision;
    internal UiMarkdownDocument Document => document;
    internal override long Revision => _revision;
    internal override string Source => document.Source;
    internal override IReadOnlyList<UiStyledSourceRun> Runs => document.Runs;
    internal override IReadOnlyList<UiStyledTextTable> Tables => document.Tables;
    internal override UiSourceTextRange? ActiveBlock => _activeBlock;
    internal override UiStyledTextSource Composition
    {
        get
        {
            var composition = _composition ??= new(new());
            composition.ConfigureImages(_imageResolver, _baseUri);
            return composition;
        }
    }
    internal void Release()
    {
        ReleaseProjection();
        _images.Dispose();
        document.Release();
        _imageResolver = null;
        _baseUri = null;
        _composition?.Release();
        _composition = null;
        _activeBlock = null;
        _revision = checked(_revision + 1);
    }
    internal override void Prepare(string source, int activeIndex, bool preserveActiveBlock = false)
    {
        var changed = document.Update(source);
        _images.Prepare(document);
        var active = preserveActiveBlock && !changed ? _activeBlock : document.BlockAt(activeIndex);
        if (_documentRevision == document.Revision && active == _activeBlock)
            return;
        _documentRevision = document.Revision;
        _activeBlock = active;
        _revision = checked(_revision + 1);
    }
}
