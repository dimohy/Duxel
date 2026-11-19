using Duxel.Core;

internal static class MarkdownCompositionRenderingTests
{
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check,
        Action<UiInputState> setInput, UiContext context, UiFluentColorScheme scheme)
    {
        var ime = new MarkdownLifetimeTests.Ime();
        context.SetImeHandler(ime);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        const string initial = "# 한글 제목\n\n본문";
        var source = new UiState<string>(initial);
        using var editor = Dux.MarkdownEditor("Composition editor", source.Binding, new(Width: 340, Height: 300));
        using var view = Dux.MarkdownView("Composition preview", source.Binding, new(Width: 340, Height: 300));
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Draw(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            var paint = new UiControlContext(ui);
            paint.Frame(new(0, 0, 760, 480), scheme == UiFluentColorScheme.Light ? new(255, 255, 255) : new(32, 32, 32), default, 0, 0);
            paint.RestartInputCaretBlink();
            ui.SetCursorScreenPos(new(12, 24));
            editor.Render(ui);
            ui.SetCursorScreenPos(new(380, 24));
            view.Render(ui);
        }
        void Key(string name, UiKey key, KeyModifiers modifiers = default)
        {
            setInput(idle with
            {
                KeyEvents = [new(key, true, modifiers)],
                Modifiers = modifiers
            });
            capture(name, Draw);
            setInput(idle with
            {
                KeyEvents = [new(key, false, modifiers)]
            });
            capture(name + "-release", Draw);
            setInput(idle);
        }
        setInput(idle);
        capture("warmup", Draw);
        var id = tree.Root.First ?? throw new InvalidOperationException("Missing composition editor.");
        if (!tree.TryGet(id, out var node) || node.Node.Name != "Composition editor")
            throw new InvalidOperationException("Composition editor is not the first accessible input.");
        tree.RequestAction(id, node.RuntimeId, UiAccessibilityAction.Focus, wait: false);
        capture("focus", Draw);
        Key("home", UiKey.Home, KeyModifiers.Ctrl);
        Key("heading-end", UiKey.End);
        var before = capture("before", Draw);
        var parsed = editor.Document.ParseCount;
        ime.Composition = "한";
        var composed = capture("preedit", Draw);
        check(source.Value == initial && editor.Document.ParseCount == parsed
            && ime.Caret.Height == 36 && ime.FontHeight == 28 && !composed.AsSpan().SequenceEqual(before),
            "GPU Markdown renders Korean composition at the heading size without changing committed source or syntax");
        check(composed.AsSpan().SequenceEqual(capture("preedit-replay", Draw)),
            "GPU Markdown composition replay preserves exact pixels");
        ime.Composition = null;
        check(before.AsSpan().SequenceEqual(capture("cancel", Draw)) && source.Value == initial,
            "GPU Markdown cancelling composition restores the original pixels and source exactly");
        ime.Queue(id, "한");
        var committed = capture("commit", Draw);
        check(source.Value == "# 한글 제목한\n\n본문" && editor.Document.ParseCount == parsed + 1
            && view.Document.Source == source.Value && !committed.AsSpan().SequenceEqual(before),
            "GPU Markdown committed composition updates both styled consumers in the same frame");
        check(committed.AsSpan().SequenceEqual(capture("commit-replay", Draw)) && editor.Document.ParseCount == parsed + 1,
            "GPU Markdown committed composition is not replayed as a second edit");
        Key("undo", UiKey.Z, KeyModifiers.Ctrl);
        Key("return-home", UiKey.Home, KeyModifiers.Ctrl);
        Key("return-heading-end", UiKey.End);
        check(source.Value == initial && before.AsSpan().SequenceEqual(capture("undo-restored", Draw)),
            "GPU Markdown undo restores original composition source and styled pixels");
        ime.SetCompositionOwner(null);
        context.SetAccessibilityTree(null);
        setInput(idle);
    }
}
