namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    private UiStyledTextPalette StyledPalette(UiColor foreground, bool dark)
        => UiStyledTextPalette.Resolve(dark, _theme.ForcedColors, foreground, IsControlEnabled);

    private UiColor StyledTextColor(UiStyledTextStyle style, UiColor foreground, bool dark)
        => StyledPalette(foreground, dark).Text(style);

    private void CaptureStyledTextPaint(string id, UiAccessibilityTextInfo info, UiVisualTextLines visual, UiColor foreground)
    {
        if (_accessibility is not { } tree)
            return;
        var projection = visual.StyledSource!.Projection;
        var dark = new UiControlContext(this).FluentColorScheme == UiFluentColorScheme.Dark;
        var key = new UiAccessibilityStyledPaintKey(LayoutMeasurementKey, projection, tree.FontNameLanguage,
            foreground, dark, _theme.ForcedColors, IsControlEnabled);
        if (tree.RetainStyledTextPaint(id, info.Content, key))
            return;
        var output = new List<UiAccessibilityTextSourcePaintRun>();
        var scalar = 0;
        foreach (var run in projection.Runs)
        {
            using var typography = BeginStyledTypography(run.Style);
            var text = projection.Text.Substring(run.Start, run.Length);
            var objectRun = IsStyledImage(text, run.Style) || IsStyledTask(text, run.Style);
            var backend = objectRun ? null : _platformTextBackend as IPlatformTextFontRunBackend;
            UiAccessibilityTextSize? size = HasDirectTextBackend() ? new(ResolveDirectTextFontSize(_textSettings), _contentScale) : null;
            size?.Validate();
            var fonts = backend?.GetFontRuns(new(_directTextPrimaryFontPath!, _directTextSecondaryFontPath, text,
                ResolveDirectTextFontSize(_textSettings)), key.Language);
            var fontIndex = 0;
            while (scalar < info.Content.CharacterCount)
            {
                var start = info.Content.GetUtf16Offset(scalar);
                var end = info.Content.GetUtf16Offset(scalar + 1);
                var first = projection.SourceToDisplay[start];
                var last = projection.SourceToDisplay[end];
                if (first >= run.Start + run.Length)
                    break;
                scalar++;
                if (first == last || last <= run.Start || objectRun)
                    continue;
                if (first < run.Start || last > run.Start + run.Length)
                    throw new InvalidOperationException("A projected source scalar cannot cross style-run boundaries.");
                var displayed = projection.Text.AsSpan(first, last - first);
                if (displayed.Contains('\n') || displayed.Contains('\r'))
                    continue;
                while (fonts is not null && fontIndex < fonts.Count && fonts[fontIndex].EndUtf16 <= first - run.Start)
                    fontIndex++;
                var font = fonts is not null && fontIndex < fonts.Count
                    && fonts[fontIndex].StartUtf16 <= first - run.Start && fonts[fontIndex].EndUtf16 >= last - run.Start
                        ? fonts[fontIndex].Font : null;
                var paint = new UiAccessibilityTextSourcePaintRun(start, end, StyledTextColor(run.Style, foreground, dark), size, font);
                if (output.Count > 0 && output[^1] is var previous && previous.EndUtf16 == start
                    && previous.Foreground == paint.Foreground && previous.Size == paint.Size && previous.Font == paint.Font)
                    output[^1] = previous with { EndUtf16 = end };
                else
                    output.Add(paint);
            }
        }
        tree.PublishStyledTextPaint(id, new(info.Content, output), key);
    }
}
