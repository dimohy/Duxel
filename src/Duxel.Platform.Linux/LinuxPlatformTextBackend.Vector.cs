using Duxel.Core;
using LibTessDotNet;
using static Duxel.Platform.Linux.LinuxFontNative;

namespace Duxel.Platform.Linux;

public sealed unsafe partial class LinuxPlatformTextBackend
{
    internal const float ObliqueSlant = 0.21255656f; // tan(12 degrees)
    private const string ObliquePrefix = "duxel-linux-oblique:";
    private const int VectorGlyphCapacity = 512;
    private readonly record struct VectorKey(string Path, float Size, bool Oblique, uint Glyph, float Tolerance);
    private readonly record struct VectorGlyph(UiVector2[] Vertices, uint[] Indices, UiVectorCurve[] Curves);
    private readonly Dictionary<VectorKey, VectorGlyph> _vectorGlyphs = [];
    private readonly Queue<VectorKey> _vectorOrder = new();
    private nint _drawFunctions;

    private static (string Path, bool Oblique) ResolveResource(string resource)
    {
        var oblique = resource.StartsWith(ObliquePrefix, StringComparison.Ordinal);
        var path = oblique ? Uri.UnescapeDataString(resource[ObliquePrefix.Length..]) : resource;
        if (oblique && hb_version_atleast(3, 3, 0) == 0)
            throw new NotSupportedException("Oblique font simulation requires HarfBuzz 3.3 or later.");
        return (Path.GetFullPath(path), oblique);
    }

    public string GetFontVariantResource(string fontResource, PlatformFontSimulation simulation)
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (simulation is not (PlatformFontSimulation.None or PlatformFontSimulation.Oblique))
                throw new ArgumentOutOfRangeException(nameof(simulation));
            var resource = ResolveResource(fontResource);
            if (!File.Exists(resource.Path))
                throw new FileNotFoundException("The requested font file does not exist.", resource.Path);
            return simulation == PlatformFontSimulation.None ? fontResource
                : ObliquePrefix + Uri.EscapeDataString(resource.Path);
        }
    }

    public PlatformTextMeshResult CreateTextMesh(in PlatformTextMeshRequest request)
    {
        if (!float.IsFinite(request.FlatteningTolerance) || request.FlatteningTolerance <= 0)
            throw new ArgumentOutOfRangeException(nameof(request));
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_drawFunctions == 0)
                _drawFunctions = LinuxGlyphOutline.CreateFunctions();
            if (request.UseProjectedLineMetrics)
                return CreateProjectedTextMesh(request);

            var layout = Layout(new(request.FontPath, request.SecondaryFontPath, request.Text, request.FontSize)
            {
                RightToLeft = request.RightToLeft
            });
            var glyphs = new List<PlatformTextGlyphMesh>();
            AppendVectorGlyphs(layout.Runs, request, layout.Metrics.FontAscent, glyphs);
            return new(ReadOnlyMemory<UiVector2>.Empty, layout.Metrics.Advance, layout.LogicalHeight, layout.Metrics.FontAscent)
            {
                Glyphs = glyphs.ToArray()
            };
        }
    }

    private PlatformTextMeshResult CreateProjectedTextMesh(in PlatformTextMeshRequest request)
    {
        ArgumentNullException.ThrowIfNull(request.Text);
        _ = StrictUtf16.GetByteCount(request.Text);
        var primary = Font(request.FontPath, request.FontSize);
        var secondary = request.SecondaryFontPath is { } path ? Font(path, request.FontSize, primary) : null;
        var glyphs = new List<PlatformTextGlyphMesh>();
        var width = 0f;
        var baseline = 0f;
        var y = 0f;
        var lines = request.Text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Replace("\t", "    ", StringComparison.Ordinal).Split('\n');
        foreach (var line in lines)
        {
            if (line.Length != 0)
            {
                var runs = ShapeProjectedParagraph(line, primary, secondary, y, out var advance,
                    out var lineBaseline, request.RightToLeft);
                width = MathF.Max(width, advance);
                baseline = MathF.Max(baseline, lineBaseline);
                AppendVectorGlyphs(runs, request, 0, glyphs);
            }
            y += request.FontSize;
        }
        return new(ReadOnlyMemory<UiVector2>.Empty, width, y, baseline)
        {
            Glyphs = glyphs.ToArray()
        };
    }

    private void AppendVectorGlyphs(List<Run> runs, in PlatformTextMeshRequest request, float yOffset,
        List<PlatformTextGlyphMesh> glyphs)
    {
        foreach (var run in runs)
        {
            foreach (var positioned in run.Glyphs)
            {
                var mesh = GetVectorGlyph(run.Font, checked((uint)positioned.Index), request.FlatteningTolerance, request.OutlineOnly);
                if (request.OutlineOnly ? mesh.Curves.Length != 0 : mesh.Indices.Length != 0)
                    glyphs.Add(new(mesh.Vertices, mesh.Indices,
                        new((float)positioned.X, (float)positioned.Y + yOffset))
                    {
                        Outline = new(mesh.Curves)
                    });
            }
        }
    }

    private VectorGlyph GetVectorGlyph(LinuxFontResource font, uint index, float tolerance, bool outlineOnly = false)
    {
        if (_drawFunctions == 0)
            _drawFunctions = LinuxGlyphOutline.CreateFunctions();
        var key = new VectorKey(font.Path, font.Size, font.Oblique, index, outlineOnly ? 0f : tolerance);
        if (_vectorGlyphs.TryGetValue(key, out var mesh))
            return mesh;
        var outline = LinuxGlyphOutline.Extract(font.Shaper, index, _drawFunctions, tolerance, outlineOnly);
        mesh = outlineOnly ? new([], [], outline.Curves) : Tessellate(outline.Contours, outline.Curves);
        if (outlineOnly ? mesh.Curves.Length == 0 : mesh.Vertices.Length == 0)
        {
            var glyph = new Glyph(index);
            cairo_scaled_font_glyph_extents(font.Scaled, &glyph, 1, out var bounds);
            if (bounds.Width > 0 && bounds.Height > 0)
                throw new NotSupportedException("The selected glyph has ink but no supported vector outline.");
        }
        if (_vectorGlyphs.Count == VectorGlyphCapacity)
            _vectorGlyphs.Remove(_vectorOrder.Dequeue());
        _vectorGlyphs.Add(key, mesh);
        _vectorOrder.Enqueue(key);
        return mesh;
    }

    private static VectorGlyph Tessellate(List<List<UiVector2>> contours, UiVectorCurve[] curves)
    {
        if (contours.Count == 0)
            return new([], [], curves);
#if DUXEL_COMPACT
        // The standard pool compiles an expression tree to construct its nodes.
        // The allocation-only pool uses the same tessellator without that dependency.
        var tessellator = new Tess(new NullPool()) { NoEmptyPolygons = true };
#else
        var tessellator = new Tess { NoEmptyPolygons = true };
#endif
        foreach (var contour in contours)
        {
            var vertices = new ContourVertex[contour.Count];
            for (var i = 0; i < vertices.Length; i++)
                vertices[i].Position = new Vec3(contour[i].X, contour[i].Y, 0);
            tessellator.AddContour(vertices);
        }
        tessellator.Tessellate(WindingRule.NonZero, ElementType.Polygons, 3, normal: new Vec3(0, 0, 1));
        var points = new UiVector2[tessellator.VertexCount];
        for (var i = 0; i < points.Length; i++)
        {
            var p = tessellator.Vertices[i].Position;
            if (!float.IsFinite(p.X) || !float.IsFinite(p.Y))
                throw new InvalidOperationException("Glyph tessellation produced nonfinite vertices.");
            points[i] = new(p.X, p.Y);
        }
        var indices = new uint[checked(tessellator.ElementCount * 3)];
        for (var i = 0; i < indices.Length; i++)
        {
            var value = tessellator.Elements[i];
            if ((uint)value >= points.Length)
                throw new InvalidOperationException("Glyph tessellation produced an invalid triangle index.");
            indices[i] = (uint)value;
        }
        return new(points, indices, curves);
    }
}
