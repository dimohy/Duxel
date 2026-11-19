using Duxel.Core;
using Duxel.Gallery;

internal static class NavigationBarTests
{
    internal static void Run(string output, Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        var actions = 0;
        var period = new UiState<string?>("day");
        var leading = Fluent.Button("Back", () => actions++);
        var trailing = Fluent.Button("Done", () => actions += 10);
        trailing.Size = new(135, 32);
        var selection = Fluent.SegmentedControl("Period", period.Binding,
            new UiSegmentOption[] { new("day", "Day"), new("week", "Week") }, new(Width : 160, ReducedMotion : true));
        using var bar = Fluent.NavigationBar("Calendar actions", "June", leading, trailing, selection, new(Width : 440));
        var screen = new TestScreen(bar.Render);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(UiInputState? input = null)
        {
            context.NewFrame(new(1f / 60, new(640, 440), new(1, 1)), input ?? idle, new(0, 0, 640, 440), UiTextSettings.Default);
            context.Render(screen);
            using var draw = context.GetDrawData();
        }
        UiAccessibilityElement Find(string title) => tree.CaptureSnapshot().Elements.Single(e => e.Node.Name == title);
        void Act(string title, UiAccessibilityAction action)
        {
            var node = Find(title);
            tree.RequestAction(node.Node.Id, node.RuntimeId, action, wait : false);
            Frame();
            Frame();
        }
        void Click(string title)
        {
            var bounds = Find(title).Node.Bounds;
            var point = new UiVector2(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
            Frame(idle with { MousePosition = point, LeftMousePressed = true, LeftMouseDown = true });
            Frame(idle with { MousePosition = point, LeftMouseReleased = true });
            Frame();
        }
        static float Center(UiRect bounds) => bounds.X + bounds.Width / 2;

        Frame();
        var root = Find("Calendar actions").Node;
        var title = Find("June").Node;
        var back = Find("Back").Node;
        var done = Find("Done").Node;
        check(root.Role == UiAccessibilityRole.Navigation && !root.Focusable
            && back.ParentId == root.Id && done.ParentId == root.Id && title.ParentId == root.Id,
            "the bar is a named navigation landmark with real controls and no extra tab stop");
        check(MathF.Abs(Center(root.Bounds) - Center(title.Bounds)) < 0.5f
            && back.Bounds.X < title.Bounds.X && title.Bounds.X < done.Bounds.X,
            "unequal action widths preserve the visual center of the title");
        check(Find("Period").Node.Bounds.Y >= done.Bounds.Y + done.Bounds.Height,
            "the secondary control occupies its own row");
        Click("Done");
        Act("Back", UiAccessibilityAction.Focus);
        Frame(idle with { KeyEvents = [new(UiKey.Enter, true, default)] });
        Frame(idle with { KeyEvents = [new(UiKey.Enter, false, default)] });
        check(actions == 11, "pointer and keyboard activate only their respective action");
        Act("Week", UiAccessibilityAction.Select);
        check(period.Value == "week" && Find("Week").Node.Selected == true,
            "the hosted segmented control retains its native accessibility selection behavior");

        bar.Title = "July";
        bar.Options = bar.Options with { RightToLeft = true };
        Frame();
        check(Find("Back").Node.Id == back.Id && Find("Back").Node.Bounds.X > Find("Done").Node.Bounds.X
            && MathF.Abs(Center(Find("July").Node.Bounds) - Center(Find("Calendar actions").Node.Bounds)) < 0.5f,
            "RTL swaps the actions while preserving identity and the live centered title");
        bar.Options = bar.Options with { Width = 160, RightToLeft = false };
        Frame();
        var narrowRoot = Find("Calendar actions").Node.Bounds;
        var narrowBack = Find("Back").Node.Bounds;
        var narrowDone = Find("Done").Node.Bounds;
        var narrowTitle = Find("July").Node.Bounds;
        check(narrowRoot.Width == 160 && narrowDone.Y >= narrowBack.Y + narrowBack.Height
            && narrowTitle.Y >= narrowDone.Y + narrowDone.Height && Find("Week").Node.Selected == true,
            "narrow layout reflows actions and title without losing selection or overlapping controls");
        check(narrowDone.X + narrowDone.Width <= narrowRoot.X + narrowRoot.Width
            && Find("Period").Node.Bounds.X + Find("Period").Node.Bounds.Width <= narrowRoot.X + narrowRoot.Width,
            "narrow actions and the filter stay inside the allocated bar");
        bar.Title = string.Empty;
        Frame();
        check(!tree.CaptureSnapshot().Elements.Any(e => e.Node.Name == "July") && Find("Done").Node.Id == done.Id,
            "removing the optional title preserves action identity");

        using var fixture = new PresentationReuseTests.Fixture(1, true, PresentationTestPlatform.Create(1100, 900));
        using var galleryTree = new UiAccessibilityTree();
        fixture.Context.SetAccessibilityTree(galleryTree);
        fixture.Context.SetDesign(UiCompiledDesign.Windows11);
        using var gallery = new ObserverScreen(ObserverServices.CreateWindows())
            { FreezeDiagnosticText = true, SelectedPage = "NavigationBar" };
        var galleryImage = fixture.Capture(ui =>
        {
            ui.GetWindowDrawList().AddRectFilled(new(0, 0, fixture.FrameInfo.DisplaySize.X, fixture.FrameInfo.DisplaySize.Y),
                new UiControlContext(ui).Theme.WindowBg, ui.WhiteTextureId);
            gallery.Render(ui);
        });
        check(gallery.SourceCountForPage("NavigationBar") == 2 && Fluent.MissingComponentNames.IsEmpty
            && galleryTree.CaptureSnapshot().Elements.Any(e => e.Node.Role == UiAccessibilityRole.Link
                && e.Node.Name == "NavigationBar" && e.Node.ItemStatus == "NEW"),
            "NavigationBar provides two adjacent source examples and completes the current factory inventory");
        fixture.WriteCapturePng(Path.Combine(output, "gallery-navigation-bar.png"), galleryImage);

        using var page = new NavigationBarPage();
        var brand = (GallerySample<UiFluentNavigationBar>)page.Samples[1];
        void RenderBrand(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding : 12);
            brand.Preview.Render(ui);
        }
        var brandImage = fixture.Capture(RenderBrand);
        var paintCaptures = page.PeriodSelector.PaintCaptureCount;
        for (var pass = 0; pass < 6; pass++)
        {
            check(brandImage.AsSpan().SequenceEqual(fixture.Capture(RenderBrand)),
                $"the branded period labels preserve their pixels across repeated presentation: {pass}");
        }
        check(paintCaptures > 0 && page.PeriodSelector.PaintCaptureCount == paintCaptures
            && fixture.Renderer.LastFrameMetrics.PaintGroupPasses == 0,
            "an unchanged branded period selector reuses its paint group after parent measurement and arrangement");
        string[] periods = ["일간", "주간", "월간"];
        void CheckPeriodLabels(string description)
        {
            var labels = page.PeriodSelector.Layouts.Select(layout =>
                layout.LabelBlock.LineCount == 1 ? layout.LabelBlock.GetLine(0).Text : null);
            check(labels.SequenceEqual(periods), description);
        }
        CheckPeriodLabels("the branded example paints all three Korean period labels without ellipses");
        var bounds = galleryTree.CaptureSnapshot().Elements.Single(e => e.Node.Name == "기간 탐색").Node.Bounds;
        var x = (int)bounds.X + 3;
        var y = (int)bounds.Y + 3;
        var bottom = (int)(bounds.Y + bounds.Height) - 4;
        var stride = (int)fixture.FrameInfo.DisplaySize.X;
        var topColor = brandImage.AsSpan((y * stride + x) * 4, 3).ToArray();
        var bottomColor = brandImage.AsSpan((bottom * stride + x) * 4, 3).ToArray();
        check(topColor[2] > bottomColor[2] && topColor[2] > topColor[1] && bottomColor[2] > bottomColor[1],
            "the branded surface renders a blue gradient on a light-themed Vulkan frame");
        fixture.WriteCapturePng(Path.Combine(output, "navigation-bar-brand.png"), brandImage);
        var forced = new UiForcedColorPalette(new(3, 6, 9), new(240, 230, 220), new(110, 120, 130),
            new(10, 90, 180), new(250, 240, 230), new(30, 200, 220), new(20, 30, 40), new(210, 220, 230));
        fixture.Context.SetTheme(forced.Apply(UiCompiledDesign.Windows11.Theme));
        var contrast = fixture.Capture(RenderBrand);
        CheckPeriodLabels("forced colors preserve the full Korean period labels");
        byte[] systemCanvas = [3, 6, 9];
        check(contrast.AsSpan((y * stride + x) * 4, 3).SequenceEqual(systemCanvas)
            && contrast.AsSpan((bottom * stride + x) * 4, 3).SequenceEqual(systemCanvas),
            "forced colors replace the gradient with the system canvas on Vulkan");
        var week = galleryTree.CaptureSnapshot().Elements.Single(e => e.Node.Role == UiAccessibilityRole.RadioButton && e.Node.Name == "주간");
        galleryTree.RequestAction(week.Node.Id, week.RuntimeId, UiAccessibilityAction.Select, wait : false);
        fixture.Capture(RenderBrand);
        brand.Preview.Options = brand.Preview.Options with { RightToLeft = true };
        fixture.Capture(RenderBrand);
        CheckPeriodLabels("the reversed period selector keeps each complete Korean label");
        brand.SynchronizeSource();
        check(brand.Source.Contains("UiState<string?>(\"week\")", StringComparison.Ordinal)
            && brand.Source.Contains("RightToLeft : true", StringComparison.Ordinal)
            && brand.Source.Contains("Width : 0, IsFixedWidth : false", StringComparison.Ordinal),
            "the gallery source follows hosted selection, direction and width behavior");
        brand.Preview.Options = brand.Preview.Options with { Width = 160 };
        fixture.Capture(RenderBrand);
        CheckPeriodLabels("a narrow navigation bar keeps complete labels in the scrollable content");
        check(page.PeriodSelector.MaximumScrollOffset > 0 && page.PeriodSelector.Value.Value == "week",
            "the narrow selector scrolls without changing the selected week");
        brand.Preview.Options = brand.Preview.Options with { Width = 440 };
        for (var index = 0; index < page.Samples.Count; index++)
        {
            var sample = page.Samples[index];
            sample.SynchronizeSource();
            File.WriteAllText(Path.Combine(output, $"navigation-bar-example-{index}.cs"),
                "using Duxel.Core;\nUiImmediateContext ui = null!;\n" + sample.Source);
        }
    }

    private sealed class TestScreen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding : 0);
            render(ui);
        }
    }
}
