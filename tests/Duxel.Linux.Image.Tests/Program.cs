using System.Runtime.InteropServices;
using System.Text.Json;
using Duxel.Core;
using Duxel.Platform.Linux;
using Duxel.Svg;

if (!OperatingSystem.IsLinux() || args.Length != 1)
    throw new ArgumentException("Run on Linux with a fresh result directory.");
var output = Path.GetFullPath(args[0]);
if (Directory.Exists(output))
    throw new IOException("Result directory already exists: " + output);
Directory.CreateDirectory(output);
var fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures");
var checks = new List<string>();
void Check(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
    checks.Add(message);
    Console.WriteLine("PASS " + message);
}
var decoder = LinuxUiImageDecoder.Shared;
foreach (var name in new[] { "alpha.png", "odd-rgb.png", "odd.bmp", "animated.gif", "photo.jpg", "icon.ico", "dib.ico", "multi.ico", "multi-dib.ico", "한글.png" })
{
    var path = Path.Combine(fixtures, name);
    Check(decoder.CanDecode(path), "The expected bitmap extension is admitted: " + name);
    var expected = File.ReadAllBytes(path + ".rgba");
    var metadata = File.ReadAllLines(path + ".size");
    var decoded = decoder.Decode(path);
    Check(decoded.Width == int.Parse(metadata[0]) && decoded.Height == int.Parse(metadata[1]), "Native dimensions match independent fixture metadata: " + name);
    var tolerance = name.EndsWith(".jpg", StringComparison.Ordinal) ? 3 : 0;
    Check(decoded.RgbaPixels.Length == expected.Length
        && decoded.RgbaPixels.Zip(expected).All(p => Math.Abs(p.First - p.Second) <= tolerance),
        "Straight RGBA, row padding and first-frame pixels match the independent fixture: " + name);
}
Check(!decoder.CanDecode("a.svg") && !decoder.CanDecode("a.tiff") && !decoder.CanDecode(""), "SVG and unsupported formats are not routed through the native bitmap codec");
var rejected = false;
try
{
    decoder.Decode(Path.Combine(fixtures, "invalid.png"));
}
catch (InvalidDataException) { rejected = true; }
Check(rejected, "Malformed bitmap data fails explicitly");
foreach (var name in new[] { "truncated.ico", "corrupt-frame.ico" })
{
    rejected = false;
    try
    {
        decoder.Decode(Path.Combine(fixtures, name));
    }
    catch (InvalidDataException) { rejected = true; }
    Check(rejected, "Malformed ICO directory or PNG frame fails explicitly: " + name);
}
rejected = false;
try
{
    decoder.Decode(Path.Combine(fixtures, "missing.png"));
}
catch (FileNotFoundException) { rejected = true; }
Check(rejected, "Missing bitmap files fail explicitly");
rejected = false;
try
{
    decoder.Decode("unsupported.svg");
}
catch (NotSupportedException) { rejected = true; }
Check(rejected, "Unsupported image dispatch fails explicitly");
var independentCopies = Enumerable.Range(0, 8).AsParallel().Select(_ => decoder.Decode(Path.Combine(fixtures, "alpha.png"))).ToArray();
Check(independentCopies.All(v => v.RgbaPixels.AsSpan().SequenceEqual(independentCopies[0].RgbaPixels)), "Concurrent native decodes return independent exact buffers");
independentCopies[0].RgbaPixels[0] = 0;
Check(independentCopies[1].RgbaPixels[0] == 255, "Returned buffers remain independent after native resources retire");
using var store = new SvgStore();
var combined = new SvgImageDecoder(store, decoder);
var svgPath = Path.Combine(fixtures, "shapes.svg");
var first = combined.Decode(svgPath);
Check(first.Width == 48 && first.Height == 32 && first.RgbaPixels.Length == 48 * 32 * 4, "The managed SVG decoder runs through the shared Linux image pipeline");
for (var i = 0; i < 20; i++)
    Check(combined.Decode(svgPath).RgbaPixels.AsSpan().SequenceEqual(first.RgbaPixels), "Repeated SVG image output is stable: " + i);
var stats = store.Statistics;
Check(stats.SourceReads == 1 && stats.XmlParses == 1 && stats.SceneCompiles == 1 && stats.Rasterizations == 1 && stats.RasterCacheHits == 20,
    "SVG source/XML/scene/raster are processed once and reused");
Check(combined.Decode(Path.Combine(fixtures, "alpha.png")).RgbaPixels.AsSpan().SequenceEqual(File.ReadAllBytes(Path.Combine(fixtures, "alpha.png.rgba"))),
    "The SVG adapter preserves explicit bitmap dispatch");
var previous = UiImageTexture.ImageDecoder;
try
{
    UiImageTexture.ImageDecoder = combined;
    var texture = UiImageTexture.LoadFromFile(svgPath, new(50400));
    Check(texture.Width == 48 && texture.Height == 32, "The public image-file API accepts SVG on Linux");
    texture = UiImageTexture.LoadFromFile(Path.Combine(fixtures, "alpha.png"), new(50401));
    Check(texture.Width == 3 && texture.Height == 2, "The same public image-file API accepts PNG on Linux");
}
finally { UiImageTexture.ImageDecoder = previous; }
File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new
{
    framework = RuntimeInformation.FrameworkDescription,
    os = RuntimeInformation.OSDescription,
    architecture = RuntimeInformation.ProcessArchitecture.ToString(),
    passed = checks.Count,
    checks,
    svgStatistics = stats,
    qualification = "Native bitmap decoding and existing SVG image dispatch; actual app/GPU qualification remains separate"
}, new JsonSerializerOptions { WriteIndented = true }));
