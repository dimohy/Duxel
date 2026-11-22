using System.Runtime.InteropServices;

namespace Duxel.Interop.Windows;

internal static unsafe partial class DirectWriteLongTextShaper
{
    internal readonly record struct FontSpan(int Start, int Length, nint Face, float Size, int PaintStyle = 0);
    internal readonly record struct RasterRun(DirectWriteTextShaper.Glyph[] Glyphs, uint BidiLevel, float OriginX, float OriginY);
    internal readonly record struct ParagraphRun(int FontSpan, Placed[] Glyphs, RasterRun Raster, int PaintStyle = 0);
    internal readonly record struct InlineSpan(int Start, float Width, float Height, float Baseline);
    internal readonly record struct PlacedInline(int Start, float X, float Y, float Width, float Height, bool RightToLeft);
    internal readonly record struct Paragraph(ParagraphRun[] Runs, float Advance, float Baseline, PlacedInline[] Objects);
    [StructLayout(LayoutKind.Sequential)]
    private struct TextMetrics
    {
        internal float Left, Top, Width, WidthIncludingTrailingWhitespace, Height, LayoutWidth, LayoutHeight;
        internal uint MaxBidiDepth, LineCount;
    }

    internal static Paragraph ShapeParagraph(nint factory, string text, FontSpan[] spans, bool rtl = false,
        Action<nint>? inspectLayout = null, float? wrapWidth = null, InlineSpan[]? objects = null,
        Dictionary<nint, ExplicitFontScope>? sharedFonts = null)
    {
        if (spans.Length == 0)
            throw new ArgumentException("A paragraph requires a declared explicit font.");
        if (wrapWidth is { } width && (!float.IsFinite(width) || width < 0))
            throw new ArgumentOutOfRangeException(nameof(wrapWidth));
        var covered = 0;
        foreach (var span in spans)
        {
            if (span.Start != covered || span.Length < 0 || span.Length > text.Length - covered || span.Face == 0
                || !float.IsFinite(span.Size) || span.Size <= 0 || span.PaintStyle < 0)
                throw new ArgumentException("Explicit font intervals must cover the original paragraph exactly.");
            covered += span.Length;
        }
        if (covered != text.Length)
            throw new ArgumentException("Explicit font intervals omit paragraph source text.");
        var fonts = sharedFonts ?? new Dictionary<nint, ExplicitFontScope>();
        var effects = new Dictionary<int, nint>();
        var inlineObjects = new Dictionary<nint, InlineSpan>();
        nint format = 0, layout = 0;
        Callback* renderer = null;
        var defaultSpan = spans[0];
        try
        {
            foreach (var span in spans)
                if (!fonts.ContainsKey(span.Face))
                    fonts.Add(span.Face, new(factory, span.Face));
            format = fonts[defaultSpan.Face].CreateFormat(defaultSpan.Size, rtl, wrapWidth.HasValue);
            fixed (char* source = text)
                Check(((delegate* unmanaged[Stdcall]<nint, char*, uint, nint, float, float, nint*, int>)Method(factory, 18))
                    (factory, source, (uint)text.Length, format, wrapWidth ?? 4096, float.MaxValue, &layout), "CreateTextLayout");
            // The layout starts with the format's font on every range. Only
            // typography overrides need setters; paint boundaries are applied below.
            foreach (var span in spans)
                if (span.Face != defaultSpan.Face || span.Size != defaultSpan.Size)
                    fonts[span.Face].Apply(layout, span);
            if (spans.Any(span => span.PaintStyle != 0))
                foreach (var span in spans)
                {
                    if (!effects.TryGetValue(span.PaintStyle, out var effect))
                    {
                        effect = (nint)CreateCallback(3, new PaintState());
                        effects.Add(span.PaintStyle, effect);
                    }
                    var range = new TextRange((uint)span.Start, (uint)span.Length);
                    Check(((delegate* unmanaged[Stdcall]<nint, nint, TextRange, int>)Method(layout, 38))
                        (layout, effect, range), "SetDrawingEffect");
                }
            foreach (var item in objects ?? [])
            {
                var callback = CreateCallback(4, new InlineState(item));
                inlineObjects.Add((nint)callback, item);
                Check(((delegate* unmanaged[Stdcall]<nint, Callback*, TextRange, int>)Method(layout, 39))
                    (layout, callback, new((uint)item.Start, 1)), "SetInlineObject");
            }
            var capture = new CaptureState(fonts[spans[0].Face].Identity, (uint)text.Length)
            {
                Fonts = spans.Select(span => new FontBinding(span.Start, span.Length, fonts[span.Face].Identity, span.Size)).ToArray(),
                PaintStyles = effects.ToDictionary(pair => pair.Value, pair => pair.Key),
                InlineObjects = inlineObjects
            };
            renderer = CreateCallback(2, capture);
            var status = ((delegate* unmanaged[Stdcall]<nint, nint, Callback*, float, float, int>)Method(layout, 58))
                (layout, 0, renderer, 0, 0);
            capture.Failure?.Throw();
            Check(status, "Draw");
            TextMetrics metrics = default;
            Check(((delegate* unmanaged[Stdcall]<nint, TextMetrics*, int>)Method(layout, 60))(layout, &metrics), "GetMetrics");
            inspectLayout?.Invoke(layout);
            var baseline = capture.Runs.Count == 0 ? 0 : capture.Runs.Min(run => run.Y);
            var runs = new List<ParagraphRun>();
            foreach (var run in capture.Runs.OrderBy(run => run.Y).ThenBy(run => run.Left))
            {
                // Preserve native absolute positions. Reconstructing them through local offsets loses precision on long rows.
                runs.Add(new(run.FontSpan, run.Glyphs, run.Raster with
                {
                    OriginY = run.Raster.OriginY - baseline
                }, run.PaintStyle));
            }
            return new(runs.ToArray(), metrics.WidthIncludingTrailingWhitespace, baseline, capture.ObjectPlacements.OrderBy(item => item.Start).ToArray());
        }
        finally
        {
            Release(ref layout);
            Release(ref format);
            var references = renderer == null ? 0 : ReleaseOwned(renderer);
            var retainedEffects = 0u;
            foreach (var effect in effects.Values)
                retainedEffects |= ReleaseOwned((Callback*)effect);
            var retainedObjects = 0u;
            foreach (var item in inlineObjects.Keys)
                retainedObjects |= ReleaseOwned((Callback*)item);
            if (sharedFonts is null)
                foreach (var font in fonts.Values)
                    font.Dispose();
            if (references != 0)
                throw new InvalidOperationException("DirectWrite retained a paragraph renderer after layout disposal.");
            if (retainedEffects != 0)
                throw new InvalidOperationException("DirectWrite retained a paint-style marker after layout disposal.");
            if (retainedObjects != 0)
                throw new InvalidOperationException("DirectWrite retained an inline object after layout disposal.");
        }
    }
}
