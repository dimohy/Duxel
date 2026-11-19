using System.Globalization;
using Duxel.Core;
using Duxel.Platform.Windows;

internal static class RatingLabelTests
{
    internal static void Run(string output, Action<bool, string> check)
    {
        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            RunLocalized(check);
            CaptureHover(output, check);
        }
        finally { CultureInfo.CurrentCulture = previousCulture; }
    }

    private static void RunLocalized(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        using var tree = new UiAccessibilityTree();
        ControlFontFixture.Configure(context);
        context.SetAccessibilityTree(tree);
        var score = 2.5f;
        var writes = 0;
        var formats = 0;
        var binding = new UiBinding<float>(() => score, value => { score = value; writes++; });
        using var rating = Fluent.Rating("Service", binding,
            new UiRatingOptions { Step = .5f, ItemLabel = value => { formats++; return $"{value:0.#} out of 5"; } });
        UiWidget current = rating;
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(UiInputState? input = null)
        {
            context.NewFrame(new(.016f, new(320, 240), new(1, 1)), input ?? idle, new(0, 0, 320, 240), UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding: 0); ui.SetCursorScreenPos(new(10, 10)); current.Render(ui); }));
            using var data = context.GetDrawData();
        }
        Frame();
        var initial = tree.CaptureSnapshot().Elements.Where(item => item.Node.Role == UiAccessibilityRole.RadioButton).ToArray();
        check(formats == 10 && initial.Length == 10 && initial[4].Node.Name == "2.5 out of 5" && initial[4].Node.Selected == true,
            "Rating names / custom formatter supplies each half-step accessible label");
        var revision = tree.Revision;
        Frame();
        Frame();
        check(formats == 10 && tree.Revision == revision && writes == 0,
            "Rating names / unchanged frames reuse formatted labels without writes or accessibility changes");
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
        Frame();
        var localized = tree.CaptureSnapshot().Elements.Where(item => item.Node.Role == UiAccessibilityRole.RadioButton).ToArray();
        check(formats == 20 && localized[4].Node.Name == "2,5 out of 5" && localized[4].Node.Selected == true,
            "Rating names / culture replacement refreshes all names while preserving selection");
        check(initial.Zip(localized).All(pair => pair.First.Node.Id == pair.Second.Node.Id && pair.First.RuntimeId == pair.Second.RuntimeId),
            "Rating names / culture-dependent labels preserve every input and accessibility identity");
        Frame();
        check(formats == 20 && writes == 0, "Rating names / refreshed culture is cached without binding writes");
        var target = initial[6];
        tree.RequestAction(target.Node.Id, target.RuntimeId, UiAccessibilityAction.Select, wait: false);
        Frame();
        check(score == 3.5f && writes == 1, "Rating names / an existing runtime identity selects its original numeric value after relabeling");
        var beforeOptions = rating.Options;
        var rejected = false;
        try
        {
            rating.Options = beforeOptions with
            {
                ItemLabel = value => value < 3 ? "temporary" : " "
            };
        }
        catch (ArgumentException) { rejected = true; }
        Frame();
        check(rejected && rating.Options == beforeOptions && tree.CaptureSnapshot().Elements.Any(item => item.Node.Name == "2,5 out of 5"),
            "Rating names / invalid formatter output fails atomically without replacing valid labels or options");
        rating.Options = rating.Options with
        {
            ItemLabel = static value => $"5점 만점에 {value:0.#}점"
        };
        Frame();
        check(tree.CaptureSnapshot().Elements.Any(item => item.Node.Name == "5점 만점에 3,5점"),
            "Rating names / replacing the formatter publishes application-localized wording");
        Frame(idle with
        {
            MousePosition = new(73, 20),
            LeftMouseDown = true,
            LeftMousePressed = true
        });
        rating.Options = rating.Options with
        {
            ItemLabel = static value => $"평가 {value:0.#}"
        };
        Frame(idle with
        {
            MousePosition = new(73, 20),
            LeftMouseReleased = true
        });
        check(score == 2.5f && writes == 2, "Rating names / relabeling between press and release preserves the pressed numeric option");
        var prefix = "Before";
        rating.Options = rating.Options with
        {
            ItemLabel = value => $"{prefix} {value:0.#}"
        };
        Frame();
        var beforeRefresh = tree.CaptureSnapshot().Elements.Single(item => item.Node.Selected == true);
        prefix = "After";
        rating.RefreshItemLabels();
        Frame();
        var afterRefresh = tree.CaptureSnapshot().Elements.Single(item => item.Node.Selected == true);
        check(afterRefresh.Node.Name == "After 2,5" && beforeRefresh.Node.Id == afterRefresh.Node.Id && beforeRefresh.RuntimeId == afterRefresh.RuntimeId && writes == 2,
            "Rating names / explicit refresh applies external translation changes without replacing input or runtime identity");
        using var numeric = Fluent.Rating("Default", binding, new UiRatingOptions { Step = .5f });
        current = numeric;
        Frame();
        var number = tree.CaptureSnapshot().Elements.Single(item => item.Node.Role == UiAccessibilityRole.RadioButton && item.Node.Selected == true);
        check(number.Node.Name == "2,5", "Rating names / default accessible labels use the current numeric culture");
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
        Frame();
        var english = tree.CaptureSnapshot().Elements.Single(item => item.Node.Role == UiAccessibilityRole.RadioButton && item.Node.Selected == true);
        check(english.Node.Name == "2.5" && number.Node.Id == english.Node.Id && number.RuntimeId == english.RuntimeId,
            "Rating names / default numeric culture changes without replacing runtime identity");
        using var disposed = Fluent.Rating("Disposed", binding, new UiRatingOptions { ItemLabel = value => $"{prefix} {value}" });
        disposed.Dispose();
        var rejectedRefresh = false;
        try
        {
            disposed.RefreshItemLabels();
        }
        catch (ObjectDisposedException) { rejectedRefresh = true; }
        check(disposed.Options.ItemLabel is null && rejectedRefresh,
            "Rating names / disposal releases formatter captures and rejects subsequent label refresh");
    }

    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }

    internal static void CaptureHover(string output, Action<bool, string> check)
    {
        var browserReference = VectorGlyphBrowserFixture.LoadReference();
        using var fixture = new PresentationReuseTests.Fixture(1, true);
        var webDark = UiCompiledDesign.Windows11Dark.Theme;
        webDark.Text = new(255, 255, 255);
        fixture.Context.SetTheme(webDark);
        var backend = new VectorBackendProbe();
        fixture.Context.SetPlatformTextBackend(backend);
        var writes = 0;
        using var rating = Fluent.Rating("Browser reference", new UiBinding<float>(() => 4, _ => writes++));
        var input = new UiInputState(new(67, 44), false, false, false, false, false, false, 0, 0, [], [], default, default);
        UiRasterTextureStatistics statistics = default;
        void Draw(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            ui.GetWindowDrawList().AddRectFilled(new(0, 0, fixture.FrameInfo.DisplaySize.X, fixture.FrameInfo.DisplaySize.Y), new(41, 41, 41), ui.WhiteTextureId);
            ui.SetCursorScreenPos(new(25, 30));
            rating.Render(ui);
            statistics = ui.FontIconTextureStatistics;
        }
        using var data = fixture.Build(Draw, input);
        VectorGlyphBrowserFixture.Write(data, Path.Combine(output, "font-icon-uploaded-geometry.json"));
        File.WriteAllLines(Path.Combine(output, "font-icon-vertices.txt"),
            data.DrawLists.SelectMany(list => list.Vertices).Select(vertex => vertex.ToString()));
        var pixels = fixture.Render(data);
        var browserDifferences = VectorGlyphBrowserFixture.CountDifferences(pixels,
            (int)fixture.FrameInfo.DisplaySize.X, (int)fixture.FrameInfo.DisplaySize.Y, browserReference);
        check(browserDifferences == 0,
            $"Vector glyph curves / fixed 6000-pixel region matches independent font-outline browser RGBA8 composition exactly ({browserDifferences} differing pixels)");
        File.WriteAllBytes(Path.Combine(output, "rating-hover-two-selected-four.rgba"), pixels);
        File.WriteAllLines(Path.Combine(output, "font-icon-scenes.txt"), data.TextureUpdates
            .Where(update => update.VectorScene is not null).Select(update => $"{update.Width}x{update.Height} source={update.VectorScene!.Size}"));
        BrowserPng.Write(Path.Combine(output, "rating-hover-two-selected-four.png"),
            (int)fixture.FrameInfo.DisplaySize.X, (int)fixture.FrameInfo.DisplaySize.Y, pixels);
        check(writes == 0, "Rating browser reference / second-star hover preserves committed score four and exports actual GPU pixels");
        var neutral = true;
        var visible = 0;
        var partial = 0;
        for (var i = 0; i < pixels.Length; i += 4)
        {
            neutral &= pixels[i] == pixels[i + 1] && pixels[i] == pixels[i + 2];
            if (pixels[i] > 41)
                visible++;
            if (pixels[i] > 41 && pixels[i] < 255)
                partial++;
        }
        check(neutral && visible > 100,
            "Rating browser reference / white vector icons remain visible without colored subpixel fringes on neutral gray");
        check(partial > 100, "Vector glyph coverage / MSAA1 pixels contain fractional neutral edge coverage instead of binary stair steps");
        var requests = statistics.GpuRenderRequests;
        using var warm = fixture.Build(Draw, input);
        var warmPixels = fixture.Render(warm);
        check(requests == 2 && statistics.GpuRenderRequests == requests
            && !warm.TextureUpdates.Any(update => update.Kind == UiTextureUpdateKind.RenderVectorScene)
            && warmPixels.AsSpan().SequenceEqual(pixels),
            "Vector glyph coverage / unchanged frames reuse both GPU masks without new scene work and preserve exact pixels");
        check(backend.MeshRequests == 2 && backend.RasterRequests == 0 && backend.NativeCurves > 0,
            "Vector glyph curves / actual DirectWrite supplies retained curves without CPU raster pixels");
        var initialScenes = data.TextureUpdates.Where(update => update.VectorScene is not null).Select(update => update.VectorScene!).ToArray();
        var retainedScenes = true;
        foreach (var scale in new[] { 2f, .75f, 1.25f, 1f })
        {
            rating.Transform = new(Scale: scale, RotationDegrees: scale == 1.25f ? 15 : 0);
            using var zoom = fixture.Build(Draw, input);
            retainedScenes &= zoom.TextureUpdates.Where(update => update.VectorScene is not null)
                .All(update => initialScenes.Any(scene => ReferenceEquals(scene, update.VectorScene)));
            fixture.Render(zoom);
        }
        check(backend.MeshRequests == 2 && backend.RasterRequests == 0 && retainedScenes,
            "Vector glyph curves / zoom and rotation reuse the original native outlines and GPU scenes");
    }

    private sealed class VectorBackendProbe : IPlatformTextBackend, IPlatformVectorTextBackend, IPlatformFontMetricsBackend, IPlatformTextLayoutBackend
    {
        internal int MeshRequests, RasterRequests, NativeCurves;
        private static WindowsPlatformTextBackend Native => WindowsPlatformTextBackend.Instance;
        public ulong TextLayoutVersion => Native.TextLayoutVersion;
        public bool TryMeasure(in PlatformTextMeasureRequest request, out PlatformTextMeasureResult result) => Native.TryMeasure(request, out result);
        public bool TryLayout(in PlatformTextMeasureRequest request, out PlatformTextLayoutResult result) => Native.TryLayout(request, out result);
        public bool TryGetFontMetrics(string fontPath, float fontSize, out PlatformFontMetrics metrics) => Native.TryGetFontMetrics(fontPath, fontSize, out metrics);
        public bool TryRasterize(in PlatformTextRasterizeRequest request, out PlatformTextRasterizeResult result)
        {
            RasterRequests++;
            return Native.TryRasterize(request, out result);
        }
        public PlatformTextMeshResult CreateTextMesh(in PlatformTextMeshRequest request)
        {
            MeshRequests++;
            var result = Native.CreateTextMesh(request);
            foreach (var glyph in result.Glyphs.Span)
                NativeCurves += glyph.Outline.Curves.Length;
            return result;
        }
    }
}
