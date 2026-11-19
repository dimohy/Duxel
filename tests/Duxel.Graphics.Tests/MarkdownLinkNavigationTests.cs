using Duxel.Core;

internal static class MarkdownLinkNavigationTests
{
    internal static void Run(Action<bool, string> check)
    {
        RunFlowLayoutPointer(check);
        RunNestedScroll(check);
        using var context = CreateContext();
        using var other = CreateContext();
        using var tree = new UiAccessibilityTree();
        using var otherTree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        var invoked = new List<UiAccessibilityEvent>();
        var committedEvents = true;
        tree.AutomationEvent += notification =>
        {
            if (notification.Kind != UiAccessibilityEventKind.Invoked)
                return;
            invoked.Add(notification);
            committedEvents &= tree.TryGet(notification.Element.Node.Id, out var current) && current == notification.Element;
        };
        other.SetAccessibilityTree(otherTree);
        var text = new UiState<string>("[한글 **첫 링크**](https://example.test/a)\n\n[여러 줄로 이어지는 긴 링크 이름입니다](https://example.test/wrapped)\n\n"
            + string.Concat(Enumerable.Repeat("본문\n\n", 20)) + "[마지막](last)");
        using var view = Dux.MarkdownView("Navigation document", text.Binding, new(Width: 220, Height: 160, BaseUri: new("https://example.test/root/")));
        var destinations = new List<Uri>();
        view.Navigate = destinations.Add;
        using var before = Fluent.Button("Before Markdown");
        using var after = Fluent.Button("After Markdown");
        var shown = true;
        var disabled = false;
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(UiContext target, UiInputState? input = null)
        {
            target.NewFrame(new(1f / 60, new(640, 400), new(1, 1)), input ?? idle, new(0, 0, 640, 400), UiTextSettings.Default);
            target.Render(new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding: 0);
                ui.SetCursorScreenPos(new(10, 10));
                before.Render(ui);
                ui.SetCursorScreenPos(new(10, 60));
                if (shown)
                {
                    ui.BeginDisabled(disabled);
                    try
                    {
                        view.Render(ui);
                    }
                    finally { ui.EndDisabled(); }
                }
                ui.SetCursorScreenPos(new(10, 280));
                after.Render(ui);
            }));
            using var draw = target.GetDrawData();
        }
        UiAccessibilityElement Find(UiAccessibilityTree source, string name)
        {
            UiAccessibilityElement? found = null;
            void Visit(string? id)
            {
                while (id is not null && source.TryGet(id, out var element))
                {
                    if (element.Node.Name == name)
                        found ??= element;
                    Visit(element.FirstChild);
                    id = element.Next;
                }
            }
            Visit(source.Root.First);
            return found ?? throw new InvalidOperationException("Missing Markdown navigation node: " + name);
        }
        void Request(string name, UiAccessibilityAction action)
        {
            var node = Find(tree, name);
            check(tree.RequestAction(node.Node.Id, node.RuntimeId, action, wait: false) == UiAccessibilityResult.Success,
                "Markdown queues semantic action: " + name + "/" + action);
            Frame(context);
        }
        void Key(UiKey key, KeyModifiers modifiers = default)
        {
            Frame(context, idle with
            {
                KeyEvents = [new(key, true, modifiers)],
                Modifiers = modifiers
            });
            Frame(context, idle with
            {
                KeyEvents = [new(key, false, modifiers)],
                Modifiers = modifiers
            });
        }
        Frame(context);
        var root = Find(tree, "Navigation document");
        var first = Find(tree, "한글 첫 링크");
        var last = Find(tree, "마지막");
        check(first.Node.Role == UiAccessibilityRole.Link && first.Node.ParentId == root.Node.Id && first.Node.SupportsInvoke
            && first.Node is { ReadOnly: true, Value: "https://example.test/a", SupportsValue: true },
            "Markdown publishes a semantic child Link with its plain label, read-only URL and Invoke");
        check(last.Node.Offscreen && last.Node.CanScrollIntoView && root.Node.Scroll?.VerticallyScrollable == true,
            "Offscreen Markdown links participate in the existing ScrollItem and document Scroll contracts");
        check(first.Node.ClickablePoint is { } point && point.X >= first.Node.Bounds.X
            && point.X < first.Node.Bounds.X + first.Node.Bounds.Width,
            "Markdown publishes a clickable point inside a real rendered link fragment");
        Request("Before Markdown", UiAccessibilityAction.Focus);
        Key(UiKey.Tab);
        check(Find(tree, "Navigation document").Node.Focused, "Tab enters the read-only Markdown document before its links");
        Key(UiKey.Tab);
        check(Find(tree, "한글 첫 링크").Node.Focused, "Tab enters the first Markdown link");
        Key(UiKey.Enter);
        check(destinations.Count == 1 && destinations[^1].AbsolutePath == "/a", "Markdown link Enter navigates once");
        Key(UiKey.Space);
        check(destinations.Count == 1, "Markdown link Space does not activate navigation");
        Key(UiKey.Tab);
        check(Find(tree, "여러 줄로 이어지는 긴 링크 이름입니다").Node.Focused,
            "Wrapped link fragments share one keyboard entry and accessible identity");
        Key(UiKey.Tab);
        last = Find(tree, "마지막");
        check(last.Node.Focused && !last.Node.Offscreen && context.State.GetScrollY(root.Node.Id) > 0,
            "Tab reveals a previously offscreen Markdown link in its viewport");
        Key(UiKey.Enter);
        check(destinations.Count == 2 && destinations[^1].AbsoluteUri == "https://example.test/root/last",
            "Keyboard navigation resolves the offscreen link against the document base URI");
        Key(UiKey.Tab);
        check(Find(tree, "After Markdown").Node.Focused, "Tab leaves Markdown after its final link");
        Key(UiKey.Tab, KeyModifiers.Shift);
        check(Find(tree, "마지막").Node.Focused, "Shift+Tab re-enters the document's final link");
        Request("한글 첫 링크", UiAccessibilityAction.ScrollIntoView);
        Frame(context);
        Frame(context);
        check(!Find(tree, "한글 첫 링크").Node.Offscreen, "ScrollItem reveals a retained offscreen Markdown link");
        Request("한글 첫 링크", UiAccessibilityAction.Focus);
        Request("한글 첫 링크", UiAccessibilityAction.Invoke);
        check(destinations.Count == 3, "Markdown UIA Invoke uses the same navigation callback");
        first = Find(tree, "한글 첫 링크");
        tree.RequestAction(first.Node.Id, first.RuntimeId, UiAccessibilityAction.Invoke, wait: false);
        Frame(context, idle with
        {
            KeyEvents = [new(UiKey.Enter, true, default)]
        });
        Frame(context, idle with
        {
            KeyEvents = [new(UiKey.Enter, false, default)]
        });
        check(destinations.Count == 4, "Simultaneous keyboard and UIA activation navigate the same link once");
        check(invoked.Count == 4 && committedEvents && invoked.All(item => item.Element.Node.Role == UiAccessibilityRole.Link),
            "Markdown sends one committed Invoked event per actual navigation without holding publication locks");
        Frame(context);
        check(invoked.Count == 4, "Unchanged Markdown frames do not repeat Invoked events");
        disabled = true;
        Frame(context);
        first = Find(tree, "한글 첫 링크");
        check(!first.Node.Enabled && !first.Node.Focused
            && tree.RequestAction(first.Node.Id, first.RuntimeId, UiAccessibilityAction.Invoke, wait: false) == UiAccessibilityResult.Disabled,
            "Disabled Markdown links retire focus and reject native invocation");
        disabled = false;
        Frame(context);
        Request("한글 첫 링크", UiAccessibilityAction.Focus);
        first = Find(tree, "한글 첫 링크");
        text.Value = text.Value.Replace("example.test/a", "example.test/changed", StringComparison.Ordinal);
        Frame(context);
        check(tree.RequestAction(first.Node.Id, first.RuntimeId, UiAccessibilityAction.Invoke, wait: false) == UiAccessibilityResult.Unavailable
            && destinations.Count == 4, "Source revisions retire stale Markdown link providers and their targets");
        Request("한글 첫 링크", UiAccessibilityAction.Focus);
        var previousId = Find(tree, "한글 첫 링크").Node.Id;
        Frame(other);
        check(context.State.FocusedId != previousId && !Find(otherTree, "한글 첫 링크").Node.Focused,
            "Markdown link context transfer releases old focus without importing it");
        var transferred = Find(otherTree, "한글 첫 링크");
        otherTree.RequestAction(transferred.Node.Id, transferred.RuntimeId, UiAccessibilityAction.Focus, wait: false);
        Frame(other);
        view.Dispose();
        check(other.State.FocusedId != transferred.Node.Id, "Markdown disposal releases focused link ownership");
        shown = false;
        Frame(other);
        check(otherTree.RequestAction(transferred.Node.Id, transferred.RuntimeId, UiAccessibilityAction.Invoke, wait: false) == UiAccessibilityResult.Unavailable,
            "Disposed Markdown link descendants retire from accessibility publication");
    }
    private static void RunFlowLayoutPointer(Action<bool, string> check)
    {
        using var context = CreateContext();
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        using var view = Dux.MarkdownView("Flow viewer", new UiState<string>(
            "[First](https://example.test/first)\n\n[Wrapped link with enough text to occupy several visual lines](https://example.test/wrapped)\n\n"
            + string.Concat(Enumerable.Repeat("Body text\n\n", 20))).Binding, new(Width: 240, Height: 180));
        var count = 0;
        view.Navigate = _ => count++;
        using var controls = Dux.Column(new(Spacing: 8, MainAxisSize: UiMainAxisSize.Min),
            Dux.Wrap(new(Spacing: 8), Fluent.Button("Disable"), Fluent.Button("Hide"), Fluent.Button("Finish")),
            Fluent.Input("Count", new UiState<string>("0").Binding, 100, 240));
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(UiInputState? input = null)
        {
            context.NewFrame(new(1f / 60, new(640, 480), new(1, 1)), input ?? idle, new(0, 0, 640, 480), UiTextSettings.Default);
            context.Render(new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding: 0);
                controls.Render(ui);
                view.Render(ui);
            }));
            using var draw = context.GetDrawData();
        }
        UiAccessibilityElement Find(string name)
        {
            UiAccessibilityElement? result = null;
            void Visit(string? id)
            {
                while (id is not null && tree.TryGet(id, out var element))
                {
                    if (element.Node.Name == name)
                        result = element;
                    Visit(element.FirstChild);
                    id = element.Next;
                }
            }
            Visit(tree.Root.First);
            return result ?? throw new InvalidOperationException("Flow Markdown element missing: " + name);
        }
        Frame();
        var first = Find("First");
        check(tree.RequestAction(first.Node.Id, first.RuntimeId, UiAccessibilityAction.Invoke, wait: false) == UiAccessibilityResult.Success,
            "Flow Markdown accepts first semantic invocation");
        Frame();
        Frame();
        var wrapped = Find("Wrapped link with enough text to occupy several visual lines");
        var point = wrapped.Node.ClickablePoint ?? throw new InvalidOperationException("Flow Markdown clickable point missing");
        Frame(idle with
        {
            MousePosition = point,
            LeftMouseDown = true,
            LeftMousePressed = true
        });
        for (var i = 0; i < 8; i++)
            Frame(idle with
            {
                MousePosition = point,
                LeftMouseDown = true
            });
        check(count == 1, "Flow Markdown held wrapped link waits for release");
        Frame(idle with
        {
            MousePosition = point,
            LeftMouseReleased = true
        });
        check(count == 2, "Flow Markdown published clickable point navigates its wrapped fragment after release");
        Frame();
        Frame(idle with
        {
            MousePosition = point,
            LeftMouseDown = true,
            LeftMousePressed = true
        });
        Frame(idle with
        {
            MousePosition = new(point.X + 12, point.Y),
            LeftMouseDown = true
        });
        Frame(idle with
        {
            MousePosition = point,
            LeftMouseReleased = true
        });
        check(count == 2, "Flow Markdown drag selection cancels link navigation even after returning to its origin");
    }

    private static void RunNestedScroll(Action<bool, string> check)
    {
        foreach (var accessible in new[] { false, true })
            foreach (var twoLevels in new[] { false, true })
            {
                using var context = CreateContext();
                using var tree = new UiAccessibilityTree();
                context.SetAccessibilityTree(accessible ? tree : null);
                using var view = Dux.MarkdownView("Nested document", new UiState<string>(
                    "[First](https://example.test/first)\n\n" + string.Concat(Enumerable.Repeat("Body text\n\n", 30))
                    + "[Last](https://example.test/last)").Binding, new(Height: 340));
                using var inner = twoLevels ? Dux.ScrollView(view, new(Width: 260, Height: 220)) : null;
                using var outer = Dux.ScrollView((UiWidget?)inner ?? view, new(Width: 280, Height: 120));
                using var sibling = Dux.ScrollView(Fluent.Body1(string.Concat(Enumerable.Repeat("Sibling\n", 30))), new(Width: 160, Height: 100));
                sibling.Offset = new(0, 50);
                var destinations = new List<Uri>();
                view.Navigate = destinations.Add;
                var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
                void Frame(UiInputState? input = null)
                {
                    context.NewFrame(new(1f / 60, new(640, 480), new(1, 1)), input ?? idle, new(0, 0, 640, 480), UiTextSettings.Default);
                    context.Render(new Screen(ui =>
                    {
                        ui.EnableRootViewportContentLayout(contentPadding: 0);
                        ui.SetCursorScreenPos(new(10, 10));
                        outer.Render(ui);
                        ui.SetCursorScreenPos(new(350, 10));
                        sibling.Render(ui);
                    }));
                    using var draw = context.GetDrawData();
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
                        KeyEvents = [new(key, false, modifiers)],
                        Modifiers = modifiers
                    });
                }
                Frame();
                Key(UiKey.Tab);
                Key(UiKey.Tab);
                Key(UiKey.Tab);
                Frame();
                check(outer.Offset.Y > 0 && (!twoLevels || inner!.Offset.Y > 0),
                    "Markdown Tab reveals its final link through ancestor ScrollViews: accessibility=" + accessible + "/twoLevels=" + twoLevels);
                Key(UiKey.Enter);
                check(destinations.Count == 1 && destinations[0].AbsolutePath == "/last",
                    "Markdown nested-scroll reveal retains the focused keyboard target: accessibility=" + accessible);
                Key(UiKey.Tab, KeyModifiers.Shift);
                Frame();
                check(outer.Offset.Y + (inner?.Offset.Y ?? 0) <= 10,
                    "Markdown reverse Tab fully reveals its first link including the 10px document inset: accessibility=" + accessible + "/twoLevels=" + twoLevels);
                check(sibling.Offset.Y == 50, "Markdown focus reveal leaves a following sibling scroll viewport unchanged");
                outer.Offset = new(0, 100);
                Frame();
                Frame();
                check(outer.Offset.Y == 100, "Markdown unchanged focus preserves manual ancestor scrolling: accessibility=" + accessible);
            }
    }

    private static UiContext CreateContext()
    {
        var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        return context;
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
