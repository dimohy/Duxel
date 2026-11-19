using Duxel.Core;

internal static class PopupInputModeTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        using var before = Fluent.Button("Before popup");
        using var after = Fluent.Button("After popup");
        var behindCount = 0;
        var insideCount = 0;
        using var behind = Fluent.Button("Behind popup", () => behindCount++);
        using var first = Fluent.Button("Popup first", () => insideCount++);
        using var last = Fluent.Button("Popup last");
        var mode = UiPopupInputMode.NonModal;
        var requestOpen = true;
        var requestClose = false;
        var shown = false;
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(UiInputState? input = null)
        {
            context.NewFrame(new(1f / 60, new(640, 480), new(1, 1)), input ?? idle, new(0, 0, 640, 480), UiTextSettings.Default);
            context.Render(new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding: 0);
                before.Render(ui);
                ui.SetCursorScreenPos(new(300, 120));
                behind.Render(ui);
                if (requestOpen)
                {
                    ui.OpenPopup("input popup");
                    requestOpen = false;
                }
                ui.SetNextPopupPosition(new(280, 100));
                shown = ui.BeginPopup("input popup", mode);
                if (shown)
                {
                    try
                    {
                        if (requestClose)
                        {
                            ui.CloseCurrentPopup();
                            ui.CloseCurrentPopup();
                            requestClose = false;
                        }
                        first.Render(ui);
                        last.Render(ui);
                    }
                    finally { ui.EndPopup(); }
                }
                ui.SetCursorScreenPos(new(0, 64));
                after.Render(ui);
            }));
            using var data = context.GetDrawData();
        }
        UiAccessibilityElement Find(string name)
        {
            UiAccessibilityElement? result = null;
            void Visit(string? id)
            {
                while (id is not null && tree.TryGet(id, out var e))
                {
                    if (e.Node.Name == name)
                        result = e;
                    Visit(e.FirstChild);
                    id = e.Next;
                }
            }
            Visit(tree.Root.First);
            return result ?? throw new InvalidOperationException("Missing popup fixture : " + name);
        }
        void Focus(string name)
        {
            var n = Find(name);
            check(tree.RequestAction(n.Node.Id, n.RuntimeId, UiAccessibilityAction.Focus, wait: false) == UiAccessibilityResult.Success, "Popup accepts native focus : " + name);
            Frame();
        }
        void Key(UiKey key, KeyModifiers modifiers = default)
        {
            Frame(idle with
            {
                KeyEvents = [new(key, true, modifiers)],
                Modifiers = modifiers
            });
            Frame(idle with
            {
                KeyEvents = [new(key, false, modifiers)]
            });
        }
        Frame();
        Frame();
        Focus("Popup last");
        Key(UiKey.Tab);
        check(Find("After popup").Node.Focused && shown, "Nonmodal popup Tab reaches background without closing the surface");
        Key(UiKey.Tab, KeyModifiers.Shift);
        check(Find("Popup last").Node.Focused, "Nonmodal popup reverse Tab returns to its real child");
        Focus("Before popup");
        check(Find("Before popup").Node.Enabled && Find("Before popup").Node.Focused, "Nonmodal popup preserves native background focus availability");
        var point = new UiVector2(320, 125);
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
        check(insideCount == 1 && behindCount == 0, "Nonmodal popup still occludes covered background pointer input");
        mode = UiPopupInputMode.Exclusive;
        Frame();
        Frame();
        check(!Find("Before popup").Node.Enabled && !Find("After popup").Node.Enabled, "Exclusive popup retains existing native background isolation");
        Focus("Popup last");
        Key(UiKey.Tab);
        check(Find("Popup first").Node.Focused, "Exclusive popup retains isolated circular Tab order");
        mode = UiPopupInputMode.NonModal;
        Frame();
        Frame();
        Focus("Popup last");
        Key(UiKey.Tab);
        check(Find("After popup").Node.Focused, "Changing back to nonmodal restores background Tab membership");
        mode = UiPopupInputMode.Exclusive;
        Frame();
        Frame();
        requestClose = true;
        Frame();
        Frame();
        check(context.State.PreviousPopupBlockingRects.Count == 0 && Find("Before popup").Node.Enabled,
            "Closing an exclusive popup clears next-frame keyboard registration without an extra idle frame");
        check(!shown && Find("After popup").Node.Enabled && context.State.NextScheduledFrameTimeSeconds is null, "Closed popup releases keyboard isolation without periodic frames");
        RunLayeredCard(check);
        RunIndependentRegistrations(check);
    }
    private static void RunLayeredCard(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        var anchor = new PopupAnchor();
        using var card = Fluent.Card("Popup card", anchor, new(Width: 240, FocusMode: UiFocusGroupMode.NoTab));
        using var before = Fluent.Button("Card before");
        using var after = Fluent.Button("Card after");
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(UiInputState? input = null)
        {
            context.NewFrame(new(1f / 60, new(640, 480), new(1, 1)), input ?? idle, new(0, 0, 640, 480), UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding: 0); before.Render(ui); card.Render(ui); after.Render(ui); }));
            using var data = context.GetDrawData();
        }
        UiAccessibilityElement Find(string name)
        {
            UiAccessibilityElement? found = null;
            void Visit(string? id)
            {
                while (id is not null && tree.TryGet(id, out var e))
                {
                    if (e.Node.Name == name)
                        found = e;
                    Visit(e.FirstChild);
                    id = e.Next;
                }
            }
            Visit(tree.Root.First);
            return found ?? throw new InvalidOperationException("Missing layered Card provider : " + name);
        }
        void Focus(string name)
        {
            var e = Find(name);
            check(tree.RequestAction(e.Node.Id, e.RuntimeId, UiAccessibilityAction.Focus, wait: false) == UiAccessibilityResult.Success, "Layered Card accepts native focus : " + name);
            Frame();
        }
        void Key(UiKey key, KeyModifiers modifiers = default)
        {
            Frame(idle with
            {
                KeyEvents = [new(key, true, modifiers)],
                Modifiers = modifiers
            });
            Frame(idle with
            {
                KeyEvents = [new(key, false, modifiers)]
            });
        }
        Frame();
        Frame();
        Focus("Portal action");
        Key(UiKey.Tab);
        check(Find("Card after").Node.Focused, "A portal does not inherit the surrounding Card NoTab trap");
        Key(UiKey.Tab, KeyModifiers.Shift);
        check(Find("Portal action").Node.Focused, "Reverse Tab enters portal independently of surrounding Card group");
        Key(UiKey.Escape);
        check(Find("Portal action").Node.Focused, "Portal Escape is not consumed by a different-layer Card group");
        Focus("Anchor action");
        Key(UiKey.Tab);
        check(Find("Anchor action").Node.Focused, "Card NoTab still traps its own-layer controls and excludes portal stops");
        Focus("Popup card");
        Key(UiKey.Enter);
        check(Find("Anchor action").Node.Focused, "Card Enter still selects an own-layer child");
    }
    private static void RunIndependentRegistrations(Action<bool, string> check)
    {
        foreach (var firstMode in Enum.GetValues<UiPopupInputMode>())
        {
            using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
            ControlFontFixture.Configure(context);
            using var tree = new UiAccessibilityTree();
            context.SetAccessibilityTree(tree);
            using var insideA = Fluent.Button("Owner A child");
            using var insideB = Fluent.Button("Owner B child");
            using var outside = Fluent.Button("Owners outside");
            var open = true;
            var closeA = false;
            var bVisible = false;
            var secondMode = firstMode == UiPopupInputMode.Exclusive ? UiPopupInputMode.NonModal : UiPopupInputMode.Exclusive;
            void Frame()
            {
                context.NewFrame(new(0, new(640, 480), new(1, 1)), new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default), new(0, 0, 640, 480), UiTextSettings.Default);
                context.Render(new Screen(ui =>
                {
                    ui.EnableRootViewportContentLayout(contentPadding: 0);
                    if (open)
                    {
                        ui.OpenPopup("owner A");
                        ui.OpenPopup("owner B");
                        open = false;
                    }
                    ui.SetNextPopupPosition(new(280, 100));
                    if (ui.BeginPopup("owner A", firstMode))
                    {
                        try
                        {
                            if (closeA)
                            {
                                ui.CloseCurrentPopup();
                                ui.CloseCurrentPopup();
                            }
                            else
                                insideA.Render(ui);
                        }
                        finally { ui.EndPopup(); }
                    }
                    ui.SetNextPopupPosition(new(280, 100));
                    bVisible = ui.BeginPopup("owner B", secondMode);
                    if (bVisible)
                    {
                        try
                        {
                            insideB.Render(ui);
                        }
                        finally { ui.EndPopup(); }
                    }
                    outside.Render(ui);
                }));
                using var data = context.GetDrawData();
            }
            UiAccessibilityElement Outside()
            {
                var id = tree.Root.First;
                while (id is not null && tree.TryGet(id, out var e))
                {
                    if (e.Node.Name == "Owners outside")
                        return e;
                    id = e.Next;
                }
                throw new InvalidOperationException("Missing independent-owner outside control");
            }
            Frame();
            Frame();
            closeA = true;
            Frame();
            Frame();
            check(bVisible && Outside().Node.Enabled == (secondMode == UiPopupInputMode.NonModal),
                "Closing one popup preserves the other owner's keyboard mode at identical geometry : " + firstMode);
            check(context.State.PreviousPopupBlockingRects.Count == 1,
                "Closing one popup removes only its current registration at identical geometry : " + firstMode);
        }
    }
    private sealed class PopupAnchor : UiFluentButton
    {
        private readonly UiFluentButton _portal = Fluent.Button("Portal action");
        private bool _opened;
        internal PopupAnchor() : base("Anchor action") => OwnChild("portal", _portal);
        protected override void RenderControl(UiImmediateContext ui)
        {
            base.RenderControl(ui);
            if (!_opened)
            {
                ui.OpenPopup("portal");
                _opened = true;
            }
            ui.SetNextPopupPosition(new(320, 100));
            if (ui.BeginPopup("portal", UiPopupInputMode.NonModal))
            {
                try
                {
                    _portal.Render(ui);
                }
                finally { ui.EndPopup(); }
            }
        }
    }
    private sealed class Screen(Action<UiImmediateContext> draw) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => draw(ui);
    }
}
