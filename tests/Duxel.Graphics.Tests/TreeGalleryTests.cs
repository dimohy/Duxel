using Duxel.Core;
using Duxel.Gallery;

internal static class TreeGalleryTests
{
    internal static void Run(string output, Action<bool, string> check)
    {
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
        foreach (var name in new[] { "크기 전환", "줄바꿈 전환", "방향 전환", "모션 전환", "체크 표시 위쪽 정렬" })
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
            && accessibility.CaptureSnapshot().Elements.Where(e => e.Node.Role == UiAccessibilityRole.Link && e.Node.ItemStatus == "NEW")
                .Select(e => e.Node.Name).SequenceEqual(["NavigationBar"]),
            "Tree stays registered while the NEW badge follows NavigationBar");
        fixture.WriteCapturePng(Path.Combine(output, "gallery-tree.png"), pixels);
    }
}
