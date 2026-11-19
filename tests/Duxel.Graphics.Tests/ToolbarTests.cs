using Duxel.Core;

internal static class ToolbarTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        var actions = 0;
        var selected = false;
        var parentDisabled = false;
        var a = Fluent.Button("Alpha", () => actions++);
        var b = new ConditionalButton("Disabled", () => actions++) { Disabled = true };
        var c = new ConditionalToggle("Checked", new(() => selected, value => selected = value));
        var d = Fluent.Button("Delta", () => actions++);
        using var toolbar = new ConditionalToolbar("Tools", [a, b, c, d]);
        using var before = Fluent.Button("Before", () => { });
        using var after = Fluent.Button("After", () => { });
        UiWidget current = toolbar;
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(UiInputState? input = null)
        {
            context.NewFrame(new(1f / 60, new(640, 480), new(1, 1)), input ?? idle, new(0, 0, 640, 480), UiTextSettings.Default);
            context.Render(new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding: 0);
                before.Render(ui);
                ui.BeginDisabled(parentDisabled);
                try
                {
                    current.Render(ui);
                }
                finally { ui.EndDisabled(); }
                after.Render(ui);
            }));
            using var data = context.GetDrawData();
        }
        List<UiAccessibilityElement> Nodes()
        {
            var all = new List<UiAccessibilityElement>();
            void Visit(string? id)
            {
                while (id is not null && tree.TryGet(id, out var e))
                {
                    all.Add(e);
                    Visit(e.FirstChild);
                    id = e.Next;
                }
            }
            Visit(tree.Root.First);
            return all;
        }
        UiAccessibilityElement Find(string name) => Nodes().Single(e => e.Node.Name == name);
        void Send(string name, UiAccessibilityAction action)
        {
            var n = Find(name);
            check(tree.RequestAction(n.Node.Id, n.RuntimeId, action, wait: false) == UiAccessibilityResult.Success, "Toolbar native request " + action + " : " + name);
            Frame();
        }
        void Key(UiKey key, KeyModifiers modifiers = default, bool down = true)
        {
            Frame(idle with
            {
                KeyEvents = [new(key, down, modifiers)],
                Modifiers = modifiers
            });
            if (down && key != UiKey.Space)
                Frame(idle with
                {
                    KeyEvents = [new(key, false, modifiers)],
                    Modifiers = modifiers
                });
        }
        void Focus(string name) => check(Find(name).Node.Focused, "Toolbar actual focus : " + name);
        Frame();
        var identity = Find("Tools").RuntimeId;
        check(Find("Tools").Node is { Role: UiAccessibilityRole.Toolbar, Focusable: false, SupportsInvoke: false, ValuePattern: false, Orientation: UiAccessibilityOrientation.Horizontal }, "Toolbar passive named native root");
        check(Find("Alpha").Node.ParentId == Find("Tools").Node.Id && Find("Checked").Node.ParentId == Find("Tools").Node.Id, "Toolbar children retain actual action/Toggle providers under toolbar");
        Send("Before", UiAccessibilityAction.Focus);
        Key(UiKey.Tab);
        Focus("Alpha");
        Key(UiKey.RightArrow);
        Focus("Checked");
        Key(UiKey.Space);
        Key(UiKey.Space, down: false);
        check(selected, "Toolbar Space activates the actual bound toggle after arrow navigation");
        Key(UiKey.End);
        Focus("Delta");
        Key(UiKey.RightArrow);
        Focus("Alpha");
        Key(UiKey.LeftArrow);
        Focus("Delta");
        Key(UiKey.Tab);
        Focus("After");
        Key(UiKey.Tab, KeyModifiers.Shift);
        Focus("Delta");
        Key(UiKey.Home);
        Focus("Alpha");
        Key(UiKey.Enter);
        check(actions == 1, "Toolbar Enter runs the focused real button once");
        var rect = Find("Delta").Node.Bounds;
        var point = new UiVector2(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
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
        check(actions == 2, "Toolbar pointer press-release runs the actual child action");
        toolbar.Options = new(RightToLeft: true);
        Frame();
        check(Find("Alpha").Node.Bounds.X > Find("Delta").Node.Bounds.X, "Toolbar RTL reverses logical child placement");
        Send("Delta", UiAccessibilityAction.Focus);
        Key(UiKey.RightArrow);
        Focus("Checked");
        Key(UiKey.DownArrow);
        Focus("Delta");
        toolbar.Options = new(Vertical: true);
        Frame();
        check(Find("Tools").Node.Orientation == UiAccessibilityOrientation.Vertical && Find("Alpha").Node.Bounds.Y < Find("Delta").Node.Bounds.Y, "Toolbar vertical layout updates native orientation");
        Send("Checked", UiAccessibilityAction.Focus);
        c.Disabled = true;
        Frame();
        Focus("Alpha");
        toolbar.Hidden = 0;
        Frame();
        Focus("Delta");
        check(Find("Tools").RuntimeId == identity, "Toolbar option/visibility transitions preserve provider identity");
        toolbar.Hidden = -1;
        c.Disabled = false;
        Frame();
        foreach (var size in Enum.GetValues<UiToolbarSize>())
        {
            foreach (var vertical in new[] { false, true })
            {
                foreach (var rtl in new[] { false, true })
                {
                    toolbar.Options = new(size, vertical, rtl);
                    Frame();
                    var bounds = Find("Tools").Node.Bounds;
                    var child = Find("Alpha").Node.Bounds;
                    var padding = vertical ? 8 : size == UiToolbarSize.Small ? 4 : size == UiToolbarSize.Large ? 20 : 8;
                    check(bounds.Width > 0 && bounds.Height > 0 && (vertical || (rtl ? bounds.X + bounds.Width - child.X - child.Width : child.X - bounds.X) == padding),
                        $"Toolbar configured layout {size}/{vertical}/{rtl}");
                }
            }
        }
        parentDisabled = true;
        Frame();
        check(!Find("Tools").Node.Enabled && !Find("Alpha").Node.Enabled, "Toolbar parent-disabled scope applies to actual children");
        parentDisabled = false;
        Frame();
        var text = "ab";
        using var editing = Fluent.Toolbar("Editing tools", [Fluent.Button("Command"), Fluent.Input("Query", new UiBinding<string>(() => text, value => text = value), 32, 120)]);
        current = editing;
        Frame();
        Send("Query", UiAccessibilityAction.Focus);
        Key(UiKey.End);
        var afterEnd = context.State.GetCursor(Find("Query").Node.Id);
        Key(UiKey.LeftArrow);
        var afterLeft = context.State.GetCursor(Find("Query").Node.Id);
        Frame(idle with
        {
            CharEvents = [new('X')]
        });
        check(afterEnd == 2 && afterLeft == 1, "Toolbar preserves exact input End/Left caret positions");
        check(text == "aXb" && Find("Query").Node.Focused, "Toolbar preserves text editor caret-arrow ownership : " + text + " / focused " + Find("Query").Node.Focused + $" / cursors {afterEnd},{afterLeft}");
        var standaloneText = "ab";
        using var standalone = Fluent.Input("Standalone", new UiBinding<string>(() => standaloneText, v => standaloneText = v), 32, 120);
        current = standalone;
        Frame();
        Send("Standalone", UiAccessibilityAction.Focus);
        Key(UiKey.End);
        Key(UiKey.LeftArrow);
        Frame(idle with
        {
            CharEvents = [new('X')]
        });
        check(standaloneText == "aXb", "Standalone control same-input caret oracle : " + standaloneText);
        var value = 30f;
        using var range = Fluent.Toolbar("Range tools", [Fluent.Slider("Range", new UiBinding<float>(() => value, v => value = v), 0, 100, 1)]);
        current = range;
        Frame();
        Send("Range", UiAccessibilityAction.Focus);
        Key(UiKey.RightArrow);
        check(value == 31 && Find("Range").Node.Focused, "Toolbar preserves range-control arrow ownership");
        using var empty = Fluent.Toolbar("Empty", []);
        current = empty;
        Frame();
        check(Find("Empty").Node.Role == UiAccessibilityRole.Toolbar && context.State.NextScheduledFrameTimeSeconds is null, "Toolbar empty passive root needs no periodic frames");
        check(Fluent.ComponentNames.Contains("Toolbar") && !Fluent.MissingComponentNames.Contains("Toolbar"), "Toolbar connects official top-level catalog");
    }
    private sealed class ConditionalToolbar(string label, UiWidget[] children) : UiFluentToolbar(label, children)
    {
        internal int Hidden = -1;
        protected override bool ShouldRenderChild(int index) => index != Hidden;
    }
    private sealed class ConditionalButton(string label, Action action) : UiFluentButton(label, action)
    {
        internal bool Disabled;
        protected override void RenderControl(UiImmediateContext ui)
        {
            ui.BeginDisabled(Disabled);
            try
            {
                base.RenderControl(ui);
            }
            finally { ui.EndDisabled(); }
        }
    }
    private sealed class ConditionalToggle(string label, UiBinding<bool> value) : UiFluentToggleButton(label, value)
    {
        internal bool Disabled;
        protected override void RenderControl(UiImmediateContext ui)
        {
            ui.BeginDisabled(Disabled);
            try
            {
                base.RenderControl(ui);
            }
            finally { ui.EndDisabled(); }
        }
    }
    private sealed class Screen(Action<UiImmediateContext> draw) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => draw(ui);
    }
}
