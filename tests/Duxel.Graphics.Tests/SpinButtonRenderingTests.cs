using Duxel.Core;

internal static class SpinButtonRenderingTests
{
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check,
        Action<UiInputState> setInput, UiContext context, UiFluentColorScheme scheme)
    {
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        var number = new UiState<double?>(4);
        using var spin = new UiSpinButton<UiFluentSpinButtonPresentation>("수량", number.Binding,
            new(Minimum: 0, Maximum: 10, IncrementLabel: "수량 늘리기", DecrementLabel: "수량 줄이기"));
        using var after = Fluent.Button("다음 입력");
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        var clip = new UiRect(0, 0, 320, 240);
        void Draw(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            new UiControlContext(ui).Frame(new(0, 0, 320, 240),
                scheme == UiFluentColorScheme.Light ? new(255, 255, 255) : new(32, 32, 32), default, 0, 0);
            ui.RenderLayoutChild(spin, new(20, 100, 160, spin.Options.Size == UiSpinButtonSize.Small ? 24 : 32), clip);
            ui.RenderLayoutChild(after, new(20, 180, 100, 32), clip);
        }
        UiAccessibilityElement Find(string name)
        {
            UiAccessibilityElement? found = null;
            void Visit(string? id)
            {
                while (id is not null && tree.TryGet(id, out var item))
                {
                    if (item.Node.Name == name)
                        found = item;
                    Visit(item.FirstChild);
                    id = item.Next;
                }
            }
            Visit(tree.Root.First);
            return found ?? throw new InvalidOperationException("Missing SpinButton element: " + name);
        }
        foreach (var size in Enum.GetValues<UiSpinButtonSize>())
            foreach (var appearance in Enum.GetValues<UiInputAppearance>())
            {
                var prefix = size + "-" + appearance;
                spin.Options = spin.Options with
                {
                    Size = size,
                    Appearance = appearance
                };
                setInput(idle);
                var rest = capture(prefix + "-rest", Draw);
                check(rest.AsSpan().SequenceEqual(capture(prefix + "-replay", Draw)), "GPU SpinButton static replay is exact: " + prefix);
                var increment = Find("수량 늘리기");
                check(increment.Node.Bounds.Width == 24 && increment.Node.Bounds.Height == (size == UiSpinButtonSize.Small ? 12 : 16),
                    "GPU SpinButton stepper hit geometry follows its size: " + prefix);
                var p = increment.Node.Bounds;
                var hoverInput = idle with
                {
                    MousePosition = new(p.X + p.Width / 2, p.Y + p.Height / 2)
                };
                setInput(hoverInput);
                var hover = capture(prefix + "-hover", Draw);
                check(!rest.AsSpan().SequenceEqual(hover) && context.State.MouseCursor == UiMouseCursor.Hand,
                    "GPU SpinButton hover paints its real stepper and pointer: " + prefix);
                setInput(hoverInput with
                {
                    LeftMousePressed = true,
                    LeftMouseDown = true
                });
                var pressed = capture(prefix + "-press", Draw);
                check(number.Value == 5 && !hover.AsSpan().SequenceEqual(pressed) && spin.Input.RepeatAt is not null,
                    "GPU SpinButton pointer press commits and paints its held state: " + prefix);
                setInput(idle with
                {
                    LeftMouseReleased = true
                });
                capture(prefix + "-release", Draw);
                var destination = Find("다음 입력");
                tree.RequestAction(destination.Node.Id, destination.RuntimeId, UiAccessibilityAction.Focus, wait: false);
                number.Value = 4;
                setInput(idle);
                capture(prefix + "-settle", Draw);
                var settled = capture(prefix + "-settled", Draw);
                check(spin.Input.RepeatAt is null && context.State.NextScheduledFrameTimeSeconds is null
                    && !context.State.HasActiveAnimations && settled.AsSpan().SequenceEqual(capture(prefix + "-idle-replay", Draw)),
                    "GPU SpinButton release settles without an idle repeat or animation deadline: " + prefix);
            }
        var editor = Find("수량");
        tree.RequestAction(editor.Node.Id, editor.RuntimeId, UiAccessibilityAction.Focus, wait: false);
        setInput(idle);
        capture("editor-focus", Draw);
        setInput(idle with
        {
            KeyEvents = [new(UiKey.A, true, KeyModifiers.Ctrl)],
            Modifiers = KeyModifiers.Ctrl
        });
        capture("editor-select", Draw);
        setInput(idle with
        {
            KeyEvents = [new(UiKey.A, false, default)],
            CharEvents = [new('7')]
        });
        capture("editor-draft", Draw);
        var editorPoint = new UiVector2(editor.Node.Bounds.X + 60, editor.Node.Bounds.Y + 12);
        setInput(idle with
        {
            MousePosition = editorPoint,
            LeftMousePressed = true,
            LeftMouseDown = true
        });
        capture("editor-caret-press", Draw);
        check(number.Value == 4 && spin.DisplayText == "7",
            "GPU SpinButton caret click keeps the actual drawn draft separate from its bound number");
        setInput(idle with
        {
            MousePosition = editorPoint,
            LeftMouseReleased = true
        });
        capture("editor-caret-release", Draw);
        var destinationAfterEdit = Find("다음 입력");
        tree.RequestAction(destinationAfterEdit.Node.Id, destinationAfterEdit.RuntimeId, UiAccessibilityAction.Focus, wait: false);
        setInput(idle);
        capture("editor-blur-commit", Draw);
        check(number.Value == 7 && Find("수량").Node.Range?.Value == 7,
            "GPU SpinButton leaving the actual drawn editor commits its draft and native numeric range");
        number.Value = 4;
        capture("editor-reset", Draw);
        spin.Options = spin.Options with
        {
            ReadOnly = true
        };
        var up = Find("수량 늘리기").Node.Bounds;
        setInput(idle with
        {
            MousePosition = new(up.X + 12, up.Y + 4),
            LeftMousePressed = true,
            LeftMouseDown = true
        });
        capture("readonly-press", Draw);
        check(number.Value == 4 && !Find("수량 늘리기").Node.Enabled && context.State.MouseCursor == UiMouseCursor.NotAllowed,
            "GPU SpinButton read-only steppers remain disabled under pointer input");
        clip = default;
        spin.Options = spin.Options with
        {
            ReadOnly = false
        };
        setInput(idle with
        {
            MousePosition = new(up.X + 12, up.Y + 4)
        });
        capture("fully-clipped", Draw);
        check(context.State.MouseCursor == UiMouseCursor.Arrow && Find("수량").Node.Offscreen,
            "GPU SpinButton clipped geometry does not expose a hover target");
        context.SetAccessibilityTree(null);
        setInput(idle);
    }
}
