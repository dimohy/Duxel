using Duxel.Core;

internal static class MarkdownWidgetTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        const string initial = "# title\n\nBody **bold**\n\n한글.";
        var text = new UiState<string>(initial);
        using var editor = Dux.MarkdownEditor("Smart Markdown", text.Binding, new(Width: 300, Height: 220));
        using var view = Dux.MarkdownView("Rendered Markdown", text.Binding, new(Width: 300, Height: 220));
        using var following = Fluent.Button("After Markdown");
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(UiInputState? input = null)
        {
            context.NewFrame(new(1f / 60, new(700, 360), new(1, 1)), input ?? idle, new(0, 0, 700, 360), UiTextSettings.Default);
            context.Render(new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding: 0);
                ui.SetCursorScreenPos(new(10, 20));
                editor.Render(ui);
                ui.SetCursorScreenPos(new(350, 20));
                view.Render(ui);
                ui.SetCursorScreenPos(new(10, 280));
                following.Render(ui);
            }));
            using var draw = context.GetDrawData();
        }
        UiAccessibilityElement Find(string name)
        {
            for (var id = tree.Root.First; id is not null && tree.TryGet(id, out var element); id = element.Next)
                if (element.Node.Name == name)
                    return element;
            throw new InvalidOperationException("Missing Markdown node: " + name);
        }
        void Focus(string name)
        {
            var node = Find(name);
            tree.RequestAction(node.Node.Id, node.RuntimeId, UiAccessibilityAction.Focus, wait: false);
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
        void Type(string value) => Frame(idle with
        {
            CharEvents = value.EnumerateRunes().Select(rune => new UiCharEvent((uint)rune.Value)).ToArray()
        });
        Frame();
        check(Find("Smart Markdown").Node is { Multiline: true, ReadOnly: false, Value: initial }
            && Find("Rendered Markdown").Node is { Multiline: true, ReadOnly: true, Value: initial },
            "Markdown factories expose the same source with editable and read-only native semantics");
        var parsed = editor.Document.ParseCount;
        Frame();
        Frame();
        check(editor.Document.ParseCount == parsed && view.Document.ParseCount == parsed,
            "Markdown idle frames reuse parsed documents");
        Focus("Smart Markdown");
        Key(UiKey.Home, KeyModifiers.Ctrl);
        Key(UiKey.End);
        Type("!");
        check(context.State.NextScheduledFrameTimeSeconds == context.State.TimeSeconds,
            "Markdown source edits schedule one immediate follow-up for consumers painted earlier in event-wait mode");
        check(text.Value == initial.Replace("title", "title!", StringComparison.Ordinal)
            && editor.Document.Source == text.Value && view.Document.Source == text.Value,
            "Markdown heading editing updates source and both styled consumers in one frame");
        Key(UiKey.Z, KeyModifiers.Ctrl);
        check(text.Value == initial, "Markdown undo restores source delimiters and the complete document");
        Key(UiKey.A, KeyModifiers.Ctrl);
        Type("## 새 제목");
        check(text.Value == "## 새 제목", "Markdown replacement selection spans styled blocks in one source buffer");
        Key(UiKey.Z, KeyModifiers.Ctrl);
        check(text.Value == initial, "Markdown cross-block replacement remains one undoable document edit");
        Focus("Rendered Markdown");
        Type("blocked");
        check(text.Value == initial, "MarkdownView cannot mutate its input document through typing");
        text.Value = "# 외부 수정\n\n새 문단";
        Frame();
        check(editor.Document.Source == text.Value && view.Document.Source == text.Value
            && Find("Smart Markdown").Node.Value == text.Value && Find("Rendered Markdown").Node.Value == text.Value,
            "Markdown external source changes update layout and native values coherently");
        Focus("Smart Markdown");
        Key(UiKey.Tab);
        check(Find("Rendered Markdown").Node.Focused,
            "Markdown editor participates in the shared Tab order without extra block focus stops");
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
