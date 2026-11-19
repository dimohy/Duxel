using Duxel.Core;

internal static class MarkdownSemanticRenderingTests
{
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check,
        Action<UiInputState> setInput, UiContext context, UiFluentColorScheme scheme)
    {
        const string initial = "# Styles\n\n***Both*** and ~~removed~~\n\n<https://example.test>\n\n- [ ] Pending\n- [x] Done\n\n---\n\nTail";
        var source = new UiState<string>(initial);
        using var view = Dux.MarkdownView("Semantic preview", source.Binding, new(Width: 340, Height: 380));
        using var editor = Dux.MarkdownEditor("Semantic editor", source.Binding, new(Width: 340, Height: 380));
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Draw(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            var paint = new UiControlContext(ui);
            paint.Frame(new(0, 0, 760, 480), scheme == UiFluentColorScheme.Light ? new(255, 255, 255) : new(32, 32, 32), default, 0, 0);
            paint.RestartInputCaretBlink();
            ui.SetCursorScreenPos(new(12, 24));
            view.Render(ui);
            ui.SetCursorScreenPos(new(380, 24));
            editor.Render(ui);
        }
        setInput(idle);
        capture("warmup", Draw);
        var baseline = capture("rendered", Draw);
        var parses = view.Document.ParseCount;
        check(baseline.AsSpan().SequenceEqual(capture("rendered-replay", Draw)) && view.Document.ParseCount == parses,
            "GPU Markdown semantic emphasis, autolinks, tasks and thematic break replay without reparsing");
        source.Value = initial.Replace("---", "", StringComparison.Ordinal);
        var noRule = capture("no-rule", Draw);
        check(!baseline.AsSpan().SequenceEqual(noRule),
            "GPU Markdown thematic break draws visible ink instead of an empty source line");
        source.Value = initial.Replace("[ ] Pending", "[x] Pending", StringComparison.Ordinal);
        var checkedTask = capture("checked-task", Draw);
        check(!baseline.AsSpan().SequenceEqual(checkedTask)
            && checkedTask.AsSpan().SequenceEqual(capture("checked-task-replay", Draw)),
            "GPU Markdown task state changes its actual inline checkbox pixels consistently");
        source.Value = initial;
        check(baseline.AsSpan().SequenceEqual(capture("restored", Draw)),
            "GPU Markdown restoring task source restores exact original semantic pixels");
        setInput(idle);
    }
}
