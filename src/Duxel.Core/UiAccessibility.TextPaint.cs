namespace Duxel.Core;

/// <summary>Original UTF-16 source interval with known projected text paint; gaps remain unknown.</summary>
public readonly record struct UiAccessibilityTextSourcePaintRun(int StartUtf16, int EndUtf16,
    UiColor Foreground, UiAccessibilityTextSize? Size, PlatformFontMetadata? Font);

public readonly record struct UiAccessibilityTextPaintRun(int Start, int End, UiColor Foreground, UiAccessibilityTextSize? Size);

public sealed partial record UiAccessibilityTextAppearance
{
    private readonly UiAccessibilityTextPaintRun[]? _paint;
    public bool HasUniformPaint => _paint is null;
    public int PaintRunCount => _paint?.Length ?? 0;
    public UiAccessibilityTextPaintRun GetPaintRun(int index) => _paint is not null && (uint)index < (uint)_paint.Length
        ? _paint[index] : throw new ArgumentOutOfRangeException(nameof(index));

    public UiAccessibilityTextAppearance(UiAccessibilityTextContent content, IReadOnlyList<UiAccessibilityTextSourcePaintRun> runs)
        : this(content, default, FontRuns(runs))
    {
        _paint = new UiAccessibilityTextPaintRun[runs.Count];
        var end = 0;
        for (var index = 0; index < runs.Count; index++)
        {
            var run = runs[index];
            if (run.StartUtf16 < end || run.EndUtf16 <= run.StartUtf16 || run.EndUtf16 > content.Text.Length)
                throw new ArgumentException("Paint runs must be ordered nonoverlapping source intervals.", nameof(runs));
            run.Size?.Validate();
            _paint[index] = new(content.GetCharacterOffset(run.StartUtf16), content.GetCharacterOffset(run.EndUtf16), run.Foreground, run.Size);
            end = run.EndUtf16;
        }
    }

    private static PlatformTextFontRun[] FontRuns(IReadOnlyList<UiAccessibilityTextSourcePaintRun> runs)
    {
        ArgumentNullException.ThrowIfNull(runs);
        return runs.Where(run => run.Font is not null).Select(run => new PlatformTextFontRun(run.StartUtf16, run.EndUtf16, run.Font!)).ToArray();
    }

    public (int Start, int End, UiColor? Foreground, UiAccessibilityTextSize? Size) GetPaintRangeAt(int offset)
    {
        if ((uint)offset > (uint)Content.CharacterCount)
            throw new ArgumentOutOfRangeException(nameof(offset));
        if (_paint is null)
            return (0, Content.CharacterCount, _foreground, Size);
        var lookup = offset == Content.CharacterCount && offset > 0 ? offset - 1 : offset;
        var low = 0;
        var high = _paint.Length;
        while (low < high)
        {
            var middle = low + (high - low) / 2;
            if (_paint[middle].Start <= lookup)
                low = middle + 1;
            else
                high = middle;
        }
        if (low > 0 && lookup < _paint[low - 1].End)
        {
            var run = _paint[low - 1];
            return (run.Start, run.End, run.Foreground, run.Size);
        }
        return (low > 0 ? _paint[low - 1].End : 0, low < _paint.Length ? _paint[low].Start : Content.CharacterCount, null, null);
    }
}

internal readonly record struct UiAccessibilityStyledPaintKey(UiLayoutMeasurementKey Measurement,
    UiStyledTextProjection Projection, string Language, UiColor Foreground, bool Dark,
    UiForcedColorPalette? ForcedColors, bool Enabled);

public sealed partial class UiAccessibilityTree
{
    internal bool RetainStyledTextPaint(string id, UiAccessibilityTextContent content, UiAccessibilityStyledPaintKey key)
    {
        if (!_building)
            throw new InvalidOperationException("Styled paint capture requires an active accessibility frame.");
        var entry = GetEntry(id);
        if (entry.TextAppearance is not { HasUniformPaint: false } previous
            || !ReferenceEquals(previous.Content, content) || entry.StyledPaintKey != key)
            return false;
        PublishStyledTextPaint(id, previous, key);
        return true;
    }

    internal void PublishStyledTextPaint(string id, UiAccessibilityTextAppearance appearance, UiAccessibilityStyledPaintKey key)
    {
        if (!_building || appearance.HasUniformPaint)
            throw new InvalidOperationException("Styled paint publication requires an active frame and source paint ranges.");
        var entry = GetEntry(id);
        entry.TextAppearance = appearance;
        entry.StyledPaintKey = key;
        entry.TextFontKey = null;
        entry.TextPaintGeneration = _generation;
        if (entry.Seen == _generation && ReferenceEquals(entry.Staged.TextInfo?.Content, appearance.Content))
            entry.Staged = entry.Staged with { TextAppearance = appearance };
    }
}
