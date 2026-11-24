using Duxel.Core;

internal static class HeadsUpDisplayPolicyTests
{
    private static readonly UiColor CustomBackground = new(21, 92, 74);

    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        var taps = 0;
        using var hud = new UiHeadsUpDisplayWidget<PressAndShortDelay, ColoredSurface>(
            new(UiHeadsUpDisplayKind.Success, "Saved"), new(ReducedMotion: true), () => taps++);
        using var root = Dux.Column(new(MainAxisSize: UiMainAxisSize.Min), Fluent.Button("Open", hud.Show), hud);
        var screen = new Screen(root);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);

        bool Frame(float delta = 0, UiInputState? input = null)
        {
            context.NewFrame(new(delta, new(640, 480), new(1, 1)), input ?? idle, new(0, 0, 640, 480), UiTextSettings.Default);
            context.Render(screen);
            using var draw = context.GetDrawData();
            return draw.DrawLists.Any(list => list.Vertices.Any(vertex => vertex.Color == CustomBackground)
                || list.RectFilledPrimitives?.Any(rect => rect.Color == CustomBackground) == true);
        }
        UiAccessibilityElement Find(string name) => tree.CaptureSnapshot().Elements
            .Single(e => e.Node.Name == name && e.Node.Role == UiAccessibilityRole.Button);
        void Ask(string name, UiAccessibilityAction action)
        {
            var node = Find(name);
            check(tree.RequestAction(node.Node.Id, node.RuntimeId, action, wait: false) == UiAccessibilityResult.Success,
                "HUD accepts " + action + " for " + name);
            Frame();
        }

        check(!Frame(), "closed HUD omits the custom surface");
        Ask("Open", UiAccessibilityAction.Focus);
        Ask("Open", UiAccessibilityAction.Invoke);
        check(Frame() && hud.IsOpen, "HUD paints the selected surface");
        check(Find("Saved").Node.Focused, "HUD focuses the modal body");
        var bounds = Find("Saved").Node.Bounds;
        var center = new UiVector2(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
        Frame(input: idle with { MousePosition = center, LeftMouseDown = true, LeftMousePressed = true });
        check(taps == 1 && hud.IsOpen, "HUD invokes the tap on pointer down without dismissing");
        Frame(input: idle with { MousePosition = center, LeftMouseReleased = true });
        check(taps == 1, "HUD does not invoke a second tap on pointer release");
        Ask("Saved", UiAccessibilityAction.Invoke);
        check(taps == 2 && hud.IsOpen, "HUD custom pointer input preserves accessibility invocation");
        hud.Options = hud.Options with { IsPersistent = false };
        Frame();
        Frame(.2f);
        check(hud.IsOpen, "temporary HUD stays open before the custom deadline");
        Frame(.11f);
        check(!hud.IsOpen && Find("Open").Node.Focused, "HUD closes at the custom deadline and restores focus");

        var desired = true;
        var rejected = 0;
        hud.Open = new UiBinding<bool>(() => desired, _ => rejected++);
        Frame();
        Frame(.31f);
        Frame(.5f);
        check(hud.IsOpen && rejected == 1 && context.State.NextScheduledFrameTimeSeconds is null,
            "HUD makes one request when the owner rejects the custom deadline");
        desired = false;
        check(!Frame() && !hud.IsOpen, "HUD owner can close after rejecting the custom deadline");
    }

    private readonly struct PressAndShortDelay : IUiHeadsUpDisplayBehavior
    {
        public static UiButtonActivation PointerActivation => UiButtonActivation.Press;
        public static TimeSpan AutoDismissDelay => TimeSpan.FromMilliseconds(300);
    }

    private readonly struct ColoredSurface : IUiHeadsUpDisplayPresentation
    {
        public static UiHeadsUpDisplayForeground PaintSurface(UiControlContext context, UiRect bounds, UiButtonInteraction interaction)
        {
            context.Frame(bounds, CustomBackground, default, 6, 0);
            return new(new(255, 255, 255), new(203, 255, 221));
        }
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
