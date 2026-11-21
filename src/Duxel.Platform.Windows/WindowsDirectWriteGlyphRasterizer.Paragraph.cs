using Duxel.Interop.Windows;

namespace Duxel.Platform.Windows;

internal sealed partial class WindowsDirectWriteGlyphRasterizer
{
    internal readonly record struct ContextualFontRun(string FontPath, DirectWriteLongTextShaper.Placed[] Glyphs,
        DirectWriteLongTextShaper.RasterRun Raster = default);
    internal readonly record struct ContextualParagraph(ContextualFontRun[] Runs, float Advance, float Baseline);

    internal static ContextualParagraph ShapeContextualParagraph(string primary, string? secondary, string text, float size,
        Action<nint>? inspectLayout = null, List<WindowsPlatformTextBackend.FontRun>? selectedFontRuns = null)
    {
        lock (Sync)
        {
            var prepared = DirectWriteHangulInput.Prepare(text).Text;
            var selected = selectedFontRuns is not null && string.Equals(text, prepared, StringComparison.Ordinal)
                ? selectedFontRuns
                : WindowsPlatformTextBackend.BuildFontRuns(primary, secondary, prepared);
            text = prepared;
            if (selected.Count == 0)
                throw new ArgumentException("A contextual paragraph must contain source text.");
            var spans = new DirectWriteLongTextShaper.FontSpan[selected.Count];
            var start = 0;
            for (var index = 0; index < selected.Count; index++)
            {
                var run = selected[index];
                if (!TryGetFontFace(run.FontPath, out var face))
                    throw new InvalidOperationException("Cannot load the explicit contextual font.");
                spans[index] = new(start, run.Text.Length, face, size);
                start += run.Text.Length;
            }
            var paragraph = DirectWriteLongTextShaper.ShapeParagraph(_factory, text, spans, inspectLayout: inspectLayout);
            return new(paragraph.Runs.Select(run => new ContextualFontRun(selected[run.FontSpan].FontPath, run.Glyphs, run.Raster)).ToArray(),
                paragraph.Advance, paragraph.Baseline);
        }
    }

    internal static DirectWriteLongTextShaper.LineGeometry CreateContextualLineGeometry(string primary, string? secondary,
        string text, float size)
    {
        if (text.Length == 0)
            return new([0], [], 0);
        lock (Sync)
            return DirectWriteLongTextShaper.Geometry(text,
                (prepared, inspect) => ShapeContextualParagraph(primary, secondary, prepared, size, inspect).Advance);
    }
}
