using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Duxel.Core;
using Duxel.Svg;

internal static class SvgFeatureCompositionTests
{
    internal static void Run(string output, Action<bool, string> check)
    {
        const int width = 320, height = 160;
        var root = Path.Combine(AppContext.BaseDirectory, "Fixtures", "SvgFeaturesBrowser");
        var source = Path.Combine(AppContext.BaseDirectory, "Assets", "Svg", "features.svg");
        var opaque = Read(Path.Combine(root, "reference.rgba"), "F9A39B61DFFEE4EFCBC43FE45E90ACA9E5ECF32F854DB48A530824881E1FFCB8");
        var transparent = Read(Path.Combine(root, "transparent.rgba"), "C1EA158B62970FFD3E3EF9A4295E4D431271D9439AF1A6AE265A31ABDE03F1C9");
        var sourceHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source)));
        if (sourceHash != "0A9EDC2927BA02A75BD1BE173A9C97A731C81E500D4100136F11D5D441855D1D")
            throw new InvalidDataException("The original SVG feature source changed.");
        byte[] Read(string path, string hash)
        {
            var bytes = File.ReadAllBytes(path);
            if (bytes.Length != width * height * 4 || Convert.ToHexString(SHA256.HashData(bytes)) != hash)
                throw new InvalidDataException("The independent browser phase reference changed : " + path);
            return bytes;
        }
        var expectedPremultiplied = transparent.ToArray();
        for (var pixel = 0; pixel < expectedPremultiplied.Length; pixel += 4)
            for (var channel = 0; channel < 3; channel++)
                expectedPremultiplied[pixel + channel] = (byte)((transparent[pixel + channel] * transparent[pixel + 3] + 127) / 255);
        var verification = JsonSerializer.Deserialize(File.ReadAllText(Path.Combine(root, "renderer-verification.json")),
            SvgFeatureCompositionJson.Default.SvgFeatureRendererVerification)
            ?? throw new InvalidDataException("The browser renderer verification is missing.");
        var opaqueHash = Convert.ToHexString(SHA256.HashData(opaque));
        var premultipliedHash = Convert.ToHexString(SHA256.HashData(expectedPremultiplied));
        ValidateReference(verification, sourceHash, opaqueHash, premultipliedHash);
        check(true, "SVG feature composition / observed browser renderer is linked to the exact source and phase references before GPU work");
        void Reject(SvgFeatureRendererVerification changed, string label)
        {
            var rejected = false;
            try
            {
                ValidateReference(changed, sourceHash, opaqueHash, premultipliedHash);
            }
            catch (InvalidDataException)
            {
                rejected = true;
            }
            check(rejected, "SVG feature composition / reference contract rejects " + label + " before GPU work");
        }
        Reject(verification with { SourceSha256 = opaqueHash }, "a different SVG source");
        Reject(verification with { OpaqueSha256 = premultipliedHash }, "a different opaque capture");
        Reject(verification with { TransparentPremultipliedSha256 = opaqueHash }, "a different premultiplied capture");
        Reject(verification with { Backend = "" }, "an unrecorded renderer");
        Reject(verification with { SkiaCommit = "unknown" }, "an unqualified Skia revision");
        using var store = new SvgStore();
        var scene = store.Load(source).GpuScene;
        using var fixture = new PresentationReuseTests.Fixture(1, true);
        var texture = new UiTextureId(2_300_000_089);
        var update = true;
        void Draw(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            if (update)
            {
                fixture.Context.QueueTextureUpdate(new(UiTextureUpdateKind.RenderVectorScene, texture,
                    UiTextureFormat.Rgba8Unorm, width, height, ReadOnlyMemory<byte>.Empty)
                {
                    VectorScene = scene, IsColorImage = true
                });
                update = false;
            }
            var draw = ui.GetWindowDrawList();
            draw.AddRectFilled(new(0, 0, fixture.FrameInfo.DisplaySize.X, fixture.FrameInfo.DisplaySize.Y), new(41, 41, 41), ui.WhiteTextureId);
            draw.AddImage(texture, default, new(width, height), default, new(1, 1), new(255, 255, 255));
        }
        byte[] ReadVector()
        {
            using var drawing = fixture.Build(Draw);
            return fixture.Renderer.RenderVectorAndReadback(drawing, texture, width, height);
        }
        var premultiplied = ReadVector();
        var work = fixture.Renderer.VectorSceneDiagnostics;
        var replay = ReadVector();
        check(premultiplied.AsSpan().SequenceEqual(replay) && fixture.Renderer.VectorSceneDiagnostics == work,
            "SVG feature composition / raw readback reuses exact retained GPU output without redispatch");
        var framebuffer = fixture.Capture(Draw);
        var composited = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
            framebuffer.AsSpan(y * (int)fixture.FrameInfo.DisplaySize.X * 4, width * 4).CopyTo(composited.AsSpan(y * width * 4));
        var integerComposite = new byte[composited.Length];
        for (var pixel = 0; pixel < premultiplied.Length; pixel += 4)
        {
            for (var channel = 0; channel < 3; channel++)
                integerComposite[pixel + channel] = (byte)Math.Min(255,
                    premultiplied[pixel + channel] + (41 * (255 - premultiplied[pixel + 3]) + 127) / 255);
            integerComposite[pixel + 3] = 255;
        }
        var rows = new[]
        {
            Compare("browser-transparent-to-GPU-premultiplied", expectedPremultiplied, premultiplied),
            Compare("integer-premultiplied-over-to-framebuffer", integerComposite, composited),
            Compare("browser-direct-to-framebuffer", opaque, composited)
        };
        File.WriteAllBytes(Path.Combine(output, "features-premultiplied.rgba"), premultiplied);
        File.WriteAllBytes(Path.Combine(output, "features-composited.rgba"), composited);
        File.WriteAllBytes(Path.Combine(output, "features-integer-over.rgba"), integerComposite);
        File.WriteAllText(Path.Combine(output, "composition.json"), JsonSerializer.Serialize(
            new SvgFeatureCompositionResult(width, height, verification, rows, store.Statistics), SvgFeatureCompositionJson.Default.SvgFeatureCompositionResult));
        check(store.Statistics.Rasterizations == 0, "SVG feature composition / all phases create zero CPU SVG raster pixels");
        check(rows[1].DifferentPixels == 0,
            "SVG feature composition / framebuffer equals independent integer premultiplied-over reference");
        check(rows[2].DifferentPixels == 0,
            "SVG feature composition / complete original browser RGBA is byte-identical");
    }

    private static void ValidateReference(SvgFeatureRendererVerification verification, string sourceHash, string opaqueHash, string premultipliedHash)
    {
        if (string.IsNullOrWhiteSpace(verification.BrowserVersion) || string.IsNullOrWhiteSpace(verification.Backend)
            || verification.SkiaCommit is not { Length: 40 } || !verification.SkiaCommit.All(char.IsAsciiHexDigit)
            || verification.ObservedAtUtc == default || verification.SourceSha256 != sourceHash
            || verification.OpaqueSha256 != opaqueHash || verification.TransparentPremultipliedSha256 != premultipliedHash)
            throw new InvalidDataException("The observed browser renderer does not qualify these SVG phase references.");
    }

    private static SvgFeatureCompositionCase Compare(string id, byte[] expected, byte[] actual)
    {
        if (expected.Length != actual.Length)
            throw new InvalidDataException("Composition phase extents differ.");
        var different = 0;
        var alpha = 0;
        var rgb = 0;
        var maximum = 0;
        for (var pixel = 0; pixel < actual.Length; pixel += 4)
        {
            var error = 0;
            for (var channel = 0; channel < 4; channel++)
                error = Math.Max(error, Math.Abs(expected[pixel + channel] - actual[pixel + channel]));
            if (error == 0)
                continue;
            different++;
            if (expected[pixel + 3] != actual[pixel + 3])
                alpha++;
            if (!expected.AsSpan(pixel, 3).SequenceEqual(actual.AsSpan(pixel, 3)))
                rgb++;
            maximum = Math.Max(maximum, error);
        }
        return new(id, actual.Length / 4, different, alpha, rgb, maximum);
    }
}

internal sealed record SvgFeatureCompositionCase(string Id, int ComparedPixels, int DifferentPixels,
    int AlphaDifferentPixels, int RgbDifferentPixels, int MaximumChannelDifference);
internal sealed record SvgFeatureRendererVerification(DateTimeOffset ObservedAtUtc, string BrowserVersion, string Backend,
    string SkiaCommit, bool OriginalCaptureBackendRecorded, string SourceSha256, string OpaqueSha256, string TransparentPremultipliedSha256);
internal sealed record SvgFeatureCompositionResult(int Width, int Height, SvgFeatureRendererVerification RendererVerification,
    SvgFeatureCompositionCase[] Cases, SvgStoreStatistics Statistics);
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(SvgFeatureCompositionResult))]
[JsonSerializable(typeof(SvgFeatureRendererVerification))]
internal partial class SvgFeatureCompositionJson : JsonSerializerContext;
