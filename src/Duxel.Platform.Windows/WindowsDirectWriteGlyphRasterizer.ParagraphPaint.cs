using System.Runtime.InteropServices;
using Duxel.Core;

namespace Duxel.Platform.Windows;

internal sealed unsafe partial class WindowsDirectWriteGlyphRasterizer
{
    internal static PlatformTextParagraphRaster RasterizeStyledParagraphRun(PlatformTextParagraphRun paragraph)
    {
        ArgumentNullException.ThrowIfNull(paragraph);
        lock (Sync)
        {
            if (!TryGetFontFace(paragraph.FontPath, out var face))
                throw new InvalidOperationException("Cannot load the paragraph paint font.");
            var glyphs = paragraph.Glyphs.Span;
            if (glyphs.IsEmpty)
                return new(0, 0, ReadOnlyMemory<byte>.Empty, default);
            var oversample = paragraph.FontSize < 10 ? 2 : 1;
            var indices = new ushort[glyphs.Length];
            var advances = new float[glyphs.Length];
            var offsets = new UiVector2[glyphs.Length];
            for (var index = 0; index < glyphs.Length; index++)
            {
                indices[index] = checked((ushort)glyphs[index].Index);
                advances[index] = glyphs[index].Advance * oversample;
                offsets[index] = new(glyphs[index].AdvanceOffset * oversample, glyphs[index].AscenderOffset * oversample);
            }
            nint analysis = 0;
            fixed (ushort* glyphIndices = indices)
            fixed (float* glyphAdvances = advances)
            fixed (UiVector2* glyphOffsets = offsets)
            {
                var run = new DWriteGlyphRun
                {
                    FontFace = face,
                    FontEmSize = paragraph.FontSize * oversample,
                    GlyphCount = (uint)glyphs.Length,
                    GlyphIndices = (nint)glyphIndices,
                    GlyphAdvances = (nint)glyphAdvances,
                    GlyphOffsets = (nint)glyphOffsets,
                    IsSideways = false,
                    BidiLevel = paragraph.BidiLevel
                };
                try
                {
                    Marshal.ThrowExceptionForHR(CreateGlyphRunAnalysis(ref run, run.FontEmSize, out analysis,
                        paragraph.Origin.X * oversample, paragraph.Origin.Y * oversample));
                    if (!TryExtractAlphaTexture(analysis, out var alpha, out var rgb, out var width, out var height,
                        out var left, out var top, rasterizePixels: true))
                        return new(0, 0, ReadOnlyMemory<byte>.Empty, default);
                    if (oversample > 1)
                    {
                        alpha = Downsample(alpha, width, height, oversample, out width, out height);
                        rgb = null;
                        left = (int)MathF.Floor(left / (float)oversample);
                        top = (int)MathF.Floor(top / (float)oversample);
                    }
                    var pixels = new byte[checked(width * height * 4)];
                    for (var index = 0; index < alpha.Length; index++)
                    {
                        for (var channel = 0; channel < 3; channel++)
                            pixels[index * 4 + channel] = rgb is null ? alpha[index] : rgb[index * 3 + channel];
                        pixels[index * 4 + 3] = alpha[index];
                    }
                    return new(width, height, pixels, new(left, top));
                }
                finally
                {
                    if (analysis != 0)
                        Release(analysis);
                }
            }
        }
    }

    internal static PlatformTextMeshResult CreateStyledParagraphRunMesh(PlatformTextParagraphRun paragraph, float tolerance)
    {
        ArgumentNullException.ThrowIfNull(paragraph);
        if (!float.IsFinite(tolerance) || tolerance <= 0)
            throw new ArgumentOutOfRangeException(nameof(tolerance));
        lock (Sync)
        {
            if (!TryGetFontFace(paragraph.FontPath, out var face))
                throw new InvalidOperationException("Cannot load the paragraph vector font.");
            var meshes = new List<PlatformTextGlyphMesh>();
            nint factory = 0;
            try
            {
                foreach (var placed in paragraph.Glyphs.Span)
                {
                    var index = checked((ushort)placed.Index);
                    var key = new VectorGlyphKey(paragraph.FontPath, index, paragraph.FontSize, tolerance);
                    if (!VectorGlyphs.TryGetValue(key, out var glyph))
                    {
                        if (factory == 0)
                        {
                            var id = new Guid("06152247-6F50-465A-9245-118BFD3B6007");
                            Marshal.ThrowExceptionForHR(D2D1CreateFactory(0, &id, null, &factory));
                        }
                        glyph = CreateVectorGlyph(factory, face, index, paragraph.FontSize, tolerance);
                        if (VectorGlyphs.Count == VectorGlyphCapacity)
                            VectorGlyphs.Remove(VectorGlyphOrder.Dequeue());
                        VectorGlyphs.Add(key, glyph);
                        VectorGlyphOrder.Enqueue(key);
                    }
                    meshes.Add(new(glyph.Points, glyph.Indices, new(placed.X, placed.Y)));
                }
                return new(ReadOnlyMemory<UiVector2>.Empty, 0, 0, 0)
                {
                    Glyphs = meshes.ToArray()
                };
            }
            finally
            {
                if (factory != 0)
                    Release(factory);
            }
        }
    }
}
