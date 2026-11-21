using Duxel.Core;
using static Duxel.Platform.Linux.LinuxFontNative;

namespace Duxel.Platform.Linux;

public sealed unsafe partial class LinuxPlatformTextBackend
{
    public PlatformTextParagraphRaster RasterizeParagraphRun(PlatformTextParagraphRun run)
    {
        ArgumentNullException.ThrowIfNull(run);
        lock (_sync)
        {
            var font = Font(run.FontPath, run.FontSize);
            var glyphs = run.Glyphs.ToArray().Select(glyph => new Glyph(glyph.Index, glyph.X, glyph.Y)).ToArray();
            if (run.RasterContext is { } rasterContext)
            {
                var x = (int)MathF.Floor(run.Origin.X) + rasterContext.LeftFromOrigin;
                var y = (int)MathF.Floor(run.Origin.Y) + rasterContext.TopFromOrigin;
                var coverage = RasterizeSurface(rasterContext.Width, rasterContext.Height, x, y, context =>
                {
                    cairo_set_scaled_font(context, font.Scaled);
                    fixed (Glyph* pointer = glyphs)
                        cairo_show_glyphs(context, pointer, glyphs.Length);
                }, (data, stride, width, height) =>
                    CropParagraphRun(new ReadOnlySpan<byte>(data, checked(height * stride)), stride, width, height, x, y),
                    rasterContext.PreviousInk);
                return coverage.ToRgba();
            }

            var paint = MeasureGlyphPaint(font, glyphs);
            if (paint.Glyphs.Length == 0)
                return new(0, 0, ReadOnlyMemory<byte>.Empty, default);
            var left = Math.Floor(paint.Left) - 1;
            var top = Math.Floor(paint.Top) - 1;
            var width = checked((int)(Math.Ceiling(paint.Right) + 1 - left));
            var height = checked((int)(Math.Ceiling(paint.Bottom) + 1 - top));
            var alpha = Rasterize([new(font, paint.Glyphs)], width, height, left, top);
            return new PlatformTextParagraphCoverage(width, height, alpha, new((float)left, (float)top)).ToRgba();
        }
    }

    private static PlatformTextParagraphCoverage CropParagraphRun(ReadOnlySpan<byte> alpha, int stride, int width, int height, int originX, int originY)
    {
        var left = width;
        var top = height;
        var right = 0;
        var bottom = 0;
        for (var y = 0; y < height; y++)
        {
            var row = alpha.Slice(y * stride, width);
            var first = row.IndexOfAnyExcept((byte)0);
            if (first < 0)
                continue;
            left = Math.Min(left, first);
            top = Math.Min(top, y);
            right = Math.Max(right, row.LastIndexOfAnyExcept((byte)0) + 1);
            bottom = y + 1;
        }
        if (right == 0)
            return default;
        left = Math.Max(0, left - 1);
        top = Math.Max(0, top - 1);
        right = Math.Min(width, right + 1);
        bottom = Math.Min(height, bottom + 1);
        var cropWidth = right - left;
        var cropHeight = bottom - top;
        var cropped = new byte[checked(cropWidth * cropHeight)];
        for (var y = 0; y < cropHeight; y++)
            alpha.Slice((y + top) * stride + left, cropWidth).CopyTo(cropped.AsSpan(y * cropWidth, cropWidth));
        return new(cropWidth, cropHeight, cropped, new(originX + left, originY + top));
    }

    public PlatformTextMeshResult CreateParagraphRunMesh(PlatformTextParagraphRun run, float flatteningTolerance = .001f)
    {
        ArgumentNullException.ThrowIfNull(run);
        if (!float.IsFinite(flatteningTolerance) || flatteningTolerance <= 0)
            throw new ArgumentOutOfRangeException(nameof(flatteningTolerance));
        lock (_sync)
        {
            var font = Font(run.FontPath, run.FontSize);
            var glyphs = new List<PlatformTextGlyphMesh>();
            foreach (var glyph in run.Glyphs.Span)
            {
                var mesh = GetVectorGlyph(font, glyph.Index, flatteningTolerance);
                if (mesh.Indices.Length != 0)
                    glyphs.Add(new(mesh.Vertices, mesh.Indices, new(glyph.X, glyph.Y)));
            }
            return new(ReadOnlyMemory<UiVector2>.Empty, 0, 0, 0)
            {
                Glyphs = glyphs.ToArray()
            };
        }
    }
}
