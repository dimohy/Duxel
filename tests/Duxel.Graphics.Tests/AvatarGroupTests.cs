using Duxel.Core;

internal static class AvatarGroupTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        var calls = new List<string>();
        UiAvatarGroupItem[] items = Enumerable.Range(0, 6).Select(i => new UiAvatarGroupItem("value" + i, "Person " + i)).ToArray();
        using var group = Fluent.AvatarGroup("People", items, selected: calls.Add);
        var disabled = false;
        Action<UiImmediateContext>? renderOverride = null;
        void Frame(UiInputState? input = null)
        {
            context.NewFrame(new(0, new(640, 480), new(1, 1)), input ?? idle, new(0, 0, 640, 480), UiTextSettings.Default);
            context.Render(new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding: 0);
                ui.BeginDisabled(disabled);
                try
                {
                    if (renderOverride is null)
                        group.Render(ui);
                    else
                        renderOverride(ui);
                }
                finally { ui.EndDisabled(); }
            }));
            using var data = context.GetDrawData();
        }
        UiAccessibilityElement[] Elements()
        {
            var all = new List<UiAccessibilityElement>();
            void Visit(string? id)
            {
                while (id is not null && tree.TryGet(id, out var element))
                {
                    all.Add(element);
                    Visit(element.FirstChild);
                    id = element.Next;
                }
            }
            Visit(tree.Root.First);
            return all.ToArray();
        }
        UiAccessibilityElement Find(string name) => Elements().Single(e => e.Node.Name == name);
        UiAccessibilityResult Send(UiAccessibilityElement e, UiAccessibilityAction a) => tree.RequestAction(e.Node.Id, e.RuntimeId, a, wait: false);
        Frame();
        var root = Find("People");
        var images = Elements().Where(e => e.Node.Role == UiAccessibilityRole.Image).ToArray();
        check(root.Node.Role == UiAccessibilityRole.AvatarGroup && !root.Node.Focusable
            && images.Length == 4 && images.All(e => e.Node.ParentId == root.Node.Id && !e.Node.Focusable),
            "AvatarGroup exposes one passive named group with four independent named images");
        check(images.Select(e => e.Node.Name).SequenceEqual(new[] { "Person 2", "Person 3", "Person 4", "Person 5" }),
            "AvatarGroup partitions from the beginning and retains the source order of inline members");
        var more = Find("More people (2)");
        check(more.Node.Bounds.Width == 32 && root.Node.Bounds.Width == 224 && more.Node.Expansion == UiAccessibilityExpansionState.Collapsed,
            "AvatarGroup reserves the fifth source slot for a focusable exact overflow count");
        var id = more.Node.Id;
        Send(more, UiAccessibilityAction.Expand);
        Frame();
        check(group.IsOverflowOpen && Find("Person 0").Node.Role == UiAccessibilityRole.MenuItem
            && Find("Person 1").Node.Role == UiAccessibilityRole.MenuItem,
            "AvatarGroup opens the real shared popup with every hidden member");
        Send(Find("Person 1"), UiAccessibilityAction.Invoke);
        Frame();
        check(calls.SequenceEqual(new[] { "value1" }) && !group.IsOverflowOpen,
            "AvatarGroup hidden member invokes its stable value once and dismisses");
        Frame();
        Send(Find("More people (2)"), UiAccessibilityAction.Expand);
        Frame();
        Frame(idle with
        {
            KeyEvents = [new(UiKey.Escape, true, default)]
        });
        Frame();
        check(!group.IsOverflowOpen && Find("More people (2)").Node.Focused,
            "AvatarGroup Escape dismisses and restores trigger focus");
        group.Options = group.Options with
        {
            Layout = UiAvatarGroupLayout.Stack
        };
        Frame();
        images = Elements().Where(e => e.Node.Role == UiAccessibilityRole.Image).ToArray();
        check(images[1].Node.Bounds.X < images[0].Node.Bounds.X + 32 && Find("More people (2)").Node.Id == id,
            "AvatarGroup stack overlaps source avatars while preserving the trigger identity");
        group.Options = group.Options with
        {
            RightToLeft = true
        };
        Frame();
        images = Elements().Where(e => e.Node.Role == UiAccessibilityRole.Image).ToArray();
        check(images[0].Node.Bounds.X > images[1].Node.Bounds.X, "AvatarGroup RTL reverses visual progression without reversing semantic order");
        group.Options = group.Options with
        {
            Layout = UiAvatarGroupLayout.Pie
        };
        Frame();
        check(Find("People").Node.Bounds.Width == 32 && Elements().Count(e => e.Node.Role == UiAccessibilityRole.Image) == 3,
            "AvatarGroup pie uses three source members in one circle");
        var circle = Find("More people (6)").Node.Bounds;
        var corner = new UiVector2(circle.X + .1f, circle.Y + .1f);
        Frame(idle with
        {
            MousePosition = corner,
            LeftMouseDown = true,
            LeftMousePressed = true
        });
        Frame(idle with
        {
            MousePosition = corner,
            LeftMouseReleased = true
        });
        check(!group.IsOverflowOpen, "AvatarGroup circular trigger excludes clipped corner pointer input");
        Send(Find("More people (6)"), UiAccessibilityAction.Expand);
        Frame();
        check(Elements().Count(e => e.Node.Role == UiAccessibilityRole.MenuItem) == 6,
            "AvatarGroup pie popup includes the entire group including visible members");
        Send(Find("Person 4"), UiAccessibilityAction.Invoke);
        Frame();
        Frame();
        check(calls.SequenceEqual(new[] { "value1", "value4" }), "AvatarGroup pie popup uses real member values");
        disabled = true;
        Frame();
        check(Send(Find("More people (6)"), UiAccessibilityAction.Expand) == UiAccessibilityResult.Disabled && !group.IsOverflowOpen,
            "AvatarGroup parent disabled blocks native expansion");
        disabled = false;
        group.Options = group.Options with
        {
            MaximumInlineItems = 1,
            Layout = UiAvatarGroupLayout.Spread
        };
        Frame();
        check(!Elements().Any(e => e.Node.Role == UiAccessibilityRole.Image) && Find("More people (6)").Node.Bounds.Width == 32,
            "AvatarGroup one-slot partition exposes only the complete overflow trigger");
        group.AccessibleName = "Accessible people";
        Frame();
        check(Find("Accessible people").Node.Role == UiAccessibilityRole.AvatarGroup
            && Find("More people (6)").Node.Role == UiAccessibilityRole.Button,
            "AvatarGroup accessible root name does not replace its independently named overflow trigger");
        check(context.State.NextScheduledFrameTimeSeconds is null, "AvatarGroup idle rendering schedules no periodic frames");
        using var firstGroup = Fluent.AvatarGroup("First group", items,
            new(OverflowLabel: "First more"), identity: UiWidgetIdentity.Parent);
        using var secondGroup = Fluent.AvatarGroup("Second group", items,
            new(OverflowLabel: "Second more"), identity: UiWidgetIdentity.Parent);
        renderOverride = ui => { firstGroup.Render(ui); secondGroup.Render(ui); };
        Frame();
        var triggers = Elements().Where(e => e.Node.Name is "First more (2)" or "Second more (2)").ToArray();
        check(triggers.Length == 2 && triggers[0].Node.Id != triggers[1].Node.Id,
            "AvatarGroup Parent identities isolate overflow triggers for distinct named roots");
        using var firstPath = Fluent.Breadcrumb("First path", [new("home", "Home"), new("here", "Here")], identity: UiWidgetIdentity.Parent);
        using var secondPath = Fluent.Breadcrumb("Second path", [new("home", "Home"), new("here", "Here")], identity: UiWidgetIdentity.Parent);
        renderOverride = ui => { firstPath.Render(ui); secondPath.Render(ui); };
        Frame();
        var homes = Elements().Where(e => e.Node.Name == "Home" && e.Node.Role == UiAccessibilityRole.Button).ToArray();
        check(homes.Length == 2 && homes[0].Node.Id != homes[1].Node.Id,
            "Breadcrumb Parent identities isolate repeated home values under distinct named paths");
        check(Fluent.ComponentNames.Contains("AvatarGroup") && !Fluent.MissingComponentNames.Contains("AvatarGroup"),
            "AvatarGroup connects its official catalog requirement");

        void PointerAt(UiRect bounds, bool down)
        {
            Frame(idle with
            {
                MousePosition = new(bounds.X + bounds.Width * .5f, bounds.Y + bounds.Height * .5f),
                LeftMouseDown = down,
                LeftMousePressed = down,
                LeftMouseReleased = !down
            });
        }

        void VerifyActivation<TBehavior>(string label, bool openOnPress, bool selectOnPress)
            where TBehavior : IUiMenuBehavior
        {
            var values = new List<string>();
            using var custom = new UiAvatarGroup<TBehavior, UiFluentAvatarGroupPresentation>(label, items,
                selected: values.Add);
            renderOverride = custom.Render;
            Frame();
            var trigger = Find("More people (2)");
            PointerAt(trigger.Node.Bounds, true);
            check(custom.IsOverflowOpen == openOnPress, label + " applies trigger pointer-down policy");
            PointerAt(trigger.Node.Bounds, false);
            check(custom.IsOverflowOpen && values.Count == 0, label + " opens without selecting an option");
            var option = Find("Person 1").Node.Bounds;
            PointerAt(option, true);
            check(custom.IsOverflowOpen != selectOnPress && values.Count == (selectOnPress ? 1 : 0),
                label + " applies option pointer-down policy");
            PointerAt(option, false);
            check(!custom.IsOverflowOpen && values.SequenceEqual(new[] { "value1" }),
                label + " selects once and closes after pointer activation");
            Frame();
            Send(Find("More people (2)"), UiAccessibilityAction.Expand);
            Frame();
            Frame(idle with { KeyEvents = [new(UiKey.End, true, default)] });
            Frame(idle with { KeyEvents = [new(UiKey.End, false, default)] });
            Frame(idle with { KeyEvents = [new(UiKey.Enter, true, default)] });
            Frame(idle with { KeyEvents = [new(UiKey.Enter, false, default)] });
            check(!custom.IsOverflowOpen && values.SequenceEqual(new[] { "value1", "value1" }),
                label + " retains menu keyboard selection under its pointer policy");
        }

        VerifyActivation<UiDefaultMenuBehavior>("Default AvatarGroup", false, false);
        VerifyActivation<PressTrigger>("Press trigger AvatarGroup", true, false);
        VerifyActivation<PressOption>("Press option AvatarGroup", false, true);

        renderOverride = group.Render;
        group.Options = new(MaximumInlineItems : 2)
        {
            OverflowMode = UiAvatarGroupOverflowMode.KeepFirstAndAppend,
            AdditionalOverflowCount = 3,
        };
        Frame();
        var firstVisible = Elements().Where(e => e.Node.Role == UiAccessibilityRole.Image).Select(e => e.Node.Name);
        check(firstVisible.SequenceEqual(new[] { "Person 0", "Person 1" }) && Find("More people (7)").Node.Focusable,
            "AvatarGroup keeps the first two members and appends a total of seven hidden people");
        Send(Find("More people (7)"), UiAccessibilityAction.Expand);
        Frame();
        var menuNames = Elements().Where(e => e.Node.Role == UiAccessibilityRole.MenuItem).Select(e => e.Node.Name);
        check(menuNames.SequenceEqual(new[] { "Person 2", "Person 3", "Person 4", "Person 5" }),
            "AvatarGroup overflow lists only hidden members supplied by the caller");

        group.Options = group.Options with { MaximumInlineItems = 6, RightToLeft = true };
        Frame();
        var countOnly = Find("More people (3)");
        check(!group.IsOverflowOpen && countOnly.Node.Role == UiAccessibilityRole.Label && !countOnly.Node.Focusable
            && !Elements().Any(e => e.Node.Role == UiAccessibilityRole.MenuItem)
            && countOnly.Node.Bounds.X < Find("Person 5").Node.Bounds.X,
            "AvatarGroup closes its popup and exposes a passive RTL count when no known members are hidden");

        group.Options = group.Options with { Layout = UiAvatarGroupLayout.Pie };
        Frame();
        check(Find("More people (9)").Node.Role == UiAccessibilityRole.Button
            && Elements().Count(e => e.Node.Role == UiAccessibilityRole.Image) == 3,
            "AvatarGroup pie keeps its full-list trigger and includes the unlisted count");
        using var remoteOnly = Fluent.AvatarGroup("Remote members", [], new() { AdditionalOverflowCount = 3 });
        renderOverride = remoteOnly.Render;
        Frame();
        check(Find("More people (3)").Node.Role == UiAccessibilityRole.Label
            && !Elements().Any(e => e.Node.Role == UiAccessibilityRole.Image),
            "AvatarGroup can display an unlisted count with no locally available members");
        remoteOnly.Options = remoteOnly.Options with { Layout = UiAvatarGroupLayout.Pie };
        Frame();
        check(Find("More people (3)").Node.Bounds.Width == 32,
            "AvatarGroup uses the count badge when an empty pie has no avatars to paint");
    }

    private readonly struct PressTrigger : IUiMenuBehavior
    {
        public static UiButtonActivation TriggerActivation => UiButtonActivation.Press;
        public static UiButtonActivation OptionActivation => UiButtonActivation.Release;
    }

    private readonly struct PressOption : IUiMenuBehavior
    {
        public static UiButtonActivation TriggerActivation => UiButtonActivation.Release;
        public static UiButtonActivation OptionActivation => UiButtonActivation.Press;
    }

    private sealed class Screen(Action<UiImmediateContext> draw) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => draw(ui);
    }
}
