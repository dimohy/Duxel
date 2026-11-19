using Duxel.Core;

internal static class WidgetTransformPopupTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        var ime = new Ime();
        context.SetImeHandler(ime);
        var clicks = 0;
        var text = "edit";
        var command = Fluent.Button("Popup command", () => clicks++);
        var editor = Fluent.Input("Popup editor", new UiBinding<string>(() => text, value => text = value), 100, 150);
        using var popover = Fluent.Popover("Rotated trigger", Dux.Column(new(Spacing: 4, MainAxisSize: UiMainAxisSize.Min), command, editor),
            new(Width: 180, MotionEnabled: false, Positioning: new()
            {
                Position = UiPopupPosition.BelowStart
            }));
        popover.Transform = new(RotationDegrees: 90);
        UiWidget current = popover;
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        var view = new View(ui =>
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            ui.SetCursorScreenPos(new(150, 80));
            current.Render(ui);
        });
        void Frame(UiInputState? input = null)
        {
            context.NewFrame(new(1f / 60, new(640, 480), new(1, 1)), input ?? idle, new(0, 0, 640, 480), UiTextSettings.Default);
            context.Render(view);
            using var data = context.GetDrawData();
        }
        UiAccessibilityNode Find(string name, UiAccessibilityRole role) =>
            tree.CaptureSnapshot().Elements.Single(e => e.Node.Name == name && e.Node.Role == role).Node;
        void Click(UiRect bounds)
        {
            var point = new UiVector2(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
            Frame(idle with
            {
                MousePosition = point,
                LeftMouseDown = true,
                LeftMousePressed = true
            });
            Frame(idle with
            {
                MousePosition = point,
                LeftMouseReleased = true
            });
        }
        popover.Show();
        Frame();
        var trigger = Find("Rotated trigger", UiAccessibilityRole.Button);
        var surface = Find("Rotated trigger", UiAccessibilityRole.Group);
        var expected = UiPopupPlacement.Arrange(trigger.Bounds, new(surface.Bounds.Width, surface.Bounds.Height),
            new(0, 0, 640, 480), popover.Options.Positioning).Bounds;
        check(surface.Bounds == expected, "rotated Popover surface uses the screen trigger bounds without a second transform");
        Click(Find("Popup command", UiAccessibilityRole.Button).Bounds);
        check(clicks == 1 && popover.IsOpen, "actual screen-space popup pointer activates its child under a rotated trigger");
        var field = Find("Popup editor", UiAccessibilityRole.Edit).Bounds;
        Click(field);
        check(ime.Input == field && ime.Caret is { } caret && caret.X >= field.X && caret.Y >= field.Y
            && caret.X + caret.Width <= field.X + field.Width && caret.Y + caret.Height <= field.Y + field.Height,
            "popup editor IME geometry escapes the rotated owner's caret mapping");
        Frame(idle with
        {
            KeyEvents = [new(UiKey.Escape, true, default)]
        });
        check(!popover.IsOpen && context.State.FocusedId == trigger.Id, "transformed Popover Escape restores its trigger focus");
        using var tooltip = new UiTooltip<UiHoverFocusTooltipBehavior, ProbeTooltip>(Fluent.Button("Tooltip target"), "Tip")
        {
            Visible = true,
            Transform = new(RotationDegrees: 90)
        };
        current = tooltip;
        Frame();
        Frame();
        check(ProbeTooltip.Target == Find("Tooltip target", UiAccessibilityRole.Button).Bounds,
            $"deferred Tooltip arrangement receives transformed screen bounds : target={ProbeTooltip.Target}, button={Find("Tooltip target", UiAccessibilityRole.Button).Bounds}");
        tooltip.Transform = new(Scale: 2, RotationDegrees: 90);
        Frame();
        check(ProbeTooltip.Target == Find("Tooltip target", UiAccessibilityRole.Button).Bounds,
            "visible Tooltip follows dynamic transformed target scale without pointer presence");
        string? selected = null;
        using var menu = new UiMenu<UiFluentMenuPresentation>("Rotated menu", [new("one", "First action"), new("two", "Second action")], value => selected = value)
        {
            Transform = new(RotationDegrees: 90)
        };
        current = menu;
        Frame();
        Frame();
        Click(Find("Rotated menu", UiAccessibilityRole.Button).Bounds);
        var item = Find("First action", UiAccessibilityRole.MenuItem);
        check(item.Bounds.X >= 140 && item.Bounds.Width > item.Bounds.Height,
            "transformed menu publishes upright screen-space item geometry");
        Click(item.Bounds);
        check(selected == "one", "actual screen pointer chooses an item under a rotated menu trigger");
        var nestedClicks = 0;
        var inner = Fluent.Popover("Inner trigger", Fluent.Button("Nested command", () => nestedClicks++),
            options: new(Width: 180, MotionEnabled: false));
        inner.Transform = new(RotationDegrees: 90);
        using var outer = Fluent.Popover("Outer trigger", inner, options: new(Width: 240, MotionEnabled: false));
        outer.Transform = new(RotationDegrees: 45);
        current = outer;
        outer.Show();
        Frame();
        Frame();
        inner.Show();
        Frame();
        var innerTrigger = Find("Inner trigger", UiAccessibilityRole.Button);
        var innerSurface = Find("Inner trigger", UiAccessibilityRole.Group);
        check(innerSurface.Bounds == UiPopupPlacement.Arrange(innerTrigger.Bounds,
            new(innerSurface.Bounds.Width, innerSurface.Bounds.Height), new(0, 0, 640, 480), inner.Options.Positioning).Bounds,
            "nested transformed Popover starts a new screen portal from its actual trigger");
        Click(Find("Nested command", UiAccessibilityRole.Button).Bounds);
        check(nestedClicks == 1 && outer.IsOpen && inner.IsOpen, "nested portal click preserves both transformed owners");
        Frame(idle with
        {
            KeyEvents = [new(UiKey.Escape, true, default)]
        });
        check(!inner.IsOpen && outer.IsOpen && context.State.FocusedId == innerTrigger.Id,
            "nested screen portal Escape restores the inner trigger without closing its outer owner");
    }

    private readonly struct ProbeTooltip : IUiTooltipPresentation
    {
        internal static UiRect Target;
        public static UiPopupLayout Arrange(UiControlContext context, string text, UiRect target, UiRect viewport, UiTooltipOptions options)
        {
            Target = target;
            return new(new(target.X, target.Y + target.Height + 8, 90, 28), UiPopupPosition.Below);
        }
        public static void Paint(UiControlContext context, string text, UiPopupLayout layout) =>
            context.Frame(layout.Bounds, new(200, 50, 20), default, 0, 0);
    }
    private sealed class View(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
    private sealed class Ime : IUiImeHandler
    {
        internal UiRect Input;
        internal UiRect? Caret;
        public void SetCaretRect(UiRect caretRect, UiRect inputRect, float fontPixelHeight, float fontPixelWidth) => (Caret, Input) = (caretRect, inputRect);
        public string? GetCompositionText() => null;
        public void SetCompositionOwner(string? inputId)
        {
        }
        public string? ConsumeCommittedText(string inputId) => null;
        public void ReleaseInput(string inputId) => _ = ConsumeCommittedText(inputId);
        public string? ConsumeRecentCommittedText() => null;
    }
}
