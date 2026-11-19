using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Duxel.Core;
using Duxel.Platform.Linux;
using Duxel.Vulkan;

internal static unsafe partial class Program
{
    private static void RunText(string resultPath, string latinPath, string cjkPath)
    {
        var output = Path.GetFullPath(resultPath);
        if (Directory.Exists(output))
            throw new IOException("Result directory already exists: " + output);
        if (!File.Exists(latinPath) || !File.Exists(cjkPath))
            throw new FileNotFoundException("Both explicit fixture fonts must exist.");
        Directory.CreateDirectory(output);
        var checks = new List<string>();
        void Check(bool condition, string name)
        {
            if (!condition)
                throw new InvalidOperationException(name);
            checks.Add(name);
            Console.WriteLine("PASS " + name);
        }
        void Throws<T>(Action action, string name) where T : Exception
        {
            var thrown = false;
            try
            {
                action();
            }
            catch (T) { thrown = true; }
            Check(thrown, name);
        }

        using var text = new LinuxPlatformTextBackend();
        Check(text.TryGetFontMetrics(cjkPath, 24, out var metrics) && metrics.Ascent > 0 && metrics.Descent > 0,
            "Real font metrics preserve native positive ascent and descent distances");
        var samples = new[] { "AV fi office", "정세일 한글", "e\u0301 A\u030a", "line one\n둘째 줄", "A\tB", "   ", "", "jÁgy" };
        var sampleResults = new List<LinuxTextSample>();
        foreach (var sample in samples)
        {
            var measureRequest = new PlatformTextMeasureRequest(latinPath, cjkPath, sample, 24);
            Check(text.TryLayout(measureRequest, out var layout), "Layout: " + JsonSerializer.Serialize(sample, LinuxValidationJson.Default.String));
            Check(text.TryMeasure(measureRequest, out var measured), "Measure: " + JsonSerializer.Serialize(sample, LinuxValidationJson.Default.String));
            Check(text.TryRasterize(new(latinPath, cjkPath, sample, 24), out var raster), "Raster: " + JsonSerializer.Serialize(sample, LinuxValidationJson.Default.String));
            Check(layout.Width == raster.Width && layout.Height == raster.Height && layout.Baseline == raster.Baseline
                && layout.FontAscent == raster.FontAscent && layout.Advance == raster.Advance && layout.OriginX == raster.OriginX
                && measured.Width == layout.Width && measured.Height == layout.Height && measured.Baseline == layout.Baseline,
                "Layout-only and raster bounds/baseline/advance match exactly");
            var pixels = raster.RgbaPixels.Span;
            var coverage = false;
            var grayscale = true;
            for (var i = 0; i < pixels.Length; i += 4)
            {
                coverage |= pixels[i] > 0;
                grayscale &= pixels[i] == pixels[i + 1] && pixels[i] == pixels[i + 2] && pixels[i] == pixels[i + 3];
            }
            Check(grayscale && pixels.Length == raster.Width * raster.Height * 4, "Font textures encode identical RGB/alpha coverage");
            Check(coverage == !string.IsNullOrWhiteSpace(sample), "Visible content has real coverage; whitespace has no ink");
            text.TryRasterize(new(latinPath, cjkPath, sample, 24), out var repeated);
            Check(pixels.SequenceEqual(repeated.RgbaPixels.Span), "Repeated immutable-font requests are pixel deterministic");
            if (coverage)
            {
                var file = Path.Combine(output, "sample-" + sampleResults.Count + ".pgm");
                using var stream = File.Create(file);
                stream.Write(System.Text.Encoding.ASCII.GetBytes($"P5\n{raster.Width} {raster.Height}\n255\n"));
                for (var i = 0; i < pixels.Length; i += 4)
                    stream.WriteByte(pixels[i]);
            }
            sampleResults.Add(new(sample, layout, Convert.ToHexString(SHA256.HashData(pixels))));
        }
        text.TryLayout(new(latinPath, null, "A", 24), out var single);
        text.TryLayout(new(latinPath, null, "A   ", 24), out var trailing);
        Check(trailing.Advance > single.Advance && trailing.Width == single.Width, "Trailing whitespace changes advance without inflating ink bounds");
        text.TryLayout(new(latinPath, cjkPath, "line one", 24), out single);
        text.TryLayout(new(latinPath, cjkPath, "line one\n둘째 줄", 24), out var multiline);
        Check(multiline.Height > single.Height && multiline.Baseline > 0, "Multiline content preserves baseline and increases height");
        Throws<NotSupportedException>(() => text.TryLayout(new(latinPath, null, "정", 24), out _), "Missing CJK glyph fails instead of silently substituting another font");
        Throws<FileNotFoundException>(() => text.TryLayout(new(Path.Combine(output, "missing.ttf"), null, "A", 24), out _), "Missing explicit font fails before raster allocation");
        Throws<ArgumentOutOfRangeException>(() => text.TryLayout(new(latinPath, null, "A", float.NaN), out _), "Nonfinite size is rejected");
        Throws<ArgumentOutOfRangeException>(() => text.TryLayout(new(latinPath, null, "A", 0.001f), out _), "Sizes below native 26.6 precision fail explicitly");
        Throws<ArgumentOutOfRangeException>(() => text.TryLayout(new(latinPath, null, "A", float.MaxValue), out _), "Unrepresentable native font size fails before resource creation");
        var invalidFont = Path.Combine(output, "invalid-font.ttf");
        File.WriteAllBytes(invalidFont, [0, 1, 2, 3]);
        Throws<InvalidOperationException>(() => text.TryLayout(new(invalidFont, null, "A", 24), out _), "Invalid font input releases partial native initialization and fails explicitly");
        Throws<System.Text.EncoderFallbackException>(() => text.TryLayout(new(latinPath, null, "\uD800", 24), out _), "Malformed UTF-16 fails instead of substituting a replacement glyph");
        text.TryLayout(new(latinPath, null, "A", 24), out var a);
        text.TryLayout(new(latinPath, null, "V", 24), out var v);
        text.TryLayout(new(latinPath, null, "AV", 24), out var av);
        Check(av.Advance < a.Advance + v.Advance, "HarfBuzz applies actual font kerning instead of scalar-only positioning");
        for (var i = 0; i < 6; i++)
        {
            var value = "정세일 한글 Linux " + i;
            text.TryLayout(new(cjkPath, null, value, 18), out var before);
            text.TryRasterize(new(cjkPath, null, value, 18), out var painted);
            text.TryRasterize(new(latinPath, null, "AV e\u0301", 31 + i), out _);
            text.TryLayout(new(cjkPath, null, value, 18), out var after);
            Check(before == after && before.Width == painted.Width && before.Height == painted.Height
                && before.Advance == painted.Advance && before.Baseline == painted.Baseline,
                "OpenType shaping stays immutable across Cairo glyph loads, size changes and digit " + i);
        }
        using (var eviction = new LinuxPlatformTextBackend())
        {
            eviction.TryRasterize(new(latinPath, null, "A", 24), out var original);
            for (var size = 40; size < 71; size++)
                eviction.TryLayout(new(latinPath, null, "A", size), out _);
            eviction.TryRasterize(new(latinPath, cjkPath, "A정", 24), out var mixed);
            Check(mixed.RgbaPixels.Length > original.RgbaPixels.Length,
                "Adding the secondary font at full cache capacity does not retire the active primary");
        }
        // Exercise FIFO eviction while a previously cached primary is still referenced by a mixed-font request.
        text.TryLayout(new(latinPath, null, "A", 25), out _);
        for (var size = 50; size < 90; size++)
            text.TryLayout(new(latinPath, cjkPath, "A정", size), out _);
        Check(text.TryRasterize(new(latinPath, cjkPath, "A정", 25), out var afterEviction) && afterEviction.RgbaPixels.Length > 0,
            "Cache eviction and mixed-font layouts retain live native resources");
        Parallel.For(0, 12, i =>
        {
            if (!text.TryLayout(new(cjkPath, null, "정세일 " + i, 24), out var value) || value.Advance <= 0)
                throw new InvalidOperationException("Concurrent layout failed.");
        });
        Check(true, "Concurrent font operations serialize native face access");
        text.Dispose();
        Throws<ObjectDisposedException>(() => text.TryLayout(new(latinPath, null, "A", 24), out _), "Disposed backend rejects cached and uncached layout access");
        // A separate atlas service must survive window/standalone text-backend disposal.
        using var next = new LinuxPlatformTextBackend();
        Check(next.TryRasterize(new(cjkPath, null, "정", 18), out var nextResult) && nextResult.RgbaPixels.Length > 0,
            "A subsequent independent backend remains usable after disposal");
        var atlas = UiFontAtlasBuilder.CreateFromTtf(cjkPath, new int[] { 32, 63, 65, '정', '세', '일' }, 18, 256, 256, oversample: 2);
        Check(atlas.Glyphs.TryGetValue('정', out var hangul) && hangul.Width > 0 && hangul.Height > 0,
            "The existing Core atlas factory consumes the Linux rasterizer and preserves Korean placement");
        Check(atlas.Pixels.Any(value => value is > 0 and < 255), "Oversampled atlas contains antialiased native coverage");
        var glyphIndex = FontGlyphIndex(cjkPath, '정');
        Check(next.TryRasterize(cjkPath, '정', glyphIndex, 18, 1, 1, 1, out var glyph1)
            && next.TryRasterize(cjkPath, '정', glyphIndex, 18, 2, 1, 1, out var glyph2)
            && glyph1.Advance == glyph2.Advance && glyph1.OffsetY <= 0 && glyph2.OffsetY <= 0
            && glyph1.Alpha.Any(value => value > 0) && glyph2.Alpha.Any(value => value > 0),
            "Atlas oversampling preserves logical advance, negative baseline bearing and nonempty coverage");
        Throws<ArgumentException>(() => next.TryRasterize(cjkPath, '정', glyphIndex + 1, 18, 2, 1, 1, out _),
            "Mismatched atlas glyph IDs fail explicitly");
        RunTextRender(cjkPath, atlas, Check);
        File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new LinuxTextResult(
            RuntimeInformation.FrameworkDescription, RuntimeInformation.OSDescription,
            RuntimeInformation.ProcessArchitecture.ToString(), "text", [latinPath, cjkPath], checks.Count, checks, sampleResults),
            LinuxValidationJson.Default.LinuxTextResult));
    }

    private static int FontGlyphIndex(string path, int codepoint)
    {
        // Query through a separately owned native face, not backend/cache state.
        if (FT_Init_FreeType(out var library) != 0)
            throw new InvalidOperationException("Fixture FreeType initialization failed.");
        nint face = 0;
        try
        {
            if (FT_New_Face(library, path, 0, out face) != 0)
                throw new InvalidOperationException("Fixture font open failed.");
            return checked((int)FT_Get_Char_Index(face, (nuint)codepoint));
        }
        finally
        {
            if (face != 0)
                FT_Done_Face(face);
            FT_Done_FreeType(library);
        }
    }

    private static void RunTextRender(string path, UiFontAtlas atlas, Action<bool, string> check, bool vector = false)
    {
        using var platform = new LinuxX11PlatformBackend(new(400, 240, "Duxel owned Linux text fixture"));
        check(platform.TextBackend is IPlatformTextLayoutBackend && platform.TextBackend is IPlatformFontMetricsBackend,
            "The actual X11 platform exposes its owned text/layout/metrics backend");
        {
            using var renderer = new VulkanRendererBackend(platform, new(2, EnableVSync: false, MsaaSamples: 1,
                DiagnosticsLog: Console.WriteLine));
            renderer.CreateDeviceObjects();
            using var context = new UiContext(atlas, new(1), new(2));
            context.SetPlatformTextBackend(platform.TextBackend);
            context.SetDirectTextPrimaryFontPath(path);
            context.SetDirectTextBaseFontSize(18);
            context.SetDirectTextEnabled(true);
            context.SetDirectTextFallbackEnabled(false);
            for (var frame = 0; frame < 3; frame++)
            {
                platform.PollEvents();
                context.NewFrame(new(1f / 60, new(400, 240), new(1, 1)),
                    new(new(-1, -1), false, false, false, false, false, false, 0, 0, [], [], default, default),
                    new(0, 0, 400, 240), UiTextSettings.Default);
                if (frame == 0)
                {
                    context.QueueTextureUpdate(atlas.CreateTextureUpdate(new(1)));
                    context.QueueTextureUpdate(new(UiTextureUpdateKind.Create, new(2), UiTextureFormat.Rgba8Unorm, 1, 1,
                        new byte[] { 255, 255, 255, 255 }));
                }
                context.Render(new Screen(ui =>
                {
                    ui.GetBackgroundDrawList().AddText(atlas, "정세일 A", new(12, 150), new(255, 255, 255), new(1),
                        new(0, 0, 400, 240), UiTextSettings.Default);
                    if (vector)
                        ui.BeginLayer("owned-linux-vector", new(false, 1, default)
                        {
                            Scene3D = new(System.Numerics.Matrix4x4.Identity, false)
                        });
                    ui.Text("정세일 한글 Linux " + frame);
                    if (vector)
                        ui.EndLayer();
                }));
                using var data = context.GetDrawData();
                check(renderer.TryRenderDrawData(data), "The common renderer presents native " + (vector ? "vector-text" : "direct-text") + " and atlas frame " + frame);
            }
        }
        var retained = platform.TextBackend!;
        platform.Dispose();
        var disposed = false;
        try
        {
            retained.TryMeasure(new(path, null, "정", 18), out _);
        }
        catch (ObjectDisposedException) { disposed = true; }
        check(disposed, "Window disposal retires its retained text backend");
    }

    [LibraryImport("libfreetype.so.6")] private static partial int FT_Init_FreeType(out nint library);
    [LibraryImport("libfreetype.so.6", StringMarshalling = StringMarshalling.Utf8)] private static partial int FT_New_Face(nint library, string path, nint index, out nint face);
    [LibraryImport("libfreetype.so.6")] private static partial uint FT_Get_Char_Index(nint face, nuint codepoint);
    [LibraryImport("libfreetype.so.6")] private static partial int FT_Done_Face(nint face);
    [LibraryImport("libfreetype.so.6")] private static partial int FT_Done_FreeType(nint library);
}
