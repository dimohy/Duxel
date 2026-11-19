using Duxel.Core;

internal static class MarkdownNestedScrollRenderingTests
{
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check,
        Action<UiInputState> setInput, UiContext context, UiFluentColorScheme scheme)
    {
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        try
        {
            using var view = Dux.MarkdownView("Nested GPU document", new UiState<string>(
                "[Nested first](https://example.test/first)\n\n" + string.Concat(Enumerable.Repeat("Body text\n\n", 30))
                + "[Nested last](https://example.test/last)").Binding, new(Height: 340));
            using var inner = Dux.ScrollView(view, new(Width: 260, Height: 220));
            using var outer = Dux.ScrollView(inner, new(Width: 280, Height: 120));
            var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
            void Draw(UiImmediateContext ui)
            {
                ui.EnableRootViewportContentLayout(contentPadding: 0);
                var paint = new UiControlContext(ui);
                paint.Frame(new(0, 0, 760, 480), scheme == UiFluentColorScheme.Light ? new(255, 255, 255) : new(32, 32, 32), default, 0, 0);
                paint.RestartInputCaretBlink();
                ui.SetCursorScreenPos(new(20, 20));
                outer.Render(ui);
            }
            byte[] Frame(string name, UiInputState? input = null)
            {
                setInput(input ?? idle);
                return capture("nested-link-" + name, Draw);
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
                return result ?? throw new InvalidOperationException("Nested GPU link missing: " + name);
            }
            void Tab(string name, KeyModifiers modifiers = default)
            {
                Frame(name + "-down", idle with
                {
                    KeyEvents = [new(UiKey.Tab, true, modifiers)],
                    Modifiers = modifiers
                });
                Frame(name + "-up", idle with
                {
                    KeyEvents = [new(UiKey.Tab, false, modifiers)],
                    Modifiers = modifiers
                });
            }
            var before = Frame("before");
            Tab("document");
            Tab("first");
            Tab("last");
            var after = Frame("last-settled");
            check(inner.Offset.Y > 0 && outer.Offset.Y > 0 && Find("Nested last").Node is { Focused: true, Offscreen: false },
                "GPU Markdown Tab reveals its real link through two scroll ancestors");
            check(!before.AsSpan().SequenceEqual(after), "GPU Markdown nested reveal paints the newly visible link rather than retaining old pixels");
            Tab("reverse", KeyModifiers.Shift);
            Frame("first-settled");
            check(Find("Nested first").Node is { Focused: true, Offscreen: false },
                "GPU Markdown reverse Tab reveals the first link through both ancestors");
            outer.Offset = new(0, 100);
            Frame("manual-scroll");
            Frame("manual-scroll-idle");
            check(outer.Offset.Y == 100, "GPU Markdown does not undo manual ancestor scrolling while focus stays unchanged");
        }
        finally
        {
            context.SetAccessibilityTree(null);
        }
    }
}
