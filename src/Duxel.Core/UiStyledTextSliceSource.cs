namespace Duxel.Core;

/// <summary>An immutable rendered source interval; resources stay owned by the complete source.</summary>
internal sealed class UiStyledTextSliceSource : UiStyledTextSource
{
    private readonly string _source;
    private readonly UiStyledSourceRun[] _runs;
    private readonly IReadOnlyList<UiStyledTextImage> _images;
    private readonly IReadOnlyList<UiStyledTextObject> _objects;
    private readonly UiSourceTextRange? _active;
    internal override long Revision => 0;
    internal override string Source => _source;
    internal override IReadOnlyList<UiStyledSourceRun> Runs => _runs;
    internal override IReadOnlyList<UiStyledTextImage> Images => _images;
    internal override IReadOnlyList<UiStyledTextObject> Objects => _objects;
    internal override UiSourceTextRange? ActiveBlock => _active;
    internal override UiStyledTextSource Composition => throw new NotSupportedException("A rendered source slice cannot own an edit composition.");

    internal UiStyledTextSliceSource(UiStyledTextSource parent, UiSourceTextRange range)
    {
        _source = parent.Source.Substring(range.Start, range.Length);
        _images = parent.Images;
        _objects = parent.Objects;
        if (parent.ActiveBlock is { } active && active.Start < range.End && active.End > range.Start)
        {
            var start = Math.Max(active.Start, range.Start);
            _active = new(start - range.Start, Math.Min(active.End, range.End) - start);
        }
        var runs = parent.Runs;
        var low = 0;
        var high = runs.Count;
        while (low < high)
        {
            var middle = low + (high - low) / 2;
            if (runs[middle].Source.End <= range.Start)
                low = middle + 1;
            else
                high = middle;
        }
        var selected = new List<UiStyledSourceRun>();
        for (var index = low; index < runs.Count && runs[index].Source.Start < range.End; index++)
        {
            var run = runs[index];
            var start = Math.Max(range.Start, run.Source.Start);
            var end = Math.Min(range.End, run.Source.End);
            var display = run.Display;
            if (display.Length == run.Source.Length)
                display = display.Substring(start - run.Source.Start, end - start);
            else if (display.Length != 0 && (start != run.Source.Start || end != run.Source.End))
                throw new InvalidOperationException("A rendered source slice cannot split an atomic replacement.");
            selected.Add(new(new(start - range.Start, end - start), display, run.Style, run.Syntax));
        }
        _runs = selected.ToArray();
    }

    internal override void Prepare(string source, int activeIndex, bool preserveActiveBlock = false)
    {
        if (source != _source || activeIndex >= 0 || preserveActiveBlock)
            throw new InvalidOperationException("A rendered source slice must retain its immutable source and captured display mode.");
    }
}
