using Duxel.Core;
using Duxel.Gallery;

internal static class MessageBarPageTests
{
    internal static void Run(Action<bool, string> check)
    {
        foreach (var viewport in new[] { (Width: 1280f, Height: 707.4625f, Scale: 1f), (Width: 640f, Height: 240f, Scale: 1.25f), (Width: 1280f, Height: 900f, Scale: 1f) })
        {
            using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
            ControlFontFixture.Configure(context);
            using var tree = new UiAccessibilityTree();
            context.SetAccessibilityTree(tree);
            using var page = new MessageBarPage();
            void Frame(float delta = 1f / 60)
            {
                context.NewFrame(new(delta, new(viewport.Width, viewport.Height), new(1, 1)),
                    new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default),
                    new(0, 0, viewport.Width, viewport.Height), UiTextSettings.Default with
                    {
                        Scale = viewport.Scale
                    });
                context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding: 0); page.Render(ui); }));
                using var data = context.GetDrawData();
            }
            Frame();
            check(tree.CaptureSnapshot().Elements.Any(element => element.Node.Name == "메시지 표시"),
                $"Actual MessageBar page renders within bounded height without vertical overflow : {viewport}");
            foreach (var action in new[] { "add", "reverse", "remove", "clear" })
            {
                page.ApplyVerificationAction(action);
                for (var frame = 0; frame < 8; frame++)
                    Frame(.1f);
                page.VerifyDynamicState(action);
                check(tree.CaptureSnapshot().Elements.Count > 0,
                    $"Bounded MessageBar page keeps live content through actual dynamic commands : {viewport} / {action}");
            }
        }
    }
    private sealed class Screen(Action<UiImmediateContext> draw) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => draw(ui);
    }
}
