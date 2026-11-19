namespace Duxel.Core;

[Flags]
internal enum UiStyledTextFlags
{
    None = 0, Bold = 1, Italic = 2, Strike = 4, Code = 8, Link = 16, Quote = 32, Rule = 64, Syntax = 128,
    Task = 256, Checked = 512, Semibold = 1024, Caption = 2048
}

internal readonly record struct UiStyledTextStyle(int Heading = 0, UiStyledTextFlags Flags = UiStyledTextFlags.None,
    int ImageIndex = 0, int ObjectIndex = 0, UiCodeToken CodeToken = UiCodeToken.None, bool CodeScript = false);
internal readonly record struct UiStyledTextObject(UiVector2 Size);
internal readonly record struct UiSourceTextRange(int Start, int Length)
{
    internal int End => checked(Start + Length);
    internal bool Contains(int index) => index >= Start && index < End;
}

/// <summary>A source span and its display text; source offsets always refer to the unchanged editor buffer.</summary>
internal readonly record struct UiStyledSourceRun(UiSourceTextRange Source, string Display,
    UiStyledTextStyle Style, bool Syntax);

/// <summary>Shared source/display contract for text measurement, painting and source-based editing.</summary>
internal abstract class UiStyledTextSource
{
    private long _projectionRevision = -1;
    private UiStyledTextProjection? _projection;
    internal UiStyledTextProjection Projection
    {
        get
        {
            if (_projectionRevision != Revision)
            {
                _projection = new(Source, Runs, ActiveBlock, Images, Objects);
                _projectionRevision = Revision;
            }
            return _projection ?? throw new InvalidOperationException("A styled source must be prepared before layout.");
        }
    }
    internal abstract long Revision
    {
        get;
    }
    internal abstract string Source
    {
        get;
    }
    internal abstract IReadOnlyList<UiStyledSourceRun> Runs
    {
        get;
    }
    internal virtual IReadOnlyList<UiStyledTextTable> Tables => [];
    internal virtual IReadOnlyList<UiStyledTextImage> Images => [];
    internal virtual IReadOnlyList<UiStyledTextObject> Objects => [];
    internal abstract UiSourceTextRange? ActiveBlock
    {
        get;
    }
    internal abstract UiStyledTextSource Composition
    {
        get;
    }
    internal abstract void Prepare(string source, int activeIndex, bool preserveActiveBlock = false);
    protected void ReleaseProjection()
    {
        _projection = null;
        _projectionRevision = -1;
    }
}

internal readonly record struct UiStyledDisplayRun(int Start, int Length, UiStyledTextStyle Style);

/// <summary>Cached display text with a total, monotonic mapping from every UTF-16 source boundary.</summary>
internal sealed class UiStyledTextProjection
{
    internal readonly string Text;
    internal readonly int[] SourceToDisplay;
    internal readonly UiStyledDisplayRun[] Runs;
    internal readonly IReadOnlyList<UiStyledTextImage> Images;
    internal readonly IReadOnlyList<UiStyledTextObject> Objects;
    internal readonly UiSourceTextRange[] Atoms;
    internal readonly bool NativeAtomsCanWrap;
    internal UiStyledTextStyle StyleAt(int displayIndex)
    {
        if (Runs.Length == 0)
            return default;
        var low = 0;
        var high = Runs.Length - 1;
        while (low < high)
        {
            var middle = low + (high - low) / 2;
            if (Runs[middle].Start + Runs[middle].Length <= displayIndex)
                low = middle + 1;
            else
                high = middle;
        }
        return Runs[low].Style;
    }

    internal UiStyledTextProjection(string source, IReadOnlyList<UiStyledSourceRun> runs, UiSourceTextRange? active,
        IReadOnlyList<UiStyledTextImage>? images = null, IReadOnlyList<UiStyledTextObject>? objects = null)
    {
        Images = images ?? [];
        Objects = objects ?? [];
        var text = new System.Text.StringBuilder(source.Length);
        var displayRuns = new List<UiStyledDisplayRun>();
        SourceToDisplay = new int[source.Length + 1];
        foreach (var run in runs)
        {
            var at = run.Source.Start;
            while (at < run.Source.End)
            {
                var raw = active is { } block && block.Contains(at);
                var end = run.Source.End;
                if (active is { } range)
                    end = raw ? Math.Min(end, range.End) : range.Start > at ? Math.Min(end, range.Start) : end;
                var begin = text.Length;
                var style = raw && run.Syntax ? run.Style with
                {
                    Flags = run.Style.Flags | UiStyledTextFlags.Syntax
                } : run.Style;
                if (raw || run.Display.Length == run.Source.Length)
                {
                    text.Append(raw ? source.AsSpan(at, end - at) : run.Display.AsSpan(at - run.Source.Start, end - at));
                    for (var i = at; i <= end; i++)
                        SourceToDisplay[i] = begin + i - at;
                }
                else
                {
                    SourceToDisplay[at] = begin;
                    if (at == run.Source.Start)
                        text.Append(run.Display);
                    for (var i = at + 1; i <= end; i++)
                        SourceToDisplay[i] = text.Length;
                }
                if (text.Length != begin)
                {
                    if (style.ImageIndex == 0 && style.ObjectIndex == 0
                        && (style.Flags & (UiStyledTextFlags.Task | UiStyledTextFlags.Rule)) == 0
                        && displayRuns.Count > 0 && displayRuns[^1].Style == style
                        && displayRuns[^1].Start + displayRuns[^1].Length == begin)
                        displayRuns[^1] = displayRuns[^1] with
                        {
                            Length = text.Length - displayRuns[^1].Start
                        };
                    else
                        displayRuns.Add(new(begin, text.Length - begin, style));
                }
                at = end;
            }
        }
        Text = text.ToString();
        Runs = displayRuns.ToArray();
        Atoms = runs.Where(run => run.Source.Length > 0 && run.Display.Length > 0 && run.Display.Length != run.Source.Length
            && SourceToDisplay[run.Source.End] - SourceToDisplay[run.Source.Start] == run.Display.Length
            && Text.AsSpan(SourceToDisplay[run.Source.Start], run.Display.Length).SequenceEqual(run.Display))
            .Select(run => run.Source).ToArray();
        NativeAtomsCanWrap = Atoms.All(atom => System.Globalization.StringInfo.ParseCombiningCharacters(
            Text.Substring(SourceToDisplay[atom.Start], SourceToDisplay[atom.End] - SourceToDisplay[atom.Start])).Length == 1);
    }
}
