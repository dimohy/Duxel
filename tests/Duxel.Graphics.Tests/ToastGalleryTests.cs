using Duxel.Core;
using Duxel.Gallery;

internal static class ToastGalleryTests
{
    internal static void Run(string output, Action<bool, string> check)
    {
        using var fixture = new PresentationReuseTests.Fixture(1, true, PresentationTestPlatform.Create(1000, 700));
        using var tree = new UiAccessibilityTree();
        fixture.Context.SetAccessibilityTree(tree);
        using var page = new ToastPage();
        using var frame = new GalleryPageFrame(page);
        void Draw(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding : 0);
            new UiControlContext(ui).Frame(new(0, 0, fixture.FrameInfo.DisplaySize.X, fixture.FrameInfo.DisplaySize.Y),
                new UiControlContext(ui).Theme.WindowBg, default, 0, 0);
            frame.Render(ui);
        }
        void ExportSources(string phase)
        {
            for (var index = 0; index < page.Samples.Count; index++)
            {
                page.Samples[index].SynchronizeSource();
                File.WriteAllText(Path.Combine(output, $"sample-{index}-{phase}.cs"),
                    "using System;\nusing System.Collections.Generic;\nusing Duxel.Core;\n"
                    + "UiImmediateContext ui = null!; _ = ui;\n" + page.Samples[index].Source);
            }
        }
        fixture.Capture(Draw);
        ExportSources("initial");
        void Invoke(string name)
        {
            var button = tree.CaptureSnapshot().Elements.Single(e => e.Node.Role == UiAccessibilityRole.Button && e.Node.Name == name);
            if (button.Node.Offscreen)
            {
                if (tree.RequestAction(button.Node.Id, button.RuntimeId, UiAccessibilityAction.ScrollIntoView, wait : false) != UiAccessibilityResult.Success)
                    throw new InvalidOperationException("Toast Gallery target could not be revealed: " + name);
                for (var settle = 0; settle < 4; settle++) fixture.Capture(Draw);
                button = tree.CaptureSnapshot().Elements.Single(e => e.Node.Role == UiAccessibilityRole.Button && e.Node.Name == name);
            }
            if (tree.RequestAction(button.Node.Id, button.RuntimeId, UiAccessibilityAction.Invoke, wait : false) != UiAccessibilityResult.Success)
                throw new InvalidOperationException("Toast Gallery target could not be invoked: " + name);
            fixture.Capture(Draw);
        }
        Invoke("시간제 알림 추가");
        check(page.Controller.Visible.Count == 1 && page.Controller.Visible[0].Status == UiToastStatus.Entering
            && page.Samples[3].Source.Contains("UiToastController", StringComparison.Ordinal),
            "actual Gallery notification action updates the lifecycle preview and adjacent controller source");
        Invoke("등장 완료"); Invoke("1초 진행");
        check(page.Controller.NextTimeout == TimeSpan.FromSeconds(6), "Gallery manual clock advances only after visible completion");
        Invoke("일시 정지 전환"); Invoke("1초 진행");
        check(page.Controller.NextTimeout is null && page.Controller.Visible[0].PauseReasons == UiToastPauseReason.Explicit,
            "Gallery pause action removes its deadline while manual time continues");
        Invoke("동작 있는 알림 표시");
        fixture.Context.State.AdvanceTime(.3f); fixture.Capture(Draw);
        check(tree.CaptureSnapshot().Elements.Any(e => e.Node.Role == UiAccessibilityRole.Group && e.Node.Name == "확인할 알림이 있습니다")
            && page.Samples[0].Source.Contains("Fluent.Toaster", StringComparison.Ordinal),
            "Gallery live example opens an actual toast portal with matching host source");
        Invoke("진행 알림 표시");
        fixture.Context.State.AdvanceTime(.3f); fixture.Capture(Draw);
        page.Samples[0].SynchronizeSource();
        check(page.Samples[0].Source.Contains("Progress = new(0.35f", StringComparison.Ordinal)
            && page.Samples[0].Source.Contains("Timeout = null", StringComparison.Ordinal)
            && tree.CaptureSnapshot().Elements.Any(e => e.Node.Role == UiAccessibilityRole.Group
                && e.Node.Name == "파일 전송 중" && e.Node.Busy),
            "live progress toast exposes busy semantics and matching persistent progress source");
        Invoke("긴 알림 표시");
        fixture.Context.State.AdvanceTime(.3f); fixture.Capture(Draw);
        page.Samples[0].SynchronizeSource();
        var longSurface = tree.CaptureSnapshot().Elements.Single(e => e.Node.Role == UiAccessibilityRole.Group && e.Node.Name == "긴 알림").Node.Bounds;
        check(longSurface.Y >= 16 && longSurface.Y + longSurface.Height <= fixture.FrameInfo.DisplaySize.Y - 16
            && page.Samples[0].Source.Contains("긴 알림", StringComparison.Ordinal),
            "Gallery long-content action opens a constrained scrollable Toast with matching source");
        Invoke("실제 알림 모두 닫기");
        fixture.Context.State.AdvanceTime(.3f); fixture.Capture(Draw);
        Invoke("혼합 언어 알림"); Invoke("알림 방향 전환");
        Invoke("알림 너비 전환"); Invoke("알림 너비 전환");
        fixture.Context.State.AdvanceTime(.3f); fixture.Capture(Draw);
        page.Samples[0].SynchronizeSource();
        var mixedSurface = tree.CaptureSnapshot().Elements.Single(e => e.Node.Role == UiAccessibilityRole.Group && e.Node.Name == "אבג English 한글 123").Node.Bounds;
        check(mixedSurface.Width == 80 && page.Samples[0].Source.Contains("Width : 80f", StringComparison.Ordinal)
            && page.Samples[0].Source.Contains("RightToLeft : true", StringComparison.Ordinal),
            "Gallery compact mixed-language RTL controls update the actual portal and adjacent host source");
        var inline = (GallerySample<UiFluentToast>)page.Samples[1];
        var progress = (GallerySample<UiFluentToast>)page.Samples[2];
        inline.Preview.RightToLeft = true;
        inline.Preview.Hide(); inline.SynchronizeSource();
        check(inline.Source.Contains("RightToLeft = true", StringComparison.Ordinal)
            && inline.Source.Contains("toast.Hide();", StringComparison.Ordinal),
            "independent toast source tracks direction and visibility without a controller");
        progress.Preview.Options = progress.Preview.Options with { Progress = new(null, "처리 중") };
        progress.SynchronizeSource();
        check(progress.Source.Contains("Progress = new(null,", StringComparison.Ordinal)
            && progress.Source.Contains("처리 중", StringComparison.Ordinal),
            "indeterminate composition source tracks spinner state and status text");
        ExportSources("changed");
        var pixels = fixture.Capture(Draw);
        fixture.WriteCapturePng(Path.Combine(output, "preview.png"), pixels);
        using var screen = new ObserverScreen(ObserverServices.CreateWindows()) { FreezeDiagnosticText = true, SelectedPage = "Toast" };
        void DrawRoute(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding : 0);
            new UiControlContext(ui).Frame(new(0, 0, fixture.FrameInfo.DisplaySize.X, fixture.FrameInfo.DisplaySize.Y),
                new UiControlContext(ui).Theme.WindowBg, default, 0, 0);
            screen.Render(ui);
        }
        var route = fixture.Capture(DrawRoute);
        check(screen.SelectedNavigationLabel == "Toast" && screen.SourceCountForPage("Toast") == 4
            && !Fluent.MissingComponentNames.Contains("Toast"), "actual Toast route exposes the registered Fluent Toast factory");
        var destination = tree.CaptureSnapshot().Elements.Single(e => e.Node.Role == UiAccessibilityRole.Link && e.Node.Name == "Toast");
        check(destination.Node.ItemStatus != "미구현", "registered Toast remains available when a later NEW batch replaces its badge");
        tree.RequestAction(destination.Node.Id, destination.RuntimeId, UiAccessibilityAction.ScrollIntoView, wait : false);
        for (var settle = 0; settle < 4; settle++) route = fixture.Capture(DrawRoute);
        fixture.WriteCapturePng(Path.Combine(output, "route.png"), route);
    }
}
