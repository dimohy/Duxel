using Duxel.Core;

internal static class PopoverFocusLeaveTests
{
    internal static void Run(Action<bool, string> check)
    {
        RunCase(check, false);
        RunCase(check, true);
    }

    private static void RunCase(Action<bool, string> check, bool disableAutoFocus)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        using var before = Fluent.Button("Focus before");
        using var after = Fluent.Button("Focus after");
        var nested = Fluent.Popover("Nested focus", Fluent.Button("Nested child"), options: new(MotionEnabled: false));
        var child = Fluent.Button("Focus child");
        using var owner = Fluent.Popover("Focus owner", Dux.Column(new(MainAxisSize: UiMainAxisSize.Min), child, nested),
            options: new(MotionEnabled: false, DisableAutoFocus: disableAutoFocus)
            {
                CloseOnFocusLeave = true
            });
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        var changes = new List<bool>();
        owner.OpenChanged = changes.Add;
        void Frame(UiInputState? input = null)
        {
            context.NewFrame(new(1f / 60, new(640, 480), new(1, 1)), input ?? idle, new(0, 0, 640, 480), UiTextSettings.Default);
            context.Render(new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding: 0);
                before.Render(ui);
                owner.Render(ui);
                after.Render(ui);
            }));
            using var draw = context.GetDrawData();
        }
        List<UiAccessibilityElement> Nodes()
        {
            var nodes = new List<UiAccessibilityElement>();
            void Visit(string? id)
            {
                while (id is not null && tree.TryGet(id, out var element))
                {
                    nodes.Add(element);
                    Visit(element.FirstChild);
                    id = element.Next;
                }
            }
            Visit(tree.Root.First);
            return nodes;
        }
        UiAccessibilityElement Find(string name) => Nodes().Single(e => e.Node.Name == name && e.Node.Role == UiAccessibilityRole.Button);
        void Focus(string name)
        {
            var element = Find(name);
            var result = tree.RequestAction(element.Node.Id, element.RuntimeId, UiAccessibilityAction.Focus, wait: false);
            if (result != UiAccessibilityResult.Success)
                throw new InvalidOperationException("Focus-leave fixture cannot focus : " + name);
            Frame();
            Frame();
        }
        void Open()
        {
            Focus("Focus owner");
            owner.Show();
            Frame();
        }
        Frame();
        Open();
        check(owner.IsOpen && Find(disableAutoFocus ? "Focus owner" : "Focus child").Node.Focused,
            "Focus-leave Popover honors opening autofocus mode");
        Focus("Focus child");
        Focus("Focus owner");
        check(owner.IsOpen, "Focus-leave Popover retains focus transfers between trigger and content");
        Focus("Nested focus");
        nested.Show();
        Frame();
        Frame();
        check(owner.IsOpen && nested.IsOpen && Find("Nested child").Node.Focused,
            "Focus-leave Popover includes owned descendant surfaces");
        Focus("Focus after");
        check(!owner.IsOpen && !nested.IsOpen && Find("Focus after").Node.Focused,
            "Focus-leave Popover closes descendants and preserves following sibling focus");
        check(changes.SequenceEqual(new[] { true, false }) && Nodes().All(e => e.Node.Name != "Focus child"),
            "Focus-leave dismissal notifies once and retires hidden children");
        Open();
        Focus("Focus before");
        check(!owner.IsOpen && Find("Focus before").Node.Focused,
            "Focus-leave Popover observes a preceding sibling without stealing focus");
        Open();
        Focus("Nested focus");
        Frame(idle with
        {
            KeyEvents = [new(UiKey.Tab, true, default)]
        });
        Frame(idle with
        {
            KeyEvents = [new(UiKey.Tab, false, default)]
        });
        check(!owner.IsOpen && Find("Focus after").Node.Focused,
            "Focus-leave Popover dismisses on real forward Tab navigation");
        var model = false;
        var reject = false;
        owner.Open = new(() => model, value => { if (value || !reject) model = value; });
        Open();
        reject = true;
        Focus("Focus after");
        check(owner.IsOpen && model && Find("Focus after").Node.Focused,
            "Focus-leave dismissal respects rejected model writes without restoring trigger focus");
        reject = false;
        Frame();
        check(owner.IsOpen && context.State.NextScheduledFrameTimeSeconds is null,
            "Rejected focus-leave dismissal does not poll or repeat without a new focus transition");
        Focus("Focus child");
        Focus("Focus after");
        check(!owner.IsOpen && !model, "Reentering focus region rearms accepted focus-leave dismissal");
        owner.Open = null;
        owner.Options = owner.Options with
        {
            CloseOnFocusLeave = false
        };
        Open();
        Focus("Focus after");
        check(owner.IsOpen, "Default Popover behavior remains open across nonmodal focus departure");
    }

    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
