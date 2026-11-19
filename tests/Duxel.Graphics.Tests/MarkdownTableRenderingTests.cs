using Duxel.Core;

internal static class MarkdownTableRenderingTests
{
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check,
        Action<UiInputState> setInput, UiContext context, UiFluentColorScheme scheme)
    {
        const string initial = "# Table\n\n| Name | Count | State |\n| :--- | ---: | :---: |\n| **한글** | 12 | Ready |\n| Long value | 2 | Done |\n| | | |\n\nAfter";
        var source = new UiState<string>(initial);
        var ime = new MarkdownLifetimeTests.Ime();
        context.SetImeHandler(ime);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        using var view = Dux.MarkdownView("Table preview", source.Binding, new(Width: 340, Height: 380));
        using var editor = Dux.MarkdownEditor("Table editor", source.Binding, new(Width: 340, Height: 380));
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        var mirror = new UiVisualTextLines();
        var mirrorSource = new UiMarkdownTextSource(new(initial));
        var korean = initial.IndexOf("한글", StringComparison.Ordinal) + 1;
        var point = default(UiVector2);
        var computePoint = true;
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
            if (computePoint)
            {
                mirror.Prepare(ui, initial, 316, 360, 20, 0, styled: mirrorSource);
                var row = mirror.FindLine(korean);
                point = new(392 + mirror.X(korean, row) + .1f, 34 + mirror.Y(row) + mirror.LineHeight(row) / 2);
                computePoint = false;
            }
        }
        UiAccessibilityElement Find(string name)
        {
            for (var id = tree.Root.First; id is not null && tree.TryGet(id, out var node); id = node.Next)
                if (node.Node.Name == name)
                    return node;
            throw new InvalidOperationException("Missing table input: " + name);
        }
        setInput(idle);
        capture("warmup", Draw);
        var baseline = capture("rendered", Draw);
        check(baseline.AsSpan().SequenceEqual(capture("rendered-replay", Draw)),
            "GPU Markdown table headers, cell typography, borders and independent column alignment replay exact pixels");
        var editorId = Find("Table editor").Node.Id;
        var parses = editor.Document.ParseCount;
        setInput(idle with
        {
            MousePosition = point,
            LeftMouseDown = true,
            LeftMousePressed = true
        });
        capture("cell-press", Draw);
        setInput(idle with
        {
            MousePosition = point,
            LeftMouseDown = true
        });
        capture("cell-hold", Draw);
        capture("cell-hold-again", Draw);
        check(context.State.GetCursor(editorId, 0) == korean && !context.State.GetSelection(editorId).HasSelection,
            "GPU Markdown held table-cell click preserves its original Korean character without accidental selection");
        setInput(idle with
        {
            MousePosition = point,
            LeftMouseReleased = true
        });
        var raw = capture("cell-release-raw", Draw);
        check(context.State.GetCursor(editorId, 0) == korean && !context.State.GetSelection(editorId).HasSelection
            && editor.Document.ParseCount == parses && !baseline.AsSpan().SequenceEqual(raw),
            "GPU Markdown releasing the cell click reveals raw table source with the same logical caret and no reparse");
        ime.Queue(editorId, "X");
        setInput(idle);
        capture("cell-type", Draw);
        check(source.Value == initial.Insert(korean, "X") && context.State.NextScheduledFrameTimeSeconds == context.State.TimeSeconds,
            "GPU Markdown table cell editing inserts at the clicked source character and schedules an immediate preview refresh");
        capture("cell-type-preview-refresh", Draw);
        check(view.Document.Source == source.Value && context.State.NextScheduledFrameTimeSeconds != context.State.TimeSeconds,
            "GPU Markdown an earlier preview catches up on the requested frame without scheduling unlimited rendering");
        setInput(idle with
        {
            KeyEvents = [new(UiKey.Z, true, KeyModifiers.Ctrl)],
            Modifiers = KeyModifiers.Ctrl
        });
        capture("cell-undo", Draw);
        setInput(idle with
        {
            KeyEvents = [new(UiKey.Z, false, KeyModifiers.Ctrl)]
        });
        capture("cell-undo-release", Draw);
        setInput(idle with
        {
            MousePosition = new(750, 450),
            LeftMouseDown = true,
            LeftMousePressed = true
        });
        capture("blur-press", Draw);
        setInput(idle with
        {
            MousePosition = new(750, 450),
            LeftMouseReleased = true
        });
        capture("blur-release", Draw);
        setInput(idle);
        check(source.Value == initial && baseline.AsSpan().SequenceEqual(capture("restored", Draw)),
            "GPU Markdown undo and leaving the table restore the original structured table pixels");
        view.Options = view.Options with
        {
            Width = 180
        };
        var narrow = capture("narrow", Draw);
        var viewId = Find("Table preview").Node.Id;
        setInput(idle with
        {
            MousePosition = new(60, 150),
            MouseWheelHorizontal = -3
        });
        var scrolled = capture("narrow-horizontal-scroll", Draw);
        check(context.State.GetScrollX(viewId) > 0 && !narrow.AsSpan().SequenceEqual(scrolled),
            "GPU Markdown wide table supports actual horizontal scrolling while paragraph wrapping remains enabled");
        setInput(idle);
        context.State.AdvanceTime(2);
        capture("narrow-settle", Draw);
        var settled = capture("narrow-rest", Draw);
        check(settled.AsSpan().SequenceEqual(capture("narrow-rest-replay", Draw)),
            "GPU Markdown scrolled table settles to coherent cached pixels");
        source.Value = "| Header only |\n| --- |";
        var headerOnly = capture("header-only", Draw);
        check(view.Document.Tables[0].Rows.Length == 1 && headerOnly.AsSpan().SequenceEqual(capture("header-only-replay", Draw)),
            "GPU Markdown header-only table renders its one structured row consistently");
        foreach (var newline in new[] { "\r\n", "\r" })
        {
            source.Value = "| Header only |" + newline + "| --- |";
            var pixels = capture("header-only-newlines-" + newline.Length, Draw);
            check(view.Document.Source.Contains('\r') && headerOnly.AsSpan().SequenceEqual(pixels),
                "GPU Markdown read-only table parses CRLF and CR source with the same cell pixels as LF");
        }
        context.SetAccessibilityTree(null);
        ime.SetCompositionOwner(null);
        setInput(idle);
    }
}
