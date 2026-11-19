using Duxel.Core;
using Duxel.Gallery;

internal static class OpacityMaskGalleryTests
{
    internal static void Run(string output, Action<bool, string> check)
    {
        using var fixture = new PresentationReuseTests.Fixture(1, true, PresentationTestPlatform.Create(1000, 800));
        fixture.Context.SetDesign(UiCompiledDesign.Windows11Dark);
        using var sample = new OpacityMaskSample();
        using var frame = new GalleryPageFrame(new Page(sample));
        byte[] Capture()
        {
            return fixture.Capture(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding : 0);
                ui.GetWindowDrawList().AddRectFilled(new(0, 0, fixture.FrameInfo.DisplaySize.X, fixture.FrameInfo.DisplaySize.Y),
                    new UiControlContext(ui).Theme.WindowBg, ui.WhiteTextureId);
                frame.Render(ui);
            });
        }
        Capture();
        var first = Capture();
        var builds = sample.CaptureBuilds;
        check(builds == 1 && fixture.Renderer.LastFrameMetrics.PaintGroupPasses == 0,
            "Gallery mask sample reuses its captured real text and overlapping shapes after warmup");
        sample.Mask = new(.25f, 0);
        sample.LightBackground = true;
        var background = Capture();
        check(sample.CaptureBuilds == builds && !first.AsSpan().SequenceEqual(background)
            && sample.Sample.Source.Contains("new UiHorizontalOpacityMask(0.25f, 0f)", StringComparison.Ordinal)
            && sample.Sample.Source.Contains("var _light = true", StringComparison.Ordinal),
            "Gallery mask and rear-surface changes update both pixels and source without recapturing content");
        sample.Camera = new(new(.25f, .25f), new(.5f, 0), new(0, .5f));
        var camera = Capture();
        check(sample.CaptureBuilds == builds && !background.AsSpan().SequenceEqual(camera)
            && sample.Sample.Source.Contains("new(0.25f, 0.25f), new(0.5f, 0f)", StringComparison.Ordinal)
            && sample.Sample.Source.Contains("UiPaintGroupTextMode.Projected", StringComparison.Ordinal),
            "Gallery camera changes reuse the same capture and expose the actual affine UV source");
        fixture.WriteCapturePng(Path.Combine(output, "mask-gallery.png"), first);
        fixture.WriteCapturePng(Path.Combine(output, "mask-gallery-camera.png"), camera);
        sample.InvalidateContent();
        Capture();
        check(sample.CaptureBuilds == builds + 1 && sample.Sample.Source.Contains("ulong _version = 2UL", StringComparison.Ordinal),
            "explicit content-version changes recapture once and update the matching public source example");
        using var observer = new ObserverScreen(ObserverServices.CreateWindows()) { FreezeDiagnosticText = true, SelectedPage = "렌더링" };
        fixture.Capture(observer.Render);
        check(observer.SelectedNavigationLabel == "렌더링" && observer.SourceCountForPage("렌더링") == 6,
            "actual Rendering route includes the new mask sample and its own adjacent source");
    }
    private sealed class Page(OpacityMaskSample sample) : IObserverPage, IObserverNaturalPage, IGallerySourcePage
    {
        public string Title => "투명 마스크";
        public string Description => "배경과 카메라를 바꿔도 준비한 내용을 다시 만들지 않습니다.";
        public IReadOnlyList<IGallerySample> Samples => new[] { sample.Sample };
        public UiWidget MeasureRoot => sample.View;
        public void Render(UiImmediateContext ui) => sample.View.Render(ui);
        public void Dispose() { }
    }
}
