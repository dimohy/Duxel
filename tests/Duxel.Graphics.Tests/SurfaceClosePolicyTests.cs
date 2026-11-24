using Duxel.Core;

internal static class SurfaceClosePolicyTests
{
    internal static void Run(Action<bool, string> check)
    {
        Verify("Dialog", modal: true, open =>
        {
            var widget = new UiDialog<UiPressButtonBehavior, UiFluentDialogPresentation>(
                "Surface", Fluent.Body1("Body"), options: new(Width: 320, ShowCloseButton: true, CloseLabel: "Dismiss", MotionEnabled: false))
            {
                Open = open
            };
            return (widget, widget.Show, () => widget.IsOpen);
        }, check);
        Verify("Overlay Drawer", modal: true, open => CreateDrawer(open, UiDrawerType.Overlay), check);
        Verify("Inline Drawer", modal: false, open => CreateDrawer(open, UiDrawerType.Inline), check);
    }

    private static (UiWidget, Action, Func<bool>) CreateDrawer(UiBinding<bool> open, UiDrawerType type)
    {
        var widget = new UiDrawer<UiPressButtonBehavior, UiFluentDrawerPresentation>(
            "Surface", Fluent.Body1("Body"), options: new(Type: type, InlineCrossExtent: 220, CloseLabel: "Dismiss", MotionEnabled: false))
        {
            Open = open
        };
        return (widget, widget.Show, () => widget.IsOpen);
    }

    private static void Verify(string name, bool modal,
        Func<UiBinding<bool>, (UiWidget Widget, Action Show, Func<bool> IsOpen)> create, Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        var open = false;
        var allowClose = false;
        var closeRequests = 0;
        var binding = new UiBinding<bool>(() => open, value =>
        {
            if (!value) closeRequests++;
            if (value || allowClose) open = value;
        });
        var (widget, show, isOpen) = create(binding);
        using var root = Dux.Column(new(MainAxisSize: UiMainAxisSize.Min), Fluent.Button("Open", show), widget);
        var screen = new Screen(root);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);

        void Frame(UiInputState? input = null)
        {
            context.NewFrame(new(1f / 60, new(640, 480), new(1, 1)), input ?? idle, new(0, 0, 640, 480), UiTextSettings.Default);
            context.Render(screen);
            using var draw = context.GetDrawData();
        }
        UiAccessibilityElement Find(string label) => tree.CaptureSnapshot().Elements
            .Single(e => e.Node.Role == UiAccessibilityRole.Button && e.Node.Name == label);
        void Ask(string label, UiAccessibilityAction action)
        {
            var node = Find(label);
            check(tree.RequestAction(node.Node.Id, node.RuntimeId, action, wait: false) == UiAccessibilityResult.Success,
                name + " accepts " + action + " for " + label);
            Frame();
            Frame();
        }
        UiVector2 CloseCenter()
        {
            var bounds = Find("Dismiss").Node.Bounds;
            return new(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
        }
        void Down(UiVector2 p) => Frame(idle with { MousePosition = p, LeftMousePressed = true, LeftMouseDown = true });
        void Up(UiVector2 p) => Frame(idle with { MousePosition = p, LeftMouseReleased = true });

        Frame();
        Ask("Open", UiAccessibilityAction.Focus);
        Ask("Open", UiAccessibilityAction.Invoke);
        var center = CloseCenter();
        Down(center);
        check(isOpen() && open && closeRequests == 1, name + " requests close on pointer down and honors binding rejection");
        Up(center);
        Frame();
        check(isOpen() && closeRequests == 1, name + " does not repeat the close request on release");
        allowClose = true;
        Ask("Dismiss", UiAccessibilityAction.Invoke);
        check(!isOpen() && !open && closeRequests == 2, name + " supports accessibility invocation with custom pointer input");
        if (modal)
            check(Find("Open").Node.Focused, name + " restores focus to the invoking control");

        Ask("Open", UiAccessibilityAction.Invoke);
        center = CloseCenter();
        Down(center);
        check(!isOpen() && !open && closeRequests == 3, name + " closes before pointer release when accepted");
        Up(center);
        Frame();
        check(closeRequests == 3, name + " retires the accepted press once");
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
