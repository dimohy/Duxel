using Duxel.Core;

internal static class MarkdownPointerTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        const string original = "# 한글 Markdown\n\n본문의 **굵은 글자**를 편집합니다.";
        var text = new UiState<string>(original);
        using var editor = Dux.MarkdownEditor("Pointer Markdown", text.Binding, new(Width: 420, Height: 240));
        var mirror = new UiVisualTextLines();
        var mirrorSource = new UiMarkdownTextSource(new(original));
        var target = original.IndexOf("Markdown", StringComparison.Ordinal) + 1;
        var point = default(UiVector2);
        var computePoint = true;
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(UiInputState? input = null)
        {
            context.NewFrame(new(1f / 60, new(640, 360), new(1, 1)), input ?? idle, new(0, 0, 640, 360), UiTextSettings.Default);
            context.Render(new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding: 0);
                ui.SetCursorScreenPos(new(10, 20));
                editor.Render(ui);
                if (computePoint)
                {
                    mirror.Prepare(ui, original, 396, 220, 20, 0, styled: mirrorSource);
                    var line = mirror.FindLine(target);
                    point = new(22 + mirror.X(target, line), 30 + mirror.Y(line) + mirror.LineHeight(line) / 2);
                    computePoint = false;
                }
            }));
            using var draw = context.GetDrawData();
        }
        Frame();
        tree.TryGet(tree.Root.First!, out var node);
        Frame(idle with
        {
            MousePosition = point,
            LeftMouseDown = true,
            LeftMousePressed = true
        });
        Frame(idle with
        {
            MousePosition = point,
            LeftMouseDown = true
        });
        Frame(idle with
        {
            MousePosition = point,
            LeftMouseDown = true
        });
        Frame(idle with
        {
            MousePosition = point,
            LeftMouseDown = true
        });
        check(!context.State.GetSelection(node.Node.Id).HasSelection && context.State.GetCursor(node.Node.Id, 0) == target,
            "Markdown stationary held click retains the clicked source caret without creating a selection");
        Frame(idle with
        {
            MousePosition = point,
            LeftMouseReleased = true
        });
        check(!context.State.GetSelection(node.Node.Id).HasSelection && context.State.GetCursor(node.Node.Id, 0) == target,
            "Markdown source reveal on click completion preserves the same logical caret");
        Frame(idle with
        {
            CharEvents = [new((uint)'X')]
        });
        check(text.Value == original.Insert(target, "X"),
            "Markdown typing after source reveal inserts at the clicked character rather than replacing an accidental selection");
        void ResetPreview()
        {
            text.Value = original;
            Frame();
            context.State.AdvanceTime(.6f);
            Frame(idle with
            {
                MousePosition = new(600, 330),
                LeftMouseDown = true,
                LeftMousePressed = true
            });
            Frame(idle with
            {
                MousePosition = new(600, 330),
                LeftMouseReleased = true
            });
            computePoint = true;
            Frame();
        }
        ResetPreview();
        var moved = point with
        {
            X = point.X + 38
        };
        var expectedEnd = mirror.Hit(moved.X - 22, moved.Y - 30, 20);
        Frame(idle with
        {
            MousePosition = point,
            LeftMouseDown = true,
            LeftMousePressed = true
        });
        Frame(idle with
        {
            MousePosition = moved,
            LeftMouseDown = true
        });
        Frame(idle with
        {
            MousePosition = moved,
            LeftMouseDown = true
        });
        var selected = context.State.GetSelection(node.Node.Id);
        check(selected.Start == target && selected.End == expectedEnd && selected.HasSelection,
            "Markdown genuine pointer movement still selects the source range from the original display map");
        Frame(idle with
        {
            MousePosition = moved,
            LeftMouseReleased = true
        });
        check(context.State.GetSelection(node.Node.Id) == selected,
            "Markdown revealing the selected block after drag completion preserves both selection endpoints");
        Frame(idle with
        {
            CharEvents = [new((uint)'Y')]
        });
        check(text.Value == original.Remove(target, expectedEnd - target).Insert(target, "Y"),
            "Markdown typing replaces only the genuinely dragged source range");
        ResetPreview();
        Frame(idle with
        {
            MousePosition = point,
            LeftMousePressed = true,
            LeftMouseReleased = true
        });
        check(!context.State.GetSelection(node.Node.Id).HasSelection && context.State.GetCursor(node.Node.Id, 0) == target,
            "Markdown press and release in one input frame still hit-test the original rendered text");
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
