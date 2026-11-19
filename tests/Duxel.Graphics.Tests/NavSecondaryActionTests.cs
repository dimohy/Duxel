using Duxel.Core;

internal static class NavSecondaryActionTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        var selected = new UiState<string?>("other");
        var calls = 0;
        var afterCalls = 0;
        var icon = UiMenuIcon.Create<ActionIcon, ActionIcon>();
        var action = new UiNavSecondaryAction("Pin Home", () => calls++, icon);
        using var nav = new UiNav<Presentation>("Navigation actions", selected, new UiNavGroup[]
        {
            new("main", "Main", new UiNavItem[] { new("home", "Home", SecondaryAction : action),
                new("other", "Other"), new("disabled", "Disabled destination", false, SecondaryAction : action with { Label = "Disabled owner action" }),
                new("disabled-action", "Disabled action owner", SecondaryAction : action with { Label = "Disabled action", Enabled = false }) })
        }, new(250));
        using var after = Fluent.Button("After navigation", () => afterCalls++);
        var parentDisabled = false;
        var renderNav = true;
        var scope = "original";
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(UiInputState? input = null)
        {
            Presentation.ActionPaints = 0;
            context.NewFrame(new(1f / 60, new(640, 640), new(1, 1)), input ?? idle, new(0, 0, 640, 640), UiTextSettings.Default);
            context.Render(new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding : 0);
                ui.BeginDisabled(parentDisabled);
                ui.PushID(scope);
                try { if (renderNav) nav.Render(ui); }
                finally { ui.PopID(); ui.EndDisabled(); }
                after.Render(ui);
            }));
            using var data = context.GetDrawData();
        }
        UiAccessibilityElement Find(string name, UiAccessibilityRole role) => tree.CaptureSnapshot().Elements.Single(element => element.Node.Name == name && element.Node.Role == role);
        UiVector2 Point(string name, UiAccessibilityRole role)
        {
            var bounds = Find(name, role).Node.Bounds;
            return new(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
        }
        void Focus(string name, UiAccessibilityRole role)
        {
            var entry = Find(name, role);
            if (tree.RequestAction(entry.Node.Id, entry.RuntimeId, UiAccessibilityAction.Focus, wait : false) != UiAccessibilityResult.Success)
                throw new InvalidOperationException("Nav action focus rejected : " + name);
            Frame();
        }
        void Key(UiKey key, KeyModifiers modifiers = default) => Frame(idle with { KeyEvents = [new(key, true, modifiers)] });
        void Click(UiVector2 point)
        {
            Frame(idle with { MousePosition = point, LeftMouseDown = true, LeftMousePressed = true });
            Frame(idle with { MousePosition = point, LeftMouseReleased = true });
        }
        void RightClick(UiVector2 down, UiVector2 up)
        {
            Frame(idle with { MousePosition = down, RightMouseDown = true, RightMousePressed = true });
            Frame(idle with { MousePosition = up, RightMouseReleased = true });
        }
        bool MenuOpen() => nav.IsContextMenuOpen;
        Frame();
        var actionNode = Find("Pin Home", UiAccessibilityRole.Button);
        check(!actionNode.Node.Offscreen && actionNode.Node.Enabled && Presentation.ActionPaints == 0,
            "Nav secondary action stays in the accessibility tree while its idle visual is hidden");
        check(Find("Disabled action", UiAccessibilityRole.Button).Node.Enabled == false
            && Find("Disabled owner action", UiAccessibilityRole.Button).Node.Enabled == false,
            "Nav secondary action combines its own and its destination enabled state");
        var home = Point("Home", UiAccessibilityRole.Link);
        var pin = Point("Pin Home", UiAccessibilityRole.Button);
        var other = Point("Other", UiAccessibilityRole.Link);
        var primaryBounds = Find("Home", UiAccessibilityRole.Link).Node.Bounds;
        var secondaryBounds = Find("Pin Home", UiAccessibilityRole.Button).Node.Bounds;
        check(primaryBounds.X + primaryBounds.Width <= secondaryBounds.X,
            "Nav accessible primary and secondary bounds match their separate pointer targets");
        var initialMeasures = Presentation.Measures;
        Frame(idle with { MousePosition = home });
        check(Presentation.ActionPaints > 0, "Nav destination hover reveals its secondary action");
        Click(pin);
        check(calls == 1 && selected.Value == "other", "Nav secondary pointer action executes once without selecting its destination");
        Frame(idle with { MousePosition = home, LeftMouseDown = true, LeftMousePressed = true });
        Frame(idle with { MousePosition = pin, LeftMouseReleased = true });
        check(calls == 1 && selected.Value == "other", "Nav primary press released on its secondary action activates neither control");
        Frame(idle with { MousePosition = pin, LeftMouseDown = true, LeftMousePressed = true });
        Frame(idle with { MousePosition = home, LeftMouseReleased = true });
        check(calls == 1 && selected.Value == "other", "Nav secondary press released on its primary destination activates neither control");
        Click(home);
        check(selected.Value == "home" && calls == 1, "Nav destination remains independently pointer-operable next to its action");
        Focus("Home", UiAccessibilityRole.Link);
        Key(UiKey.Tab);
        check(context.State.FocusedId == Find("Pin Home", UiAccessibilityRole.Button).Node.Id, "Nav Tab moves from a destination to its secondary action");
        check(Presentation.ActionPaints > 0, "Nav focused secondary action remains visibly painted");
        Key(UiKey.Enter);
        check(calls == 2 && selected.Value == "home", "Nav secondary Enter invokes its action without navigation");
        Key(UiKey.Tab);
        check(context.State.FocusedId == Find("After navigation", UiAccessibilityRole.Button).Node.Id,
            "Nav Tab exits the composite after its focused secondary action");
        Focus("Home", UiAccessibilityRole.Link);
        Key(UiKey.RightArrow);
        check(context.State.FocusedId == Find("Pin Home", UiAccessibilityRole.Button).Node.Id, "Nav outgoing horizontal arrow reaches the secondary action");
        Key(UiKey.LeftArrow);
        check(context.State.FocusedId == Find("Home", UiAccessibilityRole.Link).Node.Id, "Nav incoming horizontal arrow returns from action to destination");
        Key(UiKey.RightArrow);
        Key(UiKey.DownArrow);
        check(context.State.FocusedId == Find("Other", UiAccessibilityRole.Link).Node.Id, "Nav Down from a secondary action continues destination navigation");
        check(Presentation.Measures == initialMeasures, "Nav hover, action, selection and focus changes reuse unchanged row measurement");
        selected.Value = "other";
        Frame(idle with { MousePosition = home, RightMouseDown = true, RightMousePressed = true });
        Frame();
        Frame(idle with { MousePosition = home, RightMouseReleased = true });
        check(!MenuOpen(), "Nav cancelled right-button input cannot leave a stale contextual press");
        Frame(idle with { MousePosition = home, RightMouseDown = true, RightMousePressed = true });
        renderNav = false;
        Frame(idle with { MousePosition = home, RightMouseDown = true });
        renderNav = true;
        Frame(idle with { MousePosition = home, RightMouseReleased = true });
        check(!MenuOpen(), "Nav removal while right-button input is held cancels its contextual press");
        RightClick(home, other);
        check(!MenuOpen() && selected.Value == "other", "Nav right press released on a different row does not open its contextual action");
        RightClick(home, home);
        check(MenuOpen() && selected.Value == "other", "Nav matched right click opens the contextual menu without navigation");
        var menuItem = Find("Pin Home", UiAccessibilityRole.MenuItem);
        check(tree.RequestAction(menuItem.Node.Id, menuItem.RuntimeId, UiAccessibilityAction.Invoke, wait : false) == UiAccessibilityResult.Success,
            "Nav contextual action exposes a real accessible MenuItem");
        Frame();
        check(calls == 3 && !MenuOpen() && selected.Value == "other", "Nav contextual action invokes the same callback and dismisses its menu");
        Frame();
        check(!tree.CaptureSnapshot().Elements.Any(element => element.Node.Role == UiAccessibilityRole.Menu),
            "Nav dismissed contextual menu retires its accessibility subtree on the following frame");
        Focus("Home", UiAccessibilityRole.Link);
        Key(UiKey.F10, KeyModifiers.Shift);
        check(MenuOpen(), "Nav Shift F10 opens the destination contextual action");
        Key(UiKey.Escape);
        check(!MenuOpen() && context.State.FocusedId == Find("Home", UiAccessibilityRole.Link).Node.Id,
            "Nav contextual Escape restores destination focus");
        Key(UiKey.ContextMenu);
        check(MenuOpen(), "Nav ContextMenu key opens the same contextual action");
        nav.SetExpanded("main", false);
        Frame();
        check(!MenuOpen() && context.State.FocusedId == Find("Main", UiAccessibilityRole.Button).Node.Id,
            "Nav category collapse retires an open contextual menu and restores category focus");
        nav.SetExpanded("main", true);
        Frame();
        var pinEntry = Find("Pin Home", UiAccessibilityRole.Button);
        check(tree.RequestAction(pinEntry.Node.Id, pinEntry.RuntimeId, UiAccessibilityAction.Expand, wait : false) == UiAccessibilityResult.Success,
            "Nav secondary action exposes accessible context-menu expansion while visually idle");
        Frame();
        check(MenuOpen(), "Nav consumes accessible context-menu expansion");
        pinEntry = Find("Pin Home", UiAccessibilityRole.Button);
        tree.RequestAction(pinEntry.Node.Id, pinEntry.RuntimeId, UiAccessibilityAction.Collapse, wait : false);
        Frame();
        check(!MenuOpen() && context.State.FocusedId == Find("Pin Home", UiAccessibilityRole.Button).Node.Id,
            "Nav accessible collapse closes its contextual menu and restores the invoking action focus");
        Focus("Home", UiAccessibilityRole.Link);
        nav.Options = nav.Options with { RightToLeft = true };
        Frame();
        Key(UiKey.LeftArrow);
        check(context.State.FocusedId == Find("Pin Home", UiAccessibilityRole.Button).Node.Id, "Nav RTL reverses the horizontal action-entry arrow");
        primaryBounds = Find("Home", UiAccessibilityRole.Link).Node.Bounds;
        secondaryBounds = Find("Pin Home", UiAccessibilityRole.Button).Node.Bounds;
        check(secondaryBounds.X + secondaryBounds.Width <= primaryBounds.X,
            "Nav RTL preserves nonoverlapping primary and secondary accessibility targets");
        parentDisabled = true;
        Frame();
        pin = Point("Pin Home", UiAccessibilityRole.Button);
        Click(pin);
        RightClick(Point("Home", UiAccessibilityRole.Link), Point("Home", UiAccessibilityRole.Link));
        check(calls == 3 && !MenuOpen(), "Nav inherited disabled state blocks direct and contextual secondary actions");
        parentDisabled = false;
        Frame();
        Focus("Home", UiAccessibilityRole.Link);
        Key(UiKey.ContextMenu);
        check(MenuOpen(), "Nav contextual menu can reopen after disabled-state recovery");
        var originalItems = Find("Main", UiAccessibilityRole.Group).Node.Id;
        scope = "reparented";
        Frame();
        check(!MenuOpen(), "Nav identity changes retire the old contextual menu and tooltip ownership");
        var newItems = Find("Main", UiAccessibilityRole.Group).Node.Id;
        check(newItems != originalItems && Find("Main", UiAccessibilityRole.Button).Node.ControlledElementId == newItems
            && Find("Home", UiAccessibilityRole.Link).Node.ParentId == newItems
            && Find("Pin Home", UiAccessibilityRole.Button).Node.ParentId == newItems,
            "Nav reparenting refreshes the shared accessibility group ID and its relationships");
        Focus("Home", UiAccessibilityRole.Link);
        Key(UiKey.ContextMenu);
        check(MenuOpen(), "Nav contextual actions remain operable after an identity change");
        renderNav = false;
        Frame();
        check(!MenuOpen(), "Nav removal retires its active contextual menu through the common menu frame lifecycle");
        check(afterCalls == 0, "Nav secondary keyboard and popup input never invokes unrelated following controls");
    }
    private readonly struct ActionIcon : IUiButtonIcon
    {
        public static void Paint(UiControlContext context, UiRect bounds, UiColor color, UiButtonInteraction interaction) =>
            context.Circle(new(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2), 4, color);
    }
    private readonly struct Presentation : IUiNavPresentation
    {
        internal static int ActionPaints;
        internal static int Measures;
        public static bool CanRetainMeasurement => true;
        public static UiNavRowLayout Measure(UiControlContext context, UiNavRowContent content, float width)
        {
            Measures++;
            return UiFluentNavPresentation.Measure(context, content, width);
        }
        public static void Paint(UiControlContext context, UiNavRowContent content, UiNavRowLayout layout, UiRect bounds,
            bool current, bool expanded, UiButtonInteraction interaction, UiNavOptions options) =>
            UiFluentNavPresentation.Paint(context, content, layout, bounds, current, expanded, interaction, options);
        public static UiRect SecondaryActionBounds(UiRect row, UiNavOptions options) => UiFluentNavPresentation.SecondaryActionBounds(row, options);
        public static void PaintSecondaryAction(UiControlContext context, UiNavSecondaryAction action, UiRect bounds, UiButtonInteraction interaction, UiNavOptions options)
        {
            ActionPaints++;
            UiFluentNavPresentation.PaintSecondaryAction(context, action, bounds, interaction, options);
        }
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
