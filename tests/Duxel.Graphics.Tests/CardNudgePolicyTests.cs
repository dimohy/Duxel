using Duxel.Core;

internal static class CardNudgePolicyTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        var open = true;
        var accept = false;
        var dismissals = 0;
        var actions = 0;
        var binding = new UiBinding<bool>(() => open, value => { dismissals++; if (accept) open = value; });
        using var root = Dux.Column(new(MainAxisSize: UiMainAxisSize.Min),
            new UiCardNudgeWidget<PressCloseRightSwipe, PaddedSurface>("Suggestion", "A helpful tip", binding,
                new UiButton<UiPressButtonBehavior, UiFluentButtonPresentation>("Use tip", () => actions++),
                new(Width: 280, ReducedMotion: true)),
            Fluent.Button("Next"));
        var screen = new Screen(root);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);

        void Frame(UiInputState? input = null)
        {
            context.NewFrame(new(1f / 60, new(640, 480), new(1, 1)), input ?? idle, new(0, 0, 640, 480), UiTextSettings.Default);
            context.Render(screen);
            using var draw = context.GetDrawData();
        }
        UiAccessibilityElement Find(string name, UiAccessibilityRole role = UiAccessibilityRole.Button) =>
            tree.CaptureSnapshot().Elements.Single(e => e.Node.Role == role && e.Node.Name == name);
        void Down(UiVector2 point) => Frame(idle with { MousePosition = point, LeftMouseDown = true, LeftMousePressed = true });
        void Up(UiVector2 point) => Frame(idle with { MousePosition = point, LeftMouseReleased = true });
        void Drag(UiVector2 point, float delta)
        {
            var end = point with { X = point.X + delta };
            Down(point);
            Frame(idle with { MousePosition = end, LeftMouseDown = true });
            Up(end);
            Frame();
        }

        Frame();
        var card = Find("Suggestion", UiAccessibilityRole.Card).Node.Bounds;
        var action = Find("Use tip").Node.Bounds;
        check(action.X == card.X + 26, "CardNudge uses its custom surface padding independently of input");
        static UiVector2 Center(UiRect bounds) => new(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
        Drag(Center(action), 90);
        check(open && actions == 1 && dismissals == 0, "CardNudge honors the child action's press and excludes its drag from swipe dismissal");
        var point = new UiVector2(card.X + card.Width / 2, card.Y + 12);
        Drag(point, -90);
        Drag(point, 60);
        check(open && dismissals == 0, "CardNudge custom swipe rejects the opposite direction and shorter distance");
        Drag(point, 90);
        check(open && dismissals == 1, "CardNudge custom swipe asks the binding to dismiss and honors rejection");

        var close = Center(Find("Dismiss").Node.Bounds);
        Down(close);
        check(open && dismissals == 2, "CardNudge custom close requests dismissal on pointer down");
        Up(close);
        Frame();
        check(open && dismissals == 2 && actions == 1, "CardNudge custom close does not fire again on release");

        accept = true;
        var button = Find("Dismiss");
        check(tree.RequestAction(button.Node.Id, button.RuntimeId, UiAccessibilityAction.Focus, wait: false)
            == UiAccessibilityResult.Success, "CardNudge custom close accepts an accessibility focus request");
        Frame();
        Frame(idle with { KeyEvents = [new(UiKey.Enter, true, default)] });
        Frame(idle with { KeyEvents = [new(UiKey.Enter, false, default)] });
        Frame();
        check(!open && dismissals == 3 && Find("Next").Node.Focused, "CardNudge custom keyboard close repairs focus");
        open = true;
        Frame();
        Drag(point, 90);
        check(!open && dismissals == 4, "CardNudge custom swipe closes when the binding accepts it");
    }

    private readonly struct PressCloseRightSwipe : IUiCardNudgeBehavior
    {
        public static bool AccessibilityFocus => true;
        public static bool ShouldDismiss(UiVector2 displacement) => displacement.X >= 80 && displacement.X > MathF.Abs(displacement.Y);
        public static UiButtonInteraction Process(UiControlContext context, string id, UiRect bounds) =>
            UiPressButtonBehavior.Process(context, id, bounds);
    }

    private readonly struct PaddedSurface : IUiCardPresentation
    {
        public static UiCardMetrics Measure(UiCardOptions options) =>
            UiFluentCardPresentation.Measure(options) with { Padding = 26 };

        public static void Paint(UiControlContext context, UiRect bounds, UiCardOptions options,
            UiCardMetrics metrics, UiButtonInteraction interaction) =>
            UiFluentCardPresentation.Paint(context, bounds, options, metrics, interaction);
    }

    private sealed class Screen(UiWidget root) : UiScreen
    {
        public override void Render(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            root.Render(ui);
        }
    }
}
