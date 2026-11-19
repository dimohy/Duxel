using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Duxel.Core;
using Duxel.Svg;

if (args.Length != 1) throw new ArgumentException("Expected an owned output directory.");
var output = Path.GetFullPath(args[0]);
Directory.CreateDirectory(output);
var passed = new List<string>();
void Check(bool condition, string name)
{
    if (!condition)
        throw new InvalidOperationException(name);
    passed.Add(name);
    Console.WriteLine("PASS " + name);
}
const string original = "<svg xmlns='http://www.w3.org/2000/svg' width='64' height='48' viewBox='0 0 64 48'><rect x='4' y='4' width='24' height='16' fill='#e03020'/><rect x='32' y='24' width='16' height='16' fill='#2080e0' opacity='.5'/></svg>";
var path = Path.Combine(output, "source.svg");
File.WriteAllText(path, original);
using var store = new SvgStore();
var asset = store.Load(path);
var sameAsset = true;
for (var i = 0; i < 100; i++) sameAsset &= ReferenceEquals(asset, store.Load(path));
Check(sameAsset, "one hundred repeated loads return the retained resource");
Check(asset.IntrinsicSize == new UiVector2(64, 48) && store.Statistics is { SourceReads: 1, XmlParses: 1, SceneCompiles: 1 },
    "one hundred load requests read and parse source once and compile one scene");
var raster = asset.Rasterize(64, 48);
var sameRaster = true;
for (var i = 0; i < 100; i++) sameRaster &= ReferenceEquals(raster, asset.Rasterize(64, 48));
Check(sameRaster, "one hundred unchanged raster requests reuse immutable pixels");
Check(store.Statistics is { XmlParses: 1, SceneCompiles: 1, Rasterizations: 1, RasterCacheHits: 100 },
    "unchanged rendering does not repeat XML, scene compilation or rasterization");
var replayStatistics = store.Statistics;
byte Channel(UiSvgRaster image, int x, int y, int channel) => image.Pixels.Span[(y * image.Width + x) * 4 + channel];
Check(Channel(raster, 12, 10, 0) == 224 && Channel(raster, 12, 10, 1) == 48 && Channel(raster, 0, 0, 3) == 0,
    "actual SVG raster contains the expected opaque color and transparent exterior");
// Native browser capture is straight RGBA(32,128,225,127), which encodes premultiplied RGBA(16,64,112,127).
var translucentAlpha = Channel(raster, 36, 28, 3);
Check(translucentAlpha == 127 && (Channel(raster, 36, 28, 0) * translucentAlpha + 127) / 255 == 16
    && (Channel(raster, 36, 28, 1) * translucentAlpha + 127) / 255 == 64
    && (Channel(raster, 36, 28, 2) * translucentAlpha + 127) / 255 == 112,
    "straight SVG export encodes the exact independently captured premultiplied browser pixel");
var enlarged = asset.Rasterize(256, 192);
Check(Channel(enlarged, 48, 40, 0) == 224 && store.Statistics is { XmlParses: 1, SceneCompiles: 1, Rasterizations: 2 }
    && ReferenceEquals(raster, asset.Rasterize(64, 48)), "scale changes rasterize the retained scene and returning to a cached size reuses pixels without XML");
var square = asset.Rasterize(128, 128);
Check(Channel(square, 24, 36, 0) == 224 && Channel(square, 24, 4, 3) == 0, "raster viewport preserves aspect ratio and centers the SVG");
var copy = raster.CopyImageData();
copy.RgbaPixels[0] = 255;
Check(raster.Pixels.Span[0] == 0, "bitmap decoder copies cannot mutate cached SVG pixels");
var notifications = 0;
asset.Changed += () =>
{
    notifications++;
    var otherThread = Task.Run(() => store.Statistics);
    Check(asset.Revision == 1 && otherThread.Wait(TimeSpan.FromSeconds(2)),
        "source change is committed before notifying consumers outside the store lock");
};
var beforeIdentical = store.Statistics;
Check(ReferenceEquals(asset, store.Reload(path)) && notifications == 0 && store.Statistics.XmlParses == beforeIdentical.XmlParses
    && ReferenceEquals(raster, asset.Rasterize(64, 48)), "explicit reload of identical bytes preserves revision, scene and rasters");
File.WriteAllText(path, original.Replace("#e03020", "#20c060", StringComparison.Ordinal));
store.Reload(path);
var changed = asset.Rasterize(64, 48);
Check(notifications == 1 && asset.Revision == 1 && Channel(changed, 12, 10, 0) == 32
    && Channel(raster, 12, 10, 0) == 224 && !ReferenceEquals(raster, changed),
    "changed source invalidates only its resource while old frame snapshots remain immutable");
var currentRevision = asset.Revision;
File.WriteAllText(path, "<svg>");
var invalidReload = false;
try { store.Reload(path); } catch (System.Xml.XmlException) { invalidReload = true; }
Check(invalidReload && asset.Revision == currentRevision && notifications == 1 && ReferenceEquals(changed, asset.Rasterize(64, 48)),
    "failed source reload reports its error and preserves the last committed resource");
File.WriteAllText(path, original.Replace("#e03020", "#20c060", StringComparison.Ordinal));
var bitmap = new BitmapDecoder();
var decoder = new SvgImageDecoder(store, bitmap);
Check(decoder.CanDecode(path) && decoder.Decode(path).Width == 64 && bitmap.Calls == 0, "SVG image decoding consumes the same retained store resource");
var previousDecoder = UiImageTexture.ImageDecoder;
try
{
    UiImageTexture.ImageDecoder = decoder;
    var texture = UiImageTexture.LoadFromFile(path, new(101));
    Check(texture.Width == 64 && texture.Height == 48 && store.Statistics.SceneCompiles == 2,
        "the ordinary image-file API accepts SVG without another XML parse");
}
finally { UiImageTexture.ImageDecoder = previousDecoder; }
decoder.Decode("ordinary.png");
Check(bitmap.Calls == 1, "format dispatch preserves the supplied bitmap decoder path");
using (var limited = new SvgStore(maximumRasterBytes: 64 * 48 * 4))
{
    var item = limited.Load(path);
    var first = item.Rasterize(64, 48);
    item.Rasterize(32, 24);
    var replacement = item.Rasterize(64, 48);
    Check(limited.Statistics is { XmlParses: 1, SceneCompiles: 1, Rasterizations: 3, CachedRasterBytes: 12288 }
        && !ReferenceEquals(first, replacement) && first.Pixels.Span.SequenceEqual(replacement.Pixels.Span),
        "bounded raster eviction recompiles no XML and reproduces identical pixels");
    var rejected = false;
    try
    {
        item.Rasterize(1000, 1000);
    }
    catch (ArgumentOutOfRangeException) { rejected = true; }
    Check(rejected && limited.Statistics.Rasterizations == 3, "oversized raster requests fail before allocation or rendering");
}
var concurrentStatistics = default(SvgStoreStatistics);
using (var concurrent = new SvgStore())
{
    var requests = Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
    {
        UiSvgRaster? result = null;
        for (var i = 0; i < 100; i++)
            result = concurrent.Load(path).Rasterize(64, 48);
        return result!;
    })).ToArray();
    var results = await Task.WhenAll(requests);
    concurrentStatistics = concurrent.Statistics;
    Check(results.All(result => ReferenceEquals(result, results[0]))
        && concurrent.Statistics is { SourceReads: 1, XmlParses: 1, SceneCompiles: 1, Rasterizations: 1, RasterCacheHits: 799 },
        "eight concurrent consumers still parse/compile/rasterize once and share the same immutable result");
}
var failures = new[]
{
    "<!DOCTYPE svg [<!ENTITY local SYSTEM 'file:///does-not-exist'>]><svg xmlns='http://www.w3.org/2000/svg'>&local;</svg>",
    "<svg xmlns='http://www.w3.org/2000/svg'><script>alert(1)</script></svg>",
    "<svg xmlns='http://www.w3.org/2000/svg'><image href='https://example.invalid/no-network.png'/></svg>",
    "<svg xmlns='http://www.w3.org/2000/svg'><rect fill='url(#missing)'/></svg>",
    "<svg xmlns='http://www.w3.org/2000/svg'><text>Missing font contract</text></svg>",
    "<svg xmlns='http://www.w3.org/2000/svg'><rect style='fill:u\\72l(https://example.invalid/no-network.svg)'/></svg>"
};
var features = Path.Combine(output, "features.svg");
File.WriteAllText(features, "<svg xmlns='http://www.w3.org/2000/svg' width='64' height='48' viewBox='0 0 64 48'><defs><linearGradient id='g'><stop offset='0' stop-color='#ff0000'/><stop offset='1' stop-color='#0000ff'/></linearGradient><clipPath id='c'><rect x='8' y='8' width='40' height='24'/></clipPath></defs><g clip-path='url(#c)'><path d='M0 0H64V48H0Z' fill='url(#g)'/></g></svg>");
var featureRaster = store.Load(features).Rasterize(64, 48);
Check(Channel(featureRaster, 0, 0, 3) == 0 && Channel(featureRaster, 12, 16, 3) == 255
    && Channel(featureRaster, 12, 16, 0) > Channel(featureRaster, 40, 16, 0),
    "shared SVG compilation preserves path geometry, gradients and clip-path rather than reparsing per raster");
var viewBox = Path.Combine(output, "viewbox.svg");
File.WriteAllText(viewBox, "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24'><circle cx='12' cy='12' r='10' fill='#20c060'/></svg>");
var viewBoxAsset = store.Load(viewBox);
Check(viewBoxAsset.IntrinsicSize == new UiVector2(24, 24) && Channel(viewBoxAsset.Rasterize(48, 48), 24, 24, 1) == 192,
    "viewBox-only SVG icons retain intrinsic geometry and scale without inventing dimensions");
for (var i = 0; i < failures.Length; i++)
{
    var invalidPath = Path.Combine(output, "invalid-" + i + ".svg");
    File.WriteAllText(invalidPath, failures[i]);
    var rejected = false;
    try
    {
        store.Load(invalidPath);
    }
    catch (Exception error) when (error is System.Xml.XmlException or NotSupportedException or InvalidDataException) { rejected = true; }
    Check(rejected, "invalid or unsupported SVG reports its exact input error before publication : " + i);
}
var widgetStatistics = SvgWidgetTests.Run(output, Check);
ImagePipelineTests.Run(Check);
var fontStatistics = SvgFontTests.Run(output, Check);
SvgLifetimeTests.Run(output, Check);
SvgFeatureTests.Run(output, Check);
SvgBrowserCorpusTests.Run(Check);
SvgTextReferenceTests.Run(output, Check);
var disposedAsset = store.Load(path);
store.Dispose();
var unavailable = false;
try { disposedAsset.Rasterize(64, 48); } catch (ObjectDisposedException) { unavailable = true; }
Check(unavailable && store.Statistics.CachedRasterBytes == 0, "store disposal releases owned scenes/cache and rejects stale consumers");
File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new Result(RuntimeInformation.FrameworkDescription,
    passed.Count, passed.ToArray(), replayStatistics, concurrentStatistics, widgetStatistics, fontStatistics, false), ResultJson.Default.Result));

internal sealed class BitmapDecoder : IUiImageDecoder
{
    internal int Calls;
    public bool CanDecode(string path) => Path.GetExtension(path) == ".png";
    public UiImageData Decode(string path)
    {
        Calls++;
        return new(1, 1, [1, 2, 3, 255]);
    }
}
internal sealed record Result(string Framework, int Passed, string[] Checks, SvgStoreStatistics Replay, SvgStoreStatistics Concurrent,
    UiSvgTextureStatistics Widget, SvgFontStatistics FontReplay, bool NativeAotQualified);
[JsonSerializable(typeof(Result))]
internal partial class ResultJson : JsonSerializerContext;
