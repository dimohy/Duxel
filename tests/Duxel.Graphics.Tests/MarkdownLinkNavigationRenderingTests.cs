using Duxel.Core;

internal static class MarkdownLinkNavigationRenderingTests
{
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check,
        Action<UiInputState> setInput, UiContext context, UiFluentColorScheme scheme)
    {
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        try
        {
            var source = new UiState<string>("[First link](https://example.test/first)\n\n[Wrapped link with enough text for several lines](https://example.test/wrapped)\n\n"
                + string.Concat(Enumerable.Repeat("Body text\n\n", 20)) + "[Last link](https://example.test/last)");
            using var view = Dux.MarkdownView("Keyboard preview", source.Binding, new(Width: 220, Height: 180));
            var destinations = new List<Uri>();
            view.Navigate = destinations.Add;
            var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
            void Draw(UiImmediateContext ui)
            {
                ui.EnableRootViewportContentLayout(contentPadding: 0);
                var paint = new UiControlContext(ui);
                paint.Frame(new(0, 0, 760, 480), scheme == UiFluentColorScheme.Light ? new(255, 255, 255) : new(32, 32, 32), default, 0, 0);
                paint.RestartInputCaretBlink();
                ui.SetCursorScreenPos(new(20, 20));
                view.Render(ui);
            }
            byte[] Frame(string name, UiInputState? input = null)
            {
                setInput(input ?? idle);
                return capture("link-navigation-" + name, Draw);
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
                return result ?? throw new InvalidOperationException("GPU Markdown link missing: " + name);
            }
            void Request(string name, UiAccessibilityAction action)
            {
                var node = Find(name);
                if (tree.RequestAction(node.Node.Id, node.RuntimeId, action, wait: false) != UiAccessibilityResult.Success)
                    throw new InvalidOperationException("GPU Markdown link action failed: " + name + "/" + action);
            }
            void Key(string name, UiKey key, KeyModifiers modifiers = default)
            {
                Frame(name + "-down", idle with
                {
                    KeyEvents = [new(key, true, modifiers)],
                    Modifiers = modifiers
                });
                Frame(name + "-up", idle with
                {
                    KeyEvents = [new(key, false, modifiers)],
                    Modifiers = modifiers
                });
            }
            var baseline = Frame("baseline");
            Request("First link", UiAccessibilityAction.Focus);
            var focused = Frame("first-focus");
            var replay = Frame("first-replay");
            check(Find("First link").Node.Focused && !focused.AsSpan().SequenceEqual(baseline),
                "GPU Markdown keyboard focus paints a visible link outline");
            check(focused.AsSpan().SequenceEqual(replay), "GPU Markdown unchanged focused link replays exact pixels");
            Key("wrapped", UiKey.Tab);
            var wrapped = Find("Wrapped link with enough text for several lines");
            check(wrapped.Node.Focused && wrapped.Node.Bounds.Height > Find("First link").Node.Bounds.Height,
                "GPU Markdown wrapped label occupies several lines but remains one Tab stop");
            var wrappedPixels = Frame("wrapped-focus");
            check(!wrappedPixels.AsSpan().SequenceEqual(focused)
                && wrappedPixels.AsSpan().SequenceEqual(Frame("wrapped-replay")),
                "GPU Markdown wrapped focus changes the visible outline and replays exact pixels");
            Key("last", UiKey.Tab);
            check(Find("Last link").Node.Focused && !Find("Last link").Node.Offscreen,
                "GPU Markdown Tab reveals the offscreen final link in the same frame");
            Key("activate-last", UiKey.Enter);
            check(destinations.Count == 1 && destinations[0].AbsolutePath == "/last",
                "GPU Markdown Enter activates the actual keyboard-focused target once");
            Key("previous", UiKey.Tab, KeyModifiers.Shift);
            check(Find("Wrapped link with enough text for several lines").Node.Focused
                && !Find("Wrapped link with enough text for several lines").Node.Offscreen,
                "GPU Markdown reverse Tab reveals the preceding wrapped link");
            Request("First link", UiAccessibilityAction.ScrollIntoView);
            Frame("reveal-first");
            Frame("reveal-first-committed");
            var point = Find("First link").Node.ClickablePoint ?? throw new InvalidOperationException("GPU Markdown link has no visible fragment");
            context.State.AdvanceTime(.6f);
            Frame("drag-down", idle with
            {
                MousePosition = point,
                LeftMousePressed = true,
                LeftMouseDown = true
            });
            Frame("drag-move", idle with
            {
                MousePosition = new(point.X + 24, point.Y),
                LeftMouseDown = true
            });
            Frame("drag-up", idle with
            {
                MousePosition = new(point.X + 24, point.Y),
                LeftMouseReleased = true
            });
            var root = Find("Keyboard preview");
            check(destinations.Count == 1 && context.State.GetSelection(root.Node.Id).HasSelection,
                "GPU Markdown link text keeps real drag selection without navigating");
            check(view.Document.ParseCount == 1, "GPU Markdown link focus, scrolling and activation reuse the parsed document");
        }
        finally
        {
            context.SetAccessibilityTree(null);
        }
    }
}
