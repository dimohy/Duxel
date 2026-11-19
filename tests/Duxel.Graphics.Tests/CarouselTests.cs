using Duxel.Core;
using Duxel.Gallery;

internal static class CarouselTests
{
    internal static void Run(string output, Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        var selected = "a";
        var accept = true;
        var actions = 0;
        var binding = new UiBinding<string>(() => selected, key => { if (accept) selected = key; });
        using var carousel = Fluent.Carousel("Places", binding, new UiCarouselSlide[]
        {
            new("a", "First", Fluent.Button("First action", () => actions++)),
            new("b", "Second", Dux.Column(new(Spacing: 14, MainAxisSize: UiMainAxisSize.Min),
                Fluent.Button("Second action", () => actions++), Fluent.Text("Extra content"))),
            new("c", "Third", Fluent.Button("Third action", () => actions++))
        }, new() { ReducedMotion = true, AutoPlayInterval = TimeSpan.FromSeconds(1) });
        using var root = Dux.Column(new(Spacing: 8, MainAxisSize: UiMainAxisSize.Min),
            Fluent.Button("Outside"), carousel);
        var screen = new Screen(root.Render);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(float elapsed = 1f / 60, UiInputState? input = null)
        {
            context.NewFrame(new(elapsed, new(320, 320), new(1, 1)), input ?? idle, new(0, 0, 320, 320), UiTextSettings.Default);
            context.Render(screen);
            using var draw = context.GetDrawData();
        }
        UiAccessibilityElement Find(string name) => tree.CaptureSnapshot().Elements.Single(e => e.Node.Name == name);
        bool Has(string name) => tree.CaptureSnapshot().Elements.Any(e => e.Node.Name == name);
        void Act(string name, UiAccessibilityAction action = UiAccessibilityAction.Invoke)
        {
            var node = Find(name);
            tree.RequestAction(node.Node.Id, node.RuntimeId, action, wait: false);
            Frame();
            Frame();
        }
        void Click(string name)
        {
            var bounds = Find(name).Node.Bounds;
            var point = new UiVector2(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
            Frame(input: idle with { MousePosition = point, LeftMousePressed = true, LeftMouseDown = true });
            Frame(input: idle with { MousePosition = point, LeftMouseReleased = true });
            Frame();
        }

        Frame();
        check(Has("First action") && !Has("Second action") && !Find("Previous slide").Node.Enabled,
            "only the active slide is exposed and the first edge is disabled");
        check(context.State.NextScheduledFrameTimeSeconds is null, "autoplay is initially stopped");
        Act("First action");
        check(actions == 1 && selected == "a", "slide actions do not change selection");
        accept = false;
        Act("Next slide");
        check(selected == "a" && Has("First action"), "a controlled selection can reject previous/next requests");
        accept = true;
        var initial = Find("Next slide").Node.Bounds;
        Click("Next slide");
        check(selected == "b" && Has("Second action") && !Has("First action")
            && Find("Third").Node.Bounds.Y > initial.Y, "pointer navigation lays out the newly selected slide");
        Act("Third", UiAccessibilityAction.Select);
        check(selected == "c" && Has("Third action") && !Find("Next slide").Node.Enabled,
            "assistive selection reaches the last slide and disables forward navigation");
        var third = Find("Third");
        check(tree.RequestAction(third.Node.Id, third.RuntimeId, UiAccessibilityAction.Deselect, wait: false)
                == UiAccessibilityResult.InvalidOperation && third.Node.Selected == true,
            "accessibility keeps one step selected and rejects clearing the active slide");
        Act("Previous slide");
        check(selected == "b", "previous navigation moves back from the edge");

        Act("Outside", UiAccessibilityAction.Focus);
        // Use assistive toggle without taking focus so the schedule can be inspected independently.
        Act("Auto-play", UiAccessibilityAction.Toggle);
        check(carousel.IsPlaying && context.State.NextScheduledFrameTimeSeconds.HasValue,
            "playing declares a deadline in event-wait mode");
        var body = Find("Second action").Node.Bounds;
        var hovered = idle with { MousePosition = new(body.X + 5, body.Y - 4) };
        for (var i = 0; i < 12; i++)
            Frame(0.1f, hovered);
        check(selected == "b" && context.State.NextScheduledFrameTimeSeconds is null,
            "hover suspends autoplay scheduling past the rotation interval");
        Act("Second action", UiAccessibilityAction.Focus);
        check(context.State.NextScheduledFrameTimeSeconds is null, "focus inside slide content suspends autoplay");
        Act("Outside", UiAccessibilityAction.Focus);
        Frame();
        for (var i = 0; i < 12 && selected == "b"; i++)
            Frame(0.1f);
        Frame();
        check(selected == "c" && context.State.NextScheduledFrameTimeSeconds is null,
            "autoplay advances on a deadline and sleeps at the nonwrapping edge");
        Act("Auto-play", UiAccessibilityAction.Toggle);
        Act("Previous slide");
        for (var i = 0; i < 12; i++)
            Frame(0.1f);
        check(!carousel.IsPlaying && selected == "b" && context.State.NextScheduledFrameTimeSeconds is null,
            "the pause control stops future deadlines");

        using var fixture = new PresentationReuseTests.Fixture(1, true, PresentationTestPlatform.Create(1100, 900));
        using var galleryTree = new UiAccessibilityTree();
        fixture.Context.SetAccessibilityTree(galleryTree);
        fixture.Context.SetDesign(UiCompiledDesign.Windows11Dark);
        using var gallery = new ObserverScreen(ObserverServices.CreateWindows())
            { FreezeDiagnosticText = true, SelectedPage = "Carousel" };
        var pixels = fixture.Capture(gallery.Render);
        check(gallery.SourceCountForPage("Carousel") == 2 && !Fluent.MissingComponentNames.Contains("Carousel")
            && galleryTree.CaptureSnapshot().Elements.Any(e => e.Node.Name == "Carousel"
                && e.Node.Role == UiAccessibilityRole.Link && e.Node.ItemStatus is null),
            "the Carousel destination retains both public examples after the next addition");
        fixture.WriteCapturePng(Path.Combine(output, "gallery-carousel.png"), pixels);
        using var page = new CarouselPage();
        var previewSample = (GallerySample<UiFluentCarousel>)page.Samples[1];
        void ImageExample(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding: 12);
            previewSample.Preview.Render(ui);
        }
        fixture.Capture(ImageExample);
        var last = galleryTree.CaptureSnapshot().Elements.Single(e => e.Node.Role == UiAccessibilityRole.Tab && e.Node.Name == "노을");
        var first = galleryTree.CaptureSnapshot().Elements.Single(e => e.Node.Role == UiAccessibilityRole.Button && e.Node.Name == "이전 슬라이드");
        check(first.Node.Enabled, "wrapping enables the previous arrow on the first slide");
        galleryTree.RequestAction(first.Node.Id, first.RuntimeId, UiAccessibilityAction.Invoke, wait: false);
        fixture.Capture(ImageExample);
        fixture.Capture(ImageExample);
        check(previewSample.Preview.Value.Value == "sunset" && galleryTree.CaptureSnapshot().Elements.Any(e =>
                e.Node.Id == last.Node.Id && e.Node.Selected == true),
            "the wrapped slide and image preview agree on the selected key");
        var thumbnail = galleryTree.CaptureSnapshot().Elements.Single(e => e.Node.Role == UiAccessibilityRole.Tab && e.Node.Name == "숲");
        galleryTree.RequestAction(thumbnail.Node.Id, thumbnail.RuntimeId, UiAccessibilityAction.Select, wait: false);
        fixture.Capture(ImageExample);
        fixture.WriteCapturePng(Path.Combine(output, "carousel-image-previews.png"), fixture.Capture(ImageExample));
        previewSample.Preview.IsPlaying = true;
        previewSample.SynchronizeSource();
        check(previewSample.Source.Contains("UiState<string>(\"forest\")", StringComparison.Ordinal)
            && previewSample.Source.Contains("carousel.IsPlaying = true;", StringComparison.Ordinal),
            "the exported image example follows both selection and autoplay");
        for (var index = 0; index < page.Samples.Count; index++)
        {
            var sample = page.Samples[index];
            sample.SynchronizeSource();
            File.WriteAllText(Path.Combine(output, $"carousel-example-{index}.cs"),
                "using System;\nusing Duxel.Core;\nUiImmediateContext ui = null!;\n" + sample.Source);
        }
    }

    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            render(ui);
        }
    }
}
