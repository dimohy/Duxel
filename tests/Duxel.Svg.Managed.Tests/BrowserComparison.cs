using System.Diagnostics;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using Duxel.Svg.Managed;

internal static class BrowserComparison
{
    internal static bool Run(string directory, string group = "", bool straightAlpha = false, bool whiteTarget = false)
    {
        if (straightAlpha && whiteTarget)
            throw new ArgumentException("Straight-alpha export and an opaque initial target are different comparison phases.");
        var output = Path.GetFullPath(directory);
        if (Directory.Exists(output))
            throw new IOException("Specify a fresh comparison directory.");
        Directory.CreateDirectory(output);
        var fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Browser", group);
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(fixtures, "references.json")));
        foreach (var reference in manifest.RootElement.GetProperty("references").EnumerateArray())
            BrowserReferenceValidation.Validate(reference);
        List<BrowserResult> results = [];
        foreach (var item in manifest.RootElement.GetProperty("references").EnumerateArray())
        {
            var file = item.GetProperty("file").GetString()!;
            var mode = item.GetProperty("mode").GetString()!;
            var source = File.ReadAllBytes(Path.Combine(fixtures, file));
            if (!Convert.ToHexString(SHA256.HashData(source)).Equals(item.GetProperty("sourceSha256").GetString(), StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The reference SVG source changed: " + file);
            var width = item.GetProperty("width").GetInt32();
            var height = item.GetProperty("height").GetInt32();
            var stem = item.GetProperty("id").GetString()!;
            var expected = File.ReadAllBytes(Path.Combine(fixtures, stem + ".browser.rgba"));
            if (expected.Length != checked(width * height * 4) ||
                !Convert.ToHexString(SHA256.HashData(expected)).Equals(item.GetProperty("rgbaSha256").GetString(), StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The browser pixel reference changed: " + file);
            try
            {
                using var stream = new MemoryStream(source);
                var clock = Stopwatch.StartNew();
                var scene = SvgManagedScene.Compile(XDocument.Load(stream));
                var compileMs = clock.Elapsed.TotalMilliseconds;
                clock.Restart();
                var pixels = scene.Render(width, height, mode == "direct-svg" || whiteTarget ? Vector4.One : Vector4.Zero);
                var transparentPhase = whiteTarget && mode == "image-canvas" ? scene.Render(width, height) : pixels;
                var renderMs = clock.Elapsed.TotalMilliseconds;
                var transparentDifferent = 0;
                if (mode == "image-canvas")
                {
                    var transparent = File.ReadAllBytes(Path.Combine(fixtures, stem + ".transparent.browser.rgba"));
                    if (transparent.Length != pixels.Length ||
                        !Convert.ToHexString(SHA256.HashData(transparent)).Equals(item.GetProperty("transparentRgbaSha256").GetString(), StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("The transparent browser reference changed: " + file);
                    if (straightAlpha)
                        expected = transparent;
                    for (var i = 0; i < pixels.Length; i += 4)
                    {
                        var alpha = transparentPhase[i + 3];
                        var differentPixel = alpha != transparent[i + 3];
                        for (var channel = 0; channel < 3; channel++)
                            differentPixel |= (transparentPhase[i + channel] * alpha + 127) / 255 !=
                                (transparent[i + channel] * transparent[i + 3] + 127) / 255;
                        if (differentPixel)
                            transparentDifferent++;
                        if (!straightAlpha && !whiteTarget)
                        {
                            for (var channel = 0; channel < 3; channel++)
                                pixels[i + channel] = (byte)Math.Clamp((pixels[i + channel] * alpha + 127) / 255 + 255 - alpha, 0, 255);
                            pixels[i + 3] = 255;
                        }
                    }
                }
                else if (mode != "direct-svg")
                    throw new InvalidDataException("Unknown browser rendering mode.");
                var diff = new byte[pixels.Length];
                long sum = 0;
                var severe = 0;
                var different = 0;
                var maximum = 0;
                for (var i = 0; i < pixels.Length; i += 4)
                {
                    var pixelMax = 0;
                    for (var channel = 0; channel < 4; channel++)
                    {
                        var actual = pixels[i + channel];
                        var delta = Math.Abs(actual - expected[i + channel]);
                        if (channel < 3)
                            sum += delta;
                        pixelMax = Math.Max(pixelMax, delta);
                        diff[i + channel] = (byte)delta;
                    }
                    diff[i + 3] = 255;
                    maximum = Math.Max(maximum, pixelMax);
                    if (pixelMax != 0)
                        different++;
                    if (pixelMax > 32)
                        severe++;
                }
                var mean = sum / (width * (double)height * 3);
                var severeRatio = severe / (width * (double)height);
                BrowserPng.Write(Path.Combine(output, stem + ".actual.png"), width, height, pixels);
                BrowserPng.Write(Path.Combine(output, stem + ".diff.png"), width, height, diff);
                // Error metrics diagnose mismatches; they never relax the user's identical-output acceptance.
                var passed = maximum == 0 && transparentDifferent == 0;
                results.Add(new(file, mode + (straightAlpha ? "/straight-alpha" : whiteTarget ? "/white-target" : ""),
                    passed, passed, mean, severeRatio, maximum, different, transparentDifferent, compileMs, renderMs, null));
            }
            catch (Exception error) when (error is InvalidDataException or NotSupportedException)
            {
                results.Add(new(file, mode, false, false, null, null, null, null, null, null, null, error.ToString()));
            }
            Console.WriteLine(JsonSerializer.Serialize(results[^1], BrowserJsonContext.Default.BrowserResult));
        }
        File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(results.ToArray(), BrowserJsonContext.Default.BrowserResultArray));
        return results.All(result => result.Passed);
    }
}

internal sealed record BrowserResult(string File, string Mode, bool Passed, bool Exact, double? MeanAbsoluteRgbError,
    double? SeverePixelRatio, int? MaximumChannelError, int? DifferentPixels, int? TransparentDifferentPixels,
    double? CompileMs, double? RenderMs, string? Error);

[JsonSerializable(typeof(BrowserResult))]
[JsonSerializable(typeof(BrowserResult[]))]
internal partial class BrowserJsonContext : JsonSerializerContext;
