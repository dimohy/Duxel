using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Duxel.Svg.Managed;

if (args is ["--probe-pixel", var probeSource, var probeWidth, var probeHeight, var probeX, var probeY, var probeOutput])
{
    if (File.Exists(probeOutput))
        throw new IOException("Specify a fresh pixel probe output file.");
    var document = System.Xml.Linq.XDocument.Load(probeSource);
    var scene = SvgManagedScene.Compile(document);
    using var destination = File.Create(probeOutput);
    scene.WritePixelProbe(destination, document,
        int.Parse(probeWidth, System.Globalization.CultureInfo.InvariantCulture),
        int.Parse(probeHeight, System.Globalization.CultureInfo.InvariantCulture),
        int.Parse(probeX, System.Globalization.CultureInfo.InvariantCulture),
        int.Parse(probeY, System.Globalization.CultureInfo.InvariantCulture));
    return;
}

if (args is ["--zoom-profile", var profileOutput])
{
    ZoomProfile.Run(Path.GetFullPath(profileOutput));
    return;
}

if (args is [var rasterMode, var sourcePath, var rasterWidth, var rasterHeight, var imagePath] && rasterMode is "--raster" or "--raster-on-white")
{
    if (File.Exists(imagePath))
        throw new IOException("Specify a fresh output image.");
    var width = int.Parse(rasterWidth, System.Globalization.CultureInfo.InvariantCulture);
    var height = int.Parse(rasterHeight, System.Globalization.CultureInfo.InvariantCulture);
    var scene = SvgManagedScene.Compile(System.Xml.Linq.XDocument.Load(sourcePath));
    BrowserPng.Write(imagePath, width, height, scene.Render(width, height, rasterMode == "--raster-on-white" ? Vector4.One : Vector4.Zero));
    return;
}

if (args is ["--browser", var browserOutput])
{
    Environment.ExitCode = BrowserComparison.Run(browserOutput) ? 0 : 1;
    return;
}

if (args is ["--browser-blend-backdrops", var blendOutput])
{
    Environment.ExitCode = BrowserComparison.Run(blendOutput, "BlendBackdrops") ? 0 : 1;
    return;
}

if (args is ["--browser-arc-geometry", var arcOutput])
{
    Environment.ExitCode = BrowserComparison.Run(arcOutput, "ArcGeometry") ? 0 : 1;
    return;
}

if (args is ["--browser-arc-opaque-target", var arcTargetOutput])
{
    Environment.ExitCode = BrowserComparison.Run(arcTargetOutput, "ArcGeometry", whiteTarget: true) ? 0 : 1;
    return;
}

if (args is ["--browser-root-transforms", var rootTransformOutput])
{
    Environment.ExitCode = BrowserComparison.Run(rootTransformOutput, "RootTransforms", whiteTarget: true) ? 0 : 1;
    return;
}

if (args is ["--browser-clip-storage", var clipOutput])
{
    Environment.ExitCode = BrowserComparison.Run(clipOutput, "ClipStorage") ? 0 : 1;
    return;
}

if (args is ["--browser-layer-origin", var layerOutput])
{
    Environment.ExitCode = BrowserComparison.Run(layerOutput, "LayerOrigin") ? 0 : 1;
    return;
}

if (args is ["--browser-stroke-domains", var domainOutput])
{
    Environment.ExitCode = BrowserComparison.Run(domainOutput, "StrokeDomains") ? 0 : 1;
    return;
}

if (args is ["--browser-color-storage", var storageOutput])
{
    if (Directory.Exists(storageOutput))
        throw new IOException("Specify a fresh color-storage qualification directory.");
    var opaque = BrowserComparison.Run(Path.Combine(storageOutput, "opaque"), "ColorStorage", straightAlpha: true);
    var transparent = BrowserComparison.Run(Path.Combine(storageOutput, "transparent"), Path.Combine("ColorStorage", "Transparent"), straightAlpha: true);
    Environment.ExitCode = opaque && transparent ? 0 : 1;
    return;
}

if (args.Length != 1) throw new ArgumentException("Specify a fresh result directory.");
var output = Path.GetFullPath(args[0]);
if (Directory.Exists(output)) throw new IOException("Use a fresh result directory.");
Directory.CreateDirectory(output);
List<string> checks = [];
void Check(bool condition, string text)
{
    if (!condition)
        throw new InvalidOperationException(text);
    checks.Add(text);
    Console.WriteLine("PASS " + text);
}
byte[] Paint(string path, Matrix3x2? transform = null, bool evenOdd = false)
{
    var surface = new SvgRasterSurface(32, 32);
    surface.Fill(SvgPathCompiler.Parse(path), transform ?? Matrix3x2.Identity, new(1, 0, 0, 1), evenOdd);
    return surface.CopyRgba();
}
byte Alpha(byte[] bytes, int x, int y) => bytes[(y * 32 + x) * 4 + 3];
var square = Paint("M4 4H28V28H4Z");
Check(Alpha(square, 10, 10) == 255 && Alpha(square, 2, 10) == 0 && Alpha(square, 28, 10) == 0,
    "Managed path fill respects the authored rectangle and half-open exterior");
Check(square.SequenceEqual(Paint("m4 4 24 0 0 24-24 0z")), "Relative and implicit line commands match absolute geometry");
Check(square.SequenceEqual(Paint("M4e0,4.0 L+28,4 28,28 4,28z")), "Number grammar handles signs, exponents, commas and implicit repeats");
Check(Alpha(Paint("M4 4H28V28H4Z M8 8H24V24H8Z"), 16, 16) == 255 &&
    Alpha(Paint("M4 4H28V28H4Z M8 8H24V24H8Z", evenOdd: true), 16, 16) == 0,
    "Nonzero and even-odd winding distinguish identical-direction nested contours");
Check(Alpha(Paint("M4 4H28V28H4Z M8 8V24H24V8Z"), 16, 16) == 0,
    "Nonzero winding preserves opposite-direction contour holes");
var shifted = Paint("M0 0H8V8H0Z", Matrix3x2.CreateScale(2) * Matrix3x2.CreateTranslation(8, 8));
Check(Alpha(shifted, 10, 10) == 255 && Alpha(shifted, 25, 10) == 0, "Scale and translation transform retained paths before coverage");
var half = Paint("M4.5 4H8.5V8H4.5Z");
Check(Alpha(half, 4, 5) is >= 127 and <= 128 && Alpha(half, 5, 5) == 255 && Alpha(half, 8, 5) is >= 127 and <= 128,
    "Fractional edge coverage produces antialiasing without a native raster library");
Check(Alpha(Paint("M-100-100H100V100H-100Z"), 0, 0) == 255,
    "Geometry outside the viewport clips coverage without changing its winding");
foreach (var pair in new[]
{
    ("M4 16Q16 0 28 16T4 16Z", "M4 16Q16 0 28 16Q40 32 4 16Z"),
    ("M4 16C8 0 24 0 28 16S8 32 4 16Z", "M4 16C8 0 24 0 28 16C32 32 8 32 4 16Z"),
    ("M4 16q12-16 24 0t-24 0z", "M4 16Q16 0 28 16Q40 32 4 16Z")
})
    Check(Paint(pair.Item1).SequenceEqual(Paint(pair.Item2)), "Smooth curve reflection matches explicit controls: " + pair.Item1);
var circle = Paint("M28 16A12 12 0 1 1 4 16A12 12 0 1 1 28 16Z");
Check(Alpha(circle, 16, 16) == 255 && Alpha(circle, 2, 16) == 0 && Alpha(circle, 16, 4) > 150,
    "Elliptical arc commands normalize to retained curves with a transparent exterior");
Check(circle.SequenceEqual(Paint("M28 16a12 12 0 11-24 0a12 12 0 1124 0z")),
    "Arc flags are consumed separately from following coordinate digits");
Check(Paint("M4 4A0 5 0 0 1 28 4L28 28H4Z").SequenceEqual(square),
    "A zero arc radius produces the specified straight segment");
var surfaceAlpha = new SvgRasterSurface(1, 1);
var unit = SvgPathCompiler.Parse("M0 0H1V1H0Z");
surfaceAlpha.Fill(unit, Matrix3x2.Identity, new(1, 0, 0, .5f));
surfaceAlpha.Fill(unit, Matrix3x2.Identity, new(0, 0, 1, .5f));
var rgba = surfaceAlpha.CopyRgba();
Check(rgba[0] == 85 && rgba[1] == 0 && rgba[2] == 170 && rgba[3] == 192,
    "Source-over quantizes each RGBA8 premultiplied target write before straight-alpha export");
foreach (var bad in new[] { "L1 1", "M0 0L", "M0 0A1 1 0 2 0 3 3", "M0 0L1e+ 2", "M0 0L1e99 2", "M0 0Z1 2", "M3e38 0c3e38 0 0 0-3e38 0" })
{
    var rejected = false;
    try
    {
        SvgPathCompiler.Parse(bad);
    }
    catch (InvalidDataException) { rejected = true; }
    Check(rejected, "Malformed geometry rejects before rendering: " + bad);
}
File.WriteAllBytes(Path.Combine(output, "circle.rgba"), circle);
SceneTests.Run(Check);
ConicTests.Run(Check);
PixelProbeTests.Run(Check);
GpuStrokeClassificationTests.Run(Check);
OpacityReferenceTests.Run(Check);
ShaderColorReferenceTests.Run(Check);
StrokeDashTests.Run(Check);
PaintEffectTests.Run(Check);
BlurAxisTests.Run(Check);
CoverageOracleTests.Run(Check);
CoverageOracleTests.Run(Check, "Compositing");
CoverageOracleTests.Run(Check, "PaintServers");
CoverageOracleTests.Run(Check, "StrokeJoins");
CoverageOracleTests.Run(Check, "CurveStrokes");
CoverageOracleTests.Run(Check, "Viewports");
CoverageOracleTests.Run(Check, "Analytic");
File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new Result(checks.Count, checks.ToArray()), ResultContext.Default.Result));
internal sealed record Result(int Passed, string[] Checks);
[JsonSerializable(typeof(Result))]
internal partial class ResultContext : JsonSerializerContext;
