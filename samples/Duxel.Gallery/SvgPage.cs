using Duxel.Core;
using Duxel.Svg;

namespace Duxel.Gallery;

internal sealed class SvgPage(ObserverServices services) : IObserverDocumentPage, IGallerySourcePage
{
    internal static string BrowserResolutionLabel => GalleryText.Get("Redraw at high resolution when zoomed");
    private Content? _content;
    public string Title => "SVG";
    public string Description => "The gallery parses SVG once and renders it with Vulkan. Zooming and rotation reuse the prepared paths.";
    internal SvgStoreStatistics? Statistics => _content?.Statistics;
    internal UiSvg? Preview => _content?.Preview;
    internal IReadOnlyList<UiSvg> BrowserPreviews => _content?.BrowserPreviews ?? [];
    internal IReadOnlyList<UiSvg> ArcPreviews => _content?.ArcPreviews ?? [];
    internal (UiRect Reference, UiRect Vector)? LayerPairBounds => _content?.LayerPairBounds;
    internal UiFluentImage? Bitmap => _content?.Bitmap;
    internal IReadOnlyList<IGallerySample> PrimarySamples => _content?.PrimarySamples ?? [];
    public IReadOnlyList<IGallerySample> Samples => (_content ??= new(services)).Samples;
    bool IGallerySourcePage.SynchronizesOwnSources => true;
    internal SvgFontStatistics? FontStatistics => _content?.FontStatistics;
    internal void ConfigureBrowserPreview(float zoom, float rotation = 0, bool highResolution = false) =>
        (_content ??= new(services)).ConfigureBrowserPreview(zoom, rotation, highResolution);
    internal void ConfigureFeaturePreview() => (_content ??= new(services)).ConfigureFeaturePreview();
    internal void ConfigureArcPreview(bool coalescePointerMoves = true) =>
        (_content ??= new(services)).ConfigureArcPreview(coalescePointerMoves);
    internal UiSvgTextureStatistics TextureStatistics
    {
        get; private set;
    }
    public void Render(UiImmediateContext ui) => Render(ui, header : null);
    void IObserverDocumentPage.RenderDocument(UiImmediateContext ui, UiWidget header) => Render(ui, header);

    private void Render(UiImmediateContext ui, UiWidget? header)
    {
        var available = ui.GetContentRegionAvail();
        if (ui.BeginChild("observer-svg-page", new(available.X, MathF.Max(1, available.Y - 8)), true, coalesceDocumentScrollInputs : true))
        {
            if (header is not null)
                ui.RenderNaturalHeight(header);
            (_content ??= new(services)).Render(ui);
            TextureStatistics = ui.SvgTextureStatistics;
        }
        ui.EndChild();
    }
    public void Dispose() => _content?.Dispose();

    private sealed class Content : IDisposable

    {
        private const double SourceUpdateDelaySeconds = 0.12;
        private (float Zoom, float Rotation, float Margin, float Radius)? _lastSourceTransform;
        private double _sourceUpdateAt;
        private bool _transformDragging;
        private bool _synchronizeTransformSources;
        private UiVector2? _lastViewportOrigin;
        private double _viewportSettlesAt;
        private bool _useBrowserPreview;
        private readonly ObserverServices _services;
        private readonly SvgStore _store;
        private readonly UiSvg _text;
        private readonly UiSvg _features;
        private readonly GallerySample<UiWrap> _comparisonSample;
        private readonly GallerySample<UiSvg> _textSample;
        private readonly GallerySample<UiSvg> _featureSample;
        internal IReadOnlyList<IGallerySample> PrimarySamples { get; }
        private readonly List<IGallerySample> _samples = [];
        private string? _sourceSelection;
        internal IReadOnlyList<IGallerySample> Samples
        {
            get { RefreshSources(); return _samples; }
        }
        private readonly GallerySample<UiFluentTabList> _tabsSample;
        private readonly GallerySample<UiMenu<UiFluentMenuPresentation>> _menuSample;
        private readonly GallerySample<UiWidget> _iconSample;
        private readonly Action<UiImmediateContext> _renderComparison;
        private readonly Action<UiImmediateContext> _renderFeatures;
        private readonly Action<UiImmediateContext> _renderBrowser;
        private readonly Action<UiImmediateContext> _renderClips;
        private readonly Action<UiImmediateContext> _renderLayers;
        private readonly Action<UiImmediateContext> _renderDomains;
        private readonly Action<UiImmediateContext> _renderArcs;
        private SvgBrowserGallery? _browser;
        private SvgBrowserGallery? _clips;
        private SvgBrowserGallery? _layers;
        private SvgBrowserGallery? _domains;
        private SvgBrowserGallery? _arcs;
        private readonly UiSvgIcon _icon;
        private readonly UiWidget _standaloneIcon;
        private readonly UiFluentImage _bitmap;
        private readonly SvgImageDecoder _decoder;
        private readonly UiWidget _controls;
        private readonly UiSlider<UiFluentSliderBehavior, UiFluentSliderPresentation>[] _transformSliders;
        private readonly UiWidget _hint = Fluent.Body1(GalleryText.Get("SVG widgets and icons draw paths and effects on the GPU. Zooming, rotating, and returning to this page reuse the parsed XML. The standard image example converts SVG to a bitmap."));
        private readonly UiFluentTabList _tabs;
        private readonly UiMenu<UiFluentMenuPresentation> _menu;
        private readonly UiState<float> _zoom = new(1);
        private readonly UiState<float> _rotation = new(0);
        private readonly UiState<float> _margin = new(0);
        private readonly UiState<float> _radius = new(0);
        private readonly UiState<bool> _browserHighResolution = new(false);
        private readonly UiWidget _browserResolutionSwitch;
        private readonly UiState<string?> _selection = new("first");
        private readonly string[] _paths;
        private string? _action;
        private string _reload = GalleryText.Get("Select Reload after the file changes.");
        internal UiSvg Preview
        {
            get;
        }
        internal UiFluentImage Bitmap => _bitmap;
        internal IReadOnlyList<UiSvg> BrowserPreviews => _browser?.Previews ?? [];
        internal IReadOnlyList<UiSvg> ArcPreviews => _arcs?.Previews ?? [];
        internal (UiRect Reference, UiRect Vector)? LayerPairBounds => _layers?.FirstPairBounds;
        internal SvgStoreStatistics Statistics => _store.Statistics;
        internal SvgFontStatistics FontStatistics => _store.FontStatistics;
        internal void ConfigureBrowserPreview(float zoom, float rotation, bool highResolution)
        {
            if (!float.IsFinite(zoom) || zoom < .25f || zoom > 4)
                throw new ArgumentOutOfRangeException(nameof(zoom));
            _selection.Value = "browser";
            _browserHighResolution.Value = highResolution;
            _zoom.Value = zoom;
            _rotation.Value = rotation;
        }
        internal void ConfigureFeaturePreview() => _selection.Value = "second";
        internal void ConfigureArcPreview(bool coalescePointerMoves)
        {
            _selection.Value = "arcs";
            foreach (var slider in _transformSliders)
                slider.Options = slider.Options with { CoalescePointerMoves = coalescePointerMoves };
        }

        internal Content(ObserverServices services)
        {
            _browserResolutionSwitch = Fluent.Switch(BrowserResolutionLabel, _browserHighResolution);
            _services = services;
            _store = new(fonts: _services.SvgFonts);
            _paths = SvgExampleSource.AssetNames
                .Select(name => Path.Combine(AppContext.BaseDirectory, "Assets", "Svg", name)).ToArray();
            _decoder = new(_store, _services.ImageDecoder);
            var bitmapDefinition = GallerySample.Define("bitmap", () => Fluent.Image(new UiImageSource(_decoder.Decode(_paths[0]))));
            _bitmap = bitmapDefinition.Value;
            _bitmap.Size = new(160, 100);
            var vectorDefinition = GallerySample.Define("vector", () => new UiSvg(_store.Load(_paths[0])) { Size = new(320, 200) });
            Preview = vectorDefinition.Value;
            var bitmap = _bitmap;
            var vector = Preview;
            _comparisonSample = GallerySample.Create(() => Dux.Wrap(new(Spacing : 8, RunSpacing : 8), bitmap, vector));
            var comparisonFactory = _comparisonSample.Source;
            _comparisonSample.BindSource(() => (_zoom.Value, Rotation : _rotation.Value, Margin : _margin.Value, Radius : _radius.Value, _bitmap.Fit), state =>
                SvgExampleSource.Factory("CreateImageComparison", bitmapDefinition.Source + "\nbitmap.Size = new(160, 100);\n"
                    + $"bitmap.Fit = UiImageFit.{state.Fit};\nbitmap.CornerRadius = {GallerySample.Number(state.Radius)};\n\n"
                    + vectorDefinition.Source + "\n" + SvgExampleSource.Transform("vector", state.Value, state.Rotation, new(state.Margin, 0, 0, 0), state.Radius)
                    + "\n" + comparisonFactory + "\nreturn (_store, example);", images : true));
            _textSample = GallerySample.Create(() => new UiSvg(_store.Load(_paths[3])) { Size = new(400, 70) });
            _text = _textSample.Preview;
            var textFactory = _textSample.Source;
            _textSample.BindSource(() => (_zoom.Value, Rotation : _rotation.Value, Margin : _margin.Value), state =>
                SvgExampleSource.Factory("CreateSvgText", textFactory + "\n"
                    + SvgExampleSource.Transform("((UiSvg)example)", state.Value, state.Rotation, new(state.Margin, 0, 0, 0), null) + "\nreturn (_store, example);"));
            _featureSample = GallerySample.Create(() => new UiSvg(_store.Load(_paths[4])) { Size = new(480, 240) });
            _features = _featureSample.Preview;
            var featureFactory = _featureSample.Source;
            _featureSample.BindSource(() => (_zoom.Value, Rotation : _rotation.Value, Margin : _margin.Value), state =>
                SvgExampleSource.Factory("CreateSvgFeatures", featureFactory + "\n"
                    + SvgExampleSource.Transform("((UiSvg)example)", state.Value, state.Rotation, new(state.Margin, 0, 0, 0), null) + "\nreturn (_store, example);"));
            PrimarySamples = [_comparisonSample, _textSample, _featureSample];
            _renderComparison = RenderComparison;
            _renderFeatures = RenderFeatures;
            _renderBrowser = RenderBrowser;
            _renderClips = ui => (_clips ??= new(_store, _decoder, "ClipStorage")).Render(ui,
                _zoom.Value, _rotation.Value, new(_margin.Value, 0, 0, 0), _radius.Value, _synchronizeTransformSources, _useBrowserPreview);
            _renderLayers = ui => (_layers ??= new(_store, _decoder, "LayerOrigin")).Render(ui,
                _zoom.Value, _rotation.Value, new(_margin.Value, 0, 0, 0), _radius.Value, _synchronizeTransformSources, _useBrowserPreview);
            _renderDomains = ui => (_domains ??= new(_store, _decoder, "StrokeDomains")).Render(ui,
                _zoom.Value, _rotation.Value, new(_margin.Value, 0, 0, 0), _radius.Value, _synchronizeTransformSources, _useBrowserPreview);
            _renderArcs = ui => (_arcs ??= new(_store, _decoder, "ArcGeometry")).Render(ui,
                _zoom.Value, _rotation.Value, new(_margin.Value, 0, 0, 0), _radius.Value, _synchronizeTransformSources, _useBrowserPreview);
            var iconDefinition = GallerySample.Define("icon", () => new UiSvgIcon(_store.Load(_paths[1]), _store.Load(_paths[2]), UiSvgIconColorMode.MultiplyForeground));
            var icon = iconDefinition.Value;
            _icon = icon;
            _iconSample = GallerySample.Create<UiWidget>(() => Fluent.Icon(icon.MenuIcon, new(32, 32), new(80, 190, 240)));
            _standaloneIcon = _iconSample.Preview;
            var standaloneFactory = _iconSample.Source;
            _iconSample.BindSource(() => 0, _ => SvgExampleSource.Factory("CreateSvgIcon", iconDefinition.Source + "\n\n"
                + standaloneFactory + "\nreturn (_store, icon, example);", icons : true));
            _tabsSample = GallerySample.Create(() => Fluent.TabList(GalleryText.Get("SVG icon tabs"), _selection.Binding,
            new UiTabOption[]
            {
                new("first", GalleryText.Get("Images and text"), Icon : icon.TabIcon),
                new("second", GalleryText.Get("Document features"), Icon : icon.TabIcon),
                new("browser", GalleryText.Get("Browser comparison"), Icon : icon.TabIcon),
                new("clips", GalleryText.Get("Clips and edges"), Icon : icon.TabIcon),
                new("layers", GalleryText.Get("Layers and strokes"), Icon : icon.TabIcon),
                new("domains", GalleryText.Get("Stroke coverage"), Icon : icon.TabIcon),
                new("arcs", GalleryText.Get("Arcs and curves"), Icon : icon.TabIcon)
            }));
            _tabs = _tabsSample.Preview;
            var tabsFactory = _tabsSample.Source;
            _tabsSample.BindSource(() => _selection.Value, value => SvgExampleSource.Factory("CreateSvgIconTabs",
                $"var _selection = new UiState<string?>({GallerySample.String(value)});\n\n" + iconDefinition.Source
                + "\n\n" + tabsFactory + "\nreturn (_store, icon, example);", icons : true));
            _menuSample = GallerySample.Create<UiMenu<UiFluentMenuPresentation>>(() => Fluent.Menu(GalleryText.Get("SVG icon menu"),
                [new("svg", GalleryText.Get("SVG icon item")) { Icon = icon.MenuIcon }], value => _action = value));
            _menu = _menuSample.Preview;
            var menuFactory = _menuSample.Source;
            _menuSample.BindSource(() => _action, value => SvgExampleSource.Factory("CreateSvgIconMenu",
                $"string? _action = {GallerySample.String(value)};\n\n" + iconDefinition.Source
                + "\n\n" + menuFactory + "\nreturn (_store, icon, example);", icons : true));
            var zoom = Fluent.Slider(GalleryText.Get("SVG zoom"), _zoom.Binding, .25f, 4, .01f);
            var rotation = Fluent.Slider(GalleryText.Get("SVG rotation"), _rotation.Binding, 0, 360, 1);
            var margin = Fluent.Slider(GalleryText.Get("SVG margin"), _margin.Binding, 0, 100, 1);
            var radius = Fluent.Slider(GalleryText.Get("Image and SVG corner radius"), _radius.Binding, 0, 100, 1);
            _transformSliders = [zoom, rotation, margin, radius];
            foreach (var slider in _transformSliders)
            {
                slider.Options = slider.Options with { ShowStepMarkers = false, CoalescePointerMoves = true };
                slider.Size = new(240, 32);
            }
            _controls = Dux.Column(new(Spacing: 12, MainAxisSize: UiMainAxisSize.Min),
                Dux.Wrap(new(Spacing: 16, RunSpacing: 12),
                    Dux.Column(new(Spacing: 4, MainAxisSize: UiMainAxisSize.Min), Fluent.Caption1(GalleryText.Get("SVG zoom (1%)")), zoom),
                    Dux.Column(new(Spacing: 4, MainAxisSize: UiMainAxisSize.Min), Fluent.Caption1(GalleryText.Get("SVG rotation (1°)")), rotation),
                    Dux.Column(new(Spacing: 4, MainAxisSize: UiMainAxisSize.Min), Fluent.Caption1(GalleryText.Get("SVG left margin (1 px)")), margin),
                    Dux.Column(new(Spacing: 4, MainAxisSize: UiMainAxisSize.Min), Fluent.Caption1(GalleryText.Get("Image and SVG corner radius (1 px)")), radius)),
                Dux.Wrap(new(Spacing: 12, RunSpacing: 8),
                    Fluent.Button(GalleryText.Get("Stretch image"), () => _bitmap.Fit = UiImageFit.Stretch),
                    Fluent.Button(GalleryText.Get("Fit entire image"), () => _bitmap.Fit = UiImageFit.Contain),
                    Fluent.Button(GalleryText.Get("Fill with image"), () => _bitmap.Fit = UiImageFit.Cover)),
                Dux.Wrap(new(Spacing: 16, RunSpacing: 12),
                    Fluent.Button(GalleryText.Get("Reset SVG transform"), Reset),
                    Fluent.Button(GalleryText.Get("Reload SVG file"), Reload)));
        }
        private void RefreshSources()
        {
            if (_sourceSelection == _selection.Value) return;
            _samples.Clear();
            _samples.Add(_tabsSample);
            switch (_selection.Value)
            {
                case "first": _samples.Add(_comparisonSample); _samples.Add(_textSample); break;
                case "second": _samples.Add(_featureSample); break;
                case "browser": _samples.AddRange((_browser ??= new(_store, _decoder)).Samples); break;
                case "clips": _samples.AddRange((_clips ??= new(_store, _decoder, "ClipStorage")).Samples); break;
                case "layers": _samples.AddRange((_layers ??= new(_store, _decoder, "LayerOrigin")).Samples); break;
                case "domains": _samples.AddRange((_domains ??= new(_store, _decoder, "StrokeDomains")).Samples); break;
                case "arcs": _samples.AddRange((_arcs ??= new(_store, _decoder, "ArcGeometry")).Samples); break;
                default: throw new InvalidOperationException("Unknown SVG example tab.");
            }
            _samples.Add(_menuSample);
            _samples.Add(_iconSample);
            _sourceSelection = _selection.Value;
        }
        private void Reset()
        {
            _zoom.Value = 1;
            _rotation.Value = 0;
            _margin.Value = 0;
            _radius.Value = 0;
            _bitmap.Fit = UiImageFit.Stretch;
        }
        private void Reload()
        {
            var before = _store.Statistics.XmlParses;
            var artwork = _store.Load(_paths[0]);
            var revision = artwork.Revision;
            foreach (var path in _paths)
                _store.Reload(path);
            if (artwork.Revision != revision)
                _bitmap.Source = LoadBitmap();
            _reload = before == _store.Statistics.XmlParses ? GalleryText.Get("The file is unchanged. The parsed XML, scene, and cache were reused.") : GalleryText.Get("Updated the scene from the changed file.");
        }
        private UiImageSource LoadBitmap()
        {
            var image = _decoder.Decode(_paths[0]);
            return new(image);
        }
        internal void Render(UiImmediateContext ui)
        {
            var viewportOrigin = ui.GetCursorScreenPos();
            ui.RenderNaturalHeight(_controls);
            _synchronizeTransformSources = UpdateSourceTiming(ui);
            Preview.Zoom = _zoom.Value;
            Preview.RotationDegrees = _rotation.Value;
            Preview.Margin = new(_margin.Value, 0, 0, 0);
            Preview.CornerRadius = _bitmap.CornerRadius = _radius.Value;
            _text.Zoom = _zoom.Value;
            _text.RotationDegrees = _rotation.Value;
            _text.Margin = new(_margin.Value, 0, 0, 0);
            _features.Zoom = _zoom.Value;
            _features.RotationDegrees = _rotation.Value;
            _features.Margin = new(_margin.Value, 0, 0, 0);
            SvgTelemetryText.Show(ui,
                GalleryText.Format("Zoom: {0:F0}% / Rotation: {1:F0}° / Left margin: {2:F0}px",
                    _zoom.Value * 100, _rotation.Value, _margin.Value),
                _synchronizeTransformSources);
            var fit = _bitmap.Fit switch
            {
                UiImageFit.Stretch => GalleryText.Get("Stretch"),
                UiImageFit.Contain => GalleryText.Get("Contain"),
                UiImageFit.Cover => GalleryText.Get("Cover"),
                _ => throw new InvalidOperationException()
            };
            ui.Text(GalleryText.Format("Corner radius: {0:F0}px / Image fit: {1}", _radius.Value, fit));
            _tabs.Render(ui);
            var scrolling = IsComparisonScrolling(ui, viewportOrigin);
            _useBrowserPreview = !_synchronizeTransformSources || scrolling;
            RefreshSources();
            _tabs.RenderPanel(ui, "first", _renderComparison, focusable: false);
            _tabs.RenderPanel(ui, "second", _renderFeatures, focusable: false);
            _tabs.RenderPanel(ui, "browser", _renderBrowser, focusable: false);
            _tabs.RenderPanel(ui, "clips", _renderClips, focusable: false);
            _tabs.RenderPanel(ui, "layers", _renderLayers, focusable: false);
            _tabs.RenderPanel(ui, "domains", _renderDomains, focusable: false);
            _tabs.RenderPanel(ui, "arcs", _renderArcs, focusable: false);
            _tabsSample.SynchronizeSource();
            ui.RenderNaturalHeight(_tabsSample.CodeView);
            _menu.Render(ui);
            _menuSample.SynchronizeSource();
            ui.RenderNaturalHeight(_menuSample.CodeView);
            ui.Text(GalleryText.Get("Standalone SVG icon (Fluent.Icon)"));
            ui.RenderNaturalHeight(_iconSample);
            ui.Text(GalleryText.Format("Tab: {0} / Menu: {1}", SelectedTabName(),
                GalleryText.Get(_action is null ? "No menu command selected" : "SVG icon item")));
            ui.TextWrapped(_reload);
            var source = _store.Statistics;
            var texture = ui.SvgTextureStatistics;
            var imageTexture = ui.ImageTextureStatistics;
            var font = _store.FontStatistics;
            ui.Text(GalleryText.Format("File reads: {0} / XML parses: {1} / Scenes: {2} / GPU preparation: {3} / Bitmap renders: {4}",
                source.SourceReads, source.XmlParses, source.SceneCompiles, source.GpuSceneCompiles, source.Rasterizations));
            ui.Text(GalleryText.Format("SVG GPU render requests: {0} / CPU pixel uploads: {1} B / Cached textures: {2}",
                texture.GpuRenderRequests, texture.UploadedBytes, texture.ResidentTextures));
            ui.Text(GalleryText.Format("Explicit bitmap render cache: {0:F1} KB", source.CachedRasterBytes / 1024d));
            ui.Text(GalleryText.Format("Standard image uploads: {0} / Cached textures: {1} / Pixels: {2:F1} KB",
                imageTexture.Uploads, imageTexture.ResidentTextures, imageTexture.ResidentBytes / 1024d));
            ui.Text(GalleryText.Format("Loaded fonts: {0} / Typeface requests: {1} / Glyph checks: {2}",
                font.LoadedFaces, font.TypefaceRequests, font.GlyphChecks));
            _hint.Render(ui);
        }
        private string SelectedTabName() => GalleryText.Get(_selection.Value switch
        {
            "first" => "Images and text",
            "second" => "Document features",
            "browser" => "Browser comparison",
            "clips" => "Clips and edges",
            "layers" => "Layers and strokes",
            "domains" => "Stroke coverage",
            "arcs" => "Arcs and curves",
            _ => "None selected"
        });

        private void RenderComparison(UiImmediateContext ui)
        {
            ui.Text(GalleryText.Get("Standard image (left) and SVG widget (right)"));
            if (_synchronizeTransformSources)
            {
                _comparisonSample.SynchronizeSource();
                _textSample.SynchronizeSource();
            }
            ui.RenderNaturalHeight(_comparisonSample);
            ui.RenderNaturalHeight(_textSample);
        }
        private void RenderFeatures(UiImmediateContext ui)
        {
            ui.TextWrapped(GalleryText.Get("Look for repeating patterns, radial gradients, reused symbols, masks, dashes, and arrow markers. The white diamond is a visible child of a hidden group. Use the same controls to zoom, rotate, or add margin."));
            if (_synchronizeTransformSources)
                _featureSample.SynchronizeSource();
            ui.RenderNaturalHeight(_featureSample);
        }
        private void RenderBrowser(UiImmediateContext ui)
        {
            _browserResolutionSwitch.Render(ui);
            var highResolution = _browserHighResolution.Value;
            ui.TextWrapped(GalleryText.Get("By default, the camera zooms and rotates the GPU-rendered image. Enable high-resolution redraw to compare the zoomed pixels."));
            (_browser ??= new(_store, _decoder)).Render(ui, _zoom.Value, _rotation.Value,
                new(_margin.Value, 0, 0, 0), _radius.Value, _synchronizeTransformSources,
                _useBrowserPreview || !highResolution, highResolution);
        }

        private bool IsComparisonScrolling(UiImmediateContext ui, UiVector2 viewportOrigin)
        {
            var previousOrigin = _lastViewportOrigin;
            _lastViewportOrigin = viewportOrigin;
            if (_selection.Value is not ("browser" or "clips" or "layers" or "domains" or "arcs")
                || (_selection.Value == "browser" && !_browserHighResolution.Value))
            {
                _viewportSettlesAt = 0;
                return false;
            }

            // The child content origin includes the applied scroll offset. Read it before
            // painting, so an intermediate camera window never requests exact pixels.
            var now = ui.GetTime();
            if (previousOrigin.HasValue && previousOrigin != viewportOrigin)
                _viewportSettlesAt = now + SourceUpdateDelaySeconds;
            if (now >= _viewportSettlesAt)
                return false;

            ui.RequestFrameAt(_viewportSettlesAt);
            return true;
        }

        private bool UpdateSourceTiming(UiImmediateContext ui)
        {
            var transform = (_zoom.Value, _rotation.Value, _margin.Value, _radius.Value);
            var now = ui.GetTime();
            if (_lastSourceTransform != transform)
            {
                _sourceUpdateAt = _lastSourceTransform is null ? now : now + SourceUpdateDelaySeconds;
                _lastSourceTransform = transform;
            }
            var dragging = _transformSliders.Any(slider => slider.IsPointerDragging);
            // A pause with the pointer captured is still part of the gesture.
            // Give the next gesture the full idle interval after release.
            if (_transformDragging && !dragging)
                _sourceUpdateAt = now + SourceUpdateDelaySeconds;
            _transformDragging = dragging;
            if (dragging)
                return false;
            if (now >= _sourceUpdateAt)
                return true;

            ui.RequestFrameAt(_sourceUpdateAt);
            return false;
        }
        public void Dispose()
        {
            _controls.Dispose();
            _browserResolutionSwitch.Dispose();
            _hint.Dispose();
            _tabsSample.Dispose();
            _menuSample.Dispose();
            _iconSample.Dispose();
            _comparisonSample.Dispose();
            _textSample.Dispose();
            _featureSample.Dispose();
            _icon.Dispose();

            _browser?.Dispose();
            _clips?.Dispose();
            _layers?.Dispose();
            _domains?.Dispose();
            _arcs?.Dispose();
            _store.Dispose();
        }
    }
}


