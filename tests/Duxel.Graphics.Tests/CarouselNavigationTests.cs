using Duxel.Core;

internal static class CarouselNavigationTests
{
    internal static void Run(Action<bool, string> check)
    {
        Verify<UiPressButtonBehavior, UiFluentButtonPresentation>("Press/Fluent", onPress: true,
            customWidth: false, rtl: false, check);
        Verify<UiReleaseButtonBehavior, WideStepPresentation>("Release/Wide", onPress: false,
            customWidth: true, rtl: true, check);
        Verify<UiPressButtonBehavior, WideStepPresentation>("Press/Wide", onPress: true,
            customWidth: true, rtl: true, check);
    }

    private static void Verify<TBehavior, TPresentation>(string name, bool onPress, bool customWidth, bool rtl,
        Action<bool, string> check)
        where TBehavior : IUiButtonBehavior
        where TPresentation : IUiButtonPresentation
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        var selected = "a";
        var accept = true;
        using var carousel = new UiCarouselWidget<TBehavior, TPresentation>("Places",
            new(() => selected, value => { if (accept) selected = value; }),
            new UiCarouselSlide[]
            {
                new("a", "First", Fluent.Text("First body")),
                new("b", "Second", Fluent.Text("Second body")),
                new("c", "Third", Fluent.Text("Third body"))
            }, new() { RightToLeft = rtl, ReducedMotion = true });
        var screen = new Screen(carousel.Render);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);

        void Frame(UiInputState? input = null)
        {
            context.NewFrame(new(1f / 60, new(320, 320), new(1, 1)), input ?? idle, new(0, 0, 320, 320), UiTextSettings.Default);
            context.Render(screen);
            using var draw = context.GetDrawData();
        }

        UiAccessibilityElement Find(string label) => tree.CaptureSnapshot().Elements.Single(e => e.Node.Name == label);
        void Ask(string label, UiAccessibilityAction action)
        {
            var node = Find(label);
            check(tree.RequestAction(node.Node.Id, node.RuntimeId, action, wait: false) == UiAccessibilityResult.Success,
                name + " accepts " + action + " for " + label);
            Frame();
            Frame();
        }

        Frame();
        var next = Find("Next slide").Node.Bounds;
        var previous = Find("Previous slide").Node;
        check(!previous.Enabled && (previous.Bounds.X > next.X) == rtl && (next.Width == 72) == customWidth,
            name + " measures the chosen presentation, orders RTL arrows and disables the first edge");
        var point = new UiVector2(next.X + next.Width / 2, next.Y + next.Height / 2);
        Frame(idle with { MousePosition = point, LeftMousePressed = true, LeftMouseDown = true });
        check(selected == (onPress ? "b" : "a"), name + " uses the requested pointer-down edge");
        Frame(idle with { MousePosition = point, LeftMouseReleased = true });
        Frame();
        check(selected == "b" && Find("Second").Node.Selected == true, name + " selects one step on the full click");

        accept = false;
        Ask("Next slide", UiAccessibilityAction.Invoke);
        check(selected == "b" && Find("Second").Node.Selected == true, name + " honors rejected controlled selection");
        accept = true;
        Ask("Second", UiAccessibilityAction.Focus);
        var key = rtl ? UiKey.LeftArrow : UiKey.RightArrow;
        Frame(idle with { KeyEvents = [new(key, true, default)] });
        Frame(idle with { KeyEvents = [new(key, false, default)] });
        check(selected == "b" && Find("Third").Node.Focused, name + " moves tab focus without changing the selected slide");
        Frame(idle with { KeyEvents = [new(UiKey.Enter, true, default)] });
        Frame(idle with { KeyEvents = [new(UiKey.Enter, false, default)] });
        check(selected == "c" && !Find("Next slide").Node.Enabled, name + " commits the focused step on Enter");
        Ask("Previous slide", UiAccessibilityAction.Invoke);
        check(selected == "b" && Find("Second").Node.Selected == true, name + " invokes the custom previous button accessibly");
    }

    private readonly struct WideStepPresentation : IUiButtonPresentation
    {
        public static UiControlTypographyScope BeginText(UiControlContext context, bool hasText) =>
            UiFluentButtonPresentation.BeginText(context, hasText);
        public static UiVector2 Measure(UiControlContext context, UiVector2 textSize, UiVector2 requestedSize) => new(72, 32);
        public static void Paint(UiControlContext context, string text, UiVector2 textSize, UiRect bounds,
            UiButtonInteraction interaction) => UiFluentButtonPresentation.Paint(context, text, textSize, bounds, interaction);
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
