using System.Globalization;

namespace Duxel.Core;

/// <summary>Prepared bounded text, measured and painted with the same typography scale.</summary>
internal readonly record struct UiLimitedTextLayout(UiControlTextBlock Block, float Scale)
{
    internal static UiLimitedTextLayout Measure<TTypeface, TMetrics>(UiControlContext context, string text,
        float width, int maximumLines, float minimumScale)
        where TTypeface : IUiTypographyTypeface where TMetrics : IUiTypographyMetrics
    {
        if (!float.IsFinite(width) || width <= 0 || maximumLines < 1
            || !float.IsFinite(minimumScale) || minimumScale <= 0 || minimumScale > 1)
            throw new ArgumentOutOfRangeException(nameof(width));
        UiControlTextBlock At(float scale)
        {
            using var font = context.BeginTypography<TTypeface, TMetrics>(scale : scale);
            return context.MeasureTextBlock(context.WrapText(text, width));
        }
        var full = At(1);
        if (Fits(full)) return new(full, 1);
        var small = At(minimumScale);
        if (Fits(small))
        {
            var low = minimumScale;
            var high = 1f;
            // Bounded search chooses the largest fitting scale without a frame-time loop.
            for (var i = 0; i < 8; i++)
            {
                var middle = (low + high) * .5f;
                var candidate = At(middle);
                if (Fits(candidate)) { low = middle; small = candidate; }
                else high = middle;
            }
            return new(small, low);
        }
        using var typography = context.BeginTypography<TTypeface, TMetrics>(scale : minimumScale);
        var lines = new UiControlTextLine[Math.Min(maximumLines, small.LineCount)];
        for (var i = 0; i < lines.Length; i++) lines[i] = small.GetLine(i);
        var last = lines[^1].Text;
        var elements = StringInfo.ParseCombiningCharacters(last);
        var left = 0;
        var right = elements.Length;
        while (left < right)
        {
            var middle = (left + right + 1) / 2;
            var end = middle == elements.Length ? last.Length : elements[middle];
            if (context.MeasureText(last[..end] + "…").X <= width) left = middle;
            else right = middle - 1;
        }
        var retainedEnd = left == elements.Length ? last.Length : elements[left];
        var trimmed = last[..retainedEnd] + "…";
        lines[^1] = new(trimmed, context.MeasureText(trimmed) with { Y = typography.LineHeight });
        return new(new(lines, new(lines.Max(line => line.Size.X), lines.Length * typography.LineHeight),
            lines.Sum(line => line.Text.Length)), minimumScale);

        bool Fits(UiControlTextBlock block) => block.LineCount <= maximumLines && block.Size.X <= width;
    }
}
