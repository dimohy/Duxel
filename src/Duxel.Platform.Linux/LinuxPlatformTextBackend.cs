using System.Text;
using Duxel.Core;
using static Duxel.Platform.Linux.LinuxFontNative;
using static Duxel.Platform.Linux.LinuxFontResource;

namespace Duxel.Platform.Linux;

/// <summary>Explicit-file FreeType/HarfBuzz/Cairo text. Instances own a bounded native font cache.</summary>
public sealed unsafe partial class LinuxPlatformTextBackend : IPlatformTextBackend, IPlatformTextLayoutBackend,
    IPlatformFontMetricsBackend, IPlatformGlyphBitmapRasterizer, IPlatformVectorTextBackend, IPlatformFontVariantBackend,
    IPlatformTextFontRunBackend, IPlatformTextGeometryBackend, IPlatformTextParagraphBackend, IDisposable
{
    private const int FontCapacity = 32;
    private static readonly UnicodeEncoding StrictUtf16 = new(false, false, true);
    private static readonly LinuxPlatformTextBackend AtlasRasterizer = new(false);
    private readonly object _sync = new();
    private readonly Dictionary<(string Path, float Size, bool Oblique), LinuxFontResource> _fonts = [];
    private readonly Queue<(string Path, float Size, bool Oblique)> _order = new();
    private bool _disposed;
    private int _fontLeaseDepth;

    public LinuxPlatformTextBackend() : this(true) { }

    private LinuxPlatformTextBackend(bool registerAtlas)
    {
        if (!OperatingSystem.IsLinux() || !Environment.Is64BitProcess)
            throw new PlatformNotSupportedException("The font backend requires 64-bit Linux.");
        if (LinuxParagraphNative.pango_version_check(1, 50, 0) != 0)
            throw new NotSupportedException("The text backend requires Pango1.50 or later for complete native paragraph snapshots.");
        if (registerAtlas)
            UiFontAtlasBuilder.ConfigurePlatformGlyphRasterizerFactory(static () => AtlasRasterizer);
    }

    public string CacheKeyTag => "linux-pango-harfbuzz-cairo-gray-v2";
    public ulong TextLayoutVersion => 0;

    private LinuxFontResource Font(string path, float size, LinuxFontResource? retained = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!float.IsFinite(size) || size < 1f / 64 || size > int.MaxValue / 64d)
            throw new ArgumentOutOfRangeException(nameof(size));
        var resource = ResolveResource(path);
        var key = (resource.Path, size, resource.Oblique);
        if (_fonts.TryGetValue(key, out var font))
            return font;
        if (!File.Exists(key.Item1))
            throw new FileNotFoundException("The requested font file does not exist.", key.Item1);
        font = new(key.Item1, size, resource.Oblique);
        if (_fonts.Count >= FontCapacity && _fontLeaseDepth == 0)
        {
            var oldest = _order.Dequeue();
            if (_fonts[oldest] == retained)
            {
                _order.Enqueue(oldest);
                oldest = _order.Dequeue();
            }
            _fonts.Remove(oldest, out var retired);
            retired!.Dispose();
        }
        _fonts.Add(key, font);
        _order.Enqueue(key);
        return font;
    }

    public bool TryGetFontMetrics(string fontPath, float fontSize, out PlatformFontMetrics metrics)
    {
        lock (_sync)
        {
            var value = Font(fontPath, fontSize).Metrics;
            // Cairo and the shared contract both report descent as a distance below the baseline.
            metrics = new((float)value.Ascent, (float)value.Descent, (float)(value.Height - value.Ascent - value.Descent));
            return true;
        }
    }

    public bool TryMeasure(in PlatformTextMeasureRequest request, out PlatformTextMeasureResult result)
    {
        TryLayout(request, out var layout);
        result = new(layout.Width, layout.Height, layout.Baseline);
        return true;
    }

    public bool TryLayout(in PlatformTextMeasureRequest request, out PlatformTextLayoutResult result)
    {
        lock (_sync)
        {
            var run = Layout(request);
            result = run.Metrics;
            return true;
        }
    }

    public bool TryRasterize(in PlatformTextRasterizeRequest request, out PlatformTextRasterizeResult result)
    {
        lock (_sync)
        {
            var run = Layout(new(request.FontPath, request.SecondaryFontPath, request.Text, request.FontSize));
            var metrics = run.Metrics;
            var alpha = Rasterize(run.Runs, metrics.Width, metrics.Height, metrics.OriginX, -metrics.Baseline);
            var rgba = new byte[checked(alpha.Length * 4)];
            for (var i = 0; i < alpha.Length; i++)
                rgba.AsSpan(i * 4, 4).Fill(alpha[i]);
            result = new(metrics.Width, metrics.Height, rgba, metrics.Baseline, metrics.FontAscent, metrics.Advance)
            {
                OriginX = metrics.OriginX
            };
            return true;
        }
    }

    private sealed record Run(LinuxFontResource Font, Glyph[] Glyphs);
    private sealed record TextLayout(List<Run> Runs, PlatformTextLayoutResult Metrics, float LogicalHeight);

    private TextLayout Layout(in PlatformTextMeasureRequest request)
    {
        ArgumentNullException.ThrowIfNull(request.Text);
        _ = StrictUtf16.GetByteCount(request.Text);
        var primary = Font(request.FontPath, request.FontSize);
        var secondary = request.SecondaryFontPath is { } second ? Font(second, request.FontSize, primary) : null;
        var lineHeight = Math.Max(primary.Metrics.Height, secondary?.Metrics.Height ?? 0);
        var ascent = Math.Max(primary.Metrics.Ascent, secondary?.Metrics.Ascent ?? 0);
        var runs = new List<Run>();
        double y = 0, advance = 0;
        foreach (var line in request.Text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n'))
        {
            var text = line.Replace("\t", "    ", StringComparison.Ordinal);
            runs.AddRange(ShapeParagraph(text, primary, secondary, y, out var lineAdvance));
            advance = Math.Max(advance, lineAdvance);
            y += lineHeight;
        }
        double left = 0, top = 0, right = 0, bottom = 0;
        var ink = false;
        var paintRuns = new List<Run>();
        foreach (var run in runs)
        {
            var paint = MeasureGlyphPaint(run.Font, run.Glyphs);
            if (paint.Glyphs.Length == 0)
                continue;
            paintRuns.Add(new(run.Font, paint.Glyphs));
            ink = true;
            left = Math.Min(left, paint.Left);
            top = Math.Min(top, paint.Top);
            right = Math.Max(right, paint.Right);
            bottom = Math.Max(bottom, paint.Bottom);
        }
        // GRAY coverage can occupy the neighboring device pixel beyond nominal
        // outline metrics at fractional glyph positions. Keep one device sample
        // on every ink edge; this never changes logical advances or empty text.
        const double coverageSample = 1;
        var originX = ink ? (float)(Math.Floor(left) - coverageSample) : 0;
        var originY = ink ? (float)(Math.Floor(top) - coverageSample) : 0;
        var width = ink ? checked((int)(Math.Ceiling(right) + coverageSample - originX)) : 0;
        var height = ink ? checked((int)(Math.Ceiling(bottom) + coverageSample - originY)) : 0;
        return new(paintRuns, new(width, height, -originY, (float)ascent, (float)advance)
        {
            OriginX = originX
        }, (float)y);
    }

    private readonly record struct GlyphPaintBounds(Glyph[] Glyphs, double Left, double Top, double Right, double Bottom);

    private static GlyphPaintBounds MeasureGlyphPaint(LinuxFontResource font, ReadOnlySpan<Glyph> glyphs)
    {
        var visible = new List<Glyph>();
        double left = double.PositiveInfinity, top = double.PositiveInfinity;
        double right = double.NegativeInfinity, bottom = double.NegativeInfinity;
        foreach (var placed in glyphs)
        {
            var glyph = new Glyph(placed.Index);
            cairo_scaled_font_glyph_extents(font.Scaled, &glyph, 1, out var bounds);
            if (bounds.Width <= 0 || bounds.Height <= 0)
                continue;
            // Inkless leading glyphs outside an ink-only target affect Cairo's cropped glyph coverage.
            // Keep them in source/layout snapshots, but never submit them to the raster paint call.
            visible.Add(placed);
            left = Math.Min(left, placed.X + bounds.XBearing);
            top = Math.Min(top, placed.Y + bounds.YBearing);
            right = Math.Max(right, placed.X + bounds.XBearing + bounds.Width);
            bottom = Math.Max(bottom, placed.Y + bounds.YBearing + bounds.Height);
        }
        CheckCairo(cairo_scaled_font_status(font.Scaled), "measure glyph paint");
        return new(visible.ToArray(), left, top, right, bottom);
    }

    private static byte[] Rasterize(List<Run> runs, int width, int height, double left, double top)
        => RasterizeSurface(width, height, left, top, context =>
        {
            foreach (var run in runs)
            {
                cairo_set_scaled_font(context, run.Font.Scaled);
                fixed (Glyph* glyphs = run.Glyphs)
                    cairo_show_glyphs(context, glyphs, run.Glyphs.Length);
            }
        });

    private static byte[] RasterizeSurface(int width, int height, double left, double top, Action<nint> paint)
    {
        if (width == 0 || height == 0)
            return [];
        var alpha = new byte[checked(width * height)];
        var surface = cairo_image_surface_create(2, width, height); // A8, native stride is not necessarily width.
        nint context = 0;
        try
        {
            CheckCairo(cairo_surface_status(surface), "allocate alpha surface");
            context = cairo_create(surface);
            cairo_translate(context, -left, -top);
            cairo_set_source_rgba(context, 1, 1, 1, 1);
            paint(context);
            CheckCairo(cairo_status(context), "render glyphs");
            cairo_surface_flush(surface);
            CheckCairo(cairo_surface_status(surface), "flush alpha surface");
            var data = cairo_image_surface_get_data(surface);
            var stride = cairo_image_surface_get_stride(surface);
            for (var row = 0; row < height; row++)
                new ReadOnlySpan<byte>(data + row * stride, width).CopyTo(alpha.AsSpan(row * width, width));
            return alpha;
        }
        finally
        {
            if (context != 0)
                cairo_destroy(context);
            cairo_surface_destroy(surface);
        }
    }

    public bool TryRasterize(string fontPath, int codepoint, int glyphIndex, int fontSize, int oversample,
        float sourceScale, float renderScale, out GlyphRasterizationResult result)
    {
        if (!Rune.IsValid(codepoint) || glyphIndex <= 0 || oversample is < 1 or > 8
            || !float.IsFinite(sourceScale) || sourceScale <= 0 || !float.IsFinite(renderScale) || renderScale <= 0)
            throw new ArgumentException("Glyph rasterization arguments must be valid and finite; oversample must be 1..8.");
        lock (_sync)
        {
            var font = Font(fontPath, fontSize * sourceScale * oversample);
            if (FT_Get_Char_Index(font.Face, (nuint)codepoint) != (uint)glyphIndex)
                throw new ArgumentException("The requested glyph index does not match the explicit font/codepoint.");
            var glyph = new Glyph((nuint)glyphIndex);
            cairo_scaled_font_glyph_extents(font.Scaled, &glyph, 1, out var bounds);
            var left = Math.Floor(bounds.XBearing / oversample);
            var top = Math.Floor(bounds.YBearing / oversample);
            var width = bounds.Width > 0 ? checked((int)(Math.Ceiling((bounds.XBearing + bounds.Width) / oversample) - left)) : 0;
            var height = bounds.Height > 0 ? checked((int)(Math.Ceiling((bounds.YBearing + bounds.Height) / oversample) - top)) : 0;
            var alpha = Rasterize([new(font, [glyph])], checked(width * oversample), checked(height * oversample), left * oversample, top * oversample);
            if (oversample > 1 && alpha.Length > 0)
            {
                var reduced = new byte[checked(width * height)];
                for (var y = 0; y < height; y++)
                {
                    for (var x = 0; x < width; x++)
                    {
                        var sum = 0;
                        for (var dy = 0; dy < oversample; dy++)
                            for (var dx = 0; dx < oversample; dx++)
                                sum += alpha[((y * oversample + dy) * width + x) * oversample + dx];
                        reduced[y * width + x] = (byte)((sum + oversample * oversample / 2) / (oversample * oversample));
                    }
                }
                alpha = reduced;
            }
            CheckCairo(cairo_scaled_font_status(font.Scaled), "measure atlas glyph");
            result = new(width, height, alpha, true, (float)left, (float)top, (float)bounds.XAdvance / oversample);
            return true;
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
                return;
            _disposed = true;
            foreach (var fonts in _paragraphFonts.Values)
                fonts.Dispose();
            _paragraphFonts.Clear();
            _paragraphOrder.Clear();
            foreach (var font in _fonts.Values)
                font.Dispose();
            _fonts.Clear();
            _order.Clear();
            _vectorGlyphs.Clear();
            _vectorOrder.Clear();
            if (_drawFunctions != 0)
                hb_draw_funcs_destroy(_drawFunctions);
            _drawFunctions = 0;
        }
    }
}
