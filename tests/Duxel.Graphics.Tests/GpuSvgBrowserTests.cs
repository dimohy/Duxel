using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Duxel.Core;
using Duxel.Svg;

internal static class GpuSvgBrowserTests
{
    internal static string? ReferenceGroup(string profile) => profile switch
    {
        "gpu-svg-browser" or "gpu-svg-image-browser" or "gpu-svg-opaque-target-browser" or "gpu-transparent-browser" => "",
        "gpu-gradient-pixels" or "gpu-transparent-gradients" => "Gradients",
        "gpu-blend-backdrops" => "BlendBackdrops",
        "gpu-arc-geometry" or "gpu-transparent-arc-geometry" or "gpu-arc-opaque-target" => "ArcGeometry",
        "gpu-shadow-pixels" or "gpu-transparent-shadow" => "ShadowAlpha",
        "gpu-clip-storage" or "gpu-transparent-clip-storage" => "ClipStorage",
        "gpu-layer-origin" or "gpu-transparent-layer-origin" or "gpu-transparent-layer-origin-tiled" => "LayerOrigin",
        "gpu-stroke-domains" or "gpu-transparent-stroke-domains" => "StrokeDomains",
        "gpu-opacity-pixels" or "gpu-transparent-opacity" => "OpacityGroups",
        "gpu-color-storage" => "ColorStorage",
        "gpu-transparent-color" => Path.Combine("ColorStorage", "Transparent"),
        _ => null
    };

    internal static GpuSvgBrowserReference[] LoadReferences(string group = "", bool premultiplied = false, bool imageOnly = false)
    {
        var fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Browser", group);
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(fixtures, "references.json")));
        var references = manifest.RootElement.GetProperty("references").EnumerateArray().ToArray();
        var inputs = new List<GpuSvgBrowserReference>();
        // Validate the original input and independent oracle before device work.
        foreach (var reference in references)
        {
            if ((premultiplied || imageOnly) && reference.GetProperty("mode").GetString() != "image-canvas")
                continue;
            BrowserReferenceValidation.Validate(reference);
            var file = reference.GetProperty("file").GetString()!;
            var id = reference.GetProperty("id").GetString()!;
            var width = reference.GetProperty("width").GetInt32();
            var height = reference.GetProperty("height").GetInt32();
            var expected = File.ReadAllBytes(Path.Combine(fixtures, id + (premultiplied ? ".transparent.browser.rgba" : ".browser.rgba")));
            if (width <= 0 || height <= 0 || (long)width * height > 16 * 1024 * 1024
                || (long)width * height * 4 != expected.Length)
                throw new InvalidDataException("GPU browser reference dimensions changed : " + id);
            string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
            if (!Hash(Path.Combine(fixtures, file)).Equals(reference.GetProperty("sourceSha256").GetString(), StringComparison.OrdinalIgnoreCase)
                || !Convert.ToHexString(SHA256.HashData(expected)).Equals(reference.GetProperty(premultiplied ? "transparentRgbaSha256" : "rgbaSha256").GetString(), StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("GPU browser input or reference changed : " + id);
            if (premultiplied)
                for (var pixel = 0; pixel < expected.Length; pixel += 4)
                    for (var channel = 0; channel < 3; channel++)
                        expected[pixel + channel] = (byte)((expected[pixel + channel] * expected[pixel + 3] + 127) / 255);
            inputs.Add(new(Path.Combine(fixtures, file), id, width, height, expected));
        }
        if (inputs.Count == 0)
            throw new InvalidDataException("The selected GPU browser reference set is empty.");
        return inputs.ToArray();
    }

    internal static void Run(string output, UiContext context,
        Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check, int snapshotWidth,
        IReadOnlyList<GpuSvgBrowserReference> references, string scope = "Full original browser RGBA on white; integral GPU camera tiles, no CPU raster and no reduced image resolution",
        Func<string, Action<UiImmediateContext>, UiTextureId, int, int, byte[]>? captureVector = null,
        int tileWidthLimit = 256, int tileHeightLimit = 128, UiColor vectorBackground = default)
    {
        if (tileWidthLimit is < 1 or > 256 || tileHeightLimit is < 1 or > 128)
            throw new ArgumentOutOfRangeException(nameof(tileWidthLimit), "Camera tiles must fit the readback viewport.");
        using var store = new SvgStore();
        var texture = new UiTextureId(2_300_000_013);
        var rows = new List<GpuSvgBrowserCase>();
        var passed = 0;
        foreach (var reference in references)
        {
            var id = reference.Id;
            var width = reference.Width;
            var height = reference.Height;
            var scene = store.Load(reference.Path).GpuScene;
            var actual = new byte[checked(width * height * 4)];
            // Integral camera windows assemble the full original resolution without UI resampling.
            for (var top = 0; top < height; top += tileHeightLimit)
                for (var left = 0; left < width; left += tileWidthLimit)
                {
                    var tileWidth = Math.Min(tileWidthLimit, width - left);
                    var tileHeight = Math.Min(tileHeightLimit, height - top);
                    Action<UiImmediateContext> draw = ui =>
                    {
                        context.QueueTextureUpdate(new(UiTextureUpdateKind.RenderVectorScene, texture,
                            UiTextureFormat.Rgba8Unorm, tileWidth, tileHeight, ReadOnlyMemory<byte>.Empty)
                        {
                            VectorScene = scene,
                            VectorBackground = vectorBackground,
                            VectorCamera = new(new(width, height), new(left, top)),
                            IsColorImage = true
                        });
                        var draw = ui.GetWindowDrawList();
                        draw.AddRectFilled(new(8, 8, 272, 152), new(255, 255, 255, 255), ui.WhiteTextureId);
                        draw.AddImage(texture, new(16, 16), new(16 + tileWidth, 16 + tileHeight), default, new(1, 1), new(255, 255, 255));
                    };
                    var name = id + "-" + left + "-" + top;
                    var pixels = captureVector is null ? capture(name, draw) : captureVector(name, draw, texture, tileWidth, tileHeight);
                    for (var y = 0; y < tileHeight; y++)
                        pixels.AsSpan(captureVector is null ? ((y + 16) * snapshotWidth + 16) * 4 : y * tileWidth * 4, tileWidth * 4)
                            .CopyTo(actual.AsSpan(((top + y) * width + left) * 4));
                }
            var expected = reference.Expected.Span;
            var different = 0;
            var alphaDifferent = 0;
            var rgbDifferent = 0;
            var maximum = 0;
            long sum = 0;
            var histogram = new int[256];
            var difference = new byte[actual.Length];
            for (var index = 0; index < actual.Length; index += 4)
            {
                if (actual[index + 3] != expected[index + 3])
                    alphaDifferent++;
                if (!actual.AsSpan(index, 3).SequenceEqual(expected.Slice(index, 3)))
                    rgbDifferent++;
                var delta = 0;
                for (var channel = 0; channel < 4; channel++)
                {
                    var value = Math.Abs(actual[index + channel] - expected[index + channel]);
                    delta = Math.Max(delta, value);
                    difference[index + channel] = (byte)value;
                    if (channel < 3)
                        sum += value;
                }
                difference[index + 3] = 255;
                histogram[delta]++;
                if (delta != 0)
                    different++;
                maximum = Math.Max(maximum, delta);
            }
            if (different == 0)
                passed++;
            File.WriteAllBytes(Path.Combine(output, id + ".gpu.rgba"), actual);
            var preview = actual;
            if (captureVector is not null)
            {
                preview = actual.ToArray();
                for (var pixel = 0; pixel < preview.Length; pixel += 4)
                    for (var channel = 0; channel < 3; channel++)
                        preview[pixel + channel] = preview[pixel + 3] == 0 ? (byte)0
                            : (byte)Math.Clamp((preview[pixel + channel] * 255 + preview[pixel + 3] / 2) / preview[pixel + 3], 0, 255);
            }
            BrowserPng.Write(Path.Combine(output, id + ".gpu.png"), width, height, preview);
            BrowserPng.Write(Path.Combine(output, id + ".diff.png"), width, height, difference);
            rows.Add(new(id, width, height, different == 0, different, maximum,
                sum / (width * (double)height * 3), histogram, alphaDifferent, rgbDifferent)
            {
                GeneratedStrokePrograms = scene.Strokes.Count,
                CommandCount = scene.CommandCount,
                SceneBytes = scene.Data.Length * 16
            });
            Console.WriteLine($"GPU BROWSER {id} : different={different}, maximum={maximum}");
        }
        capture("gpu-browser-destroy", _ => context.QueueTextureUpdate(new(UiTextureUpdateKind.Destroy, texture,
            UiTextureFormat.Rgba8Unorm, 0, 0, ReadOnlyMemory<byte>.Empty)));
        File.WriteAllText(Path.Combine(output, "comparison.json"), JsonSerializer.Serialize(new GpuSvgBrowserResult(
            scope,
            passed, references.Count, store.Statistics, rows.ToArray())
        {
            PixelFormat = captureVector is null ? "opaque RGBA8" : "premultiplied RGBA8; PNG preview converted to straight alpha"
        }, GpuSvgBrowserJson.Default.GpuSvgBrowserResult));
        check(store.Statistics.Rasterizations == 0, "Full browser comparison creates no CPU SVG raster pixels");
        check(passed == references.Count, "Every full original browser path is byte-identical : " + passed + "/" + references.Count);
    }
}

internal sealed record GpuSvgBrowserReference(string Path, string Id, int Width, int Height, ReadOnlyMemory<byte> Expected);

internal sealed record GpuSvgBrowserCase(string Id, int Width, int Height, bool Exact, int DifferentPixels,
    int MaximumChannelError, double MeanAbsoluteRgbError, int[] Histogram, int AlphaDifferentPixels, int RgbDifferentPixels)
{
    public int GeneratedStrokePrograms
    {
        get; init;
    }
    public int CommandCount
    {
        get; init;
    }
    public int SceneBytes
    {
        get; init;
    }
}
internal sealed record GpuSvgBrowserResult(string Scope, int Passed, int Total, SvgStoreStatistics Statistics,
    GpuSvgBrowserCase[] Results)
{
    public string PixelFormat { get; init; } = "opaque RGBA8";
}
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(GpuSvgBrowserResult))]
internal partial class GpuSvgBrowserJson : JsonSerializerContext;
