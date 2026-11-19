using Duxel.Core;

internal static class NavTests
{
    internal static void Run(Action<bool, string> check)
    {
        NavSectionHeaderTests.Run(check);
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        var value = new UiState<string?>("home");
        UiNavItem[] copied = [new("home", "Home"), new("disabled", "Disabled", false), new("recent", "Recent", Badge : new("New"))];
        using var nav = Fluent.Nav("Main navigation", value, new UiNavGroup[]
        {
            new("main", "Main", copied),
            new("more", "More", new UiNavItem[] { new("long", "A long destination name wraps in the available navigation width") })
        }, new(160));
        copied[0] = new("replaced", "Replaced");
        void Reject(Action action, string name)
        {
            var rejected = false;
            try { action(); }
            catch (ArgumentException) { rejected = true; }
            check(rejected, name);
        }
        Reject(() => { using var invalid = Fluent.Nav("Invalid", value, new UiNavGroup[]
            { new("same", "Group", new UiNavItem[] { new("same", "Destination") }) }); },
            "Nav rejects duplicate keys across categories and destinations before rendering");
        Reject(() => { using var invalid = Fluent.Nav("Invalid", new UiState<string?>("missing"), new UiNavGroup[]
            { new("group", "Group", new UiNavItem[] { new("known", "Known") }) }); },
            "Nav rejects an undeclared controlled destination before rendering");
        Reject(() => { using var invalid = Fluent.Nav("Invalid", value, Array.Empty<UiNavGroup>(), new(float.NaN)); },
            "Nav rejects nonfinite width before rendering");
        Reject(() => nav.SetExpanded("missing", false), "Nav rejects changing an undeclared category");
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        var duplicateRejected = false;
        var parentDisabled = false;
        void Frame(UiInputState? input = null, bool duplicate = false)
        {
            context.NewFrame(new(1f / 60, new(640, 640), new(1, 1)), input ?? idle, new(0, 0, 640, 640), UiTextSettings.Default);
            context.Render(new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding : 0);
                ui.BeginDisabled(parentDisabled);
                try
                {
                    nav.Render(ui);
                    ui.SetCursorScreenPos(new(200, 0));
                    nav.RenderPage(ui, value.Value!, body => body.Text("Current body"));
                    if (duplicate)
                    {
                        try { nav.RenderPage(ui, value.Value!, body => body.Text("Duplicate")); }
                        catch (InvalidOperationException) { duplicateRejected = true; }
                    }
                }
                finally { ui.EndDisabled(); }
            }));
            using var draw = context.GetDrawData();
        }
        UiAccessibilityElement Find(string name, UiAccessibilityRole role) => tree.CaptureSnapshot().Elements.Single(element => element.Node.Name == name && element.Node.Role == role);
        void Key(UiKey key) => Frame(idle with { KeyEvents = [new(key, true, default)] });
        void Focus(string name, UiAccessibilityRole role)
        {
            var item = Find(name, role);
            check(tree.RequestAction(item.Node.Id, item.RuntimeId, UiAccessibilityAction.Focus, wait : false) == UiAccessibilityResult.Success,
                "Nav accepts accessible focus : " + name);
            Frame();
        }
        void Click(string name, UiAccessibilityRole role)
        {
            var bounds = Find(name, role).Node.Bounds;
            var point = new UiVector2(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
            Frame(idle with { MousePosition = point, LeftMouseDown = true, LeftMousePressed = true });
            Frame(idle with { MousePosition = point, LeftMouseReleased = true });
        }
        Frame(duplicate : true);
        check(duplicateRejected, "Nav rejects rendering its borrowed page twice in one frame");
        var home = Find("Home", UiAccessibilityRole.Link).Node;
        var page = Find("Home", UiAccessibilityRole.Pane).Node;
        check(home.IsCurrent && home.ControlledElementId == page.Id && page.LabeledBy == home.Id
            && Find("Main navigation", UiAccessibilityRole.Navigation).Node.Role == UiAccessibilityRole.Navigation,
            "Nav copies source data and exposes Navigation, current Link and its reciprocal Pane");
        check(Find("Recent", UiAccessibilityRole.Link).Node.ItemStatus == "New", "Nav badges preserve the destination name and expose their status");
        check(!Find("Disabled", UiAccessibilityRole.Link).Node.Focusable, "Nav disabled destinations do not participate in keyboard focus");
        check(Find("A long destination name wraps in the available navigation width", UiAccessibilityRole.Link).Node.Bounds.Height > home.Bounds.Height,
            "Nav long destination labels wrap rather than becoming icon-only");
        Focus("Home", UiAccessibilityRole.Link);
        Key(UiKey.DownArrow);
        check(context.State.FocusedId == Find("Recent", UiAccessibilityRole.Link).Node.Id && value.Value == "home",
            "Nav Down skips disabled destinations without changing the current page");
        Key(UiKey.Enter);
        check(value.Value == "recent" && Find("Recent", UiAccessibilityRole.Link).Node.IsCurrent, "Nav Enter activates the focused destination in the same frame");
        Key(UiKey.LeftArrow);
        check(context.State.FocusedId == Find("Main", UiAccessibilityRole.Button).Node.Id, "Nav Left returns from a destination to its category");
        Key(UiKey.LeftArrow);
        check(!nav.IsExpanded("main") && value.Value == "recent" && Find("Main", UiAccessibilityRole.Button).Node.IsCurrent
            && Find("Recent", UiAccessibilityRole.Pane).Node.LabeledBy == Find("Main", UiAccessibilityRole.Button).Node.Id,
            "Nav collapsed category preserves and represents its selected descendant and page label");
        Key(UiKey.RightArrow);
        check(nav.IsExpanded("main"), "Nav Right expands a focused category");
        Key(UiKey.RightArrow);
        check(context.State.FocusedId == Find("Home", UiAccessibilityRole.Link).Node.Id, "Nav Right enters the expanded category");
        Key(UiKey.End);
        check(context.State.FocusedId == Find("A long destination name wraps in the available navigation width", UiAccessibilityRole.Link).Node.Id,
            "Nav End reaches the final enabled destination");
        Key(UiKey.Home);
        check(context.State.FocusedId == Find("Main", UiAccessibilityRole.Button).Node.Id, "Nav Home reaches the first category");
        nav.Options = nav.Options with { RightToLeft = true };
        Frame();
        Key(UiKey.RightArrow);
        check(!nav.IsExpanded("main"), "Nav RTL reverses horizontal collapse navigation");
        Click("Main", UiAccessibilityRole.Button);
        check(nav.IsExpanded("main"), "Nav pointer disclosure expands the category");
        Click("Home", UiAccessibilityRole.Link);
        check(value.Value == "home", "Nav pointer destination updates the controlled selection");
        Focus("Recent", UiAccessibilityRole.Link);
        var category = Find("Main", UiAccessibilityRole.Button);
        check(tree.RequestAction(category.Node.Id, category.RuntimeId, UiAccessibilityAction.Collapse, wait : false) == UiAccessibilityResult.Success,
            "Nav category supports accessible Collapse");
        Frame();
        check(!nav.IsExpanded("main") && context.State.FocusedId == Find("Main", UiAccessibilityRole.Button).Node.Id,
            "Nav accessible Collapse restores hidden descendant focus to its category");
        nav.SetExpanded("main", true);
        parentDisabled = true;
        Frame();
        Click("Recent", UiAccessibilityRole.Link);
        check(value.Value == "home", "Nav inherited disabled state blocks destination activation");
        using var empty = Fluent.Nav("Empty navigation", new UiState<string?>(null), new UiNavGroup[]
            { new("empty", "Empty category", Array.Empty<UiNavItem>()), new("static", "Static category", Array.Empty<UiNavItem>(), Collapsible : false) });
        context.NewFrame(new(1f / 60, new(640, 640), new(1, 1)), idle, new(0, 0, 640, 640), UiTextSettings.Default);
        context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding : 0); empty.Render(ui); }));
        using (var draw = context.GetDrawData())
        {
            check(Find("Empty category", UiAccessibilityRole.Button).Node.ControlledElementId is null,
                "Nav empty categories do not publish dangling accessibility relationships");
            check(!Find("Static category", UiAccessibilityRole.Label).Node.Focusable
                && Find("Static category", UiAccessibilityRole.Label).Node.Enabled,
                "Nav noncollapsible categories publish passive headings");
        }

        context.NewFrame(new(1f / 60, new(640, 640), new(1, 1)), idle, new(0, 0, 640, 640), UiTextSettings.Default);
        context.Render(new Screen(ui =>
        {
            ui.EnableRootViewportContentLayout(contentPadding : 0);
            nav.RenderPage(ui, "home", body => body.Text("Standalone page"), navigationVisible : false);
        }));
        using (var draw = context.GetDrawData())
        {
            check(Find("Home", UiAccessibilityRole.Pane).Node.LabeledBy is null
                && !tree.CaptureSnapshot().Elements.Any(element => element.Node.Role == UiAccessibilityRole.Navigation),
                "Nav hidden-navigation page remains named without referencing an absent destination");
        }

    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
