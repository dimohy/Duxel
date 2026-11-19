using System.Numerics;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using Duxel.Svg;
using Duxel.Svg.Managed;

if (args.Length != 2) throw new ArgumentException("Specify a fresh result directory and an explicit font file.");
if (Directory.Exists(args[0])) throw new IOException("Use a fresh result directory.");
Directory.CreateDirectory(args[0]);
List<string> checks = [];
void Check(bool condition, string text)
{
    if (!condition)
        throw new InvalidOperationException(text);
    checks.Add(text);
    Console.WriteLine("PASS " + text);
}
var bytes = File.ReadAllBytes(args[1]);
using var font = SvgNativeFont.Create(bytes, 0);
Check(OperatingSystem.IsWindows() ? font is SvgDirectWriteFont : font is SvgHarfBuzzFont,
    "The font factory uses DirectWrite on Windows and HarfBuzz on Linux");
if (OperatingSystem.IsWindows())
    Check(!Process.GetCurrentProcess().Modules.Cast<ProcessModule>().Any(module =>
        module.ModuleName.Contains("HarfBuzz", StringComparison.OrdinalIgnoreCase) || module.ModuleName.Contains("Skia", StringComparison.OrdinalIgnoreCase)),
        "Windows font processing loads no HarfBuzz or Skia native library");
Check(font.UnitsPerEm > 0 && font.Weight == 400 && font.Width == 5, "Explicit font bytes expose actual OpenType units and regular style metadata");
Check(font.Contains('A') && !font.Contains(0x0378), "Glyph coverage is checked against the registered font rather than installed font discovery");
var glyphs = font.Shape("ABC", false);
Check(glyphs.Length == 3 && glyphs.All(g => g.Glyph != 0 && g.Advance.X > 0 && g.Outline.Segments.Length > 0),
    "OpenType shaping and native draw callbacks return retained glyph outlines without Skia");
var surface = new SvgRasterSurface(128, 64);
var x = 4f;
var scale = 36f / font.UnitsPerEm;
foreach (var glyph in glyphs)
{
    surface.Fill(glyph.Outline, Matrix3x2.CreateScale(scale) * Matrix3x2.CreateTranslation(x, 48), new(1, 0, 0, 1));
    x += glyph.Advance.X * scale;
}
var pixels = surface.CopyRgba();
Check(pixels.Where((_, index) => index % 4 == 3).Any(alpha => alpha == 255), "Managed SVG coverage paints actual shaped font outlines");
var repeated = font.Shape("ABC", false);
Check(ReferenceEquals(glyphs[0].Outline, repeated[0].Outline) && glyphs[0].Advance == repeated[0].Advance,
    "Repeated shaping reuses immutable per-font glyph geometry");
var arabic = font.Shape("سلام", true);
Check(arabic.Length > 0 && arabic.All(g => g.Glyph != 0) && arabic[0].Cluster > arabic[^1].Cluster,
    "Arabic shaping preserves right-to-left glyph order and registered coverage");
bytes.AsSpan().Clear();
Check(font.Shape("ABC", false)[0].Advance == glyphs[0].Advance, "The native font owns a snapshot independently of mutated source bytes");
var invalid = false;
try { using var bad = SvgNativeFont.Create([0, 1, 2, 3], 0); } catch (InvalidDataException) { invalid = true; }
Check(invalid, "Invalid font bytes reject instead of choosing another installed font");
font.Dispose();
var disposed = false;
try { font.Shape("ABC", false); } catch (ObjectDisposedException) { disposed = true; }
Check(disposed, "Disposed font handles reject further native access");
using var catalog = new SvgFontCatalog(new("UI", [new("UI", args[1])]));
var compiler = new SvgManagedTextCompiler(catalog);
SvgManagedScene Scene(string body) => SvgManagedScene.Compile(XDocument.Parse("<svg xmlns='http://www.w3.org/2000/svg' width='200' height='80'>" + body + "</svg>"), compiler);
var textScene = Scene("<text x='8' y='52' font-size='36' fill='#e03020'>ABC</text>");
var textPixels = textScene.Render(200, 80);
Check(textPixels.Where((_, index) => index % 4 == 3).Count(alpha => alpha > 0) > 400,
    "The compiled SVG scene renders explicit-font text through managed geometry");
var statistics = catalog.Statistics;
textScene.Render(400, 160);
Check(catalog.Statistics == statistics && textScene.Render(200, 80).SequenceEqual(textPixels),
    "Scaling and replay perform no font lookup, glyph coverage validation or shaping after compilation");
var reference = Scene("<defs><text id='label' fill='blue' font-size='8'>ABC</text></defs><text x='8' y='52' font-size='36' fill='#e03020'><tref href='#label'/></text>");
Check(reference.Render(200, 80).SequenceEqual(textPixels), "Local text references retain referring styles and match literal text pixels");
var hiddenSpan = Scene("<text x='8' y='52' font-size='36'><tspan visibility='hidden'><tspan visibility='unset'>A</tspan></tspan>B</text>");
var transparentSpan = Scene("<text x='8' y='52' font-size='36'><tspan fill-opacity='0'>A</tspan>B</text>");
Check(hiddenSpan.Render(200, 80).SequenceEqual(transparentSpan.Render(200, 80)),
    "Native font spans preserve hidden inheritance and subsequent glyph advances");
var curved = Scene("<defs><path id='curve' d='M8 64 Q100 0 190 64'/></defs><text font-size='24'><textPath href='#curve'>ABC SVG</textPath></text>");
Check(curved.Render(200, 80).Where((_, index) => index % 4 == 3).Any(alpha => alpha == 255),
    "Text on a retained path follows shaped advances and baseline tangents");
var rejectedStyle = false;
try { Scene("<text font-size='36' font-weight='900'>ABC</text>"); } catch (InvalidDataException) { rejectedStyle = true; }
Check(rejectedStyle, "Unregistered font styles reject before publishing a text scene");
const string embeddedPng = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAABHNCSVQICAgIfAhkiAAAAA1JREFUCJljeGCg0AAABNUBsTmap/UAAAAASUVORK5CYII=";
var decodedBitmap = new SvgEmbeddedImageDecoder().Decode(Convert.FromBase64String(embeddedPng));
Check(decodedBitmap.Rgba.SequenceEqual(new byte[] { 224, 48, 32, 128 }),
    "Platform bitmap decoding preserves original straight RGBA before SVG compositing");
var bitmap = SvgManagedScene.Compile(XDocument.Parse("<svg xmlns='http://www.w3.org/2000/svg' width='16' height='16'><image width='16' height='16' href='data:image/png;base64," + embeddedPng + "'/></svg>"), imageDecoder: new SvgEmbeddedImageDecoder()).Render(16, 16);
Check(bitmap[0] == 223 && bitmap[1] == 48 && bitmap[2] == 32 && bitmap[3] == 128,
    "Platform decoding followed by SVG RGBA8 compositing matches the independent browser capture");
var hiddenBitmap = SvgManagedScene.Compile(XDocument.Parse("<svg xmlns='http://www.w3.org/2000/svg' width='16' height='16'><image width='16' height='16' visibility='hidden' href='data:image/png;base64," + embeddedPng + "'/></svg>"), imageDecoder: new SvgEmbeddedImageDecoder()).Render(16, 16);
Check(hiddenBitmap.All(value => value == 0), "Hidden native-decoded SVG images retain their document geometry without painting");
var badBitmap = false;
try { new SvgEmbeddedImageDecoder().Decode([0, 1, 2, 3]); } catch (InvalidDataException) { badBitmap = true; }
Check(badBitmap, "Invalid embedded bitmap bytes report an input error without publishing an image");
File.WriteAllBytes(Path.Combine(args[0], "glyphs.rgba"), pixels);
File.WriteAllText(Path.Combine(args[0], "result.json"), JsonSerializer.Serialize(new Result(checks.Count, checks.ToArray()), ResultContext.Default.Result));
internal sealed record Result(int Passed, string[] Checks);
[JsonSerializable(typeof(Result))]
internal partial class ResultContext : JsonSerializerContext;
