using Duxel.Core;
using Duxel.Gallery;

internal static class ObserverBatchPresentationTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        foreach (var originY in new float[] { 256, 4096, 8192.125f })
        {
            const float height = 78.8f;
            var visited = false;
            using var content = Dux.Column(new(MainAxisSize : UiMainAxisSize.Min),
                Dux.SizedBox(Fluent.Body1("Precision"), 3180, height));
            using var host = Dux.SizedBox(new UiViewHost(new UiView(ui =>
            {
                visited = true;
                check(ui.GetContentRegionAvail() == new UiVector2(3180, height),
                    "Borrowed view preserves exact allocated extents at translated origin : " + originY);
                content.Render(ui);
            })), 3180, height);
            context.NewFrame(new(.016f, new(4000, 10000), new(1, 1)),
                new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default),
                new(0, 0, 4000, 10000), UiTextSettings.Default);
            context.Render(new PrecisionScreen(ui => { ui.EnableRootViewportContentLayout(contentPadding : 0); ui.SetCursorScreenPos(new(100, originY)); host.Render(ui); }));
            using var precisionDrawing = context.GetDrawData();
            check(visited, "Translated exact-fit column is rendered without a false overflow : " + originY);
        }
        using var screen = new ObserverScreen(ObserverServices.CreateWindows()) { FreezeDiagnosticText = true };
        ControlFontFixture.Configure(context);
        var fonts = GalleryText.ConfigureFonts(new());
        context.SetDirectTextSecondaryFontPath(fonts.SecondaryFontPath);
        context.SetTypographyFonts(fonts.Typography);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        foreach (var title in screen.PageTitles)
        {
            screen.SelectedPage = title;
            context.NewInputBatch(new(.016f, new(1280, 900), new(1, 1)), [idle, idle, idle], new(0, 0, 1280, 900), UiTextSettings.Default);
            context.Render(screen);
            using var drawing = context.GetDrawData();
            check(context.InputProcessingMetrics is { RequestedSteps: 3, CompletedSteps: 3, BuiltDrawings: 1, Completed: true }
                && drawing.DrawLists.Any(list => list.Commands.Count > 0),
                "Observer batched presentation / " + title + " / three traversals and one nonempty final drawing");
            var nodes = tree.CaptureSnapshot().Elements.Select(element => element.Node).ToArray();
            var menuLabel = screen.NavigationLabelForPage(title);
            var heading = nodes.Single(node => node.Role == UiAccessibilityRole.Label && node.Name == menuLabel);
            var descriptionText = screen.DescriptionForPage(title);
            if (string.IsNullOrWhiteSpace(descriptionText))
            {
                check(heading.Bounds.Height > 0, "Gallery displays its heading : " + title);
            }
            else
            {
                var description = nodes.Single(node => node.Role == UiAccessibilityRole.Label
                    && node.Name == GalleryText.Get(descriptionText));
                check(heading.Bounds.Y + heading.Bounds.Height <= description.Bounds.Y && heading.Bounds.X == description.Bounds.X,
                    "Gallery displays its menu label and aligned description : " + title);
            }
            var sources = tree.CaptureSnapshot().Elements.Where(element => element.Node.Role == UiAccessibilityRole.Edit && element.Node.Name == "C# 예제 소스").ToArray();
            check(GalleryPageFrame.HasSource(title) ? sources.Length == screen.SourceCountForPage(title) && sources.All(source => source.Node.ReadOnly && source.Node.Value is { Length: > 0 })
                : sources.Length == 0, "Gallery page source policy matches its functional or informational role : " + title);
            Console.WriteLine("GALLERY_SOURCE_STATUS {\"page\":" + GallerySample.String(title)
                + ",\"kind\":" + GallerySample.String(screen.SourceKindForPage(title))
                + ",\"blocks\":" + sources.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}");
            if (GalleryPageFrame.HasSource(title))
                foreach (var size in new[] { new UiVector2(1280, 843.35f), new UiVector2(1100, 979.4f) })
                {
                    void ResizeFrame()
                    {
                        context.NewFrame(new(.016f, size, new(1, 1)), idle, new(0, 0, size.X, size.Y), UiTextSettings.Default);
                        context.Render(screen);
                        using var resizedDrawing = context.GetDrawData();
                    }
                    ResizeFrame();
                    // SVG owns its scrolling document rather than using the common page pane.
                    var paneName = title == "SVG" ? "observer-svg-page" : "갤러리 페이지";
                    var pane = tree.CaptureSnapshot().Elements.Single(element => element.Node.Name == paneName);
                    if (pane.Node.Scroll is { VerticallyScrollable: true })
                        check(tree.RequestScroll(pane.Node.Id, pane.RuntimeId, UiAccessibilityScrollCommand.ByPercent(-1, 100), wait: false)
                            == UiAccessibilityResult.Success, "Gallery accepts end-scroll after fractional resize : " + title + " / " + size.Y);
                    ResizeFrame();
                    var scrolled = tree.CaptureSnapshot().Elements.Single(element => element.Node.Name == paneName).Node.Scroll;
                    check(scrolled is null || scrolled.Value.Offset.Y == MathF.Max(0, scrolled.Value.Content.Y - scrolled.Value.Viewport.Y),
                        "Gallery preserves the complete bottom viewport after fractional resize : " + title + " / " + size.Y);
                }
        }
        check(new[] { "아바타", "평가", "추가 정보" }.All(page => screen.NavigationKindForPage(page) == GalleryDestinationKind.Fluent),
            "Korean page headings do not misclassify actual Fluent controls as Duxel extensions");
        check(new[] { "SVG", "Markdown", "위젯 배치", "가상 목록" }.All(page => screen.NavigationKindForPage(page) == GalleryDestinationKind.Duxel)
            && screen.NavigationKindForPage("갱신 관찰") == GalleryDestinationKind.Diagnostics,
            "Duxel extensions and diagnostic tools have separate navigation locations from Fluent controls");
        check(screen.NavigationKindForPage("배치 통합") == GalleryDestinationKind.Integration,
            "compound tab and menu layout examples belong to integration rather than the basic container page");
        void Batch(params UiInputState[] steps)
        {
            context.NewInputBatch(new(.016f, new(1280, 900), new(1, 1)), steps, new(0, 0, 1280, 900), UiTextSettings.Default);
            context.Render(screen);
            using var drawing = context.GetDrawData();
        }
        Batch(idle);
        var themeBefore = context.GetStyle().FluentColorScheme;
        var toggle = tree.CaptureSnapshot().Elements.Single(static element => element.Node.Role == UiAccessibilityRole.Switch && element.Node.Name == "밝은 테마").Node;
        var point = new UiVector2(toggle.Bounds.X + toggle.Bounds.Width / 2, toggle.Bounds.Y + toggle.Bounds.Height / 2);
        var down = idle with { MousePosition = point, LeftMouseDown = true, LeftMousePressed = true };
        var up = idle with { MousePosition = point, LeftMouseReleased = true };
        Batch(down, up, down, up);
        var allClicksProcessed = context.InputProcessingMetrics is { RequestedSteps: 4, CompletedSteps: 4 };
        Batch(idle);
        check(allClicksProcessed && context.GetStyle().FluentColorScheme == themeBefore,
            "gallery two theme clicks in one ordered packet preserve both toggles before the requested theme is applied");
        Batch(down, up);
        Batch(idle);
        check(context.GetStyle().FluentColorScheme != themeBefore,
            "gallery one ordered theme click changes the applied palette instead of ignoring pointer input");
    }
    private sealed class PrecisionScreen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }

}
