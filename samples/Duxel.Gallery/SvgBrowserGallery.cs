using Duxel.Core;
using Duxel.Svg;
using System.Text.Json;

namespace Duxel.Gallery;

/// <summary>Loads original complex vectors and their independent browser captures only on first use.</summary>
internal sealed class SvgBrowserGallery : IDisposable
{
    private const int SourcesPerFrame = 2;
    private readonly Entry[] _entries;
    private readonly UiWidget _backgroundSwitch;
    private bool _includeBackground;
    private bool _highResolution = true;
    private bool _sourceUpdatesDeferred;
    private int? _nextSource;
    private (float Zoom, float Rotation, UiEdgeInsets Margin, float Radius, bool Background, bool HighResolution) _sourceState;
    internal IReadOnlyList<IGallerySample> Samples { get; }
    internal (UiRect Reference, UiRect Vector)? FirstPairBounds
    {
        get; private set;
    }
    internal IReadOnlyList<UiSvg> Previews
    {
        get;
    }

    internal SvgBrowserGallery(SvgStore store, SvgImageDecoder decoder, string group = "")
    {
        var _store = store;
        var _decoder = decoder;
        var backgroundDefinition = GallerySample.Define("backgroundSwitch", () => Fluent.Switch(GalleryText.Get("Include the white background in the SVG"), new UiBinding<bool>(() => _includeBackground, value => _includeBackground = value)));
        _backgroundSwitch = backgroundDefinition.Value;
        var declarations = GalleryPageSource.Declaration(nameof(SvgBrowserPair)) + "\n\n" + GalleryPageSource.Declaration(nameof(SvgBrowserReference));
        var directory = Path.Combine(AppContext.BaseDirectory, "Assets", "Svg", "Browser");
        if (group is not ("" or "ClipStorage" or "LayerOrigin" or "StrokeDomains" or "ArcGeometry"))
            throw new ArgumentOutOfRangeException(nameof(group));
        directory = Path.Combine(directory, group);
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "references.json")));
        _entries = group switch
        {
            "ArcGeometry" => [
                Create("arc-circle", GalleryText.Get("Circular arc with background compositing"), new(64, 64)),
                Create("arc-ellipse", GalleryText.Get("Elliptical arc with background compositing"), new(64, 64)),
                Create("arc-transformed", GalleryText.Get("Transformed ellipse arc with background compositing"), new(64, 64)),
                Create("arc-rotated", GalleryText.Get("Rotated arc with background compositing"), new(64, 64)),
                Create("control-cubic", GalleryText.Get("Cubic curve control with background compositing"), new(64, 64)),
                Create("control-rectangle", GalleryText.Get("Straight line control"), new(64, 64))
            ],
            "StrokeDomains" => [
                Create("thin", GalleryText.Get("Thin stroke, calibration in progress"), new(64, 64)),
                Create("thick", GalleryText.Get("Thick stroke that fills the center"), new(64, 64)),
                Create("reflected", GalleryText.Get("Reflected stroke"), new(64, 64)),
                Create("scaled", GalleryText.Get("Nonuniform scale, fine differences under review"), new(64, 64)),
                Create("opaque", GalleryText.Get("Opaque stroke"), new(64, 64))
            ],
            "LayerOrigin" => [
                Create("base", GalleryText.Get("Fill and stroke, width 8"), new(64, 64)),
            Create("stroke4", GalleryText.Get("Fill and stroke, width 4"), new(64, 64)),
            Create("stroke6", GalleryText.Get("Fill and stroke, width 6"), new(64, 64)),
            Create("translated", GalleryText.Get("Translated translucent layer"), new(64, 64)),
            Create("fractional", GalleryText.Get("Fractional coordinates and analytical stroke coverage"), new(64, 64))
            ],
            "ClipStorage" => [
                Create("base", GalleryText.Get("Basic clipping and translucency"), new(64, 64)),
            Create("source-translated", GalleryText.Get("Translated source with group opacity and clip"), new(64, 64)),
            Create("clip-translated", GalleryText.Get("Translated clip"), new(64, 64)),
            Create("clip-reflected", GalleryText.Get("Reflected clip"), new(64, 64)),
            Create("subject-inside", GalleryText.Get("Rectangle inside the clip"), new(64, 64)),
            Create("near-edge", GalleryText.Get("Subpixel edge"), new(64, 64)),
            Create("transformed-no-clip", GalleryText.Get("Transform and translucency without clipping"), new(64, 64)),
            Create("opaque-rect", GalleryText.Get("Opaque edge"), new(64, 64)),
            Create("partial-rect", GalleryText.Get("Translucent edge"), new(64, 64))
            ],
            "" => [
                Create("Red_Gallardo", GalleryText.Get("Red Gallardo: 251 paths, 279 gradients, 17 patterns, and Gaussian blur"), new(382, 186)),
            Create("TheStructorr_Lamborghini_Gallardo", GalleryText.Get("Yellow Gallardo: 132 paths, 113 gradients, and 3 patterns"), new(384, 150))
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(group))
        };
        Previews = Array.AsReadOnly(_entries.Select(entry => entry.Vector).ToArray());
        Samples = _entries.Select(entry => (IGallerySample)entry.Sample).ToArray();
        Entry Create(string name, string label, UiVector2 size)
        {
            var metadata = manifest.RootElement.GetProperty("references").EnumerateArray().Single(item =>
                item.GetProperty("file").GetString() == name + ".svg" && item.GetProperty("mode").GetString() == "image-canvas");
            var sourcePath = Path.Combine(directory, name + ".svg");
            var referencePath = Path.Combine(directory, name + ".image-canvas.browser.png");
            var bitmapDefinition = GallerySample.Define("bitmap", () => _decoder.Decode(referencePath));
            var bitmap = bitmapDefinition.Value;
            var sourceHash = metadata.GetProperty("sourceSha256").GetString()!;
            var pixelHash = metadata.GetProperty("rgbaSha256").GetString()!;
            SvgBrowserReference.Validate(sourcePath, bitmap.RgbaPixels, sourceHash, pixelHash);
            var referenceDefinition = GallerySample.Define("reference", () => Fluent.Image(new UiImageSource(bitmap)));
            var reference = referenceDefinition.Value;
            reference.Size = size;
            reference.CornerRadius = 0;
            var rasterScale = metadata.GetProperty("width").GetInt32() / size.X;
            if (rasterScale != metadata.GetProperty("height").GetInt32() / size.Y)
                throw new InvalidDataException("Browser comparison reference has incompatible aspect ratio.");
            var vectorDefinition = GallerySample.Define("vector", () => new UiSvg(_store.Load(sourcePath)) { Size = size });
            var vector = vectorDefinition.Value;
            var sample = GallerySample.Create(() => new SvgBrowserPair(reference, vector, rasterScale, () => _includeBackground));
            var factory = sample.Source;
            sample.BindSource(() => (vector.Zoom, vector.RotationDegrees, vector.Margin, vector.CornerRadius, Background : _includeBackground, HighResolution : _highResolution), state =>
            {
                var setup = $"var directory = Path.Combine(AppContext.BaseDirectory, \"Assets\", \"Svg\", \"Browser\", {GallerySample.String(group)});\n"
                    + $"var sourcePath = Path.Combine(directory, {GallerySample.String(name + ".svg")});\n"
                    + $"var referencePath = Path.Combine(directory, {GallerySample.String(name + ".image-canvas.browser.png")});\n"
                    + $"var size = new UiVector2({GallerySample.Number(size.X)}, {GallerySample.Number(size.Y)});\n"
                    + $"float rasterScale = {GallerySample.Number(rasterScale)};\nbool _includeBackground = {GallerySample.Boolean(state.Background)};\n";
                var body = bitmapDefinition.Source + "\n"
                    + $"SvgBrowserReference.Validate(sourcePath, bitmap.RgbaPixels, {GallerySample.String(sourceHash)}, {GallerySample.String(pixelHash)});\n\n"
                    + referenceDefinition.Source + "\nreference.Size = size;\nreference.CornerRadius = 0;\n\n"
                    + vectorDefinition.Source + "\n"
                    + (!state.HighResolution ? "vector.UseFixedResolutionPreview = true;\n" : "")
                    + SvgExampleSource.Transform("vector", state.Zoom, state.RotationDegrees, state.Margin, state.CornerRadius)
                    + "\n" + backgroundDefinition.Source + "\n\n" + factory.Replace("UiWidget example =", "UiWidget pair =", StringComparison.Ordinal)
                    + "\nUiWidget example = Dux.Column(new(Spacing : 8, MainAxisSize : UiMainAxisSize.Min), backgroundSwitch, pair);\nreturn (_store, example);";
                return SvgExampleSource.Factory("CreateBrowserComparison", body, images : true, setup : setup, declarations : declarations);
            });
            return new(label, reference, vector, size, rasterScale, sample);

        }
    }

    internal void Render(UiImmediateContext ui, float zoom, float rotationDegrees, UiEdgeInsets margin, float cornerRadius,
        bool synchronizeSource, bool useFixedResolutionPreview, bool highResolution = true)
    {
        _highResolution = highResolution;
        ui.TextWrapped(GalleryText.Get("The Chromium reference is on the left. The GPU-rendered SVG is on the right. Use the controls above to zoom, rotate, and change margins or corners."));
        ui.TextWrapped(GalleryText.Get("Pixel differences along outlines and fine patterns are under review. Sources, authors, and licenses are listed in Assets/Svg/Browser/ATTRIBUTION.md."));
        _backgroundSwitch.Render(ui);
        FirstPairBounds = null;
        var sources = SourceUpdateRange(ui, synchronizeSource, (zoom, rotationDegrees, margin, cornerRadius, _includeBackground, highResolution));
        for (var index = 0; index < _entries.Length; index++)
        {
            var entry = _entries[index];
            entry.Vector.Zoom = zoom;
            entry.Vector.RotationDegrees = rotationDegrees;
            entry.Vector.Margin = margin;
            entry.Vector.CornerRadius = cornerRadius;
            entry.Vector.UseFixedResolutionPreview = useFixedResolutionPreview;
            ui.Text(entry.Label);
            var measured = entry.Vector.MeasureConstrained(ui, new UiBoxConstraints());
            SvgTelemetryText.Show(ui, GalleryText.Format("SVG layout: {0:F1} × {1:F1} px (including transforms and margins)", measured.X, measured.Y), synchronizeSource);
            if (index >= sources.Start && index < sources.End)
                entry.Sample.SynchronizeSource();
            ui.RenderNaturalHeight(entry.Sample);
            FirstPairBounds ??= entry.Sample.Preview.Bounds;
        }
    }

    private (int Start, int End) SourceUpdateRange(UiImmediateContext ui, bool synchronize,
        (float Zoom, float Rotation, UiEdgeInsets Margin, float Radius, bool Background, bool HighResolution) state)
    {
        if (!ui.IsPresentationPass)
            return default;
        if (!synchronize)
        {
            _sourceUpdatesDeferred = true;
            _nextSource = null;
            return default;
        }
        if (_sourceUpdatesDeferred)
        {
            // Let the first settled frame display the SVG before rebuilding its example code.
            _sourceUpdatesDeferred = false;
            _nextSource = 0;
            ui.RequestFrameAt(ui.GetTime());
            return default;
        }
        if (_sourceState != state)
        {
            _sourceState = state;
            if (_nextSource.HasValue)
                _nextSource = 0;
        }
        if (_nextSource is not { } start)
            return (0, _entries.Length);

        var end = Math.Min(start + SourcesPerFrame, _entries.Length);
        _nextSource = end < _entries.Length ? end : null;
        if (_nextSource.HasValue)
            ui.RequestFrameAt(ui.GetTime());
        return (start, end);
    }

    public void Dispose()
    {
        _backgroundSwitch.Dispose();
        foreach (var entry in _entries)
        {
            entry.Sample.Dispose();
        }
    }

    private sealed record Entry(string Label, UiFluentImage Reference, UiSvg Vector, UiVector2 Size, float RasterScale, GallerySample<SvgBrowserPair> Sample);
}
