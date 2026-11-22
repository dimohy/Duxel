namespace Duxel.Core;

internal enum UiCodeToken { None, Plain, Keyword, String, Comment, Number, Type, Directive, Punctuation, Interpolation, Escape, Method, Operator }

/// <summary>A cached lexical projection. Every displayed boundary maps to the unchanged C# buffer.</summary>
internal sealed partial class UiCodeDocument : UiStyledTextSource
{
    private static readonly HashSet<string> Keywords = new(("abstract as async await base bool break byte case catch char checked class const continue decimal default delegate do double else enum event explicit extern false finally fixed float for foreach goto if implicit in int interface internal is lock long namespace new null object operator out override params partial private protected public readonly record ref required return sbyte scoped sealed short sizeof stackalloc static string struct switch this throw true try typeof uint ulong unchecked unmanaged unsafe ushort using var virtual void volatile when where while with yield get set init value global file not and or nint nuint").Split(' '), StringComparer.Ordinal);
    internal readonly UiCodeParagraphLayouts ParagraphLayouts = new();
    internal (UiLayoutMeasurementKey Key, long Revision, PlatformTextParagraphStyle[] Styles, float[] Heights)? ParagraphStyles;
    private string _source = string.Empty;
    private readonly List<UiStyledSourceRun> _runs = [];
    private long _revision;
    internal int ParseCount { get; private set; }
    internal override long Revision => _revision;
    internal override string Source => _source;
    internal override IReadOnlyList<UiStyledSourceRun> Runs => _runs;
    internal override UiSourceTextRange? ActiveBlock => null;
    internal override UiStyledTextSource Composition => this;
    internal override void Prepare(string source, int activeIndex, bool preserveActiveBlock = false)
    {
        if (_source == source && ParseCount != 0) return;
        _source = source;
        _runs.Clear();
        var spans = new Tokenizer(source).Scan();
        foreach (var span in spans)
        {
            var begin = span.Start;
            var script = source[begin] > 0x024f;
            var cursor = begin;
            while (cursor < span.Start + span.Length)
            {
                var rune = System.Text.Rune.GetRuneAt(source, cursor);
                var category = System.Text.Rune.GetUnicodeCategory(rune);
                var nextScript = category is System.Globalization.UnicodeCategory.NonSpacingMark
                    or System.Globalization.UnicodeCategory.SpacingCombiningMark ? script : rune.Value > 0x024f;
                if (nextScript != script && cursor > begin)
                {
                    _runs.Add(new(new(begin, cursor - begin), source.Substring(begin, cursor - begin), new(CodeToken : span.Kind, CodeScript : script), false));
                    begin = cursor;
                }
                script = nextScript;
                cursor += rune.Utf16SequenceLength;
            }
            _runs.Add(new(new(begin, cursor - begin), source.Substring(begin, cursor - begin), new(CodeToken : span.Kind, CodeScript : script), false));
        }
        ParseCount++;
        _revision++;
        ReleaseProjection();
    }
}
