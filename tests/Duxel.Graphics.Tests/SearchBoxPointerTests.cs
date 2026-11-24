using Duxel.Core;

internal static class SearchBoxPointerTests
{
    internal static void Run(Action<bool, string> check)
    {
        foreach (var size in Enum.GetValues<UiSearchBoxSize>())
        {
            foreach (var rtl in new[] { false, true })
            {
                RunCase(check, size, rtl, onPress: false);
                RunCase(check, size, rtl, onPress: true);
            }
        }
    }

    private static void RunCase(Action<bool, string> check, UiSearchBoxSize size, bool rtl, bool onPress)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        var text = "정세일";
        var writes = 0;
        var reject = false;
        var binding = new UiBinding<string>(() => text, value =>
        {
            writes++;
            if (!reject)
                text = value;
        });
        var options = new UiSearchBoxOptions(Size: size, RightToLeft: rtl);
        using UiWidget search = onPress
            ? new UiSearchBox<UiPressButtonBehavior, UiFluentSearchBoxPresentation>("Held search", binding, options: options)
            : Fluent.SearchBox("Held search", binding, options: options);
        using var after = Fluent.Button("After held search");
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(UiInputState? input = null)
        {
            context.NewFrame(new(1f / 60, new(640, 480), new(1, 1)), input ?? idle, new(0, 0, 640, 480), UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding: 0); search.Render(ui); after.Render(ui); }));
            using var data = context.GetDrawData();
        }
        UiAccessibilityElement Find(string name)
        {
            UiAccessibilityElement? found = null;
            void Visit(string? id)
            {
                while (id is not null && tree.TryGet(id, out var element))
                {
                    if (element.Node.Name == name)
                        found = element;
                    Visit(element.FirstChild);
                    id = element.Next;
                }
            }
            Visit(tree.Root.First);
            return found ?? throw new InvalidOperationException("Held search fixture missing : " + name);
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
        var label = onPress ? $"Press/{size}/{rtl}" : $"{size}/{rtl}";
        Frame();
        var editor = Find("Held search");
        if (tree.RequestAction(editor.Node.Id, editor.RuntimeId, UiAccessibilityAction.Focus, wait: false) != UiAccessibilityResult.Success)
            throw new InvalidOperationException("Held search focus request failed.");
        Frame();
        var clearButton = Find("Clear search");
        var clear = clearButton.Node.Bounds;
        var pointer = idle with
        {
            MousePosition = new(clear.X + clear.Width / 2, clear.Y + clear.Height / 2)
        };
        if (onPress)
        {
            Frame(pointer with { LeftMouseDown = true, LeftMousePressed = true });
            check(text == "" && writes == 1 && Find("Held search").Node.Focused
                && context.State.ActiveId == clearButton.Node.Id,
                "Press clears immediately while the button retains pointer ownership : " + label);
            var editPoint = new UiVector2(editor.Node.Bounds.X + editor.Node.Bounds.Width / 2,
                editor.Node.Bounds.Y + editor.Node.Bounds.Height / 2);
            Frame(idle with { MousePosition = editPoint, LeftMouseDown = true });
            check(text == "" && writes == 1 && context.State.ActiveId == clearButton.Node.Id,
                "Press clear keeps its capture when dragged over the editor : " + label);
            Frame(idle with { MousePosition = editPoint, LeftMouseReleased = true });
            check(text == "" && writes == 1 && context.State.ActiveId == editor.Node.Id,
                "Press clear releases capture back to the editor without a second edit : " + label);
        }
        else
        {
            Frame(pointer with { LeftMouseDown = true, LeftMousePressed = true });
            Frame(pointer with { LeftMouseDown = true });
            Frame(idle with { LeftMouseReleased = true });
            check(text == "정세일" && writes == 0, "Dragging out of clear and releasing cancels the action : " + label);
            Frame(pointer with { LeftMouseDown = true, LeftMousePressed = true });
            for (var held = 0; held < 4; held++)
                Frame(pointer with { LeftMouseDown = true });
            check(Find("Held search").Node.Focused && text == "정세일" && writes == 0,
                "Clear button retains editor focus without clearing while held across frames : " + label);
            Frame(pointer with { LeftMouseReleased = true });
            check(text == "" && Find("Held search").Node.Value == "" && writes == 1 && Find("Held search").Node.Focused,
                "Held clear press-release clears the authoritative model exactly once : " + label);
        }
        Key(UiKey.Z, KeyModifiers.Ctrl);
        check(text == "정세일", "Held clear remains one undoable edit : " + label);
        Key(UiKey.Y, KeyModifiers.Ctrl);
        check(text == "", "Held clear redo restores the accepted empty value : " + label);
        Frame(idle with
        {
            CharEvents = [new(0xAC00)]
        });
        check(text == "가", "Korean typing resumes at the empty caret after held clear : " + label);
        if (onPress)
        {
            var count = writes;
            Frame(pointer with { LeftMousePressed = true, LeftMouseReleased = true });
            check(text == "" && writes == count + 1 && context.State.ActiveId == editor.Node.Id,
                "Press clear handles both pointer edges in one frame : " + label);
            Key(UiKey.Z, KeyModifiers.Ctrl);
            reject = true;
            count = writes;
            Frame(pointer with { LeftMouseDown = true, LeftMousePressed = true });
            Frame(pointer with { LeftMouseReleased = true });
            check(text == "가" && Find("Held search").Node.Value == "가" && writes == count + 1,
                "Press clear reads back a rejected edit without retrying on release : " + label);
            reject = false;
            clearButton = Find("Clear search");
            check(tree.RequestAction(clearButton.Node.Id, clearButton.RuntimeId, UiAccessibilityAction.Invoke, wait: false)
                == UiAccessibilityResult.Success, "Press clear still accepts accessibility Invoke : " + label);
            Frame();
            check(text == "" && Find("Held search").Node.Focused, "Press clear preserves accessible invocation and focus : " + label);
        }
        Key(UiKey.Tab);
        check(Find("After held search").Node.Focused, "Tab still skips the clear action after a held click : " + label);
    }

    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
