using Duxel.Core;

internal static class MarkdownRenderingTests
{
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check,
        Action<UiInputState> setInput, UiContext context, UiFluentColorScheme scheme)
    {
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        const string initial = "# title\n\nA **bold** and *italic* paragraph.\n\n## 한글 제목\n\n\u0060code\u0060 and ~~removed~~ text.\n\n- First item\n- 두 번째 항목\n\n> Quoted text\n";
        var text = new UiState<string>(initial);
        using var editor = Dux.MarkdownEditor("GPU Markdown editor", text.Binding, new(Width: 340, Height: 380));
        using var view = Dux.MarkdownView("GPU Markdown view", text.Binding, new(Width: 340, Height: 380));
        using var after = Fluent.Button("After Markdown");
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        var mirror = new UiVisualTextLines();
        var mirrorSource = new UiMarkdownTextSource(new(initial));
        var headingPoint = default(UiVector2);
        var computePoint = true;
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
            ui.SetCursorScreenPos(new(12, 420));
            after.Render(ui);
            if (computePoint)
            {
                mirror.Prepare(ui, initial, 316, 360, 20, 0, styled: mirrorSource);
                headingPoint = new(24 + mirror.X(3, 0), 34 + mirror.LineHeight(0) / 2);
                computePoint = false;
            }
        }
        UiAccessibilityElement Find(string name)
        {
            for (var id = tree.Root.First; id is not null && tree.TryGet(id, out var node); id = node.Next)
                if (node.Node.Name == name)
                    return node;
            throw new InvalidOperationException("Missing GPU Markdown node: " + name);
        }
        void Focus(string name)
        {
            var node = Find(name);
            tree.RequestAction(node.Node.Id, node.RuntimeId, UiAccessibilityAction.Focus, wait: false);
        }
        byte[] Key(string name, UiKey key, KeyModifiers modifiers = default)
        {
            setInput(idle with
            {
                KeyEvents = [new(key, true, modifiers)],
                Modifiers = modifiers
            });
            var pixels = capture(name, Draw);
            setInput(idle with
            {
                KeyEvents = [new(key, false, modifiers)]
            });
            capture(name + "-key-up", Draw);
            setInput(idle);
            return pixels;
        }
        setInput(idle);
        capture("warmup", Draw);
        var baseline = capture("rendered", Draw);
        check(baseline.AsSpan().SequenceEqual(capture("rendered-replay", Draw)),
            "GPU Markdown rendered headings, emphasis, code, lists and Korean replay exact pixels");
        var parses = editor.Document.ParseCount;
        Focus("GPU Markdown editor");
        capture("focus", Draw);
        Key("heading-home", UiKey.Home, KeyModifiers.Ctrl);
        var heading = capture("heading-active", Draw);
        check(heading.AsSpan().SequenceEqual(capture("heading-active-replay", Draw))
            && !baseline.AsSpan().SequenceEqual(heading) && editor.Document.ParseCount == parses,
            "GPU Markdown active heading reveals source styling without reparsing the document");
        Key("heading-end", UiKey.End);
        var beforeEdit = capture("heading-before-edit", Draw);
        setInput(idle with
        {
            CharEvents = [new((uint)'!')]
        });
        var changed = capture("heading-edited", Draw);
        setInput(idle);
        check(text.Value.StartsWith("# title!\n", StringComparison.Ordinal)
            && Find("GPU Markdown view").Node.Value == text.Value && !beforeEdit.AsSpan().SequenceEqual(changed),
            "GPU Markdown typing updates the authoritative source and the actual styled preview");
        check(changed.AsSpan().SequenceEqual(capture("heading-edited-replay", Draw)),
            "GPU Markdown edited source has coherent same-frame pixels");
        Key("undo-heading", UiKey.Z, KeyModifiers.Ctrl);
        check(text.Value == initial, "GPU Markdown undo restores the complete original source");
        Key("undo-return-home", UiKey.Home, KeyModifiers.Ctrl);
        Key("undo-return-end", UiKey.End);
        check(beforeEdit.AsSpan().SequenceEqual(capture("undo-heading-idle", Draw)),
            "GPU Markdown returning the caret after undo restores the original styled pixels exactly");
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
        check(baseline.AsSpan().SequenceEqual(capture("blur-idle", Draw)),
            "GPU Markdown leaving the editor collapses every source marker back to the rendered document");
        var editorId = Find("GPU Markdown editor").Node.Id;
        setInput(idle with
        {
            MousePosition = headingPoint,
            LeftMouseDown = true,
            LeftMousePressed = true
        });
        capture("pointer-press", Draw);
        setInput(idle with
        {
            MousePosition = headingPoint,
            LeftMouseDown = true
        });
        capture("pointer-hold", Draw);
        capture("pointer-hold-again", Draw);
        check(!context.State.GetSelection(editorId).HasSelection && context.State.GetCursor(editorId, 0) == 3,
            "GPU Markdown held click does not select text when source markers reveal");
        setInput(idle with
        {
            MousePosition = headingPoint,
            LeftMouseReleased = true
        });
        capture("pointer-release", Draw);
        check(!context.State.GetSelection(editorId).HasSelection && context.State.GetCursor(editorId, 0) == 3,
            "GPU Markdown click completion keeps the clicked logical character");
        setInput(idle with
        {
            CharEvents = [new((uint)'X')]
        });
        capture("pointer-type", Draw);
        check(text.Value == initial.Insert(3, "X"), "GPU Markdown typing after click inserts without accidental replacement");
        Key("pointer-type-undo", UiKey.Z, KeyModifiers.Ctrl);
        setInput(idle);
        editor.Options = editor.Options with
        {
            Width = 160
        };
        var narrow = capture("narrow", Draw);
        check(narrow.AsSpan().SequenceEqual(capture("narrow-replay", Draw))
            && !baseline.AsSpan().SequenceEqual(narrow), "GPU Markdown source layout wraps consistently at a narrow allocation");
        editor.Options = editor.Options with
        {
            Width = 340
        };
        setInput(idle with
        {
            MousePosition = new(750, 450),
            LeftMouseDown = true,
            LeftMousePressed = true
        });
        capture("code-blur-press", Draw);
        setInput(idle with
        {
            MousePosition = new(750, 450),
            LeftMouseReleased = true
        });
        capture("code-blur-release", Draw);
        setInput(idle);
        const string code = "var greeting = \"안녕하세요\";";
        text.Value = "\u0060" + code + "\u0060";
        var inlineCode = capture("code-one-line-reference", Draw);
        foreach (var ending in new[] { "\n", "\r\n", "" })
        {
            var newline = ending == "\r\n" ? ending : "\n";
            text.Value = "\u0060\u0060\u0060csharp" + newline + code + newline + "\u0060\u0060\u0060" + ending;
            var fenced = capture("code-fenced-" + (ending == "" ? "none" : ending.Length.ToString()), Draw);
            check(fenced.AsSpan().SequenceEqual(inlineCode),
                "GPU Markdown inactive single-line fenced code equals its code-only rendering with no fence glyphs: " + ending.Length);
        }
        var codeSource = text.Value;
        var codeParses = editor.Document.ParseCount;
        Focus("GPU Markdown editor");
        Key("code-edit-home", UiKey.Home, KeyModifiers.Ctrl);
        var activeCode = capture("code-edit-active", Draw);
        check(!activeCode.AsSpan().SequenceEqual(inlineCode) && text.Value == codeSource
            && editor.Document.ParseCount == codeParses && !context.State.GetSelection(editorId).HasSelection,
            "GPU Markdown editing a code block reveals its original fences without editing or reparsing source");
        text.Value = codeSource + "\n\nAfter code";
        Key("code-leave-block", UiKey.End, KeyModifiers.Ctrl);
        var anotherBlock = capture("code-edit-other-block", Draw);
        text.Value = "\u0060" + code + "\u0060\n\nAfter code";
        Key("code-reference-other-block", UiKey.End, KeyModifiers.Ctrl);
        var otherReference = capture("code-edit-other-block-reference", Draw);
        check(anotherBlock.AsSpan().SequenceEqual(otherReference),
            "GPU Markdown moving to another editable block collapses both code fences to the code-only result");
        context.SetAccessibilityTree(null);
        setInput(idle);
    }
}
