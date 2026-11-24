using Duxel.Core;
using Duxel.Gallery;

internal static class TreeGalleryTests
{
    internal static void Run(string output, Action<bool, string> check)
    {
        using var language = GalleryText.Use(GalleryLanguage.English);
        using var fixture = new PresentationReuseTests.Fixture(1, true, PresentationTestPlatform.Create(1100, 800));
        using var accessibility = new UiAccessibilityTree();
        fixture.Context.SetAccessibilityTree(accessibility);
        fixture.Context.SetDesign(UiCompiledDesign.Windows11Dark);
        using var page = new TreePage();
        using var frame = new GalleryPageFrame(page);
        void Draw(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding: 10);
            var context = new UiControlContext(ui);
            context.Frame(new(0, 0, fixture.FrameInfo.DisplaySize.X, fixture.FrameInfo.DisplaySize.Y), context.Theme.WindowBg, default, 0, 0);
            frame.Render(ui);
        }
        fixture.Capture(Draw);
        check(page.Samples.Count == 2 && page.Samples.All(sample => sample.Source.Contains("Fluent.Tree(", StringComparison.Ordinal)),
            "each Tree example owns an adjacent factory source panel");
        foreach (var name in new[] { "Change size", "Toggle line wrapping", "Change direction", "Toggle reduced motion", "Align checkbox to top" })
        {
            var button = accessibility.CaptureSnapshot().Elements.First(element => element.Node.Role == UiAccessibilityRole.Button && element.Node.Name == name);
            accessibility.RequestAction(button.Node.Id, button.RuntimeId, UiAccessibilityAction.Invoke, wait: false);
            fixture.Capture(Draw);
        }
        page.Samples[0].SynchronizeSource();
        var updated = page.Samples[0].Source;
        check(updated.Contains("UiTreeSize.Small", StringComparison.Ordinal) && updated.Contains("UiTreeTextOverflow.Ellipsis", StringComparison.Ordinal)
            && updated.Contains("RightToLeft: true", StringComparison.Ordinal) && updated.Contains("ReducedMotion = true", StringComparison.Ordinal)
            && updated.Contains("AlignSelectorToTop = true", StringComparison.Ordinal),
            "changing the Tree example settings updates the matching code");
        for (var index = 0; index < page.Samples.Count; index++)
            File.WriteAllText(Path.Combine(output, $"tree-example-{index}.cs"),
                "using Duxel.Core;\nUiImmediateContext ui = null!;\n" + page.Samples[index].Source);
        using var screen = new ObserverScreen(ObserverServices.CreateWindows()) { FreezeDiagnosticText = true, SelectedPage = "Tree" };
        var pixels = fixture.Capture(screen.Render);
        check(!Fluent.MissingComponentNames.Contains("Tree") && screen.SourceCountForPage("Tree") == 2
            && accessibility.CaptureSnapshot().Elements.Any(e => e.Node.Role == UiAccessibilityRole.Link && e.Node.Name == "Tree"),
            "Tree stays registered and reachable in Gallery navigation");
        fixture.WriteCapturePng(Path.Combine(output, "gallery-tree.png"), pixels);
    }
}
