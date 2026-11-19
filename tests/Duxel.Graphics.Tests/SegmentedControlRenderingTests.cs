using Duxel.Core;
using Duxel.Gallery;

internal static class SegmentedControlRenderingTests
{
    internal static void Run(string output, Action<bool, string> check, bool galleryOnly = false)
    {
        if (!galleryOnly) SegmentedControlStyleRenderingTests.Run(output, check);
        using var fixture = new PresentationReuseTests.Fixture(1, true, PresentationTestPlatform.Create(900, 700));
        fixture.Context.SetDesign(UiCompiledDesign.Windows11Dark);
        var state = new UiState<string?>("a");
        using var widget = new UiSegmentedControlWidget("View", state.Binding,
            new UiSegmentOption[] { new("a", "Day"), new("b", "Month"), new("c", "Year") });
        void Draw(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding : 0);
            var context = new UiControlContext(ui);
            ui.GetWindowDrawList().AddRectFilled(new(0, 0, fixture.FrameInfo.DisplaySize.X, fixture.FrameInfo.DisplaySize.Y), context.Theme.WindowBg, ui.WhiteTextureId);
            widget.Render(ui);
        }
        foreach (var samples in galleryOnly ? Array.Empty<int>() : new[] { 1, 4 })
        {
            fixture.Renderer.SetMsaaSamples(samples);
            state.Value = "a"; widget.Options = new(ReducedMotion : true);
            var initial = fixture.Capture(Draw); var builds = widget.LayoutBuildCount;
            widget.Options = new(); fixture.Capture(Draw);
            builds = widget.LayoutBuildCount;
            state.Value = "b"; var start = fixture.Capture(Draw);
            fixture.Context.State.AdvanceTime(.08f); var middle = fixture.Capture(Draw);
            fixture.Context.State.AdvanceTime(.3f); var end = fixture.Capture(Draw);
            check(!middle.AsSpan().SequenceEqual(initial) && !middle.AsSpan().SequenceEqual(end)
                && !end.AsSpan().SequenceEqual(initial), "msaa" + samples + " selection thumb has distinct initial, intermediate and final pixels");
            var idle = fixture.Capture(Draw);
            check(end.AsSpan().SequenceEqual(idle), "msaa" + samples + " settled selection preserves exact pixels");
            check(widget.LayoutBuildCount == builds, "msaa" + samples + " selection and animation reuse prepared segment text and geometry");
            widget.Options = widget.Options with { ReducedMotion = true }; state.Value = "a";
            var reduced = fixture.Capture(Draw);
            check(reduced.AsSpan().SequenceEqual(initial), "msaa" + samples + " reduced motion immediately matches the exact final pose");
            fixture.WriteCapturePng(Path.Combine(output, "msaa" + samples + "-body.png"), end);
            fixture.WriteCapturePng(Path.Combine(output, "msaa" + samples + "-motion.png"), middle);
        }
        using (var scrolling = new UiSegmentedControlWidget("Scroll GPU", new UiState<string?>("0").Binding,
            Enumerable.Range(0, 8).Select(i => new UiSegmentOption(i.ToString(), "Segment " + i)).ToArray(),
            new(Width : 220, ReducedMotion : true, IsFixedWidth : false)))
        {
            void DrawScroll(UiImmediateContext ui)
            {
                ui.EnableRootViewportContentLayout(contentPadding : 0);
                ui.GetWindowDrawList().AddRectFilled(new(0, 0, fixture.FrameInfo.DisplaySize.X, fixture.FrameInfo.DisplaySize.Y),
                    new UiControlContext(ui).Theme.WindowBg, ui.WhiteTextureId);
                scrolling.Render(ui);
            }
            var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
            var first = fixture.Capture(DrawScroll);
            var builds = scrolling.LayoutBuildCount;
            var moved = fixture.Capture(DrawScroll, idle with { MousePosition = new(100, 15), MouseWheelHorizontal = 2 });
            check(scrolling.ScrollOffset == 96 && !first.AsSpan().SequenceEqual(moved),
                "Vulkan scrolling moves the visible segmented content");
            var restored = fixture.Capture(DrawScroll, idle with { MousePosition = new(100, 15), MouseWheelHorizontal = -2 });
            check(first.AsSpan().SequenceEqual(restored), "returning to the first offset restores exact Vulkan pixels");
            check(scrolling.LayoutBuildCount == builds, "Vulkan scroll composition retains the prepared segment layout");
            fixture.WriteCapturePng(Path.Combine(output, "scroll-initial.png"), first);
            fixture.WriteCapturePng(Path.Combine(output, "scroll-moved.png"), moved);
        }
        using var page = new SegmentedControlPage();
        using var frame = new GalleryPageFrame(page);
        check(page.Samples.Count == 5, "Gallery separates four segmented styles and horizontal scrolling with adjacent sources");
        using var tree = new UiAccessibilityTree(); fixture.Context.SetAccessibilityTree(tree);
        void DrawPage(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding : 0);
            ui.GetWindowDrawList().AddRectFilled(new(0, 0, fixture.FrameInfo.DisplaySize.X, fixture.FrameInfo.DisplaySize.Y), new UiControlContext(ui).Theme.WindowBg, ui.WhiteTextureId);
            frame.Render(ui);
        }
        fixture.Capture(DrawPage);
        check(page.Samples[4].Source.Contains("IsFixedWidth : false", StringComparison.Ordinal),
            "Gallery scrolling example exposes its actual scrolling option in adjacent source");
        var preview = page.Preview(0);
        preview.Options = preview.Options with { RightToLeft = true, Width = 480 };
        var gallery = fixture.Capture(DrawPage);
        check(page.Samples[0].Source.Contains("RightToLeft : true", StringComparison.Ordinal)
            && page.Samples[0].Source.Contains("Width : 480", StringComparison.Ordinal),
            "Gallery segmented source follows actual changed options");
        fixture.WriteCapturePng(Path.Combine(output, "gallery.png"), gallery);
        using var screen = new ObserverScreen(ObserverServices.CreateWindows()) { FreezeDiagnosticText = true, SelectedPage = "SegmentedControl" };
        var route = fixture.Capture(screen.Render);
        fixture.WriteCapturePng(Path.Combine(output, "route.png"), route);
        check(screen.SelectedNavigationLabel == "SegmentedControl" && screen.SourceCountForPage("SegmentedControl") == 5
            && !Fluent.MissingComponentNames.Contains("SegmentedControl"), "actual Gallery route exposes the registered Fluent segmented factory");
    }
}
