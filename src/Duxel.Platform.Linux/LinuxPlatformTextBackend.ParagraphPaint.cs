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
            var paint = MeasureGlyphPaint(font, glyphs);
            if (paint.Glyphs.Length == 0)
                return new(0, 0, ReadOnlyMemory<byte>.Empty, default);
            var left = Math.Floor(paint.Left) - 1;
            var top = Math.Floor(paint.Top) - 1;
            var width = checked((int)(Math.Ceiling(paint.Right) + 1 - left));
            var height = checked((int)(Math.Ceiling(paint.Bottom) + 1 - top));
            var alpha = Rasterize([new(font, paint.Glyphs)], width, height, left, top);
            var pixels = new byte[checked(alpha.Length * 4)];
            for (var index = 0; index < alpha.Length; index++)
                pixels.AsSpan(index * 4, 4).Fill(alpha[index]);
            return new(width, height, pixels, new((float)left, (float)top));
        }
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
