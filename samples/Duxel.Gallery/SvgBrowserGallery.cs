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
        var backgroundDefinition = GallerySample.Define("backgroundSwitch", () => Fluent.Switch("흰색 배경을 SVG에 포함", new UiBinding<bool>(() => _includeBackground, value => _includeBackground = value)));
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
                Create("arc-circle", "원형 호 · 배경 합성 비교", new(64, 64)),
                Create("arc-ellipse", "타원 호 · 배경 합성 비교", new(64, 64)),
                Create("arc-transformed", "변환된 타원 호 · 배경 합성 비교", new(64, 64)),
                Create("arc-rotated", "회전된 호 · 배경 합성 비교", new(64, 64)),
                Create("control-cubic", "Cubic 대조군 · 배경 합성 비교", new(64, 64)),
                Create("control-rectangle", "직선 대조군", new(64, 64))
            ],
            "StrokeDomains" => [
                Create("thin", "얇은 테두리 · 보정 중", new(64, 64)),
                Create("thick", "중앙이 채워지는 두꺼운 테두리", new(64, 64)),
                Create("reflected", "반사된 테두리", new(64, 64)),
                Create("scaled", "비균등 배율 · 미세 차이 검증 중", new(64, 64)),
                Create("opaque", "불투명 테두리", new(64, 64))
            ],
            "LayerOrigin" => [
                Create("base", "채우기·테두리 · 폭 8", new(64, 64)),
            Create("stroke4", "채우기·테두리 · 폭 4", new(64, 64)),
            Create("stroke6", "채우기·테두리 · 폭 6", new(64, 64)),
            Create("translated", "이동된 반투명 레이어", new(64, 64)),
            Create("fractional", "분수 좌표 · 분석적 테두리 커버리지", new(64, 64))
            ],
            "ClipStorage" => [
                Create("base", "기본 클립·반투명", new(64, 64)),
            Create("source-translated", "이동된 소스 · 그룹 불투명도·클립", new(64, 64)),
            Create("clip-translated", "이동된 클립", new(64, 64)),
            Create("clip-reflected", "반사된 클립", new(64, 64)),
            Create("subject-inside", "클립 안의 사각형", new(64, 64)),
            Create("near-edge", "서브픽셀 경계", new(64, 64)),
            Create("transformed-no-clip", "변환·반투명 저장", new(64, 64)),
            Create("opaque-rect", "불투명 모서리", new(64, 64)),
            Create("partial-rect", "반투명 모서리", new(64, 64))
            ],
            "" => [
                Create("Red_Gallardo", "빨간 Gallardo · 경로 251 / 그라데이션 279 / 패턴 17 / Gaussian blur", new(382, 186)),
            Create("TheStructorr_Lamborghini_Gallardo", "노란 Gallardo · 경로 132 / 그라데이션 113 / 패턴 3", new(384, 150))
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
        ui.TextWrapped("왼쪽은 Chromium 기준 이미지, 오른쪽은 GPU에서 그린 SVG입니다. 위의 조절기로 확대·회전하거나 여백과 모서리를 바꿔 보세요.");
        ui.TextWrapped("외곽선·미세 패턴의 픽셀 차이는 검증 중입니다. 원본·저작자·라이선스 : Assets/Svg/Browser/ATTRIBUTION.md");
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
            SvgTelemetryText.Show(ui, $"SVG 배치 : {measured.X:F1} × {measured.Y:F1} px (변환·여백 포함)", synchronizeSource);
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
