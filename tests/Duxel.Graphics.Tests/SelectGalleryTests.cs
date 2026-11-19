using Duxel.Core;
using Duxel.Gallery;

internal static class SelectGalleryTests
{
    internal static void Run(string output, Action<bool, string> check)
    {
        using var fixture = new PresentationReuseTests.Fixture(1, true, PresentationTestPlatform.Create(1100, 800));
        using var tree = new UiAccessibilityTree(); fixture.Context.SetAccessibilityTree(tree);
        fixture.Context.SetDesign(UiCompiledDesign.Windows11Dark);
        using var page = new SelectPage();
        using var frame = new GalleryPageFrame(page);
        void Background(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding : 0);
            new UiControlContext(ui).Frame(new(0, 0, fixture.FrameInfo.DisplaySize.X, fixture.FrameInfo.DisplaySize.Y),
                new UiControlContext(ui).Theme.WindowBg, default, 0, 0);
        }
        void Draw(UiImmediateContext ui) { Background(ui); frame.Render(ui); }
        void Export(string phase)
        {
            for (var i = 0; i < page.Samples.Count; i++)
            {
                page.Samples[i].SynchronizeSource();
                File.WriteAllText(Path.Combine(output, $"sample-{i}-{phase}.cs"),
                    "using System;\nusing Duxel.Core;\nUiImmediateContext ui = null!; _ = ui;\n" + page.Samples[i].Source);
            }
        }
        void Invoke(string name)
        {
            var button = tree.CaptureSnapshot().Elements.Single(e => e.Node.Name == name && e.Node.Role == UiAccessibilityRole.Button);
            check(tree.RequestAction(button.Node.Id, button.RuntimeId, UiAccessibilityAction.Invoke, wait : false) == UiAccessibilityResult.Success,
                "Select Gallery accepts actual action : " + name);
            fixture.Capture(Draw);
        }
        fixture.Capture(Draw); Export("initial");
        check(page.Samples.Count == 3 && page.Samples.All(sample => sample.Source.Contains("Fluent.Select(", StringComparison.Ordinal)),
            "three independent Select samples each show their own real factory source");
        Invoke("기본 선택 크기 전환"); Invoke("기본 선택 모양 전환");
        Invoke("기본 선택 방향 전환"); Invoke("기본 선택 검증 전환");
        page.Samples[0].SynchronizeSource();
        check(page.Samples[0].Source.Contains("Size : UiDropdownSize.Small", StringComparison.Ordinal)
            && page.Samples[0].Source.Contains("RightToLeft : true", StringComparison.Ordinal)
            && page.Samples[0].Source.Contains("Invalid : true", StringComparison.Ordinal),
            "actual size, appearance, direction and validation changes update the adjacent Select source");
        var list = tree.CaptureSnapshot().Elements.Single(e => e.Node.Role == UiAccessibilityRole.ListBox && e.Node.Name == "지역");
        var option = tree.CaptureSnapshot().Elements.Single(e => e.Node.ParentId == list.Node.Id && e.Node.Name == "日本");
        check(tree.RequestAction(option.Node.Id, option.RuntimeId, UiAccessibilityAction.Select, wait : false) == UiAccessibilityResult.Success,
            "Select Gallery accepts an actual option selection");
        fixture.Capture(Draw); Export("changed");
        check(page.Samples[0].Source.Contains("new UiState<string?>(\"jp\")", StringComparison.Ordinal),
            "Select snippet initializer follows its current bound value");
        // Source synchronization updates text immediately; its code-view syntax is consumed on the next frame.
        fixture.Capture(Draw);
        var source = page.Samples[0].Source; var parses = page.Samples[0].Metrics.SyntaxParses;
        fixture.Capture(Draw); fixture.Capture(Draw);
        check(ReferenceEquals(source, page.Samples[0].Source) && parses == page.Samples[0].Metrics.SyntaxParses,
            "unchanged Select source retains its string and syntax instead of rebuilding");
        using var screen = new ObserverScreen(ObserverServices.CreateWindows()) { FreezeDiagnosticText = true, SelectedPage = "Select" };
        void Route(UiImmediateContext ui) { Background(ui); screen.Render(ui); }
        fixture.Capture(Route);
        var link = tree.CaptureSnapshot().Elements.Single(e => e.Node.Role == UiAccessibilityRole.Link && e.Node.Name == "Select");
        check(!Fluent.MissingComponentNames.Contains("Select") && screen.SourceCountForPage("Select") == 3
            && link.Node.ItemStatus != "NEW",
            "Select stays implemented with three samples");
        tree.RequestAction(link.Node.Id, link.RuntimeId, UiAccessibilityAction.ScrollIntoView, wait : false);
        byte[] pixels = [];
        for (var i = 0; i < 4; i++) pixels = fixture.Capture(Route);
        fixture.WriteCapturePng(Path.Combine(output, "route.png"), pixels);
    }
}
