using System.Runtime.InteropServices;
using System.Text.Json;
using Duxel.Core;
using Duxel.Platform.Linux;

internal static unsafe partial class Program
{
    private static void RunVector(string resultPath, string latinPath, string cjkPath)
    {
        var output = Path.GetFullPath(resultPath);
        if (Directory.Exists(output))
            throw new IOException("Result directory already exists: " + output);
        if (!File.Exists(latinPath) || !File.Exists(cjkPath))
            throw new FileNotFoundException("Both explicit vector fixture fonts must exist.");
        Directory.CreateDirectory(output);
        var checks = new List<string>();
        var comparisons = new List<LinuxVectorComparison>();
        void Check(bool condition, string name)
        {
            if (!condition)
                throw new InvalidOperationException(name);
            checks.Add(name);
            Console.WriteLine("PASS " + name);
        }
        void Throws<T>(Action action, string name) where T : Exception
        {
            var caught = false;
            try
            {
                action();
            }
            catch (T) { caught = true; }
            Check(caught, name);
        }
        using var backend = new LinuxPlatformTextBackend();
        var cubicFont = Path.Combine(AppContext.BaseDirectory, "Fixtures", "CubicOutline.otf");
        Check(File.Exists(cubicFont), "The explicit original CFF fixture is present before native execution");
        var italicLatin = backend.GetFontVariantResource(latinPath, PlatformFontSimulation.Oblique);
        var italicCjk = backend.GetFontVariantResource(cjkPath, PlatformFontSimulation.Oblique);
        Check(italicLatin == backend.GetFontVariantResource(italicLatin, PlatformFontSimulation.Oblique), "Oblique resources are stable and idempotent");
        Check(backend.GetFontVariantResource(latinPath, PlatformFontSimulation.None) == latinPath, "Normal variant preserves the requested resource");
        Throws<ArgumentOutOfRangeException>(() => backend.GetFontVariantResource(latinPath, (PlatformFontSimulation)99), "Unknown font simulations fail explicitly");
        Throws<ArgumentOutOfRangeException>(() => backend.CreateTextMesh(new(latinPath, null, "O", 24, 0)), "Invalid flattening tolerance fails explicitly");

        var cases = new[]
        {
            (latinPath, cjkPath, "O8B"),
            (latinPath, cjkPath, "정세일"),
            (latinPath, cjkPath, "office e\u0301"),
            (latinPath, cjkPath, "A\n정"),
            (italicLatin, italicCjk, "O8B"),
            (italicLatin, italicCjk, "정세일"),
            (cubicFont, cubicFont, "O"),
            (backend.GetFontVariantResource(cubicFont, PlatformFontSimulation.Oblique), cubicFont, "O")
        };
        foreach (var (font, secondary, content) in cases)
        {
            var request = new PlatformTextMeshRequest(font, secondary, content, 72, 0.01f);
            var mesh = backend.CreateTextMesh(request);
            backend.TryRasterize(new(font, secondary, content, 72), out var raster);
            backend.TryGetFontMetrics(font, 72, out var metrics);
            Check(mesh.TriangleVertices.IsEmpty && mesh.Glyphs.Length > 0, "Vector text uses indexed immutable glyph buffers");
            Check(mesh.Width == raster.Advance && mesh.Baseline == raster.FontAscent && mesh.Height >= metrics.Ascent - metrics.Descent,
                "Vector and raster share shaping advance and font baseline");
            var triangles = Triangles(mesh);
            Check(triangles.Count > 0 && triangles.All(t => float.IsFinite(t.A.X) && float.IsFinite(t.A.Y)
                && Math.Abs(Cross(t.A, t.B, t.C)) > 0), "Native outline tessellation produces finite nondegenerate triangles");
            var samples = 0;
            var bad = 0;
            var filled = 0;
            var vectorPixels = new byte[checked(raster.Width * raster.Height)];
            for (var y = 0; y < raster.Height; y++)
            {
                for (var x = 0; x < raster.Width; x++)
                {
                    var point = new UiVector2(x + raster.OriginX + 0.5f,
                        y + raster.FontAscent - raster.Baseline + 0.5f);
                    var inside = Contains(triangles, point);
                    vectorPixels[y * raster.Width + x] = inside ? (byte)255 : (byte)0;
                    var solid = true;
                    var empty = true;
                    for (var dy = -1; dy <= 1; dy++)
                    {
                        for (var dx = -1; dx <= 1; dx++)
                        {
                            var sx = x + dx;
                            var sy = y + dy;
                            var alpha = (uint)sx < raster.Width && (uint)sy < raster.Height
                                ? raster.RgbaPixels.Span[(sy * raster.Width + sx) * 4] : (byte)0;
                            solid &= alpha >= 250;
                            empty &= alpha <= 5;
                        }
                    }
                    if (!solid && !empty)
                        continue;
                    samples++;
                    if (solid)
                        filled++;
                    if (solid != inside)
                        bad++;
                }
            }
            Console.WriteLine($"COMPARE {JsonSerializer.Serialize(content, LinuxValidationJson.Default.String)} interior={samples} filled={filled} mismatch={bad}");
            Check(samples > 100 && filled > 20 && bad == 0, "Independent Cairo raster interiors and vector triangles agree, including holes");
            var stem = "vector-" + comparisons.Count;
            WritePgm(Path.Combine(output, stem + ".pgm"), raster.Width, raster.Height, vectorPixels);
            var gray = new byte[vectorPixels.Length];
            for (var i = 0; i < gray.Length; i++)
                gray[i] = raster.RgbaPixels.Span[i * 4];
            WritePgm(Path.Combine(output, stem + "-raster.pgm"), raster.Width, raster.Height, gray);
            comparisons.Add(new(content, font, triangles.Count, samples, filled, bad));
            var repeated = backend.CreateTextMesh(request);
            Check(mesh.Glyphs.Span[0].Vertices.Equals(repeated.Glyphs.Span[0].Vertices)
                && mesh.Glyphs.Span[0].Indices.Equals(repeated.Glyphs.Span[0].Indices), "Repeated glyphs reuse the same immutable mesh storage");
        }
        var blank = backend.CreateTextMesh(new(latinPath, null, "   ", 24));
        Check(blank.Glyphs.IsEmpty && blank.Width > 0 && blank.Height > 0, "Whitespace advances without creating vector geometry");
        var retained = backend.CreateTextMesh(new(latinPath, null, "I", 24));
        var held = retained.Glyphs.Span[0].Vertices.ToArray();
        for (var i = 0; i < 520; i++)
            backend.CreateTextMesh(new(latinPath, null, "I", 24, 0.1f + i * 0.001f));
        var renewed = backend.CreateTextMesh(new(latinPath, null, "I", 24));
        Check(!retained.Glyphs.Span[0].Vertices.Equals(renewed.Glyphs.Span[0].Vertices)
            && held.AsSpan().SequenceEqual(renewed.Glyphs.Span[0].Vertices.Span)
            && held.AsSpan().SequenceEqual(retained.Glyphs.Span[0].Vertices.Span), "Bounded mesh eviction preserves retained buffers and deterministic reconstruction");
        var coarse = backend.CreateTextMesh(new(latinPath, null, "O", 72, 1));
        var fine = backend.CreateTextMesh(new(latinPath, null, "O", 72, 0.001f));
        Check(fine.Glyphs.Span[0].Vertices.Length > coarse.Glyphs.Span[0].Vertices.Length, "Requested curve tolerance controls actual outline subdivision");
        Parallel.For(0, 4, i =>
        {
            using var independent = new LinuxPlatformTextBackend();
            var value = independent.CreateTextMesh(new(latinPath, cjkPath, "O정", 24 + i));
            if (value.Glyphs.IsEmpty)
                throw new InvalidOperationException("Independent native callback state was lost.");
        });
        Check(true, "Concurrent independent backends isolate native callbacks, contours and tessellation");
        backend.TryRasterize(new(latinPath, null, "HI", 48), out var upright);
        backend.TryRasterize(new(italicLatin, null, "HI", 48), out var slanted);
        Check(upright.Advance == slanted.Advance && (upright.Width != slanted.Width
            || !upright.RgbaPixels.Span.SequenceEqual(slanted.RgbaPixels.Span)), "Oblique changes visible shape while retaining horizontal advance");
        backend.Dispose();
        Throws<ObjectDisposedException>(() => backend.CreateTextMesh(new(latinPath, null, "I", 24)), "Disposed backends reject even previously cached mesh requests");
        Throws<ObjectDisposedException>(() => backend.GetFontVariantResource(latinPath, PlatformFontSimulation.None), "Disposed backends reject font-variant requests");
        Check(held.AsSpan().SequenceEqual(retained.Glyphs.Span[0].Vertices.Span), "Borrowed immutable meshes survive backend disposal");
        using var next = new LinuxPlatformTextBackend();
        var atlas = UiFontAtlasBuilder.CreateFromTtf(cjkPath, new int[] { 32, 63, 65, '정', '세', '일' }, 18, 256, 256, oversample: 2);
        RunTextRender(cjkPath, atlas, Check, vector: true);
        RunVectorReadback(output, latinPath, cjkPath, atlas, Check);
        File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new LinuxVectorResult(
            RuntimeInformation.FrameworkDescription, RuntimeInformation.OSDescription,
            RuntimeInformation.ProcessArchitecture.ToString(), "vector", [latinPath, cjkPath], checks.Count, checks, comparisons),
            LinuxValidationJson.Default.LinuxVectorResult));
    }

    private readonly record struct Triangle(UiVector2 A, UiVector2 B, UiVector2 C);
    private static float Cross(UiVector2 a, UiVector2 b, UiVector2 c) => (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);

    private static List<Triangle> Triangles(PlatformTextMeshResult mesh)
    {
        var result = new List<Triangle>();
        foreach (var glyph in mesh.Glyphs.Span)
        {
            var points = glyph.Vertices.Span;
            var indices = glyph.Indices.Span;
            if (indices.Length % 3 != 0)
                throw new InvalidOperationException("A vector glyph contains incomplete triangles.");
            UiVector2 At(uint i) => new(glyph.Vertices.Span[(int)i].X + glyph.Offset.X, glyph.Vertices.Span[(int)i].Y + glyph.Offset.Y);
            for (var i = 0; i < indices.Length; i += 3)
                result.Add(new(At(indices[i]), At(indices[i + 1]), At(indices[i + 2])));
        }
        return result;
    }

    private static bool Contains(List<Triangle> triangles, UiVector2 point)
    {
        foreach (var t in triangles)
        {
            if (point.X < MathF.Min(t.A.X, MathF.Min(t.B.X, t.C.X)) || point.X > MathF.Max(t.A.X, MathF.Max(t.B.X, t.C.X))
                || point.Y < MathF.Min(t.A.Y, MathF.Min(t.B.Y, t.C.Y)) || point.Y > MathF.Max(t.A.Y, MathF.Max(t.B.Y, t.C.Y)))
                continue;
            var a = Cross(t.A, t.B, point);
            var b = Cross(t.B, t.C, point);
            var c = Cross(t.C, t.A, point);
            if ((a >= 0 && b >= 0 && c >= 0) || (a <= 0 && b <= 0 && c <= 0))
                return true;
        }
        return false;
    }

    private static void WritePgm(string file, int width, int height, byte[] coverage)
    {
        using var stream = File.Create(file);
        stream.Write(System.Text.Encoding.ASCII.GetBytes($"P5\n{width} {height}\n255\n"));
        stream.Write(coverage);
    }
}
