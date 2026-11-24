using Duxel.Core;
using Duxel.Gallery;

internal static class GalleryPinnedHeaderTests
{
    internal static void RunGpu(string output, Action<bool, string> check)
    {
        using var fixture = new PresentationReuseTests.Fixture(1, true, PresentationTestPlatform.Create(1280, 900));
        using var gallery = new ObserverScreen(ObserverServices.CreateWindows()) { FreezeDiagnosticText = true, SelectedPage = "렌더링" };
        using var tree = new UiAccessibilityTree();
        fixture.Context.SetTheme(UiCompiledDesign.Windows11Dark.Theme);
        fixture.Context.SetAccessibilityTree(tree);
        void Draw(UiImmediateContext ui)
        {
            ui.GetWindowDrawList().AddRectFilled(new(0, 0, fixture.FrameInfo.DisplaySize.X, fixture.FrameInfo.DisplaySize.Y),
                new UiControlContext(ui).Theme.WindowBg, ui.WhiteTextureId);
            gallery.Render(ui);
        }
        UiAccessibilityElement Find(string name, UiAccessibilityRole role) => tree.CaptureSnapshot().Elements.Single(element => element.Node.Name == name && element.Node.Role == role);
        void Reveal(string name, UiAccessibilityRole role)
        {
            var target = Find(name, role);
            check(tree.RequestAction(target.Node.Id, target.RuntimeId, UiAccessibilityAction.ScrollIntoView, wait : false) == UiAccessibilityResult.Success,
                "pinned header GPU target accepts reveal : " + name);
            for (var index = 0; index < 4; index++) fixture.Capture(Draw);
        }
        fixture.Capture(Draw); fixture.Capture(Draw);
        Reveal(gallery.SelectedNavigationLabel, UiAccessibilityRole.Link);
        var before = fixture.Capture(Draw);
        var initial = Find("테마 전환", UiAccessibilityRole.Switch).Node.Bounds;
        var status = Find("갤러리 상태와 옵션", UiAccessibilityRole.Pane).Node.Bounds;
        Reveal("그룹 합성 예제", UiAccessibilityRole.Pane);
        var after = fixture.Capture(Draw);
        var final = Find("테마 전환", UiAccessibilityRole.Switch).Node.Bounds;
        var finalStatus = Find("갤러리 상태와 옵션", UiAccessibilityRole.Pane).Node.Bounds;
        Console.WriteLine($"GPU header theme : {initial} -> {final}; status : {status} -> {finalStatus}");
        var width = (int)fixture.FrameInfo.DisplaySize.X;
        var height = (int)fixture.FrameInfo.DisplaySize.Y;
        BrowserPng.Write(Path.Combine(output, "before.png"), width, height, before);
        BrowserPng.Write(Path.Combine(output, "after.png"), width, height, after);
        check(initial == final && status == finalStatus, "GPU body reveal preserves actual header node bounds");
        var equal = true;
        for (var y = 0; y < 54; y++)
            equal &= before.AsSpan((y * width + 252) * 4, (width - 252) * 4).SequenceEqual(after.AsSpan((y * width + 252) * 4, (width - 252) * 4));
        check(equal, "GPU body reveal preserves fixed global options pixels across partial repaint");
    }
}
